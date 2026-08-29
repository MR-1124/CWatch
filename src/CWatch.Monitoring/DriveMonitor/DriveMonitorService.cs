using System.Text.Json;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.Monitoring.DriveMonitor;

public sealed class DriveMonitorService : IDriveMonitor
{
    private readonly IStorageAnalyzer _storageAnalyzer;
    private readonly ISnapshotRepository _snapshotRepo;
    private readonly ISettingsService _settingsService;
    private readonly ILoggerService? _logger;
    private System.Threading.Timer? _timer;
    private DriveStatus _currentStatus = new();
    private bool _isRunning;
    private long _lastSnapshotRecordedBytes = -1;

    public event EventHandler<DriveStatus>? DriveStatusChanged;
    public event EventHandler<DriveStatus>? LowDiskSpaceAlert;

    public DriveStatus CurrentStatus => _currentStatus;
    public bool IsRunning => _isRunning;

    public DriveMonitorService(
        IStorageAnalyzer storageAnalyzer,
        ISnapshotRepository snapshotRepo,
        ISettingsService settingsService,
        ILoggerService? logger = null)
    {
        _storageAnalyzer = storageAnalyzer;
        _snapshotRepo = snapshotRepo;
        _settingsService = settingsService;
        _logger = logger;
    }

    public void StartMonitoring(int intervalMinutes = 30)
    {
        if (_isRunning) return;
        _isRunning = true;
        int intervalMs = Math.Max(1, intervalMinutes) * 60 * 1000;
        _timer = new System.Threading.Timer(OnTimerTick, null, 0, intervalMs);
        _logger?.LogInfo($"Drive monitoring started with {intervalMinutes}m interval.");
    }

    public void StopMonitoring()
    {
        _isRunning = false;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _timer?.Dispose();
        _timer = null;
        _logger?.LogInfo("Drive monitoring stopped.");
    }

    public async Task TriggerImmediateCheckAsync()
    {
        await PerformCheckAsync();
    }

    private async void OnTimerTick(object? state)
    {
        try
        {
            await PerformCheckAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error during drive monitor periodic check.", ex);
        }
    }

    private async Task PerformCheckAsync()
    {
        string driveLetter = _settingsService.Settings.TargetDriveLetter;
        var status = _storageAnalyzer.GetDriveStatus(driveLetter);
        _currentStatus = status;

        DriveStatusChanged?.Invoke(this, status);

        // Check Low Free Space Thresholds
        long criticalBytes = _settingsService.Settings.CriticalThresholdGb * 1024 * 1024 * 1024;
        long warningBytes = _settingsService.Settings.WarningThresholdGb * 1024 * 1024 * 1024;

        if (status.FreeBytes <= criticalBytes || status.FreeBytes <= warningBytes)
        {
            LowDiskSpaceAlert?.Invoke(this, status);
        }

        // Save a snapshot if free bytes changed by > 500MB or never recorded yet
        if (_lastSnapshotRecordedBytes < 0 || Math.Abs(status.FreeBytes - _lastSnapshotRecordedBytes) > 500 * 1024 * 1024)
        {
            _lastSnapshotRecordedBytes = status.FreeBytes;
            await _snapshotRepo.SaveSnapshotAsync(new StorageSnapshot
            {
                DriveLetter = status.DriveLetter,
                TotalBytes = status.TotalBytes,
                FreeBytes = status.FreeBytes,
                TimestampUtc = DateTime.UtcNow
            });
        }
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
