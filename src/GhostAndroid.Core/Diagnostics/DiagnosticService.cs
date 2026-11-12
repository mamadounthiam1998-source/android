using System.Diagnostics;
using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Models;
using GhostAndroid.Core.PlatformTools;

namespace GhostAndroid.Core.Diagnostics;

/// <summary>
/// Exécute une batterie de contrôles réels et renvoie un rapport honnête.
/// Les contrôles qui ne peuvent pas conclure sont marqués « Unknown », jamais « Ok ».
/// </summary>
public sealed class DiagnosticService
{
    private readonly IAdbEngine _adb;
    private readonly IFastbootEngine _fastboot;
    private readonly PlatformToolsLocator _locator;

    public DiagnosticService(IAdbEngine adb, IFastbootEngine fastboot, PlatformToolsLocator locator)
    {
        _adb = adb;
        _fastboot = fastboot;
        _locator = locator;
    }

    /// <summary>Contrôles d'environnement (indépendants d'un appareil).</summary>
    public DiagnosticReport CheckEnvironment()
    {
        var report = new DiagnosticReport();

        report.Checks.Add(Timed("adb", "Présence d'ADB", () =>
            _locator.AdbAvailable
                ? (DiagnosticSeverity.Ok, $"adb trouvé : {_locator.FindAdb()}", null)
                : (DiagnosticSeverity.Error, "adb introuvable.", "Installez les Android Platform Tools (fournis avec l'application).")));

        report.Checks.Add(Timed("fastboot", "Présence de Fastboot", () =>
            _locator.FastbootAvailable
                ? (DiagnosticSeverity.Ok, $"fastboot trouvé : {_locator.FindFastboot()}", null)
                : (DiagnosticSeverity.Warning, "fastboot introuvable.", "Requis uniquement pour les opérations en mode bootloader.")));

        return report;
    }

    /// <summary>Contrôles ciblant un appareil précis.</summary>
    public async Task<DiagnosticReport> CheckDeviceAsync(AndroidDevice device, CancellationToken ct = default)
    {
        var report = new DiagnosticReport { DeviceSerial = device.Serial };

        // 1) Connexion USB / mode
        report.Checks.Add(new DiagnosticCheck
        {
            Id = "connection",
            Title = "État de connexion",
            Severity = device.Mode switch
            {
                ConnectionMode.Adb or ConnectionMode.Fastboot or ConnectionMode.Recovery => DiagnosticSeverity.Ok,
                ConnectionMode.Unauthorized => DiagnosticSeverity.Warning,
                ConnectionMode.Offline => DiagnosticSeverity.Error,
                _ => DiagnosticSeverity.Unknown
            },
            Detail = $"Mode détecté : {device.Mode}.",
            Recommendation = device.Mode == ConnectionMode.Unauthorized
                ? "Acceptez l'invite « Autoriser le débogage USB » sur l'appareil."
                : null,
        });

        if (device.Mode == ConnectionMode.Adb)
        {
            await CheckResponsivenessAsync(report, device, ct).ConfigureAwait(false);
            await CheckBootStateAsync(report, device, ct).ConfigureAwait(false);
            await CheckStorageAsync(report, device, ct).ConfigureAwait(false);
            await CheckBatteryAsync(report, device, ct).ConfigureAwait(false);
        }
        else if (device.Mode == ConnectionMode.Fastboot)
        {
            await CheckFastbootAsync(report, device, ct).ConfigureAwait(false);
        }

        return report;
    }

    private async Task CheckResponsivenessAsync(DiagnosticReport report, AndroidDevice device, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var r = await _adb.ShellAsync(device.Serial, "echo ghost_ok", ct).ConfigureAwait(false);
        sw.Stop();
        report.Checks.Add(new DiagnosticCheck
        {
            Id = "responsive",
            Title = "Réactivité du shell ADB",
            Severity = r.Success && r.StandardOutput.Contains("ghost_ok") ? DiagnosticSeverity.Ok : DiagnosticSeverity.Error,
            Detail = r.Success ? $"Réponse en {sw.ElapsedMilliseconds} ms." : $"Pas de réponse : {r.StandardError}",
            Duration = sw.Elapsed,
        });
    }

    private async Task CheckBootStateAsync(DiagnosticReport report, AndroidDevice device, CancellationToken ct)
    {
        var boot = await _adb.GetPropertyAsync(device.Serial, "sys.boot_completed", ct).ConfigureAwait(false);
        var uptime = await _adb.ShellAsync(device.Serial, "cat /proc/uptime", ct).ConfigureAwait(false);

        double? seconds = null;
        if (uptime.Success)
        {
            var token = uptime.StandardOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (double.TryParse(token, System.Globalization.CultureInfo.InvariantCulture, out var s)) seconds = s;
        }

        DiagnosticSeverity sev;
        string detail;
        string? reco = null;

        if (boot is null)
        {
            sev = DiagnosticSeverity.Unknown;
            detail = "Impossible de lire sys.boot_completed.";
        }
        else if (boot.Trim() == "1")
        {
            // Démarrage OK, mais un uptime très court et répété peut évoquer un redémarrage en boucle.
            if (seconds is < 60)
            {
                sev = DiagnosticSeverity.Warning;
                detail = $"Système démarré mais uptime faible ({seconds:0} s) : surveillez un éventuel redémarrage en boucle.";
                reco = "Si l'appareil redémarre sans cesse, consultez le module Réparation.";
            }
            else
            {
                sev = DiagnosticSeverity.Ok;
                detail = $"Démarrage complet (uptime {seconds:0} s).";
            }
        }
        else
        {
            sev = DiagnosticSeverity.Warning;
            detail = "Le système n'a pas terminé son démarrage (sys.boot_completed ≠ 1).";
            reco = "Possible blocage au démarrage ; vérifiez le recovery/bootloader.";
        }

        report.Checks.Add(new DiagnosticCheck { Id = "boot", Title = "État de démarrage", Severity = sev, Detail = detail, Recommendation = reco });
    }

    private async Task CheckStorageAsync(DiagnosticReport report, AndroidDevice device, CancellationToken ct)
    {
        var df = await _adb.ShellAsync(device.Serial, "df -h /data", ct).ConfigureAwait(false);
        report.Checks.Add(new DiagnosticCheck
        {
            Id = "storage",
            Title = "Espace de stockage (/data)",
            Severity = df.Success ? DiagnosticSeverity.Info : DiagnosticSeverity.Unknown,
            Detail = df.Success ? df.StandardOutput : "Information de stockage indisponible.",
        });
    }

    private async Task CheckBatteryAsync(DiagnosticReport report, AndroidDevice device, CancellationToken ct)
    {
        var level = await _adb.GetBatteryLevelAsync(device.Serial, ct).ConfigureAwait(false);
        report.Checks.Add(new DiagnosticCheck
        {
            Id = "battery",
            Title = "Niveau de batterie",
            Severity = level switch
            {
                null => DiagnosticSeverity.Unknown,
                < 20 => DiagnosticSeverity.Warning,
                _ => DiagnosticSeverity.Ok
            },
            Detail = level is null ? "Niveau indisponible." : $"{level}%",
            Recommendation = level is < 20 ? "Chargez l'appareil à ≥ 50 % avant toute opération sensible (firmware)." : null,
        });
    }

    private async Task CheckFastbootAsync(DiagnosticReport report, AndroidDevice device, CancellationToken ct)
    {
        var vars = await _fastboot.GetAllVariablesAsync(device.Serial, ct).ConfigureAwait(false);
        report.Checks.Add(new DiagnosticCheck
        {
            Id = "fastboot",
            Title = "Mode Fastboot",
            Severity = vars.Count > 0 ? DiagnosticSeverity.Ok : DiagnosticSeverity.Warning,
            Detail = vars.Count > 0
                ? $"{vars.Count} variables lues (product={vars.GetValueOrDefault("product")}, unlocked={vars.GetValueOrDefault("unlocked")})."
                : "Aucune variable lue ; vérifiez les pilotes Fastboot.",
        });
    }

    private static DiagnosticCheck Timed(string id, string title, Func<(DiagnosticSeverity, string, string?)> check)
    {
        var sw = Stopwatch.StartNew();
        var (sev, detail, reco) = check();
        sw.Stop();
        return new DiagnosticCheck { Id = id, Title = title, Severity = sev, Detail = detail, Recommendation = reco, Duration = sw.Elapsed };
    }
}
