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
    private string _selectedMinSize = "All"; // All, >10GB, >1GB, >500MB, >100MB
    private string _sortBy = "SizeDesc"; // SizeDesc, DateDesc, NameAsc
    private List<StorageItem> _allLargestFiles = [];
    private StorageItem? _selectedFile;
    private string _statusMessage = "Index loaded.";

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

    public string SelectedMinSize
    {
        get => _selectedMinSize;
        set
        {
            if (SetProperty(ref _selectedMinSize, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SortBy
    {
        get => _sortBy;
        set
        {
            if (SetProperty(ref _sortBy, value))
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

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<StorageItem> DisplayedFiles { get; } = [];

    public ICommand OpenFileCommand { get; }
    public ICommand ShowInExplorerCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }
    public ICommand DeleteSelectedFileCommand { get; }
    public ICommand SetTypeFilterCommand { get; }
    public ICommand SetMinSizeFilterCommand { get; }
    public ICommand SetSortCommand { get; }
    public ICommand ClearSearchCommand { get; }

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

        DeleteSelectedFileCommand = new RelayCommand(param =>
        {
            var item = param as StorageItem ?? SelectedFile;
            if (item != null && File.Exists(item.FullPath))
            {
                bool deleted = NativeMethods.SendToRecycleBin(item.FullPath);
                if (deleted)
                {
                    _allLargestFiles.Remove(item);
                    DisplayedFiles.Remove(item);
                    StatusMessage = $"Moved '{item.Name}' ({item.DisplaySize}) to Recycle Bin.";
                    SelectedFile = DisplayedFiles.FirstOrDefault();
                }
            }
        });

        SetTypeFilterCommand = new RelayCommand(param =>
        {
            if (param is string filter) SelectedTypeFilter = filter;
        });

        SetMinSizeFilterCommand = new RelayCommand(param =>
        {
            if (param is string size) SelectedMinSize = size;
        });

        SetSortCommand = new RelayCommand(param =>
        {
            if (param is string sort) SortBy = sort;
        });

        ClearSearchCommand = new RelayCommand(() => SearchFilter = string.Empty);
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

        if (SelectedMinSize != "All")
        {
            long minBytes = SelectedMinSize switch
            {
                ">10GB" => 10L * 1024 * 1024 * 1024,
                ">1GB" => 1L * 1024 * 1024 * 1024,
                ">500MB" => 500L * 1024 * 1024,
                ">100MB" => 100L * 1024 * 1024,
                _ => 0
            };
            files = files.Where(f => f.SizeBytes >= minBytes);
        }

        files = SortBy switch
        {
            "DateDesc" => files.OrderByDescending(f => f.LastModifiedUtc ?? DateTime.MinValue),
            "NameAsc" => files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
            _ => files.OrderByDescending(f => f.SizeBytes)
        };

        foreach (var file in files)
        {
            DisplayedFiles.Add(file);
        }

        StatusMessage = $"Tracking {DisplayedFiles.Count} largest files ({ByteSizeFormatter.Format(DisplayedFiles.Sum(f => f.SizeBytes))} total).";

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
    public string Extension { get; set; } = string.Empty;
    public ObservableCollection<DuplicateFileEntry> Files { get; } = [];

    public long TotalWastedInGroup => Math.Max(0, (Files.Count(f => f.IsSelectedForDeletion)) * FileSizeBytes);
    public string FormattedWastedInGroup => ByteSizeFormatter.Format(TotalWastedInGroup);

    public void NotifyWastedChanged()
    {
        OnPropertyChanged(nameof(FormattedWastedInGroup));
        OnPropertyChanged(nameof(TotalWastedInGroup));
    }
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
    private string _currentPhase = string.Empty;
    private string _scanTarget = "UserProfile"; // UserProfile, Downloads, Documents
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

    public string CurrentPhase
    {
        get => _currentPhase;
        set => SetProperty(ref _currentPhase, value);
    }

    public string ScanTarget
    {
        get => _scanTarget;
        set => SetProperty(ref _scanTarget, value);
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
    public ICommand SetScanTargetCommand { get; }

    public DuplicatesViewModel(IFileSystemScanner scanner)
    {
        _scanner = scanner;

        StartScanCommand = new AsyncRelayCommand(ScanDuplicatesAsync, () => !IsScanning);
        DeleteSelectedDuplicatesCommand = new RelayCommand(DeleteSelected, () => DuplicateGroups.Count > 0);
        ShowInExplorerCommand = new RelayCommand(param =>
        {
            if (param is DuplicateFileEntry entry) NativeMethods.OpenInExplorer(entry.FullPath);
        });

        SetScanTargetCommand = new RelayCommand(param =>
        {
            if (param is string target) ScanTarget = target;
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
            grp.NotifyWastedChanged();
        }
        TotalWastedBytes = wasted;
        OnPropertyChanged(nameof(FormattedWastedBytes));
    }

    private async Task ScanDuplicatesAsync()
    {
        IsScanning = true;
        CurrentPhase = "Initializing scan scope...";
        StatusMessage = "Analyzing file signatures and computing SHA-256 byte hashes...";
        DuplicateGroups.Clear();
        TotalWastedBytes = 0;

        try
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string scanPath = ScanTarget switch
            {
                "Downloads" => Path.Combine(userProfile, "Downloads"),
                "Documents" => Path.Combine(userProfile, "Documents"),
                _ => userProfile
            };

            if (!Directory.Exists(scanPath)) scanPath = userProfile;

            CurrentPhase = $"Scanning: {scanPath}";
            var results = await _scanner.FindDuplicateFilesAsync(scanPath);

            long wasted = 0;
            foreach (var group in results)
            {
                if (group.Count < 2) continue;

                var grp = new DuplicateGroupItem
                {
                    FileSizeBytes = group[0].SizeBytes,
                    Extension = group[0].Extension ?? string.Empty
                };

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
                : "No duplicate files found in selected directory. Everything is clean!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            CurrentPhase = string.Empty;
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
        StatusMessage = $"Successfully moved {deletedCount} duplicate file(s) to Recycle Bin ({ByteSizeFormatter.Format(freedBytes)} recovered).";
    }
}
