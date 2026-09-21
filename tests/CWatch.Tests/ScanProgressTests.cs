using CWatch.Analysis.Scanning;
using CWatch.Core.Models;
using CWatch.Core.Safety;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Scanner progress reporting: EstimatedPercent grows monotonically, stays indeterminate
/// while it is 0, never reads 100 before the walk ends, and reads 100 when it does.
/// </summary>
public class ScanProgressTests : IDisposable
{
    private readonly string _root;

    public ScanProgressTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cwatch_progress_{Guid.NewGuid():N}");
        for (int i = 0; i < 12; i++)
        {
            string dir = Path.Combine(_root, $"dir{i:D2}");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "file.bin"), new byte[512]);
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task ScanDirectory_FinalReport_Is100PercentAndDeterminate()
    {
        var progress = new RecordingProgress();
        await new FileSystemScanner().ScanDirectoryAsync(_root, progress);

        var last = progress.Reports[^1];
        Assert.Equal(100.0, last.EstimatedPercent);
        Assert.False(last.IsIndeterminate);
        Assert.Equal("Analysis complete", last.CurrentPhase);
    }

    [Fact]
    public async Task ScanDirectory_EmptyRoot_StillEndsAt100Percent()
    {
        string empty = Path.Combine(_root, "empty_root");
        Directory.CreateDirectory(empty);

        var progress = new RecordingProgress();
        await new FileSystemScanner().ScanDirectoryAsync(empty, progress);

        Assert.Equal(100.0, progress.Reports[^1].EstimatedPercent);
    }

    [Fact]
    public async Task ScanDirectory_IntermediateReports_AreMonotonicSnapshotsBelow100()
    {
        // The scanner throttles reports to one per ~120 ms; a slow exclusion matcher
        // stretches the walk so several intermediate reports are emitted.
        var scanner = new FileSystemScanner(exclusionMatcher: new SlowIncludeAllMatcher(TimeSpan.FromMilliseconds(40)));
        var progress = new RecordingProgress();
        await scanner.ScanDirectoryAsync(_root, progress);

        var reports = progress.Reports;
        var intermediate = reports.Take(reports.Count - 1).ToList();
        Assert.True(intermediate.Count >= 2, $"expected several intermediate reports, got {intermediate.Count}");

        // Every report is a distinct snapshot, so WPF bindings see a new object each time.
        Assert.Equal(reports.Count, reports.Distinct(ReferenceEqualityComparer.Instance).Count());

        for (int i = 1; i < reports.Count; i++)
        {
            Assert.True(reports[i].EstimatedPercent >= reports[i - 1].EstimatedPercent, "progress went backwards");
        }

        Assert.All(intermediate, r =>
        {
            Assert.InRange(r.EstimatedPercent, 0.0, 99.0);
            Assert.Equal(r.EstimatedPercent <= 0, r.IsIndeterminate);
        });
        Assert.Contains(intermediate, r => r.EstimatedPercent > 0);
        Assert.Equal(100.0, reports[^1].EstimatedPercent);
    }

    /// <summary>Synchronous IProgress so reports are recorded in emission order.</summary>
    private sealed class RecordingProgress : IProgress<ScanProgressInfo>
    {
        public List<ScanProgressInfo> Reports { get; } = [];
        public void Report(ScanProgressInfo value) => Reports.Add(value);
    }

    private sealed class SlowIncludeAllMatcher(TimeSpan delay) : IPathExclusionMatcher
    {
        public PathExclusionDecision Evaluate(string path, bool checkAncestors = false)
        {
            Thread.Sleep(delay);
            return PathExclusionDecision.Included;
        }
    }
}
