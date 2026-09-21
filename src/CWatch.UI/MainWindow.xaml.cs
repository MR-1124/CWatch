using System.Windows;
using System.Windows.Controls;
using CWatch.UI.ViewModels;

namespace CWatch.UI;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = mainViewModel;
    }

    /// <summary>
    /// Checked-state handler for sidebar nav items. The page name lives in the
    /// RadioButton's Tag, keeping the XAML declarative; programmatic navigation
    /// (e.g. cross-page actions) re-checks the matching radio by Tag.
    /// </summary>
    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string page } && DataContext is MainViewModel vm)
        {
            vm.NavigateCommand.Execute(page);
        }
    }
}
