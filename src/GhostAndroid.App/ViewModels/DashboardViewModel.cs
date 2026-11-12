using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GhostAndroid.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly DeviceContext _context;

    public ObservableCollection<DeviceCardViewModel> Cards { get; } = new();

    [ObservableProperty] private DeviceCardViewModel? _selectedCard;
    [ObservableProperty] private bool _isScanning;

    public bool HasDevices => Cards.Count > 0;
    public bool NoDevices => Cards.Count == 0;

    public DashboardViewModel(DeviceContext context)
    {
        _context = context;
        _context.Updated += Sync;
        Sync();
    }

    private void Sync()
    {
        // Retire les cartes disparues.
        for (var i = Cards.Count - 1; i >= 0; i--)
        {
            if (_context.Devices.All(d => !string.Equals(d.Serial, Cards[i].Serial, StringComparison.OrdinalIgnoreCase)))
                Cards.RemoveAt(i);
        }

        // Ajoute/rafraîchit.
        foreach (var dev in _context.Devices)
        {
            var card = Cards.FirstOrDefault(c => string.Equals(c.Serial, dev.Serial, StringComparison.OrdinalIgnoreCase));
            if (card is null)
                Cards.Add(new DeviceCardViewModel(dev));
            else
                card.Refresh();
        }

        SelectedCard ??= Cards.FirstOrDefault();
        IsScanning = _context.IsScanning;
        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(NoDevices));
    }

    partial void OnSelectedCardChanged(DeviceCardViewModel? value)
    {
        if (value is not null)
            _context.SelectedDevice = value.Device;
    }

    [RelayCommand]
    private async Task DetectAsync()
    {
        IsScanning = true;
        try { await _context.ScanNowAsync(); }
        finally { IsScanning = false; Sync(); }
    }
}
