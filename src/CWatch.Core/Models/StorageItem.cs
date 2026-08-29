using CWatch.Core.Enums;

namespace CWatch.Core.Models;

/// <summary>
/// Hierarchical node representing a file or directory on disk with classified storage metadata.
/// </summary>
public sealed class StorageItem
{
    public string FullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsDirectory { get; set; }
    public StorageCategoryType Category { get; set; } = StorageCategoryType.Other;
    public string? SubCategory { get; set; }
    public DateTime? LastModifiedUtc { get; set; }
    public string? Extension { get; set; }
    public long FileCount { get; set; }
    public long DirectoryCount { get; set; }
    public bool IsInaccessible { get; set; }
    public string? ParentPath { get; set; }
    public double RelativePercentage { get; set; }
    public List<StorageItem> Children { get; set; } = [];

    public string DisplaySize { get => ByteSizeFormatter.Format(SizeBytes); set { } }

    public override string ToString() => $"{Name} ({DisplaySize}) - {Category}";
}
