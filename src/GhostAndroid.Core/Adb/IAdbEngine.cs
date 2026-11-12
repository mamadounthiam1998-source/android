using GhostAndroid.Core.Models;

namespace GhostAndroid.Core.Adb;

/// <summary>Moteur ADB : enveloppe réelle et typée autour de l'exécutable adb officiel.</summary>
public interface IAdbEngine
{
    /// <summary>true si adb a été localisé sur le système.</summary>
    bool IsAvailable { get; }

    /// <summary>Chemin résolu vers adb, ou null.</summary>
    string? AdbPath { get; }

    /// <summary>Version renvoyée par `adb version`.</summary>
    Task<string?> GetVersionAsync(CancellationToken ct = default);

    /// <summary>Liste les appareils via `adb devices -l` (sans enrichissement).</summary>
    Task<IReadOnlyList<AndroidDevice>> ListDevicesAsync(CancellationToken ct = default);

    /// <summary>Récupère toutes les propriétés système via getprop.</summary>
    Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(string serial, CancellationToken ct = default);

    /// <summary>Lit une propriété unique.</summary>
    Task<string?> GetPropertyAsync(string serial, string key, CancellationToken ct = default);

    /// <summary>Niveau de batterie (0-100) ou null.</summary>
    Task<int?> GetBatteryLevelAsync(string serial, CancellationToken ct = default);

    /// <summary>Exécute une commande shell arbitraire et renvoie le résultat brut.</summary>
    Task<CommandResult> ShellAsync(string serial, string command, CancellationToken ct = default);

    /// <summary>Liste les paquets installés (pm list packages). thirdPartyOnly = -3.</summary>
    Task<IReadOnlyList<string>> ListPackagesAsync(string serial, bool thirdPartyOnly, CancellationToken ct = default);

    /// <summary>Installe un APK. Vérifie l'existence du fichier avant d'agir.</summary>
    Task<CommandResult> InstallApkAsync(string serial, string apkPath, bool allowDowngrade = false, CancellationToken ct = default);

    /// <summary>Désinstalle un paquet.</summary>
    Task<CommandResult> UninstallAsync(string serial, string packageName, bool keepData = false, CancellationToken ct = default);

    /// <summary>Copie un fichier/dossier de l'appareil vers le PC.</summary>
    Task<CommandResult> PullAsync(string serial, string devicePath, string localPath, CancellationToken ct = default);

    /// <summary>Copie un fichier/dossier du PC vers l'appareil.</summary>
    Task<CommandResult> PushAsync(string serial, string localPath, string devicePath, CancellationToken ct = default);

    /// <summary>Redémarre l'appareil. mode: null (normal), "recovery", "bootloader", "sideload".</summary>
    Task<CommandResult> RebootAsync(string serial, string? mode = null, CancellationToken ct = default);

    /// <summary>Vidage logcat (-d) courant.</summary>
    Task<CommandResult> LogcatDumpAsync(string serial, int maxLines = 2000, CancellationToken ct = default);

    /// <summary>Démarre le démon adb.</summary>
    Task<CommandResult> StartServerAsync(CancellationToken ct = default);

    /// <summary>Arrête le démon adb.</summary>
    Task<CommandResult> KillServerAsync(CancellationToken ct = default);
}
