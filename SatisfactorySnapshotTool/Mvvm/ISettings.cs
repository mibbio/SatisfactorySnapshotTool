namespace SatisfactorySnapshotTool.Mvvm
{
    public interface ISettings
    {
        string BackupPath { get; set; }

        string GamePath { get; set; }

        string SavegamePath { get; }

        void Save();
    }
}
