namespace CWatch.Core.Models;

/// <summary>
/// Represents the high-level physical storage metrics of a disk volume.
/// </summary>
public sealed class DriveStatus
{
    public string DriveLetter { get; set; } = "C:";
    public string VolumeLabel { get; set; } = string.Empty;
    public string FileSystem { get; set; } = "NTFS";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get => TotalBytes - FreeBytes; set { } }
    public double FreePercentage { get => TotalBytes > 0 ? (double)FreeBytes / TotalBytes * 100.0 : 0; set { } }
    public double UsedPercentage { get => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100.0 : 0; set { } }
    public bool IsSystemDrive { get; set; } = true;
    public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;

    public bool IsCriticallyLow(long criticalThresholdBytes = 10L * 1024 * 1024 * 1024)
        => FreeBytes <= criticalThresholdBytes;

    public bool IsWarningLow(long warningThresholdBytes = 25L * 1024 * 1024 * 1024)
        => FreeBytes <= warningThresholdBytes;
}
