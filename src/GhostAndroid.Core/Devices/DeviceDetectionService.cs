using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GhostAndroid.Core.Devices;

public interface IDeviceDetectionService
{
    /// <summary>Détecte tous les appareils (ADB + Fastboot) sans enrichissement complet.</summary>
    Task<IReadOnlyList<AndroidDevice>> DetectAsync(CancellationToken ct = default);

    /// <summary>Enrichit un appareil en mode ADB avec propriétés système et batterie.</summary>
    Task EnrichAsync(AndroidDevice device, CancellationToken ct = default);

    /// <summary>Détecte puis enrichit tous les appareils accessibles.</summary>
    Task<IReadOnlyList<AndroidDevice>> DetectAndEnrichAsync(CancellationToken ct = default);
}

/// <summary>
/// Fusionne les listes ADB et Fastboot et enrichit les appareils réellement
/// accessibles. Les informations manquantes restent nulles.
/// </summary>
public sealed class DeviceDetectionService : IDeviceDetectionService
{
    private readonly IAdbEngine _adb;
    private readonly IFastbootEngine _fastboot;
    private readonly ILogger<DeviceDetectionService> _logger;

    public DeviceDetectionService(IAdbEngine adb, IFastbootEngine fastboot, ILogger<DeviceDetectionService>? logger = null)
    {
        _adb = adb;
        _fastboot = fastboot;
        _logger = logger ?? NullLogger<DeviceDetectionService>.Instance;
    }

    public async Task<IReadOnlyList<AndroidDevice>> DetectAsync(CancellationToken ct = default)
    {
        var devices = new Dictionary<string, AndroidDevice>(StringComparer.OrdinalIgnoreCase);

        if (_adb.IsAvailable)
        {
            foreach (var d in await _adb.ListDevicesAsync(ct).ConfigureAwait(false))
                devices[d.Serial] = d;
        }

        if (_fastboot.IsAvailable)
        {
            foreach (var d in await _fastboot.ListDevicesAsync(ct).ConfigureAwait(false))
            {
                // Un appareil en fastboot peut avoir le même série ; priorité au mode fastboot réel.
                devices[d.Serial] = d;
            }
        }

        return devices.Values.OrderBy(d => d.Serial, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task EnrichAsync(AndroidDevice device, CancellationToken ct = default)
    {
        try
        {
            switch (device.Mode)
            {
                case ConnectionMode.Adb or ConnectionMode.Recovery:
                    var props = await _adb.GetPropertiesAsync(device.Serial, ct).ConfigureAwait(false);
                    if (props.Count > 0)
                        DevicePropertyMapper.Apply(device, props);

                    var battery = await _adb.GetBatteryLevelAsync(device.Serial, ct).ConfigureAwait(false);
                    if (battery.HasValue) device.BatteryLevel = battery;
                    break;

                case ConnectionMode.Fastboot:
                    var vars = await _fastboot.GetAllVariablesAsync(device.Serial, ct).ConfigureAwait(false);
                    if (vars.TryGetValue("product", out var product)) device.Codename = product;
                    if (vars.TryGetValue("unlocked", out var unlocked))
                        device.BootloaderUnlocked = AdbOutputParser.ParseYesNo(unlocked);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Enrichissement partiel de l'appareil {Serial}", device.Serial);
        }
        finally
        {
            device.LastSeen = DateTimeOffset.Now;
        }
    }

    public async Task<IReadOnlyList<AndroidDevice>> DetectAndEnrichAsync(CancellationToken ct = default)
    {
        var devices = await DetectAsync(ct).ConfigureAwait(false);
        foreach (var d in devices)
        {
            if (d.IsActionable)
                await EnrichAsync(d, ct).ConfigureAwait(false);
        }
        return devices;
    }
}
