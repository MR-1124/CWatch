using CWatch.Core.Models;
using CWatch.Core.Safety;
using Xunit;

namespace CWatch.Tests;

/// <summary>
/// Hardening regression tests for PathSafetyValidator: subtree protection, path-evasion
/// techniques (8.3 short names, trailing dot/space, UNC/device paths), reparse-point
/// rejection, and the Windows\Temp sanctioned carve-out.
/// </summary>
public class SecurityHardeningTests
{
    private static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static string Profile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [Fact]
    public void Descendants_OfProtectedSubtrees_AreRejected()
    {
        // Deeper descendants of exact-blocked directories must also be rejected.
        string deepSystem = Path.Combine(WinDir, "System32", "config", "regback");
        Assert.True(PathSafetyValidator.IsCriticalSystemPath(deepSystem));
        Assert.False(PathSafetyValidator.IsSafeForCleanup(deepSystem));

        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.False(PathSafetyValidator.IsSafeForCleanup(Path.Combine(pf, "Some App", "cache")));

        string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        Assert.False(PathSafetyValidator.IsSafeForCleanup(Path.Combine(pd, "Vendor", "Temp")));
    }

    [Fact]
    public void ProgramFiles_Subtree_IsBlocked_EvenThoughOnlyRootWasExactBlocked()
    {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string nested = Path.Combine(pf, "Contoso", "logs");

        Assert.True(PathSafetyValidator.IsCriticalSystemPath(pf));
        // Root still exact-blocked; descendants rejected through subtree protection.
        Assert.False(PathSafetyValidator.IsSafeForCleanup(nested));
    }

    [Fact]
    public void WindowsTemp_CarveOut_IsAllowed_UnderProtectedWindowsSubtree()
    {
        Assert.False(PathSafetyValidator.IsSafeForCleanup(WinDir));

        string winTemp = Path.Combine(WinDir, "Temp");
        Assert.True(PathSafetyValidator.IsSafeForCleanup(winTemp));

        string winTempSub = Path.Combine(winTemp, "nsresult.tmp");
        Assert.True(PathSafetyValidator.IsSafeForCleanup(winTempSub));
    }

    [Fact]
    public void TrailingDotAndSpace_Evasion_IsRejected()
    {
        string docs = Path.Combine(Profile, "Documents");

        // Win32 strips trailing dots/spaces when the path is opened: "Documents." and
        // "Documents   " resolve to "Documents" - a protected personal library.
        Assert.False(PathSafetyValidator.IsSafeForCleanup(docs + "."));
        Assert.False(PathSafetyValidator.IsSafeForCleanup(docs + "   "));
        Assert.False(PathSafetyValidator.IsSafeForCleanup(docs + ". ."));
    }

    [Fact]
    public void UncAndDevicePaths_AreRejected()
    {
        Assert.False(PathSafetyValidator.IsSafeForCleanup(@"\\server\share\cache"));
        Assert.False(PathSafetyValidator.IsSafeForCleanup(@"\\?\C:\Users\me\AppData\Local\Temp"));
        Assert.False(PathSafetyValidator.IsSafeForCleanup(@"\\.\C:\Users\me\AppData\Local\Temp"));
        Assert.True(PathSafetyValidator.IsCriticalSystemPath(@"\\server\share\cache"));
    }

    [Fact]
    public void ShortNameSegments_AreRejected()
    {
        // Even without on-disk short-name generation, candidate paths that look like
        // 8.3 aliases must be rejected as a class.
        Assert.False(PathSafetyValidator.IsSafeForCleanup(@"C:\Users\me\APPDATA~1\Temp"));
        Assert.False(PathSafetyValidator.IsSafeForCleanup(@"C:\PROGRA~1\Contoso\cache.txt"));

        // Verbatim long path remains acceptable.
        Assert.True(PathSafetyValidator.IsSafeForCleanup(@"C:\Users\me\AppData\Local\npm-cache"));
    }

    [Fact]
    public void ReparsePointCandidate_IsRejected()
    {
        string junction = Path.Combine(Path.GetTempPath(), $"cwatch_junction_{Guid.NewGuid():N}");
        string target = Path.Combine(Path.GetTempPath(), $"cwatch_jtarget_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "payload.txt"), new string('x', 128));

            // Windows: directory junction via mklink /J; Unix-style symlink on other hosts.
            if (OperatingSystem.IsWindows())
            {
                RunHidden("cmd.exe", $"/c mklink /J \"{junction}\" \"{target}\"");
            }
            else
            {
                RunHidden("ln", $"-s \"{target}\" \"{junction}\"");
            }

            if (Directory.Exists(junction))
            {
                Assert.False(PathSafetyValidator.IsSafeForCleanup(junction));
            }
        }
        finally
        {
            TryDelete(junction);
            TryDelete(target);
        }
    }

    [Fact]
    public void CaseVariants_OfBlockedPaths_AreRejected()
    {
        Assert.False(PathSafetyValidator.IsSafeForCleanup(WinDir.ToLowerInvariant()));
        Assert.False(PathSafetyValidator.IsSafeForCleanup(WinDir.ToUpperInvariant()));
        Assert.False(PathSafetyValidator.IsSafeForCleanup(Path.Combine(Profile, "DOCUMENTS")));
    }

    [Fact]
    public void DotDotSegments_EscapingIntoProtectedAreas_AreRejected()
    {
        // "C:\Users\me\..\..\Windows\System32" canonicalizes into the protected subtree.
        Assert.False(PathSafetyValidator.IsSafeForCleanup(@"C:\Users\me\..\..\Windows\System32"));
    }

    [Fact]
    public void DriveLetters_NormalizesTokens()
    {
        Assert.Equal("C:", DriveLetters.Normalize("C:"));
        Assert.Equal("D:", DriveLetters.Normalize("d"));
        Assert.Equal("E:", DriveLetters.Normalize(" e:\\ "));
        Assert.Equal("C:", DriveLetters.Normalize(null));
        Assert.Equal("C:", DriveLetters.Normalize(""));
        Assert.Equal("C:", DriveLetters.Normalize("CDE"));
        Assert.Equal("C:", DriveLetters.Normalize("1:"));
        Assert.Equal("D:\\", DriveLetters.GetRootPath("d"));
    }

    [Fact]
    public void SafeCaches_StillPass_AfterHardening()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.True(PathSafetyValidator.IsSafeForCleanup(Path.Combine(localAppData, "npm-cache")));
        Assert.True(PathSafetyValidator.IsSafeForCleanup(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache", "Cache_Data")));
        Assert.True(PathSafetyValidator.IsSafeForCleanup(Path.GetTempPath()));
        Assert.True(PathSafetyValidator.IsSafeForCleanup(Path.Combine(localAppData, "pip", "cache")));
    }

    [Fact]
    public void UserProfileRoot_BlockedExactly_ButCachesUnderneath_RemainAllowed()
    {
        Assert.False(PathSafetyValidator.IsSafeForCleanup(Profile));
        Assert.False(PathSafetyValidator.IsSafeForCleanup(Path.Combine(Profile, "Documents")));

        string cache = Path.Combine(Profile, "AppData", "Local", "Temp");
        Assert.True(PathSafetyValidator.IsSafeForCleanup(cache));
    }

    private static void RunHidden(string fileName, string arguments)
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            p?.WaitForExit(5000);
        }
        catch { }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                // Remove junction first (deleting junction does not delete target).
                var di = new DirectoryInfo(path);
                di.Delete(recursive: false);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch { }
    }
}
