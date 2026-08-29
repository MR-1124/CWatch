using CWatch.Core.Enums;

namespace CWatch.Analysis.Classifiers;

public static class CategoryClassifier
{
    private static readonly HashSet<string> DevFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nuget", ".gradle", ".m2", ".vscode", ".cargo", ".rustup", ".docker", ".android",
        "node_modules", "npm-cache", "yarn-cache", "pnpm-store", "pip", "PyPI", "wheels",
        ".dotnet", "packages", "bin", "obj", ".git", ".vs"
    };

    private static readonly HashSet<string> DevFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".pdb", ".nuget", ".nupkg", ".jar", ".aar", ".pyc", ".whl", ".tgz", ".map"
    };

    private static readonly HashSet<string> VmFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vhdx", ".vhd", ".vmdk", ".vdi", ".iso", ".img", ".qcow2"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v"
    };

    private static readonly HashSet<string> PictureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".tiff", ".raw", ".psd"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".txt", ".md", ".csv", ".rtf", ".epub"
    };

    public static StorageCategoryType Classify(string fullPath, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return StorageCategoryType.Other;

        string normalized = fullPath.Replace('/', '\\');

        // Recycle Bin
        if (normalized.Contains(@"\$Recycle.Bin", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.RecycleBin;
        }

        // Windows System Files
        if (normalized.StartsWith(@"C:\Windows", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(@"C:\$WinREAgent", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(@"C:\System Volume Information", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(@"C:\Recovery", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(@"\hiberfil.sys", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(@"\pagefile.sys", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(@"\swapfile.sys", StringComparison.OrdinalIgnoreCase))
        {
            // Windows Temp sub-classification
            if (normalized.Contains(@"\Windows\Temp", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(@"\SoftwareDistribution\Download", StringComparison.OrdinalIgnoreCase))
            {
                return StorageCategoryType.TemporaryFiles;
            }
            return StorageCategoryType.WindowsSystem;
        }

        // Temporary Files
        if (normalized.Contains(@"\AppData\Local\Temp", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(@"\Temp", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.TemporaryFiles;
        }

        // Browser Data
        if (normalized.Contains(@"\AppData\Local\Google\Chrome\User Data", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Local\Microsoft\Edge\User Data", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Roaming\Mozilla\Firefox", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Local\Mozilla\Firefox", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Local\BraveSoftware\Brave-Browser", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Roaming\Opera Software", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.BrowserData;
        }

        // Virtual Machines / Container / Emulator Disks
        string ext = Path.GetExtension(fullPath);
        if (VmFileExtensions.Contains(ext) ||
            normalized.Contains(@"\.android\avd", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\VirtualBox VMs", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\Hyper-V", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\DockerDesktop", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\wsl\docker-desktop", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.VirtualMachinesEmulators;
        }

        // Development Tools & Dependencies
        string fileName = Path.GetFileName(normalized);
        if (DevFolderNames.Contains(fileName) ||
            normalized.Contains(@"\node_modules", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\.nuget\packages", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\.gradle\caches", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\.m2\repository", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Local\pip\cache", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Local\npm-cache", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Local\pnpm", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Local\Yarn\Cache", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Local\Android\Sdk", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\Android\Sdk", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\.cargo\registry", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\.rustup\toolchains", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.DevelopmentTools;
        }

        // Installed Applications
        if (normalized.StartsWith(@"C:\Program Files (x86)", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(@"C:\Program Files", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\AppData\Local\Programs", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(@"\ProgramData\Microsoft\Windows\AppRepository", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.InstalledApps;
        }

        // ProgramData (general)
        if (normalized.StartsWith(@"C:\ProgramData", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.ProgramData;
        }

        // User Folders & Categories
        if (normalized.Contains(@"\Downloads\", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(@"\Downloads", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.Downloads;
        }
        if (normalized.Contains(@"\Documents\", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(@"\Documents", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.Documents;
        }
        if (normalized.Contains(@"\Pictures\", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(@"\Pictures", StringComparison.OrdinalIgnoreCase) || PictureExtensions.Contains(ext))
        {
            return StorageCategoryType.Pictures;
        }
        if (normalized.Contains(@"\Videos\", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(@"\Videos", StringComparison.OrdinalIgnoreCase) || VideoExtensions.Contains(ext))
        {
            return StorageCategoryType.Videos;
        }
        if (normalized.Contains(@"\Desktop\", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith(@"\Desktop", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.Desktop;
        }
        if (normalized.Contains(@"\AppData\", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.AppData;
        }
        if (normalized.StartsWith(@"C:\Users\", StringComparison.OrdinalIgnoreCase))
        {
            return StorageCategoryType.UserFiles;
        }

        return StorageCategoryType.Other;
    }

    public static string GetCategoryDisplayName(StorageCategoryType type) => type switch
    {
        StorageCategoryType.WindowsSystem => "Windows / System",
        StorageCategoryType.InstalledApps => "Installed Applications",
        StorageCategoryType.UserFiles => "User Files",
        StorageCategoryType.Downloads => "Downloads",
        StorageCategoryType.Documents => "Documents",
        StorageCategoryType.Pictures => "Pictures",
        StorageCategoryType.Videos => "Videos",
        StorageCategoryType.Desktop => "Desktop",
        StorageCategoryType.AppData => "AppData",
        StorageCategoryType.ProgramData => "ProgramData",
        StorageCategoryType.TemporaryFiles => "Temporary Files",
        StorageCategoryType.BrowserData => "Browser Data",
        StorageCategoryType.DevelopmentTools => "Development Tools",
        StorageCategoryType.VirtualMachinesEmulators => "Virtual Machines & Containers",
        StorageCategoryType.RecycleBin => "Recycle Bin",
        _ => "Other"
    };

    public static string GetCategoryColorHex(StorageCategoryType type) => type switch
    {
        StorageCategoryType.WindowsSystem => "#6366F1",           // Indigo
        StorageCategoryType.InstalledApps => "#3B82F6",           // Blue
        StorageCategoryType.UserFiles => "#10B981",               // Emerald Green
        StorageCategoryType.Downloads => "#F59E0B",               // Amber
        StorageCategoryType.Documents => "#06B6D4",               // Cyan
        StorageCategoryType.Pictures => "#EC4899",                // Pink
        StorageCategoryType.Videos => "#8B5CF6",                  // Violet
        StorageCategoryType.Desktop => "#14B8A6",                 // Teal
        StorageCategoryType.AppData => "#F97316",                 // Orange
        StorageCategoryType.ProgramData => "#64748B",             // Slate
        StorageCategoryType.TemporaryFiles => "#EF4444",          // Red
        StorageCategoryType.BrowserData => "#0284C7",             // Light Blue
        StorageCategoryType.DevelopmentTools => "#84CC16",        // Lime Green
        StorageCategoryType.VirtualMachinesEmulators => "#D946EF", // Fuchsia
        StorageCategoryType.RecycleBin => "#94A3B8",              // Gray
        _ => "#A855F7"                                            // Purple
    };

    public static string GetCategoryIcon(StorageCategoryType type) => type switch
    {
        StorageCategoryType.WindowsSystem => "🖥️",
        StorageCategoryType.InstalledApps => "📦",
        StorageCategoryType.UserFiles => "👤",
        StorageCategoryType.Downloads => "📥",
        StorageCategoryType.Documents => "📄",
        StorageCategoryType.Pictures => "🖼️",
        StorageCategoryType.Videos => "🎬",
        StorageCategoryType.Desktop => "🖥️",
        StorageCategoryType.AppData => "📁",
        StorageCategoryType.ProgramData => "🗄️",
        StorageCategoryType.TemporaryFiles => "🧹",
        StorageCategoryType.BrowserData => "🌐",
        StorageCategoryType.DevelopmentTools => "⚡",
        StorageCategoryType.VirtualMachinesEmulators => "💾",
        StorageCategoryType.RecycleBin => "🗑️",
        _ => "📁"
    };
}
