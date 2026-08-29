using System.Text;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.Analysis.Reports;

public sealed class StorageReportGenerator : IStorageReportGenerator
{
    private readonly IGrowthAnalyzer _growthAnalyzer;
    private readonly IRecurringGrowthDetector _recurringDetector;

    public StorageReportGenerator(
        IGrowthAnalyzer growthAnalyzer,
        IRecurringGrowthDetector recurringDetector)
    {
        _growthAnalyzer = growthAnalyzer;
        _recurringDetector = recurringDetector;
    }

    public async Task<StorageReport> GenerateReportAsync(
        DriveStatus driveStatus,
        StorageItem rootItem,
        IReadOnlyList<StorageSnapshot> history,
        IReadOnlyList<CleanupCandidate> recommendations,
        CancellationToken cancellationToken = default)
    {
        var report = new StorageReport
        {
            DriveStatus = driveStatus,
            RecommendedCleanups = recommendations.ToList(),
            TotalRecommendedCleanupBytes = recommendations.Where(r => r.IsSelected).Sum(r => r.SizeBytes)
        };

        // Extract top growth deltas if history exists
        if (history.Count >= 2)
        {
            var latest = history.Last();
            var older = history.First();
            report.RecentGrowthDeltas = await _growthAnalyzer.CompareSnapshotsAsync(older, latest, cancellationToken);
        }

        // Recurring growth alerts
        report.RecurringGrowthAlerts = await _recurringDetector.DetectRecurringGrowthAsync(history, [], cancellationToken);

        // Build Summary Markdown
        var sb = new StringBuilder();
        sb.AppendLine($"# C:Watch Storage Intelligence Diagnostics Report");
        sb.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss} | **Target:** {driveStatus.DriveLetter} ({driveStatus.VolumeLabel})");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("## 1. Drive Capacity Overview");
        sb.AppendLine($"- **Total Capacity:** {ByteSizeFormatter.Format(driveStatus.TotalBytes)}");
        sb.AppendLine($"- **Used Space:** {ByteSizeFormatter.Format(driveStatus.UsedBytes)} ({driveStatus.UsedPercentage:F1}%)");
        sb.AppendLine($"- **Free Space:** {ByteSizeFormatter.Format(driveStatus.FreeBytes)} ({driveStatus.FreePercentage:F1}%)");
        sb.AppendLine($"- **File System:** {driveStatus.FileSystem}");
        sb.AppendLine();

        if (driveStatus.IsCriticallyLow())
        {
            sb.AppendLine("> ⚠️ **CRITICAL STORAGE WARNING:** Free space is critically low! Immediate cleanup recommended.");
            sb.AppendLine();
        }

        sb.AppendLine("## 2. Storage Category Breakdown");
        sb.AppendLine("| Category | Size | % of Total | Items |");
        sb.AppendLine("| :--- | :--- | :--- | :--- |");
        foreach (var cat in rootItem.Children.OrderByDescending(c => c.SizeBytes).Take(8))
        {
            sb.AppendLine($"| {cat.Name} | {ByteSizeFormatter.Format(cat.SizeBytes)} | {cat.RelativePercentage:F1}% | {cat.FileCount:N0} files |");
        }
        sb.AppendLine();

        if (report.RecentGrowthDeltas.Count > 0)
        {
            sb.AppendLine("## 3. Notable Storage Growth");
            foreach (var delta in report.RecentGrowthDeltas.Take(5))
            {
                sb.AppendLine($"- **{delta.DisplayName}:** {delta.FormattedDelta} ({delta.FormattedCurrentSize})");
            }
            sb.AppendLine();
        }

        if (report.RecurringGrowthAlerts.Count > 0)
        {
            sb.AppendLine("## 4. Recurring Storage Growth Detected");
            foreach (var rec in report.RecurringGrowthAlerts.Take(4))
            {
                sb.AppendLine($"- **{rec.DisplayName}** ({rec.FormattedCurrentSize}, {rec.FormattedDailyRate}): {rec.Reason}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 5. Safe Cleanup Recommendations");
        sb.AppendLine($"Total potentially reclaimable: **{report.FormattedRecommendedCleanup}**");
        sb.AppendLine();
        foreach (var rec in report.RecommendedCleanups.Where(r => r.CanClean).Take(6))
        {
            sb.AppendLine($"- **[{rec.SafetyBadgeText}] {rec.Title} ({rec.FormattedSize}):** {rec.Reason}");
        }

        report.SummaryText = sb.ToString();
        report.KeyTakeaway = driveStatus.IsCriticallyLow()
            ? $"Critical: Only {ByteSizeFormatter.Format(driveStatus.FreeBytes)} remaining. Recover up to {report.FormattedRecommendedCleanup} safely."
            : $"Drive health is normal ({ByteSizeFormatter.Format(driveStatus.FreeBytes)} free). {report.FormattedRecommendedCleanup} available for safe cleanup.";

        return report;
    }
}
