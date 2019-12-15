namespace SatisfactorySnapshotTool.Mvvm.Commands
{
    using System;
    using System.Windows.Input;

    public sealed class RelayCommand<T> : ICommand
    {
        #region Fields
        private readonly Action<T> _execute;

        private readonly Func<T, bool> _canExecute;
        #endregion

        #region Constructors
        public RelayCommand(Action<T> execute) : this(execute, null) { }

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        #endregion

        #region ICommand members
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);

        public void Execute(object parameter) => _execute((T)parameter);

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
