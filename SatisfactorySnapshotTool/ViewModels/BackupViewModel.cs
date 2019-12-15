namespace SatisfactorySnapshotTool.ViewModels
{
    using SatisfactorySnapshotTool.Backup;
    using SatisfactorySnapshotTool.Models;
    using SatisfactorySnapshotTool.Mvvm;
    using SatisfactorySnapshotTool.Mvvm.Commands;

    using System;
    using System.Collections.ObjectModel;
    using System.Windows.Input;

    public class BackupViewModel : ContentViewModel
    {
        private BackupManager _backupManager;

        private BackupModel _selectedBackup;

        public ObservableCollection<BackupModel> BackupCollection { get; private set; }

        public BackupModel SelectedBackup
        {
            get => _selectedBackup;
            set
            {
                if (_selectedBackup == value) return;
                _selectedBackup = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowDetails));
            }
        }

        public bool ShowDetails => SelectedBackup != null;

        public BackupViewModel(BackupManager backupManager)
        {
            if (backupManager == null) throw new ArgumentNullException(nameof(backupManager));
            _backupManager = backupManager;
            BackupCollection = _backupManager.Backups;
        }

        private ICommand _cmdDeleteBackup;

        public ICommand CmdDeleteBackup => _cmdDeleteBackup ?? (_cmdDeleteBackup = new RelayCommand<BackupModel>(bm =>
        {
            int newIndex = BackupCollection.IndexOf(bm) - 1;
            _backupManager.DeleteBackup(bm);
            if (newIndex >= 0)
            {
                SelectedBackup = BackupCollection[newIndex];
            }
        }, bm => bm != null));

        private ICommand _cmdLaunchBackup;

        public ICommand CmdLaunchBackup => _cmdLaunchBackup ?? (_cmdLaunchBackup = new RelayCommand<BackupModel>(async bm =>
        {
            await _backupManager.Launch(bm);
        }, bm => bm != null));
    }
}
