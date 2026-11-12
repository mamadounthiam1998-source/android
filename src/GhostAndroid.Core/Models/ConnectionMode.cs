namespace GhostAndroid.Core.Models;

/// <summary>
/// Mode de connexion sous lequel un appareil Android est actuellement détecté.
/// </summary>
public enum ConnectionMode
{
    /// <summary>Indéterminé / aucun appareil.</summary>
    Unknown = 0,

    /// <summary>Appareil accessible via ADB en mode système (device).</summary>
    Adb,

    /// <summary>Appareil en mode recovery (adb de recovery).</summary>
    Recovery,

    /// <summary>Appareil en mode sideload (adb sideload).</summary>
    Sideload,

    /// <summary>Appareil en mode bootloader / Fastboot.</summary>
    Fastboot,

    /// <summary>Appareil détecté mais non autorisé (clé RSA ADB non acceptée).</summary>
    Unauthorized,

    /// <summary>Appareil listé mais hors ligne.</summary>
    Offline
}
