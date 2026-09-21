using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Infrastructure.WindowsApi;

namespace CWatch.UI.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private readonly IStorageReportGenerator _reportGenerator;
    private readonly IStorageAnalyzer _storageAnalyzer;
    private readonly ISnapshotRepository _snapshotRepo;
    private readonly ICleanupEngine _cleanupEngine;
    private readonly ISettingsService _settingsService;

    private StorageReport? _currentReport;
    private string _reportText = "Click 'Generate Report' to create an executive filesystem diagnosis.";
    private bool _isGenerating;
    private string _statusMessage = string.Empty;
    private int _healthScore = 85;
    private string _healthRating = "HEALTHY";

    public StorageReport? CurrentReport
    {
        get => _currentReport;
        set => SetProperty(ref _currentReport, value);
    }

    public string ReportText
    {
        get => _reportText;
        set => SetProperty(ref _reportText, value);
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        set => SetProperty(ref _isGenerating, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int HealthScore
    {
        get => _healthScore;
        set => SetProperty(ref _healthScore, value);
    }

    public string HealthRating
    {
        get => _healthRating;
        set => SetProperty(ref _healthRating, value);
    }

    public ICommand GenerateReportCommand { get; }
    public ICommand CopyReportCommand { get; }
    public ICommand ExportHtmlReportCommand { get; }

    public ReportsViewModel(
        IStorageReportGenerator reportGenerator,
        IStorageAnalyzer storageAnalyzer,
        ISnapshotRepository snapshotRepo,
        ICleanupEngine cleanupEngine,
        ISettingsService settingsService)
    {
        _reportGenerator = reportGenerator;
        _storageAnalyzer = storageAnalyzer;
        _snapshotRepo = snapshotRepo;
        _cleanupEngine = cleanupEngine;
        _settingsService = settingsService;

        GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync, () => !IsGenerating);
        CopyReportCommand = new RelayCommand(CopyReportToClipboard);
        ExportHtmlReportCommand = new RelayCommand(ExportReportHtml);
    }

    public async Task GenerateReportAsync()
    {
        IsGenerating = true;
        StatusMessage = "Analyzing volume telemetry, growth trends, and safe cleanup potential...";

        try
        {
            string drive = DriveLetters.Normalize(_settingsService.Settings.TargetDriveLetter);
            var driveStatus = _storageAnalyzer.GetDriveStatus(drive);
            var history = await _snapshotRepo.GetAllSnapshotsAsync(drive, 30);
            var cleanups = await _cleanupEngine.ScanAllRecommendationsAsync();

            var dummyRoot = new StorageItem
            {
                Name = drive,
                SizeBytes = driveStatus.UsedBytes
            };

            var report = await _reportGenerator.GenerateReportAsync(driveStatus, dummyRoot, history, cleanups);
            CurrentReport = report;
            ReportText = report.SummaryText;

            // Calculate overall storage health rating
            CalculateHealthScore(driveStatus, cleanups);

            StatusMessage = "Storage Intelligence Report generated successfully.";
        }
        catch (Exception ex)
        {
            ReportText = $"Report generation failed: {ex.Message}";
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private void CalculateHealthScore(DriveStatus drive, List<CleanupCandidate> cleanups)
    {
        int score = 100;

        // Deduct for high usage
        if (drive.UsedPercentage > 90) score -= 35;
        else if (drive.UsedPercentage > 80) score -= 20;
        else if (drive.UsedPercentage > 70) score -= 10;

        // Deduct for large accumulating junk (> 20GB)
        long totalJunk = cleanups.Sum(c => c.SizeBytes);
        if (totalJunk > 20L * 1024 * 1024 * 1024) score -= 15;
        else if (totalJunk > 5L * 1024 * 1024 * 1024) score -= 8;

        HealthScore = Math.Clamp(score, 10, 100);

        HealthRating = HealthScore switch
        {
            >= 85 => "Excellent",
            >= 70 => "Healthy",
            >= 50 => "Attention needed",
            _ => "Critical"
        };
    }

    private void CopyReportToClipboard()
    {
        if (CurrentReport != null)
        {
            Clipboard.SetText(CurrentReport.SummaryText);
            StatusMessage = "Markdown report copied to clipboard.";
        }
    }

    private void ExportReportHtml()
    {
        if (CurrentReport == null) return;

        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktop, $"CWatch_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html");

            string html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>C:Watch Storage Intelligence Report</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #0C0E12; color: #F2F4F7; padding: 40px; line-height: 1.6; max-width: 960px; margin: 0 auto; }}
        h1 {{ color: #F2632B; border-bottom: 2px solid #252B38; padding-bottom: 12px; font-size: 24px; }}
        h2 {{ color: #9AA6B5; margin-top: 28px; font-size: 14px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 16px 0; font-size: 13px; }}
        th, td {{ border: 1px solid #252B38; padding: 10px 14px; text-align: left; }}
        th {{ background-color: #151922; color: #9AA6B5; font-weight: bold; }}
        tr:nth-child(even) {{ background-color: #11141C; }}
        .badge {{ display: inline-block; padding: 3px 8px; border-radius: 3px; font-weight: bold; font-size: 11px; }}
        .badge-safe {{ background: #14301F; color: #3DB583; }}
        .badge-warn {{ background: #33270D; color: #E3A93C; }}
        .card {{ background: #151922; border-radius: 6px; padding: 20px; margin: 16px 0; border: 1px solid #252B38; }}
        .metric-grid {{ display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-top: 14px; }}
        .metric-box {{ background: #11141C; padding: 12px; border-radius: 4px; border: 1px solid #252B38; }}
        .metric-val {{ font-size: 18px; font-weight: bold; color: #F2F4F7; margin-top: 4px; }}
    </style>
</head>
<body>
    <h1>C:Watch Storage Intelligence Report</h1>
    <div class='card'>
        <h2>Drive overview ({CurrentReport.DriveStatus.DriveLetter})</h2>
        <div class='metric-grid'>
            <div class='metric-box'><div>Total capacity</div><div class='metric-val'>{ByteSizeFormatter.Format(CurrentReport.DriveStatus.TotalBytes)}</div></div>
            <div class='metric-box'><div>Used</div><div class='metric-val' style='color:#F2632B;'>{ByteSizeFormatter.Format(CurrentReport.DriveStatus.UsedBytes)} ({CurrentReport.DriveStatus.UsedPercentage:F1}%)</div></div>
            <div class='metric-box'><div>Free</div><div class='metric-val' style='color:#3DB583;'>{ByteSizeFormatter.Format(CurrentReport.DriveStatus.FreeBytes)} ({CurrentReport.DriveStatus.FreePercentage:F1}%)</div></div>
        </div>
        <p style='margin-top: 16px;'><strong>Safe cleanup potential:</strong> <span style='color:#3DB583; font-weight:bold;'>{CurrentReport.FormattedRecommendedCleanup}</span></p>
    </div>

    <h2>Recommended cleanups</h2>
    <table>
        <tr><th>Item</th><th>Category</th><th>Size</th><th>Safety</th><th>Reason</th></tr>";

            foreach (var rec in CurrentReport.RecommendedCleanups.Where(r => r.CanClean))
            {
                html += $@"
        <tr>
            <td><strong>{rec.Title}</strong></td>
            <td>{rec.Category}</td>
            <td>{rec.FormattedSize}</td>
            <td><span class='badge badge-safe'>{rec.SafetyBadgeText}</span></td>
            <td>{rec.Reason}</td>
        </tr>";
            }

            html += @"
    </table>
    <p style='color: #677182; font-size: 12px; margin-top: 40px;'>Generated by C:Watch — 100% local and privacy-focused storage intelligence.</p>
</body>
</html>";

            File.WriteAllText(filePath, html);
            StatusMessage = $"Exported HTML report to {filePath}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }
}

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IDriveMonitor _driveMonitor;
    private readonly Func<Task> _applySettingsToApp;
    private readonly string _configuredDrive;
    private string _statusMessage = string.Empty;
    private bool _hasUnsavedChanges;
    private bool _isSaving;
    private AppSettings _snapshotAtLoad;

    public SettingsViewModel(ISettingsService settingsService, IDriveMonitor driveMonitor)
        : this(settingsService, driveMonitor, null)
    {
    }

    /// <summary>
    /// Full constructor. <paramref name="applySettingsToApp"/> re-targets the live app
    /// (dashboard, monitoring, theme) after a successful save.
    /// </summary>
    public SettingsViewModel(
        ISettingsService settingsService,
        IDriveMonitor driveMonitor,
        Func<Task>? applySettingsToApp)
    {
        _settingsService = settingsService;
        _driveMonitor = driveMonitor;
        _applySettingsToApp = applySettingsToApp ?? ApplyAsync;

        // Edit a clone; the service's live instance only changes on Save.
        _snapshotAtLoad = Clone(_settingsService.Settings);
        Settings = Clone(_settingsService.Settings);
        _configuredDrive = DriveLetters.Normalize(Settings.TargetDriveLetter);

        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => DriveLetters.Normalize(d.Name))
            .Distinct()
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Always offer the configured target even if not enumerable right now.
        if (!drives.Contains(_configuredDrive))
        {
            drives.Insert(0, _configuredDrive);
        }

        AvailableDrives = drives;

        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => HasUnsavedChanges && !IsSaving);
        ResetDefaultsCommand = new AsyncRelayCommand(ResetDefaultsAsync);
        DiscardChangesCommand = new RelayCommand(DiscardChanges);

        WireSettingsNotifications();
    }

    public AppSettings Settings { get; private set; }

    public IReadOnlyList<string> AvailableDrives { get; }

    public string SelectedDrive
    {
        get => DriveLetters.Normalize(Settings.TargetDriveLetter);
        set
        {
            string normalized = DriveLetters.Normalize(value);
            if (Settings.TargetDriveLetter != normalized)
            {
                Settings.TargetDriveLetter = normalized;
                OnPropertyChanged();
            }
        }
    }

    public string SelectedTheme
    {
        get => Settings.AppTheme;
        set
        {
            if (Settings.AppTheme != value)
            {
                Settings.AppTheme = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>True when any edited value differs from the last saved snapshot.</summary>
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set
        {
            if (SetProperty(ref _hasUnsavedChanges, value))
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        set
        {
            if (SetProperty(ref _isSaving, value))
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetDefaultsCommand { get; }
    public ICommand DiscardChangesCommand { get; }

    public async Task SaveSettingsAsync()
    {
        IsSaving = true;
        try
        {
            // Push edited clone into the live service instance, then persist.
            CopyInto(Settings, _settingsService.Settings);
            await _settingsService.SaveSettingsAsync();
            _snapshotAtLoad = Clone(_settingsService.Settings);
            HasUnsavedChanges = false;
            StatusMessage = "Settings saved.";

            // Re-target the running app: dashboard, monitoring, theme.
            await _applySettingsToApp();
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void DiscardChanges()
    {
        Settings = Clone(_snapshotAtLoad);
        WireSettingsNotifications();
        HasUnsavedChanges = false;
        StatusMessage = "Changes discarded.";
        OnPropertyChanged(nameof(SelectedDrive));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(Settings));
    }

    private async Task ResetDefaultsAsync()
    {
        // Stage defaults on the clone. Resetting used to swap the service's live
        // AppSettings reference and save immediately, desyncing persistence; now
        // reset is an edit like any other and requires Save to take effect.
        Settings = new AppSettings();
        WireSettingsNotifications();
        HasUnsavedChanges = true;
        StatusMessage = "Defaults staged. Review and save to apply.";
        OnPropertyChanged(nameof(SelectedDrive));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(Settings));
        await Task.CompletedTask;
    }

    private async Task ApplyAsync()
    {
        // Fallback applier when no app-level hook is wired: theme + monitor only.
        if (Enum.TryParse<CWatch.UI.Services.ThemeMode>(Settings.AppTheme, true, out var mode))
        {
            CWatch.UI.Services.ThemeManager.Instance.SetTheme(mode);
        }

        ApplyMonitoring();
        await Task.CompletedTask;
    }

    private void ApplyMonitoring()
    {
        if (Settings.MonitoringEnabled && !_driveMonitor.IsRunning)
        {
            _driveMonitor.StartMonitoring(Settings.MonitorIntervalMinutes);
        }
        else if (!Settings.MonitoringEnabled && _driveMonitor.IsRunning)
        {
            _driveMonitor.StopMonitoring();
        }
    }

    private bool HasPendingEdits()
    {
        var a = _snapshotAtLoad;
        var b = Settings;
        return a.TargetDriveLetter != b.TargetDriveLetter
            || a.AppTheme != b.AppTheme
            || a.MonitoringEnabled != b.MonitoringEnabled
            || a.MonitorIntervalMinutes != b.MonitorIntervalMinutes
            || a.WarningThresholdGb != b.WarningThresholdGb
            || a.CriticalThresholdGb != b.CriticalThresholdGb
            || a.RetentionDays != b.RetentionDays
            || a.AutoScanOnLaunch != b.AutoScanOnLaunch
            || !a.ExcludedPaths.SequenceEqual(b.ExcludedPaths);
    }

    private static AppSettings Clone(AppSettings s) => new()
    {
        AppTheme = s.AppTheme,
        MonitoringEnabled = s.MonitoringEnabled,
        MonitorIntervalMinutes = s.MonitorIntervalMinutes,
        WarningThresholdGb = s.WarningThresholdGb,
        CriticalThresholdGb = s.CriticalThresholdGb,
        RetentionDays = s.RetentionDays,
        ExcludedPaths = [.. s.ExcludedPaths],
        AutoScanOnLaunch = s.AutoScanOnLaunch,
        TargetDriveLetter = s.TargetDriveLetter
    };

    private static void CopyInto(AppSettings from, AppSettings to)
    {
        to.AppTheme = from.AppTheme;
        to.MonitoringEnabled = from.MonitoringEnabled;
        to.MonitorIntervalMinutes = from.MonitorIntervalMinutes;
        to.WarningThresholdGb = from.WarningThresholdGb;
        to.CriticalThresholdGb = from.CriticalThresholdGb;
        to.RetentionDays = from.RetentionDays;
        to.ExcludedPaths = [.. from.ExcludedPaths];
        to.AutoScanOnLaunch = from.AutoScanOnLaunch;
        to.TargetDriveLetter = from.TargetDriveLetter;
    }

    private void WireSettingsNotifications()
    {
        Settings.PropertyChanged -= OnSettingsPropertyChanged;
        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.TargetDriveLetter) or nameof(AppSettings.AppTheme))
        {
            OnPropertyChanged(nameof(SelectedDrive));
            OnPropertyChanged(nameof(SelectedTheme));
        }
        HasUnsavedChanges = HasPendingEdits();
    }
}
