using System.Text.Json;
using CWatch.Analysis.Classifiers;
using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.Analysis.Storage;

public sealed class StorageAnalyzer : IStorageAnalyzer
{
    private readonly ILoggerService? _logger;

    public StorageAnalyzer(ILoggerService? logger = null)
    {
        _logger = logger;
    }

    public DriveStatus GetDriveStatus(string driveLetter = "C:")
    {
        string normalizedDrive = driveLetter.TrimEnd('\\') + "\\";
        try
        {
            var drive = new DriveInfo(normalizedDrive);
            return new DriveStatus
            {
                DriveLetter = drive.Name.TrimEnd('\\'),
                VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                FileSystem = drive.DriveFormat,
                TotalBytes = drive.TotalSize,
                FreeBytes = drive.TotalFreeSpace,
                IsSystemDrive = string.Equals(drive.Name, Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase),
                LastCheckedUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to query DriveInfo for {driveLetter}", ex);
            return new DriveStatus
            {
                DriveLetter = driveLetter,
                VolumeLabel = "Unknown Drive",
                TotalBytes = 0,
                FreeBytes = 0
            };
        }
    }

    public async Task<List<CategoryBreakdown>> AnalyzeCategoriesAsync(
        StorageItem rootItem,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var categoryMap = new Dictionary<StorageCategoryType, (long size, long count, List<StorageItem> top)>();

            foreach (StorageCategoryType cat in Enum.GetValues<StorageCategoryType>())
            {
                categoryMap[cat] = (0, 0, []);
            }

            void Accumulate(StorageItem item)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // If item has children, traverse them
                if (item.Children.Count > 0)
                {
                    foreach (var child in item.Children)
                    {
                        Accumulate(child);
                    }
                }
                else
                {
                    // Leaf file or empty folder
                    var (size, count, top) = categoryMap[item.Category];
                    size += item.SizeBytes;
                    count++;

                    if (top.Count < 20 || item.SizeBytes > top[^1].SizeBytes)
                    {
                        int idx = top.BinarySearch(item, Comparer<StorageItem>.Create((a, b) => b.SizeBytes.CompareTo(a.SizeBytes)));
                        if (idx < 0) idx = ~idx;
                        top.Insert(idx, item);
                        if (top.Count > 20) top.RemoveAt(top.Count - 1);
                    }

                    categoryMap[item.Category] = (size, count, top);
                }
            }

            Accumulate(rootItem);

            long totalScannedSize = categoryMap.Values.Sum(v => v.size);
            if (totalScannedSize <= 0) totalScannedSize = rootItem.SizeBytes;

            var breakdowns = new List<CategoryBreakdown>();

            foreach (var kvp in categoryMap)
            {
                var (size, count, top) = kvp.Value;
                if (size <= 0 && count <= 0) continue;

                double pct = totalScannedSize > 0 ? (double)size / totalScannedSize * 100.0 : 0.0;
                breakdowns.Add(new CategoryBreakdown
                {
                    CategoryType = kvp.Key,
                    DisplayName = CategoryClassifier.GetCategoryDisplayName(kvp.Key),
                    SizeBytes = size,
                    PercentageOfUsed = pct,
                    ItemCount = count,
                    ColorHex = CategoryClassifier.GetCategoryColorHex(kvp.Key),
                    IconKey = CategoryClassifier.GetCategoryIcon(kvp.Key),
                    Description = GetCategoryDescription(kvp.Key),
                    TopItems = top
                });
            }

            // Order largest to smallest
            breakdowns.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
            return breakdowns;
        }, cancellationToken);
    }

    private static string GetCategoryDescription(StorageCategoryType type) => type switch
    {
        StorageCategoryType.WindowsSystem => "Windows OS files, system drivers, hibernation, and pagefile memory.",
        StorageCategoryType.InstalledApps => "Applications installed in Program Files and system app packages.",
        StorageCategoryType.UserFiles => "User profile documents, media, and personal files.",
        StorageCategoryType.Downloads => "Downloaded installer packages, archives, and files.",
        StorageCategoryType.Documents => "Office documents, PDFs, and text archives.",
        StorageCategoryType.Pictures => "Images, photos, and graphic media.",
        StorageCategoryType.Videos => "Video recordings, movies, and media clips.",
        StorageCategoryType.Desktop => "Items placed on the user desktop.",
        StorageCategoryType.AppData => "Application settings, caches, and local configurations.",
        StorageCategoryType.ProgramData => "Shared application data and cached components across users.",
        StorageCategoryType.TemporaryFiles => "Disposable caches, crash dumps, and Windows temp files.",
        StorageCategoryType.BrowserData => "Web browser caches, profiles, and offline data.",
        StorageCategoryType.DevelopmentTools => "Developer caches (npm, NuGet, Gradle, pip, Docker, SDKs, build artifacts).",
        StorageCategoryType.VirtualMachinesEmulators => "Virtual machine disks, Android emulators, and container storage.",
        StorageCategoryType.RecycleBin => "Deleted files pending permanent removal.",
        _ => "Unclassified files and directories."
    };
}
