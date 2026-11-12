using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostAndroid.App.Services;
using GhostAndroid.Core.Commands;
using GhostAndroid.Core.Infrastructure;
using GhostAndroid.Core.Models;
using GhostAndroid.Core.PlatformTools;

namespace GhostAndroid.App.ViewModels;

/// <summary>
/// Console ADB/Fastboot réelle. Chaque commande est classée (CommandGuard) ; les
/// commandes destructives exigent une confirmation explicite avant exécution.
/// </summary>
public sealed partial class ConsoleViewModel : ObservableObject
{
    private readonly IProcessRunner _runner;
    private readonly PlatformToolsLocator _locator;
    private readonly IDialogService _dialogs;
    private readonly DeviceContext _context;
    private readonly StringBuilder _log = new();

    [ObservableProperty] private bool _fastbootMode;
    [ObservableProperty] private string _commandText = "";
    [ObservableProperty] private string _output = "GHOST ANDROID — console. Les commandes « adb » et « fastboot » sont préfixées automatiquement.\n";
    [ObservableProperty] private bool _isBusy;

    public DeviceContext Context => _context;

    public ConsoleViewModel(IProcessRunner runner, PlatformToolsLocator locator, IDialogService dialogs, DeviceContext context)
    {
        _runner = runner;
        _locator = locator;
        _dialogs = dialogs;
        _context = context;
    }

    private string Tool => FastbootMode ? "fastboot" : "adb";

    [RelayCommand]
    private Task RunAsync() => ExecuteAsync(CommandText);

    [RelayCommand]
    private async Task QuickAsync(string preset)
    {
        var device = _context.SelectedDevice;
        var serial = device?.Serial;

        var (tool, args) = preset switch
        {
            "devices" => ("adb", "devices -l"),
            "getprop" => ("adb", "shell getprop"),
            "battery" => ("adb", "shell dumpsys battery"),
            "logcat" => ("adb", "logcat -d -t 500"),
            "reboot" => ("adb", "reboot"),
            "recovery" => ("adb", "reboot recovery"),
            "bootloader" => ("adb", "reboot bootloader"),
            "fb-devices" => ("fastboot", "devices"),
            "fb-getvar" => ("fastboot", "getvar all"),
            "fb-reboot" => ("fastboot", "reboot"),
            _ => ("adb", "version"),
        };

        await ExecuteRawAsync(tool, args, serial);
    }

    private async Task ExecuteAsync(string input)
    {
        input = input.Trim();
        if (input.Length == 0) return;

        var tool = Tool;
        // Permet à l'utilisateur de taper "adb ..." ou "fastboot ..." explicitement.
        if (input.StartsWith("adb ", StringComparison.OrdinalIgnoreCase)) { tool = "adb"; input = input[4..]; }
        else if (input.StartsWith("fastboot ", StringComparison.OrdinalIgnoreCase)) { tool = "fastboot"; input = input[9..]; }

        await ExecuteRawAsync(tool, input, _context.SelectedDevice?.Serial);
        CommandText = "";
    }

    private async Task ExecuteRawAsync(string tool, string args, string? serial)
    {
        var path = tool == "fastboot" ? _locator.FindFastboot() : _locator.FindAdb();
        if (path is null)
        {
            Append($"[erreur] {tool} introuvable. Installez les Android Platform Tools.");
            return;
        }

        // Insère -s <serial> si un appareil est sélectionné et non déjà ciblé.
        var fullArgs = args;
        if (!string.IsNullOrEmpty(serial) && !args.Contains("-s ", StringComparison.Ordinal) && !args.StartsWith("devices"))
            fullArgs = $"-s {serial} {args}";

        var classification = CommandGuard.Classify(tool, fullArgs);
        if (classification.Risk == CommandRisk.Destructive)
        {
            if (!_dialogs.ConfirmDestructive("Commande sensible",
                    $"{tool} {fullArgs}\n\n{classification.Reason}"))
            {
                Append($"[annulé] {tool} {fullArgs}");
                return;
            }
        }

        Append($"> {tool} {fullArgs}");
        IsBusy = true;
        try
        {
            var result = await _runner.RunAsync(path, fullArgs, TimeSpan.FromMinutes(5));
            if (!string.IsNullOrWhiteSpace(result.CombinedOutput))
                Append(result.CombinedOutput.TrimEnd());
            Append($"[exit {result.ExitCode}{(result.TimedOut ? " · délai dépassé" : "")}] ({result.Duration.TotalMilliseconds:0} ms)\n");
        }
        catch (Exception ex)
        {
            Append($"[exception] {ex.Message}\n");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Append(string text)
    {
        _log.AppendLine(text);
        Output = _log.ToString();
    }

    [RelayCommand]
    private void Clear()
    {
        _log.Clear();
        Output = "";
    }
}
