
namespace SatisfactorySnapshotTool.Backup
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class BackupFileGroup
    {
        #region fields
        public string Version;

        public int Build;
        #endregion

        #region properties
        public List<string> NeededDirectories { get; }

        public HashSet<string> FilesToCopy { get; }

        public Dictionary<string, string> Checksums { get; }

        public Dictionary<string, Guid> Dependencies { get; }
        #endregion

        #region constructors
        public BackupFileGroup()
        {
            NeededDirectories = new List<string>();
            FilesToCopy = new HashSet<string>();
            Checksums = new Dictionary<string, string>();
            Dependencies = new Dictionary<string, Guid>();
        }
        #endregion

        #region methods
        public void AddDirectory(string path)
        {
            if (NeededDirectories.Contains(path)) return;
            for (int i = 0; i < NeededDirectories.Count; i++)
            {
                // current path already covered by path in list
                if (NeededDirectories[i].StartsWith(path)) return;
                // replace existing path as it is covered by current path
                if (path.StartsWith(NeededDirectories[i]))
                {
                    NeededDirectories[i] = path;
                    return;
                }
            }
            NeededDirectories.Add(path);
        }

        public bool AddFile(string path)
        {
            AddDirectory(Path.GetDirectoryName(path));
            return FilesToCopy.Add(path);
        }

        public bool AddFile(string path, string checksum)
        {
            if (!Checksums.ContainsKey(checksum))
            {
                Checksums.Add(checksum, path);
            }
            return AddFile(path);
        }

        public bool AddDependency(Guid backupId, string path, string checksum)
        {
            if (!Dependencies.ContainsKey(path))
            {
                Dependencies.Add(path, backupId);
                if (!Checksums.ContainsKey(checksum))
                {
                    Checksums.Add(checksum, path);
                }
                AddDirectory(Path.GetDirectoryName(path));
                return true;
            }
            return false;
        }
        #endregion
    }
}
