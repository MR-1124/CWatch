using CWatch.Analysis.Classifiers;
using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.Analysis.Recurring;

public sealed class RecurringGrowthDetector : IRecurringGrowthDetector
{
    private readonly ILoggerService? _logger;

    public RecurringGrowthDetector(ILoggerService? logger = null)
    {
        _logger = logger;
    }

    public async Task<List<RecurringGrowthAlert>> DetectRecurringGrowthAsync(
        IReadOnlyList<StorageSnapshot> snapshots,
        IReadOnlyList<CleanHistoryItem> cleanHistory,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var alerts = new List<RecurringGrowthAlert>();

            if (snapshots.Count < 2 && cleanHistory.Count == 0)
            {
                // Return empty if not enough history
                return alerts;
            }

            try
            {
                // 1. Correlate Clean History with subsequent snapshots
                var cleanGroups = cleanHistory
                    .GroupBy(c => c.TargetPath, StringComparer.OrdinalIgnoreCase);

                foreach (var group in cleanGroups)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string targetPath = group.Key;
                    var sortedCleans = group.OrderByDescending(c => c.CleanedUtc).ToList();
                    var lastClean = sortedCleans.First();
                    int timesCleaned = sortedCleans.Count;

                    // Check if current directory exists and has grown back
                    if (Directory.Exists(targetPath))
                    {
                        long currentSize = GetDirectorySizeSafe(targetPath);
                        TimeSpan timeSinceClean = DateTime.UtcNow - lastClean.CleanedUtc;

                        // If it regrew > 500MB within 14 days
                        if (currentSize > 500 * 1024 * 1024 && timeSinceClean.TotalDays <= 14)
                        {
                            double days = Math.Max(0.5, timeSinceClean.TotalDays);
                            double dailyRate = (double)currentSize / days;

                            alerts.Add(new RecurringGrowthAlert
                            {
                                Path = targetPath,
                                DisplayName = Path.GetFileName(targetPath),
                                Category = CategoryClassifier.Classify(targetPath, true),
                                CurrentSizeBytes = currentSize,
                                DailyGrowthRateBytes = dailyRate,
                                CleanedCount = timesCleaned,
                                LastCleanedUtc = lastClean.CleanedUtc,
                                RegrownBytesSinceClean = currentSize,
                                Reason = GuessRecurringReason(targetPath),
                                Consequence = "Cleaning will recover space temporarily, but the application will likely recreate cached assets."
                            });
                        }
                    }
                }

                // 2. Also check common high-growth recurring cache paths on machine even without prior in-app clean
                var knownRecurringPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gradle", "caches"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pip", "cache"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "Cache"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data", "Default", "Cache"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spotify", "Data"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord", "Cache")
                };

                foreach (var path in knownRecurringPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (alerts.Any(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (Directory.Exists(path))
                    {
                        long size = GetDirectorySizeSafe(path);
                        if (size > 1L * 1024 * 1024 * 1024) // > 1 GB
                        {
                            alerts.Add(new RecurringGrowthAlert
                            {
                                Path = path,
                                DisplayName = Path.GetFileName(path) == "Cache" || Path.GetFileName(path) == "Data"
                                    ? $"{Directory.GetParent(path)?.Parent?.Name ?? "App"} Cache"
                                    : Path.GetFileName(path),
                                Category = CategoryClassifier.Classify(path, true),
                                CurrentSizeBytes = size,
                                DailyGrowthRateBytes = size / 7.0, // Estimated 7 day accumulation
                                CleanedCount = 0,
                                LastCleanedUtc = null,
                                RegrownBytesSinceClean = size,
                                Reason = GuessRecurringReason(path),
                                Consequence = "Application cache / build artifacts will automatically accumulate again during use."
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error detecting recurring storage growth.", ex);
            }

            return alerts.OrderByDescending(a => a.CurrentSizeBytes).ToList();
        }, cancellationToken);
    }

    private static long GetDirectorySizeSafe(string path)
    {
        try
        {
            var di = new DirectoryInfo(path);
            var opt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            return di.EnumerateFiles("*", opt).Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }

    private static string GuessRecurringReason(string path)
    {
        string norm = path.ToLowerInvariant();
        if (norm.Contains("gradle") || norm.Contains("nuget") || norm.Contains("npm") || norm.Contains("pip"))
        {
            return "Package manager and build tool cache. Accumulates downloaded dependencies over time.";
        }
        if (norm.Contains("chrome") || norm.Contains("edge") || norm.Contains("firefox"))
        {
            return "Web browser media and asset cache. Recreated constantly during web browsing.";
        }
        if (norm.Contains("spotify") || norm.Contains("discord") || norm.Contains("teams") || norm.Contains("slack"))
        {
            return "Streaming and communication app cache (cached media, avatars, audio chunks).";
        }
        if (norm.Contains("temp"))
        {
            return "Operating system and application temporary working files.";
        }
        return "Application cache or generated working data.";
    }
}
