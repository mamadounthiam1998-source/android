using System.Globalization;
using System.Text.RegularExpressions;
using GhostAndroid.Core.Models;

namespace GhostAndroid.Core.Adb;

/// <summary>
/// Analyse des sorties texte d'adb/fastboot. Fonctions pures, sans effet de bord,
/// afin d'être testées intégralement sans appareil physique.
/// </summary>
public static partial class AdbOutputParser
{
    public readonly record struct ParsedDevice(string Serial, ConnectionMode Mode, string? Description);

    /// <summary>Analyse la sortie de `adb devices -l`.</summary>
    public static IReadOnlyList<ParsedDevice> ParseAdbDevices(string? output)
    {
        var result = new List<ParsedDevice>();
        if (string.IsNullOrWhiteSpace(output)) return result;

        foreach (var raw in SplitLines(output))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("*", StringComparison.Ordinal)) continue; // messages du démon adb
            if (line.StartsWith("adb server", StringComparison.OrdinalIgnoreCase)) continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var serial = parts[0];
            var state = parts[1].ToLowerInvariant();
            var mode = MapAdbState(state);
            var description = parts.Length > 2 ? string.Join(' ', parts[2..]) : null;

            result.Add(new ParsedDevice(serial, mode, description));
        }
        return result;
    }

    /// <summary>Analyse la sortie de `fastboot devices`.</summary>
    public static IReadOnlyList<ParsedDevice> ParseFastbootDevices(string? output)
    {
        var result = new List<ParsedDevice>();
        if (string.IsNullOrWhiteSpace(output)) return result;

        foreach (var raw in SplitLines(output))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            var serial = parts[0];
            // La 2e colonne est généralement "fastboot".
            result.Add(new ParsedDevice(serial, ConnectionMode.Fastboot, parts.Length > 1 ? parts[1] : null));
        }
        return result;
    }

    private static ConnectionMode MapAdbState(string state) => state switch
    {
        "device" => ConnectionMode.Adb,
        "recovery" => ConnectionMode.Recovery,
        "sideload" => ConnectionMode.Sideload,
        "bootloader" or "fastboot" => ConnectionMode.Fastboot,
        "unauthorized" => ConnectionMode.Unauthorized,
        "offline" => ConnectionMode.Offline,
        "no" => ConnectionMode.Unauthorized, // "no permissions"
        _ => ConnectionMode.Unknown
    };

    /// <summary>
    /// Analyse la sortie complète de `getprop` au format `[clé]: [valeur]`.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseGetProp(string? output)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(output)) return dict;

        foreach (Match m in GetPropLine().Matches(output))
        {
            var key = m.Groups["k"].Value;
            var value = m.Groups["v"].Value;
            if (key.Length > 0)
                dict[key] = value;
        }
        return dict;
    }

    /// <summary>Extrait le niveau de batterie (0-100) de la sortie `dumpsys battery`.</summary>
    public static int? ParseBatteryLevel(string? dumpsysOutput)
    {
        if (string.IsNullOrWhiteSpace(dumpsysOutput)) return null;
        var m = BatteryLevelLine().Match(dumpsysOutput);
        if (m.Success && int.TryParse(m.Groups["v"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return Math.Clamp(v, 0, 100);
        return null;
    }

    /// <summary>Détermine l'état de charge depuis `dumpsys battery` (AC/USB/Wireless powered ou status=2).</summary>
    public static bool? ParseBatteryCharging(string? dumpsysOutput)
    {
        if (string.IsNullOrWhiteSpace(dumpsysOutput)) return null;

        foreach (var (key, _) in new[] { ("AC powered", 0), ("USB powered", 0), ("Wireless powered", 0) })
        {
            var m = Regex.Match(dumpsysOutput, Regex.Escape(key) + @"\s*:\s*(true|false)", RegexOptions.IgnoreCase);
            if (m.Success && bool.TryParse(m.Groups[1].Value, out var b) && b)
                return true;
        }

        var status = Regex.Match(dumpsysOutput, @"status\s*:\s*(?<v>\d+)", RegexOptions.IgnoreCase);
        if (status.Success && int.TryParse(status.Groups["v"].Value, out var s))
            return s == 2; // 2 = charging (BatteryManager.BATTERY_STATUS_CHARGING)

        return null;
    }

    /// <summary>
    /// Analyse la sortie de `fastboot getvar all` / `getvar &lt;x&gt;` (lignes `(bootloader) clé: valeur`
    /// ou `clé: valeur`). fastboot écrit ces lignes sur stderr : passez la sortie combinée.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseFastbootVars(string? output)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(output)) return dict;

        foreach (var raw in SplitLines(output))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("(bootloader)", StringComparison.OrdinalIgnoreCase))
                line = line["(bootloader)".Length..].Trim();

            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            if (key.Length == 0) continue;
            if (key.StartsWith("finished", StringComparison.OrdinalIgnoreCase)) continue;
            if (key.StartsWith("all", StringComparison.OrdinalIgnoreCase) && value.Length == 0) continue;
            dict[key] = value;
        }
        return dict;
    }

    /// <summary>Interprète une valeur booléenne fastboot ("yes"/"true"/"1" → true).</summary>
    public static bool? ParseYesNo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "yes" or "true" or "1" => true,
            "no" or "false" or "0" => false,
            _ => null
        };
    }

    /// <summary>Analyse `pm list packages` → liste de noms de paquets.</summary>
    public static IReadOnlyList<string> ParsePackageList(string? output)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(output)) return list;
        foreach (var raw in SplitLines(output))
        {
            var line = raw.Trim();
            const string prefix = "package:";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                line = line[prefix.Length..].Trim();
            if (line.Length > 0)
                list.Add(line);
        }
        return list;
    }

    private static IEnumerable<string> SplitLines(string s) =>
        s.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

    [GeneratedRegex(@"^\s*\[(?<k>[^\]]*)\]\s*:\s*\[(?<v>[^\]]*)\]\s*$", RegexOptions.Multiline)]
    private static partial Regex GetPropLine();

    [GeneratedRegex(@"\blevel\s*:\s*(?<v>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex BatteryLevelLine();
}
