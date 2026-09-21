using System.Windows;
using CWatch.Core.Interfaces;
using CWatch.UI.Services;
using CWatch.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CWatch.UI;

public partial class App : Application
{
    private ServiceProvider? _services;
    private ILoggerService? _logger;

    private DateTime _lastErrorMessageTime = DateTime.MinValue;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global Exception Handling
        _services = AppComposition.BuildServices();
        _logger = _services.GetRequiredService<ILoggerService>();
        _logger.LogInfo("C:Watch Application Starting...");

        DispatcherUnhandledException += (s, args) =>
        {
            _logger.LogError("Unhandled UI Dispatcher Exception", args.Exception);
            args.Handled = true;

            // Debounce popup to avoid cascading dialogue loops
            if ((DateTime.UtcNow - _lastErrorMessageTime).TotalSeconds > 3)
            {
                _lastErrorMessageTime = DateTime.UtcNow;
                MessageBox.Show($"An unexpected error occurred:\n{args.Exception.Message}", "C:Watch Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                _logger.LogError("Unhandled AppDomain Exception", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            _logger.LogError("Unobserved Task Exception", args.Exception);
            args.SetObserved();
        };

        // Splash: instant visual feedback while services and data initialize.
        var splash = new SplashWindow();
        MainWindow = splash;
        splash.Show();
        splash.SetStatus("Preparing services");

        // Initialize Theme Engine
        ThemeManager.Instance.Initialize(ThemeMode.Dark);

        splash.SetStatus("Loading settings");
        var mainVm = _services.GetRequiredService<MainViewModel>();
        var mainWindow = _services.GetRequiredService<MainWindow>();

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Fast init: schema/prune/dashboard load off the UI thread's critical path,
        // then swap the splash for the main window. A failed init still shows the
        // main window — pages surface their own error states.
        splash.SetStatus("Reading drive history");
        try
        {
            await mainVm.InitializeAsync();
            splash.SetStatus("Ready");
        }
        catch (Exception ex)
        {
            _logger.LogError("Startup initialization failed; continuing with empty state.", ex);
        }

        MainWindow = mainWindow;
        mainWindow.Show();
        splash.CloseWithFade();

        _logger.LogInfo("Startup complete.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInfo("C:Watch Application Exiting.");

        // Disposes IDisposable singletons (FileLoggerService, DriveMonitorService timer, ...)
        _services?.Dispose();

        base.OnExit(e);
    }
}
