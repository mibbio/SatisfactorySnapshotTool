namespace SatisfactorySnapshotTool.Events
{
    using System;

    public sealed class FileProgressEventArgs : EventArgs
    {
        #region properties
        public string Filename { get; }

        public long ProcessedBytes { get; }

        public long Filesize { get; }
        #endregion

        #region constructors
        public FileProgressEventArgs(string filename, long copiedBytes, long filesize)
        {
            Filename = filename ?? throw new ArgumentNullException(nameof(filename));
            ProcessedBytes = copiedBytes;
            Filesize = filesize;
        }
        #endregion
    }
}
