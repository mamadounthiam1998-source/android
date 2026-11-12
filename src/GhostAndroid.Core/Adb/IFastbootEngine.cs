using GhostAndroid.Core.Models;

namespace GhostAndroid.Core.Adb;

/// <summary>Moteur Fastboot : enveloppe réelle autour de l'exécutable fastboot officiel.</summary>
public interface IFastbootEngine
{
    bool IsAvailable { get; }
    string? FastbootPath { get; }

    Task<string?> GetVersionAsync(CancellationToken ct = default);

    /// <summary>Liste les appareils en mode fastboot (`fastboot devices`).</summary>
    Task<IReadOnlyList<AndroidDevice>> ListDevicesAsync(CancellationToken ct = default);

    /// <summary>Lit toutes les variables (`getvar all`).</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllVariablesAsync(string serial, CancellationToken ct = default);

    /// <summary>Lit une variable unique (`getvar &lt;name&gt;`).</summary>
    Task<string?> GetVariableAsync(string serial, string name, CancellationToken ct = default);

    /// <summary>État du verrouillage du bootloader, s'il est déterminable (getvar unlocked).</summary>
    Task<bool?> IsBootloaderUnlockedAsync(string serial, CancellationToken ct = default);

    /// <summary>Redémarre vers le système.</summary>
    Task<CommandResult> RebootAsync(string serial, CancellationToken ct = default);

    /// <summary>Redémarre vers le bootloader.</summary>
    Task<CommandResult> RebootBootloaderAsync(string serial, CancellationToken ct = default);

    /// <summary>Redémarre vers le recovery.</summary>
    Task<CommandResult> RebootRecoveryAsync(string serial, CancellationToken ct = default);
}
