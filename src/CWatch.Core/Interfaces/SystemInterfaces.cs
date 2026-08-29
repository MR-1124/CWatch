using CWatch.Core.Models;

namespace CWatch.Core.Interfaces;

public interface ICleanupProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    string CategoryName { get; }
    bool IsAdvanced { get; }

    Task<List<CleanupCandidate>> ScanCandidatesAsync(CancellationToken cancellationToken = default);

    Task<CleanupResult> ExecuteCleanupAsync(
        IReadOnlyList<CleanupCandidate> candidates,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface ICleanupEngine
{
    IReadOnlyList<ICleanupProvider> Providers { get; }

    Task<List<CleanupCandidate>> ScanAllRecommendationsAsync(
        bool includeAdvanced = true,
        CancellationToken cancellationToken = default);

    Task<CleanupResult> ExecuteCleanupAsync(
        IReadOnlyList<CleanupCandidate> selectedCandidates,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface ISnapshotRepository
{
    Task InitializeAsync();
    Task<long> SaveSnapshotAsync(StorageSnapshot snapshot);
    Task<StorageSnapshot?> GetLatestSnapshotAsync(string driveLetter = "C:");
    Task<List<StorageSnapshot>> GetSnapshotsAsync(string driveLetter, DateTime fromUtc, DateTime toUtc);
    Task<List<StorageSnapshot>> GetAllSnapshotsAsync(string driveLetter, int limit = 300);
    Task RecordCleanHistoryAsync(CleanHistoryItem historyItem);
    Task<List<CleanHistoryItem>> GetCleanHistoryAsync(int limit = 100);
    Task PruneOldSnapshotsAsync(int retentionDays);
}

public interface IDriveMonitor : IDisposable
{
    event EventHandler<DriveStatus>? DriveStatusChanged;
    event EventHandler<DriveStatus>? LowDiskSpaceAlert;
    DriveStatus CurrentStatus { get; }
    bool IsRunning { get; }
    void StartMonitoring(int intervalMinutes = 30);
    void StopMonitoring();
    Task TriggerImmediateCheckAsync();
}

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadSettingsAsync();
    Task SaveSettingsAsync();
}

public interface ILoggerService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
}

public interface IProcessInspector
{
    List<LockedFileInfo> FindLockingProcesses(string filePath);
}
