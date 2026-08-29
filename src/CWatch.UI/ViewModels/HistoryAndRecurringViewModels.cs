using System.Collections.ObjectModel;
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
    private bool _isLoading;

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

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<StorageSnapshot> Snapshots { get; } = [];
    public ObservableCollection<GrowthDelta> GrowthDeltas { get; } = [];

    public ICommand SelectTimeRangeCommand { get; }
    public ICommand RefreshCommand { get; }

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

        RefreshCommand = new AsyncRelayCommand(LoadHistoryAsync);
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

            GrowthDeltas.Clear();
            if (list.Count >= 2)
            {
                var oldest = list.First();
                var newest = list.Last();
                var deltas = await _growthAnalyzer.CompareSnapshotsAsync(oldest, newest);

                foreach (var d in deltas) GrowthDeltas.Add(d);

                long netUsed = newest.UsedBytes - oldest.UsedBytes;
                GrowthSummary = netUsed >= 0
                    ? $"C: drive gained {ByteSizeFormatter.Format(netUsed)} in this period."
                    : $"C: drive freed {ByteSizeFormatter.Format(-netUsed)} in this period.";

                var (dailyRate, daysLeft) = _trendAnalyzer.CalculateExhaustionTrend(list, newest.FreeBytes);
                BurnRateSummary = dailyRate > 0
                    ? $"Estimated storage burn rate: +{ByteSizeFormatter.Format((long)dailyRate)}/day" + (daysLeft.HasValue ? $" (~{daysLeft.Value.TotalDays:F0} days until full)" : "")
                    : "Storage growth rate is steady.";
            }
            else
            {
                GrowthSummary = "Historical snapshots are accumulating. Check back as periodic snapshots are recorded.";
                BurnRateSummary = "Recording background baseline metrics...";
            }
        }
        finally
        {
            IsLoading = false;
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
                ? $"Detected {Alerts.Count} location(s) exhibiting repeating cache regeneration."
                : "No abnormal recurring storage leaks detected.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
