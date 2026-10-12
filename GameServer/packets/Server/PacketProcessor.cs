using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using DOL.GS.ServerProperties;
using DOL.Network;
using DOL.Timing;
using log4net;

namespace DOL.GS.PacketHandler
{
    /// <summary>
    /// Per-client packet processor.
    /// Inbound : bytes arrive on a network thread and are split into GSPacketIn. Handlers that require a player in game are queued
    ///           on the client and executed by ClientService on the game loop, in arrival order. Login/character-screen handlers run inline.
    /// Outbound: while the client is Playing, packets are queued and flushed once per tick by ClientService (SendPendingPackets).
    ///           Otherwise (login, character screen, world enter) they are sent immediately.
    /// </summary>
    public class PacketProcessor
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public const int TCP_SEND_BUFFER_SIZE = 8192;
        public const int UDP_SEND_BUFFER_SIZE = 1024;
        private const int MAX_TCP_PACKET_SIZE = 2048; // a single packet bigger than this crashes the client
        private const int MAX_LAST_PACKETS = 16;

        private static readonly Dictionary<string, IPacketHandler[]> m_cachedPacketHandlerSearchResults = new();
        private static readonly Dictionary<string, List<PacketHandlerAttribute>> m_cachedPreprocessorSearchResults = new();
        private static readonly object m_packetHandlerCacheLock = new();
        private static readonly AsyncCallback m_asyncTcpCallback = AsyncTcpSendCallback;

        protected readonly GameClient m_client;
        protected readonly IPacketEncoding m_encoding;
        protected IPacketHandler[] m_packetHandlers = new IPacketHandler[256];
        protected IPacketHandler m_activePacketHandler;
        protected PacketPreprocessing m_packetPreprocessor;
        private readonly bool[] m_runOnGameLoop = new bool[256];
        private readonly object m_receiveLock = new();

        // Debugging: last inbound packets (inbound packets are not pooled, so keeping them is safe).
        private readonly Queue<IPacket> m_lastPackets = new(MAX_LAST_PACKETS);
        private readonly object m_lastPacketsLock = new();

        // Outbound queue, filled from any thread, drained once per tick by ClientService.
        private readonly DrainArray<OutPacket> m_outQueue = new();

        // TCP send state.
        protected byte[] m_tcpSendBuffer;
        private readonly Queue<byte[]> m_tcpQueue = new(64);
        private readonly object m_tcpLock = new();
        private bool m_sendingTcp;

        // UDP send state.
        protected byte[] m_udpSendBuffer;
        private readonly Queue<byte[]> m_udpQueue = new(64);
        private readonly object m_udpLock = new();
        private readonly AsyncCallback m_asyncUdpCallback;
        private bool m_sendingUdp;
        private ushort m_udpCounter;

        private struct OutPacket
        {
            public const byte KIND_TCP_PACKET = 1;
            public const byte KIND_TCP_BYTES = 2;
            public const byte KIND_UDP_PACKET = 3;
            public const byte KIND_UDP_BYTES = 4;

            public byte Kind;
            public GSTCPPacketOut Tcp;
            public GSUDPPacketOut Udp;
            public byte[] Bytes;
            public bool UdpForced;

            /// <summary>Gives pooled packets back to the tick pool (no-op for packets created with 'new').</summary>
            public void Release()
            {
                Tcp?.ReleasePooledObject();
                Udp?.ReleasePooledObject();
            }
        }

        protected PacketProcessor() { }

        public PacketProcessor(GameClient client)
        {
            m_client = client ?? throw new ArgumentNullException(nameof(client));
            m_packetPreprocessor = new PacketPreprocessing();
            LoadPacketHandlers(client);
            m_encoding = client.Version < GameClient.eClientVersion.Version1110 ? new PacketEncoding168() : new PacketEncoding1110();
            m_asyncUdpCallback = AsyncUdpSendCallback;
            m_tcpSendBuffer = new byte[TCP_SEND_BUFFER_SIZE];
            m_udpSendBuffer = new byte[UDP_SEND_BUFFER_SIZE];
        }

        public IPacketEncoding Encoding => m_encoding;

        /// <summary>Kept for old GM commands. Packet handlers run on the game loop now; there are no per-connection threads to inspect.</summary>
        public static string GetConnectionThreadpoolStacks()
        {
            return "Packet handlers run on the game loop (see ClientService).";
        }

        #region Handler tables

        public virtual void LoadPacketHandlers(GameClient client)
        {
            const string baseVersion = "v168";
            LoadPacketHandlers(baseVersion, out m_packetHandlers, out List<PacketHandlerAttribute> attributes);

            Array.Clear(m_runOnGameLoop, 0, m_runOnGameLoop.Length);

            foreach (PacketHandlerAttribute pha in attributes)
            {
                m_packetPreprocessor.RegisterPacketDefinition(pha.Code, pha.PreprocessorID);

                // PacketHandlerAttribute stores the eClientStatus as PreprocessorID.
                // Every handler that requires a player in game touches world state and is therefore executed on the game loop.
                if (pha.PreprocessorID == (int)eClientStatus.PlayerInGame && pha.Code >= 0 && pha.Code < m_runOnGameLoop.Length)
                    m_runOnGameLoop[pha.Code] = true;
            }
        }

        public virtual void LoadPacketHandlers(string version, out IPacketHandler[] packetHandlers, out List<PacketHandlerAttribute> attributes)
        {
            lock (m_packetHandlerCacheLock)
            {
                if (!m_cachedPacketHandlerSearchResults.TryGetValue(version, out IPacketHandler[] cached))
                {
                    cached = new IPacketHandler[256];
                    List<PacketHandlerAttribute> found = new();

                    int count = SearchAndAddPacketHandlers(version, Assembly.GetAssembly(typeof(GameServer)), cached, found);
                    if (log.IsInfoEnabled)
                        log.Info($"PacketProcessor: Loaded {count} handlers from GameServer Assembly!");

                    count = 0;
                    foreach (Assembly asm in ScriptMgr.Scripts)
                        count += SearchAndAddPacketHandlers(version, asm, cached, found);
                    if (log.IsInfoEnabled)
                        log.Info($"PacketProcessor: Loaded {count} handlers from Script Assemblies!");

                    m_cachedPacketHandlerSearchResults[version] = cached;
                    m_cachedPreprocessorSearchResults[version] = found;
                }

                packetHandlers = (IPacketHandler[])cached.Clone();
                attributes = m_cachedPreprocessorSearchResults.TryGetValue(version, out List<PacketHandlerAttribute> list) ? list : new List<PacketHandlerAttribute>();
            }
        }

        public void RegisterPacketHandler(int packetCode, IPacketHandler handler, IPacketHandler[] packetHandlers)
        {
            if (packetHandlers[packetCode] != null && log.IsInfoEnabled)
                log.Info($"Overwriting Client Packet Code {packetCode}, with handler {handler.GetType().FullName} in PacketProcessor");

            packetHandlers[packetCode] = handler;
        }

        protected int SearchAndAddPacketHandlers(string version, Assembly assembly, IPacketHandler[] packetHandlers, List<PacketHandlerAttribute> attributes)
        {
            int count = 0;

            foreach (Type type in assembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract)
                    continue;
                if (type.GetInterface("DOL.GS.PacketHandler.IPacketHandler") == null)
                    continue;
                if (type.Namespace == null || !type.Namespace.EndsWith(version, StringComparison.OrdinalIgnoreCase))
                    continue;

                PacketHandlerAttribute[] attribs = (PacketHandlerAttribute[])type.GetCustomAttributes(typeof(PacketHandlerAttribute), true);
                if (attribs.Length == 0)
                    continue;

                count++;
                RegisterPacketHandler(attribs[0].Code, (IPacketHandler)Activator.CreateInstance(type), packetHandlers);
                attributes.Add(attribs[0]);
            }

            return count;
        }

        #endregion

        #region Inbound

        protected void SavePacket(IPacket pak)
        {
            lock (m_lastPacketsLock)
            {
                while (m_lastPackets.Count >= MAX_LAST_PACKETS)
                    m_lastPackets.Dequeue();
                m_lastPackets.Enqueue(pak);
            }
        }

        public IPacket[] GetLastPackets()
        {
            lock (m_lastPacketsLock)
                return m_lastPackets.ToArray();
        }

        /// <summary>Called by GameClient.OnReceive on a network thread.</summary>
        public void ReceiveBytes(int numBytes)
        {
            lock (m_receiveLock)
            {
                byte[] buffer = m_client.ReceiveBuffer;
                int bufferSize = m_client.ReceiveBufferOffset + numBytes;

                if (bufferSize < GSPacketIn.HDR_SIZE)
                {
                    m_client.ReceiveBufferOffset = bufferSize;
                    return;
                }

                m_client.ReceiveBufferOffset = 0;
                int curOffset = 0;

                do
                {
                    int packetLength = (buffer[curOffset] << 8) + buffer[curOffset + 1] + GSPacketIn.HDR_SIZE;
                    int dataLeft = bufferSize - curOffset;

                    if (dataLeft < packetLength)
                    {
                        Buffer.BlockCopy(buffer, curOffset, buffer, 0, dataLeft);
                        m_client.ReceiveBufferOffset = dataLeft;
                        break;
                    }

                    m_encoding.DecryptPacket(buffer, curOffset, false);

                    int packetEnd = curOffset + packetLength;
                    int calcCheck = CalculateChecksum(buffer, curOffset, packetLength - 2);
                    int pakCheck = (buffer[packetEnd - 2] << 8) | buffer[packetEnd - 1];

                    if (pakCheck != calcCheck)
                    {
                        if (log.IsWarnEnabled)
                            log.Warn($"Bad TCP packet checksum (packet:0x{pakCheck:X4} calculated:0x{calcCheck:X4}) -> disconnecting\nclient: {m_client}\ncurOffset={curOffset}; packetLength={packetLength}");

                        if (log.IsInfoEnabled)
                        {
                            log.Info("Last client received packets (from older to newer):");
                            foreach (IPacket prevPak in GetLastPackets())
                                log.Info(prevPak.ToHumanReadable());
                            log.Info(Marshal.ToHexDump("Last Received Bytes : ", buffer));
                        }

                        m_client.Disconnect();
                        return;
                    }

                    GSPacketIn pak = new GSPacketIn(packetLength - GSPacketIn.HDR_SIZE);
                    pak.Load(buffer, curOffset, packetLength);

                    try
                    {
                        HandlePacket(pak);
                    }
                    catch (Exception e)
                    {
                        if (log.IsErrorEnabled)
                            log.Error("HandlePacket(pak)", e);
                    }

                    curOffset += packetLength;
                }
                while (bufferSize - 1 > curOffset);

                if (bufferSize - 1 == curOffset)
                {
                    buffer[0] = buffer[curOffset];
                    m_client.ReceiveBufferOffset = 1;
                }
            }
        }

        public void HandlePacket(GSPacketIn packet)
        {
            if (packet == null || m_client == null)
                return;

            int code = packet.ID;
            Statistics.BytesIn += packet.PacketSize;
            Statistics.PacketsIn++;
            SavePacket(packet);

            if (code >= m_packetHandlers.Length)
            {
                if (log.IsErrorEnabled)
                    log.Error($"Received packet code 0x{code:X2} is outside of m_packetHandlers array bounds! {m_client}");
                return;
            }

            IPacketHandler handler = m_packetHandlers[code];
            if (handler == null)
                return;

            if (!m_packetPreprocessor.CanProcessPacket(m_client, packet))
            {
                if (log.IsDebugEnabled)
                    log.Debug($"PacketPreprocessor prevents handling of packet 0x{code:X2} for {m_client}");
                return;
            }

            if (m_runOnGameLoop[code] && !GameLoopPost.IsOnGameLoop)
            {
                // World-touching packet received on a network thread: run it on the game loop, in order with this client's other packets.
                m_client.Post(static s => s.processor.InvokeHandler(s.handler, s.packet), (processor: this, handler, packet));
                return;
            }

            InvokeHandler(handler, packet);
        }

        private void InvokeHandler(IPacketHandler handler, GSPacketIn packet)
        {
            // The client may have left between reception and execution.
            if (m_runOnGameLoop[packet.ID] && (m_client.Player == null || m_client.ClientState is GameClient.eClientState.Disconnected))
                return;

            m_activePacketHandler = handler;
            TickMonitor monitor = new();

            try
            {
                handler.HandlePacket(m_client, packet);
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error($"Error while processing packet (handler={handler.GetType().FullName} client={m_client})", e);
            }
            finally
            {
                m_activePacketHandler = null;
            }

            // PacketHandler-derived handlers already log their own long ticks.
            if (handler is not PacketHandler && monitor.IsLongTick(out long elapsedMs) && log.IsWarnEnabled)
                log.Warn($"({m_client.Account?.Name ?? m_client.TcpEndpoint}) {handler.GetType().Name} took {elapsedMs}ms!");
        }

        public static ushort CalculateChecksum(byte[] packet, int dataOffset, int dataSize)
        {
            byte val1 = 0x7E;
            byte val2 = 0x7E;
            int i = dataOffset;
            int len = i + dataSize;

            while (i < len)
            {
                val1 += packet[i++];
                val2 += val1;
            }

            return (ushort)(val2 - ((val1 + val2) << 8));
        }

        #endregion

        #region Outbound API

        private bool UseQueue()
        {
            return GameLoop.IsRunning && m_client.ClientState is GameClient.eClientState.Playing;
        }

        public void SendTCP(GSTCPPacketOut packet)
        {
            packet.WritePacketLength();

            if (UseQueue())
            {
                m_outQueue.Add(new OutPacket { Kind = OutPacket.KIND_TCP_PACKET, Tcp = packet });
                return;
            }

            SendTcpBuffer(packet.GetBuffer(), (int)packet.Length);
            packet.ReleasePooledObject();
        }

        public void SendTCP(byte[] buf)
        {
            if (UseQueue())
            {
                m_outQueue.Add(new OutPacket { Kind = OutPacket.KIND_TCP_BYTES, Bytes = buf });
                return;
            }

            SendTcpBuffer(buf, buf.Length);
        }

        /// <summary>Sends a private copy of an already finalized packet (use this when the same packet object is sent to several clients).</summary>
        public void SendTCPRaw(GSTCPPacketOut packet)
        {
            SendTCP(CopyPacketBytes(packet));
        }

        public virtual void SendUDP(GSUDPPacketOut packet, bool isForced)
        {
            packet.WritePacketLength();

            if (UseQueue())
            {
                m_outQueue.Add(new OutPacket { Kind = OutPacket.KIND_UDP_PACKET, Udp = packet, UdpForced = isForced });
                return;
            }

            SendUdpBuffer(packet.GetBuffer(), (int)packet.Length, isForced);
            packet.ReleasePooledObject();
        }

        public void SendUDP(byte[] buf, bool isForced)
        {
            if (UseQueue())
            {
                m_outQueue.Add(new OutPacket { Kind = OutPacket.KIND_UDP_BYTES, Bytes = buf, UdpForced = isForced });
                return;
            }

            SendUdpBuffer(buf, buf.Length, isForced);
        }

        public void SendUDPRaw(GSUDPPacketOut packet)
        {
            SendUDP(CopyPacketBytes(packet), false);
        }

        private static byte[] CopyPacketBytes(PacketOut packet)
        {
            int length = (int)packet.Length;
            byte[] copy = new byte[length];
            Buffer.BlockCopy(packet.GetBuffer(), 0, copy, 0, length);
            return copy;
        }

        /// <summary>Called once per tick by ClientService (one thread per client).</summary>
        public void SendPendingPackets()
        {
            if (!m_outQueue.Any)
                return;

            m_outQueue.DrainTo(static (p, proc) => proc.DispatchQueued(p), this);
        }

        private void DispatchQueued(OutPacket p)
        {
            try
            {
                switch (p.Kind)
                {
                    case OutPacket.KIND_TCP_PACKET:
                        SendTcpBuffer(p.Tcp.GetBuffer(), (int)p.Tcp.Length);
                        break;
                    case OutPacket.KIND_TCP_BYTES:
                        SendTcpBuffer(p.Bytes, p.Bytes.Length);
                        break;
                    case OutPacket.KIND_UDP_PACKET:
                        SendUdpBuffer(p.Udp.GetBuffer(), (int)p.Udp.Length, p.UdpForced);
                        break;
                    case OutPacket.KIND_UDP_BYTES:
                        SendUdpBuffer(p.Bytes, p.Bytes.Length, p.UdpForced);
                        break;
                }
            }
            finally
            {
                p.Release(); // bytes were copied (or the packet was dropped): the pooled object can be reused
            }
        }

        public virtual void OnDisconnect()
        {
            m_tcpSendBuffer = null;
            m_udpSendBuffer = null;

            lock (m_tcpLock)
                m_tcpQueue.Clear();
            lock (m_udpLock)
                m_udpQueue.Clear();

            // Release what is still queued. DrainArray tolerates a concurrent drain by SendPendingPackets.
            m_outQueue.DrainTo(static (p, _) => p.Release(), 0);
        }

        #endregion

        #region TCP

        /// <summary>
        /// Sends the packet contained in the first 'length' bytes of 'buf'. The real size is read from the packet header,
        /// so 'buf' may be bigger than the packet (pooled packets). 'buf' may be recycled by the caller as soon as this returns.
        /// </summary>
        private void SendTcpBuffer(byte[] buf, int length)
        {
            byte[] sendBuffer = m_tcpSendBuffer;
            if (sendBuffer == null) // disconnected / fake client
                return;

            Socket socket = m_client.Socket;
            if (socket == null || !socket.Connected)
                return;

            if (buf == null || length < 3)
                return;

            int packetLength = ((buf[0] << 8) | buf[1]) + 3;

            if (packetLength > length)
            {
                if (log.IsErrorEnabled)
                    log.Error($"Malformed outgoing TCP packet (header length {packetLength} > buffer length {length}) for {m_client}");
                return;
            }

            if (log.IsDebugEnabled)
                log.Debug(Marshal.ToHexDump($"<=== <{m_client.Account?.Name ?? m_client.TcpEndpoint}> Packet 0x{buf[2]:X2} (0x{buf[2] ^ 168:X2}) length: {packetLength}", buf, 0, packetLength));

            if (packetLength > MAX_TCP_PACKET_SIZE)
            {
                if (log.IsErrorEnabled)
                {
                    string desc = $"Sending packets longer than {MAX_TCP_PACKET_SIZE} cause client to crash, check Log for stacktrace. Packet code: 0x{buf[2]:X2}, account: {m_client.Account?.Name ?? m_client.TcpEndpoint}, packet size: {packetLength}.";
                    log.Error(Marshal.ToHexDump(desc, buf, 0, packetLength) + "\n" + Environment.StackTrace);
                }

                if (Properties.IGNORE_TOO_LONG_OUTCOMING_PACKET)
                    m_client.Out.SendMessage("ALERT: Error sending an update to your client. Oversize packet detected and discarded. Please /report this issue!", eChatType.CT_Staff, eChatLoc.CL_SystemWindow);
                else
                    GameServer.Instance.Disconnect(m_client);

                return;
            }

            m_encoding.EncryptPacket(buf, 0, false);
            Statistics.BytesOut += packetLength;
            Statistics.PacketsOut++;

            lock (m_tcpLock)
            {
                if (m_sendingTcp)
                {
                    // A send is in flight: keep our own copy, the caller may recycle 'buf' right after we return.
                    byte[] copy = new byte[packetLength];
                    Buffer.BlockCopy(buf, 0, copy, 0, packetLength);
                    m_tcpQueue.Enqueue(copy);
                    return;
                }

                m_sendingTcp = true;
                Buffer.BlockCopy(buf, 0, sendBuffer, 0, packetLength);
            }

            try
            {
                long start = MonotonicTime.NowMs;
                socket.BeginSend(sendBuffer, 0, packetLength, SocketFlags.None, m_asyncTcpCallback, m_client);
                long took = MonotonicTime.NowMs - start;

                if (took > 100 && log.IsWarnEnabled)
                    log.Warn($"SendTCP.BeginSend took {took}ms! (TCP to client: {m_client})");
            }
            catch (Exception e)
            {
                lock (m_tcpLock)
                {
                    m_sendingTcp = false;
                    m_tcpQueue.Clear();
                }

                if (log.IsWarnEnabled)
                    log.Warn($"It seems <{m_client.Account?.Name ?? "???"}> went linkdead. Closing connection. (SendTCP, {e.GetType()}: {e.Message})");

                GameServer.Instance.Disconnect(m_client);
            }
        }

        protected static void AsyncTcpSendCallback(IAsyncResult ar)
        {
            if (ar == null)
            {
                if (log.IsErrorEnabled)
                    log.Error("AsyncTcpSendCallback: ar == null");
                return;
            }

            GameClient client = (GameClient)ar.AsyncState;
            PacketProcessor proc = client?.PacketProcessor;
            if (proc == null)
                return;

            try
            {
                if (client.IsConnected && client.Socket != null)
                    client.Socket.EndSend(ar);

                byte[] data = proc.m_tcpSendBuffer;
                if (data == null) // disconnected meanwhile
                    return;

                int dataLength;

                lock (proc.m_tcpLock)
                {
                    dataLength = CombinePackets(data, proc.m_tcpQueue, 3);

                    if (dataLength <= 0)
                    {
                        proc.m_sendingTcp = false;
                        return;
                    }
                }

                long start = MonotonicTime.NowMs;
                client.Socket.BeginSend(data, 0, dataLength, SocketFlags.None, m_asyncTcpCallback, client);
                long took = MonotonicTime.NowMs - start;

                if (took > 100 && log.IsWarnEnabled)
                    log.Warn($"AsyncTcpSendCallback.BeginSend took {took}ms! (TCP to client: {client})");
            }
            catch (ObjectDisposedException)
            {
                proc.ResetTcpSendState();
                GameServer.Instance.Disconnect(client);
            }
            catch (SocketException ex)
            {
                proc.ResetTcpSendState();
                if (log.IsDebugEnabled)
                    log.Debug($"Socket exception on TCP send (Code: {ex.SocketErrorCode}) client: {client}");
                GameServer.Instance.Disconnect(client);
            }
            catch (Exception ex)
            {
                proc.ResetTcpSendState();
                if (log.IsErrorEnabled)
                    log.Error($"AsyncTcpSendCallback. client: {client}", ex);
                GameServer.Instance.Disconnect(client);
            }
        }

        private void ResetTcpSendState()
        {
            lock (m_tcpLock)
            {
                m_sendingTcp = false;
                m_tcpQueue.Clear();
            }
        }

        #endregion

        #region UDP

        /// <summary>
        /// Sends the UDP packet contained in the first 'length' bytes of 'buf' (real size read from the header).
        /// Falls back to TCP when the client has no confirmed UDP channel. 'buf' may be recycled by the caller as soon as this returns.
        /// </summary>
        private void SendUdpBuffer(byte[] buf, int length, bool isForced)
        {
            if (buf == null || length < 5)
                return;

            int packetSize = ((buf[0] << 8) | buf[1]) + 5;

            if (packetSize > length)
            {
                if (log.IsErrorEnabled)
                    log.Error($"Malformed outgoing UDP packet (header length {packetSize} > buffer length {length}) for {m_client}");
                return;
            }

            if (m_client.UdpEndPoint == null || !(isForced || m_client.UdpConfirm))
            {
                SendUdpPacketOverTcp(buf, packetSize);
                return;
            }

            // UDP is considered dead when the client has not pinged over UDP for a minute.
            // Both values are GameLoop.GameLoopTime (the old code compared it with DateTime.Now.Ticks, which disabled UDP permanently).
            if (m_client.ClientState is GameClient.eClientState.Playing && GameLoop.GameLoopTime - m_client.UdpPingTime > 60_000)
                m_client.UdpConfirm = false;

            ushort counter = ++m_udpCounter;
            buf[2] = (byte)(counter >> 8);
            buf[3] = (byte)counter;
            m_encoding.EncryptPacket(buf, 0, true);
            Statistics.BytesOut += packetSize;
            Statistics.PacketsOut++;

            byte[] sendBuffer = m_udpSendBuffer;
            if (sendBuffer == null || packetSize > sendBuffer.Length)
                return;

            lock (m_udpLock)
            {
                if (m_sendingUdp)
                {
                    byte[] copy = new byte[packetSize];
                    Buffer.BlockCopy(buf, 0, copy, 0, packetSize);
                    m_udpQueue.Enqueue(copy);
                    return;
                }

                m_sendingUdp = true;
                Buffer.BlockCopy(buf, 0, sendBuffer, 0, packetSize);
            }

            try
            {
                GameServer.Instance.SendUDP(sendBuffer, packetSize, m_client.UdpEndPoint, m_asyncUdpCallback);
            }
            catch (Exception e)
            {
                int dropped = ResetUdpSendState();
                m_client.UdpConfirm = false;

                if (log.IsErrorEnabled)
                    log.Error($"Error while sending UDP ({dropped} packets dropped) to {m_client}", e);
            }
        }

        /// <summary>UDP layout [len:2][sequence:2][code:1][payload] -> TCP layout [len:2][code:1][payload].</summary>
        private void SendUdpPacketOverTcp(byte[] buf, int packetSize)
        {
            byte[] tcp = new byte[packetSize - 2];
            tcp[0] = buf[0];
            tcp[1] = buf[1];
            Buffer.BlockCopy(buf, 4, tcp, 2, packetSize - 4);
            SendTcpBuffer(tcp, tcp.Length);
        }

        private void AsyncUdpSendCallback(IAsyncResult ar)
        {
            try
            {
                Socket socket = (Socket)ar.AsyncState;
                socket.EndSendTo(ar);

                byte[] data = m_udpSendBuffer;
                if (data == null)
                    return;

                int packetSize;

                lock (m_udpLock)
                {
                    packetSize = CombinePackets(data, m_udpQueue, 5);

                    if (packetSize <= 0)
                    {
                        m_sendingUdp = false;
                        return;
                    }
                }

                GameServer.Instance.SendUDP(data, packetSize, m_client.UdpEndPoint, m_asyncUdpCallback);
            }
            catch (Exception e)
            {
                int dropped = ResetUdpSendState();

                if (log.IsErrorEnabled)
                    log.Error($"AsyncUdpSendCallback ({dropped} packets dropped) client: {m_client}", e);
            }
        }

        private int ResetUdpSendState()
        {
            lock (m_udpLock)
            {
                int count = m_udpQueue.Count;
                m_udpQueue.Clear();
                m_sendingUdp = false;
                return count;
            }
        }

        #endregion

        /// <summary>Moves as many queued packets as fit into 'buffer'. Returns the number of bytes written (0 = nothing queued).</summary>
        private static int CombinePackets(byte[] buffer, Queue<byte[]> queue, int headerSize)
        {
            int written = 0;

            while (queue.Count > 0)
            {
                byte[] pak = queue.Peek();
                int len = ((pak[0] << 8) | pak[1]) + headerSize;

                if (len > buffer.Length)
                {
                    if (log.IsErrorEnabled)
                        log.Error($"Dropping queued packet: size {len} exceeds send buffer {buffer.Length}");
                    queue.Dequeue();
                    continue;
                }

                if (written + len > buffer.Length)
                    break;

                Buffer.BlockCopy(pak, 0, buffer, written, len);
                written += len;
                queue.Dequeue();
            }

            return written;
        }
    }
}