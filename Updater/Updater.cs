namespace Updater
{
    using global::Updater.Events;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using System.Windows.Threading;

    public abstract class Updater
    {
        protected readonly string _applicationRoot;

        protected readonly string _downloadDir;

        protected readonly DispatcherTimer _checkTimer;

        protected List<UpdateData> _availableUpdates;

        protected string _latestDownloadedFile;

        public bool IncludePreReleases { get; set; }

        public Updater(string applicationRoot, bool includePreReleases = false)
        {
            if (string.IsNullOrWhiteSpace(applicationRoot)) throw new ArgumentNullException(nameof(applicationRoot));
            if (!Directory.Exists(applicationRoot)) throw new ArgumentException("The path does not exist.", nameof(applicationRoot));
            IncludePreReleases = includePreReleases;
            _applicationRoot = applicationRoot;
            _downloadDir = Path.Combine(_applicationRoot, "Updates");
            _checkTimer = new DispatcherTimer(DispatcherPriority.Background);
            _availableUpdates = new List<UpdateData>();
        }

        public void StartTimedCheck(Version installedVersion, TimeSpan interval)
        {
            if (_checkTimer.IsEnabled) return;
            _checkTimer.Interval = interval;
            _checkTimer.Tick += async (s, e) => await CheckAsync(installedVersion);
            _checkTimer.Start();
        }

        public void StopTimedCheck()
        {
            if (!_checkTimer.IsEnabled) return;
            _checkTimer.Stop();
        }

        protected virtual async Task CheckAsync(Version installedVersion)
        {
            var filteredUpdates = _availableUpdates
                .Where(u => u.Version > installedVersion && u.IsPrerelease == IncludePreReleases)
                .OrderByDescending(u => u.Version);

            if (filteredUpdates.Any())
            {
                StopTimedCheck();
                OnUpdateAvailable?.Invoke(this, new UpdateDataEventArgs(filteredUpdates.First()));
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task DownloadUpdateAsync(string targetDir = "")
        {
            var update = _availableUpdates.First();

            var targetPath = string.Empty;
            var targetFile = $"{update.Version.ToString()}_{update.Filename}";
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                targetPath = Path.Combine(targetDir, targetFile);
            }
            else
            {
                if (!Directory.Exists(_downloadDir))
                {
                    Directory.CreateDirectory(_downloadDir);
                }
                targetPath = Path.Combine(_downloadDir, targetFile);
            }

            using (var wc = new WebClient())
            {
                try
                {
                    wc.DownloadProgressChanged += OnUpdateDownloadProgressChanged;
                    wc.DownloadFileCompleted += (s, e) =>
                    {
                        if (!e.Cancelled)
                        {
                            _latestDownloadedFile = targetPath;
                            OnUpdateDownloadDone?.Invoke(this, targetPath);
                        }
                        else
                        {
                            OnUpdateDownloadDone?.Invoke(this, string.Empty);
                        }
                    };

                    OnUpdateDownloadStarted?.Invoke(this, update.Filename);

                    await wc.DownloadFileTaskAsync(update.DownloadUri, targetPath);
                }
                catch (Exception ex) when (ex is WebException || ex is InvalidOperationException)
                {
                    wc.DownloadProgressChanged -= OnUpdateDownloadProgressChanged;
                    OnDownloadFailed?.Invoke(this, ex.Message);
                }
            }
        }

        protected virtual void RaiseUpdateApplied(Updater sender)
        {
            OnUpdateApplied?.Invoke(sender, null);
        }

        public abstract void ApplyUpdate();

        public event EventHandler<UpdateDataEventArgs> OnUpdateAvailable;

        public event EventHandler<string> OnUpdateDownloadStarted;

        public event EventHandler<string> OnUpdateDownloadDone;

        public event EventHandler<string> OnDownloadFailed;

        public event DownloadProgressChangedEventHandler OnUpdateDownloadProgressChanged;

        public event EventHandler OnUpdateApplied;
    }
}
