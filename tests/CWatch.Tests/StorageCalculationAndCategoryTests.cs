using CWatch.Analysis.Classifiers;
using CWatch.Core.Enums;
using CWatch.Core.Models;
using Xunit;

namespace CWatch.Tests;

public class StorageCalculationTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.0 GB")]
    [InlineData(1099511627776, "1.0 TB")]
    public void ByteSizeFormatter_FormatsCorrectly(long bytes, string expected)
    {
        string result = ByteSizeFormatter.Format(bytes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ByteSizeFormatter_FormatsDeltas()
    {
        Assert.Equal("+1.5 GB", ByteSizeFormatter.FormatDelta((long)(1.5 * 1024 * 1024 * 1024)));
        Assert.Equal("-500.0 MB", ByteSizeFormatter.FormatDelta((long)(-500.0 * 1024 * 1024)));
        Assert.Equal("No change", ByteSizeFormatter.FormatDelta(0));
    }

    [Fact]
    public void DriveStatus_EvaluatesThresholdsCorrectly()
    {
        var status = new DriveStatus
        {
            TotalBytes = 500L * 1024 * 1024 * 1024,
            FreeBytes = 8L * 1024 * 1024 * 1024 // 8 GB free
        };

        Assert.True(status.IsCriticallyLow()); // default threshold is 10 GB
        Assert.True(status.IsWarningLow());

        status.FreeBytes = 35L * 1024 * 1024 * 1024; // 35 GB free
        Assert.False(status.IsCriticallyLow());
        Assert.False(status.IsWarningLow());
    }
}

public class CategoryClassificationTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32\cmd.exe", false, StorageCategoryType.WindowsSystem)]
    [InlineData(@"C:\Windows\Temp\sample.tmp", false, StorageCategoryType.TemporaryFiles)]
    [InlineData(@"C:\Users\Alice\AppData\Local\Temp\log.txt", false, StorageCategoryType.TemporaryFiles)]
    [InlineData(@"C:\Users\Alice\Downloads\installer.exe", false, StorageCategoryType.Downloads)]
    [InlineData(@"C:\Users\Alice\Documents\report.pdf", false, StorageCategoryType.Documents)]
    [InlineData(@"C:\Users\Alice\Pictures\vacation.jpg", false, StorageCategoryType.Pictures)]
    [InlineData(@"C:\Users\Alice\Videos\stream.mp4", false, StorageCategoryType.Videos)]
    [InlineData(@"C:\Users\Alice\.nuget\packages\newtonsoft.json\13.0.1", true, StorageCategoryType.DevelopmentTools)]
    [InlineData(@"C:\Users\Alice\.gradle\caches\modules-2", true, StorageCategoryType.DevelopmentTools)]
    [InlineData(@"C:\Users\Alice\project\node_modules\react", true, StorageCategoryType.DevelopmentTools)]
    [InlineData(@"C:\Users\Alice\AppData\Local\Google\Chrome\User Data\Default\Cache", true, StorageCategoryType.BrowserData)]
    [InlineData(@"C:\Users\Alice\vm\ubuntu.vhdx", false, StorageCategoryType.VirtualMachinesEmulators)]
    [InlineData(@"C:\$Recycle.Bin\S-1-5-21\sample.bin", false, StorageCategoryType.RecycleBin)]
    [InlineData(@"C:\Program Files\Adobe\Photoshop.exe", false, StorageCategoryType.InstalledApps)]
    public void ClassifiesKnownPathsAccurately(string path, bool isDir, StorageCategoryType expected)
    {
        var category = CategoryClassifier.Classify(path, isDir);
        Assert.Equal(expected, category);
    }
}
