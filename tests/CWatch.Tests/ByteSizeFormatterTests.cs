using System.Globalization;
using CWatch.Core.Models;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Culture-aware formatting per issue #4. The null-culture contract is
/// byte-for-byte identical to the historical invariant output; cultures only
/// change the decimal separator of scaled values, never byte-count grouping.
/// </summary>
public class ByteSizeFormatterTests
{
    [Fact]
    public void Format_NullCulture_MatchesInvariantDefault()
    {
        Assert.Equal("0 B", ByteSizeFormatter.Format(0, 1, null));
        Assert.Equal("500 B", ByteSizeFormatter.Format(500, 1, null));
        Assert.Equal("1.5 KB", ByteSizeFormatter.Format(1536, 1, null));
        Assert.Equal("1.0 MB", ByteSizeFormatter.Format(1024 * 1024, 1, null));
    }

    [Fact]
    public void Format_DefaultParameter_MatchesHistoricalOutput()
    {
        // The two-parameter call sites (the vast majority) must be untouched.
        Assert.Equal("1.5 KB", ByteSizeFormatter.Format(1536));
        Assert.Equal("3.2 GB", ByteSizeFormatter.Format(3_435_973_836_8L / 10, 1));
    }

    [Fact]
    public void Format_GermanCulture_UsesCommaSeparator()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("1,5 KB", ByteSizeFormatter.Format(1536, 1, de));
        Assert.Equal("1,0 MB", ByteSizeFormatter.Format(1024 * 1024, 1, de));
    }

    [Fact]
    public void Format_ByteCounts_GroupInvariant_InAllCultures()
    {
        // Byte counts occupy 1..1023 — a range no locale group-separates — but they
        // are still formatted invariantly so output never depends on the host's
        // CurrentCulture. The proposal routed them through the passed culture;
        // 1023 B is the boundary case just before the KB branch takes over.
        var de = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("1023 B", ByteSizeFormatter.Format(1023, 1, de));
        Assert.Equal("1023 B", ByteSizeFormatter.Format(1023, 1, CultureInfo.InvariantCulture));
        Assert.Equal("1 B", ByteSizeFormatter.Format(1, 1, de));
        Assert.Equal("0 B", ByteSizeFormatter.Format(0, 1, de));

        // The boundary: 1024 crosses into KB and the culture governs the separator.
        Assert.Equal("1,0 KB", ByteSizeFormatter.Format(1024, 1, de));
        Assert.Equal("1.0 KB", ByteSizeFormatter.Format(1024, 1, CultureInfo.InvariantCulture));

        // 1500 bytes is 1.5 KB, not a byte count.
        Assert.Equal("1,5 KB", ByteSizeFormatter.Format(1500, 1, de));
    }

    [Fact]
    public void Format_NegativeAndZeroBytes_WorksInBothModes()
    {
        Assert.Equal("0 B", ByteSizeFormatter.Format(0, 1, CultureInfo.InvariantCulture));
        Assert.Equal("0 B", ByteSizeFormatter.Format(0, 1, CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal("-1.5 KB", ByteSizeFormatter.Format(-1536, 1, CultureInfo.InvariantCulture));
        Assert.Equal("-1,5 KB", ByteSizeFormatter.Format(-1536, 1, CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public void FormatDelta_PassesCultureThrough()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("No change", ByteSizeFormatter.FormatDelta(0));
        Assert.Equal("+1,5 KB", ByteSizeFormatter.FormatDelta(1536, de));
        Assert.Equal("-1,5 KB", ByteSizeFormatter.FormatDelta(-1536, de));
        Assert.Equal("+1.5 KB", ByteSizeFormatter.FormatDelta(1536, CultureInfo.InvariantCulture));
    }
}
