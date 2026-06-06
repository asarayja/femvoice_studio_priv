using System;
using System.Windows.Input;

namespace FemVoiceStudio.ViewModels
{
    /// <summary>
    /// Shared RelayCommand used across the ViewModels.
    /// Uses CommandManager.RequerySuggested for automatic CanExecute re-evaluation,
    /// plus RaiseCanExecuteChanged() for manual invalidation.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        /// <summary>
        /// Manually raises CanExecuteChanged. Call when command availability changes.
        /// </summary>
        public void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
