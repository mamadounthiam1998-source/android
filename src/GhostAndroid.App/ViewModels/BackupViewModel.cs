using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostAndroid.App.Services;
using GhostAndroid.Core.Backup;
using GhostAndroid.Core.Models;
using GhostAndroid.Core.Storage;

namespace GhostAndroid.App.ViewModels;

public sealed partial class BackupViewModel : ObservableObject
{
    private readonly BackupService _backup;
    private readonly RestoreService _restore;
    private readonly IHistoryRepository _history;
    private readonly IDialogService _dialogs;
    private readonly DeviceContext _context;

    public ObservableCollection<BackupSource> Sources { get; } = new();
    public ObservableCollection<BackupRecord> History { get; } = new();
    public IReadOnlyList<string> KnownLimitations => BackupSources.KnownLimitations;

    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _progressStage = "";
    [ObservableProperty] private string _status = "";

    public DeviceContext Context => _context;

    public BackupViewModel(BackupService backup, RestoreService restore, IHistoryRepository history,
        IDialogService dialogs, DeviceContext context)
    {
        _backup = backup;
        _restore = restore;
        _history = history;
        _dialogs = dialogs;
        _context = context;

        foreach (var s in BackupSources.Default)
            Sources.Add(new BackupSource { Category = s.Category, Label = s.Label, DevicePath = s.DevicePath, Selected = true });

        _ = LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        History.Clear();
        foreach (var r in await _history.GetBackupsAsync())
            History.Add(r);
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        var device = _context.SelectedDevice;
        if (device is null) { _dialogs.Error("Sauvegarde", "Aucun appareil sélectionné."); return; }
        if (device.Mode != ConnectionMode.Adb) { _dialogs.Error("Sauvegarde", "L'appareil doit être en mode ADB (système)."); return; }
        if (Password.Length < 6) { _dialogs.Error("Sauvegarde", "Choisissez un mot de passe d'au moins 6 caractères."); return; }

        var selected = Sources.Where(s => s.Selected).ToList();
        if (selected.Count == 0) { _dialogs.Error("Sauvegarde", "Sélectionnez au moins une catégorie."); return; }

        var defaultName = $"{device.DisplayName}_{DateTime.Now:yyyyMMdd_HHmm}{BackupService.FileExtension}";
        var output = _dialogs.SaveFile($"Sauvegarde GHOST ANDROID (*{BackupService.FileExtension})|*{BackupService.FileExtension}",
            defaultName, "Enregistrer la sauvegarde chiffrée");
        if (output is null) return;

        IsRunning = true;
        Progress = 0;
        var progress = new Progress<BackupProgress>(p =>
        {
            Progress = p.Percent;
            ProgressStage = p.CurrentItem is null ? p.Stage : $"{p.Stage} — {p.CurrentItem}";
        });

        try
        {
            var result = await _backup.BackupDeviceAsync(device, selected, output, Password, progress);
            if (result.Success)
            {
                await _history.AddBackupAsync(new BackupRecord
                {
                    FilePath = result.FilePath,
                    DeviceSerial = device.Serial,
                    DeviceModel = device.DisplayName,
                    TotalBytes = result.Manifest.TotalBytes,
                    FileCount = result.Manifest.FileCount,
                    Encrypted = true,
                    Notes = string.Join(", ", result.Manifest.IncludedCategories),
                });
                await LoadHistoryAsync();
                Status = $"Sauvegarde réussie : {result.Manifest.FileCount} fichiers, {Human(result.Manifest.TotalBytes)}. " +
                         "Rappel : certaines données (SMS, données privées d'apps, partitions) ne sont pas incluses.";
                _dialogs.Alert("Sauvegarde", Status);
            }
            else
            {
                Status = $"Échec : {result.Error}";
                _dialogs.Error("Sauvegarde", result.Error ?? "Erreur inconnue.");
            }
        }
        finally
        {
            IsRunning = false;
            ProgressStage = "";
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var archive = _dialogs.OpenFile($"Sauvegarde GHOST ANDROID (*{BackupService.FileExtension})|*{BackupService.FileExtension}",
            "Choisir une sauvegarde à restaurer");
        if (archive is null) return;

        if (Password.Length == 0) { _dialogs.Error("Restauration", "Saisissez le mot de passe de la sauvegarde."); return; }

        var dest = _dialogs.PickFolder("Dossier de destination");
        if (dest is null) return;

        IsRunning = true;
        try
        {
            var result = await _restore.ExtractArchiveAsync(archive, dest, Password);
            if (!result.Success)
            {
                Status = $"Échec de la restauration : {result.Error}";
                _dialogs.Error("Restauration", result.Error ?? "Erreur inconnue.");
                return;
            }

            if (result.IntegrityVerified)
                Status = $"Restauration réussie : {result.FilesExtracted} fichiers, intégrité vérifiée (SHA-256).";
            else
                Status = $"Restauration terminée avec {result.IntegrityIssues.Count} anomalie(s) d'intégrité.";

            _dialogs.Alert("Restauration", Status);
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task VerifyAsync()
    {
        var archive = _dialogs.OpenFile($"Sauvegarde GHOST ANDROID (*{BackupService.FileExtension})|*{BackupService.FileExtension}",
            "Vérifier une sauvegarde");
        if (archive is null) return;
        if (Password.Length == 0) { _dialogs.Error("Vérification", "Saisissez le mot de passe."); return; }

        try
        {
            var manifest = await _restore.ReadManifestAsync(archive, Password);
            if (manifest is null) { _dialogs.Error("Vérification", "Manifeste illisible."); return; }
            _dialogs.Alert("Manifeste de sauvegarde",
                $"Appareil : {manifest.DeviceModel}\nCréée le : {manifest.CreatedAt:g}\n" +
                $"Fichiers : {manifest.FileCount}\nTaille : {Human(manifest.TotalBytes)}\n" +
                $"Chiffrement : {manifest.EncryptionAlgorithm}\n\nNon inclus :\n- " +
                string.Join("\n- ", manifest.NotIncludedNotes));
        }
        catch (Exception ex)
        {
            _dialogs.Error("Vérification", ex.Message);
        }
    }

    private static string Human(long bytes)
    {
        string[] u = { "o", "Ko", "Mo", "Go", "To" };
        double s = bytes; var i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{s:0.#} {u[i]}";
    }
}
