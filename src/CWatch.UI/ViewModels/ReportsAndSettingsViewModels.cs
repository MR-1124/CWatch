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
        ICleanupEngine cleanupEngine)
    {
        _reportGenerator = reportGenerator;
        _storageAnalyzer = storageAnalyzer;
        _snapshotRepo = snapshotRepo;
        _cleanupEngine = cleanupEngine;

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

            // Calculate overall storage health rating
            CalculateHealthScore(drive, cleanups);

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
            >= 85 => "EXCELLENT",
            >= 70 => "HEALTHY",
            >= 50 => "ATTENTION REQUIRED",
            _ => "CRITICAL CAPACITY RISK"
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
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #0B0C10; color: #F8F9FA; padding: 40px; line-height: 1.6; max-width: 960px; margin: 0 auto; }}
        h1 {{ color: #FF5722; border-bottom: 2px solid #232838; padding-bottom: 12px; font-size: 24px; }}
        h2 {{ color: #94A3B8; margin-top: 28px; font-size: 16px; text-transform: uppercase; letter-spacing: 0.5px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 16px 0; font-size: 13px; }}
        th, td {{ border: 1px solid #232838; padding: 10px 14px; text-align: left; }}
        th {{ background-color: #151822; color: #94A3B8; font-weight: bold; }}
        tr:nth-child(even) {{ background-color: #12141D; }}
        .badge {{ display: inline-block; padding: 3px 8px; border-radius: 3px; font-weight: bold; font-size: 11px; }}
        .badge-safe {{ background: #064E3B; color: #34D399; }}
        .badge-warn {{ background: #78350F; color: #FDE047; }}
        .card {{ background: #151822; border-radius: 4px; padding: 20px; margin: 16px 0; border: 1px solid #232838; }}
        .metric-grid {{ display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-top: 14px; }}
        .metric-box {{ background: #12141D; padding: 12px; border-radius: 3px; border: 1px solid #232838; }}
        .metric-val {{ font-size: 18px; font-weight: bold; color: #F8F9FA; margin-top: 4px; }}
    </style>
</head>
<body>
    <h1>C:Watch Storage Intelligence Report</h1>
    <div class='card'>
        <h2>Drive Overview (C:)</h2>
        <div class='metric-grid'>
            <div class='metric-box'><div>TOTAL CAPACITY</div><div class='metric-val'>{ByteSizeFormatter.Format(CurrentReport.DriveStatus.TotalBytes)}</div></div>
            <div class='metric-box'><div>USED ALLOCATION</div><div class='metric-val' style='color:#FF5722;'>{ByteSizeFormatter.Format(CurrentReport.DriveStatus.UsedBytes)} ({CurrentReport.DriveStatus.UsedPercentage:F1}%)</div></div>
            <div class='metric-box'><div>AVAILABLE HEADROOM</div><div class='metric-val' style='color:#10B981;'>{ByteSizeFormatter.Format(CurrentReport.DriveStatus.FreeBytes)} ({CurrentReport.DriveStatus.FreePercentage:F1}%)</div></div>
        </div>
        <p style='margin-top: 16px;'><strong>Safe Cleanup Potential:</strong> <span style='color:#10B981; font-weight:bold;'>{CurrentReport.FormattedRecommendedCleanup}</span></p>
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
    <p style='color: #64748B; font-size: 12px; margin-top: 40px;'>Generated by C:Watch — 100% Local & Privacy-Focused Storage Intelligence.</p>
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
