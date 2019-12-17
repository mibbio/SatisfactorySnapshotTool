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

        /// <summary>
        /// Should Pre-releases be included when checking for updates
        /// </summary>
        public bool IncludePreReleases { get; set; }

        /// <summary>
        /// Creates an Updater instance
        /// </summary>
        /// <param name="applicationRoot">Folder where the applications resides which should be updated</param>
        /// <param name="includePreReleases">Should Pre-releases be included when checking for updates</param>
        protected Updater(string applicationRoot, bool includePreReleases = false)
        {
            if (string.IsNullOrWhiteSpace(applicationRoot)) throw new ArgumentNullException(nameof(applicationRoot));
            if (!Directory.Exists(applicationRoot)) throw new ArgumentException("The path does not exist.", nameof(applicationRoot));
            IncludePreReleases = includePreReleases;
            _applicationRoot = applicationRoot;
            _downloadDir = Path.Combine(_applicationRoot, "Updates");
            _checkTimer = new DispatcherTimer(DispatcherPriority.Background);
            _availableUpdates = new List<UpdateData>();
        }

        /// <summary>
        /// Starts the frequent check for available updates
        /// </summary>
        /// <param name="installedVersion">The currently installed version of the application</param>
        /// <param name="interval">How often should the check be executed</param>
        public void StartTimedCheck(Version installedVersion, TimeSpan interval)
        {
            if (_checkTimer.IsEnabled) return;
            _checkTimer.Interval = interval;
            _checkTimer.Tick += async (s, e) => await CheckAsync(installedVersion);
            _checkTimer.Start();
        }

        /// <summary>
        /// Stops the frequent check for available updates
        /// </summary>
        public void StopTimedCheck()
        {
            if (!_checkTimer.IsEnabled) return;
            _checkTimer.Stop();
        }

        /// <summary>
        /// Does the comparision of current version and available update version
        /// </summary>
        /// <param name="installedVersion"></param>
        /// <returns></returns>
        /// <remarks>
        /// <para>Base class only does the comparison as fetching update data depends on implementation in a derived class.</para>
        /// </remarks>
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

        /// <summary>
        /// Downloads the update file to the given directory
        /// </summary>
        /// <param name="targetDir">Directory where the download should be saved. Uses application directory if omitted</param>
        /// <returns></returns>
        public async Task DownloadUpdateAsync(string targetDir = "")
        {
            var update = _availableUpdates.OrderByDescending(u => u.Version).First();

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

        /// <summary>
        /// Invokes event indicating the update is done
        /// </summary>
        /// <param name="sender">The source of the event</param>
        protected virtual void RaiseUpdateApplied(Updater sender)
        {
            OnUpdateApplied?.Invoke(sender, null);
        }

        /// <summary>
        /// Updates the application files
        /// </summary>
        public abstract void ApplyUpdate();

        /// <summary>
        /// Indicates that there is an update available
        /// </summary>
        public event EventHandler<UpdateDataEventArgs> OnUpdateAvailable;

        /// <summary>
        /// Indicates that the download has started
        /// </summary>
        public event EventHandler<string> OnUpdateDownloadStarted;

        /// <summary>
        /// Indicates that the download is finished
        /// </summary>
        public event EventHandler<string> OnUpdateDownloadDone;

        /// <summary>
        /// Indicates errer event while downloading the update
        /// </summary>
        public event EventHandler<string> OnDownloadFailed;

        /// <summary>
        /// Indicates the progress of the download
        /// </summary>
        public event DownloadProgressChangedEventHandler OnUpdateDownloadProgressChanged;

        /// <summary>
        /// Indicates the end of the update process
        /// </summary>
        public event EventHandler OnUpdateApplied;
    }
}
