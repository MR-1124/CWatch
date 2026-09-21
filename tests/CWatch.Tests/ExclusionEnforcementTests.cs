using System.Text.Json;
using CWatch.Analysis.Scanning;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Core.Safety;
using CWatch.Cleanup.Engine;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Proves that excluded directories/files are skipped by every scan mode and that
/// the cleanup engine never proposes (or executes against) excluded targets.
/// </summary>
public class ExclusionEnforcementTests : IDisposable
{
    private readonly string _root;
    private readonly string _mediaDir;      // will be excluded wholesale
    private readonly string _logsDir;       // *.log excluded by extension
    private readonly string _keepDir;       // stays included

    public ExclusionEnforcementTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cwatch_excl_{Guid.NewGuid():N}");
        _mediaDir = Path.Combine(_root, "BigMedia");
        _logsDir = Path.Combine(_root, "logs");
        _keepDir = Path.Combine(_root, "keep");

        Directory.CreateDirectory(_mediaDir);
        Directory.CreateDirectory(_logsDir);
        Directory.CreateDirectory(_keepDir);

        // BigMedia: 2 MB file (should vanish from all scans)
        File.WriteAllBytes(Path.Combine(_mediaDir, "movie.mkv"), new byte[2 * 1024 * 1024]);

        // logs: log file excluded by pattern + a keeper
        File.WriteAllBytes(Path.Combine(_logsDir, "trace.log"), new byte[512 * 1024]);
        File.WriteAllBytes(Path.Combine(_logsDir, "readme.txt"), new byte[256 * 1024]);

        // keep: control file that must always appear
        File.WriteAllBytes(Path.Combine(_keepDir, "data.bin"), new byte[1024 * 1024]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private IPathExclusionMatcher CreateMatcher()
        => new GlobPathExclusionMatcher([_mediaDir, "*.log"]);

    /// <summary>Recursive modes index files deep in the tree, so ancestor exclusion applies.</summary>
    private bool ExcludedForRecursiveScan(string fullPath)
        => CreateMatcher().IsExcluded(fullPath, checkAncestors: true);

    [Fact]
    public async Task ScanDirectory_SkipsExcludedDirectoriesAndFiles()
    {
        var scanner = new FileSystemScanner(exclusionMatcher: CreateMatcher());
        var matcher = CreateMatcher();

        var root = await scanner.ScanDirectoryAsync(_root);

        // Excluded directory must not appear anywhere in the tree.
        Assert.DoesNotContain(root.Children, c => c.Name == "BigMedia");

        // Extension-excluded file is skipped, sibling file is kept.
        var logsNode = Assert.Single(root.Children, c => c.Name == "logs");
        Assert.DoesNotContain(logsNode.Children, c => matcher.IsExcluded(c.FullPath!));
        Assert.Contains(logsNode.Children, c => c.Name == "readme.txt");

        // Included control survives with correct size aggregation.
        var keepNode = Assert.Single(root.Children, c => c.Name == "keep");
        Assert.Equal(1024 * 1024, keepNode.SizeBytes);
    }

    [Fact]
    public async Task ScanDirectory_WithoutMatcher_IncludesEverything()
    {
        var scanner = new FileSystemScanner();

        var root = await scanner.ScanDirectoryAsync(_root);

        Assert.Contains(root.Children, c => c.Name == "BigMedia");
        var logsNode = Assert.Single(root.Children, c => c.Name == "logs");
        Assert.Contains(logsNode.Children, c => c.Name == "trace.log");
    }

    [Fact]
    public async Task FindLargestFiles_SkipsExcludedFiles()
    {
        var scanner = new FileSystemScanner(exclusionMatcher: CreateMatcher());

        var files = await scanner.FindLargestFilesAsync(_root, count: 100);

        // Excluded by ancestor (BigMedia) and by extension (*.log) must be absent.
        Assert.DoesNotContain(files, f => f.Name == "movie.mkv");
        Assert.DoesNotContain(files, f => f.Name == "trace.log");
        Assert.DoesNotContain(files, f => ExcludedForRecursiveScan(f.FullPath!));

        // Included control file must survive.
        Assert.Contains(files, f => f.Name == "data.bin");
    }

    [Fact]
    public async Task FindDuplicateFiles_SkipsExcludedFiles()
    {
        // Two identical 200KB files: one in keep/, one in the excluded BigMedia dir.
        byte[] payload = new byte[200 * 1024];
        Random.Shared.NextBytes(payload);
        File.WriteAllBytes(Path.Combine(_keepDir, "dup_a.bin"), payload);
        File.WriteAllBytes(Path.Combine(_mediaDir, "dup_b.bin"), payload);

        var scanner = new FileSystemScanner(exclusionMatcher: CreateMatcher());

        var groups = await scanner.FindDuplicateFilesAsync(_root);

        // The excluded copy is filtered during enumeration, so dup_a loses its twin and
        // no group can be reported.
        Assert.All(groups.SelectMany(g => g), item =>
            Assert.False(ExcludedForRecursiveScan(item.FullPath!)));
        Assert.Empty(groups);
    }

    [Fact]
    public async Task CleanupEngine_NeverProposesExcludedPaths()
    {
        // A provider whose candidate lives inside the excluded BigMedia directory.
        var engine = new CleanupEngine(
            exclusionMatcher: CreateMatcher(),
            providers: [new StubProvider(Path.Combine(_mediaDir, "cache"), sizeBytes: 50 * 1024 * 1024)]);

        var candidates = await engine.ScanAllRecommendationsAsync();

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task CleanupEngine_KeepsNonExcludedPaths()
    {
        var engine = new CleanupEngine(
            exclusionMatcher: CreateMatcher(),
            providers: [new StubProvider(Path.Combine(_keepDir, "appcache"), sizeBytes: 50 * 1024 * 1024)]);

        var candidates = await engine.ScanAllRecommendationsAsync();

        Assert.Single(candidates);
        Assert.Equal(Path.Combine(_keepDir, "appcache"), candidates[0].Path);
    }

    [Fact]
    public async Task CleanupEngine_Execute_BlocksExcludedTargets_AsSecondLineOfDefense()
    {
        string excludedTarget = Path.Combine(_mediaDir, "cache");
        var engine = new CleanupEngine(
            exclusionMatcher: CreateMatcher(),
            providers: [new StubProvider(excludedTarget, sizeBytes: 50 * 1024 * 1024)]);

        // Bypass scan filtering and force the candidate into execution directly.
        var result = await engine.ExecuteCleanupAsync(
            [new CleanupCandidate { ProviderId = "stub", Path = excludedTarget, SizeBytes = 1234 }]);

        Assert.Equal(0, result.BytesCleaned);
        Assert.Equal(1, result.FailedItemsCount);
        Assert.Contains(result.ErrorMessages, m => m.Contains("excluded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CleanupEngine_Execute_StillRunsForIncludedTargets()
    {
        string includedTarget = Path.Combine(_keepDir, "appcache");
        Directory.CreateDirectory(includedTarget);
        File.WriteAllBytes(Path.Combine(includedTarget, "junk.tmp"), new byte[4096]);

        var engine = new CleanupEngine(
            exclusionMatcher: CreateMatcher(),
            providers: [new StubProvider(includedTarget, sizeBytes: 4096)]);

        var result = await engine.ExecuteCleanupAsync(
            [new CleanupCandidate { ProviderId = "stub", Path = includedTarget, SizeBytes = 4096 }]);

        Assert.Equal(4096, result.BytesCleaned);
        Assert.True(File.Exists(Path.Combine(includedTarget, "junk.tmp")) == false || !Directory.GetFiles(includedTarget).Any());
    }

    /// <summary>Minimal provider that reports a fixed candidate and deletes its directory contents.</summary>
    private sealed class StubProvider : ICleanupProvider
    {
        private readonly string _path;
        private readonly long _sizeBytes;

        public StubProvider(string path, long sizeBytes)
        {
            _path = path;
            _sizeBytes = sizeBytes;
        }

        public string ProviderId => "stub";
        public string DisplayName => "Stub Provider";
        public string CategoryName => "Other";
        public bool IsAdvanced => false;

        public Task<List<CleanupCandidate>> ScanCandidatesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<CleanupCandidate>
            {
                new()
                {
                    ProviderId = ProviderId,
                    Title = "Stub candidate",
                    Description = "Stub",
                    Path = _path,
                    SizeBytes = _sizeBytes,
                    Reason = "Stub",
                    WhatWillHappen = "Stub"
                }
            });

        public Task<CleanupResult> ExecuteCleanupAsync(
            IReadOnlyList<CleanupCandidate> candidates,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new CleanupResult();
            foreach (var cand in candidates)
            {
                if (Directory.Exists(cand.Path))
                {
                    foreach (var file in Directory.GetFiles(cand.Path))
                    {
                        long len = new FileInfo(file).Length;
                        File.Delete(file);
                        result.BytesCleaned += len;
                        result.ItemsCleanedCount++;
                    }
                }
            }
            return Task.FromResult(result);
        }
    }
}
