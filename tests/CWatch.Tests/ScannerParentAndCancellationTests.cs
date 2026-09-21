using CWatch.Analysis.Scanning;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Scanner tests for the Parent back-reference (O(1) breadcrumb navigation) and
/// cancellation behavior of the long-running duplicate scan.
/// </summary>
public class ScannerParentAndCancellationTests : IDisposable
{
    private readonly string _root;

    public ScannerParentAndCancellationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cwatch_parent_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "level1", "level2", "level3"));
        File.WriteAllBytes(Path.Combine(_root, "level1", "file1.bin"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(_root, "level1", "level2", "file2.bin"), new byte[2048]);
        File.WriteAllBytes(Path.Combine(_root, "level1", "level2", "level3", "file3.bin"), new byte[4096]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task ScanDirectory_PopulatesParentReferences()
    {
        var scanner = new FileSystemScanner();
        var root = await scanner.ScanDirectoryAsync(_root);

        Assert.Null(root.Parent); // root has no parent

        var level1 = Assert.Single(root.Children, c => c.Name == "level1");
        Assert.Same(root, level1.Parent);

        var level2 = Assert.Single(level1.Children, c => c.Name == "level2");
        Assert.Same(level1, level2.Parent);

        var level3 = Assert.Single(level2.Children, c => c.Name == "level3");
        Assert.Same(level2, level3.Parent);

        var file3 = Assert.Single(level3.Children, c => c.Name == "file3.bin");
        Assert.Same(level3, file3.Parent);
    }

    [Fact]
    public async Task BreadcrumbWalk_ViaParent_MatchesScannedDepth()
    {
        var scanner = new FileSystemScanner();
        var root = await scanner.ScanDirectoryAsync(_root);

        // Sorted largest-first: level3 (4KB) > level1's direct file (1KB).
        var level1 = root.Children.Single(c => c.Name == "level1");
        var level2 = level1.Children.Single(c => c.Name == "level2");
        var level3 = level2.Children.Single(c => c.Name == "level3");

        // Walk up: root <- level1 <- level2 <- level3
        var chain = new List<string>();
        for (var node = level3; node != null; node = node.Parent)
        {
            chain.Add(node.Name);
        }

        Assert.Equal(4, chain.Count);
        Assert.Equal("level3", chain[0]);
        Assert.Equal(root.Name, chain[^1]);
    }

    [Fact]
    public async Task FindDuplicateFiles_RespectsCancellation()
    {
        byte[] payload = new byte[64 * 1024];
        Random.Shared.NextBytes(payload);
        for (int i = 0; i < 8; i++)
        {
            File.WriteAllBytes(Path.Combine(_root, $"same_{i}.bin"), payload);
        }

        var scanner = new FileSystemScanner();
        using var cts = new CancellationTokenSource();
        // Pre-canceled: deterministic. Verifies the token is honored from the
        // first check instead of racing a timer against real hashing work.
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.FindDuplicateFilesAsync(_root, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task FindLargestFiles_RespectsCancellation()
    {
        var scanner = new FileSystemScanner();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-canceled: deterministically verifies the async propagation contract

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.FindLargestFilesAsync(_root, count: 10, cancellationToken: cts.Token));
    }
}
