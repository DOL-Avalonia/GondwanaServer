using DOL.Network;

namespace DOL.GS.PacketHandler
{
    /// <summary>
    /// Outgoing game server UDP packet
    /// </summary>
    public class GSUDPPacketOut : PacketOut, IPooledObject<GSUDPPacketOut>
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
        public GSUDPPacketOut() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="packetCode">ID of the packet</param>
        public GSUDPPacketOut(byte packetCode)
        {
            Init(packetCode);
        }

        public GSUDPPacketOut Init(byte packetCode)
        {
            m_packetCode = packetCode;
            Position = 0;
            SetLength(0);
            WriteShort(0x00);
            WriteShort(0x00);
            WriteByte(packetCode);
            return this;
        }

        /// <summary>
        /// Calculates the packet size and prepends it
        /// </summary>
        /// <returns>The packet size</returns>
        public override ushort WritePacketLength()
        {
            Position = 0;
            WriteShort((ushort)(Length - 5));
            return (ushort)(Length - 5);
        }

        protected override void Dispose(bool disposing)
        {
            IssuedTimestamp = 0;
        }

        public override string ToString()
        {
            return base.ToString() + $": Size={Length - 5} ID=0x{m_packetCode:X2}";
        }
    }
}