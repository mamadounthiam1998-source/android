namespace GhostAndroid.Core.Models;

/// <summary>
/// Résultat brut et complet de l'exécution d'un processus externe (adb/fastboot).
/// Aucun masquage : le code de sortie, stdout et stderr réels sont exposés.
/// </summary>
public sealed class CommandResult
{
    public required string FileName { get; init; }
    public required string Arguments { get; init; }
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool TimedOut { get; init; }

    /// <summary>true seulement si le processus s'est terminé avec un code 0 et sans timeout.</summary>
    public bool Success => !TimedOut && ExitCode == 0;

    /// <summary>Sortie combinée (stdout puis stderr), utile pour l'affichage console.</summary>
    public string CombinedOutput
    {
        get
        {
            if (string.IsNullOrEmpty(StandardError))
                return StandardOutput;
            if (string.IsNullOrEmpty(StandardOutput))
                return StandardError;
            return StandardOutput + Environment.NewLine + StandardError;
        }
    }

    public override string ToString() =>
        $"{FileName} {Arguments} -> exit={ExitCode} timedOut={TimedOut} ({Duration.TotalMilliseconds:0} ms)";
}
