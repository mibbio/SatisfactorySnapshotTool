
namespace SatisfactorySnapshotTool.Mvvm
{
    using System;
    using System.IO;

    public class UserSettings : ISettings
    {
        public string BackupPath
        {
            get => Properties.Settings.Default.BackupPath;
            set
            {
                if (string.IsNullOrWhiteSpace(value) || Properties.Settings.Default.BackupPath == value) return;
                Properties.Settings.Default.BackupPath = value;
                Save();
            }
        }

        public string GamePath
        {
            get => Properties.Settings.Default.GamePath;
            set
            {
                if (string.IsNullOrWhiteSpace(value) || Properties.Settings.Default.GamePath == value) return;
                Properties.Settings.Default.GamePath = value;
                Save();
            }
        }

        public string SavegamePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FactoryGame", "Saved", "SaveGames");

        public UserSettings()
        {
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }
            if (string.IsNullOrEmpty(BackupPath))
            {
                BackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SatisfactoryBackups");
            }
        }

        public void Save()
        {
            Properties.Settings.Default.Save();
        }
    }
}
