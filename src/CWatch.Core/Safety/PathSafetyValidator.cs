using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace CWatch.Core.Safety;

/// <summary>
/// Centralized safety barrier preventing accidental deletion of system-critical directories,
/// user roots, drive roots, and symbolic link traversal.
/// Hardened against common path-evasion techniques:
/// <list type="bullet">
/// <item>Deletions targeting descendants of protected subtrees (e.g. C:\Windows\System32\..., C:\Program Files\...)</item>
/// <item>8.3 short-name aliases (e.g. C:\PROGRA~1) that evade literal blocklist matches</item>
/// <item>Win32 trailing dot/space normalization ("C:\...\Documents. " actually opens "...\Documents")</item>
/// <item>UNC network shares and device paths (\\server\share, \\.\, \\?\)</item>
/// <item>Junction/symlink candidates (reparse points) whose traversal could escape the scanned tree</item>
/// </list>
/// </summary>
public static class PathSafetyValidator
{
    private static readonly HashSet<string> BlockedExactPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BlockedSubtreeRoots = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> AllowedSubtreePrefixes = [];

    /// <summary>
    /// Matches MS-DOS 8.3 short-name segments such as "RUNNER~1", "PROGRA~1" or "ABCDEF~1.TXT".
    /// </summary>
    private static readonly Regex ShortNameSegment = new(
        @"^[^.\s]{1,8}~\d{1,4}(\.[^.\s]{1,3})?$",
        RegexOptions.Compiled);

    static PathSafetyValidator()
    {
        // 1. Windows System Directories — blocked exactly, whole subtree protected.
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(winDir))
        {
            BlockRoot(winDir, protectSubtree: true);
            BlockRoot(Path.Combine(winDir, "System32"));
            BlockRoot(Path.Combine(winDir, "SysWOW64"));
            BlockRoot(Path.Combine(winDir, "WinSxS"));
            BlockRoot(Path.Combine(winDir, "System"));

            // Carve-out: Windows\Temp is a sanctioned, explicitly-supported cleanup target,
            // even though its parent (C:\Windows) is a protected subtree.
            string winTemp = Path.Combine(winDir, "Temp");
            try
            {
                AllowedSubtreePrefixes.Add(Normalize(Path.GetFullPath(winTemp)));
            }
            catch { /* Fall back to exact-path blocking only. */ }
        }

        // 2. Program Files — subtree protected (installed applications must never be touched).
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(pf)) BlockRoot(pf, protectSubtree: true);

        string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(pfx86)) BlockRoot(pfx86, protectSubtree: true);

        // 3. ProgramData root — subtree protected.
        string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrEmpty(pd)) BlockRoot(pd, protectSubtree: true);

        // 4. User Profile Root and Standard Personal Libraries.
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            // Profile root is blocked exactly but NOT subtree-protected: caches such as
            // %LOCALAPPDATA%\Temp live underneath it and are legitimate cleanup targets.
            BlockRoot(userProfile);

            foreach (string library in new[] { "Desktop", "Documents", "Pictures", "Videos", "Music", "Downloads" })
            {
                BlockRoot(Path.Combine(userProfile, library), protectSubtree: true);
            }
        }

        // 5. System Drive Roots (C:\, D:\, etc.)
        foreach (var drive in DriveInfo.GetDrives())
        {
            BlockRoot(drive.RootDirectory.FullName);
        }
    }

    /// <summary>
    /// Registers a blocked path. When <paramref name="protectSubtree"/> is set, every descendant
    /// of the path is also rejected by <see cref="IsSafeForCleanup"/>.
    /// Both the long path and its 8.3 short-name alias (when resolvable) are registered.
    /// </summary>
    private static void BlockRoot(string path, bool protectSubtree = false)
    {
        try
        {
            string full = Path.GetFullPath(path);
            string canonical = Normalize(full);
            if (canonical.Length == 0) return;

            BlockedExactPaths.Add(canonical);

            // Register the 8.3 short alias (e.g. C:\PROGRA~1) when the volume generates one,
            // so evasion via short paths against existing protected roots is impossible.
            string? shortForm = GetShortPathOrNull(full);
            if (!string.IsNullOrEmpty(shortForm))
            {
                string normalizedShort = Normalize(shortForm);
                BlockedExactPaths.Add(normalizedShort);

                if (protectSubtree)
                {
                    BlockedSubtreeRoots.Add(normalizedShort);
                }
            }

            if (protectSubtree)
            {
                BlockedSubtreeRoots.Add(canonical);
            }
        }
        catch
        {
            // Fail closed: skip registration rather than crash static initialization.
        }
    }

    /// <summary>
    /// Checks if a path is a critical system directory or user profile root that must NEVER be deleted.
    /// </summary>
    public static bool IsCriticalSystemPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        try
        {
            string raw = path.Trim();

            // UNC network shares and device paths (\\.\, \\?\) are treated as critical-unknown.
            if (raw.StartsWith(@"\\", StringComparison.Ordinal)) return true;

            string fullPath = Normalize(ResolveLongPath(Path.GetFullPath(raw)));

            // Check exact root match
            if (BlockedExactPaths.Contains(fullPath))
            {
                return true;
            }

            // Check if it's a drive root like "C:\"
            string? root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) && Normalize(root).Equals(fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Descendants of protected subtrees (e.g. C:\Windows\System32\config) are
            // also critical — nothing inside them may ever be deleted.
            if (IsInsideProtectedSubtree(fullPath))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return true; // Reject unparseable paths defensively
        }
    }

    /// <summary>
    /// Validates whether a target directory or file is safe for automated cache/temp cleanup.
    /// Rejects drive roots, system roots, descendants of protected subtrees, paths containing
    /// directory traversal, 8.3 short-name segments, UNC/device paths, and reparse points.
    /// </summary>
    public static bool IsSafeForCleanup(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!Path.IsPathRooted(path)) return false;

        try
        {
            string raw = path.Trim();

            // UNC network shares (\\server\share) and device paths (\\.\, \\?\) are never
            // valid automated cleanup targets.
            if (raw.StartsWith(@"\\", StringComparison.Ordinal)) return false;

            // Expand 8.3 short names (when the target exists) and resolve ".." segments.
            string fullPath = Normalize(ResolveLongPath(Path.GetFullPath(raw)));

            // Must not be a critical root
            if (IsCriticalSystemPath(fullPath)) return false;

            // Must have a valid directory depth (e.g. at least 2 levels: C:\Users\xxx\...)
            if (!HasMinimumDepth(fullPath)) return false;

            // Must not use Win32-normalization evasion (trailing dot/space) or 8.3 short names
            if (ContainsEvadingSegment(fullPath)) return false;

            // Must not live inside a protected subtree (C:\Windows\*, Program Files\*, ...)
            if (IsInsideProtectedSubtree(fullPath)) return false;

            // Must not itself be a junction/symlink whose traversal escapes the scanned tree
            if (IsReparsePointOnDisk(fullPath)) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasMinimumDepth(string normalizedFullPath)
    {
        string[] segments = normalizedFullPath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2;
    }

    private static bool ContainsEvadingSegment(string normalizedFullPath)
    {
        string[] segments = normalizedFullPath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            // Win32 strips trailing dots/spaces when the path is opened, so a candidate like
            // "C:\Users\me\Documents. " would actually delete "...\Documents".
            if (segment.EndsWith('.') || segment.EndsWith(' ')) return true;

            // An 8.3 short segment can alias a protected directory even when long-path
            // resolution failed (e.g. the candidate does not exist yet).
            if (ShortNameSegment.IsMatch(segment)) return true;
        }

        return false;
    }

    private static bool IsInsideProtectedSubtree(string normalizedFullPath)
    {
        // Sanctioned carve-out (Windows\Temp and its descendants) always wins.
        foreach (string prefix in AllowedSubtreePrefixes)
        {
            if (normalizedFullPath.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                normalizedFullPath.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Walk proper ancestors, strongest (closest to root) first.
        string current = normalizedFullPath;
        while (true)
        {
            int lastSeparator = current.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastSeparator <= 0) return false;

            current = current[..lastSeparator];
            if (BlockedSubtreeRoots.Contains(current)) return true;
        }
    }

    private static bool IsReparsePointOnDisk(string normalizedFullPath)
    {
        try
        {
            return (File.GetAttributes(normalizedFullPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false; // Nothing on disk to be a junction/symlink — string rules above decide.
        }
        catch
        {
            return true; // Cannot inspect (access denied, offline volume, ...) → fail closed.
        }
    }

    private static string Normalize(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
    }

    /// <summary>
    /// Expands 8.3 short-name segments (C:\PROGRA~1 → C:\Program Files) when the target exists
    /// on a volume that generated short names. Nonexistent targets are returned unchanged.
    /// </summary>
    private static string ResolveLongPath(string fullPath)
    {
        try
        {
            var buffer = new StringBuilder(1024);
            uint length = GetLongPathNameW(fullPath, buffer, (uint)buffer.Capacity);
            if (length == 0)
            {
                return fullPath;
            }

            if (length > (uint)buffer.Capacity)
            {
                buffer = new StringBuilder((int)length);
                length = GetLongPathNameW(fullPath, buffer, length);
                if (length == 0) return fullPath;
            }

            string result = buffer.ToString(0, (int)length);
            return string.IsNullOrWhiteSpace(result) ? fullPath : result;
        }
        catch (DllNotFoundException)
        {
            // Non-Windows host (e.g. unit tests on Linux): keep the given path.
            return fullPath;
        }
        catch (EntryPointNotFoundException)
        {
            return fullPath;
        }
    }

    private static string? GetShortPathOrNull(string fullPath)
    {
        try
        {
            var buffer = new StringBuilder(1024);
            uint length = GetShortPathNameW(fullPath, buffer, (uint)buffer.Capacity);
            if (length == 0 || length > (uint)buffer.Capacity) return null;

            string result = buffer.ToString(0, (int)length);
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathNameW(string lpszShortPath, [Out] StringBuilder lpszLongPath, uint cchBuffer);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(string lpszLongPath, [Out] StringBuilder lpszShortPath, uint cchBuffer);
}
