using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.UI.Services;

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

    /// <summary>The configured target drive ("X:") that scans, snapshots, and monitoring observe.</summary>
    public string TargetDrive => DriveLetters.Normalize(_settingsService.Settings.TargetDriveLetter);

    public string CurrentThemeMode
    {
        get => _settingsService.Settings.AppTheme;
        set
        {
            _settingsService.Settings.AppTheme = value;
            SaveSettingsSafely();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeModeLabel));
            OnPropertyChanged(nameof(ThemeModeTooltip));
            ApplyThemeFromSetting(value);
        }
    }

    public string ThemeModeLabel => CurrentThemeMode switch
    {
        "Light" => "LIGHT",
        "System" => "AUTO",
        _ => "DARK"
    };

    public string ThemeModeTooltip => CurrentThemeMode switch
    {
        "Light" => "Theme: Light (Click to switch to Auto/System)",
        "System" => "Theme: Auto/System (Click to switch to Dark)",
        _ => "Theme: Dark Cockpit (Click to switch to Light)"
    };

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
    public ICommand ToggleThemeCommand { get; }
    public ICommand SetThemeCommand { get; }

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

        DashboardVM = new DashboardViewModel(storageAnalyzer, snapshotRepo, growthAnalyzer, settingsService, NavigateTo);
        ExplorerVM = new ExplorerViewModel(scanner);
        LargestFilesVM = new LargestFilesViewModel(scanner);
        DuplicatesVM = new DuplicatesViewModel(scanner);
        HistoryVM = new HistoryViewModel(snapshotRepo, growthAnalyzer, trendAnalyzer, settingsService, storageAnalyzer);
        RecurringVM = new RecurringViewModel(recurringDetector, snapshotRepo, settingsService);
        CleanupVM = new CleanupViewModel(cleanupEngine, snapshotRepo);
        ReportsVM = new ReportsViewModel(reportGenerator, storageAnalyzer, snapshotRepo, cleanupEngine, settingsService);
        SettingsVM = new SettingsViewModel(settingsService, driveMonitor, ApplySavedSettingsToApp);

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is string page) NavigateTo(page);
        });

        StartScanCommand = new AsyncRelayCommand(StartFullScanAsync, () => !IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        SetThemeCommand = new RelayCommand(param =>
        {
            if (param is string themeStr) CurrentThemeMode = themeStr;
        });

        _driveMonitor.DriveStatusChanged += (s, status) =>
        {
            DriveStatus = status;
            DashboardVM.DriveStatus = status;
        };
    }

    public void ToggleTheme()
    {
        CurrentThemeMode = CurrentThemeMode switch
        {
            "Dark" => "Light",
            "Light" => "System",
            _ => "Dark"
        };
    }

    /// <summary>
    /// Re-targets the running application after settings are saved: refreshes the
    /// dashboard for a new target drive, restarts monitoring with the new
    /// interval, and applies the theme. Called by SettingsViewModel on save.
    /// </summary>
    public async Task ApplySavedSettingsToApp()
    {
        DriveStatus = _storageAnalyzer.GetDriveStatus(TargetDrive);
        await DashboardVM.LoadDashboardDataAsync();

        if (_settingsService.Settings.MonitoringEnabled && !_driveMonitor.IsRunning)
        {
            _driveMonitor.StartMonitoring(_settingsService.Settings.MonitorIntervalMinutes);
        }
        else if (!_settingsService.Settings.MonitoringEnabled && _driveMonitor.IsRunning)
        {
            _driveMonitor.StopMonitoring();
        }
    }

    private void ApplyThemeFromSetting(string themeName)
    {
        if (Enum.TryParse<ThemeMode>(themeName, true, out var mode))
        {
            ThemeManager.Instance.SetTheme(mode);
        }
    }    public void NavigateTo(string page)
    {
        CurrentPage = page;
        OnPropertyChanged(nameof(CurrentView));

        // Commands, not bare tasks: failures surface in-page instead of becoming
        // unobserved task exceptions, and the guard inside AsyncRelayCommand
        // prevents overlapping loads from rapid navigation.
        if (page == "History") HistoryVM.RefreshCommand.Execute(null);
        if (page == "Recurring") RecurringVM.RefreshCommand.Execute(null);
        if (page == "Cleanup") CleanupVM.ScanCandidatesCommand.Execute(null);
    }

    public async Task InitializeAsync()
    {
        await _settingsService.LoadSettingsAsync();
        ApplyThemeFromSetting(_settingsService.Settings.AppTheme);

        await _snapshotRepo.InitializeAsync();

        // Surface a settings-file failure instead of silently running on defaults.
        if (_settingsService is Infrastructure.Config.SettingsService concrete
            && concrete.LastLoadHadProblems)
        {
            DashboardVM.SetStartupNotice(
                "Settings file was unreadable, so defaults were restored. Check the log for details.");
        }

        // Enforce snapshot retention at startup: deletes records older than
        // Settings.RetentionDays and normalizes DB files that grew unbounded.
        try
        {
            await _snapshotRepo.PruneOldSnapshotsAsync(_settingsService.Settings.RetentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogError("Startup snapshot pruning failed.", ex);
        }

        DriveStatus = _storageAnalyzer.GetDriveStatus(TargetDrive);
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
            string targetDrive = TargetDrive;
            _logger.LogInfo($"Starting filesystem scan on {targetDrive}...");
            DriveStatus = _storageAnalyzer.GetDriveStatus(targetDrive);

            // 1. Scan user profiles and system directories for fast responsiveness
            string rootPath = ResolveScanRoot(targetDrive);

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
                DriveLetter = targetDrive,
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

    /// <summary>
    /// Fire-and-forget persistence for non-critical writes (theme toggle). The
    /// failure is logged and nothing escapes as an unobserved task exception.
    /// </summary>
    private async void SaveSettingsSafely()
    {
        try
        {
            await _settingsService.SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("Background settings save failed.", ex);
        }
    }

    /// <summary>
    /// Resolves the scan root for a drive: the \Users tree when present (fast, high-signal),
    /// otherwise the volume root itself.
    /// </summary>
    private static string ResolveScanRoot(string driveLetter)
    {
        string rootPath = DriveLetters.GetRootPath(driveLetter);
        string usersDir = Path.Combine(rootPath, "Users");
        return Directory.Exists(usersDir) ? usersDir : rootPath;
    }
}
