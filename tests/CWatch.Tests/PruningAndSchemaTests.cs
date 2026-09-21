using CWatch.Core.Models;
using CWatch.Storage.Database;
using CWatch.Storage.Repositories;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Tests for snapshot retention (PruneOldSnapshotsAsync) and the
/// PRAGMA user_version schema lifecycle in DatabaseManager.
/// </summary>
public class PruningAndSchemaTests : IDisposable
{
    private readonly string _root;
    private readonly string _dbPath;

    public PruningAndSchemaTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cwatch_prune_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "test.db");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    private static StorageSnapshot MakeSnapshot(int ageDays, string drive = "C:")
    {
        return new StorageSnapshot
        {
            DriveLetter = drive,
            TotalBytes = 100_000_000,
            FreeBytes = 40_000_000,
            TimestampUtc = DateTime.UtcNow.AddDays(-ageDays)
        };
    }

    // ---------- Pruning ----------

    [Fact]
    public async Task Prune_RemovesOnlySnapshotsOlderThanRetention()
    {
        var repo = new SnapshotRepository(new DatabaseManager(_dbPath));
        await repo.InitializeAsync();

        await repo.SaveSnapshotAsync(MakeSnapshot(ageDays: 120));
        await repo.SaveSnapshotAsync(MakeSnapshot(ageDays: 100));
        await repo.SaveSnapshotAsync(MakeSnapshot(ageDays: 30));
        await repo.SaveSnapshotAsync(MakeSnapshot(ageDays: 1));

        await repo.PruneOldSnapshotsAsync(retentionDays: 90);

        var remaining = await repo.GetAllSnapshotsAsync("C:", limit: 100);
        Assert.Equal(2, remaining.Count); // the 30-day and 1-day records
        Assert.All(remaining, s => Assert.True(s.TimestampUtc >= DateTime.UtcNow.AddDays(-90).AddMinutes(-5)));
    }

    [Fact]
    public async Task Prune_RemovesCleanHistoryOutsideRetention()
    {
        var repo = new SnapshotRepository(new DatabaseManager(_dbPath));
        await repo.InitializeAsync();

        await repo.RecordCleanHistoryAsync(new CleanHistoryItem
        {
            ProviderId = "test",
            TargetPath = @"C:\temp\old.bin",
            BytesCleaned = 1_000,
            CategoryName = "Temporary files",
            CleanedUtc = DateTime.UtcNow.AddDays(-120)
        });
        await repo.RecordCleanHistoryAsync(new CleanHistoryItem
        {
            ProviderId = "test",
            TargetPath = @"C:\temp\new.bin",
            BytesCleaned = 2_000,
            CategoryName = "Temporary files",
            CleanedUtc = DateTime.UtcNow.AddDays(-2)
        });

        await repo.PruneOldSnapshotsAsync(retentionDays: 90);

        var history = await repo.GetCleanHistoryAsync(limit: 100);
        Assert.Single(history);
        Assert.Equal(@"C:\temp\new.bin", history[0].TargetPath);
    }

    [Fact]
    public async Task Prune_KeepsEverythingWhenAllWithinRetention()
    {
        var repo = new SnapshotRepository(new DatabaseManager(_dbPath));
        await repo.InitializeAsync();

        await repo.SaveSnapshotAsync(MakeSnapshot(ageDays: 1));
        await repo.SaveSnapshotAsync(MakeSnapshot(ageDays: 5, drive: "D:"));

        await repo.PruneOldSnapshotsAsync(retentionDays: 90);

        Assert.Single(await repo.GetAllSnapshotsAsync("C:", limit: 100));
        Assert.Single(await repo.GetAllSnapshotsAsync("D:", limit: 100));
    }

    // ---------- Schema versioning ----------

    [Fact]
    public async Task FreshDatabase_InitializesAtCurrentVersion()
    {
        var db = new DatabaseManager(_dbPath);
        await db.InitializeSchemaAsync();

        int version = await ReadUserVersionAsync();
        Assert.Equal(DatabaseManager.CurrentSchemaVersion, version);
    }

    [Fact]
    public async Task LegacyDatabase_ReportsVersionZero_IsAdopted()
    {
        // Simulate a pre-versioning database: tables exist, user_version = 0.
        var db = new DatabaseManager(_dbPath);
        using (var conn = db.CreateConnection())
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE snapshots (id INTEGER PRIMARY KEY, timestamp_utc TEXT NOT NULL, drive_letter TEXT NOT NULL,
                    total_bytes INTEGER NOT NULL, free_bytes INTEGER NOT NULL, categories_json TEXT NOT NULL,
                    top_items_json TEXT NOT NULL, notes TEXT NULL);
                PRAGMA user_version = 0;";
            await cmd.ExecuteNonQueryAsync();
        }

        await db.InitializeSchemaAsync();

        int version = await ReadUserVersionAsync();
        Assert.Equal(DatabaseManager.CurrentSchemaVersion, version);
    }

    [Fact]
    public async Task VersionedDatabase_WithAllTables_DoesNotRecreate()
    {
        var db = new DatabaseManager(_dbPath);
        await db.InitializeSchemaAsync();
        var repo = new SnapshotRepository(db);
        await repo.InitializeAsync();

        var snapshot = MakeSnapshot(ageDays: 0);
        await repo.SaveSnapshotAsync(snapshot);
        long id = snapshot.Id;

        // Re-open: migration chain must not damage existing rows.
        await db.InitializeSchemaAsync();

        var reloaded = await repo.GetAllSnapshotsAsync("C:", limit: 10);
        Assert.Single(reloaded);
        Assert.Equal(id, reloaded[0].Id);
    }

    [Fact]
    public async Task NewerDatabase_ThanApplication_Throws()
    {
        var db = new DatabaseManager(_dbPath);
        using (var conn = db.CreateConnection())
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA user_version = 999;";
            await cmd.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.InitializeSchemaAsync());
    }

    private async Task<int> ReadUserVersionAsync()
    {
        var db = new DatabaseManager(_dbPath);
        using var conn = db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = await cmd.ExecuteScalarAsync();
        return result is long v ? (int)v : 0;
    }
}
