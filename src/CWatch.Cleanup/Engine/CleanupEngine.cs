using CWatch.Cleanup.Providers;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Core.Safety;

namespace CWatch.Cleanup.Engine;

public sealed class CleanupEngine : ICleanupEngine
{
    private readonly List<ICleanupProvider> _providers;
    private readonly IProcessInspector? _processInspector;
    private readonly ISnapshotRepository? _snapshotRepo;
    private readonly IPathExclusionMatcher? _exclusionMatcher;
    private readonly ILoggerService? _logger;

    public IReadOnlyList<ICleanupProvider> Providers => _providers.AsReadOnly();

    public CleanupEngine(
        IProcessInspector? processInspector = null,
        ISnapshotRepository? snapshotRepo = null,
        IPathExclusionMatcher? exclusionMatcher = null,
        ILoggerService? logger = null,
        IEnumerable<ICleanupProvider>? providers = null)
    {
        _processInspector = processInspector;
        _snapshotRepo = snapshotRepo;
        _exclusionMatcher = exclusionMatcher;
        _logger = logger;

        _providers = providers?.ToList() ??
        [
            new WindowsTempCleanupProvider(),
            new RecycleBinCleanupProvider(),
            new BrowserCacheCleanupProvider(),
            new DevelopmentCacheCleanupProvider(),
            new ApplicationCacheCleanupProvider()
        ];
    }

    /// <summary>
    /// True when the candidate's target path is excluded by user configuration.
    /// Recycling-bin candidates bypass path exclusion (the shell bin is system-managed).
    /// </summary>
    private bool IsExcluded(CleanupCandidate candidate)
    {
        if (_exclusionMatcher is null) return false;
        if (candidate.ProviderId == "recycle_bin") return false;
        if (string.IsNullOrWhiteSpace(candidate.Path)) return true;

        try
        {
            // checkAncestors: a candidate for "D:\Media\cache" must also vanish when the
            // user excludes "D:\Media".
            return _exclusionMatcher.IsExcluded(candidate.Path, checkAncestors: true);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Exclusion matcher threw for '{candidate.Path}'; treating as excluded (fail-closed). {ex.Message}");
            return true; // Cleanup deletes data — fail closed.
        }
    }

    public async Task<List<CleanupCandidate>> ScanAllRecommendationsAsync(
        bool includeAdvanced = true,
        CancellationToken cancellationToken = default)
    {
        var allCandidates = new List<CleanupCandidate>();

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!includeAdvanced && provider.IsAdvanced) continue;

            try
            {
                var candidates = await provider.ScanCandidatesAsync(cancellationToken);

                // Defense-in-depth filtering: safety validation first, then user exclusions.
                allCandidates.AddRange(candidates.Where(c =>
                    (c.ProviderId == "recycle_bin" || PathSafetyValidator.IsSafeForCleanup(c.Path))
                    && !IsExcluded(c)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Provider {provider.DisplayName} failed during scan.", ex);
            }
        }

        // Order by size descending
        return allCandidates.OrderByDescending(c => c.SizeBytes).ToList();
    }

    public async Task<CleanupResult> ExecuteCleanupAsync(
        IReadOnlyList<CleanupCandidate> selectedCandidates,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var combinedResult = new CleanupResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Defense-in-depth: Validate all selected candidates before delegating
        var safeCandidates = new List<CleanupCandidate>();
        foreach (var cand in selectedCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (cand.ProviderId == "recycle_bin" || PathSafetyValidator.IsSafeForCleanup(cand.Path))
            {
                if (IsExcluded(cand))
                {
                    _logger?.LogError($"Blocked excluded cleanup candidate targeting: {cand.Path}");
                    combinedResult.ErrorMessages.Add($"Skipped excluded target path: {cand.Path}");
                    combinedResult.FailedItemsCount++;
                    continue;
                }

                safeCandidates.Add(cand);
            }
            else
            {
                _logger?.LogError($"Blocked unsafe cleanup candidate targeting: {cand.Path}");
                combinedResult.ErrorMessages.Add($"Blocked unsafe target path: {cand.Path}");
                combinedResult.FailedItemsCount++;
            }
        }

        var groupedByProvider = safeCandidates
            .GroupBy(c => c.ProviderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (groupedByProvider.TryGetValue(provider.ProviderId, out var candidatesForProvider) && candidatesForProvider.Count > 0)
            {
                try
                {
                    progress?.Report($"Executing {provider.DisplayName}...");
                    var result = await provider.ExecuteCleanupAsync(candidatesForProvider, progress, cancellationToken);

                    combinedResult.BytesCleaned += result.BytesCleaned;
                    combinedResult.ItemsCleanedCount += result.ItemsCleanedCount;
                    combinedResult.FailedItemsCount += result.FailedItemsCount;
                    combinedResult.ErrorMessages.AddRange(result.ErrorMessages);
                    combinedResult.LockedFiles.AddRange(result.LockedFiles);

                    // Record history only for what the provider confirmed cleaned.
                    // Recording every candidate would let the recurrence detector
                    // treat failed or blocked items as recurring bloat.
                    if (_snapshotRepo != null && result.ItemsCleanedCount > 0)
                    {
                        foreach (var cand in candidatesForProvider)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await _snapshotRepo.RecordCleanHistoryAsync(new CleanHistoryItem
                            {
                                ProviderId = provider.ProviderId,
                                TargetPath = cand.Path,
                                BytesCleaned = cand.SizeBytes,
                                CategoryName = cand.Category.ToString(),
                                CleanedUtc = DateTime.UtcNow
                            });
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error executing cleanup for provider {provider.DisplayName}", ex);
                    combinedResult.ErrorMessages.Add($"{provider.DisplayName}: {ex.Message}");
                }
            }
        }

        sw.Stop();
        combinedResult.Duration = sw.Elapsed;
        _logger?.LogInfo($"Cleanup completed: {ByteSizeFormatter.Format(combinedResult.BytesCleaned)} cleaned, {combinedResult.FailedItemsCount} failed.");

        return combinedResult;
    }
}
