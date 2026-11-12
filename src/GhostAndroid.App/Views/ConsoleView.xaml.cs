using System.Windows.Controls;

namespace GhostAndroid.App.Views;

public partial class ConsoleView : UserControl
{
    public ConsoleView() => InitializeComponent();

    // Auto-défilement vers le bas quand la sortie change.
    private void OnOutputChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb) tb.ScrollToEnd();
    }
}
