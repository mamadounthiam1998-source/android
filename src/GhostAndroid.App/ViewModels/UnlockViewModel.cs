using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostAndroid.App.Services;
using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Models;
using GhostAndroid.Core.Security;

namespace GhostAndroid.App.ViewModels;

/// <summary>
/// Module « Verrous et accès ». GHOST ANDROID DIAGNOSTIQUE l'état de sécurité en
/// lecture seule et n'effectue AUCUN contournement de verrou d'écran ni de protection
/// du fabricant (FRP). La seule reprise d'accès proposée est une réinitialisation
/// d'usine AUTORISÉE, initiée par l'utilisateur, qui efface les données.
/// </summary>
public sealed partial class UnlockViewModel : ObservableObject
{
    private readonly LockDiagnosticService _lockDiag;
    private readonly IAdbEngine _adb;
    private readonly IDialogService _dialogs;
    private readonly DeviceContext _context;

    public ObservableCollection<string> Notes { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _storageEncryption = "—";
    [ObservableProperty] private string _oemUnlock = "—";
    [ObservableProperty] private string _screenLock = "Non lisible (aucun contournement effectué)";

    public string Policy =>
        "GHOST ANDROID ne contourne jamais un verrou d'écran ni une protection de réinitialisation (FRP). " +
        "Ce module fournit un diagnostic en lecture seule. En cas d'oubli de code, la seule option prise en " +
        "charge est une réinitialisation d'usine autorisée (effacement total des données), à confirmer explicitement.";

    public DeviceContext Context => _context;

    public UnlockViewModel(LockDiagnosticService lockDiag, IAdbEngine adb, IDialogService dialogs, DeviceContext context)
    {
        _lockDiag = lockDiag;
        _adb = adb;
        _dialogs = dialogs;
        _context = context;
    }

    [RelayCommand]
    private async Task DiagnoseAsync()
    {
        var device = _context.SelectedDevice;
        if (device is null) { _dialogs.Error("Verrous", "Aucun appareil sélectionné."); return; }
        if (device.Mode != ConnectionMode.Adb) { _dialogs.Error("Verrous", "L'appareil doit être en mode ADB (système)."); return; }

        IsBusy = true;
        Notes.Clear();
        try
        {
            var info = await _lockDiag.InspectAsync(device);
            StorageEncryption = info.StorageEncrypted switch { true => "Chiffré", false => "Non chiffré", null => "Inconnu" };
            OemUnlock = info.OemUnlockAllowed switch { true => "Autorisé", false => "Non autorisé", null => "Inconnu" };
            foreach (var n in info.Notes) Notes.Add(n);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task FactoryResetAsync()
    {
        var device = _context.SelectedDevice;
        if (device is null) { _dialogs.Error("Réinitialisation", "Aucun appareil sélectionné."); return; }

        var ok = _dialogs.ConfirmDestructive(
            "Réinitialisation d'usine autorisée",
            "Vous demandez une réinitialisation d'usine. TOUTES les données de l'appareil seront effacées.\n\n" +
            "N'effectuez cette opération que sur un appareil vous appartenant ou pour lequel vous êtes autorisé. " +
            "GHOST ANDROID va redémarrer l'appareil vers le mode Recovery, où la réinitialisation doit être " +
            "confirmée manuellement sur l'appareil (procédure officielle).");

        if (!ok) return;

        var result = await _adb.RebootAsync(device.Serial, "recovery");
        if (result.Success)
            _dialogs.Alert("Réinitialisation",
                "Appareil redémarré vers Recovery. Sélectionnez « Wipe data/factory reset » sur l'appareil pour terminer.");
        else
            _dialogs.Error("Réinitialisation", result.CombinedOutput);
    }
}
