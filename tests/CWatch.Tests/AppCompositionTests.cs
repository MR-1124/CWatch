using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.UI;
using Microsoft.Extensions.DependencyInjection;
using CWatch.UI.ViewModels;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Verifies the DI composition graph: every registered service resolves, every
/// ViewModel constructor dependency is satisfiable, and the configure callback
/// allows test overrides of any registration.
/// </summary>
public class AppCompositionTests
{
    [Fact]
    public void BuildServices_ResolvesAllServices_AndViewModel()
    {
        using var provider = AppComposition.BuildServices();

        Assert.NotNull(provider.GetRequiredService<ILoggerService>());
        Assert.NotNull(provider.GetRequiredService<ISettingsService>());
        Assert.NotNull(provider.GetRequiredService<ISnapshotRepository>());
        Assert.NotNull(provider.GetRequiredService<IFileSystemScanner>());
        Assert.NotNull(provider.GetRequiredService<IStorageAnalyzer>());
        Assert.NotNull(provider.GetRequiredService<IGrowthAnalyzer>());
        Assert.NotNull(provider.GetRequiredService<IRecurringGrowthDetector>());
        Assert.NotNull(provider.GetRequiredService<ITrendAnalyzer>());
        Assert.NotNull(provider.GetRequiredService<IStorageReportGenerator>());
        Assert.NotNull(provider.GetRequiredService<ICleanupEngine>());
        Assert.NotNull(provider.GetRequiredService<IDriveMonitor>());
        Assert.NotNull(provider.GetRequiredService<IProcessInspector>());
        Assert.NotNull(provider.GetRequiredService<MainViewModel>());
    }

    [Fact]
    public void BuildServices_ReturnsSingletons_ForRepeatedResolution()
    {
        using var provider = AppComposition.BuildServices();

        var a = provider.GetRequiredService<ISnapshotRepository>();
        var b = provider.GetRequiredService<ISnapshotRepository>();
        Assert.Same(a, b);

        var vm1 = provider.GetRequiredService<MainViewModel>();
        var vm2 = provider.GetRequiredService<MainViewModel>();
        Assert.Same(vm1, vm2);
    }

    [Fact]
    public void BuildServices_AllowsTestOverrides()
    {
        using var provider = AppComposition.BuildServices(services =>
        {
            // Replace the filesystem-backed settings with an in-memory fake.
            services.AddSingleton<ISettingsService, FakeSettingsService>();
        });

        var settings = provider.GetRequiredService<ISettingsService>();
        Assert.IsType<FakeSettingsService>(settings);

        // The MainViewModel consumes the fake through constructor injection:
        // its TargetDrive must reflect the fake's configured drive.
        var vm = provider.GetRequiredService<MainViewModel>();
        Assert.Equal("D:", vm.TargetDrive);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new()
        {
            TargetDriveLetter = "D:",
            MonitoringEnabled = false,
            AutoScanOnLaunch = false
        };

        public Task LoadSettingsAsync() => Task.CompletedTask;
        public Task SaveSettingsAsync() => Task.CompletedTask;
    }
}
