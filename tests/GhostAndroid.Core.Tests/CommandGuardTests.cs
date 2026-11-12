using GhostAndroid.Core.Commands;
using Xunit;

namespace GhostAndroid.Core.Tests;

public class CommandGuardTests
{
    [Theory]
    [InlineData("fastboot", "-s X flash boot boot.img")]
    [InlineData("fastboot", "-s X erase userdata")]
    [InlineData("fastboot", "-s X -w")]
    [InlineData("fastboot", "-s X flashing unlock")]
    [InlineData("adb", "-s X shell rm -rf /sdcard/DCIM")]
    [InlineData("adb", "-s X shell pm clear com.app")]
    [InlineData("adb", "-s X shell recovery --wipe_data")]
    public void Destructive_RequiresConfirmation(string file, string args)
    {
        Assert.Equal(CommandRisk.Destructive, CommandGuard.Classify(file, args).Risk);
        Assert.True(CommandGuard.RequiresConfirmation(file, args));
    }

    [Theory]
    [InlineData("adb", "-s X install app.apk")]
    [InlineData("adb", "-s X uninstall com.app")]
    [InlineData("adb", "-s X push file /sdcard/")]
    [InlineData("adb", "-s X reboot bootloader")]
    public void Caution_DoesNotRequireConfirmationButIsFlagged(string file, string args)
    {
        Assert.Equal(CommandRisk.Caution, CommandGuard.Classify(file, args).Risk);
        Assert.False(CommandGuard.RequiresConfirmation(file, args));
    }

    [Theory]
    [InlineData("adb", "-s X devices -l")]
    [InlineData("adb", "-s X shell getprop")]
    [InlineData("adb", "-s X shell dumpsys battery")]
    [InlineData("fastboot", "-s X getvar all")]
    public void Safe_IsSafe(string file, string args)
    {
        Assert.Equal(CommandRisk.Safe, CommandGuard.Classify(file, args).Risk);
        Assert.False(CommandGuard.RequiresConfirmation(file, args));
    }

    [Fact]
    public void Classification_ProvidesReason()
    {
        var c = CommandGuard.Classify("fastboot", "flash system system.img");
        Assert.False(string.IsNullOrWhiteSpace(c.Reason));
    }
}
