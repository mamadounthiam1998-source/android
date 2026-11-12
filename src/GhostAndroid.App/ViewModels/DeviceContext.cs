using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using GhostAndroid.Core.Devices;
using GhostAndroid.Core.Models;

namespace GhostAndroid.App.ViewModels;

/// <summary>
/// État partagé des appareils connectés : alimenté par le DeviceMonitor et consommé
/// par tous les modules. Marshalle les mises à jour vers le thread UI.
/// </summary>
public sealed partial class DeviceContext : ObservableObject
{
    private readonly DeviceMonitor _monitor;

    public ObservableCollection<AndroidDevice> Devices { get; } = new();

    [ObservableProperty]
    private AndroidDevice? _selectedDevice;

    [ObservableProperty]
    private bool _isScanning;

    public bool HasDevice => SelectedDevice is not null;
    public bool HasAnyDevice => Devices.Count > 0;

    public event EventHandler<AndroidDevice>? DeviceConnected;

    /// <summary>Déclenché après chaque fusion de la liste (pour rafraîchir les vues).</summary>
    public event Action? Updated;

    public DeviceContext(DeviceMonitor monitor)
    {
        _monitor = monitor;
        _monitor.DevicesChanged += OnDevicesChanged;
    }

    public void StartMonitoring() => _monitor.Start();

    public async Task ScanNowAsync()
    {
        IsScanning = true;
        try { await _monitor.ScanOnceAsync(); }
        finally { IsScanning = false; }
    }

    private void OnDevicesChanged(object? sender, DevicesChangedEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        dispatcher.Invoke(() =>
        {
            // Fusionne sans recréer la collection (préserve la sélection).
            var incoming = e.Devices.ToDictionary(d => d.Serial, StringComparer.OrdinalIgnoreCase);

            for (var i = Devices.Count - 1; i >= 0; i--)
            {
                if (!incoming.ContainsKey(Devices[i].Serial))
                    Devices.RemoveAt(i);
            }

            foreach (var dev in e.Devices)
            {
                var existing = Devices.FirstOrDefault(d => string.Equals(d.Serial, dev.Serial, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                    Devices.Add(dev);
                else
                    CopyInto(dev, existing);
            }

            SelectedDevice ??= Devices.FirstOrDefault();
            if (SelectedDevice is not null && Devices.All(d => !ReferenceEquals(d, SelectedDevice)))
                SelectedDevice = Devices.FirstOrDefault(d => string.Equals(d.Serial, SelectedDevice.Serial, StringComparison.OrdinalIgnoreCase))
                                 ?? Devices.FirstOrDefault();

            foreach (var added in e.Added)
                DeviceConnected?.Invoke(this, added);

            OnPropertyChanged(nameof(HasAnyDevice));
            OnPropertyChanged(nameof(HasDevice));
            Updated?.Invoke();
        });
    }

    private static void CopyInto(AndroidDevice src, AndroidDevice dst)
    {
        dst.Manufacturer = src.Manufacturer;
        dst.Brand = src.Brand;
        dst.Model = src.Model;
        dst.Codename = src.Codename;
        dst.AndroidVersion = src.AndroidVersion;
        dst.SdkLevel = src.SdkLevel;
        dst.BuildNumber = src.BuildNumber;
        dst.BatteryLevel = src.BatteryLevel;
        dst.BatteryCharging = src.BatteryCharging;
        dst.BootloaderUnlocked = src.BootloaderUnlocked;
        dst.BootCompleted = src.BootCompleted;
        dst.TransportDescription = src.TransportDescription;
        dst.LastSeen = src.LastSeen;
    }

    partial void OnSelectedDeviceChanged(AndroidDevice? value)
    {
        OnPropertyChanged(nameof(HasDevice));
    }
}
