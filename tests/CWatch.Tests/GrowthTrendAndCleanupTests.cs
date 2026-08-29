using System.Text.Json;
using CWatch.Analysis.Growth;
using CWatch.Analysis.Recurring;
using CWatch.Analysis.Trends;
using CWatch.Cleanup.Engine;
using CWatch.Core.Enums;
using CWatch.Core.Models;
using CWatch.Storage.Database;
using CWatch.Storage.Repositories;
using Xunit;

namespace CWatch.Tests;

public class GrowthTrendAndCleanupTests
{
    [Fact]
    public async Task GrowthAnalyzer_CalculatesDeltasBetweenSnapshots()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"cwatch_test_{Guid.NewGuid():N}.db");
        var db = new DatabaseManager(tempDb);
        var repo = new SnapshotRepository(db);
        await repo.InitializeAsync();

        var growthAnalyzer = new GrowthAnalyzer(repo);

        var snapOld = new StorageSnapshot
        {
            DriveLetter = "C:",
            TotalBytes = 500L * 1024 * 1024 * 1024,
            FreeBytes = 200L * 1024 * 1024 * 1024,
            TimestampUtc = DateTime.UtcNow.AddDays(-3),
            CategoriesJson = JsonSerializer.Serialize(new List<CategoryBreakdown>
            {
                new() { CategoryType = StorageCategoryType.DevelopmentTools, SizeBytes = 20L * 1024 * 1024 * 1024 },
                new() { CategoryType = StorageCategoryType.TemporaryFiles, SizeBytes = 5L * 1024 * 1024 * 1024 }
            })
        };

        var snapNew = new StorageSnapshot
        {
            DriveLetter = "C:",
            TotalBytes = 500L * 1024 * 1024 * 1024,
            FreeBytes = 190L * 1024 * 1024 * 1024,
            TimestampUtc = DateTime.UtcNow,
            CategoriesJson = JsonSerializer.Serialize(new List<CategoryBreakdown>
            {
                new() { CategoryType = StorageCategoryType.DevelopmentTools, SizeBytes = 28L * 1024 * 1024 * 1024 }, // +8 GB
                new() { CategoryType = StorageCategoryType.TemporaryFiles, SizeBytes = 7L * 1024 * 1024 * 1024 }    // +2 GB
            })
        };

        var deltas = await growthAnalyzer.CompareSnapshotsAsync(snapOld, snapNew);

        Assert.NotEmpty(deltas);
        var devDelta = deltas.FirstOrDefault(d => d.Category == StorageCategoryType.DevelopmentTools);
        Assert.NotNull(devDelta);
        Assert.Equal(8L * 1024 * 1024 * 1024, devDelta.DeltaBytes);

        try { File.Delete(tempDb); } catch { }
    }

    [Fact]
    public void TrendAnalyzer_ProjectsExhaustionDays()
    {
        var analyzer = new TrendAnalyzer();
        var history = new List<StorageSnapshot>
        {
            new()
            {
                TimestampUtc = DateTime.UtcNow.AddDays(-5),
                TotalBytes = 500L * 1024 * 1024 * 1024,
                FreeBytes = 100L * 1024 * 1024 * 1024 // 400 GB used
            },
            new()
            {
                TimestampUtc = DateTime.UtcNow,
                TotalBytes = 500L * 1024 * 1024 * 1024,
                FreeBytes = 90L * 1024 * 1024 * 1024 // 410 GB used (+10 GB used in 5 days = +2 GB/day)
            }
        };

        long currentFree = 90L * 1024 * 1024 * 1024; // 90 GB remaining
        var (rate, days) = analyzer.CalculateExhaustionTrend(history, currentFree);

        Assert.True(rate > 0);
        Assert.NotNull(days);
        Assert.InRange(days.Value.TotalDays, 40, 50); // ~45 days
    }

    [Fact]
    public async Task CleanupEngine_DiscoversProvidersAndDryRuns()
    {
        var engine = new CleanupEngine();
        Assert.NotEmpty(engine.Providers);

        var candidates = await engine.ScanAllRecommendationsAsync();
        Assert.NotNull(candidates);

        foreach (var c in candidates)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Title));
            Assert.False(string.IsNullOrWhiteSpace(c.Reason));
            Assert.False(string.IsNullOrWhiteSpace(c.WhatWillHappen));
            Assert.True(Enum.IsDefined(typeof(SafetyLevel), c.Safety));
        }
    }
}

public class StorageRepositoryTests
{
    [Fact]
    public async Task SnapshotRepository_PersistsAndQueriesSnapshots()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"cwatch_unit_{Guid.NewGuid():N}.db");
        var db = new DatabaseManager(testDbPath);
        var repo = new SnapshotRepository(db);

        await repo.InitializeAsync();

        var snap = new StorageSnapshot
        {
            DriveLetter = "C:",
            TotalBytes = 500L * 1024 * 1024 * 1024,
            FreeBytes = 250L * 1024 * 1024 * 1024,
            TimestampUtc = DateTime.UtcNow,
            Notes = "Unit Test Baseline"
        };

        long id = await repo.SaveSnapshotAsync(snap);
        Assert.True(id > 0);

        var latest = await repo.GetLatestSnapshotAsync("C:");
        Assert.NotNull(latest);
        Assert.Equal(snap.FreeBytes, latest.FreeBytes);
        Assert.Equal("Unit Test Baseline", latest.Notes);

        // Record clean history
        await repo.RecordCleanHistoryAsync(new CleanHistoryItem
        {
            ProviderId = "windows_temp",
            TargetPath = @"C:\Users\Test\AppData\Local\Temp",
            BytesCleaned = 1024 * 1024 * 500,
            CategoryName = "TemporaryFiles",
            CleanedUtc = DateTime.UtcNow
        });

        var history = await repo.GetCleanHistoryAsync(10);
        Assert.Single(history);
        Assert.Equal("windows_temp", history[0].ProviderId);

        try { File.Delete(testDbPath); } catch { }
    }
}
