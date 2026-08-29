using CWatch.Core.Models;

namespace CWatch.Core.Interfaces;

public interface IFileSystemScanner
{
    Task<StorageItem> ScanDirectoryAsync(
        string rootPath,
        IProgress<ScanProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task<List<StorageItem>> FindLargestFilesAsync(
        string rootPath,
        int count = 100,
        IProgress<ScanProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task<List<List<StorageItem>>> FindDuplicateFilesAsync(
        string rootPath,
        IProgress<ScanProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IStorageAnalyzer
{
    DriveStatus GetDriveStatus(string driveLetter = "C:");

    Task<List<CategoryBreakdown>> AnalyzeCategoriesAsync(
        StorageItem rootItem,
        CancellationToken cancellationToken = default);
}

public interface IGrowthAnalyzer
{
    Task<List<GrowthDelta>> CompareSnapshotsAsync(
        StorageSnapshot olderSnapshot,
        StorageSnapshot newerSnapshot,
        CancellationToken cancellationToken = default);

    Task<List<GrowthDelta>> AnalyzeGrowthSinceAsync(
        TimeSpan timeSpan,
        StorageSnapshot currentSnapshot,
        CancellationToken cancellationToken = default);
}

public interface IRecurringGrowthDetector
{
    Task<List<RecurringGrowthAlert>> DetectRecurringGrowthAsync(
        IReadOnlyList<StorageSnapshot> snapshots,
        IReadOnlyList<CleanHistoryItem> cleanHistory,
        CancellationToken cancellationToken = default);
}

public interface ITrendAnalyzer
{
    (double dailyGrowthBytes, TimeSpan? estimatedDaysToFull) CalculateExhaustionTrend(
        IReadOnlyList<StorageSnapshot> snapshots,
        long currentFreeBytes);
}

public interface IStorageReportGenerator
{
    Task<StorageReport> GenerateReportAsync(
        DriveStatus driveStatus,
        StorageItem rootItem,
        IReadOnlyList<StorageSnapshot> history,
        IReadOnlyList<CleanupCandidate> recommendations,
        CancellationToken cancellationToken = default);
}
