using System.Collections.ObjectModel;
using System.Windows.Input;
using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Core.Safety;
using CWatch.Infrastructure.WindowsApi;

namespace CWatch.UI.ViewModels;

public sealed class ExplorerViewModel : ViewModelBase
{
    private readonly IFileSystemScanner _scanner;
    private StorageItem? _rootItem;
    private StorageItem? _currentItem;
    private StorageItem? _selectedItem;
    private string _searchFilter = string.Empty;
    private string _sortBy = "SizeDesc"; // SizeDesc, SizeAsc, NameAsc, ItemsDesc
    private string _categoryFilter = "All";
    private bool _isLoading;
    private string _statusInfo = string.Empty;

    public StorageItem? CurrentItem
    {
        get => _currentItem;
        set
        {
            if (SetProperty(ref _currentItem, value))
            {
                UpdateBreadcrumbs();
                ApplyFilter();
                UpdateStatusInfo();
            }
        }
    }

    public StorageItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
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

    public string CategoryFilter
    {
        get => _categoryFilter;
        set
        {
            if (SetProperty(ref _categoryFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusInfo
    {
        get => _statusInfo;
        set => SetProperty(ref _statusInfo, value);
    }

    public bool IsCurrentPathCritical => CurrentItem != null && PathSafetyValidator.IsCriticalSystemPath(CurrentItem.FullPath);

    public ObservableCollection<StorageItem> DisplayedItems { get; } = [];
    public ObservableCollection<StorageItem> Breadcrumbs { get; } = [];

    public ICommand NavigateToItemCommand { get; }
    public ICommand NavigateUpCommand { get; }
    public ICommand OpenItemCommand { get; }
    public ICommand ShowInExplorerCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }
    public ICommand SetSortCommand { get; }
    public ICommand SetCategoryFilterCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public ExplorerViewModel(IFileSystemScanner scanner)
    {
        _scanner = scanner;

        NavigateToItemCommand = new RelayCommand(param =>
        {
            if (param is StorageItem item && item.IsDirectory)
            {
                CurrentItem = item;
            }
        });

        NavigateUpCommand = new RelayCommand(() =>
        {
            if (CurrentItem != null && Breadcrumbs.Count > 1)
            {
                CurrentItem = Breadcrumbs[^2];
            }
        }, () => Breadcrumbs.Count > 1);

        OpenItemCommand = new RelayCommand(param =>
        {
            var target = param as StorageItem ?? SelectedItem;
            if (target != null)
            {
                if (target.IsDirectory)
                {
                    CurrentItem = target;
                }
                else
                {
                    NativeMethods.OpenItem(target.FullPath);
                }
            }
        });

        ShowInExplorerCommand = new RelayCommand(param =>
        {
            var target = param as StorageItem ?? SelectedItem;
            if (target != null) NativeMethods.OpenInExplorer(target.FullPath);
        });

        CopyPathCommand = new RelayCommand(param =>
        {
            var target = param as StorageItem ?? SelectedItem;
            if (target != null) System.Windows.Clipboard.SetText(target.FullPath);
        });

        ShowPropertiesCommand = new RelayCommand(param =>
        {
            var target = param as StorageItem ?? SelectedItem;
            if (target != null) NativeMethods.ShowFileProperties(target.FullPath);
        });

        SetSortCommand = new RelayCommand(param =>
        {
            if (param is string sort) SortBy = sort;
        });

        SetCategoryFilterCommand = new RelayCommand(param =>
        {
            if (param is string cat) CategoryFilter = cat;
        });

        ClearSearchCommand = new RelayCommand(() => SearchFilter = string.Empty);
    }

    public void SetRootItem(StorageItem root)
    {
        _rootItem = root;
        CurrentItem = root;
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        if (CurrentItem == null) return;

        var chain = new List<StorageItem>();
        var curr = CurrentItem;
        while (curr != null)
        {
            chain.Add(curr);
            if (curr == _rootItem) break;
            curr = FindParentNode(_rootItem, curr);
        }

        chain.Reverse();
        foreach (var item in chain) Breadcrumbs.Add(item);
        OnPropertyChanged(nameof(IsCurrentPathCritical));
    }

    private static StorageItem? FindParentNode(StorageItem? root, StorageItem target)
    {
        if (root == null || root == target) return null;
        foreach (var child in root.Children)
        {
            if (child == target) return root;
            var found = FindParentNode(child, target);
            if (found != null) return found;
        }
        return null;
    }

    private void UpdateStatusInfo()
    {
        if (CurrentItem == null)
        {
            StatusInfo = "No directory loaded.";
            return;
        }

        int folderCount = CurrentItem.Children.Count(c => c.IsDirectory);
        int fileCount = CurrentItem.Children.Count(c => !c.IsDirectory);
        long totalBytes = CurrentItem.SizeBytes;

        StatusInfo = $"{folderCount} folders, {fileCount} files • Total Allocated: {ByteSizeFormatter.Format(totalBytes)}";
    }

    private void ApplyFilter()
    {
        DisplayedItems.Clear();
        if (CurrentItem == null) return;

        var items = CurrentItem.Children.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            items = items.Where(i => i.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                     i.FullPath.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                     (i.Extension != null && i.Extension.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)));
        }

        if (CategoryFilter != "All")
        {
            if (Enum.TryParse<StorageCategoryType>(CategoryFilter, true, out var catType))
            {
                items = items.Where(i => i.Category == catType);
            }
        }

        items = SortBy switch
        {
            "SizeAsc" => items.OrderBy(i => i.SizeBytes),
            "NameAsc" => items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            "NameDesc" => items.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase),
            "ItemsDesc" => items.OrderByDescending(i => i.FileCount),
            _ => items.OrderByDescending(i => i.SizeBytes)
        };

        foreach (var item in items)
        {
            DisplayedItems.Add(item);
        }

        if (SelectedItem == null || !DisplayedItems.Contains(SelectedItem))
        {
            SelectedItem = DisplayedItems.FirstOrDefault();
        }
    }
}
