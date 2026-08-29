using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Core.Safety;
using CWatch.Infrastructure.WindowsApi;

namespace CWatch.Cleanup.Providers;

public sealed class RecycleBinCleanupProvider : ICleanupProvider
{
    public string ProviderId => "recycle_bin";
    public string DisplayName => "Windows Recycle Bin";
    public string CategoryName => "Recycle Bin";
    public bool IsAdvanced => false;

    public async Task<List<CleanupCandidate>> ScanCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var candidates = new List<CleanupCandidate>();
            var rbInfo = new NativeMethods.SHQUERYRBINFO
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.SHQUERYRBINFO>()
            };

            int hr = NativeMethods.SHQueryRecycleBin("C:\\", ref rbInfo);
            if (hr == 0 && rbInfo.i64Size > 0)
            {
                candidates.Add(new CleanupCandidate
                {
                    ProviderId = ProviderId,
                    Title = "Recycle Bin (C: Drive)",
                    Description = $"{rbInfo.i64NumItems:N0} deleted file(s) currently held in the Recycle Bin.",
                    Path = @"C:\$Recycle.Bin",
                    SizeBytes = rbInfo.i64Size,
                    Safety = SafetyLevel.Safe,
                    Reason = "Files and folders you previously deleted that are stored in the Recycle Bin.",
                    WhatWillHappen = "Permanently deletes all items in the Recycle Bin, freeing immediate disk space.",
                    WillRegenerate = false,
                    Category = StorageCategoryType.RecycleBin
                });
            }

            return candidates;
        }, cancellationToken);
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

            progress?.Report("Emptying Recycle Bin...");
            long totalBytes = candidates.Sum(c => c.SizeBytes);

            try
            {
                int hr = NativeMethods.SHEmptyRecycleBin(
                    IntPtr.Zero,
                    "C:\\",
                    NativeMethods.RecycleFlags.SHERB_NOCONFIRMATION |
                    NativeMethods.RecycleFlags.SHERB_NOPROGRESSUI |
                    NativeMethods.RecycleFlags.SHERB_NOSOUND);

                if (hr == 0)
                {
                    result.BytesCleaned = totalBytes;
                    result.ItemsCleanedCount = 1;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessages.Add($"Recycle Bin API returned error code 0x{hr:X8}");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessages.Add(ex.Message);
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            return result;
        }, cancellationToken);
    }
}

public sealed class BrowserCacheCleanupProvider : ICleanupProvider
{
    public string ProviderId => "browser_cache";
    public string DisplayName => "Web Browser Caches";
    public string CategoryName => "Browser Data";
    public bool IsAdvanced => false;

    public async Task<List<CleanupCandidate>> ScanCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var candidates = new List<CleanupCandidate>();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Chrome
            string chromeCache = Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache", "Cache_Data");
            AddIfValid(candidates, "Google Chrome Cache", chromeCache, "Cached web pages, images, and scripts from Google Chrome.");

            // Edge
            string edgeCache = Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache", "Cache_Data");
            AddIfValid(candidates, "Microsoft Edge Cache", edgeCache, "Cached web media and resources from Microsoft Edge.");

            // Brave
            string braveCache = Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache", "Cache_Data");
            AddIfValid(candidates, "Brave Browser Cache", braveCache, "Cached browsing assets from Brave Browser.");

            // Firefox
            string firefoxDir = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(firefoxDir))
            {
                foreach (var profile in Directory.GetDirectories(firefoxDir))
                {
                    string ffCache = Path.Combine(profile, "cache2", "entries");
                    AddIfValid(candidates, $"Firefox Cache ({Path.GetFileName(profile)})", ffCache, "Cached web assets from Mozilla Firefox.");
                }
            }

            return candidates;
        }, cancellationToken);
    }

    private void AddIfValid(List<CleanupCandidate> list, string title, string path, string desc)
    {
        if (Directory.Exists(path) && PathSafetyValidator.IsSafeForCleanup(path))
        {
            long size = CalculateDirectorySize(path);
            if (size > 10 * 1024 * 1024) // > 10 MB
            {
                list.Add(new CleanupCandidate
                {
                    ProviderId = ProviderId,
                    Title = title,
                    Description = desc,
                    Path = path,
                    SizeBytes = size,
                    Safety = SafetyLevel.Safe,
                    Reason = "Web browsers store images and scripts to load visited sites faster. These can be safely purged.",
                    WhatWillHappen = "Deletes downloaded browser web cache. Browsers will re-download fresh assets when visiting websites.",
                    WillRegenerate = true,
                    Category = StorageCategoryType.BrowserData
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

                progress?.Report($"Cleaning {cand.Title}...");

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
                            result.ErrorMessages.Add($"In use or locked: {file.Name} ({ex.Message})");
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
