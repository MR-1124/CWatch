using System.Collections.ObjectModel;
using System.Windows.Input;
using CWatch.Analysis.Classifiers;
using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.UI.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IStorageAnalyzer _storageAnalyzer;
    private readonly ISnapshotRepository _snapshotRepo;
    private readonly IGrowthAnalyzer _growthAnalyzer;
    private readonly Action<string> _navigateTo;

    private DriveStatus _driveStatus = new();
    private string _trendMessage = "Analyzing drive trend...";
    private GrowthTrend _trendState = GrowthTrend.Stable;
    private bool _hasEmergencyAlert;
    private string _emergencyMessage = string.Empty;
    private bool _isLoading;

    public DriveStatus DriveStatus
    {
        get => _driveStatus;
        set => SetProperty(ref _driveStatus, value);
    }

    public string TrendMessage
    {
        get => _trendMessage;
        set => SetProperty(ref _trendMessage, value);
    }

    public GrowthTrend TrendState
    {
        get => _trendState;
        set => SetProperty(ref _trendState, value);
    }

    public bool HasEmergencyAlert
    {
        get => _hasEmergencyAlert;
        set => SetProperty(ref _hasEmergencyAlert, value);
    }

    public string EmergencyMessage
    {
        get => _emergencyMessage;
        set => SetProperty(ref _emergencyMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<CategoryBreakdown> Categories { get; } = [];
    public ObservableCollection<StorageSnapshot> RecentSnapshots { get; } = [];

    public ICommand InvestigateGrowthCommand { get; }
    public ICommand RecommendedCleanupCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenCategoryCommand { get; }

    public DashboardViewModel(
        IStorageAnalyzer storageAnalyzer,
        ISnapshotRepository snapshotRepo,
        IGrowthAnalyzer growthAnalyzer,
        Action<string> navigateTo)
    {
        _storageAnalyzer = storageAnalyzer;
        _snapshotRepo = snapshotRepo;
        _growthAnalyzer = growthAnalyzer;
        _navigateTo = navigateTo;

        InvestigateGrowthCommand = new RelayCommand(() => _navigateTo("History"));
        RecommendedCleanupCommand = new RelayCommand(() => _navigateTo("Cleanup"));
        RefreshCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
        OpenCategoryCommand = new RelayCommand(_ => _navigateTo("Explorer"));
    }

    public async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        try
        {
            DriveStatus = _storageAnalyzer.GetDriveStatus("C:");

            // Emergency check (< 10GB free)
            if (DriveStatus.IsCriticallyLow(10L * 1024 * 1024 * 1024))
            {
                HasEmergencyAlert = true;
                EmergencyMessage = $"🔴 C: DRIVE CRITICALLY LOW: Only {ByteSizeFormatter.Format(DriveStatus.FreeBytes)} remaining! Immediate cleanup recommended.";
            }
            else if (DriveStatus.IsWarningLow(25L * 1024 * 1024 * 1024))
            {
                HasEmergencyAlert = true;
                EmergencyMessage = $"⚠️ Low Disk Space: {ByteSizeFormatter.Format(DriveStatus.FreeBytes)} free space remaining.";
            }
            else
            {
                HasEmergencyAlert = false;
                EmergencyMessage = string.Empty;
            }

            // Load recent history to calculate 3-day / 7-day trend
            var snapshots = await _snapshotRepo.GetAllSnapshotsAsync("C:", 14);
            RecentSnapshots.Clear();
            foreach (var s in snapshots) RecentSnapshots.Add(s);

            // Populate categories from latest snapshot if available
            var latestSnap = snapshots.LastOrDefault();
            if (latestSnap != null && !string.IsNullOrWhiteSpace(latestSnap.CategoriesJson))
            {
                try
                {
                    var cached = System.Text.Json.JsonSerializer.Deserialize<List<CategoryBreakdown>>(latestSnap.CategoriesJson);
                    if (cached != null && cached.Count > 0 && Categories.Count == 0)
                    {
                        UpdateCategories(cached);
                    }
                }
                catch { }
            }

            if (snapshots.Count >= 2)
            {
                var oldest = snapshots.First();
                var newest = snapshots.Last();
                long netUsedChange = newest.UsedBytes - oldest.UsedBytes;
                double days = (newest.TimestampUtc - oldest.TimestampUtc).TotalDays;

                if (days >= 0.5)
                {
                    if (netUsedChange > 0)
                    {
                        TrendMessage = $"📈 C: has gained {ByteSizeFormatter.Format(netUsedChange)} in the last {days:F0} days";
                        TrendState = GrowthTrend.ModerateGrowth;
                    }
                    else if (netUsedChange < 0)
                    {
                        TrendMessage = $"📉 C: has freed {ByteSizeFormatter.Format(-netUsedChange)} in the last {days:F0} days";
                        TrendState = GrowthTrend.SpaceFreed;
                    }
                    else
                    {
                        TrendMessage = $"Stable storage usage over the last {days:F0} days";
                        TrendState = GrowthTrend.Stable;
                    }
                }
            }
            else
            {
                TrendMessage = "Monitoring C: storage changes over time";
                TrendState = GrowthTrend.Stable;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void UpdateCategories(List<CategoryBreakdown> list)
    {
        Categories.Clear();
        foreach (var c in list) Categories.Add(c);
    }
}
