using System.Collections.ObjectModel;
using System.Windows.Input;
using CWatch.Core.Enums;
using CWatch.Core.Interfaces;
using CWatch.Core.Models;
using CWatch.Infrastructure.WindowsApi;

namespace CWatch.UI.ViewModels;

public sealed class ExplorerViewModel : ViewModelBase
{
    private readonly IFileSystemScanner _scanner;
    private StorageItem? _rootItem;
    private StorageItem? _currentItem;
    private string _searchFilter = string.Empty;
    private StorageCategoryType? _categoryFilter;
    private bool _isLoading;

    public StorageItem? CurrentItem
    {
        get => _currentItem;
        set
        {
            if (SetProperty(ref _currentItem, value))
            {
                UpdateBreadcrumbs();
                ApplyFilter();
            }
        }
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

    public StorageCategoryType? CategoryFilter
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

    public ObservableCollection<StorageItem> DisplayedItems { get; } = [];
    public ObservableCollection<StorageItem> Breadcrumbs { get; } = [];

    public ICommand NavigateToItemCommand { get; }
    public ICommand NavigateUpCommand { get; }
    public ICommand OpenItemCommand { get; }
    public ICommand ShowInExplorerCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand ShowPropertiesCommand { get; }

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
            if (param is StorageItem item)
            {
                NativeMethods.OpenItem(item.FullPath);
            }
        });

        ShowInExplorerCommand = new RelayCommand(param =>
        {
            if (param is StorageItem item)
            {
                NativeMethods.OpenInExplorer(item.FullPath);
            }
        });

        CopyPathCommand = new RelayCommand(param =>
        {
            if (param is StorageItem item)
            {
                System.Windows.Clipboard.SetText(item.FullPath);
            }
        });

        ShowPropertiesCommand = new RelayCommand(param =>
        {
            if (param is StorageItem item)
            {
                NativeMethods.ShowFileProperties(item.FullPath);
            }
        });
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

        // Build path from current up to root if parent links exist, or simple sequence
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

    private void ApplyFilter()
    {
        DisplayedItems.Clear();
        if (CurrentItem == null) return;

        var items = CurrentItem.Children.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            items = items.Where(i => i.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                     i.FullPath.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (_categoryFilter.HasValue)
        {
            items = items.Where(i => i.Category == _categoryFilter.Value);
        }

        foreach (var item in items.OrderByDescending(i => i.SizeBytes))
        {
            DisplayedItems.Add(item);
        }
    }
}
