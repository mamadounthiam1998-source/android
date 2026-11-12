namespace GhostAndroid.Core.Models;

public enum DiagnosticSeverity
{
    /// <summary>Tout va bien.</summary>
    Ok = 0,
    /// <summary>Information neutre.</summary>
    Info,
    /// <summary>Avertissement : fonctionne mais attention requise.</summary>
    Warning,
    /// <summary>Erreur : problème avéré.</summary>
    Error,
    /// <summary>Non déterminé : le test n'a pas pu conclure.</summary>
    Unknown
}

/// <summary>Résultat d'un contrôle de diagnostic unitaire.</summary>
public sealed class DiagnosticCheck
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public DiagnosticSeverity Severity { get; set; } = DiagnosticSeverity.Unknown;
    public string? Detail { get; set; }
    public string? Recommendation { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>Rapport de diagnostic complet composé de plusieurs contrôles.</summary>
public sealed class DiagnosticReport
{
    public string? DeviceSerial { get; init; }
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
    public List<DiagnosticCheck> Checks { get; } = new();

    public DiagnosticSeverity Worst =>
        Checks.Count == 0
            ? DiagnosticSeverity.Unknown
            : Checks.Select(c => c.Severity)
                    .Where(s => s != DiagnosticSeverity.Unknown)
                    .DefaultIfEmpty(DiagnosticSeverity.Unknown)
                    .Max();

    public int ErrorCount => Checks.Count(c => c.Severity == DiagnosticSeverity.Error);
    public int WarningCount => Checks.Count(c => c.Severity == DiagnosticSeverity.Warning);
}
