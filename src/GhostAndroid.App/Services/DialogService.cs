using System.Windows;
using Microsoft.Win32;

namespace GhostAndroid.App.Services;

public interface IDialogService
{
    bool Confirm(string title, string message);
    bool ConfirmDestructive(string title, string message);
    void Alert(string title, string message);
    void Error(string title, string message);
    string? OpenFile(string filter, string title);
    string? SaveFile(string filter, string defaultName, string title);
    string? PickFolder(string title);
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public bool ConfirmDestructive(string title, string message)
        => MessageBox.Show(
               message + "\n\nCette opération peut être irréversible. Confirmer ?",
               "⚠ " + title, MessageBoxButton.YesNo, MessageBoxImage.Warning,
               MessageBoxResult.No) == MessageBoxResult.Yes;

    public void Alert(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public string? OpenFile(string filter, string title)
    {
        var dlg = new OpenFileDialog { Filter = filter, Title = title };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? SaveFile(string filter, string defaultName, string title)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = defaultName, Title = title };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dlg = new OpenFolderDialog { Title = title };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }
}
