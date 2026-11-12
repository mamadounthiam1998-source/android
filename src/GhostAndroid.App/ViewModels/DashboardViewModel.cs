using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostAndroid.App.Services;
using GhostAndroid.Core.PlatformTools;

namespace GhostAndroid.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly DeviceContext _context;
    private readonly PlatformToolsLocator _locator;
    private readonly PlatformToolsInstaller _installer;
    private readonly IDialogService _dialogs;

    public ObservableCollection<DeviceCardViewModel> Cards { get; } = new();

    [ObservableProperty] private DeviceCardViewModel? _selectedCard;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isInstallingTools;
    [ObservableProperty] private int _installProgress;
    [ObservableProperty] private string _installStage = "";

    public bool HasDevices => Cards.Count > 0;
    public bool NoDevices => Cards.Count == 0;

    /// <summary>true si adb est présent (outils installés).</summary>
    public bool AdbAvailable => _locator.AdbAvailable;
    /// <summary>true si les outils ADB sont absents → on propose l'installation automatique.</summary>
    public bool ToolsMissing => !_locator.AdbAvailable;

    public DashboardViewModel(DeviceContext context, PlatformToolsLocator locator,
        PlatformToolsInstaller installer, IDialogService dialogs)
    {
        _context = context;
        _locator = locator;
        _installer = installer;
        _dialogs = dialogs;
        _context.Updated += Sync;
        Sync();
    }

    private void Sync()
    {
        for (var i = Cards.Count - 1; i >= 0; i--)
        {
            if (_context.Devices.All(d => !string.Equals(d.Serial, Cards[i].Serial, StringComparison.OrdinalIgnoreCase)))
                Cards.RemoveAt(i);
        }

        foreach (var dev in _context.Devices)
        {
            var card = Cards.FirstOrDefault(c => string.Equals(c.Serial, dev.Serial, StringComparison.OrdinalIgnoreCase));
            if (card is null)
                Cards.Add(new DeviceCardViewModel(dev));
            else
                card.Refresh();
        }

        SelectedCard ??= Cards.FirstOrDefault();
        IsScanning = _context.IsScanning;
        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(NoDevices));
    }

    partial void OnSelectedCardChanged(DeviceCardViewModel? value)
    {
        if (value is not null)
            _context.SelectedDevice = value.Device;
    }

    [RelayCommand]
    private async Task DetectAsync()
    {
        IsScanning = true;
        try { await _context.ScanNowAsync(); }
        finally { IsScanning = false; Sync(); }
    }

    /// <summary>Télécharge et installe automatiquement les Android Platform Tools (Google).</summary>
    [RelayCommand]
    private async Task InstallToolsAsync()
    {
        if (!_dialogs.Confirm("Installer les outils ADB",
                "GHOST ANDROID va télécharger les Android Platform Tools officiels de Google " +
                "(adb/fastboot) et les installer localement. Une connexion Internet est requise " +
                "pour cette étape uniquement. Continuer ?"))
            return;

        IsInstallingTools = true;
        InstallProgress = 0;
        var progress = new Progress<ToolInstallProgress>(p => { InstallProgress = p.Percent; InstallStage = p.Stage; });
        try
        {
            await _installer.EnsureInstalledAsync(progress);
            OnPropertyChanged(nameof(AdbAvailable));
            OnPropertyChanged(nameof(ToolsMissing));
            _dialogs.Alert("Outils ADB", "Android Platform Tools installés. Lancement de la détection.");
            await _context.ScanNowAsync();
            Sync();
        }
        catch (Exception ex)
        {
            _dialogs.Error("Installation des outils ADB",
                "Échec du téléchargement/installation : " + ex.Message +
                "\n\nVous pouvez aussi installer manuellement les Platform Tools depuis " +
                "developer.android.com puis relancer la détection.");
        }
        finally
        {
            IsInstallingTools = false;
            InstallStage = "";
        }
    }
}
