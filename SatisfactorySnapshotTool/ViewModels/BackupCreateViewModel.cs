namespace SatisfactorySnapshotTool.ViewModels
{
    using SatisfactorySnapshotTool.Backup;
    using SatisfactorySnapshotTool.Events;
    using SatisfactorySnapshotTool.Mvvm;
    using SatisfactorySnapshotTool.Mvvm.Commands;

    using System;
    using System.IO;
    using System.Windows.Input;
    using System.Windows.Threading;

    class BackupCreateViewModel : ContentViewModel
    {
        private readonly BackupManager _backupManager;

        private string _currentStepLabel = string.Empty;

        private string _currentFile = string.Empty;

        private int _currentFileNumber;

        private int _totalFiles;

        private Tuple<float, float, string> _currentFileProgress;

        public string CurrentStepLabel
        {
            get => _currentStepLabel;
            set
            {
                if (_currentStepLabel == value) return;
                _currentStepLabel = value;
                OnPropertyChanged();
            }
        }

        public string CurrentFile
        {
            get => _currentFile;
            set
            {
                if (_currentFile == value) return;
                _currentFile = value;
                OnPropertyChanged();
            }
        }

        public int CurrentFileNumber
        {
            get => _currentFileNumber;
            set
            {
                if (_currentFileNumber == value) return;
                _currentFileNumber = value;
                OnPropertyChanged();
            }
        }

        public int TotalFiles
        {
            get => _totalFiles;
            set
            {
                if (_totalFiles == value) return;
                _totalFiles = value;
                OnPropertyChanged();
            }
        }

        public Tuple<float, float, string> CurrentFileProgress
        {
            get => _currentFileProgress;
            set
            {
                if (_currentFileProgress == value) return;
                _currentFileProgress = value;
                OnPropertyChanged();
            }
        }

        public BackupCreateViewModel(BackupManager backupManager)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _backupManager.OnBackupStep += HandleBackupStep;
            _backupManager.OnFileProcessing += HandleFileProcessing;
            _backupManager.OnFileProgress += HandleFileProgress;
        }

        private int _stepCounter = 0;
        private void HandleBackupStep(object sender, BackupEventArgs e)
        {
            string stepCountLabel = $"Step {++_stepCounter}/4";
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                TotalFiles = e.TotalFiles;
                switch (e.Step)
                {
                    case BackupStep.CalculateChecksums:
                        CurrentStepLabel = $"{stepCountLabel}: Calculate checksums";
                        break;
                    case BackupStep.CopyGameFile:
                        CurrentStepLabel = $"{stepCountLabel}: Copy game files";
                        break;
                    case BackupStep.CopySaveFile:
                        CurrentStepLabel = $"{stepCountLabel}: Copy save files";
                        break;
                    default:
                        _stepCounter = 0;
                        CurrentStepLabel = string.Empty;
                        break;
                }
            });
        }

        private void HandleFileProcessing(object sender, FileProcessEventArgs e)
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                CurrentFile = Path.GetFileName(e.Filename);
                CurrentFileNumber = e.Counter;
            });
        }

        private void HandleFileProgress(object sender, FileProgressEventArgs e)
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                CurrentFile = Path.GetFileName(e.Filename);
                CurrentFileProgress = FileHelper.GetHumanReadableSize(e.ProcessedBytes, e.Filesize);
            });
        }

        private ICommand _cmdCancelBackup;

        public ICommand CmdCancelBackup => _cmdCancelBackup ?? (_cmdCancelBackup = new RelayCommand(() => _backupManager.CancelBackup()));
    }
}
