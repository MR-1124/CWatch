using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CWatch.UI.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
    }

    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : _ => canExecute())
    {
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute == null || _canExecute(parameter));

    /// <summary>
    /// async void is required by ICommand, so this method must never throw:
    /// the in-flight guard is set/cleared synchronously and every failure path
    /// is observed, keeping exceptions off the unobserved-task finalizer.
    /// </summary>
    public async void Execute(object? parameter)
    {
        // Re-check outside CanExecute: async re-entrancy can slip past a stale
        // requery between CanExecute and here, double-starting long operations.
        if (_isExecuting) return;
        if (_canExecute != null && !_canExecute(parameter)) return;

        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await _execute(parameter);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal outcome, not an error.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AsyncRelayCommand unhandled: {ex}");
#if DEBUG
            System.Windows.MessageBox.Show(
                $"An unexpected error occurred:\n{ex.Message}", "C:Watch Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
#endif
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
