using CWatch.Cleanup.Engine;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Core.Safety;
using CWatch.Infrastructure.Config;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Regression tests for the public-readiness bug sweep: honest cleanup history,
/// settings self-healing, and sanitization of impossible persisted values.
/// </summary>
public class PublicReadinessTests : IDisposable
{
    private readonly string _root;

    public PublicReadinessTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cwatch_pub_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    // ---------------- Clean-history honesty ----------------

    private sealed class FailingProvider : ICleanupProvider
    {
        private readonly string _target;
        public FailingProvider(string target) => _target = target;

        public string ProviderId => "failing";
        public string DisplayName => "Failing provider";
        public string CategoryName => "Test";
        public bool IsAdvanced => false;

        public Task<List<CleanupCandidate>> ScanCandidatesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<CleanupCandidate>
            {
                new()
                {
                    ProviderId = ProviderId,
                    Title = "Doomed target",
                    Path = _target,
                    SizeBytes = 4096
                }
            });

        public Task<CleanupResult> ExecuteCleanupAsync(
            IReadOnlyList<CleanupCandidate> candidates,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // The provider fails: nothing cleaned, one failure reported.
            return Task.FromResult(new CleanupResult
            {
                BytesCleaned = 0,
                ItemsCleanedCount = 0,
                FailedItemsCount = candidates.Count,
                ErrorMessages = { "Simulated provider failure" }
            });
        }
    }

    [Fact]
    public async Task FailedCleanup_DoesNotRecordCleanHistory()
    {
        string dbPath = Path.Combine(_root, "history.db");
        var repo = new CWatch.Storage.Repositories.SnapshotRepository(
            new CWatch.Storage.Database.DatabaseManager(dbPath));
        await repo.InitializeAsync();

        var engine = new CleanupEngine(
            snapshotRepo: repo,
            providers: new ICleanupProvider[] { new FailingProvider(_root) });

        var candidates = await engine.ScanAllRecommendationsAsync();
        Assert.Single(candidates);

        var result = await engine.ExecuteCleanupAsync(candidates);

        Assert.Equal(0, result.ItemsCleanedCount);
        Assert.Equal(1, result.FailedItemsCount);

        var history = await repo.GetCleanHistoryAsync(50);
        Assert.Empty(history); // failed items must never look like recurring bloat
    }

    [Fact]
    public async Task ExcludedPaths_StillBlockCleanupCandidates()
    {
        // End-to-end guard from the exclusion work, re-run with the current engine.
        string excludedDir = Path.Combine(_root, "keepout");
        Directory.CreateDirectory(excludedDir);

        string settingsPath = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(settingsPath,
            $$"""{ "ExcludedPaths": [ "{{excludedDir.Replace("\\", "\\\\")}}" ] }""");

        var settings = new SettingsService(customFilePath: settingsPath);
        await settings.LoadSettingsAsync();

        string dbPath = Path.Combine(_root, "excl.db");
        var repo = new CWatch.Storage.Repositories.SnapshotRepository(
            new CWatch.Storage.Database.DatabaseManager(dbPath));
        await repo.InitializeAsync();

        var engine = new CleanupEngine(
            snapshotRepo: repo,
            exclusionMatcher: new SettingsPathExclusionMatcher(settings),
            providers: new ICleanupProvider[]
            {
                new StubScanningProvider(excludedDir)
            });

        var candidates = await engine.ScanAllRecommendationsAsync();
        Assert.Empty(candidates); // excluded dir must never be proposed
    }

    private sealed class StubScanningProvider : ICleanupProvider
    {
        private readonly string _target;
        public StubScanningProvider(string target) => _target = target;

        public string ProviderId => "stub";
        public string DisplayName => "Stub";
        public string CategoryName => "Test";
        public bool IsAdvanced => false;

        public Task<List<CleanupCandidate>> ScanCandidatesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<CleanupCandidate>
            {
                new() { ProviderId = "stub", Title = "Stub target", Path = _target, SizeBytes = 1024 }
            });

        public Task<CleanupResult> ExecuteCleanupAsync(
            IReadOnlyList<CleanupCandidate> candidates,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CleanupResult());
    }

    // ---------------- Settings self-healing ----------------

    [Fact]
    public async Task CorruptSettingsFile_IsQuarantined_AndDefaultsLoad()
    {
        string settingsPath = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{ this is not valid json !!");

        var settings = new SettingsService(customFilePath: settingsPath);
        await settings.LoadSettingsAsync();

        Assert.True(settings.LastLoadHadProblems);
        Assert.True(File.Exists(settingsPath + ".corrupt")); // original preserved for diagnosis

        // A fresh defaults file replaced the corrupt one and round-trips cleanly.
        var reloaded = new SettingsService(customFilePath: settingsPath);
        await reloaded.LoadSettingsAsync();
        Assert.False(reloaded.LastLoadHadProblems);
        Assert.Equal("C:", reloaded.Settings.TargetDriveLetter);
    }

    [Fact]
    public async Task ImpossiblePersistedValues_AreSanitized()
    {
        string settingsPath = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(settingsPath, """
            {
              "MonitorIntervalMinutes": 0,
              "RetentionDays": 999999,
              "WarningThresholdGb": -5,
              "AppTheme": "NeonRainbow",
              "TargetDriveLetter": "c"
            }
            """);

        var settings = new SettingsService(customFilePath: settingsPath);
        await settings.LoadSettingsAsync();

        var s = settings.Settings;
        Assert.Equal(1, s.MonitorIntervalMinutes);   // clamped to >= 1
        Assert.Equal(3650, s.RetentionDays);         // clamped to <= 3650
        Assert.Equal(1, s.WarningThresholdGb);       // clamped to >= 1
        Assert.Equal("Dark", s.AppTheme);            // unknown theme -> default
        Assert.Equal("C:", s.TargetDriveLetter);     // "c" normalized
    }

    [Fact]
    public async Task SaveSettingsAsync_IsAtomic_NoTempLeftBehind()
    {
        string settingsPath = Path.Combine(_root, "settings.json");
        var settings = new SettingsService(customFilePath: settingsPath);
        await settings.LoadSettingsAsync();

        settings.Settings.MonitorIntervalMinutes = 15;
        await settings.SaveSettingsAsync();

        Assert.True(File.Exists(settingsPath));
        Assert.False(File.Exists(settingsPath + ".tmp")); // temp consumed by the move

        // Reload round-trips the value.
        var reloaded = new SettingsService(customFilePath: settingsPath);
        await reloaded.LoadSettingsAsync();
        Assert.Equal(15, reloaded.Settings.MonitorIntervalMinutes);
    }
}
