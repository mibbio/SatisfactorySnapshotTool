
namespace SatisfactorySnapshotTool
{
    using SatisfactorySnapshotTool.Events;

    using System;
    using System.IO;
    using System.Security.AccessControl;
    using System.Threading;

    /// <summary>
    /// A <see cref="FileStream"/> extended by progress notification every 0.5% of total length
    /// </summary>
    internal class MonitoredFileStream : FileStream
    {
        private long readSinceLastNotify = 0;

        public CancellationToken CancellationToken { get; set; }

        public MonitoredFileStream(string path, FileMode mode) : base(path, mode)
        {
        }

        public MonitoredFileStream(string path, FileMode mode, FileAccess access) : base(path, mode, access)
        {
        }

        public MonitoredFileStream(string path, FileMode mode, FileAccess access, FileShare share) : base(path, mode, access, share)
        {
        }

        public MonitoredFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize) : base(path, mode, access, share, bufferSize)
        {
        }

        public MonitoredFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) : base(path, mode, access, share, bufferSize, options)
        {
        }

        public MonitoredFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync) : base(path, mode, access, share, bufferSize, useAsync)
        {
        }

        public MonitoredFileStream(string path, FileMode mode, FileSystemRights rights, FileShare share, int bufferSize, FileOptions options, FileSecurity fileSecurity) : base(path, mode, rights, share, bufferSize, options, fileSecurity)
        {
        }

        public MonitoredFileStream(string path, FileMode mode, FileSystemRights rights, FileShare share, int bufferSize, FileOptions options) : base(path, mode, rights, share, bufferSize, options)
        {
        }

        public override int Read(byte[] array, int offset, int count)
        {
            int dataLength = base.Read(array, offset, count);

            readSinceLastNotify += dataLength;
            if (readSinceLastNotify > Length / 200)
            {
                readSinceLastNotify = 0;
                if(CancellationToken != null && CancellationToken.IsCancellationRequested)
                {
                    return 0; // pretend there is no more data
                }
                OnPositionChange?.Invoke(this, new FileProgressEventArgs(Name, Position, Length));
            }

            return dataLength;
        }

        public event EventHandler<FileProgressEventArgs> OnPositionChange;
    }
}
