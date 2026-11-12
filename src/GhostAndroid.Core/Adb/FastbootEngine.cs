using GhostAndroid.Core.Infrastructure;
using GhostAndroid.Core.Models;
using GhostAndroid.Core.PlatformTools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GhostAndroid.Core.Adb;

/// <summary>
/// Implémentation réelle du moteur Fastboot. Opérations volontairement limitées à
/// la lecture d'informations et aux redémarrages : GHOST ANDROID ne flashe pas de
/// firmware ni ne déverrouille le bootloader de façon automatique (ces opérations
/// destructives/sécuritaires relèvent d'un module dédié et assisté, non d'un bouton).
/// </summary>
public sealed class FastbootEngine : IFastbootEngine
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(20);

    private readonly IProcessRunner _runner;
    private readonly PlatformToolsLocator _locator;
    private readonly ILogger<FastbootEngine> _logger;

    public FastbootEngine(IProcessRunner runner, PlatformToolsLocator locator, ILogger<FastbootEngine>? logger = null)
    {
        _runner = runner;
        _locator = locator;
        _logger = logger ?? NullLogger<FastbootEngine>.Instance;
    }

    public bool IsAvailable => _locator.FastbootAvailable;
    public string? FastbootPath => _locator.FindFastboot();

    private string RequireFastboot() => FastbootPath ?? throw new ToolNotFoundException(PlatformToolsLocator.FastbootFileName);

    private Task<CommandResult> RunAsync(string args, CancellationToken ct)
        => _runner.RunAsync(RequireFastboot(), args, ShortTimeout, cancellationToken: ct);

    private static string ForDevice(string serial, string rest) => $"-s {serial} {rest}";

    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        if (!IsAvailable) return null;
        var r = await RunAsync("--version", ct).ConfigureAwait(false);
        return r.Success ? r.CombinedOutput : null;
    }

    public async Task<IReadOnlyList<AndroidDevice>> ListDevicesAsync(CancellationToken ct = default)
    {
        if (!IsAvailable) return Array.Empty<AndroidDevice>();
        var r = await RunAsync("devices", ct).ConfigureAwait(false);
        var parsed = AdbOutputParser.ParseFastbootDevices(r.CombinedOutput);
        return parsed.Select(p => new AndroidDevice { Serial = p.Serial, Mode = ConnectionMode.Fastboot }).ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllVariablesAsync(string serial, CancellationToken ct = default)
    {
        var r = await RunAsync(ForDevice(serial, "getvar all"), ct).ConfigureAwait(false);
        // fastboot écrit getvar sur stderr → on analyse la sortie combinée.
        return AdbOutputParser.ParseFastbootVars(r.CombinedOutput);
    }

    public async Task<string?> GetVariableAsync(string serial, string name, CancellationToken ct = default)
    {
        var r = await RunAsync(ForDevice(serial, $"getvar {name}"), ct).ConfigureAwait(false);
        var vars = AdbOutputParser.ParseFastbootVars(r.CombinedOutput);
        return vars.TryGetValue(name, out var value) ? value : null;
    }

    public async Task<bool?> IsBootloaderUnlockedAsync(string serial, CancellationToken ct = default)
    {
        var value = await GetVariableAsync(serial, "unlocked", ct).ConfigureAwait(false);
        return AdbOutputParser.ParseYesNo(value);
    }

    public Task<CommandResult> RebootAsync(string serial, CancellationToken ct = default)
        => RunAsync(ForDevice(serial, "reboot"), ct);

    public Task<CommandResult> RebootBootloaderAsync(string serial, CancellationToken ct = default)
        => RunAsync(ForDevice(serial, "reboot-bootloader"), ct);

    public Task<CommandResult> RebootRecoveryAsync(string serial, CancellationToken ct = default)
        => RunAsync(ForDevice(serial, "reboot recovery"), ct);
}
