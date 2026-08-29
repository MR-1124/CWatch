using System.Windows.Controls;
using System.Windows.Input;
using CWatch.Core.Models;
using CWatch.UI.ViewModels;

namespace CWatch.UI.Views;

public partial class ExplorerView : UserControl
{
    public ExplorerView()
    {
        InitializeComponent();
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem lvi && lvi.DataContext is StorageItem item && item.IsDirectory)
        {
            if (DataContext is ExplorerViewModel vm)
            {
                vm.CurrentItem = item;
            }
        }
    }

    private void OnListViewDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListView lv && lv.SelectedItem is StorageItem item && item.IsDirectory)
        {
            if (DataContext is ExplorerViewModel vm)
            {
                vm.CurrentItem = item;
            }
        }
    }
}
