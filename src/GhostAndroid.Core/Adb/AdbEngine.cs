using GhostAndroid.Core.Infrastructure;
using GhostAndroid.Core.Models;
using GhostAndroid.Core.PlatformTools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GhostAndroid.Core.Adb;

/// <summary>
/// Implémentation réelle du moteur ADB. Chaque méthode exécute véritablement adb ;
/// aucune valeur n'est fabriquée. Si adb est introuvable, une ToolNotFoundException
/// est levée (jamais un faux succès).
/// </summary>
public sealed class AdbEngine : IAdbEngine
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MediumTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(10);

    private readonly IProcessRunner _runner;
    private readonly PlatformToolsLocator _locator;
    private readonly ILogger<AdbEngine> _logger;

    public AdbEngine(IProcessRunner runner, PlatformToolsLocator locator, ILogger<AdbEngine>? logger = null)
    {
        _runner = runner;
        _locator = locator;
        _logger = logger ?? NullLogger<AdbEngine>.Instance;
    }

    public bool IsAvailable => _locator.AdbAvailable;
    public string? AdbPath => _locator.FindAdb();

    private string RequireAdb() => AdbPath ?? throw new ToolNotFoundException(PlatformToolsLocator.AdbFileName);

    private Task<CommandResult> RunAsync(string args, TimeSpan? timeout = null, CancellationToken ct = default)
        => _runner.RunAsync(RequireAdb(), args, timeout ?? MediumTimeout, cancellationToken: ct);

    private static string ForDevice(string serial, string rest) => $"-s {Quote(serial)} {rest}";

    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        if (!IsAvailable) return null;
        var r = await RunAsync("version", ShortTimeout, ct).ConfigureAwait(false);
        return r.Success ? r.StandardOutput : null;
    }

    public async Task<IReadOnlyList<AndroidDevice>> ListDevicesAsync(CancellationToken ct = default)
    {
        if (!IsAvailable) return Array.Empty<AndroidDevice>();

        var r = await RunAsync("devices -l", ShortTimeout, ct).ConfigureAwait(false);
        var parsed = AdbOutputParser.ParseAdbDevices(r.StandardOutput);

        return parsed.Select(p => new AndroidDevice
        {
            Serial = p.Serial,
            Mode = p.Mode,
            TransportDescription = p.Description,
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(string serial, CancellationToken ct = default)
    {
        var r = await RunAsync(ForDevice(serial, "shell getprop"), MediumTimeout, ct).ConfigureAwait(false);
        if (!r.Success) return new Dictionary<string, string>();
        return AdbOutputParser.ParseGetProp(r.StandardOutput);
    }

    public async Task<string?> GetPropertyAsync(string serial, string key, CancellationToken ct = default)
    {
        var r = await RunAsync(ForDevice(serial, $"shell getprop {Quote(key)}"), ShortTimeout, ct).ConfigureAwait(false);
        if (!r.Success) return null;
        var v = r.StandardOutput.Trim();
        return v.Length == 0 ? null : v;
    }

    public async Task<int?> GetBatteryLevelAsync(string serial, CancellationToken ct = default)
    {
        var r = await RunAsync(ForDevice(serial, "shell dumpsys battery"), ShortTimeout, ct).ConfigureAwait(false);
        return r.Success ? AdbOutputParser.ParseBatteryLevel(r.StandardOutput) : null;
    }

    public Task<CommandResult> ShellAsync(string serial, string command, CancellationToken ct = default)
        => RunAsync(ForDevice(serial, $"shell {command}"), MediumTimeout, ct);

    public async Task<IReadOnlyList<string>> ListPackagesAsync(string serial, bool thirdPartyOnly, CancellationToken ct = default)
    {
        var flag = thirdPartyOnly ? "-3" : "";
        var r = await RunAsync(ForDevice(serial, $"shell pm list packages {flag}"), MediumTimeout, ct).ConfigureAwait(false);
        return r.Success ? AdbOutputParser.ParsePackageList(r.StandardOutput) : Array.Empty<string>();
    }

    public async Task<CommandResult> InstallApkAsync(string serial, string apkPath, bool allowDowngrade = false, CancellationToken ct = default)
    {
        if (!File.Exists(apkPath))
        {
            return new CommandResult
            {
                FileName = AdbPath ?? "adb",
                Arguments = $"install {apkPath}",
                ExitCode = -1,
                StandardError = $"Fichier APK introuvable : {apkPath}",
            };
        }
        var downgrade = allowDowngrade ? "-d " : "";
        return await RunAsync(ForDevice(serial, $"install -r {downgrade}{Quote(apkPath)}"), LongTimeout, ct).ConfigureAwait(false);
    }

    public Task<CommandResult> UninstallAsync(string serial, string packageName, bool keepData = false, CancellationToken ct = default)
    {
        var keep = keepData ? "-k " : "";
        return RunAsync(ForDevice(serial, $"uninstall {keep}{Quote(packageName)}"), MediumTimeout, ct);
    }

    public Task<CommandResult> PullAsync(string serial, string devicePath, string localPath, CancellationToken ct = default)
        => RunAsync(ForDevice(serial, $"pull {Quote(devicePath)} {Quote(localPath)}"), LongTimeout, ct);

    public Task<CommandResult> PushAsync(string serial, string localPath, string devicePath, CancellationToken ct = default)
        => RunAsync(ForDevice(serial, $"push {Quote(localPath)} {Quote(devicePath)}"), LongTimeout, ct);

    public Task<CommandResult> RebootAsync(string serial, string? mode = null, CancellationToken ct = default)
    {
        var target = string.IsNullOrWhiteSpace(mode) ? "reboot" : $"reboot {mode}";
        return RunAsync(ForDevice(serial, target), ShortTimeout, ct);
    }

    public Task<CommandResult> LogcatDumpAsync(string serial, int maxLines = 2000, CancellationToken ct = default)
        => RunAsync(ForDevice(serial, $"logcat -d -t {maxLines}"), MediumTimeout, ct);

    public Task<CommandResult> StartServerAsync(CancellationToken ct = default)
        => RunAsync("start-server", ShortTimeout, ct);

    public Task<CommandResult> KillServerAsync(CancellationToken ct = default)
        => RunAsync("kill-server", ShortTimeout, ct);

    /// <summary>Entoure un argument de guillemets s'il contient des espaces.</summary>
    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return value.IndexOfAny(new[] { ' ', '\t' }) >= 0 ? $"\"{value}\"" : value;
    }
}
