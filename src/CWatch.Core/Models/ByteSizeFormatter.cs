namespace CWatch.Core.Models;

/// <summary>
/// High performance, human readable byte size formatting and parsing.
/// </summary>
public static class ByteSizeFormatter
{
    private static readonly string[] Suffixes = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes, int decimalPlaces = 1)
    {
        if (bytes < 0)
        {
            return $"-{Format(-bytes, decimalPlaces)}";
        }

        if (bytes == 0)
        {
            return "0 B";
        }

        int mag = (int)Math.Max(0, Math.Min(Suffixes.Length - 1, Math.Floor(Math.Log(bytes, 1024))));
        double adjustedSize = (double)bytes / (1L << (mag * 10));

        if (mag == 0)
        {
            return $"{bytes} B";
        }

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, $"{{0:F{decimalPlaces}}} {{1}}", adjustedSize, Suffixes[mag]);
    }

    public static string FormatDelta(long deltaBytes)
    {
        if (deltaBytes == 0) return "No change";
        if (deltaBytes > 0) return $"+{Format(deltaBytes)}";
        return $"-{Format(-deltaBytes)}";
    }

    public static double ToGigabytes(long bytes) => (double)bytes / (1024 * 1024 * 1024);
    public static double ToMegabytes(long bytes) => (double)bytes / (1024 * 1024);
    public static long FromGigabytes(double gb) => (long)(gb * 1024 * 1024 * 1024);
}
