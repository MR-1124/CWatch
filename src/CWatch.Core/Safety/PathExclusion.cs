using CWatch.Core.Models;

namespace CWatch.Core.Safety;

/// <summary>
/// Match result for exclusion evaluation.
/// </summary>
public enum PathExclusionDecision
{
    /// <summary>Path is not excluded and neither is any ancestor.</summary>
    Included,
    /// <summary>The path itself matches an exclusion pattern.</summary>
    Excluded,
    /// <summary>An ancestor of the path matched an exclusion pattern.</summary>
    ExcludedByAncestor
}

/// <summary>
/// Contract for evaluating whether a filesystem path is excluded by user configuration.
/// Implemented in Infrastructure so it can read live settings without coupling Core to them.
/// </summary>
public interface IPathExclusionMatcher
{
    /// <summary>
    /// Evaluates the path (and, when <paramref name="checkAncestors"/> is set, its ancestors)
    /// against the configured exclusion patterns.
    /// </summary>
    PathExclusionDecision Evaluate(string path, bool checkAncestors = false);

    /// <summary>Convenience wrapper around <see cref="Evaluate"/>.</summary>
    bool IsExcluded(string path, bool checkAncestors = false)
        => Evaluate(path, checkAncestors) != PathExclusionDecision.Included;
}

/// <summary>Convenience extension usable from any <see cref="IPathExclusionMatcher"/> reference.</summary>
public static class PathExclusionMatcherExtensions
{
    public static bool IsExcluded(this IPathExclusionMatcher matcher, string path, bool checkAncestors = false)
        => matcher.Evaluate(path, checkAncestors) != PathExclusionDecision.Included;
}

/// <summary>
/// Glob-style exclusion matcher driven by <see cref="AppSettings.ExcludedPaths"/>.
/// Supported pattern syntax per entry:
/// <list type="bullet">
/// <item>"D:\BigMedia" — exact directory or file (any casing)</item>
/// <item>"D:\BigMedia\*" — every child of that directory (the directory itself stays visible)</item>
/// <item>"*.log" — files with that extension anywhere under the scanned root</item>
/// </list>
/// Blank entries, drive roots, and path-traversal segments are ignored defensively:
/// an exclusion must never widen into "exclude everything" or "exclude a parent of everything".
/// </summary>
public sealed class GlobPathExclusionMatcher : IPathExclusionMatcher
{
    private readonly IReadOnlyList<string> _patterns;
    private readonly IReadOnlyList<(string Pattern, string Normalized)> _normalizedPatterns;

    public GlobPathExclusionMatcher(IReadOnlyList<string> patterns)
    {
        _patterns = patterns ?? [];
        _normalizedPatterns = _patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => (Pattern: p.Trim(), Normalized: NormalizePattern(p)))
            .Where(t => t.Normalized.Length > 0)
            .ToList();
    }

    /// <summary>Builds a matcher from the current settings snapshot.</summary>
    public static GlobPathExclusionMatcher FromSettings(AppSettings settings)
        => new(settings?.ExcludedPaths ?? []);

    public PathExclusionDecision Evaluate(string path, bool checkAncestors = false)
    {
        if (string.IsNullOrWhiteSpace(path) || _normalizedPatterns.Count == 0)
        {
            return PathExclusionDecision.Included;
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
        }
        catch
        {
            return PathExclusionDecision.Included;
        }

        // The path itself may not be a drive root, and no pattern may name a drive root:
        // excluding "C:\" would disable the entire product.
        if (IsDriveRoot(normalized)) return PathExclusionDecision.Included;

        var self = EvaluateNormalized(normalized);
        if (self != PathExclusionDecision.Included)
        {
            return self;
        }

        if (checkAncestors)
        {
            string? parent = Path.GetDirectoryName(normalized);
            while (!string.IsNullOrEmpty(parent))
            {
                if (IsDriveRoot(parent)) break;

                if (EvaluateNormalized(parent) != PathExclusionDecision.Included)
                {
                    return PathExclusionDecision.ExcludedByAncestor;
                }
                parent = Path.GetDirectoryName(parent);
            }
        }

        return PathExclusionDecision.Included;
    }

    private PathExclusionDecision EvaluateNormalized(string normalizedPath)
    {
        string fileName = Path.GetFileName(normalizedPath);

        foreach (var (pattern, normalizedPattern) in _normalizedPatterns)
        {
            bool patternHasGlob = normalizedPattern.Contains('*');

            if (!patternHasGlob)
            {
                // Exact directory/file match.
                if (string.Equals(normalizedPath, normalizedPattern, StringComparison.Ordinal))
                {
                    return PathExclusionDecision.Excluded;
                }
                continue;
            }

            // "*.ext" style: matches this file name anywhere.
            if (pattern.StartsWith('*') && !pattern.Contains(':') && !pattern.Contains(Path.DirectorySeparatorChar))
            {
                if (MatchesGlob(fileName, normalizedPattern))
                {
                    return PathExclusionDecision.Excluded;
                }
                continue;
            }

            // "DIR\*" style: matches children of the named directory (the directory
            // itself stays visible; use the exact form to hide the directory too).
            string patternDir = normalizedPattern[..^1].TrimEnd(Path.DirectorySeparatorChar); // drop trailing "*"
            if (normalizedPath.StartsWith(patternDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return PathExclusionDecision.Excluded;
            }
        }

        return PathExclusionDecision.Included;
    }

    private static string NormalizePattern(string pattern)
    {
        string trimmed = pattern.Trim().Trim('"');

        // Bare file-name globs ("*.log") must NOT be rooted to the current directory —
        // keep their relative form so they match by file name anywhere.
        if (!Path.IsPathRooted(trimmed))
        {
            return trimmed.ToUpperInvariant();
        }

        try
        {
            trimmed = Path.GetFullPath(trimmed);
        }
        catch
        {
            // Unrootable pattern; keep as-is and let matching treat it literally.
        }
        return trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
    }

    private static bool IsDriveRoot(string normalizedPath)
    {
        return normalizedPath.Length <= 3
            && normalizedPath.Length >= 2
            && char.IsAsciiLetter(normalizedPath[0])
            && (normalizedPath.Length == 2 || normalizedPath[1] == ':');
    }

    /// <summary>Minimal glob: '*' matches any run of characters; '?' matches one character.</summary>
    private static bool MatchesGlob(string input, string pattern)
    {
        int i = 0, p = 0, star = -1, mark = 0;
        while (i < input.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == input[i]))
            {
                i++; p++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = i;
            }
            else if (star >= 0)
            {
                p = star + 1;
                i = ++mark;
            }
            else
            {
                return false;
            }
        }
        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }
}
