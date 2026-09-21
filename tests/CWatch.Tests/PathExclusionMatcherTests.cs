using CWatch.Core.Safety;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Unit tests for the glob-style exclusion matcher that enforces AppSettings.ExcludedPaths.
/// </summary>
public class PathExclusionMatcherTests
{
    [Fact]
    public void ExactDirectoryPattern_ExcludesPath_AndDescendants()
    {
        var matcher = new GlobPathExclusionMatcher([@"D:\BigMedia"]);

        Assert.Equal(PathExclusionDecision.Excluded, matcher.Evaluate(@"D:\BigMedia"));
        Assert.True(matcher.IsExcluded(@"D:\BigMedia\Season 1\ep1.mkv", checkAncestors: true));
        // Without ancestor checking, only the directory itself matches.
        Assert.False(matcher.IsExcluded(@"D:\BigMedia\Season 1\ep1.mkv"));
    }

    [Fact]
    public void WildcardChildrenPattern_ExcludesChildren_ButNotDirectoryItself()
    {
        var matcher = new GlobPathExclusionMatcher([@"D:\BigMedia\*"]);

        Assert.True(matcher.IsExcluded(@"D:\BigMedia\video.mkv"));
        // The named directory itself remains visible in scans.
        Assert.False(matcher.IsExcluded(@"D:\BigMedia"));
    }

    [Fact]
    public void ExtensionPattern_ExcludesMatchingFilesAnywhere()
    {
        var matcher = new GlobPathExclusionMatcher(["*.log"]);

        Assert.True(matcher.IsExcluded(@"C:\Users\me\app\trace.log"));
        Assert.False(matcher.IsExcluded(@"C:\Users\me\app\trace.txt"));
        // Directories are not matched by bare extension patterns.
        Assert.False(matcher.IsExcluded(@"C:\Users\me\logs"));
    }

    [Fact]
    public void MatchingIsCaseInsensitive()
    {
        var matcher = new GlobPathExclusionMatcher([@"D:\BigMedia"]);

        Assert.True(matcher.IsExcluded(@"D:\BIGMEDIA\clip.mov", checkAncestors: true));
        Assert.True(matcher.IsExcluded(@"d:\bigmedia"));
    }

    [Fact]
    public void DriveRoots_AndTraversalPatterns_AreIgnored()
    {
        var matcher = new GlobPathExclusionMatcher(["C:\\", "D:", @"..\..\Windows", "", "   "]);

        // Nothing may exclude a whole drive or escape the root.
        Assert.False(matcher.IsExcluded(@"C:\Users\me\AppData\Local\Temp"));
        Assert.False(matcher.IsExcluded(@"D:\Anything"));
        Assert.False(matcher.IsExcluded(@"C:\Windows\System32\config"));
    }

    [Fact]
    public void EmptyPatternList_ExcludesNothing()
    {
        var matcher = new GlobPathExclusionMatcher([]);

        Assert.False(matcher.IsExcluded(@"C:\Anything\At\All"));
    }

    [Fact]
    public void InvalidPaths_AreTreatedAsIncluded()
    {
        var matcher = new GlobPathExclusionMatcher([@"D:\BigMedia"]);

        Assert.False(matcher.IsExcluded(""));
        Assert.False(matcher.IsExcluded("   "));
    }

    [Fact]
    public void QuotedAndCasedSettingsEntries_AreNormalized()
    {
        var matcher = new GlobPathExclusionMatcher([" \"D:\\Big Media\\Cache\"  "]);

        Assert.True(matcher.IsExcluded(@"D:\Big Media\Cache\entry.bin", checkAncestors: true));
        Assert.True(matcher.IsExcluded(@"D:\BIG MEDIA\CACHE\entry.bin", checkAncestors: true));
    }
}
