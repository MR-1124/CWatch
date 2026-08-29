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
    private readonly ILoggerService? _logger;

    public IReadOnlyList<ICleanupProvider> Providers => _providers.AsReadOnly();

    public CleanupEngine(
        IProcessInspector? processInspector = null,
        ISnapshotRepository? snapshotRepo = null,
        ILoggerService? logger = null)
    {
        _processInspector = processInspector;
        _snapshotRepo = snapshotRepo;
        _logger = logger;

        _providers =
        [
            new WindowsTempCleanupProvider(),
            new RecycleBinCleanupProvider(),
            new BrowserCacheCleanupProvider(),
            new DevelopmentCacheCleanupProvider(),
            new ApplicationCacheCleanupProvider()
        ];
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
                // Filter out any candidates failing safety validation defensively
                allCandidates.AddRange(candidates.Where(c => c.ProviderId == "recycle_bin" || PathSafetyValidator.IsSafeForCleanup(c.Path)));
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
            if (cand.ProviderId == "recycle_bin" || PathSafetyValidator.IsSafeForCleanup(cand.Path))
            {
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

                    // Record Clean History in database
                    if (_snapshotRepo != null)
                    {
                        foreach (var cand in candidatesForProvider)
                        {
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
