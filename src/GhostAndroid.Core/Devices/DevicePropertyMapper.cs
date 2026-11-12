using System.Globalization;
using GhostAndroid.Core.Models;

namespace GhostAndroid.Core.Devices;

/// <summary>
/// Applique un dictionnaire de propriétés `getprop` sur un AndroidDevice.
/// Fonction pure pour être testée sans appareil.
/// </summary>
public static class DevicePropertyMapper
{
    public static void Apply(AndroidDevice device, IReadOnlyDictionary<string, string> props)
    {
        device.Manufacturer = First(props, "ro.product.manufacturer", "ro.product.system.manufacturer") ?? device.Manufacturer;
        device.Brand = First(props, "ro.product.brand", "ro.product.system.brand") ?? device.Brand;
        device.Model = First(props, "ro.product.model", "ro.product.system.model") ?? device.Model;
        device.Codename = First(props, "ro.product.device", "ro.product.system.device", "ro.build.product") ?? device.Codename;
        device.AndroidVersion = First(props, "ro.build.version.release") ?? device.AndroidVersion;
        device.BuildNumber = First(props, "ro.build.display.id", "ro.build.id") ?? device.BuildNumber;

        var sdk = First(props, "ro.build.version.sdk");
        if (int.TryParse(sdk, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sdkValue))
            device.SdkLevel = sdkValue;

        var boot = First(props, "sys.boot_completed");
        if (boot is not null)
            device.BootCompleted = boot.Trim() == "1";

        // État du verrouillage bootloader tel que visible en mode système.
        var locked = First(props, "ro.boot.flash.locked", "ro.boot.vbmeta.device_state");
        if (locked is not null)
        {
            var l = locked.Trim().ToLowerInvariant();
            if (l is "0" or "unlocked") device.BootloaderUnlocked = true;
            else if (l is "1" or "locked") device.BootloaderUnlocked = false;
        }
    }

    private static string? First(IReadOnlyDictionary<string, string> props, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (props.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }
}
