using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostAndroid.Core.Diagnostics;
using GhostAndroid.Core.Models;

namespace GhostAndroid.App.ViewModels;

public sealed partial class DiagnosticsViewModel : ObservableObject
{
    private readonly DiagnosticService _diagnostics;
    private readonly DeviceContext _context;

    public ObservableCollection<DiagnosticCheck> Checks { get; } = new();

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _summary = "Lancez un diagnostic pour analyser l'environnement et l'appareil.";
    [ObservableProperty] private DiagnosticSeverity _worst = DiagnosticSeverity.Unknown;

    public DeviceContext Context => _context;

    public DiagnosticsViewModel(DiagnosticService diagnostics, DeviceContext context)
    {
        _diagnostics = diagnostics;
        _context = context;
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        IsRunning = true;
        Checks.Clear();
        try
        {
            // 1) Environnement (outils, pilotes).
            var env = _diagnostics.CheckEnvironment();
            foreach (var c in env.Checks) Checks.Add(c);

            // 2) Appareil sélectionné.
            var device = _context.SelectedDevice;
            if (device is not null)
            {
                var report = await _diagnostics.CheckDeviceAsync(device);
                foreach (var c in report.Checks) Checks.Add(c);
            }

            Worst = Checks.Count == 0
                ? DiagnosticSeverity.Unknown
                : Checks.Select(c => c.Severity).Where(s => s != DiagnosticSeverity.Unknown)
                        .DefaultIfEmpty(DiagnosticSeverity.Unknown).Max();

            var errors = Checks.Count(c => c.Severity == DiagnosticSeverity.Error);
            var warnings = Checks.Count(c => c.Severity == DiagnosticSeverity.Warning);
            Summary = device is null
                ? $"Environnement analysé : {errors} erreur(s), {warnings} avertissement(s). Aucun appareil sélectionné."
                : $"{device.DisplayName} — {errors} erreur(s), {warnings} avertissement(s) sur {Checks.Count} contrôles.";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
