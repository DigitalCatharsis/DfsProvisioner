using System.Windows.Input;

namespace DFS_Provisioner.ViewModels
{
    /// <summary>Standard WPF command implementation that delegates execution to a given action.</summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        /// <summary>Creates a new RelayCommand.</summary>
        /// <param name="execute">The action to run when the command is invoked.</param>
        /// <param name="canExecute">Optional predicate to determine if the command can execute.</param>
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }
}