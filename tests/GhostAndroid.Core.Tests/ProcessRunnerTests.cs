using System.Runtime.InteropServices;
using GhostAndroid.Core.Infrastructure;
using Xunit;

namespace GhostAndroid.Core.Tests;

public class ProcessRunnerTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public async Task Run_CapturesStandardOutput()
    {
        var runner = new ProcessRunner();
        var (file, args) = IsWindows
            ? ("cmd.exe", "/c echo ghost_android")
            : ("/bin/sh", "-c \"echo ghost_android\"");

        var result = await runner.RunAsync(file, args, TimeSpan.FromSeconds(10));

        Assert.True(result.Success, result.CombinedOutput);
        Assert.Contains("ghost_android", result.StandardOutput);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Run_NonExistentExecutable_ReturnsFailure_DoesNotThrow()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync("this_binary_does_not_exist_ghost_xyz", "arg", TimeSpan.FromSeconds(5));

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.False(string.IsNullOrEmpty(result.StandardError));
    }

    [Fact]
    public async Task Run_NonZeroExit_ReportedAsFailure()
    {
        var runner = new ProcessRunner();
        var (file, args) = IsWindows
            ? ("cmd.exe", "/c exit 7")
            : ("/bin/sh", "-c \"exit 7\"");

        var result = await runner.RunAsync(file, args, TimeSpan.FromSeconds(10));

        Assert.False(result.Success);
        Assert.Equal(7, result.ExitCode);
    }
}
