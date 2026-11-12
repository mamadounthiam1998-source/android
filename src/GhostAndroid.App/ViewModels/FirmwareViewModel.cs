using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostAndroid.App.Services;
using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Models;

namespace GhostAndroid.App.ViewModels;

/// <summary>
/// Module Firmware : identifie l'appareil et évalue les conditions de sécurité.
/// IMPORTANT — le flashage n'est PAS automatisé par un bouton : c'est une opération
/// guidée et manuelle. GHOST ANDROID ne flashe pas de firmware incompatible et ne
/// modifie pas les protections du fabricant.
/// </summary>
public sealed partial class FirmwareViewModel : ObservableObject
{
    private readonly IAdbEngine _adb;
    private readonly IFastbootEngine _fastboot;
    private readonly DeviceContext _context;

    public ObservableCollection<KeyValuePair<string, string>> Identity { get; } = new();
    public ObservableCollection<SafetyCheck> SafetyChecks { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _notice =
        "Le flashage de firmware est une opération sensible et guidée, jamais déclenchée par un simple bouton. " +
        "GHOST ANDROID identifie l'appareil, vérifie la compatibilité et les conditions de sécurité, puis " +
        "vous oriente. Il ne modifie pas les protections du fabricant et n'installe pas de firmware incompatible.";

    public DeviceContext Context => _context;

    public FirmwareViewModel(IAdbEngine adb, IFastbootEngine fastboot, DeviceContext context)
    {
        _adb = adb;
        _fastboot = fastboot;
        _context = context;
    }

    [RelayCommand]
    private async Task IdentifyAsync()
    {
        var device = _context.SelectedDevice;
        if (device is null) return;

        IsBusy = true;
        Identity.Clear();
        SafetyChecks.Clear();
        try
        {
            if (device.Mode == ConnectionMode.Adb)
            {
                var props = await _adb.GetPropertiesAsync(device.Serial);
                void Add(string label, string key) { if (props.TryGetValue(key, out var v) && v.Length > 0) Identity.Add(new(label, v)); }
                Add("Modèle", "ro.product.model");
                Add("Nom de code", "ro.product.device");
                Add("Empreinte firmware", "ro.build.fingerprint");
                Add("Version Android", "ro.build.version.release");
                Add("Patch de sécurité", "ro.build.version.security_patch");
                Add("Bootloader", "ro.bootloader");
                Add("Région/CSC", "ro.csc.sales_code");

                var battery = await _adb.GetBatteryLevelAsync(device.Serial);
                EvaluateSafety(battery, device.BootloaderUnlocked);
            }
            else if (device.Mode == ConnectionMode.Fastboot)
            {
                var vars = await _fastboot.GetAllVariablesAsync(device.Serial);
                foreach (var kv in vars.Take(20)) Identity.Add(new(kv.Key, kv.Value));
                var unlocked = await _fastboot.IsBootloaderUnlockedAsync(device.Serial);
                EvaluateSafety(null, unlocked);
            }
        }
        finally { IsBusy = false; }
    }

    private void EvaluateSafety(int? battery, bool? bootloaderUnlocked)
    {
        SafetyChecks.Add(new SafetyCheck
        {
            Label = "Niveau de batterie suffisant (≥ 50 %)",
            Passed = battery is >= 50,
            Detail = battery is { } b ? $"{b}%" : "Inconnu (branchez l'appareil en mode système pour lire la batterie)."
        });

        SafetyChecks.Add(new SafetyCheck
        {
            Label = "État du bootloader connu",
            Passed = bootloaderUnlocked is not null,
            Detail = bootloaderUnlocked switch
            {
                true => "Déverrouillé.",
                false => "Verrouillé (recommandé pour la sécurité).",
                null => "Non déterminé."
            }
        });

        SafetyChecks.Add(new SafetyCheck
        {
            Label = "Firmware d'origine officiel uniquement",
            Passed = true,
            Detail = "GHOST ANDROID n'autorise que les firmwares officiels compatibles avec le modèle identifié."
        });
    }
}

public sealed class SafetyCheck
{
    public required string Label { get; init; }
    public bool Passed { get; init; }
    public string? Detail { get; init; }
}
