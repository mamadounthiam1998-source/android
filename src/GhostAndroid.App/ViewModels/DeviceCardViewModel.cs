using CommunityToolkit.Mvvm.ComponentModel;
using GhostAndroid.Core.Models;

namespace GhostAndroid.App.ViewModels;

/// <summary>Enveloppe observable d'un AndroidDevice pour l'affichage en carte.</summary>
public sealed partial class DeviceCardViewModel : ObservableObject
{
    public AndroidDevice Device { get; }

    public DeviceCardViewModel(AndroidDevice device)
    {
        Device = device;
        Refresh();
    }

    public string Serial => Device.Serial;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _androidLine = "";
    [ObservableProperty] private string _buildLine = "";
    [ObservableProperty] private string _modeText = "";
    [ObservableProperty] private ConnectionMode _mode;
    [ObservableProperty] private string _batteryText = "";
    [ObservableProperty] private bool _hasBattery;
    [ObservableProperty] private bool _isActionable;

    public void Refresh()
    {
        Title = Device.DisplayName;
        Subtitle = string.IsNullOrWhiteSpace(Device.Codename) ? Device.Serial : $"{Device.Codename} · {Device.Serial}";
        Mode = Device.Mode;
        ModeText = Device.Mode switch
        {
            ConnectionMode.Adb => "ADB (système)",
            ConnectionMode.Recovery => "Recovery",
            ConnectionMode.Fastboot => "Fastboot / Bootloader",
            ConnectionMode.Sideload => "Sideload",
            ConnectionMode.Unauthorized => "Non autorisé",
            ConnectionMode.Offline => "Hors ligne",
            _ => "Inconnu"
        };
        AndroidLine = Device.AndroidVersion is null
            ? "Version Android : —"
            : $"Android {Device.AndroidVersion}" + (Device.SdkLevel is { } s ? $" (API {s})" : "");
        BuildLine = Device.BuildNumber is null ? "Build : —" : $"Build : {Device.BuildNumber}";
        HasBattery = Device.BatteryLevel.HasValue;
        BatteryText = Device.BatteryLevel is { } b ? $"{b}%" : "—";
        IsActionable = Device.IsActionable;
    }
}
