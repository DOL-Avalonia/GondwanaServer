using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace DOL.Network
{
    public class PacketOut : MemoryStream, IPacket
    {
        protected PacketOut() { }

        public PacketOut(int size) : base(size) { }

        public string ToHumanReadable()
        {
            return Marshal.ToHexDump(ToString(), ToArray());
        }

        public void WriteShort(ushort val)
        {
            WriteByte((byte)(val >> 8));
            WriteByte((byte)(val & 0xff));
        }

        public void WriteShortLowEndian(ushort val)
        {
            WriteByte((byte)(val & 0xff));
            WriteByte((byte)(val >> 8));
        }

        public void WriteInt(uint val)
        {
            WriteByte((byte)(val >> 24));
            WriteByte((byte)((val >> 16) & 0xff));
            WriteByte((byte)((val & 0xffff) >> 8));
            WriteByte((byte)((val & 0xffff) & 0xff));
        }

        public void WriteIntLowEndian(uint val)
        {
            WriteByte((byte)((val & 0xffff) & 0xff));
            WriteByte((byte)((val & 0xffff) >> 8));
            WriteByte((byte)((val >> 16) & 0xff));
            WriteByte((byte)(val >> 24));
        }

        public void WriteLong(ulong val)
        {
            WriteByte((byte)(val >> 56));
            WriteByte((byte)((val >> 48) & 0xff));
            WriteByte((byte)((val >> 40) & 0xff));
            WriteByte((byte)((val >> 32) & 0xff));
            WriteByte((byte)((val >> 24) & 0xff));
            WriteByte((byte)((val >> 16) & 0xff));
            WriteByte((byte)((val >> 8) & 0xff));
            WriteByte((byte)(val & 0xff));
        }

        public void WriteLongLowEndian(ulong val)
        {
            WriteByte((byte)(val & 0xff));
            WriteByte((byte)((val >> 8) & 0xff));
            WriteByte((byte)((val >> 16) & 0xff));
            WriteByte((byte)((val >> 24) & 0xff));
            WriteByte((byte)((val >> 32) & 0xff));
            WriteByte((byte)((val >> 40) & 0xff));
            WriteByte((byte)((val >> 48) & 0xff));
            WriteByte((byte)(val >> 56));
        }

        public void WriteFloatLowEndian(float val)
        {
            uint i = BitConverter.SingleToUInt32Bits(val);
            WriteByte((byte)(i & 0xFF));
            WriteByte((byte)((i >> 8) & 0xFF));
            WriteByte((byte)((i >> 16) & 0xFF));
            WriteByte((byte)((i >> 24) & 0xFF));
        }

        public virtual byte GetChecksum()
        {
            byte val = 0;
            byte[] buf = GetBuffer();
            for (int i = 0; i < Position - 6; ++i)
                val += buf[i + 8];
            return val;
        }

        public void Fill(byte val, int num)
        {
            for (int i = 0; i < num; ++i)
                WriteByte(val);
        }

        /// <summary>
        /// Writes the payload length into the 2-byte header.
        /// IMPORTANT: the buffer is no longer shrunk to the packet size. Always use (int)Length for the packet size,
        /// never GetBuffer().Length (the internal buffer may be bigger than the packet).
        /// </summary>
        public virtual ushort WritePacketLength()
        {
            Position = 0;
            WriteShort((ushort)(Length - 3));
            return (ushort)(Length - 3);
        }

        /// <summary>
        /// Encodes 'str' with the server encoding, writes at most 'maxBytes' bytes, without allocating a byte[].
        /// </summary>
        private void WriteEncodedString(string str, int maxBytes, bool writePascalLength)
        {
            Encoding enc = Constants.DefaultEncoding;
            int byteCount = enc.GetByteCount(str);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, byteCount));
            try
            {
                int written = enc.GetBytes(str, 0, str.Length, buffer, 0);
                int len = Math.Min(written, maxBytes);
                if (writePascalLength)
                    WriteByte((byte)len);
                Write(buffer, 0, len);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void WritePascalString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                WriteByte(0);
                return;
            }

            WriteEncodedString(str, 255, true);
        }

        public void WritePascalStringIntLE(string str, int maxlen = 2048)
        {
            if (string.IsNullOrEmpty(str))
            {
                WriteIntLowEndian(0);
                return;
            }

            int len = Math.Min(maxlen - 1, Constants.DefaultEncoding.GetByteCount(str));
            WriteIntLowEndian((uint)len + 1);
            WriteEncodedString(str, len, false);
            WriteByte(0);
        }

        public void WriteString(string str)
        {
            WriteStringBytes(str);
            WriteByte(0x0);
        }

        public void WriteStringBytes(string str)
        {
            if (string.IsNullOrEmpty(str))
                return;

            WriteEncodedString(str, int.MaxValue, false);
        }

        /// <summary>Was using Encoding.Default (UTF-8): accents were garbled on the client. Now uses the server encoding like everything else.</summary>
        public void WriteNonNullTerminatedString(string str)
        {
            WriteStringBytes(str);
        }

        public void WriteString(string str, int maxlen)
        {
            if (string.IsNullOrEmpty(str))
                return;

            WriteEncodedString(str, maxlen, false);
        }

        public void FillString(string str, int len)
        {
            long pos = Position;
            Fill(0x0, len);

            if (string.IsNullOrEmpty(str))
            {
                Position = pos + len;
                return;
            }

            Position = pos;
            WriteEncodedString(str, len, false);
            Position = pos + len;
        }

        public override string ToString()
        {
            return GetType().Name;
        }
    }
}