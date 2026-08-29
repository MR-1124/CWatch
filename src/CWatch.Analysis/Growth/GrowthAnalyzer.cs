using System.Text.Json;
using CWatch.Analysis.Classifiers;
using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.Analysis.Growth;

public sealed class GrowthAnalyzer : IGrowthAnalyzer
{
    private readonly ISnapshotRepository _snapshotRepo;
    private readonly ILoggerService? _logger;

    public GrowthAnalyzer(ISnapshotRepository snapshotRepo, ILoggerService? logger = null)
    {
        _snapshotRepo = snapshotRepo;
        _logger = logger;
    }

    public async Task<List<GrowthDelta>> CompareSnapshotsAsync(
        StorageSnapshot olderSnapshot,
        StorageSnapshot newerSnapshot,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var deltas = new List<GrowthDelta>();

            try
            {
                var oldCategories = DeserializeCategories(olderSnapshot.CategoriesJson);
                var newCategories = DeserializeCategories(newerSnapshot.CategoriesJson);

                var oldMap = oldCategories.ToDictionary(c => c.CategoryType, c => c.SizeBytes);
                var newMap = newCategories.ToDictionary(c => c.CategoryType, c => c.SizeBytes);

                var allCategories = oldMap.Keys.Union(newMap.Keys).Distinct();

                foreach (var cat in allCategories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long oldSize = oldMap.GetValueOrDefault(cat, 0);
                    long newSize = newMap.GetValueOrDefault(cat, 0);
                    long diff = newSize - oldSize;

                    if (Math.Abs(diff) > 10 * 1024 * 1024) // Only consider changes > 10MB
                    {
                        deltas.Add(new GrowthDelta
                        {
                            Path = CategoryClassifier.GetCategoryDisplayName(cat),
                            DisplayName = CategoryClassifier.GetCategoryDisplayName(cat),
                            Category = cat,
                            PreviousSizeBytes = oldSize,
                            CurrentSizeBytes = newSize,
                            PreviousTimestampUtc = olderSnapshot.TimestampUtc,
                            CurrentTimestampUtc = newerSnapshot.TimestampUtc,
                            IsNew = oldSize == 0
                        });
                    }
                }

                // Also compare top items if present
                var oldTop = DeserializeTopItems(olderSnapshot.TopItemsJson);
                var newTop = DeserializeTopItems(newerSnapshot.TopItemsJson);

                var oldItemMap = oldTop.ToDictionary(i => i.FullPath, i => i.SizeBytes, StringComparer.OrdinalIgnoreCase);
                var newItemMap = newTop.ToDictionary(i => i.FullPath, i => i.SizeBytes, StringComparer.OrdinalIgnoreCase);

                foreach (var (path, newSize) in newItemMap)
                {
                    long oldSize = oldItemMap.GetValueOrDefault(path, 0);
                    long diff = newSize - oldSize;
                    if (diff > 50 * 1024 * 1024) // Top item growth > 50MB
                    {
                        deltas.Add(new GrowthDelta
                        {
                            Path = path,
                            DisplayName = Path.GetFileName(path),
                            Category = CategoryClassifier.Classify(path, true),
                            PreviousSizeBytes = oldSize,
                            CurrentSizeBytes = newSize,
                            PreviousTimestampUtc = olderSnapshot.TimestampUtc,
                            CurrentTimestampUtc = newerSnapshot.TimestampUtc,
                            IsNew = oldSize == 0
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error comparing snapshots.", ex);
            }

            // Order by largest positive growth first
            deltas.Sort((a, b) => b.DeltaBytes.CompareTo(a.DeltaBytes));
            return deltas;
        }, cancellationToken);
    }

    public async Task<List<GrowthDelta>> AnalyzeGrowthSinceAsync(
        TimeSpan timeSpan,
        StorageSnapshot currentSnapshot,
        CancellationToken cancellationToken = default)
    {
        DateTime targetTime = currentSnapshot.TimestampUtc - timeSpan;
        var snapshots = await _snapshotRepo.GetSnapshotsAsync(
            currentSnapshot.DriveLetter,
            targetTime.AddHours(-12),
            targetTime.AddHours(12));

        var olderSnapshot = snapshots.MinBy(s => Math.Abs((s.TimestampUtc - targetTime).TotalSeconds));
        if (olderSnapshot == null)
        {
            // Try getting the earliest snapshot available
            var all = await _snapshotRepo.GetAllSnapshotsAsync(currentSnapshot.DriveLetter, 50);
            olderSnapshot = all.FirstOrDefault(s => s.TimestampUtc < currentSnapshot.TimestampUtc);
        }

        if (olderSnapshot == null || olderSnapshot.Id == currentSnapshot.Id)
        {
            return [];
        }

        return await CompareSnapshotsAsync(olderSnapshot, currentSnapshot, cancellationToken);
    }

    private static List<CategoryBreakdown> DeserializeCategories(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<CategoryBreakdown>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<StorageItem> DeserializeTopItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<StorageItem>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
