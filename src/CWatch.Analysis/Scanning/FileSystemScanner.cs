using System.Security;
using System.Security.Cryptography;
using CWatch.Analysis.Classifiers;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Core.Safety;

namespace CWatch.Analysis.Scanning;

public sealed class FileSystemScanner : IFileSystemScanner
{
    private readonly ILoggerService? _logger;
    private readonly IPathExclusionMatcher? _exclusionMatcher;

    public FileSystemScanner(ILoggerService? logger = null, IPathExclusionMatcher? exclusionMatcher = null)
    {
        _logger = logger;
        _exclusionMatcher = exclusionMatcher;
    }

    /// <summary>
    /// Returns true when the given path is excluded by the configured exclusion patterns.
    /// Fail-open on matcher errors would defeat the user's privacy/perf intent for scans,
    /// so any exception is logged and treated as "not excluded" (scan safety is not at stake).
    /// </summary>
    private bool IsExcluded(string path, bool checkAncestors = false)
    {
        if (_exclusionMatcher is null) return false;
        try
        {
            return _exclusionMatcher.IsExcluded(path, checkAncestors);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Exclusion matcher threw for '{path}'; treating as included. {ex.Message}");
            return false;
        }
    }

    public async Task<StorageItem> ScanDirectoryAsync(
        string rootPath,
        IProgress<ScanProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var rootDir = new DirectoryInfo(rootPath);
            if (!rootDir.Exists)
            {
                throw new DirectoryNotFoundException($"Directory not found: {rootPath}");
            }

            var progressInfo = new ScanProgressInfo
            {
                CurrentPath = rootPath,
                CurrentPhase = "Scanning directory structure..."
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long filesScanned = 0;
            long dirsScanned = 0;
            long bytesProcessed = 0;
            long lastProgressTick = 0;

            // Progress heuristic (equal share per subdirectory):
            // the root owns a share of 1.0. Each directory splits its share evenly among the
            // subdirectories it will recurse into; a directory with no subdirectories adds its
            // whole share to completedShare once its files are processed. completedShare only
            // grows and reaches exactly 1.0 when the walk ends, without a costly pre-count.
            // It ignores how big each subtree really is, so the bar can speed up or stall
            // (e.g. one huge user profile next to small ones), but it never goes backwards.
            double completedShare = 0;

            // Progress<T> posts to the UI thread asynchronously, so every report is a fresh
            // snapshot: the UI never reads an instance the scan thread is still mutating, and
            // bindings see a new object (MainViewModel.SetProperty compares by reference).
            ScanProgressInfo Snapshot() => new()
            {
                CurrentPath = progressInfo.CurrentPath,
                CurrentPhase = progressInfo.CurrentPhase,
                FilesScanned = progressInfo.FilesScanned,
                DirectoriesScanned = progressInfo.DirectoriesScanned,
                BytesProcessed = progressInfo.BytesProcessed,
                EstimatedPercent = progressInfo.EstimatedPercent,
                IsIndeterminate = progressInfo.IsIndeterminate,
                Elapsed = progressInfo.Elapsed
            };

            var rootNode = new StorageItem
            {
                FullPath = rootDir.FullName,
                Name = string.IsNullOrEmpty(rootDir.Name) ? rootDir.FullName : rootDir.Name,
                IsDirectory = true,
                Category = CategoryClassifier.Classify(rootDir.FullName, true),
                LastModifiedUtc = rootDir.LastWriteTimeUtc
            };

            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            void ScanRecursive(DirectoryInfo currentDir, StorageItem currentNode, int depth, double share)
            {
                cancellationToken.ThrowIfCancellationRequested();

                dirsScanned++;
                progressInfo.DirectoriesScanned = dirsScanned;
                progressInfo.CurrentPath = currentDir.FullName;

                long now = stopwatch.ElapsedMilliseconds;
                if (now - lastProgressTick > 120)
                {
                    lastProgressTick = now;
                    progressInfo.FilesScanned = filesScanned;
                    progressInfo.BytesProcessed = bytesProcessed;
                    progressInfo.Elapsed = stopwatch.Elapsed;
                    // Capped below 100 until the walk actually finishes.
                    progressInfo.EstimatedPercent = Math.Min(completedShare * 100.0, 99.0);
                    progressInfo.IsIndeterminate = progressInfo.EstimatedPercent <= 0;
                    progress?.Report(Snapshot());
                }

                // 1. Process files in current directory
                try
                {
                    foreach (var file in currentDir.EnumerateFiles("*", enumOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (IsExcluded(file.FullName))
                        {
                            continue;
                        }

                        filesScanned++;
                        long size = file.Length;
                        bytesProcessed += size;

                        var fileNode = new StorageItem
                        {
                            FullPath = file.FullName,
                            Name = file.Name,
                            SizeBytes = size,
                            IsDirectory = false,
                            Category = CategoryClassifier.Classify(file.FullName, false),
                            LastModifiedUtc = file.LastWriteTimeUtc,
                            Extension = file.Extension,
                            ParentPath = currentDir.FullName,
                            Parent = currentNode
                        };

                        currentNode.Children.Add(fileNode);
                        currentNode.SizeBytes += size;
                        currentNode.FileCount++;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or PathTooLongException or IOException)
                {
                    currentNode.IsInaccessible = true;
                }

                // 2. Collect subdirectories first so this directory's progress share can be
                //    split evenly among them (see the heuristic note above).
                var subDirs = new List<DirectoryInfo>();
                try
                {
                    foreach (var subDir in currentDir.EnumerateDirectories("*", enumOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Avoid recursion into junction points/symlinks
                        if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }

                        if (IsExcluded(subDir.FullName))
                        {
                            continue;
                        }

                        subDirs.Add(subDir);
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or PathTooLongException or IOException)
                {
                    currentNode.IsInaccessible = true;
                }

                if (subDirs.Count == 0)
                {
                    completedShare += share;
                }

                // 3. Process subdirectories
                double childShare = subDirs.Count > 0 ? share / subDirs.Count : 0;
                try
                {
                    foreach (var subDir in subDirs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var subNode = new StorageItem
                        {
                            FullPath = subDir.FullName,
                            Name = subDir.Name,
                            IsDirectory = true,
                            Category = CategoryClassifier.Classify(subDir.FullName, true),
                            LastModifiedUtc = subDir.LastWriteTimeUtc,
                            ParentPath = currentDir.FullName,
                            Parent = currentNode
                        };

                        ScanRecursive(subDir, subNode, depth + 1, childShare);

                        currentNode.Children.Add(subNode);
                        currentNode.SizeBytes += subNode.SizeBytes;
                        currentNode.FileCount += subNode.FileCount;
                        currentNode.DirectoryCount += subNode.DirectoryCount + 1;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or PathTooLongException or IOException)
                {
                    currentNode.IsInaccessible = true;
                }

                // Calculate relative percentages among direct children
                if (currentNode.SizeBytes > 0)
                {
                    foreach (var child in currentNode.Children)
                    {
                        child.RelativePercentage = (double)child.SizeBytes / currentNode.SizeBytes * 100.0;
                    }
                }

                // Sort children largest to smallest
                currentNode.Children.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
            }

            ScanRecursive(rootDir, rootNode, 0, 1.0);

            stopwatch.Stop();
            progressInfo.FilesScanned = filesScanned;
            progressInfo.DirectoriesScanned = dirsScanned;
            progressInfo.BytesProcessed = bytesProcessed;
            progressInfo.CurrentPhase = "Analysis complete";
            progressInfo.Elapsed = stopwatch.Elapsed;
            progressInfo.EstimatedPercent = 100.0;
            progressInfo.IsIndeterminate = false;
            progress?.Report(Snapshot());

            _logger?.LogInfo($"Scanned {rootPath}: {filesScanned} files, {dirsScanned} dirs, total {ByteSizeFormatter.Format(rootNode.SizeBytes)} in {stopwatch.Elapsed.TotalSeconds:F2}s");

            return rootNode;
        }, cancellationToken);
    }

    public async Task<List<StorageItem>> FindLargestFilesAsync(
        string rootPath,
        int count = 100,
        IProgress<ScanProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var largestFiles = new List<StorageItem>(count + 1);
            var rootDir = new DirectoryInfo(rootPath);
            if (!rootDir.Exists) return [];

            var progressInfo = new ScanProgressInfo
            {
                CurrentPath = rootPath,
                CurrentPhase = "Locating largest files..."
            };

            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            long scanned = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long lastTick = 0;

            try
            {
                foreach (var file in rootDir.EnumerateFiles("*", enumOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Recursive enumeration never visits directory nodes, so files living
                    // under excluded directories are caught via ancestor evaluation.
                    if (IsExcluded(file.FullName, checkAncestors: true))
                    {
                        continue;
                    }

                    scanned++;

                    long now = sw.ElapsedMilliseconds;
                    if (now - lastTick > 150)
                    {
                        lastTick = now;
                        progressInfo.FilesScanned = scanned;
                        progressInfo.CurrentPath = file.FullName;
                        progress?.Report(progressInfo);
                    }

                    // Keep sorted top list
                    if (largestFiles.Count < count || file.Length > largestFiles[^1].SizeBytes)
                    {
                        var item = new StorageItem
                        {
                            FullPath = file.FullName,
                            Name = file.Name,
                            SizeBytes = file.Length,
                            IsDirectory = false,
                            Category = CategoryClassifier.Classify(file.FullName, false),
                            LastModifiedUtc = file.LastWriteTimeUtc,
                            Extension = file.Extension,
                            ParentPath = file.DirectoryName
                        };

                        int index = largestFiles.BinarySearch(item, Comparer<StorageItem>.Create((a, b) => b.SizeBytes.CompareTo(a.SizeBytes)));
                        if (index < 0) index = ~index;
                        largestFiles.Insert(index, item);

                        if (largestFiles.Count > count)
                        {
                            largestFiles.RemoveAt(largestFiles.Count - 1);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Cancellation must propagate to the awaiting caller.
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error during FindLargestFiles scan.", ex);
            }

            return largestFiles;
        }, cancellationToken);
    }

    public async Task<List<List<StorageItem>>> FindDuplicateFilesAsync(
        string rootPath,
        IProgress<ScanProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var duplicateGroups = new List<List<StorageItem>>();
            var rootDir = new DirectoryInfo(rootPath);
            if (!rootDir.Exists) return duplicateGroups;

            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            // Stage 1: Group by file size (> 100KB to filter out empty/trivial files)
            var sizeBuckets = new Dictionary<long, List<FileInfo>>();
            long scanned = 0;
            long lastProgressEmitMs = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var progressInfo = new ScanProgressInfo
            {
                CurrentPath = rootPath,
                CurrentPhase = "Phase 1/3: Indexing file sizes..."
            };

            try
            {
                foreach (var file in rootDir.EnumerateFiles("*", enumOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Recursive enumeration never visits directory nodes, so files living
                    // under excluded directories are caught via ancestor evaluation.
                    if (IsExcluded(file.FullName, checkAncestors: true))
                    {
                        continue;
                    }

                    scanned++;

                    if (file.Length < 100 * 1024) continue; // Skip files smaller than 100KB

                    if (!sizeBuckets.TryGetValue(file.Length, out var list))
                    {
                        list = [];
                        sizeBuckets[file.Length] = list;
                    }
                    list.Add(file);

                    long now = sw.ElapsedMilliseconds;
                    if (now - lastProgressEmitMs > 150)
                    {
                        lastProgressEmitMs = now;
                        progressInfo.FilesScanned = scanned;
                        progressInfo.CurrentPath = file.FullName;
                        progress?.Report(progressInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error enumerating files for duplicate scan.", ex);
            }

            // Filter buckets with >= 2 files
            var candidateBuckets = sizeBuckets.Values.Where(b => b.Count > 1).ToList();

            // Stage 2: Quick header hash (first 4KB)
            progressInfo.CurrentPhase = "Phase 2/3: Quick header verification...";
            progress?.Report(progressInfo);

            var headerBuckets = new Dictionary<string, List<FileInfo>>();
            foreach (var bucket in candidateBuckets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var file in bucket)
                {
                    string? headerHash = ComputePartialHash(file.FullName, 4096);
                    if (headerHash == null) continue;

                    string key = $"{file.Length}:{headerHash}";
                    if (!headerBuckets.TryGetValue(key, out var hList))
                    {
                        hList = [];
                        headerBuckets[key] = hList;
                    }
                    hList.Add(file);
                }
            }

            var confirmedCandidates = headerBuckets.Values.Where(b => b.Count > 1).ToList();

            // Stage 3: Full SHA256 verification
            progressInfo.CurrentPhase = "Phase 3/3: Full SHA-256 verification...";
            progress?.Report(progressInfo);

            var fullHashBuckets = new Dictionary<string, List<StorageItem>>();
            foreach (var bucket in confirmedCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var file in bucket)
                {
                    string? fullHash = ComputeFullSha256(file.FullName, cancellationToken);
                    if (fullHash == null) continue;

                    if (!fullHashBuckets.TryGetValue(fullHash, out var fList))
                    {
                        fList = [];
                        fullHashBuckets[fullHash] = fList;
                    }

                    fList.Add(new StorageItem
                    {
                        FullPath = file.FullName,
                        Name = file.Name,
                        SizeBytes = file.Length,
                        IsDirectory = false,
                        Category = CategoryClassifier.Classify(file.FullName, false),
                        LastModifiedUtc = file.LastWriteTimeUtc,
                        Extension = file.Extension,
                        ParentPath = file.DirectoryName
                    });
                }
            }

            foreach (var group in fullHashBuckets.Values.Where(g => g.Count > 1))
            {
                duplicateGroups.Add(group);
            }

            return duplicateGroups;
        }, cancellationToken);
    }

    private static string? ComputePartialHash(string filePath, int bytesToRead)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] buffer = new byte[bytesToRead];
            int read = fs.Read(buffer, 0, buffer.Length);
            if (read <= 0) return null;

            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(buffer, 0, read);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return null;
        }
    }

    private static string? ComputeFullSha256(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(fs);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return null;
        }
    }
}
