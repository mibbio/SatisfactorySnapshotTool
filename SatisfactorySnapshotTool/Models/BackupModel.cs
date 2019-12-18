namespace SatisfactorySnapshotTool.Models
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    using SatisfactorySnapshotTool.Backup;
    using SatisfactorySnapshotTool.Mvvm;

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public enum GameBranch
    {
        Stable,
        Experimental
    }

    [JsonObject(MemberSerialization = MemberSerialization.OptOut)]
    public class BackupModel : NotifyPropertyChangedBase
    {
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
            return Dependencies[guid].Add(file);
        }

        public bool AddSave(SavegameHeader savegameHeader)
        {
            if (savegameHeader == null) throw new ArgumentNullException();
            if (!Saves.Contains(savegameHeader))
            {
                Saves.Add(savegameHeader);
                return true;
            }
            return false;
        }
    }
}
