namespace CWatch.Core.Models;

/// <summary>
/// Normalizes and validates drive-letter tokens ("C:", "d", "D:\") used across settings,
/// snapshots, scanning, and monitoring. The canonical in-memory form is a single ASCII
/// letter followed by a colon (e.g. "C:").
/// </summary>
public static class DriveLetters
{
    public const string SystemDrive = "C:";

    /// <summary>
    /// Returns the canonical "X:" form for any drive token, falling back to the system
    /// drive when the input is blank or not a single ASCII letter.
    /// </summary>
    public static string Normalize(string? driveLetter)
    {
        if (string.IsNullOrWhiteSpace(driveLetter))
        {
            return SystemDrive;
        }

        string trimmed = driveLetter.Trim().TrimEnd(':', '\\', '/');

        if (trimmed.Length != 1 || !char.IsAsciiLetter(trimmed[0]))
        {
            return SystemDrive;
        }

        return char.ToUpperInvariant(trimmed[0]) + ":";
    }

    /// <summary>
    /// Returns the volume root path ("X:\") for a drive token.
    /// </summary>
    public static string GetRootPath(string driveLetter)
    {
        return Normalize(driveLetter) + Path.DirectorySeparatorChar;
    }
}
