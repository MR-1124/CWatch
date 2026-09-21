using System.Text.Json;
using CWatch.Analysis.Classifiers;
using CWatch.Core.Enums;
using CWatch.Core.Models;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Multi-drive support tests: drive-agnostic classification, drive-letter snapshot
/// isolation in the repository, and normalized settings round-tripping.
/// </summary>
public class MultiDriveSupportTests
{
    [Theory]
    [InlineData(@"D:\Users\alice\Documents", StorageCategoryType.Documents)]
    [InlineData(@"D:\Users\bob", StorageCategoryType.UserFiles)]
    [InlineData(@"E:\Program Files\Contoso", StorageCategoryType.InstalledApps)]
    [InlineData(@"E:\ProgramData\Vendor", StorageCategoryType.ProgramData)]
    [InlineData(@"F:\Windows\System32\drivers", StorageCategoryType.WindowsSystem)]
    [InlineData(@"F:\Windows\Temp\abc.tmp", StorageCategoryType.TemporaryFiles)]
    public void CategoryClassifier_Classifies_NonSystemDrives(string path, StorageCategoryType expected)
    {
        Assert.Equal(expected, CategoryClassifier.Classify(path, isDirectory: true));
    }

    [Theory]
    [InlineData(@"C:\Program Files\Contoso", StorageCategoryType.InstalledApps)]
    [InlineData(@"C:\Windows", StorageCategoryType.WindowsSystem)]
    [InlineData(@"C:\Users\dave", StorageCategoryType.UserFiles)]
    [InlineData(@"C:\ProgramData", StorageCategoryType.ProgramData)]
    public void CategoryClassifier_StillClassifies_SystemDrive(string path, StorageCategoryType expected)
    {
        Assert.Equal(expected, CategoryClassifier.Classify(path, isDirectory: true));
    }

    [Fact]
    public async Task SnapshotRepository_IsolatesSnapshots_ByDriveLetter()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"cwatch_multi_{Guid.NewGuid():N}.db");
        try
        {
            var db = new CWatch.Storage.Database.DatabaseManager(testDbPath);
            var repo = new CWatch.Storage.Repositories.SnapshotRepository(db);
            await repo.InitializeAsync();

            await repo.SaveSnapshotAsync(new StorageSnapshot
            {
                DriveLetter = "D:",
                TotalBytes = 1000,
                FreeBytes = 400,
                TimestampUtc = DateTime.UtcNow.AddMinutes(-10)
            });
            await repo.SaveSnapshotAsync(new StorageSnapshot
            {
                DriveLetter = "E:",
                TotalBytes = 2000,
                FreeBytes = 1000,
                TimestampUtc = DateTime.UtcNow.AddMinutes(-5)
            });

            var dSnapshots = await repo.GetAllSnapshotsAsync("D:");
            var eSnapshots = await repo.GetAllSnapshotsAsync("E:");
            var none = await repo.GetAllSnapshotsAsync("Q:");

            Assert.Single(dSnapshots);
            Assert.Single(eSnapshots);
            Assert.Empty(none);
            Assert.Equal(1000, dSnapshots[0].TotalBytes);
            Assert.Equal(2000, eSnapshots[0].TotalBytes);
        }
        finally
        {
            try { File.Delete(testDbPath); } catch { }
        }
    }

    [Fact]
    public void AppSettings_TargetDriveLetter_RoundTrips()
    {
        var settings = new AppSettings { TargetDriveLetter = "d:\\" };
        string json = JsonSerializer.Serialize(settings);
        var loaded = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(loaded);
        Assert.Equal("d:\\", loaded.TargetDriveLetter);
        // Consumer side normalizes for actual use.
        Assert.Equal("D:", DriveLetters.Normalize(loaded.TargetDriveLetter));
    }
}
