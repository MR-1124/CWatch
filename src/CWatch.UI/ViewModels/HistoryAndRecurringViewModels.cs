using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Infrastructure.WindowsApi;

namespace CWatch.UI.ViewModels;

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly ISnapshotRepository _snapshotRepo;
    private readonly IGrowthAnalyzer _growthAnalyzer;
    private readonly ITrendAnalyzer _trendAnalyzer;

    private string _selectedTimeRange = "7d";
    private string _growthSummary = "Select a timeframe to inspect storage changes.";
    private string _burnRateSummary = string.Empty;
    private string _runoutForecast = string.Empty;
    private string _deltaFilter = "All"; // All, GrowthOnly, FreedOnly
    private bool _isLoading;
    private StorageSnapshot? _selectedSnapshot;
    private List<GrowthDelta> _allDeltas = [];

    public string SelectedTimeRange
    {
        get => _selectedTimeRange;
        set
        {
            if (SetProperty(ref _selectedTimeRange, value))
            {
                _ = LoadHistoryAsync();
            }
        }
    }

    public string GrowthSummary
    {
        get => _growthSummary;
        set => SetProperty(ref _growthSummary, value);
    }

    public string BurnRateSummary
    {
        get => _burnRateSummary;
        set => SetProperty(ref _burnRateSummary, value);
    }

    public string RunoutForecast
    {
        get => _runoutForecast;
        set => SetProperty(ref _runoutForecast, value);
    }

    public string DeltaFilter
    {
        get => _deltaFilter;
        set
        {
            if (SetProperty(ref _deltaFilter, value))
            {
                ApplyDeltaFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public StorageSnapshot? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set => SetProperty(ref _selectedSnapshot, value);
    }

    public ObservableCollection<StorageSnapshot> Snapshots { get; } = [];
    public ObservableCollection<GrowthDelta> GrowthDeltas { get; } = [];

    public ICommand SelectTimeRangeCommand { get; }
    public ICommand SetDeltaFilterCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand RecordSnapshotNowCommand { get; }

    public HistoryViewModel(
        ISnapshotRepository snapshotRepo,
        IGrowthAnalyzer growthAnalyzer,
        ITrendAnalyzer trendAnalyzer)
    {
        _snapshotRepo = snapshotRepo;
        _growthAnalyzer = growthAnalyzer;
        _trendAnalyzer = trendAnalyzer;

        SelectTimeRangeCommand = new RelayCommand(param =>
        {
            if (param is string range) SelectedTimeRange = range;
        });

        SetDeltaFilterCommand = new RelayCommand(param =>
        {
            if (param is string filter) DeltaFilter = filter;
        });

        RefreshCommand = new AsyncRelayCommand(LoadHistoryAsync);

        RecordSnapshotNowCommand = new AsyncRelayCommand(async () =>
        {
            try
            {
                var drive = new DriveInfo("C:");
                var snap = new StorageSnapshot
                {
                    DriveLetter = "C:",
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.AvailableFreeSpace,
                    TimestampUtc = DateTime.UtcNow,
                    Notes = "Manual point-in-time snapshot"
                };
                await _snapshotRepo.SaveSnapshotAsync(snap);
                await LoadHistoryAsync();
            }
            catch { }
        });
    }

    public async Task LoadHistoryAsync()
    {
        IsLoading = true;
        try
        {
            DateTime now = DateTime.UtcNow;
            DateTime from = SelectedTimeRange switch
            {
                "today" => now.Date,
                "24h" => now.AddHours(-24),
                "7d" => now.AddDays(-7),
                "30d" => now.AddDays(-30),
                "90d" => now.AddDays(-90),
                _ => now.AddDays(-7)
            };

            var list = await _snapshotRepo.GetSnapshotsAsync("C:", from, now);
            if (list.Count == 0)
            {
                list = await _snapshotRepo.GetAllSnapshotsAsync("C:", 30);
            }

            Snapshots.Clear();
            foreach (var s in list) Snapshots.Add(s);

            if (SelectedSnapshot == null || !Snapshots.Contains(SelectedSnapshot))
            {
                SelectedSnapshot = Snapshots.LastOrDefault();
            }

            _allDeltas.Clear();
            if (list.Count >= 2)
            {
                var oldest = list.First();
                var newest = list.Last();
                _allDeltas = await _growthAnalyzer.CompareSnapshotsAsync(oldest, newest);

                ApplyDeltaFilter();

                long netUsed = newest.UsedBytes - oldest.UsedBytes;
                GrowthSummary = netUsed >= 0
                    ? $"C: drive gained +{ByteSizeFormatter.Format(netUsed)} across this timeframe."
                    : $"C: drive freed -{ByteSizeFormatter.Format(-netUsed)} across this timeframe.";

                var (dailyRate, daysLeft) = _trendAnalyzer.CalculateExhaustionTrend(list, newest.FreeBytes);
                if (dailyRate > 0)
                {
                    BurnRateSummary = $"+{ByteSizeFormatter.Format((long)dailyRate)} / day";
                    RunoutForecast = daysLeft.HasValue && daysLeft.Value.TotalDays < 365
                        ? $"~{daysLeft.Value.TotalDays:F0} days until capacity exhaustion"
                        : "Capacity headroom is comfortable (> 1 year)";
                }
                else
                {
                    BurnRateSummary = "Steady / Deflating";
                    RunoutForecast = "No immediate storage depletion risk detected";
                }
            }
            else
            {
                ApplyDeltaFilter();
                GrowthSummary = "Historical snapshots are accumulating. Check back as periodic background snapshots are recorded.";
                BurnRateSummary = "Recording baseline...";
                RunoutForecast = "Collecting telemetry...";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyDeltaFilter()
    {
        GrowthDeltas.Clear();
        var deltas = _allDeltas.AsEnumerable();

        if (DeltaFilter == "GrowthOnly")
        {
            deltas = deltas.Where(d => d.DeltaBytes > 0);
        }
        else if (DeltaFilter == "FreedOnly")
        {
            deltas = deltas.Where(d => d.DeltaBytes < 0);
        }

        foreach (var d in deltas.OrderByDescending(d => Math.Abs(d.DeltaBytes)))
        {
            GrowthDeltas.Add(d);
        }
    }
}

public sealed class RecurringViewModel : ViewModelBase
{
    private readonly IRecurringGrowthDetector _recurringDetector;
    private readonly ISnapshotRepository _snapshotRepo;
    private bool _isLoading;
    private string _statusMessage = "Analyzing recurring storage patterns...";

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<RecurringGrowthAlert> Alerts { get; } = [];

    public ICommand InspectItemCommand { get; }
    public ICommand IgnoreItemCommand { get; }
    public ICommand CopyMitigationCommand { get; }
    public ICommand RefreshCommand { get; }

    public RecurringViewModel(
        IRecurringGrowthDetector recurringDetector,
        ISnapshotRepository snapshotRepo)
    {
        _recurringDetector = recurringDetector;
        _snapshotRepo = snapshotRepo;

        InspectItemCommand = new RelayCommand(param =>
        {
            if (param is RecurringGrowthAlert alert)
            {
                NativeMethods.OpenInExplorer(alert.Path);
            }
        });

        IgnoreItemCommand = new RelayCommand(param =>
        {
            if (param is RecurringGrowthAlert alert)
            {
                Alerts.Remove(alert);
                StatusMessage = $"Alert for '{alert.Path}' dismissed.";
            }
        });

        CopyMitigationCommand = new RelayCommand(param =>
        {
            if (param is RecurringGrowthAlert alert)
            {
                string cmd = GetMitigationCommand(alert.Path);
                if (!string.IsNullOrEmpty(cmd))
                {
                    System.Windows.Clipboard.SetText(cmd);
                    StatusMessage = $"Copied mitigation command to clipboard: {cmd}";
                }
            }
        });

        RefreshCommand = new AsyncRelayCommand(LoadRecurringAlertsAsync);
    }

    public async Task LoadRecurringAlertsAsync()
    {
        IsLoading = true;
        try
        {
            var snapshots = await _snapshotRepo.GetAllSnapshotsAsync("C:", 50);
            var history = await _snapshotRepo.GetCleanHistoryAsync(50);
            var detected = await _recurringDetector.DetectRecurringGrowthAsync(snapshots, history);

            Alerts.Clear();
            foreach (var a in detected) Alerts.Add(a);

            StatusMessage = Alerts.Count > 0
                ? $"Detected {Alerts.Count} recurring bloat location(s) re-accumulating space."
                : "No abnormal recurring storage leaks detected.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string GetMitigationCommand(string path)
    {
        string p = path.ToLowerInvariant();
        if (p.Contains("npm") || p.Contains("node_modules")) return "npm cache clean --force";
        if (p.Contains("pip") || p.Contains("wheels")) return "pip cache purge";
        if (p.Contains("docker")) return "docker system prune -af --volumes";
        if (p.Contains("nuget")) return "dotnet nuget locals all --clear";
        if (p.Contains("cargo")) return "cargo cache --autoclean";
        if (p.Contains("gradle")) return "gradle --stop";
        if (p.Contains("temp")) return "del /q/f/s %TEMP%\\*";
        return "cleanmgr /sagerun:1";
    }
}
