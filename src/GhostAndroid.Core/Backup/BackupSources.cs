using GhostAndroid.Core.Models;

namespace GhostAndroid.Core.Backup;

/// <summary>
/// Emplacements du stockage partagé réellement accessibles via ADB sans root.
/// Ce sont les dossiers que GHOST ANDROID peut sauvegarder honnêtement.
/// </summary>
public static class BackupSources
{
    public static IReadOnlyList<BackupSource> Default { get; } = new List<BackupSource>
    {
        new() { Category = BackupCategory.Photos,    Label = "Photos (DCIM)",     DevicePath = "/sdcard/DCIM" },
        new() { Category = BackupCategory.Photos,    Label = "Images",            DevicePath = "/sdcard/Pictures" },
        new() { Category = BackupCategory.Videos,    Label = "Vidéos",            DevicePath = "/sdcard/Movies" },
        new() { Category = BackupCategory.Music,     Label = "Musique",           DevicePath = "/sdcard/Music" },
        new() { Category = BackupCategory.Documents, Label = "Documents",         DevicePath = "/sdcard/Documents" },
        new() { Category = BackupCategory.Downloads, Label = "Téléchargements",   DevicePath = "/sdcard/Download" },
    };

    /// <summary>
    /// Données que GHOST ANDROID NE peut PAS inclure sans accès privilégié (root)
    /// ou sans méthode officielle. Affiché clairement à l'utilisateur.
    /// </summary>
    public static IReadOnlyList<string> KnownLimitations { get; } = new List<string>
    {
        "SMS/MMS et journaux d'appels : non accessibles via ADB sans application dédiée ou root.",
        "Données privées des applications (/data/data) : non accessibles sans root.",
        "Partitions système et de démarrage : non sauvegardées (opération non destructive uniquement).",
        "Contacts : exportables uniquement si l'appareil/une app autorise l'export VCF.",
        "Mots de passe et jetons d'authentification : jamais extraits.",
    };
}
