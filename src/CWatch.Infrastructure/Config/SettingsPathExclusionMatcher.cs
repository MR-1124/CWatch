using CWatch.Core.Interfaces;
using CWatch.Core.Safety;

namespace CWatch.Infrastructure.Config;

/// <summary>
/// Delegates exclusion evaluation to the current <see cref="AppSettings.ExcludedPaths"/>
/// snapshot, so pattern edits take effect immediately without re-registering services.
/// </summary>
public sealed class SettingsPathExclusionMatcher : IPathExclusionMatcher
{
    private readonly ISettingsService _settingsService;

    public SettingsPathExclusionMatcher(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public PathExclusionDecision Evaluate(string path, bool checkAncestors = false)
        => GlobPathExclusionMatcher.FromSettings(_settingsService.Settings).Evaluate(path, checkAncestors);
}
