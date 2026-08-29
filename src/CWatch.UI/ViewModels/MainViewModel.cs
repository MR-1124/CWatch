using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.UI.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IFileSystemScanner _scanner;
    private readonly IStorageAnalyzer _storageAnalyzer;
    private readonly ISnapshotRepository _snapshotRepo;
    private readonly ICleanupEngine _cleanupEngine;
    private readonly IDriveMonitor _driveMonitor;
    private readonly ISettingsService _settingsService;
    private readonly ILoggerService _logger;

    private string _currentPage = "Dashboard";
    private bool _isScanning;
    private ScanProgressInfo _scanProgress = new();
    private CancellationTokenSource? _scanCts;
    private StorageItem? _scannedRootItem;
    private DriveStatus _driveStatus = new();

    public string CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public ScanProgressInfo ScanProgress
    {
        get => _scanProgress;
        set => SetProperty(ref _scanProgress, value);
    }

    public DriveStatus DriveStatus
    {
        get => _driveStatus;
        set => SetProperty(ref _driveStatus, value);
    }

    // Sub-ViewModels
    public DashboardViewModel DashboardVM { get; }
    public ExplorerViewModel ExplorerVM { get; }
    public LargestFilesViewModel LargestFilesVM { get; }
    public DuplicatesViewModel DuplicatesVM { get; }
    public HistoryViewModel HistoryVM { get; }
    public RecurringViewModel RecurringVM { get; }
    public CleanupViewModel CleanupVM { get; }
    public ReportsViewModel ReportsVM { get; }
    public SettingsViewModel SettingsVM { get; }

    public object CurrentView => CurrentPage switch
    {
        "Dashboard" => DashboardVM,
        "Explorer" => ExplorerVM,
        "LargestFiles" => LargestFilesVM,
        "Duplicates" => DuplicatesVM,
        "History" => HistoryVM,
        "Recurring" => RecurringVM,
        "Cleanup" => CleanupVM,
        "Reports" => ReportsVM,
        "Settings" => SettingsVM,
        _ => DashboardVM
    };

    public ICommand NavigateCommand { get; }
    public ICommand StartScanCommand { get; }
    public ICommand CancelScanCommand { get; }

    public MainViewModel(
        IFileSystemScanner scanner,
        IStorageAnalyzer storageAnalyzer,
        ISnapshotRepository snapshotRepo,
        ICleanupEngine cleanupEngine,
        IDriveMonitor driveMonitor,
        ISettingsService settingsService,
        IGrowthAnalyzer growthAnalyzer,
        IRecurringGrowthDetector recurringDetector,
        ITrendAnalyzer trendAnalyzer,
        IStorageReportGenerator reportGenerator,
        ILoggerService logger)
    {
        _scanner = scanner;
        _storageAnalyzer = storageAnalyzer;
        _snapshotRepo = snapshotRepo;
        _cleanupEngine = cleanupEngine;
        _driveMonitor = driveMonitor;
        _settingsService = settingsService;
        _logger = logger;

        DashboardVM = new DashboardViewModel(storageAnalyzer, snapshotRepo, growthAnalyzer, NavigateTo);
        ExplorerVM = new ExplorerViewModel(scanner);
        LargestFilesVM = new LargestFilesViewModel(scanner);
        DuplicatesVM = new DuplicatesViewModel(scanner);
        HistoryVM = new HistoryViewModel(snapshotRepo, growthAnalyzer, trendAnalyzer);
        RecurringVM = new RecurringViewModel(recurringDetector, snapshotRepo);
        CleanupVM = new CleanupViewModel(cleanupEngine, snapshotRepo);
        ReportsVM = new ReportsViewModel(reportGenerator, storageAnalyzer, snapshotRepo, cleanupEngine);
        SettingsVM = new SettingsViewModel(settingsService, driveMonitor);

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is string page) NavigateTo(page);
        });

        StartScanCommand = new AsyncRelayCommand(StartFullScanAsync, () => !IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);

        _driveMonitor.DriveStatusChanged += (s, status) =>
        {
            DriveStatus = status;
            DashboardVM.DriveStatus = status;
        };
    }

    public void NavigateTo(string page)
    {
        CurrentPage = page;
        OnPropertyChanged(nameof(CurrentView));
        if (page == "History") _ = HistoryVM.LoadHistoryAsync();
        if (page == "Recurring") _ = RecurringVM.LoadRecurringAlertsAsync();
        if (page == "Cleanup") _ = CleanupVM.ScanForRecommendationsAsync();
    }

    public async Task InitializeAsync()
    {
        await _settingsService.LoadSettingsAsync();
        await _snapshotRepo.InitializeAsync();

        DriveStatus = _storageAnalyzer.GetDriveStatus("C:");
        await DashboardVM.LoadDashboardDataAsync();

        if (_settingsService.Settings.MonitoringEnabled)
        {
            _driveMonitor.StartMonitoring(_settingsService.Settings.MonitorIntervalMinutes);
        }

        // Automatic scan on first launch
        if (_settingsService.Settings.AutoScanOnLaunch)
        {
            _ = StartFullScanAsync();
        }
    }

    public async Task StartFullScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        _scanCts = new CancellationTokenSource();
        var progress = new Progress<ScanProgressInfo>(p => ScanProgress = p);

        try
        {
            _logger.LogInfo("Starting filesystem scan on C:...");
            DriveStatus = _storageAnalyzer.GetDriveStatus("C:");

            // 1. Scan user profiles and system directories for fast responsiveness
            string rootPath = "C:\\Users";
            if (!Directory.Exists(rootPath)) rootPath = "C:\\";

            var scanned = await _scanner.ScanDirectoryAsync(rootPath, progress, _scanCts.Token);
            _scannedRootItem = scanned;

            ExplorerVM.SetRootItem(scanned);

            // 2. Classify categories
            var categories = await _storageAnalyzer.AnalyzeCategoriesAsync(scanned, _scanCts.Token);
            DashboardVM.UpdateCategories(categories);

            // 3. Find largest files in parallel/background
            var largestFiles = await _scanner.FindLargestFilesAsync(rootPath, 100, null, _scanCts.Token);
            LargestFilesVM.SetLargestFiles(largestFiles);

            // 4. Save snapshot in SQLite
            var snapshot = new StorageSnapshot
            {
                DriveLetter = "C:",
                TotalBytes = DriveStatus.TotalBytes,
                FreeBytes = DriveStatus.FreeBytes,
                CategoriesJson = System.Text.Json.JsonSerializer.Serialize(categories),
                TopItemsJson = System.Text.Json.JsonSerializer.Serialize(scanned.Children.Take(30).ToList()),
                TimestampUtc = DateTime.UtcNow
            };
            await _snapshotRepo.SaveSnapshotAsync(snapshot);

            // 5. Update dashboard
            await DashboardVM.LoadDashboardDataAsync();
            _logger.LogInfo("Full scan completed successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Filesystem scan cancelled by user.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Scan failed unexpectedly.", ex);
        }
        finally
        {
            IsScanning = false;
            _scanCts = null;
        }
    }

    public void CancelScan()
    {
        _scanCts?.Cancel();
    }
}
