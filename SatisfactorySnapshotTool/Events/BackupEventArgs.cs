namespace SatisfactorySnapshotTool.Events
{
    public enum BackupStep
    {
        None,
        CalculateChecksums,
        CopyGameFile,
        CopySaveFile
    }

    public class BackupEventArgs
    {
        #region properties
        public BackupStep Step { get; }

        public int TotalFiles { get; }
        #endregion

        #region constructors
        public BackupEventArgs(BackupStep step, int totalFiles)
        {
            Step = step;
            TotalFiles = totalFiles;
        }
        #endregion
    }
}
