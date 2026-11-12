using System.Runtime.InteropServices;

namespace GhostAndroid.Core.PlatformTools;

/// <summary>
/// Localise les exécutables adb et fastboot de façon déterministe.
/// Ordre de recherche : chemin explicite fourni → dossier local de l'application
/// (tools/platform-tools) → variable d'environnement PATH → emplacements SDK courants.
/// Ne renvoie jamais un chemin inexistant.
/// </summary>
public sealed class PlatformToolsLocator
{
    private readonly string? _explicitDirectory;

    public PlatformToolsLocator(string? explicitDirectory = null)
        => _explicitDirectory = explicitDirectory;

    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static string AdbFileName => IsWindows ? "adb.exe" : "adb";
    public static string FastbootFileName => IsWindows ? "fastboot.exe" : "fastboot";

    /// <summary>Chemin complet vers adb, ou null si introuvable.</summary>
    public string? FindAdb() => Find(AdbFileName);

    /// <summary>Chemin complet vers fastboot, ou null si introuvable.</summary>
    public string? FindFastboot() => Find(FastbootFileName);

    public bool AdbAvailable => FindAdb() is not null;
    public bool FastbootAvailable => FindFastboot() is not null;

    private string? Find(string executable)
    {
        foreach (var dir in CandidateDirectories())
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var candidate = Path.Combine(dir, executable);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>Énumère, dans l'ordre de priorité, les dossiers où chercher les outils.</summary>
    public IEnumerable<string> CandidateDirectories()
    {
        if (!string.IsNullOrWhiteSpace(_explicitDirectory))
            yield return _explicitDirectory!;

        // Dossier local embarqué avec l'application installée.
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "tools", "platform-tools");
        yield return Path.Combine(baseDir, "platform-tools");
        yield return baseDir;

        // PATH
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (var p in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                yield return p.Trim();
        }

        // Emplacements SDK Android courants.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var sdkRoots = new[]
        {
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            string.IsNullOrEmpty(localAppData) ? null : Path.Combine(localAppData, "Android", "Sdk"),
            string.IsNullOrEmpty(home) ? null : Path.Combine(home, "Android", "Sdk"),
            string.IsNullOrEmpty(home) ? null : Path.Combine(home, "Library", "Android", "sdk"),
        };

        foreach (var root in sdkRoots)
        {
            if (!string.IsNullOrWhiteSpace(root))
                yield return Path.Combine(root!, "platform-tools");
        }
    }
}
