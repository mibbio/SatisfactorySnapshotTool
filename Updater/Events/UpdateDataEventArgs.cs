namespace Updater.Events
{
    using System;

    public sealed class UpdateDataEventArgs : EventArgs
    {
        public UpdateData Update { get; }

        public UpdateDataEventArgs(UpdateData data)
        {
            Update = data ?? throw new ArgumentNullException(nameof(data));
        }

        public UpdateDataEventArgs(Version version, Uri downloadUri, DateTime createdAt, bool isPrerelease = false)
        {
            Update = new UpdateData(version)
            {
                DownloadUri = downloadUri ?? throw new ArgumentNullException(nameof(downloadUri)),
                CreatedAt = createdAt,
                IsPrerelease = isPrerelease
            };
        }
    }
}
