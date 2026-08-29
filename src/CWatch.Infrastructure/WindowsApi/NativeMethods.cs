using System.Diagnostics;
using System.Runtime.InteropServices;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;

namespace CWatch.Infrastructure.WindowsApi;

public static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [Flags]
    public enum RecycleFlags : uint
    {
        SHERB_NOCONFIRMATION = 0x00000001,
        SHERB_NOPROGRESSUI = 0x00000002,
        SHERB_NOSOUND = 0x00000004
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, RecycleFlags dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern int SHObjectProperties(IntPtr hwnd, uint shopObjectType, [MarshalAs(UnmanagedType.LPWStr)] string pszObjectName, [MarshalAs(UnmanagedType.LPWStr)] string? pszPropertyPage);

    public const uint SHOP_FILEPATH = 0x00000002;

    // Restart Manager APIs for identifying locked files
    [StructLayout(LayoutKind.Sequential)]
    public struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    public const int CCH_RM_MAX_APP_NAME = 255;
    public const int CCH_RM_MAX_SVC_NAME = 63;

    public enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string strServiceShortName;
        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmRegisterResources(
        uint dwSessionHandle,
        uint nFiles,
        string[]? rgsFilenames,
        uint nApplications,
        [In] RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
        out uint lpdwRebootReasons);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmEndSession(uint dwSessionHandle);

    public static void ShowFileProperties(string path)
    {
        try
        {
            SHObjectProperties(IntPtr.Zero, SHOP_FILEPATH, path, null);
        }
        catch
        {
            // fallback
        }
    }

    public static void OpenInExplorer(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignore failure
        }
    }

    public static void OpenItem(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback to opening containing folder
            OpenInExplorer(path);
        }
    }
}

public sealed class ProcessInspector : IProcessInspector
{
    private readonly ILoggerService? _logger;

    public ProcessInspector(ILoggerService? logger = null)
    {
        _logger = logger;
    }

    public List<LockedFileInfo> FindLockingProcesses(string filePath)
    {
        var results = new List<LockedFileInfo>();

        if (!File.Exists(filePath))
        {
            return results;
        }

        string sessionKey = Guid.NewGuid().ToString();
        int res = NativeMethods.RmStartSession(out uint sessionHandle, 0, sessionKey);
        if (res != 0) return results;

        try
        {
            string[] resources = [filePath];
            res = NativeMethods.RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, null, 0, null);
            if (res != 0) return results;

            uint pnProcInfoNeeded = 0;
            uint pnProcInfo = 0;
            uint lpdwRebootReasons = 0;

            res = NativeMethods.RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null, out lpdwRebootReasons);
            if (res == 234) // ERROR_MORE_DATA
            {
                var processInfo = new NativeMethods.RM_PROCESS_INFO[pnProcInfoNeeded];
                pnProcInfo = pnProcInfoNeeded;
                res = NativeMethods.RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, out lpdwRebootReasons);
                if (res == 0)
                {
                    for (int i = 0; i < pnProcInfo; i++)
                    {
                        var info = processInfo[i];
                        string procName = info.strAppName;
                        if (string.IsNullOrWhiteSpace(procName))
                        {
                            try
                            {
                                var p = Process.GetProcessById(info.Process.dwProcessId);
                                procName = p.ProcessName;
                            }
                            catch
                            {
                                procName = "Unknown Process";
                            }
                        }

                        results.Add(new LockedFileInfo
                        {
                            FilePath = filePath,
                            LockingProcessName = procName,
                            ProcessId = info.Process.dwProcessId,
                            Description = $"In use by {procName} (PID {info.Process.dwProcessId}). Close the program to unlock."
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error querying locked file process for: {filePath}", ex);
        }
        finally
        {
            NativeMethods.RmEndSession(sessionHandle);
        }

        return results;
    }
}
