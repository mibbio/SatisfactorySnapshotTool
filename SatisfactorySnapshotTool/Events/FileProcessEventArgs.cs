
namespace SatisfactorySnapshotTool.Events
{
    public sealed class FileProcessEventArgs
    {
        public string Filename { get; }

        public int Counter { get; }

        public FileProcessEventArgs(string filename, int counter)
        {
            Filename = filename;
            Counter = counter;
        }
    }
}
