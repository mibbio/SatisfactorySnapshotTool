namespace Updater
{
    using Newtonsoft.Json.Linq;

    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;

    public sealed class GithubUpdater : Updater
    {
        private readonly string _ghUser;

        private readonly string _ghRepo;

        public GithubUpdater(string applicationRoot, string githubUser, string githubRepo, bool includePreReleases = false)
            : base(applicationRoot, includePreReleases)
        {
            if (string.IsNullOrWhiteSpace(githubUser)) throw new ArgumentNullException(nameof(githubUser));
            if (string.IsNullOrWhiteSpace(githubRepo)) throw new ArgumentNullException(nameof(githubRepo));

            _ghUser = githubUser;
            _ghRepo = githubRepo;
        }

        protected override async Task CheckAsync(Version installedVersion)
        {
            // fill update data
            using (var client = new HttpClient())
            {
                // use authorization for higher request rate limit
                // uncomment next line & replace TOKEN with github api OAuth token without any scopes
                //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", "TOKEN");
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(_ghRepo, installedVersion.ToString()));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var result = await client.GetAsync(string.Format("https://api.github.com/repos/{0}/{1}/releases", _ghUser, _ghRepo)).ConfigureAwait(false);
                if (result.IsSuccessStatusCode)
                {
                    _availableUpdates.Clear();

                    var body = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var json = JToken.Parse(body);
                    foreach (var entry in json)
                    {
                        var isPreRelease = entry["prerelease"].Value<bool>();
                        if (!Version.TryParse(entry["tag_name"].Value<string>(), out var version)) continue;

                        var archive = entry["assets"].Where(t => t["name"].Value<string>().Contains("bin")).FirstOrDefault();
                        if (archive == null) continue;

                        try
                        {
                            _availableUpdates.Add(new UpdateData(version)
                            {
                                Version = version,
                                IsPrerelease = isPreRelease,
                                Size = archive["size"].Value<int>(),
                                CreatedAt = entry["published_at"].Value<DateTime>(),
                                DownloadUri = new Uri(archive["browser_download_url"].Value<string>())
                            });
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                }
            }

            // let base class do general checks
            await base.CheckAsync(installedVersion);
        }

        public override void ApplyUpdate()
        {
            if (!File.Exists(_latestDownloadedFile)) return;
            using (var fs = new FileStream(_latestDownloadedFile, FileMode.Open, FileAccess.Read))
            using (var archive = new ZipArchive(fs))
            {
                foreach (var entry in archive.Entries)
                {
                    var oldFile = Path.Combine(_applicationRoot, entry.Name);
                    if (!File.GetAttributes(oldFile).HasFlag(FileAttributes.Directory))
                    {
                        // rename it before extract new file
                        File.Move(oldFile, $"{oldFile}.old");

                        //extract
                        using (var output = File.Create(oldFile))
                        using (var cs = entry.Open())
                        {
                            cs.CopyTo(output);
                        }
                    }
                };
            }
            RaiseUpdateApplied(this);
        }
    }
}
