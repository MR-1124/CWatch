using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Infrastructure.WindowsApi;

namespace CWatch.UI.ViewModels;

public sealed class LargestFilesViewModel : ViewModelBase
{
    private readonly IFileSystemScanner _scanner;
    private bool _isLoading;
    private string _searchFilter = string.Empty;
    private string _selectedTypeFilter = "All";
    private List<StorageItem> _allLargestFiles = [];
    private StorageItem? _selectedFile;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public StorageItem? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public ObservableCollection<StorageItem> DisplayedFiles { get; } = [];

    public ICommand OpenFileCommand { get; }
    public ICommand ShowInExplorerCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }
    public ICommand SetTypeFilterCommand { get; }

    public LargestFilesViewModel(IFileSystemScanner scanner)
    {
        _scanner = scanner;

        OpenFileCommand = new RelayCommand(param =>
        {
            var item = param as StorageItem ?? SelectedFile;
            if (item != null) NativeMethods.OpenItem(item.FullPath);
        });

        ShowInExplorerCommand = new RelayCommand(param =>
        {
            var item = param as StorageItem ?? SelectedFile;
            if (item != null) NativeMethods.OpenInExplorer(item.FullPath);
        });

        CopyPathCommand = new RelayCommand(param =>
        {
            var item = param as StorageItem ?? SelectedFile;
            if (item != null) System.Windows.Clipboard.SetText(item.FullPath);
        });

        ShowPropertiesCommand = new RelayCommand(param =>
        {
            var item = param as StorageItem ?? SelectedFile;
            if (item != null) NativeMethods.ShowFileProperties(item.FullPath);
        });

        SetTypeFilterCommand = new RelayCommand(param =>
        {
            if (param is string filter) SelectedTypeFilter = filter;
        });
    }

    public void SetLargestFiles(List<StorageItem> files)
    {
        _allLargestFiles = files;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        DisplayedFiles.Clear();
        var files = _allLargestFiles.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            files = files.Where(f => f.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                     f.FullPath.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                     (f.Extension != null && f.Extension.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)));
        }

        if (SelectedTypeFilter != "All")
        {
            files = SelectedTypeFilter switch
            {
                "Media" => files.Where(f => IsMediaExtension(f.Extension)),
                "Archives" => files.Where(f => IsArchiveExtension(f.Extension)),
                "VirtualDisks" => files.Where(f => IsVirtualDiskExtension(f.Extension)),
                "Executables" => files.Where(f => IsExecutableExtension(f.Extension)),
                _ => files
            };
        }

        foreach (var file in files.OrderByDescending(f => f.SizeBytes))
        {
            DisplayedFiles.Add(file);
        }

        if (SelectedFile == null || !DisplayedFiles.Contains(SelectedFile))
        {
            SelectedFile = DisplayedFiles.FirstOrDefault();
        }
    }

    private static bool IsMediaExtension(string? ext) =>
        ext is ".mp4" or ".mkv" or ".mov" or ".avi" or ".wmv" or ".mp3" or ".wav" or ".flac" or ".png" or ".jpg" or ".jpeg" or ".raw" or ".psd";

    private static bool IsArchiveExtension(string? ext) =>
        ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".iso" or ".cab" or ".bz2" or ".xz";

    private static bool IsVirtualDiskExtension(string? ext) =>
        ext is ".vmdk" or ".vhdx" or ".vhd" or ".vdi" or ".qcow2" or ".img";

    private static bool IsExecutableExtension(string? ext) =>
        ext is ".exe" or ".msi" or ".dll" or ".sys" or ".appx" or ".msix";
}

public sealed class DuplicateGroupItem : ViewModelBase
{
    public long FileSizeBytes { get; set; }
    public string FormattedSize { get => ByteSizeFormatter.Format(FileSizeBytes); set { } }
    public ObservableCollection<DuplicateFileEntry> Files { get; } = [];
}

public sealed class DuplicateFileEntry : ViewModelBase
{
    private bool _isSelectedForDeletion;

    public StorageItem StorageItem { get; set; } = new();
    public string FullPath => StorageItem.FullPath;
    public string Name => StorageItem.Name;
    public DateTime? LastModifiedUtc => StorageItem.LastModifiedUtc;
    public string FormattedSize { get => StorageItem.DisplaySize; set { } }

    public bool IsSelectedForDeletion
    {
        get => _isSelectedForDeletion;
        set => SetProperty(ref _isSelectedForDeletion, value);
    }
}

public sealed class DuplicatesViewModel : ViewModelBase
{
    private readonly IFileSystemScanner _scanner;
    private bool _isScanning;
    private string _statusMessage = "Click 'Scan Duplicates' to discover identical files across user folders.";
    private long _totalWastedBytes;

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public long TotalWastedBytes
    {
        get => _totalWastedBytes;
        set => SetProperty(ref _totalWastedBytes, value);
    }

    public string FormattedWastedBytes { get => ByteSizeFormatter.Format(TotalWastedBytes); set { } }

    public ObservableCollection<DuplicateGroupItem> DuplicateGroups { get; } = [];

    public ICommand StartScanCommand { get; }
    public ICommand DeleteSelectedDuplicatesCommand { get; }
    public ICommand ShowInExplorerCommand { get; }
    public ICommand KeepNewestCopiesCommand { get; }
    public ICommand KeepOldestCopiesCommand { get; }
    public ICommand SelectAllDuplicatesCommand { get; }
    public ICommand DeselectAllDuplicatesCommand { get; }

    public DuplicatesViewModel(IFileSystemScanner scanner)
    {
        _scanner = scanner;

        StartScanCommand = new AsyncRelayCommand(ScanDuplicatesAsync, () => !IsScanning);
        DeleteSelectedDuplicatesCommand = new RelayCommand(DeleteSelected, () => DuplicateGroups.Count > 0);
        ShowInExplorerCommand = new RelayCommand(param =>
        {
            if (param is DuplicateFileEntry entry) NativeMethods.OpenInExplorer(entry.FullPath);
        });

        KeepNewestCopiesCommand = new RelayCommand(() =>
        {
            foreach (var grp in DuplicateGroups)
            {
                var newest = grp.Files.OrderByDescending(f => f.LastModifiedUtc ?? DateTime.MinValue).FirstOrDefault();
                foreach (var file in grp.Files)
                {
                    file.IsSelectedForDeletion = file != newest;
                }
            }
            RecalculateWasted();
        });

        KeepOldestCopiesCommand = new RelayCommand(() =>
        {
            foreach (var grp in DuplicateGroups)
            {
                var oldest = grp.Files.OrderBy(f => f.LastModifiedUtc ?? DateTime.MaxValue).FirstOrDefault();
                foreach (var file in grp.Files)
                {
                    file.IsSelectedForDeletion = file != oldest;
                }
            }
            RecalculateWasted();
        });

        SelectAllDuplicatesCommand = new RelayCommand(() =>
        {
            foreach (var grp in DuplicateGroups)
            {
                bool first = true;
                foreach (var file in grp.Files)
                {
                    file.IsSelectedForDeletion = !first;
                    first = false;
                }
            }
            RecalculateWasted();
        });

        DeselectAllDuplicatesCommand = new RelayCommand(() =>
        {
            foreach (var grp in DuplicateGroups)
            {
                foreach (var file in grp.Files) file.IsSelectedForDeletion = false;
            }
            RecalculateWasted();
        });
    }

    public void RecalculateWasted()
    {
        long wasted = 0;
        foreach (var grp in DuplicateGroups)
        {
            foreach (var file in grp.Files.Where(f => f.IsSelectedForDeletion))
            {
                wasted += file.StorageItem.SizeBytes;
            }
        }
        TotalWastedBytes = wasted;
        OnPropertyChanged(nameof(FormattedWastedBytes));
    }

    private async Task ScanDuplicatesAsync()
    {
        IsScanning = true;
        StatusMessage = "Analyzing file signatures and computing SHA-256 byte hashes...";
        DuplicateGroups.Clear();
        TotalWastedBytes = 0;

        try
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var results = await _scanner.FindDuplicateFilesAsync(userProfile);

            long wasted = 0;
            foreach (var group in results)
            {
                if (group.Count < 2) continue;

                var grp = new DuplicateGroupItem { FileSizeBytes = group[0].SizeBytes };
                bool first = true;
                foreach (var item in group)
                {
                    grp.Files.Add(new DuplicateFileEntry
                    {
                        StorageItem = item,
                        IsSelectedForDeletion = !first
                    });
                    if (!first) wasted += item.SizeBytes;
                    first = false;
                }
                DuplicateGroups.Add(grp);
            }

            TotalWastedBytes = wasted;
            StatusMessage = DuplicateGroups.Count > 0
                ? $"Found {DuplicateGroups.Count} duplicate groups ({ByteSizeFormatter.Format(wasted)} reclaimable)."
                : "No duplicate files found in scanned directories. Everything is clean!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void DeleteSelected()
    {
        int deletedCount = 0;
        long freedBytes = 0;

        foreach (var grp in DuplicateGroups.ToList())
        {
            foreach (var file in grp.Files.Where(f => f.IsSelectedForDeletion).ToList())
            {
                try
                {
                    if (File.Exists(file.FullPath))
                    {
                        long size = file.StorageItem.SizeBytes;
                        bool deleted = NativeMethods.SendToRecycleBin(file.FullPath);
                        if (deleted)
                        {
                            grp.Files.Remove(file);
                            freedBytes += size;
                            deletedCount++;
                        }
                    }
                }
                catch { }
            }

            if (grp.Files.Count <= 1)
            {
                DuplicateGroups.Remove(grp);
            }
        }

        TotalWastedBytes = Math.Max(0, TotalWastedBytes - freedBytes);
        StatusMessage = $"Successfully deleted {deletedCount} duplicate file(s) and reclaimed {ByteSizeFormatter.Format(freedBytes)}.";
    }
}
