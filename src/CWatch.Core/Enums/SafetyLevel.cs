namespace CWatch.Core.Enums;

/// <summary>
/// Safety classification for cleanup operations.
/// Explicitly guides users on the risk level of removing specific files or directories.
/// </summary>
public enum SafetyLevel
{
    /// <summary>
    /// Known disposable temporary or cache data that can be safely removed.
    /// Applications will reconstruct necessary files automatically.
    /// </summary>
    Safe = 0,

    /// <summary>
    /// Low-risk items like package caches (npm, NuGet, Gradle).
    /// Deleting will not damage projects, but initial subsequent builds may need to redownload dependencies.
    /// </summary>
    LowRisk = 1,

    /// <summary>
    /// Review needed. Removing may reset application cache state, log out sessions,
    /// or discard non-critical user-generated caches.
    /// </summary>
    Review = 2,

    /// <summary>
    /// Dangerous / System critical. Must NOT be deleted automatically or via one-click clean.
    /// </summary>
    Dangerous = 3,

    /// <summary>
    /// Unknown risk. Do not recommend automatic deletion.
    /// </summary>
    Unknown = 4
}
