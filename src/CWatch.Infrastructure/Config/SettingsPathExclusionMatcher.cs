using CWatch.Core.Interfaces;
using CWatch.Core.Safety;

namespace CWatch.Infrastructure.Config;

/// <summary>
/// Delegates exclusion evaluation to the current <see cref="AppSettings.ExcludedPaths"/>.
/// The parsed glob matcher is cached and invalidated when a new pattern list is
/// assigned (settings load or save both replace the list reference), so scanning
/// does not re-parse patterns for every path.
/// </summary>
public sealed class SettingsPathExclusionMatcher : IPathExclusionMatcher
{
    private readonly ISettingsService _settingsService;
    private readonly object _cacheLock = new();
    private IReadOnlyList<string>? _cachedPatterns;
    private GlobPathExclusionMatcher? _cachedMatcher;

    public SettingsPathExclusionMatcher(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public PathExclusionDecision Evaluate(string path, bool checkAncestors = false)
    {
        return GetMatcher().Evaluate(path, checkAncestors);
    }

    private GlobPathExclusionMatcher GetMatcher()
    {
        var patterns = _settingsService.Settings.ExcludedPaths;
        lock (_cacheLock)
        {
            if (_cachedMatcher == null || !ReferenceEquals(patterns, _cachedPatterns))
            {
                _cachedPatterns = patterns;
                _cachedMatcher = GlobPathExclusionMatcher.FromSettings(_settingsService.Settings);
            }
            return _cachedMatcher;
        }
    }
}
