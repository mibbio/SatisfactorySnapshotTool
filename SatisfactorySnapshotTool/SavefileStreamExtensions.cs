namespace SatisfactorySnapshotTool
{
    using System;
    using System.IO;
    using System.Text;

    public static class SavefileStreamExtensions
    {
        private const string EX_MSG_STREAM_CLOSED = "Source stream is not readable or already closed.";
        private const string EX_MSG_STREAM_INSUFFICENT_DATA = "Not enough data remaining in source stream.";

        private static readonly byte[] intBuffer = new byte[4];
        private static readonly byte[] longBuffer = new byte[8];

        public static int ReadInt(this Stream source)
        {
            if (!source.CanRead)
            {
                throw new ArgumentException(EX_MSG_STREAM_CLOSED);
            }
            if (source.Length - source.Position < intBuffer.Length)
            {
                throw new InvalidOperationException(EX_MSG_STREAM_INSUFFICENT_DATA);
            }
            try
            {
                source.Read(intBuffer, 0, intBuffer.Length);
                return BitConverter.ToInt32(intBuffer, 0);
            }
            catch (Exception e)
            {
                throw new IOException("Error while reading int value from savegame.", e);
            }
        }

        public static long ReadLong(this Stream source)
        {
            if (!source.CanRead)
            {
                throw new ArgumentException(EX_MSG_STREAM_CLOSED);
            }
            if (source.Length - source.Position < longBuffer.Length)
            {
                throw new InvalidOperationException(EX_MSG_STREAM_INSUFFICENT_DATA);
            }

            try
            {
                source.Read(longBuffer, 0, longBuffer.Length);
                return BitConverter.ToInt64(longBuffer, 0);
            }
            catch (Exception e)
            {
                throw new IOException("Error while reading long value from savegame.", e);
            }
        }

        public static string ReadSatisfactoryString(this Stream source)
        {
            try
            {
                int rawLength = source.ReadInt();
                int realLength = Math.Abs(rawLength);
                byte[] buffer = new byte[realLength];
                if (source.Length - source.Position < realLength)
                {
                    throw new InvalidOperationException(EX_MSG_STREAM_INSUFFICENT_DATA);
                }
                source.Read(buffer, 0, realLength);

                var encoding = (rawLength < 0) ? Encoding.Unicode : Encoding.UTF8;
                return encoding.GetString(buffer).Trim('\0');
            }
            catch (Exception e)
            {
                throw new IOException("Error while reading string value.", e);
            }
        }
    }
}
