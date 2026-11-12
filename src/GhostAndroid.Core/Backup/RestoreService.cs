using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using GhostAndroid.Core.Models;
using GhostAndroid.Core.Security;

namespace GhostAndroid.Core.Backup;

public sealed class IntegrityIssue
{
    public required string RelativePath { get; init; }
    public required string Problem { get; init; }
}

public sealed class RestoreResult
{
    public bool Success { get; init; }
    public BackupManifest? Manifest { get; init; }
    public int FilesExtracted { get; init; }
    public List<IntegrityIssue> IntegrityIssues { get; init; } = new();
    public string? Error { get; init; }
    public bool IntegrityVerified => IntegrityIssues.Count == 0;
}

/// <summary>
/// Restaure/vérifie une archive .ghostbak : déchiffre (AES-256-GCM), extrait le ZIP
/// et contrôle l'intégrité en recalculant le SHA-256 de chaque fichier par rapport au
/// manifeste. Toute incohérence est signalée (jamais masquée).
/// </summary>
public sealed class RestoreService
{
    private readonly GcmFileCrypto _crypto;

    public RestoreService(GcmFileCrypto? crypto = null) => _crypto = crypto ?? new GcmFileCrypto();

    /// <summary>Lit uniquement le manifeste sans tout extraire.</summary>
    public async Task<BackupManifest?> ReadManifestAsync(string archiveFile, string password, CancellationToken ct = default)
    {
        await using var zipStream = await DecryptToTempAsync(archiveFile, password, ct).ConfigureAwait(false);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = zip.GetEntry(BackupService.ManifestEntryName);
        if (entry is null) return null;
        await using var s = entry.Open();
        return await JsonSerializer.DeserializeAsync<BackupManifest>(s, cancellationToken: ct).ConfigureAwait(false);
    }

    /// <summary>Extrait l'archive vers un dossier et vérifie l'intégrité.</summary>
    public async Task<RestoreResult> ExtractArchiveAsync(
        string archiveFile,
        string destinationDir,
        string password,
        CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(destinationDir);
            await using var zipStream = await DecryptToTempAsync(archiveFile, password, ct).ConfigureAwait(false);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

            BackupManifest? manifest = null;
            var extracted = 0;

            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.FullName == BackupService.ManifestEntryName)
                {
                    await using var ms = entry.Open();
                    manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(ms, cancellationToken: ct).ConfigureAwait(false);
                    continue;
                }
                if (string.IsNullOrEmpty(entry.Name)) continue; // dossier

                var targetPath = SafeCombine(destinationDir, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await using (var outStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var entryStream = entry.Open())
                {
                    await entryStream.CopyToAsync(outStream, ct).ConfigureAwait(false);
                }
                extracted++;
            }

            var issues = manifest is null
                ? new List<IntegrityIssue>()
                : await VerifyIntegrityAsync(destinationDir, manifest, ct).ConfigureAwait(false);

            return new RestoreResult
            {
                Success = true,
                Manifest = manifest,
                FilesExtracted = extracted,
                IntegrityIssues = issues,
            };
        }
        catch (Exception ex)
        {
            return new RestoreResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>Vérifie l'intégrité des fichiers extraits via SHA-256 du manifeste.</summary>
    public static async Task<List<IntegrityIssue>> VerifyIntegrityAsync(
        string folder, BackupManifest manifest, CancellationToken ct = default)
    {
        var issues = new List<IntegrityIssue>();
        foreach (var entry in manifest.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var path = SafeCombine(folder, entry.RelativePath);
            if (!File.Exists(path))
            {
                issues.Add(new IntegrityIssue { RelativePath = entry.RelativePath, Problem = "Fichier manquant après extraction." });
                continue;
            }
            if (entry.Sha256 is null) continue;

            await using var fs = File.OpenRead(path);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false)).ToLowerInvariant();
            if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                issues.Add(new IntegrityIssue { RelativePath = entry.RelativePath, Problem = "Empreinte SHA-256 différente du manifeste." });
        }
        return issues;
    }

    private async Task<Stream> DecryptToTempAsync(string archiveFile, string password, CancellationToken ct)
    {
        if (!File.Exists(archiveFile)) throw new FileNotFoundException("Archive introuvable.", archiveFile);
        var temp = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None,
            4096, FileOptions.DeleteOnClose);
        await using (var input = File.OpenRead(archiveFile))
        {
            await _crypto.DecryptAsync(input, temp, password, ct).ConfigureAwait(false);
        }
        temp.Position = 0;
        return temp;
    }

    /// <summary>Empêche la traversée de chemin (zip slip).</summary>
    private static string SafeCombine(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root);
        var combined = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!combined.StartsWith(fullRoot, StringComparison.Ordinal))
            throw new IOException($"Chemin d'archive non sûr : {relative}");
        return combined;
    }
}
