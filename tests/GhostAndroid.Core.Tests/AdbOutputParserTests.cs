using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Models;
using Xunit;

namespace GhostAndroid.Core.Tests;

public class AdbOutputParserTests
{
    [Fact]
    public void ParseAdbDevices_ParsesStatesAndDescriptions()
    {
        const string output = """
            List of devices attached
            R58M12345ABC           device usb:1-1 product:beyond1q model:SM_G973U device:beyond1q transport_id:1
            emulator-5554          offline transport_id:2
            ab12cd34               unauthorized transport_id:3
            0123456789             recovery transport_id:4
            Z9Q8                   bootloader
            """;

        var devices = AdbOutputParser.ParseAdbDevices(output);

        Assert.Equal(5, devices.Count);
        Assert.Equal(ConnectionMode.Adb, devices[0].Mode);
        Assert.Equal("R58M12345ABC", devices[0].Serial);
        Assert.Contains("model:SM_G973U", devices[0].Description);
        Assert.Equal(ConnectionMode.Offline, devices[1].Mode);
        Assert.Equal(ConnectionMode.Unauthorized, devices[2].Mode);
        Assert.Equal(ConnectionMode.Recovery, devices[3].Mode);
        Assert.Equal(ConnectionMode.Fastboot, devices[4].Mode);
    }

    [Fact]
    public void ParseAdbDevices_IgnoresDaemonChatterAndEmptyInput()
    {
        Assert.Empty(AdbOutputParser.ParseAdbDevices(null));
        Assert.Empty(AdbOutputParser.ParseAdbDevices(""));

        const string chatter = """
            * daemon not running; starting now at tcp:5037
            * daemon started successfully
            List of devices attached
            """;
        Assert.Empty(AdbOutputParser.ParseAdbDevices(chatter));
    }

    [Fact]
    public void ParseGetProp_ParsesBracketFormat()
    {
        const string output = """
            [ro.product.manufacturer]: [samsung]
            [ro.product.model]: [SM-G973U]
            [ro.build.version.release]: [13]
            [ro.build.version.sdk]: [33]
            [sys.boot_completed]: [1]
            [empty.value]: []
            """;

        var props = AdbOutputParser.ParseGetProp(output);

        Assert.Equal("samsung", props["ro.product.manufacturer"]);
        Assert.Equal("SM-G973U", props["ro.product.model"]);
        Assert.Equal("13", props["ro.build.version.release"]);
        Assert.Equal("33", props["ro.build.version.sdk"]);
        Assert.Equal("1", props["sys.boot_completed"]);
        Assert.Equal("", props["empty.value"]);
    }

    [Fact]
    public void ParseBatteryLevel_ExtractsLevel()
    {
        const string dumpsys = """
            Current Battery Service state:
              AC powered: false
              USB powered: true
              status: 2
              level: 87
              scale: 100
            """;

        Assert.Equal(87, AdbOutputParser.ParseBatteryLevel(dumpsys));
        Assert.True(AdbOutputParser.ParseBatteryCharging(dumpsys));
        Assert.Null(AdbOutputParser.ParseBatteryLevel("no level here"));
    }

    [Fact]
    public void ParseBatteryCharging_FalseWhenUnpluggedAndNotCharging()
    {
        const string dumpsys = """
              AC powered: false
              USB powered: false
              Wireless powered: false
              status: 3
              level: 50
            """;
        Assert.False(AdbOutputParser.ParseBatteryCharging(dumpsys));
    }

    [Fact]
    public void ParsePackageList_StripsPrefix()
    {
        const string output = """
            package:com.android.chrome
            package:com.whatsapp
            com.noprefix.app
            """;
        var list = AdbOutputParser.ParsePackageList(output);
        Assert.Equal(new[] { "com.android.chrome", "com.whatsapp", "com.noprefix.app" }, list);
    }

    [Fact]
    public void ParseFastbootDevices_ParsesSerials()
    {
        const string output = "R58M12345ABC\tfastboot\n";
        var devices = AdbOutputParser.ParseFastbootDevices(output);
        Assert.Single(devices);
        Assert.Equal("R58M12345ABC", devices[0].Serial);
        Assert.Equal(ConnectionMode.Fastboot, devices[0].Mode);
    }

    [Fact]
    public void ParseFastbootVars_ParsesBootloaderLines()
    {
        const string output = """
            (bootloader) product: beyond1q
            (bootloader) unlocked: yes
            (bootloader) secure: no
            all: listed above
            finished. total time: 0.015s
            """;
        var vars = AdbOutputParser.ParseFastbootVars(output);
        Assert.Equal("beyond1q", vars["product"]);
        Assert.Equal("yes", vars["unlocked"]);
        Assert.True(AdbOutputParser.ParseYesNo(vars["unlocked"]));
        Assert.False(AdbOutputParser.ParseYesNo(vars["secure"]));
        Assert.DoesNotContain("finished", vars.Keys);
    }

    [Theory]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("1", true)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    [InlineData("maybe", null)]
    [InlineData("", null)]
    public void ParseYesNo_Works(string input, bool? expected)
        => Assert.Equal(expected, AdbOutputParser.ParseYesNo(input));
}
