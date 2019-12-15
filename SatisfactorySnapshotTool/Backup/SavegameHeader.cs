namespace SatisfactorySnapshotTool.Backup
{
    using SatisfactorySnapshotTool;

    using System;
    using System.IO;

    public class SavegameHeader
    {
        public string Filename { get; private set; }

        public int HeaderVersion { get; private set; }

        public int SaveVersion { get; private set; }

        public int BuildVersion { get; private set; }

        public string SessionName { get; private set; }

        public string StartLocation { get; private set; }

        public TimeSpan PlayTime { get; private set; }

        public DateTime SaveDate { get; private set; }

        public SavegameHeader(string path)
        {
            if (!File.Exists(path)) throw new ArgumentException("File does not exist!");

            Filename = Path.GetFileName(path);

            try
            {
                using (var input = File.OpenRead(path))
                {
                    HeaderVersion = input.ReadInt();
                    SaveVersion = input.ReadInt();
                    BuildVersion = input.ReadInt();
                    // skip world type
                    input.ReadSatisfactoryString();

                    var rawProps = input.ReadSatisfactoryString().Trim('\0', '?');
                    foreach (var p in rawProps.Split('?'))
                    {
                        var kv = p.Split('=');
                        if (kv[0] == "startloc")
                        {
                            StartLocation = kv[1];
                        }
                    }

                    SessionName = input.ReadSatisfactoryString();
                    PlayTime = TimeSpan.FromSeconds(input.ReadInt());
                    SaveDate = new DateTime(input.ReadLong(), DateTimeKind.Utc);
                }
            }
            catch (IOException)
            {
                throw;
            }
        }
    }
}
