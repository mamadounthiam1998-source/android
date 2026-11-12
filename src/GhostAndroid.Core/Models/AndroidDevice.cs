namespace GhostAndroid.Core.Models;

/// <summary>
/// Représente un appareil Android détecté et les informations réellement
/// récupérées à son sujet. Les champs non disponibles restent null : on ne
/// fabrique jamais de valeur fictive.
/// </summary>
public sealed class AndroidDevice
{
    /// <summary>Numéro de série ADB/Fastboot (identifiant de transport).</summary>
    public required string Serial { get; init; }

    /// <summary>Mode de connexion détecté.</summary>
    public ConnectionMode Mode { get; init; } = ConnectionMode.Unknown;

    /// <summary>Fabricant (ro.product.manufacturer), si disponible.</summary>
    public string? Manufacturer { get; set; }

    /// <summary>Marque (ro.product.brand), si disponible.</summary>
    public string? Brand { get; set; }

    /// <summary>Modèle commercial (ro.product.model), si disponible.</summary>
    public string? Model { get; set; }

    /// <summary>Nom de code du produit (ro.product.device), si disponible.</summary>
    public string? Codename { get; set; }

    /// <summary>Version Android (ro.build.version.release), si disponible.</summary>
    public string? AndroidVersion { get; set; }

    /// <summary>Niveau d'API SDK (ro.build.version.sdk), si disponible.</summary>
    public int? SdkLevel { get; set; }

    /// <summary>Numéro de build (ro.build.display.id / ro.build.id), si disponible.</summary>
    public string? BuildNumber { get; set; }

    /// <summary>Niveau de batterie en pourcentage (0-100), si disponible.</summary>
    public int? BatteryLevel { get; set; }

    /// <summary>true si la batterie est en charge, si l'information est disponible.</summary>
    public bool? BatteryCharging { get; set; }

    /// <summary>État du verrouillage du bootloader, si connu.</summary>
    public bool? BootloaderUnlocked { get; set; }

    /// <summary>Indique si l'appareil répond aux commandes (sys.boot_completed = 1).</summary>
    public bool? BootCompleted { get; set; }

    /// <summary>Description transport renvoyée par `adb devices -l` (model:, product:, etc.).</summary>
    public string? TransportDescription { get; set; }

    /// <summary>Horodatage de la dernière mise à jour des informations.</summary>
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.Now;

    /// <summary>Libellé lisible pour l'interface ("Samsung Galaxy S21" ou le série à défaut).</summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Manufacturer) && !string.IsNullOrWhiteSpace(Model))
                return $"{Capitalize(Manufacturer)} {Model}";
            if (!string.IsNullOrWhiteSpace(Model))
                return Model!;
            if (!string.IsNullOrWhiteSpace(Brand))
                return Capitalize(Brand);
            return Serial;
        }
    }

    public bool IsActionable =>
        Mode is ConnectionMode.Adb or ConnectionMode.Recovery or ConnectionMode.Fastboot or ConnectionMode.Sideload;

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
