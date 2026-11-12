using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Models;
using GhostAndroid.Core.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GhostAndroid.Core.Backup;

public sealed class BackupProgress
{
    public string Stage { get; init; } = "";
    public int Percent { get; init; }
    public string? CurrentItem { get; init; }
}

public sealed class BackupResult
{
    public required string FilePath { get; init; }
    public required BackupManifest Manifest { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Crée des sauvegardes chiffrées. Deux niveaux :
///  • <see cref="ArchiveFolderAsync"/> : dossier local → .ghostbak chiffré (sans ADB, testable).
///  • <see cref="BackupDeviceAsync"/> : tire les données de l'appareil via ADB puis archive.
/// </summary>
public sealed class BackupService
{
    public const string ManifestEntryName = "__ghost_manifest__.json";
    public const string FileExtension = ".ghostbak";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IAdbEngine _adb;
    private readonly GcmFileCrypto _crypto;
    private readonly ILogger<BackupService> _logger;

    public BackupService(IAdbEngine adb, GcmFileCrypto? crypto = null, ILogger<BackupService>? logger = null)
    {
        _adb = adb;
        _crypto = crypto ?? new GcmFileCrypto();
        _logger = logger ?? NullLogger<BackupService>.Instance;
    }

    /// <summary>
    /// Sauvegarde un appareil : tire chaque source sélectionnée via `adb pull` vers un
    /// dossier temporaire, construit le manifeste puis produit l'archive chiffrée.
    /// </summary>
    public async Task<BackupResult> BackupDeviceAsync(
        AndroidDevice device,
        IReadOnlyList<BackupSource> sources,
        string outputFile,
        string password,
        IProgress<BackupProgress>? progress = null,
        CancellationToken ct = default)
    {
        var selected = sources.Where(s => s.Selected).ToList();
        if (selected.Count == 0)
            return new BackupResult { FilePath = outputFile, Manifest = new(), Success = false, Error = "Aucune source sélectionnée." };

        var staging = Directory.CreateTempSubdirectory("ghost_backup_");
        try
        {
            var pulled = 0;
            foreach (var source in selected)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new BackupProgress
                {
                    Stage = "Extraction depuis l'appareil",
                    Percent = (int)(pulled / (double)selected.Count * 60),
                    CurrentItem = source.Label
                });

                var dest = Path.Combine(staging.FullName, source.Category.ToString(), SafeName(source.DevicePath));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                var result = await _adb.PullAsync(device.Serial, source.DevicePath, dest, ct).ConfigureAwait(false);
                if (!result.Success)
                    _logger.LogWarning("Échec/absence du dossier {Path}: {Err}", source.DevicePath, result.StandardError);

                pulled++;
            }

            var manifest = new BackupManifest
            {
                DeviceSerial = device.Serial,
                DeviceModel = device.DisplayName,
                AndroidVersion = device.AndroidVersion,
                IncludedCategories = selected.Select(s => s.Label).Distinct().ToList(),
                NotIncludedNotes = BackupSources.KnownLimitations.ToList(),
            };

            progress?.Report(new BackupProgress { Stage = "Compression et chiffrement", Percent = 70 });
            await ArchiveFolderAsync(staging.FullName, outputFile, password, manifest, progress, ct).ConfigureAwait(false);

            progress?.Report(new BackupProgress { Stage = "Terminé", Percent = 100 });
            return new BackupResult { FilePath = outputFile, Manifest = manifest, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la sauvegarde de {Serial}", device.Serial);
            return new BackupResult { FilePath = outputFile, Manifest = new(), Success = false, Error = ex.Message };
        }
        finally
        {
            TryDeleteDirectory(staging.FullName);
        }
    }

    /// <summary>
    /// Archive un dossier local en .ghostbak chiffré. Calcule le SHA-256 de chaque
    /// fichier pour la vérification d'intégrité. N'utilise pas ADB → testable.
    /// </summary>
    public async Task ArchiveFolderAsync(
        string sourceFolder,
        string outputFile,
        string password,
        BackupManifest manifest,
        IProgress<BackupProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(sourceFolder))
            throw new DirectoryNotFoundException(sourceFolder);

        // 1) Construire le ZIP en mémoire temporaire (fichier temp).
        var tempZip = Path.GetTempFileName();
        try
        {
            var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories).ToList();
            manifest.Entries = new List<BackupEntry>(files.Count);

            using (var zipStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var i = 0;
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var rel = Path.GetRelativePath(sourceFolder, file).Replace('\\', '/');

                    var entry = zip.CreateEntry(rel, CompressionLevel.Optimal);
                    string sha;
                    long size;
                    await using (var entryStream = entry.Open())
                    await using (var input = File.OpenRead(file))
                    {
                        sha = await CopyAndHashAsync(input, entryStream, ct).ConfigureAwait(false);
                        size = input.Length;
                    }

                    manifest.Entries.Add(new BackupEntry { RelativePath = rel, SizeBytes = size, Sha256 = sha });

                    i++;
                    if (files.Count > 0)
                        progress?.Report(new BackupProgress
                        {
                            Stage = "Compression et chiffrement",
                            Percent = 70 + (int)(i / (double)files.Count * 20),
                            CurrentItem = rel
                        });
                }

                // Manifeste écrit en dernier, après calcul des entrées.
                var manifestEntry = zip.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                await using var ms = manifestEntry.Open();
                await JsonSerializer.SerializeAsync(ms, manifest, JsonOptions, ct).ConfigureAwait(false);
            }

            // 2) Chiffrer le ZIP → fichier de sortie.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFile))!);
            await using var plainZip = new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.None);
            await using var encrypted = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await _crypto.EncryptAsync(plainZip, encrypted, password, ct).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteFile(tempZip);
        }
    }

    private static async Task<string> CopyAndHashAsync(Stream input, Stream output, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static string SafeName(string devicePath) =>
        string.Concat(devicePath.Split(Path.GetInvalidFileNameChars())).Trim('/', '_');

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { /* best effort */ }
    }
}
