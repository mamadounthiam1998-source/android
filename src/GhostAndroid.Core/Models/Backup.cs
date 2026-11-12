namespace GhostAndroid.Core.Models;

/// <summary>Catégorie de données sauvegardables accessibles via ADB/MTP.</summary>
public enum BackupCategory
{
    Photos,
    Videos,
    Music,
    Documents,
    Downloads,
    Contacts,
    AppData,
    Other
}

/// <summary>
/// Un emplacement source sur l'appareil à inclure dans la sauvegarde.
/// Le chemin correspond à un dossier réellement accessible du stockage partagé.
/// </summary>
public sealed class BackupSource
{
    public required BackupCategory Category { get; init; }
    public required string Label { get; init; }
    /// <summary>Chemin sur l'appareil (ex: /sdcard/DCIM).</summary>
    public required string DevicePath { get; init; }
    public bool Selected { get; set; } = true;

    /// <summary>
    /// true si l'accès à cette catégorie dépend d'une autorisation particulière ou
    /// peut être restreint par Android (scoped storage, etc.).
    /// </summary>
    public bool MayBeRestricted { get; init; }
}

/// <summary>Une entrée réellement archivée (fichier) dans une sauvegarde.</summary>
public sealed class BackupEntry
{
    public required string RelativePath { get; init; }
    public long SizeBytes { get; init; }
    public string? Sha256 { get; init; }
}

/// <summary>
/// Manifeste décrivant exactement ce qu'une sauvegarde contient — et, tout aussi
/// important, ce qu'elle NE contient pas en raison des restrictions Android.
/// Ce manifeste est stocké en clair à l'intérieur de l'archive chiffrée.
/// </summary>
public sealed class BackupManifest
{
    public string FormatVersion { get; init; } = "1.0";
    public string ProductName { get; init; } = "GHOST ANDROID";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public string? DeviceSerial { get; set; }
    public string? DeviceModel { get; set; }
    public string? AndroidVersion { get; set; }

    public List<string> IncludedCategories { get; set; } = new();
    public List<BackupEntry> Entries { get; set; } = new();

    /// <summary>
    /// Données explicitement NON incluses et la raison (ex: SMS, données d'application
    /// protégées, partitions système non accessibles sans droits root).
    /// Toujours renseigné honnêtement : une sauvegarde de fichiers n'est jamais présentée
    /// comme une image intégrale de l'appareil.
    /// </summary>
    public List<string> NotIncludedNotes { get; set; } = new();

    public bool Encrypted { get; set; } = true;
    public string EncryptionAlgorithm { get; set; } = "AES-256-GCM";
    public long TotalBytes => Entries.Sum(e => e.SizeBytes);
    public int FileCount => Entries.Count;
}

/// <summary>Enregistrement d'historique persistant (SQLite) d'une sauvegarde.</summary>
public sealed class BackupRecord
{
    public long Id { get; set; }
    public required string FilePath { get; init; }
    public string? DeviceSerial { get; init; }
    public string? DeviceModel { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public long TotalBytes { get; init; }
    public int FileCount { get; init; }
    public bool Encrypted { get; init; }
    public string? Notes { get; init; }
}
