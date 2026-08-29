using System.Windows;
using System.Windows.Input;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.UI.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private readonly IStorageReportGenerator _reportGenerator;
    private readonly IStorageAnalyzer _storageAnalyzer;
    private readonly ISnapshotRepository _snapshotRepo;
    private readonly ICleanupEngine _cleanupEngine;

    private bool _isGenerating;
    private StorageReport? _currentReport;
    private string _reportText = "Click 'Generate Report' to create an instant Storage Intelligence summary.";

    public bool IsGenerating
    {
        get => _isGenerating;
        set => SetProperty(ref _isGenerating, value);
    }

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

    public ICommand GenerateReportCommand { get; }
    public ICommand CopyReportCommand { get; }
    public ICommand ExportHtmlReportCommand { get; }

    public ReportsViewModel(
        IStorageReportGenerator reportGenerator,
        IStorageAnalyzer storageAnalyzer,
        ISnapshotRepository snapshotRepo,
        ICleanupEngine cleanupEngine)
    {
        _reportGenerator = reportGenerator;
        _storageAnalyzer = storageAnalyzer;
        _snapshotRepo = snapshotRepo;
        _cleanupEngine = cleanupEngine;

        GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync, () => !IsGenerating);
        CopyReportCommand = new RelayCommand(CopyReportToClipboard, () => CurrentReport != null);
        ExportHtmlReportCommand = new RelayCommand(ExportReportHtml, () => CurrentReport != null);
    }

    public async Task GenerateReportAsync()
    {
        IsGenerating = true;
        try
        {
            var drive = _storageAnalyzer.GetDriveStatus("C:");
            var history = await _snapshotRepo.GetAllSnapshotsAsync("C:", 30);
            var cleanups = await _cleanupEngine.ScanAllRecommendationsAsync();

            var dummyRoot = new StorageItem
            {
                Name = "C:",
                SizeBytes = drive.UsedBytes
            };

            var report = await _reportGenerator.GenerateReportAsync(drive, dummyRoot, history, cleanups);
            CurrentReport = report;
            ReportText = report.SummaryText;
        }
        catch (Exception ex)
        {
            ReportText = $"Report generation failed: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private void CopyReportToClipboard()
    {
        if (CurrentReport != null)
        {
            Clipboard.SetText(CurrentReport.SummaryText);
        }
    }

    private void ExportReportHtml()
    {
        if (CurrentReport == null) return;

        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = System.IO.Path.Combine(desktop, $"CWatch_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html");

            string html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>C:Watch Storage Intelligence Report</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #0f172a; color: #f8fafc; padding: 40px; line-height: 1.6; max-width: 900px; margin: 0 auto; }}
        h1 {{ color: #38bdf8; border-bottom: 2px solid #334155; padding-bottom: 12px; }}
        h2 {{ color: #94a3b8; margin-top: 28px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        th, td {{ border: 1px solid #334155; padding: 12px 16px; text-align: left; }}
        th {{ background-color: #1e293b; color: #38bdf8; }}
        tr:nth-child(even) {{ background-color: #1e293b; }}
        .badge {{ display: inline-block; padding: 4px 10px; border-radius: 9999px; font-weight: 600; font-size: 12px; }}
        .badge-safe {{ background: #065f46; color: #34d399; }}
        .badge-warn {{ background: #78350f; color: #fde047; }}
        .card {{ background: #1e293b; border-radius: 12px; padding: 20px; margin: 20px 0; border: 1px solid #334155; }}
    </style>
</head>
<body>
    <h1>C:Watch Storage Intelligence Report</h1>
    <div class='card'>
        <h2>Drive Overview (C:)</h2>
        <p><strong>Total Capacity:</strong> {ByteSizeFormatter.Format(CurrentReport.DriveStatus.TotalBytes)}</p>
        <p><strong>Used Space:</strong> {ByteSizeFormatter.Format(CurrentReport.DriveStatus.UsedBytes)} ({CurrentReport.DriveStatus.UsedPercentage:F1}%)</p>
        <p><strong>Free Space:</strong> {ByteSizeFormatter.Format(CurrentReport.DriveStatus.FreeBytes)} ({CurrentReport.DriveStatus.FreePercentage:F1}%)</p>
        <p><strong>Safe Cleanup Potential:</strong> {CurrentReport.FormattedRecommendedCleanup}</p>
    </div>

    <h2>Recommended Cleanups</h2>
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
    <p style='color: #64748b; font-size: 12px; margin-top: 40px;'>Generated by C:Watch - 100% Local & Privacy Focused Storage Intelligence.</p>
</body>
</html>";

            System.IO.File.WriteAllText(filePath, html);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch { }
    }
}

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IDriveMonitor _driveMonitor;
    private AppSettings _settings;
    private string _statusMessage = string.Empty;

    public AppSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
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
                if (Enum.TryParse<CWatch.UI.Services.ThemeMode>(value, true, out var mode))
                {
                    CWatch.UI.Services.ThemeManager.Instance.SetTheme(mode);
                }
                _ = SaveSettingsAsync();
            }
        }
    }

    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetDefaultsCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, IDriveMonitor driveMonitor)
    {
        _settingsService = settingsService;
        _driveMonitor = driveMonitor;
        _settings = _settingsService.Settings;

        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ResetDefaultsCommand = new AsyncRelayCommand(ResetDefaultsAsync);
    }

    public async Task SaveSettingsAsync()
    {
        await _settingsService.SaveSettingsAsync();
        StatusMessage = "Settings saved successfully.";
        
        if (Enum.TryParse<CWatch.UI.Services.ThemeMode>(Settings.AppTheme, true, out var mode))
        {
            CWatch.UI.Services.ThemeManager.Instance.SetTheme(mode);
        }

        if (Settings.MonitoringEnabled && !_driveMonitor.IsRunning)
        {
            _driveMonitor.StartMonitoring(Settings.MonitorIntervalMinutes);
        }
        else if (!Settings.MonitoringEnabled && _driveMonitor.IsRunning)
        {
            _driveMonitor.StopMonitoring();
        }
    }

    private async Task ResetDefaultsAsync()
    {
        Settings = new AppSettings();
        await _settingsService.SaveSettingsAsync();
        StatusMessage = "Settings restored to defaults.";
        SelectedTheme = Settings.AppTheme;
    }
}
