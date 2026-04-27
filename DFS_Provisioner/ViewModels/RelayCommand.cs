using System.Windows.Input;

namespace DFS_Provisioner.ViewModels
{
    // A standard implementation of ICommand to bind UI actions to ViewModel methods
    public class RelayCommand : ICommand
    {
        private readonly Action _execute; // The action to be performed
        private readonly Func<bool> _canExecute; // The logic that determines if the action is allowed

        // Constructor: takes the method to run and an optional check function
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // Notifies the UI that the 'CanExecute' status might have changed
        public event EventHandler CanExecuteChanged
        {
            // Hooks into the WPF CommandManager to automatically re-evaluate button state
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        // Checks if the command can run (returns true by default if no logic is provided)
        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        // Runs the actual logic assigned to this command
        public void Execute(object parameter) => _execute();
    }
}
