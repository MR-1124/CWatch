using CWatch.Core.Enums;

namespace CWatch.Core.Models;

/// <summary>
/// Aggregated storage statistics for a classified domain category.
/// </summary>
public sealed class CategoryBreakdown
{
    public StorageCategoryType CategoryType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public double PercentageOfUsed { get; set; }
    public long ItemCount { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";
    public string IconKey { get; set; } = "FolderIcon";
    public string Description { get; set; } = string.Empty;
    public List<StorageItem> TopItems { get; set; } = [];

    public string FormattedSize { get => ByteSizeFormatter.Format(SizeBytes); set { } }
    public string FormattedPercentage { get => $"{PercentageOfUsed:F1}%"; set { } }
}

/// <summary>
/// Historical point-in-time storage state stored in SQLite.
/// </summary>
public sealed class StorageSnapshot
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string DriveLetter { get; set; } = "C:";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get => TotalBytes - FreeBytes; set { } }
    public string CategoriesJson { get; set; } = "[]";
    public string TopItemsJson { get; set; } = "[]";
    public string? Notes { get; set; }

    public string FormattedFree { get => ByteSizeFormatter.Format(FreeBytes); set { } }
    public string FormattedUsed { get => ByteSizeFormatter.Format(UsedBytes); set { } }
    public string FormattedTotal { get => ByteSizeFormatter.Format(TotalBytes); set { } }
}

/// <summary>
/// Measured difference between two historical points in time for a specific directory or category.
/// </summary>
public sealed class GrowthDelta
{
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public StorageCategoryType Category { get; set; } = StorageCategoryType.Other;
    public long PreviousSizeBytes { get; set; }
    public long CurrentSizeBytes { get; set; }
    public long DeltaBytes { get => CurrentSizeBytes - PreviousSizeBytes; set { } }
    public double GrowthPercentage { get => PreviousSizeBytes > 0 ? ((double)DeltaBytes / PreviousSizeBytes) * 100.0 : 100.0; set { } }
    public DateTime PreviousTimestampUtc { get; set; }
    public DateTime CurrentTimestampUtc { get; set; }
    public bool IsNew { get; set; }

    public string FormattedDelta { get => ByteSizeFormatter.FormatDelta(DeltaBytes); set { } }
    public string FormattedCurrentSize { get => ByteSizeFormatter.Format(CurrentSizeBytes); set { } }
    public string FormattedPreviousSize { get => ByteSizeFormatter.Format(PreviousSizeBytes); set { } }
}

/// <summary>
/// Alert raised when a directory exhibits repeating storage growth cycles (e.g. cleaned, then refilled).
/// </summary>
public sealed class RecurringGrowthAlert
{
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public StorageCategoryType Category { get; set; } = StorageCategoryType.Other;
    public long CurrentSizeBytes { get; set; }
    public double DailyGrowthRateBytes { get; set; }
    public int CleanedCount { get; set; }
    public DateTime? LastCleanedUtc { get; set; }
    public long RegrownBytesSinceClean { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Consequence { get; set; } = string.Empty;
    public List<StorageSnapshot> GrowthHistory { get; set; } = [];

    public string FormattedCurrentSize { get => ByteSizeFormatter.Format(CurrentSizeBytes); set { } }
    public string FormattedDailyRate { get => $"{ByteSizeFormatter.Format((long)DailyGrowthRateBytes)}/day"; set { } }
    public string FormattedRegrown { get => ByteSizeFormatter.Format(RegrownBytesSinceClean); set { } }
}
