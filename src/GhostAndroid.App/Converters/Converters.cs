using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GhostAndroid.Core.Models;

namespace GhostAndroid.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility.Collapsed;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object p, CultureInfo c)
        => value is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is not true;
}

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var key = value is DiagnosticSeverity s ? s switch
        {
            DiagnosticSeverity.Ok => "Success",
            DiagnosticSeverity.Info => "AccentBlue",
            DiagnosticSeverity.Warning => "Warning",
            DiagnosticSeverity.Error => "Danger",
            _ => "TextSecondary"
        } : "TextSecondary";
        return Application.Current.Resources[key] as Brush ?? Brushes.Gray;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class ConnectionModeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var key = value is ConnectionMode m ? m switch
        {
            ConnectionMode.Adb => "Success",
            ConnectionMode.Fastboot => "AccentBlue",
            ConnectionMode.Recovery => "AccentCyan",
            ConnectionMode.Sideload => "AccentCyan",
            ConnectionMode.Unauthorized => "Warning",
            ConnectionMode.Offline => "Danger",
            _ => "TextSecondary"
        } : "TextSecondary";
        return Application.Current.Resources[key] as Brush ?? Brushes.Gray;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class BytesToHumanConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not long bytes) return "—";
        string[] units = { "o", "Ko", "Mo", "Go", "To" };
        double size = bytes;
        var i = 0;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return $"{size:0.#} {units[i]}";
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
