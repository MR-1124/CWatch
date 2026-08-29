using System.Windows;
using CWatch.Analysis.Growth;
using CWatch.Analysis.Recurring;
using CWatch.Analysis.Reports;
using CWatch.Analysis.Scanning;
using CWatch.Analysis.Storage;
using CWatch.Analysis.Trends;
using CWatch.Cleanup.Engine;
using CWatch.Infrastructure.Config;
using CWatch.Infrastructure.Logging;
using CWatch.Infrastructure.WindowsApi;
using CWatch.Monitoring.DriveMonitor;
using CWatch.Storage.Database;
using CWatch.Storage.Repositories;
using CWatch.UI.ViewModels;

namespace CWatch.UI;

public partial class App : Application
{
    private FileLoggerService? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global Exception Handling
        _logger = new FileLoggerService();
        _logger.LogInfo("C:Watch Application Starting...");

        DispatcherUnhandledException += (s, args) =>
        {
            _logger.LogError("Unhandled UI Dispatcher Exception", args.Exception);
            args.Handled = true;
            MessageBox.Show($"An unexpected error occurred:\n{args.Exception.Message}", "C:Watch Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        // Initialize Services & Dependency Graph
        var settingsService = new SettingsService(_logger);
        var dbManager = new DatabaseManager(null, _logger);
        var snapshotRepo = new SnapshotRepository(dbManager, _logger);
        var processInspector = new ProcessInspector(_logger);
        var scanner = new FileSystemScanner(_logger);
        var storageAnalyzer = new StorageAnalyzer(_logger);
        var growthAnalyzer = new GrowthAnalyzer(snapshotRepo, _logger);
        var recurringDetector = new RecurringGrowthDetector(_logger);
        var trendAnalyzer = new TrendAnalyzer();
        var reportGenerator = new StorageReportGenerator(growthAnalyzer, recurringDetector);
        var cleanupEngine = new CleanupEngine(processInspector, snapshotRepo, _logger);
        var driveMonitor = new DriveMonitorService(storageAnalyzer, snapshotRepo, settingsService, _logger);

        var mainVm = new MainViewModel(
            scanner,
            storageAnalyzer,
            snapshotRepo,
            cleanupEngine,
            driveMonitor,
            settingsService,
            growthAnalyzer,
            recurringDetector,
            trendAnalyzer,
            reportGenerator,
            _logger);

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var mainWindow = new MainWindow
        {
            DataContext = mainVm
        };

        MainWindow = mainWindow;
        mainWindow.Show();

        _ = mainVm.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInfo("C:Watch Application Exiting.");
        _logger?.Dispose();
        base.OnExit(e);
    }
}
