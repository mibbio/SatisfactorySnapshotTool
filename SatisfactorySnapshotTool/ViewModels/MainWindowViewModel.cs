namespace SatisfactorySnapshotTool.ViewModels
{
    using SatisfactorySnapshotTool.Backup;
    using SatisfactorySnapshotTool.Mvvm;
    using SatisfactorySnapshotTool.Mvvm.Commands;

    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Reflection;
    using System.Windows.Forms;
    using System.Windows.Input;

    using Updater;
    using Updater.Events;

    public sealed class MainWindowViewModel : WindowViewModel
    {
        private readonly ISettings _settingsProvider;

        private bool _backupRunning;

        private Updater _updater;

        private string _updateLabel = string.Empty;

        private string _updateProgressLabel = string.Empty;

        public BackupManager BackupManager { get; private set; }

        public ContentViewModel BackupPanel { get; private set; }

        public ContentViewModel CreatePanel { get; private set; }

        public bool ShowBackups => !BackupRunning;

        public string UpdateLabel
        {
            get => _updateLabel;
            set
            {
                if (_updateLabel.Equals(value)) return;
                _updateLabel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UpdateAvailable));
            }
        }

        public string UpdateProgressLabel
        {
            get => _updateProgressLabel;
            set
            {
                if (_updateProgressLabel.Equals(value)) return;
                _updateProgressLabel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UpdateRunning));
            }
        }

        public bool UpdateAvailable => !string.IsNullOrEmpty(UpdateLabel);

        public bool UpdateRunning => !string.IsNullOrEmpty(UpdateProgressLabel);

        public bool BackupRunning
        {
            get => _backupRunning;
            private set
            {
                if (_backupRunning == value) return;
                _backupRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowBackups));
            }
        }

        public string BackupPath
        {
            get => _settingsProvider.BackupPath;
            set
            {
                if (string.IsNullOrWhiteSpace(value) || _settingsProvider.BackupPath == value) return;
                _settingsProvider.BackupPath = value;
                OnPropertyChanged();
            }
        }

        public string GamePath
        {
            get => _settingsProvider.GamePath;
            set
            {
                if (string.IsNullOrWhiteSpace(value) || _settingsProvider.GamePath == value) return;
                _settingsProvider.GamePath = value;
                OnPropertyChanged();
            }
        }

        public MainWindowViewModel() : base("Satisfatory Snapshot Tool", 800, 600)
        {
            Title = $"{Title} v{Assembly.GetExecutingAssembly().GetName().Version}";
            _settingsProvider = new UserSettings();
            BackupManager = new BackupManager(_settingsProvider);
            BackupPanel = new BackupViewModel(BackupManager);
            CreatePanel = new BackupCreateViewModel(BackupManager);

            var assembly = Assembly.GetExecutingAssembly().GetName();
            _updater = new GithubUpdater(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "mibbio", "SatisfactorySnapshotTool", true);

            _updater.StartTimedCheck(assembly.Version, new TimeSpan(0, 15, 0));
            _updater.OnUpdateAvailable += OnUpdateAvailable;
            _updater.OnUpdateDownloadDone += (s, e) => UpdateLabel = string.Empty;
            _updater.OnUpdateDownloadStarted += (s, e) =>
            {
                UpdateLabel = string.Empty;
                UpdateProgressLabel = $"Starting download of {e} ...";
            };
            _updater.OnUpdateDownloadDone += (s, e) =>
            {
                UpdateProgressLabel = string.Empty;
                _updater.ApplyUpdate();
            };
            _updater.OnUpdateDownloadProgressChanged += (s, e) =>
            {
                UpdateProgressLabel = $"Downloading: {e.ProgressPercentage}%";
            };
            _updater.OnUpdateApplied += (s, e) =>
            {
                var updateMsg = "Update extracted. Application is now closing.\nRestart it to complete update.";
                var updateCpt = "Final update step";
                if (MessageBox.Show(updateMsg, updateCpt, MessageBoxButtons.OK, MessageBoxIcon.Information) == DialogResult.OK)
                {
                    System.Windows.Application.Current.MainWindow.Close();
                }
            };
        }

        private void OnUpdateAvailable(object sender, UpdateDataEventArgs e)
        {
            var readableSize = FileHelper.GetHumanReadableSize(e.Update.Size);
            UpdateLabel = string.Format("{0} ({1:n2} {2})", e.Update.Version, readableSize.Item1, readableSize.Item2);
        }

        private ICommand _cmdChangeBackupPath;

        public ICommand CmdChangeBackupPath => _cmdChangeBackupPath ?? (_cmdChangeBackupPath = new RelayCommand(() =>
        {
            var dialog = new FolderBrowserDialog()
            {
                Description = "Select preferred backup location",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                BackupPath = dialog.SelectedPath;
            }
        }, () => !BackupRunning));

        private ICommand _cmdChangeGamePath;

        public ICommand CmdChangeGamePath => _cmdChangeGamePath ?? (_cmdChangeGamePath = new RelayCommand(() =>
        {
            var dialog = new OpenFileDialog()
            {
                Title = "Select game main executable",
                Filter = "Game executable|FactoryGame.exe",
                Multiselect = false
            };

            if (!string.IsNullOrEmpty(_settingsProvider.GamePath))
            {
                dialog.InitialDirectory = _settingsProvider.GamePath;
            }

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _settingsProvider.GamePath = Path.GetDirectoryName(dialog.FileName);
            }
        }, () => !BackupRunning));

        private ICommand _cmdCreateBackup;

        public ICommand CmdCreateBackup => _cmdCreateBackup ?? (_cmdCreateBackup = new RelayCommand(async () =>
        {
            if (string.IsNullOrEmpty(_settingsProvider.GamePath))
            {
                var dialog = new OpenFileDialog()
                {
                    Title = "Select game main executable",
                    Filter = "Game executable|FactoryGame.exe",
                    Multiselect = false
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    GamePath = Path.GetDirectoryName(dialog.FileName);
                }
                else return;
            }

            BackupManager.OnBackupStep += HandleBackupStep;

            try
            {
                await BackupManager.CreateBackup();
            }
            catch (Exception ex)
            {
                App.SendReport(ex);
            }
        }, () => !BackupRunning));

        private ICommand _cmdDoUpdate;

        public ICommand CmdDoUpdate => _cmdDoUpdate ?? (_cmdDoUpdate = new RelayCommand(async () =>
        {
            await _updater.DownloadUpdateAsync();
        }));

        private ICommand _onWindowClosing;

        public ICommand OnWindowClosing => _onWindowClosing ?? (_onWindowClosing = new RelayCommand<CancelEventArgs>(e =>
        {
            e.Cancel = BackupRunning;
            if (BackupRunning)
            {
                BackupManager.CancelBackup();
            }
            System.Windows.Application.Current.Shutdown();
        }));

        private void HandleBackupStep(object sender, Events.BackupEventArgs e)
        {
            BackupRunning = (e.Step != Events.BackupStep.None);
        }
    }
}
