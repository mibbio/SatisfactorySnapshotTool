namespace SatisfactorySnapshotTool.Mvvm.Commands
{
    using System;
    using System.Windows.Input;

    public sealed class RelayCommand : ICommand
    {
        #region Fields
        private readonly Action _execute;

        private readonly Func<bool> _canExecute;
        #endregion

        #region Constructors
        public RelayCommand(Action execute) : this(execute, null) { }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        #endregion

        #region ICommand members
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add
            {
                if (_canExecute != null)
                {
                    CommandManager.RequerySuggested += value;
                }
            }
            remove
            {
                if (_canExecute != null)
                {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }
        #endregion
    }
}
