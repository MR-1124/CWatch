namespace CWatch.Core.Models;

/// <summary>
/// Progress reporting payload for filesystem scanning operations.
/// </summary>
public sealed class ScanProgressInfo
{
    public string CurrentPath { get; set; } = string.Empty;
    public string CurrentPhase { get; set; } = "Initializing...";
    public long FilesScanned { get; set; }
    public long DirectoriesScanned { get; set; }
    public long BytesProcessed { get; set; }
    public double EstimatedPercent { get; set; }
    public bool IsIndeterminate { get; set; } = true;
    public TimeSpan Elapsed { get; set; }

    public string FormattedBytes { get => ByteSizeFormatter.Format(BytesProcessed); set { } }
}

/// <summary>
/// Comprehensive Storage Intelligence report answering "Why Is My C: Drive Full?".
/// </summary>
public sealed class StorageReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
    public DriveStatus DriveStatus { get; set; } = new();
    public List<CategoryBreakdown> Categories { get; set; } = [];
    public List<GrowthDelta> RecentGrowthDeltas { get; set; } = [];
    public List<RecurringGrowthAlert> RecurringGrowthAlerts { get; set; } = [];
    public List<CleanupCandidate> RecommendedCleanups { get; set; } = [];
    public long TotalRecommendedCleanupBytes { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string KeyTakeaway { get; set; } = string.Empty;

    public string FormattedRecommendedCleanup { get => ByteSizeFormatter.Format(TotalRecommendedCleanupBytes); set { } }
}

/// <summary>
/// Application settings and user preferences.
/// </summary>
public sealed class AppSettings
{
    public string AppTheme { get; set; } = "Dark"; // "Dark", "Light", "System"
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool TrayModeEnabled { get; set; } = true;
    public bool MonitoringEnabled { get; set; } = true;
    public int MonitorIntervalMinutes { get; set; } = 30;
    public long WarningThresholdGb { get; set; } = 25;
    public long CriticalThresholdGb { get; set; } = 10;
    public int RetentionDays { get; set; } = 90;
    public List<string> ExcludedPaths { get; set; } = [];
    public bool RequireCleanupConfirmation { get; set; } = true;
    public bool ShowAdvancedCleanupProviders { get; set; } = true;
    public bool AutoScanOnLaunch { get; set; } = true;
    public string TargetDriveLetter { get; set; } = "C:";
}
