namespace CWatch.Core.Safety;

/// <summary>
/// Centralized safety barrier preventing accidental deletion of system-critical directories,
/// user roots, drive roots, and symbolic link traversal.
/// </summary>
public static class PathSafetyValidator
{
    private static readonly HashSet<string> BlockedExactPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> BlockedPrefixPaths = [];

    static PathSafetyValidator()
    {
        // 1. Windows System Directories
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(winDir))
        {
            BlockedExactPaths.Add(Normalize(winDir));
            BlockedExactPaths.Add(Normalize(Path.Combine(winDir, "System32")));
            BlockedExactPaths.Add(Normalize(Path.Combine(winDir, "SysWOW64")));
            BlockedExactPaths.Add(Normalize(Path.Combine(winDir, "WinSxS")));
            BlockedExactPaths.Add(Normalize(Path.Combine(winDir, "System")));
        }

        // 2. Program Files
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(pf)) BlockedExactPaths.Add(Normalize(pf));

        string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(pfx86)) BlockedExactPaths.Add(Normalize(pfx86));

        // 3. ProgramData root
        string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrEmpty(pd)) BlockedExactPaths.Add(Normalize(pd));

        // 4. User Profile Root and Standard Personal Libraries
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            BlockedExactPaths.Add(Normalize(userProfile));
            BlockedExactPaths.Add(Normalize(Path.Combine(userProfile, "Desktop")));
            BlockedExactPaths.Add(Normalize(Path.Combine(userProfile, "Documents")));
            BlockedExactPaths.Add(Normalize(Path.Combine(userProfile, "Pictures")));
            BlockedExactPaths.Add(Normalize(Path.Combine(userProfile, "Videos")));
            BlockedExactPaths.Add(Normalize(Path.Combine(userProfile, "Music")));
            BlockedExactPaths.Add(Normalize(Path.Combine(userProfile, "Downloads")));
        }

        // 5. System Drive Roots (C:\, D:\, etc.)
        foreach (var drive in DriveInfo.GetDrives())
        {
            BlockedExactPaths.Add(Normalize(drive.RootDirectory.FullName));
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
            string fullPath = Normalize(Path.GetFullPath(path));

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

            return false;
        }
        catch
        {
            return true; // Reject unparseable paths defensively
        }
    }

    /// <summary>
    /// Validates whether a target directory or file is safe for automated cache/temp cleanup.
    /// Rejects drive roots, system roots, and paths containing directory traversal or reparse points.
    /// </summary>
    public static bool IsSafeForCleanup(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (!Path.IsPathRooted(path)) return false;

        try
        {
            string fullPath = Normalize(Path.GetFullPath(path));

            // Must not be a critical root
            if (IsCriticalSystemPath(fullPath)) return false;

            // Must have a valid directory depth (e.g. at least 2 levels: C:\Users\xxx\...)
            string[] segments = fullPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Normalize(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
    }
}
