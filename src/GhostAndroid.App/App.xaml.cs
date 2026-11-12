using System.IO;
using System.Windows;
using System.Windows.Threading;
using GhostAndroid.App.Services;
using GhostAndroid.App.ViewModels;
using GhostAndroid.Core.Adb;
using GhostAndroid.Core.Backup;
using GhostAndroid.Core.Devices;
using GhostAndroid.Core.Diagnostics;
using GhostAndroid.Core.Infrastructure;
using GhostAndroid.Core.PlatformTools;
using GhostAndroid.Core.Security;
using GhostAndroid.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GhostAndroid.App;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services ?? throw new InvalidOperationException("Hôte non initialisé.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GhostAndroid");
        Directory.CreateDirectory(dataDir);

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(l => { l.ClearProviders(); l.AddDebug(); l.SetMinimumLevel(LogLevel.Information); })
            .ConfigureServices((_, s) =>
            {
                // --- Cœur ---
                s.AddSingleton<IProcessRunner, ProcessRunner>();
                s.AddSingleton(_ => new PlatformToolsLocator());
                s.AddSingleton<PlatformToolsInstaller>();
                s.AddSingleton<IAdbEngine, AdbEngine>();
                s.AddSingleton<IFastbootEngine, FastbootEngine>();
                s.AddSingleton<IDeviceDetectionService, DeviceDetectionService>();
                s.AddSingleton(sp => new DeviceMonitor(
                    sp.GetRequiredService<IDeviceDetectionService>(),
                    TimeSpan.FromSeconds(3),
                    sp.GetRequiredService<ILogger<DeviceMonitor>>()));
                s.AddSingleton(_ => new GcmFileCrypto());
                s.AddSingleton<BackupService>();
                s.AddSingleton<RestoreService>();
                s.AddSingleton<DiagnosticService>();
                s.AddSingleton<LockDiagnosticService>();
                s.AddSingleton<IHistoryRepository>(_ =>
                    new HistoryRepository(Path.Combine(dataDir, "ghostandroid.db")));

                // --- Services UI ---
                s.AddSingleton<IDialogService, DialogService>();

                // --- ViewModels ---
                s.AddSingleton<DeviceContext>();
                s.AddSingleton<MainViewModel>();
                s.AddSingleton<DashboardViewModel>();
                s.AddSingleton<ConsoleViewModel>();
                s.AddSingleton<DiagnosticsViewModel>();
                s.AddSingleton<BackupViewModel>();
                s.AddSingleton<SystemViewModel>();
                s.AddSingleton<FirmwareViewModel>();
                s.AddSingleton<UnlockViewModel>();
            })
            .Build();

        await _host.StartAsync();

        // Prépare la base locale.
        await _host.Services.GetRequiredService<IHistoryRepository>().InitializeAsync();

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var window = new MainWindow { DataContext = _host.Services.GetRequiredService<MainViewModel>() };
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Une erreur inattendue s'est produite :\n\n{e.Exception.Message}",
            "GHOST ANDROID", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try { await _host.Services.GetRequiredService<DeviceMonitor>().StopAsync(); } catch { /* ignore */ }
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
