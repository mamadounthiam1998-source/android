using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostAndroid.Core.Adb;

namespace GhostAndroid.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly IAdbEngine _adb;
    private readonly IFastbootEngine _fastboot;

    [ObservableProperty]
    private ObservableObject _currentViewModel;

    [ObservableProperty]
    private string _currentSection = "Dashboard";

    public DeviceContext DeviceContext { get; }

    public bool AdbAvailable => _adb.IsAvailable;
    public bool FastbootAvailable => _fastboot.IsAvailable;
    public string ToolStatus =>
        (AdbAvailable ? "ADB prêt" : "ADB introuvable") + " · " +
        (FastbootAvailable ? "Fastboot prêt" : "Fastboot introuvable");

    public MainViewModel(
        IServiceProvider services,
        DeviceContext deviceContext,
        DashboardViewModel dashboard,
        IAdbEngine adb,
        IFastbootEngine fastboot)
    {
        _services = services;
        DeviceContext = deviceContext;
        _adb = adb;
        _fastboot = fastboot;
        _currentViewModel = dashboard;

        DeviceContext.StartMonitoring();
        _ = DeviceContext.ScanNowAsync();
    }

    [RelayCommand]
    private void Navigate(string section)
    {
        CurrentSection = section;
        CurrentViewModel = section switch
        {
            "Dashboard" => Resolve<DashboardViewModel>(),
            "Console" => Resolve<ConsoleViewModel>(),
            "Diagnostics" => Resolve<DiagnosticsViewModel>(),
            "System" => Resolve<SystemViewModel>(),
            "Backup" => Resolve<BackupViewModel>(),
            "Firmware" => Resolve<FirmwareViewModel>(),
            "Unlock" => Resolve<UnlockViewModel>(),
            _ => Resolve<DashboardViewModel>(),
        };
    }

    [RelayCommand]
    private async Task ScanAsync() => await DeviceContext.ScanNowAsync();

    private T Resolve<T>() where T : ObservableObject =>
        (T)_services.GetService(typeof(T))!;
}
