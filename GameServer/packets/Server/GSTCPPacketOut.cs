using DOL.Network;

namespace DOL.GS.PacketHandler
{
    /// <summary>
    /// An outgoing TCP packet
    /// </summary>
    public class GSTCPPacketOut : PacketOut, IPooledObject<GSTCPPacketOut>
    {
        private byte m_packetCode;

        /// <summary>
        /// This Packet Byte Handling Code
        /// </summary>
        public byte PacketCode
        {
            get { return m_packetCode; }
        }

        public long IssuedTimestamp { get; set; }

        /// <summary>
        /// Pool constructor. Always call Init(packetCode) after obtaining from the pool.
        /// </summary>
        public GSTCPPacketOut() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="packetCode">ID of the packet</param>
        public GSTCPPacketOut(byte packetCode)
        {
            Init(packetCode);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="packetCode">ID of the packet</param>
        public GSTCPPacketOut(byte packetCode, int startingSize) : base(startingSize + 3)
        {
            Init(packetCode);
        }

        public GSTCPPacketOut Init(byte packetCode)
        {
            m_packetCode = packetCode;
            Position = 0;
            SetLength(0);
            WriteShort(0x00);
            WriteByte(packetCode);
            return this;
        }

        public override ushort WritePacketLength()
        {
            Position = 0;
            WriteShort((ushort)(Length - 3));
            return (ushort)(Length - 3);
        }

        protected override void Dispose(bool disposing)
        {
            IssuedTimestamp = 0;
        }

        public override string ToString()
        {
            return base.ToString() + $": Size={Length - 3} ID=0x{m_packetCode:X2}";
        }
    }
}