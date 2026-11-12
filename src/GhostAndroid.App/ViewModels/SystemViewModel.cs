using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostAndroid.App.Services;
using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Models;

namespace GhostAndroid.App.ViewModels;

public sealed partial class SystemViewModel : ObservableObject
{
    private readonly IAdbEngine _adb;
    private readonly IDialogService _dialogs;
    private readonly DeviceContext _context;

    public ObservableCollection<KeyValuePair<string, string>> Info { get; } = new();
    public ObservableCollection<string> Packages { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _thirdPartyOnly = true;
    [ObservableProperty] private string _storageInfo = "";
    [ObservableProperty] private string _packageFilter = "";
    [ObservableProperty] private string? _selectedPackage;

    public DeviceContext Context => _context;

    public SystemViewModel(IAdbEngine adb, IDialogService dialogs, DeviceContext context)
    {
        _adb = adb;
        _dialogs = dialogs;
        _context = context;
    }

    private AndroidDevice? Target => _context.SelectedDevice;

    private bool EnsureAdb()
    {
        if (Target is null) { _dialogs.Error("Système", "Aucun appareil sélectionné."); return false; }
        if (Target.Mode != ConnectionMode.Adb) { _dialogs.Error("Système", "L'appareil doit être en mode ADB."); return false; }
        return true;
    }

    [RelayCommand]
    private async Task LoadInfoAsync()
    {
        if (!EnsureAdb()) return;
        IsBusy = true;
        Info.Clear();
        try
        {
            var props = await _adb.GetPropertiesAsync(Target!.Serial);
            void Add(string label, string key)
            {
                if (props.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                    Info.Add(new(label, v));
            }
            Add("Fabricant", "ro.product.manufacturer");
            Add("Modèle", "ro.product.model");
            Add("Nom de code", "ro.product.device");
            Add("Version Android", "ro.build.version.release");
            Add("Niveau API", "ro.build.version.sdk");
            Add("Build", "ro.build.display.id");
            Add("Empreinte", "ro.build.fingerprint");
            Add("Patch de sécurité", "ro.build.version.security_patch");
            Add("Processeur (ABI)", "ro.product.cpu.abi");
            Add("Bande de base", "gsm.version.baseband");

            var battery = await _adb.GetBatteryLevelAsync(Target.Serial);
            if (battery is { } b) Info.Add(new("Batterie", $"{b}%"));

            var df = await _adb.ShellAsync(Target.Serial, "df -h /data /storage/emulated 2>/dev/null");
            StorageInfo = df.Success ? df.StandardOutput : "Information de stockage indisponible.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadPackagesAsync()
    {
        if (!EnsureAdb()) return;
        IsBusy = true;
        Packages.Clear();
        try
        {
            var list = await _adb.ListPackagesAsync(Target!.Serial, ThirdPartyOnly);
            foreach (var p in list.OrderBy(x => x))
            {
                if (string.IsNullOrEmpty(PackageFilter) || p.Contains(PackageFilter, StringComparison.OrdinalIgnoreCase))
                    Packages.Add(p);
            }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task InstallApkAsync()
    {
        if (!EnsureAdb()) return;
        var apk = _dialogs.OpenFile("Application Android (*.apk)|*.apk", "Choisir un APK à installer");
        if (apk is null) return;

        IsBusy = true;
        try
        {
            var result = await _adb.InstallApkAsync(Target!.Serial, apk);
            if (result.Success) _dialogs.Alert("Installation", "APK installé avec succès.");
            else _dialogs.Error("Installation", result.CombinedOutput);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (!EnsureAdb() || SelectedPackage is null) return;
        if (!_dialogs.ConfirmDestructive("Désinstallation", $"Désinstaller « {SelectedPackage} » ?")) return;

        IsBusy = true;
        try
        {
            var result = await _adb.UninstallAsync(Target!.Serial, SelectedPackage);
            if (result.Success) { Packages.Remove(SelectedPackage); _dialogs.Alert("Désinstallation", "Paquet désinstallé."); }
            else _dialogs.Error("Désinstallation", result.CombinedOutput);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RebootAsync(string mode)
    {
        if (Target is null) { _dialogs.Error("Redémarrage", "Aucun appareil sélectionné."); return; }

        var label = mode switch { "recovery" => "Recovery", "bootloader" => "Bootloader", _ => "normal" };
        if (!_dialogs.Confirm("Redémarrage", $"Redémarrer l'appareil en mode {label} ?")) return;

        var result = await _adb.RebootAsync(Target.Serial, mode == "normal" ? null : mode);
        if (!result.Success) _dialogs.Error("Redémarrage", result.CombinedOutput);
    }
}
