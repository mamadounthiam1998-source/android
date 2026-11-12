using GhostAndroid.Core.Devices;
using GhostAndroid.Core.Models;
using Xunit;

namespace GhostAndroid.Core.Tests;

public class DevicePropertyMapperTests
{
    [Fact]
    public void Apply_PopulatesDeviceFields()
    {
        var device = new AndroidDevice { Serial = "ABC123", Mode = ConnectionMode.Adb };
        var props = new Dictionary<string, string>
        {
            ["ro.product.manufacturer"] = "samsung",
            ["ro.product.brand"] = "samsung",
            ["ro.product.model"] = "SM-G973U",
            ["ro.product.device"] = "beyond1q",
            ["ro.build.version.release"] = "13",
            ["ro.build.version.sdk"] = "33",
            ["ro.build.display.id"] = "TP1A.220624.014",
            ["sys.boot_completed"] = "1",
            ["ro.boot.flash.locked"] = "1",
        };

        DevicePropertyMapper.Apply(device, props);

        Assert.Equal("samsung", device.Manufacturer);
        Assert.Equal("SM-G973U", device.Model);
        Assert.Equal("beyond1q", device.Codename);
        Assert.Equal("13", device.AndroidVersion);
        Assert.Equal(33, device.SdkLevel);
        Assert.Equal("TP1A.220624.014", device.BuildNumber);
        Assert.True(device.BootCompleted);
        Assert.False(device.BootloaderUnlocked); // flash.locked = 1 → verrouillé
        Assert.Equal("Samsung SM-G973U", device.DisplayName);
    }

    [Fact]
    public void Apply_LeavesMissingFieldsNull()
    {
        var device = new AndroidDevice { Serial = "X" };
        DevicePropertyMapper.Apply(device, new Dictionary<string, string>());

        Assert.Null(device.Model);
        Assert.Null(device.AndroidVersion);
        Assert.Null(device.SdkLevel);
        Assert.Equal("X", device.DisplayName); // repli sur le numéro de série
    }

    [Fact]
    public void Apply_DetectsUnlockedBootloader()
    {
        var device = new AndroidDevice { Serial = "X" };
        DevicePropertyMapper.Apply(device, new Dictionary<string, string> { ["ro.boot.flash.locked"] = "0" });
        Assert.True(device.BootloaderUnlocked);
    }
}
