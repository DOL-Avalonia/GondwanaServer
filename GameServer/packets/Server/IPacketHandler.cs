using DOL.GS;
using log4net;
using System.Reflection;

namespace DOL.GS.PacketHandler
{
    /// <summary>
    /// The interface for all received packets
    /// </summary>
    public interface IPacketHandler
    {
        /// <summary>
        /// Handles every received packet
        /// </summary>
        /// <param name="client">The client that sent the packet</param>
        /// <param name="packet">The received packet data</param>
        /// <returns></returns>
        void HandlePacket(GameClient client, GSPacketIn packet);
    }

    /// <summary>
    /// Base handler with long-tick monitoring (OpenDAoC style).
    /// Existing handlers may keep implementing IPacketHandler directly.</summary>
    public abstract class PacketHandler : IPacketHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            TickMonitor monitor = new();
            HandlePacketInternal(client, packet);

            if (monitor.IsLongTick(out long elapsedMs) && log.IsWarnEnabled)
            {
                string context = GetLogContext(client, packet);
                string contextMsg = string.IsNullOrEmpty(context) ? string.Empty : $" Context: {context}";
                log.Warn($"Long {GetType().Name} ({(eClientPackets)packet.ID}) for {client.Player?.Name}({client.Player?.ObjectID}){contextMsg} Time: {elapsedMs}ms");
            }
        }

        protected abstract void HandlePacketInternal(GameClient client, GSPacketIn packet);
        protected virtual string GetLogContext(GameClient client, GSPacketIn packet)
        {
            return null;
        }
    }
}
