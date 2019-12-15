namespace SatisfactorySnapshotTool
{
    using SatisfactorySnapshotTool.Events;

    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public static class FileHelper
    {
        public static readonly string[] Units = new string[] { "B", "KB", "MB", "GB", "TB" };

        public static readonly string[] GameExecutableNames = new string[] { "FactoryGame.exe" };

        public static string GetMD5WithProgress(string path, EventHandler<FileProgressEventArgs> notifyHandler, CancellationToken ct)
        {
            if (!File.Exists(path)) return string.Empty;
            try
            {
                using (var stream = new MonitoredFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    stream.CancellationToken = ct;
                    if (notifyHandler != null)
                    {
                        stream.OnPositionChange += notifyHandler;
                    }
                    using (var algo = new MD5Cng())
                    {
                        byte[] checksumBuffer = algo.ComputeHash(stream);
                        return BitConverter.ToString(checksumBuffer).Replace("-", string.Empty).ToLower();
                    }
                }
            }
            catch (TaskCanceledException)
            {
                throw;
            }
        }

        public static void CreateHardLink(string sourcePath, string targetPath)
        {
            if (!File.Exists(targetPath)) throw new ArgumentException("Target does not exist.");

            if (!Win32Interop.CreateHardLink(sourcePath, targetPath, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), string.Format("{0}\n\t=> {1}", sourcePath, targetPath));
            }
        }

        //TODO win32 error handling
        // based on https://stackoverflow.com/a/43229358
        public static bool TryGetBuild(string path, out string version, out int build)
        {
            version = string.Empty;
            build = -1;

            int size = Win32Interop.GetFileVersionInfoSize(path, out int handle);
            if (size > 0)
            {
                var bytes = new byte[size];
                if (Win32Interop.GetFileVersionInfo(path, 0, size, bytes))
                {
                    uint[] langs;
                    if (Win32Interop.VerQueryValue(bytes, @"\VarFileInfo\Translation", out IntPtr ptr, out int size2))
                    {
                        langs = new uint[size2 / 4];
                        for (int i = 0, j = 0; j < size2; i++, j += 4)
                        {
                            langs[i] = unchecked((uint)(((ushort)Marshal.ReadInt16(ptr, j) << 16) | (ushort)Marshal.ReadInt16(ptr, j + 2)));
                        }
                    }
                    else
                    {
                        langs = new uint[] { 0x040904B0, 0x040904E4, 0x04090000 };
                    }

                    string[] langs2 = Array.ConvertAll(langs, x => @"\StringFileInfo\" + x.ToString("X8") + @"\");

                    foreach (var lang in langs2)
                    {
                        var success = Win32Interop.VerQueryValue(bytes, lang + "ProductVersion", out ptr, out size2);
                        if (success)
                        {
                            version = Marshal.PtrToStringUni(ptr);
                            var match = Regex.Match(version, @".*?(\d{5,}).*");
                            if (match.Success)
                            {
                                build = int.Parse(match.Groups[1].Value);
                                return true;
                            }
                            break;
                        }
                    }
                }
            }
            return false;
        }

        public static Tuple<float, string> GetHumanReadableSize(float sizeInBytes)
        {
            int exponent = 0;
            while (sizeInBytes > 1000f && exponent < Units.Length)
            {
                sizeInBytes /= 1024f;
                exponent++;
            }

            return Tuple.Create(sizeInBytes, Units[exponent]);
        }

        public static Tuple<float, float, string> GetHumanReadableSize(float sizeInBytes1, float sizeInBytes2)
        {
            var highSize = Math.Max(sizeInBytes1, sizeInBytes2);
            var lowSize = Math.Min(sizeInBytes1, sizeInBytes2);

            int exponent = 0;
            while (highSize > 1000f && exponent < Units.Length)
            {
                highSize /= 1024f;
                exponent++;
            }

            lowSize /= (float)Math.Pow(1024, exponent);
            return Tuple.Create(lowSize, highSize, Units[exponent]);
        }
    }
}
