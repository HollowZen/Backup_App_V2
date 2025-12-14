using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BackupApp.WPF.Commands;

public class RelayCommand : ICommand
{
    private readonly Func<object?, bool>? _canExecute;
    private readonly Func<object?, Task>? _executeAsync;
    private readonly Action<object?>? _executeSync;

    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _executeSync = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) =>
        _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter)
    {
        _ = ExecuteInternalAsync(parameter);
    }

    private async Task ExecuteInternalAsync(object? parameter)
    {
        try
        {
            if (_executeAsync != null)
            {
                await _executeAsync(parameter).ConfigureAwait(false);
            }
            else
            {
                _executeSync?.Invoke(parameter);
            }
        }
        catch (Exception ex)
        {
            // Логировать ошибку без падения UI
            System.Diagnostics.Debug.WriteLine($"RelayCommand exception: {ex}");
        }
    }

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}










