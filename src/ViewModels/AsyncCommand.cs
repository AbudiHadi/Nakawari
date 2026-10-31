using System.Windows;
using System.Windows.Input;
using Nakawari.Core;

namespace Nakawari.ViewModels;

/// <summary>One command type for the whole UI: disables itself while running and never lets an exception escape.</summary>
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _running;

    public static AsyncCommand From(Action action, Func<bool>? canExecute = null) =>
        new(() => { action(); return Task.CompletedTask; }, canExecute);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        _running = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await execute();
        }
        catch (Exception ex)
        {
            Log.Error("Unexpected error", ex);
            MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _running = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
