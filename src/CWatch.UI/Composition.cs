using Microsoft.Extensions.DependencyInjection;
using CWatch.Analysis.Growth;
using CWatch.Analysis.Recurring;
using CWatch.Analysis.Reports;
using CWatch.Analysis.Scanning;
using CWatch.Analysis.Storage;
using CWatch.Analysis.Trends;
using CWatch.Cleanup.Engine;
using CWatch.Cleanup.Providers;
using CWatch.Core.Interfaces;
using CWatch.Infrastructure.Config;
using CWatch.Infrastructure.Logging;
using CWatch.Infrastructure.WindowsApi;
using CWatch.Monitoring.DriveMonitor;
using CWatch.Storage.Database;
using CWatch.Core.Safety;
using CWatch.Storage.Repositories;
using CWatch.UI.ViewModels;

namespace CWatch.UI;

/// <summary>
/// Central composition root. Registers every application service and ViewModel with
/// Microsoft.Extensions.DependencyInjection so the dependency graph is explicit,
/// validated at resolution time, and overridable in tests via the
/// <paramref name="configure"/> callback.
/// </summary>
public static class AppComposition
{
    public static ServiceProvider BuildServices(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        // Infrastructure
        services.AddSingleton<ILoggerService, FileLoggerService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IPathExclusionMatcher, SettingsPathExclusionMatcher>();
        services.AddSingleton<DatabaseManager>(sp => new DatabaseManager(null, sp.GetRequiredService<ILoggerService>()));
        services.AddSingleton<ISnapshotRepository, SnapshotRepository>();
        services.AddSingleton<IProcessInspector, ProcessInspector>();
        services.AddSingleton<IDriveMonitor, DriveMonitorService>();

        // Analysis
        services.AddSingleton<IFileSystemScanner, FileSystemScanner>();
        services.AddSingleton<IStorageAnalyzer, StorageAnalyzer>();
        services.AddSingleton<IGrowthAnalyzer, GrowthAnalyzer>();
        services.AddSingleton<IRecurringGrowthDetector, RecurringGrowthDetector>();
        services.AddSingleton<ITrendAnalyzer, TrendAnalyzer>();
        services.AddSingleton<IStorageReportGenerator, StorageReportGenerator>();

        // Cleanup — providers registered individually so the engine receives the real
        // provider set via IEnumerable<T> (an unregistered IEnumerable would resolve empty).
        services.AddSingleton<ICleanupProvider, WindowsTempCleanupProvider>();
        services.AddSingleton<ICleanupProvider, RecycleBinCleanupProvider>();
        services.AddSingleton<ICleanupProvider, BrowserCacheCleanupProvider>();
        services.AddSingleton<ICleanupProvider, DevelopmentCacheCleanupProvider>();
        services.AddSingleton<ICleanupProvider, ApplicationCacheCleanupProvider>();
        services.AddSingleton<ICleanupEngine>(sp => new CleanupEngine(
            sp.GetRequiredService<IProcessInspector>(),
            sp.GetRequiredService<ISnapshotRepository>(),
            sp.GetRequiredService<IPathExclusionMatcher>(),
            sp.GetRequiredService<ILoggerService>(),
            sp.GetServices<ICleanupProvider>()));

        // Presentation
        services.AddSingleton<DuplicatesViewModel>();
        services.AddSingleton<CleanupViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }
}
