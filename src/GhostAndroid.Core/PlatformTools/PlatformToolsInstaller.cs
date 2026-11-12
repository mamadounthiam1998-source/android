using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GhostAndroid.Core.PlatformTools;

public sealed class ToolInstallProgress
{
    public string Stage { get; init; } = "";
    public int Percent { get; init; }
}

/// <summary>
/// Télécharge et installe les Android Platform Tools OFFICIELS de Google si adb est
/// absent. Les outils sont déposés dans %AppData%/GhostAndroid/platform-tools, où le
/// localisateur les trouve ensuite. Rend l'application autonome : plus besoin
/// d'installer adb séparément. Le téléchargement s'effectue depuis les serveurs
/// officiels de Google uniquement.
/// </summary>
public sealed class PlatformToolsInstaller
{
    // URLs officielles Google (sources sûres).
    private const string WindowsUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";
    private const string LinuxUrl = "https://dl.google.com/android/repository/platform-tools-latest-linux.zip";
    private const string MacUrl = "https://dl.google.com/android/repository/platform-tools-latest-darwin.zip";

    private readonly ILogger<PlatformToolsInstaller> _logger;

    public PlatformToolsInstaller(ILogger<PlatformToolsInstaller>? logger = null)
        => _logger = logger ?? NullLogger<PlatformToolsInstaller>.Instance;

    public static string DownloadUrl => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? WindowsUrl
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? MacUrl : LinuxUrl;

    /// <summary>
    /// S'assure qu'adb est disponible. S'il manque, télécharge et installe les
    /// Platform Tools. Renvoie le chemin d'adb, ou lève une exception explicite.
    /// </summary>
    public async Task<string> EnsureInstalledAsync(
        IProgress<ToolInstallProgress>? progress = null, CancellationToken ct = default)
    {
        var locator = new PlatformToolsLocator();
        var existing = locator.FindAdb();
        if (existing is not null) return existing;

        var managedDir = PlatformToolsLocator.ManagedToolsDirectory;        // .../GhostAndroid/platform-tools
        var parentDir = Path.GetDirectoryName(managedDir)!;                 // .../GhostAndroid
        Directory.CreateDirectory(parentDir);

        var tempZip = Path.Combine(Path.GetTempPath(), $"platform-tools-{Guid.NewGuid():N}.zip");
        try
        {
            progress?.Report(new ToolInstallProgress { Stage = "Téléchargement des Android Platform Tools (Google)…", Percent = 0 });
            await DownloadAsync(DownloadUrl, tempZip, progress, ct).ConfigureAwait(false);

            progress?.Report(new ToolInstallProgress { Stage = "Extraction…", Percent = 92 });
            // Le zip contient un dossier racine « platform-tools/ ».
            if (Directory.Exists(managedDir))
                Directory.Delete(managedDir, recursive: true);
            ZipFile.ExtractToDirectory(tempZip, parentDir, overwriteFiles: true);

            var adb = Path.Combine(managedDir, PlatformToolsLocator.AdbFileName);
            if (!File.Exists(adb))
                throw new InvalidOperationException("Installation terminée mais adb est introuvable dans l'archive extraite.");

            // Sur Unix, s'assurer que les binaires sont exécutables.
            if (!PlatformToolsLocator.IsWindows)
                TryMakeExecutable(managedDir);

            progress?.Report(new ToolInstallProgress { Stage = "Terminé.", Percent = 100 });
            _logger.LogInformation("Platform Tools installés dans {Dir}", managedDir);
            return adb;
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* best effort */ }
        }
    }

    private static async Task DownloadAsync(string url, string destination, IProgress<ToolInstallProgress>? progress, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GhostAndroid/0.1");

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;
            if (total > 0)
                progress?.Report(new ToolInstallProgress
                {
                    Stage = "Téléchargement des Android Platform Tools (Google)…",
                    Percent = (int)Math.Min(90, received * 90 / total)
                });
        }
    }

    private static void TryMakeExecutable(string dir)
    {
        try
        {
            foreach (var name in new[] { "adb", "fastboot" })
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
#pragma warning disable CA1416
                    var mode = File.GetUnixFileMode(path);
                    File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
#pragma warning restore CA1416
                }
            }
        }
        catch { /* best effort */ }
    }
}
