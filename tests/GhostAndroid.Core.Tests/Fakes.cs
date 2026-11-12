using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Models;

namespace GhostAndroid.Core.Tests;

/// <summary>
/// Double de test minimal pour IAdbEngine. Les tests d'archivage local n'utilisent
/// pas ADB ; ce double sert uniquement à satisfaire le constructeur.
/// </summary>
internal sealed class FakeAdbEngine : IAdbEngine
{
    public bool IsAvailable => false;
    public string? AdbPath => null;

    public Task<string?> GetVersionAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task<IReadOnlyList<AndroidDevice>> ListDevicesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AndroidDevice>>(Array.Empty<AndroidDevice>());
    public Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(string serial, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    public Task<string?> GetPropertyAsync(string serial, string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task<int?> GetBatteryLevelAsync(string serial, CancellationToken ct = default) => Task.FromResult<int?>(null);
    public Task<CommandResult> ShellAsync(string serial, string command, CancellationToken ct = default)
        => Task.FromResult(new CommandResult { FileName = "adb", Arguments = command });
    public Task<IReadOnlyList<string>> ListPackagesAsync(string serial, bool thirdPartyOnly, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    public Task<CommandResult> InstallApkAsync(string serial, string apkPath, bool allowDowngrade = false, CancellationToken ct = default)
        => Task.FromResult(new CommandResult { FileName = "adb", Arguments = "install" });
    public Task<CommandResult> UninstallAsync(string serial, string packageName, bool keepData = false, CancellationToken ct = default)
        => Task.FromResult(new CommandResult { FileName = "adb", Arguments = "uninstall" });
    public Task<CommandResult> PullAsync(string serial, string devicePath, string localPath, CancellationToken ct = default)
        => Task.FromResult(new CommandResult { FileName = "adb", Arguments = "pull" });
    public Task<CommandResult> PushAsync(string serial, string localPath, string devicePath, CancellationToken ct = default)
        => Task.FromResult(new CommandResult { FileName = "adb", Arguments = "push" });
    public Task<CommandResult> RebootAsync(string serial, string? mode = null, CancellationToken ct = default)
        => Task.FromResult(new CommandResult { FileName = "adb", Arguments = "reboot" });
    public Task<CommandResult> LogcatDumpAsync(string serial, int maxLines = 2000, CancellationToken ct = default)
        => Task.FromResult(new CommandResult { FileName = "adb", Arguments = "logcat" });
    public Task<CommandResult> StartServerAsync(CancellationToken ct = default)
        => Task.FromResult(new CommandResult { FileName = "adb", Arguments = "start-server" });
    public Task<CommandResult> KillServerAsync(CancellationToken ct = default)
        => Task.FromResult(new CommandResult { FileName = "adb", Arguments = "kill-server" });
}
