using System.Windows;
using System.Windows.Controls;
using GhostAndroid.App.ViewModels;

namespace GhostAndroid.App.Views;

public partial class BackupView : UserControl
{
    public BackupView() => InitializeComponent();

    // PasswordBox n'est pas liable (sécurité) : on transmet la valeur au ViewModel.
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupViewModel vm && sender is PasswordBox pb)
            vm.Password = pb.Password;
    }
}
