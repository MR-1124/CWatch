using CWatch.Core.Safety;
using Xunit;

namespace CWatch.Tests;

public class SecurityAndSafetyTests
{
    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files (x86)")]
    public void PathSafetyValidator_Blocks_CriticalSystemPaths(string criticalPath)
    {
        bool isCritical = PathSafetyValidator.IsCriticalSystemPath(criticalPath);
        Assert.True(isCritical, $"Path {criticalPath} must be identified as critical.");

        bool isSafe = PathSafetyValidator.IsSafeForCleanup(criticalPath);
        Assert.False(isSafe, $"Path {criticalPath} must NOT be considered safe for automated cleanup.");
    }

    [Fact]
    public void PathSafetyValidator_Blocks_UserProfileRootAndPersonalLibraries()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            Assert.True(PathSafetyValidator.IsCriticalSystemPath(userProfile));
            Assert.False(PathSafetyValidator.IsSafeForCleanup(userProfile));

            string desktop = Path.Combine(userProfile, "Desktop");
            Assert.True(PathSafetyValidator.IsCriticalSystemPath(desktop));
            Assert.False(PathSafetyValidator.IsSafeForCleanup(desktop));

            string documents = Path.Combine(userProfile, "Documents");
            Assert.True(PathSafetyValidator.IsCriticalSystemPath(documents));
            Assert.False(PathSafetyValidator.IsSafeForCleanup(documents));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"..\..\Windows")]
    [InlineData(@"relative\temp\folder")]
    public void PathSafetyValidator_Rejects_InvalidAndRelativePaths(string? invalidPath)
    {
        bool isSafe = PathSafetyValidator.IsSafeForCleanup(invalidPath);
        Assert.False(isSafe);
    }

    [Fact]
    public void PathSafetyValidator_Allows_SafeCacheAndTempDirectories()
    {
        string userTemp = Path.GetTempPath();
        Assert.True(PathSafetyValidator.IsSafeForCleanup(userTemp));

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string npmCache = Path.Combine(localAppData, "npm-cache");
        Assert.True(PathSafetyValidator.IsSafeForCleanup(npmCache));

        string chromeCache = Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache");
        Assert.True(PathSafetyValidator.IsSafeForCleanup(chromeCache));
    }

    [Fact]
    public void ZipSlip_Guard_Rejects_DirectoryTraversalPaths()
    {
        string installDir = @"C:\Users\TestUser\AppData\Local\Programs\CWatch";
        string canonicalInstallDir = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        string safeEntry = "CWatch.UI.exe";
        string safeDest = Path.GetFullPath(Path.Combine(installDir, safeEntry));
        Assert.StartsWith(canonicalInstallDir, safeDest, StringComparison.OrdinalIgnoreCase);

        string maliciousEntry = @"../../Windows/System32/malicious.dll";
        string maliciousDest = Path.GetFullPath(Path.Combine(installDir, maliciousEntry));
        Assert.False(maliciousDest.StartsWith(canonicalInstallDir, StringComparison.OrdinalIgnoreCase));
    }
}
