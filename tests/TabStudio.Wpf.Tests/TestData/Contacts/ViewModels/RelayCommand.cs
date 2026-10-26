using System.Windows.Input;

namespace Contacts.ViewModels
{
    /// <summary>A command that runs a method, when a condition allows.</summary>
    public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
