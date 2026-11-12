namespace GhostAndroid.Core.Models;

/// <summary>
/// Informations de sécurité/verrouillage détectées sur l'appareil.
/// GHOST ANDROID DIAGNOSTIQUE l'état de sécurité ; il ne contourne jamais une
/// protection du fabricant ni un verrou d'écran. La seule action de reprise d'accès
/// proposée est une réinitialisation d'usine AUTORISÉE, qui efface les données.
/// </summary>
public sealed class LockInfo
{
    /// <summary>Type de verrouillage d'écran détecté (ex: PIN, motif, mot de passe, aucun).</summary>
    public ScreenLockKind ScreenLock { get; set; } = ScreenLockKind.Unknown;

    /// <summary>Protection de réinitialisation (FRP / compte Google) active, si déterminable.</summary>
    public bool? FactoryResetProtectionActive { get; set; }

    /// <summary>Chiffrement du stockage actif, si déterminable.</summary>
    public bool? StorageEncrypted { get; set; }

    /// <summary>Verrouillage OEM / bootloader autorisé dans les options développeur, si connu.</summary>
    public bool? OemUnlockAllowed { get; set; }

    /// <summary>Notes de diagnostic lisibles.</summary>
    public List<string> Notes { get; set; } = new();
}

public enum ScreenLockKind
{
    Unknown = 0,
    None,
    Swipe,
    Pin,
    Pattern,
    Password,
    BiometricBacked
}
