namespace SatisfactorySnapshotTool.Models
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    using SatisfactorySnapshotTool.Backup;
    using SatisfactorySnapshotTool.Mvvm;

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;

    public enum GameBranch
    {
        None,
        Stable,
        Experimental
    }

    [JsonObject(MemberSerialization = MemberSerialization.OptOut)]
    public class BackupModel : NotifyPropertyChangedBase
    {
        public const string BinarySubdir = "game";

        public const string SavesSubdir = "saves";

        private const string BackupInfoFile = "backup.json";

        private static string _backupRootPath = string.Empty;

        private long _backupSize = 0;

        [JsonIgnore]
        public Guid Guid { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public GameBranch Branch { get; set; }

        public int Build { get; set; }

        public DateTime CreatedAt { get; set; }

        public long BackupSize
        {
            get => _backupSize;
            set
            {
                if (_backupSize == value) return;
                _backupSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReadableBackupSize));
            }
        }

        [JsonIgnore]
        public string ReadableBackupSize
        {
            get
            {
                var readableSize = FileHelper.GetHumanReadableSize(BackupSize);
                return string.Format("{0:n2} {1}", readableSize.Item1, readableSize.Item2);
            }
        }

        public Dictionary<string, string> Checksums { get; private set; }

        public Dictionary<Guid, HashSet<string>> Dependencies { get; private set; }

        [JsonIgnore]
        public IEnumerable<string> Files
        {
            get
            {
                var shared = Dependencies.Values.SelectMany(x => x).ToList();
                return Checksums.Select(cs => cs.Value).Except(shared);
            }
        }

        [JsonIgnore]
        public Tuple<int, int> DependencyCount => Tuple.Create(Dependencies.Count, Dependencies.Sum(kvp => kvp.Value.Count));

        [JsonIgnore]
        public ObservableCollection<SavegameHeader> Saves { get; private set; } = new ObservableCollection<SavegameHeader>();

        [JsonIgnore]
        public static BackupModel Empty => new BackupModel();

        private BackupModel()
        {
            Guid = Guid.NewGuid();
            Checksums = new Dictionary<string, string>();
            Dependencies = new Dictionary<Guid, HashSet<string>>();
            Branch = GameBranch.None;
            Build = 0;
            CreatedAt = DateTime.UtcNow;
        }

        public BackupModel(Guid guid)
        {
            Guid = guid;
            Checksums = new Dictionary<string, string>();
            Dependencies = new Dictionary<Guid, HashSet<string>>();
        }

        [JsonConstructor]
        public BackupModel(Guid guid, GameBranch branch, int build, DateTime createdAt) : this(guid)
        {
            Branch = branch;
            Build = build;
            CreatedAt = createdAt;
        }

        public bool AddChecksum(string file, string checksum)
        {
            try
            {
                Checksums.Add(file, checksum);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is ArgumentNullException)
            {
                return false;
            }
        }

        public bool AddChecksums(IDictionary<string, string> checksums)
        {
            try
            {
                Checksums = Checksums
                        .Concat(checksums)
                        .GroupBy(i => i.Key)
                        .ToDictionary(
                        group => group.Key,
                        group => group.Last().Value
                        );
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is ArgumentNullException)
            {
                return false;
            }
        }

        public bool AddDependency(Guid guid, string file)
        {
            if (!Dependencies.ContainsKey(guid))
            {
                Dependencies.Add(guid, new HashSet<string>());
            }
            OnPropertyChanged(nameof(Dependencies));
            OnPropertyChanged(nameof(DependencyCount));
            return Dependencies[guid].Add(file);
        }

        public void RemoveDependency(Guid guid)
        {
            if (string.IsNullOrEmpty(_backupRootPath)) throw new InvalidOperationException("No backup root path set.");
            if (Dependencies.ContainsKey(guid))
            {
                var backupPath = Path.Combine(_backupRootPath, Guid.ToString(), BinarySubdir);
                BackupSize += Dependencies[guid].Sum(file =>
                {
                    var fi = new FileInfo(Path.Combine(backupPath, file));
                    return fi.Length;
                });
                Dependencies.Remove(guid);
                OnPropertyChanged(nameof(Dependencies));
                OnPropertyChanged(nameof(DependencyCount));

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(Path.Combine(_backupRootPath, Guid.ToString(), "backup.json"), json);
            }
        }

        public bool AddSavegame(SavegameHeader savegameHeader)
        {
            if (savegameHeader == null) throw new ArgumentNullException();
            if (!Saves.Contains(savegameHeader))
            {
                Saves.Add(savegameHeader);
                return true;
            }
            return false;
        }

        public void Save(string path)
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            var infoFile = Path.Combine(path, Guid.ToString(), BackupInfoFile);
            try
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(infoFile, json);
            }
            catch (Exception ex)
            {
                throw new IOException("Error while writing backup info.", ex);
            }
        }

        public static BackupModel Load(string path)
        {
            var infoFile = Path.Combine(path, BackupInfoFile);

            if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                throw new ArgumentException("Path is not a directory.");

            if (!File.Exists(infoFile))
                throw new ArgumentException("Invalid backup directory.");

            var model = new BackupModel(Guid.Parse(Path.GetFileNameWithoutExtension(path)));
            try
            {
                var json = File.ReadAllText(Path.Combine(path, "backup.json"));
                JsonConvert.PopulateObject(json, model);
                foreach (var dir in Directory.EnumerateDirectories(Path.Combine(path, SavesSubdir)))
                {
                    foreach (var save in Directory.EnumerateFiles(dir))
                    {
                        model.AddSavegame(new SavegameHeader(save));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new IOException("Error while loading backup info", ex);
            }
            return model;
        }

        public static void SetBackupRoot(string path)
        {
            if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory))
            {
                path = Path.GetDirectoryName(path);
            }
            if (!Directory.Exists(path)) throw new ArgumentException("Path does not exist.");
            _backupRootPath = path;
        }
    }
}
