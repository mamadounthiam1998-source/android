using System.Diagnostics;
using System.Text;
using GhostAndroid.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GhostAndroid.Core.Infrastructure;

/// <summary>
/// Exécuteur de processus externe robuste : lecture asynchrone de stdout/stderr
/// (sans interblocage), délai d'expiration avec arrêt forcé de l'arborescence de
/// processus, et annulation. C'est la seule porte d'entrée vers adb.exe / fastboot.exe.
/// </summary>
public interface IProcessRunner
{
    Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan? timeout = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner>? logger = null)
        => _logger = logger ?? NullLogger<ProcessRunner>.Instance;

    public async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan? timeout = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (!string.IsNullOrEmpty(workingDirectory))
            psi.WorkingDirectory = workingDirectory;

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) stdoutDone.TrySetResult(true);
            else stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) stderrDone.TrySetResult(true);
            else stderr.AppendLine(e.Data);
        };

        _logger.LogDebug("Exécution: {File} {Args}", fileName, arguments);

        try
        {
            if (!process.Start())
            {
                return new CommandResult
                {
                    FileName = fileName,
                    Arguments = arguments,
                    ExitCode = -1,
                    StandardError = "Impossible de démarrer le processus.",
                    Duration = stopwatch.Elapsed,
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec du démarrage de {File}", fileName);
            return new CommandResult
            {
                FileName = fileName,
                Arguments = arguments,
                ExitCode = -1,
                StandardError = ex.Message,
                Duration = stopwatch.Elapsed,
            };
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            // S'assurer que tous les flux ont été entièrement vidangés.
            await Task.WhenAll(stdoutDone.Task, stderrDone.Task).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeoutCts.IsCancellationRequested;
            TryKill(process);
            if (!timedOut)
            {
                // Annulation explicite demandée par l'appelant.
                throw new OperationCanceledException(cancellationToken);
            }
            _logger.LogWarning("Délai dépassé ({Timeout}s): {File} {Args}",
                effectiveTimeout.TotalSeconds, fileName, arguments);
        }

        stopwatch.Stop();

        var exitCode = timedOut ? -1 : SafeExitCode(process);
        return new CommandResult
        {
            FileName = fileName,
            Arguments = arguments,
            ExitCode = exitCode,
            StandardOutput = stdout.ToString().TrimEnd('\r', '\n'),
            StandardError = stderr.ToString().TrimEnd('\r', '\n'),
            Duration = stopwatch.Elapsed,
            TimedOut = timedOut,
        };
    }

    private static int SafeExitCode(Process p)
    {
        try { return p.ExitCode; }
        catch { return -1; }
    }

    private void TryKill(Process p)
    {
        try
        {
            if (!p.HasExited)
                p.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Impossible d'arrêter le processus {Id}", SafeId(p));
        }
    }

    private static int SafeId(Process p)
    {
        try { return p.Id; }
        catch { return -1; }
    }
}
