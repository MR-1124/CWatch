using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Core.Safety;

namespace CWatch.Cleanup.Providers;

public sealed class DevelopmentCacheCleanupProvider : ICleanupProvider
{
    public string ProviderId => "dev_caches";
    public string DisplayName => "Developer Package & Build Caches";
    public string CategoryName => "Development Tools";
    public bool IsAdvanced => false;

    public async Task<List<CleanupCandidate>> ScanCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var candidates = new List<CleanupCandidate>();
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // npm cache
            string npmCache = Path.Combine(localAppData, "npm-cache");
            AddCandidate(candidates, "npm Cache", npmCache, "Downloaded Node.js packages cached globally.",
                "npm keeps tarballs of downloaded packages.",
                "Subsequent 'npm install' commands will download packages directly from registry.",
                SafetyLevel.LowRisk);

            // pip cache
            string pipCache = Path.Combine(localAppData, "pip", "cache");
            AddCandidate(candidates, "Python pip Cache", pipCache, "Cached Python wheels and source distributions.",
                "pip stores downloaded Python packages for faster offline installs.",
                "Future 'pip install' runs will download needed packages afresh.",
                SafetyLevel.LowRisk);

            // Gradle cache
            string gradleCache = Path.Combine(userProfile, ".gradle", "caches");
            AddCandidate(candidates, "Gradle Build & Dependency Cache", gradleCache, "Downloaded JVM dependencies and compilation build caches.",
                "Gradle maintains downloaded libraries across Android/Java projects.",
                "Future Gradle builds will re-download required project dependencies once.",
                SafetyLevel.LowRisk);

            // NuGet global cache
            string nugetCache = Path.Combine(userProfile, ".nuget", "packages");
            AddCandidate(candidates, "NuGet Global Packages Cache", nugetCache, ".NET package cache.",
                "NuGet caches installed .NET library packages.",
                "Visual Studio / dotnet restore will re-download project dependencies when built.",
                SafetyLevel.LowRisk);

            // Yarn cache
            string yarnCache = Path.Combine(localAppData, "Yarn", "Cache");
            AddCandidate(candidates, "Yarn Cache", yarnCache, "Cached Yarn package archives.",
                "Yarn stores downloaded JS packages.",
                "Yarn will fetch packages on next install.",
                SafetyLevel.LowRisk);

            // pnpm store
            string pnpmStore = Path.Combine(localAppData, "pnpm", "store");
            AddCandidate(candidates, "pnpm Store", pnpmStore, "pnpm content-addressable store.",
                "pnpm stores shared dependency hardlinks.",
                "pnpm will rebuild store as projects are installed.",
                SafetyLevel.Review);

            // Rust Cargo cache
            string cargoCache = Path.Combine(userProfile, ".cargo", "registry", "cache");
            AddCandidate(candidates, "Rust Cargo Registry Cache", cargoCache, "Downloaded Rust crates cache.",
                "Cargo caches crate archive downloads.",
                "Cargo will re-fetch crates when building new projects.",
                SafetyLevel.LowRisk);

            return candidates;
        }, cancellationToken);
    }

    private void AddCandidate(List<CleanupCandidate> list, string title, string path, string desc, string reason, string whatHappen, SafetyLevel safety)
    {
        if (Directory.Exists(path) && PathSafetyValidator.IsSafeForCleanup(path))
        {
            long size = CalculateDirectorySize(path);
            if (size > 20 * 1024 * 1024) // > 20MB
            {
                list.Add(new CleanupCandidate
                {
                    ProviderId = ProviderId,
                    Title = title,
                    Description = desc,
                    Path = path,
                    SizeBytes = size,
                    Safety = safety,
                    Reason = reason,
                    WhatWillHappen = whatHappen,
                    WillRegenerate = true,
                    Category = StorageCategoryType.DevelopmentTools,
                    IsSelected = safety == SafetyLevel.Safe // only auto-select Safe items by default
                });
            }
        }
    }

    public async Task<CleanupResult> ExecuteCleanupAsync(
        IReadOnlyList<CleanupCandidate> candidates,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var result = new CleanupResult();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var enumOptions = new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            };

            foreach (var cand in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!PathSafetyValidator.IsSafeForCleanup(cand.Path))
                {
                    result.ErrorMessages.Add($"Security guard blocked unsafe deletion path: {cand.Path}");
                    continue;
                }

                progress?.Report($"Purging {cand.Title}...");

                if (Directory.Exists(cand.Path))
                {
                    var di = new DirectoryInfo(cand.Path);
                    foreach (var file in di.EnumerateFiles("*", enumOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            long len = file.Length;
                            file.Delete();
                            result.BytesCleaned += len;
                            result.ItemsCleanedCount++;
                        }
                        catch (Exception ex)
                        {
                            result.FailedItemsCount++;
                            result.ErrorMessages.Add($"Cannot remove {file.Name}: {ex.Message}");
                        }
                    }
                }
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            return result;
        }, cancellationToken);
    }

    private static long CalculateDirectorySize(string path)
    {
        try
        {
            var di = new DirectoryInfo(path);
            var opt = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return di.EnumerateFiles("*", opt).Sum(f => f.Length);
        }
        catch { return 0; }
    }
}

public sealed class ApplicationCacheCleanupProvider : ICleanupProvider
{
    public string ProviderId => "app_caches";
    public string DisplayName => "Application & Media Caches";
    public string CategoryName => "AppData";
    public bool IsAdvanced => false;

    public async Task<List<CleanupCandidate>> ScanCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var candidates = new List<CleanupCandidate>();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Spotify
            string spotifyStorage = Path.Combine(localAppData, "Spotify", "Data");
            if (!Directory.Exists(spotifyStorage)) spotifyStorage = Path.Combine(localAppData, "Spotify", "Storage");
            AddCandidate(candidates, "Spotify Offline Media Cache", spotifyStorage, "Downloaded song streams and playlist cache.",
                "Spotify caches songs locally to reduce network streaming bandwidth.",
                "Streaming songs will download them again on-demand.", SafetyLevel.Review);

            // Discord
            string discordCache = Path.Combine(appData, "discord", "Cache", "Cache_Data");
            AddCandidate(candidates, "Discord Media Cache", discordCache, "Cached avatars, images, and attachments.",
                "Discord caches voice avatars and posted media.",
                "Media will re-download as conversations are viewed.", SafetyLevel.LowRisk);

            // Slack
            string slackCache = Path.Combine(appData, "Slack", "Cache", "Cache_Data");
            AddCandidate(candidates, "Slack Media Cache", slackCache, "Cached Slack files, thumbnails, and profile photos.",
                "Slack caches team messages and workspace files.",
                "Slack will fetch content again as needed.", SafetyLevel.LowRisk);

            // VS Code Cache
            string vscodeCache = Path.Combine(appData, "Code", "Cache", "Cache_Data");
            AddCandidate(candidates, "Visual Studio Code Cache", vscodeCache, "Cached editor resources, extension icons, and markdown previews.",
                "VS Code caches web assets for editor panels.",
                "Editor will regenerate cache automatically.", SafetyLevel.Safe);

            // Windows Thumbnail Cache
            string thumbCache = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
            if (Directory.Exists(thumbCache))
            {
                long size = CalculateThumbCacheSize(thumbCache);
                if (size > 15 * 1024 * 1024)
                {
                    candidates.Add(new CleanupCandidate
                    {
                        ProviderId = ProviderId,
                        Title = "Windows Thumbnail Cache",
                        Description = "Cached image and video thumbnail previews generated by File Explorer.",
                        Path = thumbCache,
                        SizeBytes = size,
                        Safety = SafetyLevel.Safe,
                        Reason = "Windows stores thumbnail previews in thumbcache_*.db databases.",
                        WhatWillHappen = "Deletes thumbnail databases. Windows Explorer will regenerate previews automatically when browsing folders.",
                        WillRegenerate = true,
                        Category = StorageCategoryType.AppData,
                        IsSelected = true
                    });
                }
            }

            return candidates;
        }, cancellationToken);
    }

    private void AddCandidate(List<CleanupCandidate> list, string title, string path, string desc, string reason, string whatHappen, SafetyLevel safety)
    {
        if (Directory.Exists(path) && PathSafetyValidator.IsSafeForCleanup(path))
        {
            long size = CalculateDirectorySize(path);
            if (size > 15 * 1024 * 1024)
            {
                list.Add(new CleanupCandidate
                {
                    ProviderId = ProviderId,
                    Title = title,
                    Description = desc,
                    Path = path,
                    SizeBytes = size,
                    Safety = safety,
                    Reason = reason,
                    WhatWillHappen = whatHappen,
                    WillRegenerate = true,
                    Category = StorageCategoryType.AppData,
                    IsSelected = safety == SafetyLevel.Safe
                });
            }
        }
    }

    public async Task<CleanupResult> ExecuteCleanupAsync(
        IReadOnlyList<CleanupCandidate> candidates,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var result = new CleanupResult();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var enumOptions = new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            };

            foreach (var cand in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!PathSafetyValidator.IsSafeForCleanup(cand.Path))
                {
                    result.ErrorMessages.Add($"Security guard blocked unsafe deletion path: {cand.Path}");
                    continue;
                }

                progress?.Report($"Purging {cand.Title}...");

                if (cand.Title.Contains("Thumbnail"))
                {
                    PurgeThumbCache(cand.Path, result);
                    continue;
                }

                if (Directory.Exists(cand.Path))
                {
                    var di = new DirectoryInfo(cand.Path);
                    foreach (var file in di.EnumerateFiles("*", enumOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            long len = file.Length;
                            file.Delete();
                            result.BytesCleaned += len;
                            result.ItemsCleanedCount++;
                        }
                        catch (Exception ex)
                        {
                            result.FailedItemsCount++;
                            result.ErrorMessages.Add($"Could not delete {file.Name}: {ex.Message}");
                        }
                    }
                }
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            return result;
        }, cancellationToken);
    }

    private static void PurgeThumbCache(string explorerPath, CleanupResult result)
    {
        try
        {
            var di = new DirectoryInfo(explorerPath);
            foreach (var file in di.EnumerateFiles("thumbcache_*.db"))
            {
                try
                {
                    long len = file.Length;
                    file.Delete();
                    result.BytesCleaned += len;
                    result.ItemsCleanedCount++;
                }
                catch { }
            }
        }
        catch { }
    }

    private static long CalculateThumbCacheSize(string path)
    {
        try
        {
            var di = new DirectoryInfo(path);
            return di.EnumerateFiles("thumbcache_*.db").Sum(f => f.Length);
        }
        catch { return 0; }
    }

    private static long CalculateDirectorySize(string path)
    {
        try
        {
            var di = new DirectoryInfo(path);
            var opt = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return di.EnumerateFiles("*", opt).Sum(f => f.Length);
        }
        catch { return 0; }
    }
}
