using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Models;

namespace GhostAndroid.Core.Security;

/// <summary>
/// Diagnostique l'état de sécurité d'un appareil EN LECTURE SEULE. GHOST ANDROID ne
/// contourne jamais un verrou d'écran ni une protection du fabricant (FRP). Lorsqu'une
/// information ne peut pas être obtenue sans privilèges (ex: type exact de verrou
/// d'écran), elle est honnêtement marquée inconnue.
/// </summary>
public sealed class LockDiagnosticService
{
    private readonly IAdbEngine _adb;

    public LockDiagnosticService(IAdbEngine adb) => _adb = adb;

    public async Task<LockInfo> InspectAsync(AndroidDevice device, CancellationToken ct = default)
    {
        var info = new LockInfo();

        // Chiffrement du stockage (ro.crypto.state : encrypted / unencrypted / unsupported).
        var crypto = await _adb.GetPropertyAsync(device.Serial, "ro.crypto.state", ct).ConfigureAwait(false);
        if (crypto is not null)
        {
            info.StorageEncrypted = crypto.Trim().Equals("encrypted", StringComparison.OrdinalIgnoreCase);
            info.Notes.Add($"État du chiffrement du stockage : {crypto.Trim()}.");
        }

        // Déverrouillage OEM autorisé (options développeur).
        var oem = await _adb.GetPropertyAsync(device.Serial, "sys.oem_unlock_allowed", ct).ConfigureAwait(false);
        if (oem is not null)
        {
            info.OemUnlockAllowed = oem.Trim() == "1";
            info.Notes.Add($"Déverrouillage OEM autorisé : {(info.OemUnlockAllowed == true ? "oui" : "non")}.");
        }
        else
        {
            info.Notes.Add("Déverrouillage OEM : état non déterminé.");
        }

        // Type de verrou d'écran : non lisible de façon fiable sans privilèges.
        info.ScreenLock = ScreenLockKind.Unknown;
        info.Notes.Add("Type de verrou d'écran : non lisible via ADB standard (nécessiterait un accès privilégié). " +
                       "GHOST ANDROID ne tente aucun contournement de l'écran de verrouillage.");

        // Protection de réinitialisation (FRP) : non déterminable sans privilèges.
        info.FactoryResetProtectionActive = null;
        info.Notes.Add("Protection de réinitialisation (FRP) : état non déterminé ; aucune tentative de contournement n'est effectuée.");

        return info;
    }
}
