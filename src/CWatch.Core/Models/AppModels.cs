using System.ComponentModel;
using System.Runtime.CompilerServices;

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
/// Application settings and user preferences. Observable so editors can track
/// unsaved changes while the persistence layer keeps its own instance.
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    private string _appTheme = "Dark";
    private bool _monitoringEnabled = true;
    private int _monitorIntervalMinutes = 30;
    private long _warningThresholdGb = 25;
    private long _criticalThresholdGb = 10;
    private int _retentionDays = 90;
    private List<string> _excludedPaths = [];
    private bool _autoScanOnLaunch = true;
    private string _targetDriveLetter = "C:";

    public string AppTheme { get => _appTheme; set => Set(ref _appTheme, value); }
    public bool MonitoringEnabled { get => _monitoringEnabled; set => Set(ref _monitoringEnabled, value); }
    public int MonitorIntervalMinutes { get => _monitorIntervalMinutes; set => Set(ref _monitorIntervalMinutes, value); }
    public long WarningThresholdGb { get => _warningThresholdGb; set => Set(ref _warningThresholdGb, value); }
    public long CriticalThresholdGb { get => _criticalThresholdGb; set => Set(ref _criticalThresholdGb, value); }
    public int RetentionDays { get => _retentionDays; set => Set(ref _retentionDays, value); }

    public List<string> ExcludedPaths { get => _excludedPaths; set => Set(ref _excludedPaths, value); }

    public bool AutoScanOnLaunch { get => _autoScanOnLaunch; set => Set(ref _autoScanOnLaunch, value); }
    public string TargetDriveLetter { get => _targetDriveLetter; set => Set(ref _targetDriveLetter, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
