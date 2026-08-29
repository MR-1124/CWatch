namespace CWatch.Core.Enums;

/// <summary>
/// Broad domain categorization of disk storage.
/// </summary>
public enum StorageCategoryType
{
    WindowsSystem,
    InstalledApps,
    UserFiles,
    Downloads,
    Documents,
    Pictures,
    Videos,
    Desktop,
    AppData,
    ProgramData,
    TemporaryFiles,
    BrowserData,
    DevelopmentTools,
    VirtualMachinesEmulators,
    RecycleBin,
    Other
}

public enum GrowthTrend
{
    Stable,
    ModerateGrowth,
    RapidGrowth,
    CriticalExhaustion,
    SpaceFreed
}

public enum ScanStatus
{
    Idle,
    Scanning,
    Paused,
    Completed,
    Cancelled,
    Failed
}
