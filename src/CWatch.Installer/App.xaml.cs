using System.Windows;

namespace CWatch.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Setup Encountered an Error:\n{args.Exception.Message}\n\nStack Trace:\n{args.Exception.StackTrace}", "C:Watch Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Setup Fatal Error:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "C:Watch Setup Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }
}
