namespace SatisfactorySnapshotTool
{
    using System;
    using System.Runtime.InteropServices;

    internal static class Win32Interop
    {
        // VersionInfo
        [DllImport("version.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int GetFileVersionInfoSize(string sFileName, out int handle);

        [DllImport("version.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool GetFileVersionInfo(string sFileName, int handle, int size, byte[] bData);

        [DllImport("version.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool VerQueryValue(byte[] bBlock, string sSubBlock, out IntPtr buffer, out int len);

        // HardLink
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateHardLink(string sFilename, string sExistingFilename, IntPtr pSecurityAttributes);
    }
}
