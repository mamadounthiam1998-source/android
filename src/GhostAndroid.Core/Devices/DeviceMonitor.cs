using GhostAndroid.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GhostAndroid.Core.Devices;

public sealed class DevicesChangedEventArgs : EventArgs
{
    public required IReadOnlyList<AndroidDevice> Devices { get; init; }
    public IReadOnlyList<AndroidDevice> Added { get; init; } = Array.Empty<AndroidDevice>();
    public IReadOnlyList<string> Removed { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Surveille en continu les appareils connectés par scrutation périodique et
/// publie un événement à chaque changement (branchement/débranchement). Alimente
/// la « détection animée » du tableau de bord.
/// </summary>
public sealed class DeviceMonitor : IAsyncDisposable
{
    private readonly IDeviceDetectionService _detection;
    private readonly ILogger<DeviceMonitor> _logger;
    private readonly TimeSpan _interval;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private Dictionary<string, AndroidDevice> _current = new(StringComparer.OrdinalIgnoreCase);

    public DeviceMonitor(IDeviceDetectionService detection, TimeSpan? interval = null, ILogger<DeviceMonitor>? logger = null)
    {
        _detection = detection;
        _interval = interval ?? TimeSpan.FromSeconds(3);
        _logger = logger ?? NullLogger<DeviceMonitor>.Instance;
    }

    public event EventHandler<DevicesChangedEventArgs>? DevicesChanged;
    public event EventHandler<Exception>? ScanFailed;

    public bool IsRunning => _loop is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        try { if (_loop is not null) await _loop.ConfigureAwait(false); }
        catch (OperationCanceledException) { /* arrêt normal */ }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loop = null;
        }
    }

    /// <summary>Force un scan immédiat (ex: bouton « Détecter »).</summary>
    public async Task<IReadOnlyList<AndroidDevice>> ScanOnceAsync(CancellationToken ct = default)
    {
        var devices = await _detection.DetectAndEnrichAsync(ct).ConfigureAwait(false);
        Publish(devices);
        return devices;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var devices = await _detection.DetectAndEnrichAsync(ct).ConfigureAwait(false);
                Publish(devices);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec d'un cycle de détection");
                ScanFailed?.Invoke(this, ex);
            }

            try { await Task.Delay(_interval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void Publish(IReadOnlyList<AndroidDevice> devices)
    {
        var next = devices.ToDictionary(d => d.Serial, StringComparer.OrdinalIgnoreCase);

        var added = next.Keys.Except(_current.Keys, StringComparer.OrdinalIgnoreCase)
                              .Select(k => next[k]).ToList();
        var removed = _current.Keys.Except(next.Keys, StringComparer.OrdinalIgnoreCase).ToList();

        _current = next;

        if (added.Count > 0 || removed.Count > 0 || devices.Count > 0)
        {
            DevicesChanged?.Invoke(this, new DevicesChangedEventArgs
            {
                Devices = devices,
                Added = added,
                Removed = removed,
            });
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
