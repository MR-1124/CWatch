using System.Globalization;

namespace CWatch.Core.Models;

/// <summary>
/// High performance, human readable byte size formatting.
/// </summary>
public static class ByteSizeFormatter
{
    private static readonly string[] Suffixes = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Formats a byte count for display.
    ///
    /// Culture handling: passing <c>null</c> (the default) keeps the historical
    /// invariant output byte-for-byte — serialized snapshots and existing tests
    /// depend on it. Passing a culture routes the decimal separator (and any
    /// group separators) through it; unit suffixes stay standard in all
    /// cultures, and whole byte counts are never group-separated.
    /// </summary>
    public static string Format(long bytes, int decimalPlaces = 1, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.InvariantCulture;

        if (bytes < 0)
        {
            return $"-{Format(-bytes, decimalPlaces, culture)}";
        }

        if (bytes == 0)
        {
            return $"0 {Suffixes[0]}";
        }

        int mag = (int)Math.Max(0, Math.Min(Suffixes.Length - 1, Math.Floor(Math.Log(bytes, 1024))));
        double adjustedSize = (double)bytes / (1L << (mag * 10));

        if (mag == 0)
        {
            // Whole byte counts: invariant formatting with no grouping — "1023 B"
            // stays "1023 B" in every locale. Interpolation would silently use
            // CurrentCulture and render "1,023 B" on grouping locales.
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}", bytes, Suffixes[0]);
        }

        return string.Format(culture, $"{{0:F{decimalPlaces}}} {{1}}", adjustedSize, Suffixes[mag]);
    }

    public static string FormatDelta(long deltaBytes, CultureInfo? culture = null)
    {
        if (deltaBytes == 0) return "No change";
        if (deltaBytes > 0) return $"+{Format(deltaBytes, 1, culture)}";
        return $"-{Format(-deltaBytes, 1, culture)}";
    }

    public static double ToGigabytes(long bytes) => (double)bytes / (1024 * 1024 * 1024);
    public static double ToMegabytes(long bytes) => (double)bytes / (1024 * 1024);
    public static long FromGigabytes(double gb) => (long)(gb * 1024 * 1024 * 1024);
}
