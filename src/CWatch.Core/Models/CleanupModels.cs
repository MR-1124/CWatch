using CWatch.Core.Enums;

namespace CWatch.Core.Models;

/// <summary>
/// A recommended cleanup candidate discovered on the system with explicit human rationale.
/// </summary>
public sealed class CleanupCandidate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProviderId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public SafetyLevel Safety { get; set; } = SafetyLevel.Safe;
    public string Reason { get; set; } = string.Empty;
    public string WhatWillHappen { get; set; } = string.Empty;
    public bool WillRegenerate { get; set; } = true;
    public bool CanClean { get; set; } = true;
    public bool IsSelected { get; set; } = true;
    public bool RequiresElevation { get; set; }
    public List<string> AffectedFilePaths { get; set; } = [];
    public StorageCategoryType Category { get; set; } = StorageCategoryType.TemporaryFiles;

    public string FormattedSize { get => ByteSizeFormatter.Format(SizeBytes); set { } }

    public string SafetyBadgeText
    {
        get => Safety switch
        {
            SafetyLevel.Safe => "SAFE",
            SafetyLevel.LowRisk => "LOW RISK",
            SafetyLevel.Review => "REVIEW",
            SafetyLevel.Dangerous => "DO NOT DELETE",
            _ => "UNKNOWN"
        };
        set { }
    }

    public string SafetyBadgeColor
    {
        get => Safety switch
        {
            SafetyLevel.Safe => "#10B981",       // Emerald Green
            SafetyLevel.LowRisk => "#3B82F6",    // Blue
            SafetyLevel.Review => "#F59E0B",     // Amber
            SafetyLevel.Dangerous => "#EF4444",  // Red
            _ => "#6B7280"                       // Gray
        };
        set { }
    }
}

/// <summary>
/// Information about a locked or in-use file discovered during cleanup operations.
/// </summary>
public sealed class LockedFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string LockingProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// The outcome of an executed cleanup operation.
/// </summary>
public sealed class CleanupResult
{
    public bool Success { get; set; } = true;
    public long BytesCleaned { get; set; }
    public int ItemsCleanedCount { get; set; }
    public int FailedItemsCount { get; set; }
    public List<LockedFileInfo> LockedFiles { get; set; } = [];
    public List<string> ErrorMessages { get; set; } = [];
    public TimeSpan Duration { get; set; }
    public DateTime ExecutedUtc { get; set; } = DateTime.UtcNow;

    public string FormattedBytesCleaned { get => ByteSizeFormatter.Format(BytesCleaned); set { } }
}

/// <summary>
/// Historical record of performed cleanups to facilitate recurrence detection.
/// </summary>
public sealed class CleanHistoryItem
{
    public long Id { get; set; }
    public DateTime CleanedUtc { get; set; } = DateTime.UtcNow;
    public string ProviderId { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public long BytesCleaned { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}
