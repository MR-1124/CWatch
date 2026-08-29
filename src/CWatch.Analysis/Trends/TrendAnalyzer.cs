using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.Analysis.Trends;

public sealed class TrendAnalyzer : ITrendAnalyzer
{
    public (double dailyGrowthBytes, TimeSpan? estimatedDaysToFull) CalculateExhaustionTrend(
        IReadOnlyList<StorageSnapshot> snapshots,
        long currentFreeBytes)
    {
        if (snapshots.Count < 2)
        {
            return (0.0, null);
        }

        var ordered = snapshots.OrderBy(s => s.TimestampUtc).ToList();
        var oldest = ordered.First();
        var newest = ordered.Last();

        double totalDays = (newest.TimestampUtc - oldest.TimestampUtc).TotalDays;
        if (totalDays < 0.1)
        {
            return (0.0, null);
        }

        // Net change in used bytes
        long deltaUsed = newest.UsedBytes - oldest.UsedBytes;
        double dailyGrowthRate = (double)deltaUsed / totalDays;

        if (dailyGrowthRate <= 0 || currentFreeBytes <= 0)
        {
            return (dailyGrowthRate, null);
        }

        double daysRemaining = (double)currentFreeBytes / dailyGrowthRate;
        if (daysRemaining > 3650) // More than 10 years
        {
            return (dailyGrowthRate, null);
        }

        return (dailyGrowthRate, TimeSpan.FromDays(daysRemaining));
    }
}
