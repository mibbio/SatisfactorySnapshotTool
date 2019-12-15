namespace Updater
{
    using System;

    public class UpdateData
    {
        public Version Version { get; set; }

        public bool IsPrerelease { get; set; }

        public Uri DownloadUri { get; set; }

        public string Filename => DownloadUri != null ? DownloadUri.Segments[DownloadUri.Segments.Length - 1] : String.Empty;

        public int Size { get; set; }

        public DateTime CreatedAt { get; set; }

        public UpdateData(Version version) {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            IsPrerelease = false;
            Size = -1;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
