namespace GhostAndroid.Core.Commands;

public enum CommandRisk
{
    /// <summary>Lecture seule / sans effet destructeur connu.</summary>
    Safe = 0,
    /// <summary>Modifie l'appareil mais réversible ou peu risqué.</summary>
    Caution,
    /// <summary>Potentiellement destructeur (perte de données, modification de partition, déverrouillage).</summary>
    Destructive
}

public readonly record struct CommandClassification(CommandRisk Risk, string Reason);

/// <summary>
/// Classe le niveau de risque d'une commande adb/fastboot avant exécution.
/// Toute commande destructive DOIT faire l'objet d'une confirmation explicite de
/// l'utilisateur dans l'interface. Fonction pure, entièrement testable.
/// </summary>
public static class CommandGuard
{
    // Motifs clairement destructeurs (perte de données / sécurité).
    private static readonly string[] DestructivePatterns =
    {
        "format ", "erase", "wipe", " -w", "flash ", "flashall",
        "oem unlock", "flashing unlock", "flashing lock", "oem lock",
        "rm -rf", "rm -r", "mke2fs", "make_f2fs", " dd ",
        "fastboot update", "sideload", "cmd recovery", "--wipe-data",
        "content delete", "pm clear", "set-verity", "disable-verity",
    };

    // Motifs à manier avec précaution (modifient l'appareil, réversibles).
    private static readonly string[] CautionPatterns =
    {
        "uninstall", "install", "push ", "reboot bootloader", "reboot-bootloader",
        "reboot recovery", "pm disable", "pm enable", "pm grant", "pm revoke",
        "settings put", "svc ", "setprop",
    };

    public static CommandClassification Classify(string fileName, string arguments)
    {
        var haystack = ((fileName ?? string.Empty) + " " + (arguments ?? string.Empty)).ToLowerInvariant();

        foreach (var p in DestructivePatterns)
        {
            if (haystack.Contains(p, StringComparison.Ordinal))
                return new CommandClassification(CommandRisk.Destructive,
                    $"Opération potentiellement destructive détectée (« {p.Trim()} »). Confirmation requise.");
        }

        foreach (var p in CautionPatterns)
        {
            if (haystack.Contains(p, StringComparison.Ordinal))
                return new CommandClassification(CommandRisk.Caution,
                    $"Opération modifiant l'appareil (« {p.Trim()} »).");
        }

        return new CommandClassification(CommandRisk.Safe, "Commande de lecture/diagnostic.");
    }

    public static bool RequiresConfirmation(string fileName, string arguments)
        => Classify(fileName, arguments).Risk == CommandRisk.Destructive;
}
