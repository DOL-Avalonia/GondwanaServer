using System.Reflection;
using log4net;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.CreatePlayerRequest, "Handles requests for players(0x7C) in game", eClientStatus.PlayerInGame)]
    public class PlayerCreationRequestHandler : PacketHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            uint id = client.Version >= GameClient.eClientVersion.Version1126 ? packet.ReadIntLowEndian() : packet.ReadShort();

            GameClient target = WorldMgr.GetClientFromID(id);

            if (target == null)
            {
                // A stale id (the other player just logged out) is normal: log it, do not disconnect the requester.
                if (log.IsDebugEnabled)
                    log.Debug($"Client {client.SessionID}:{client.TcpEndpointAddress} account {client.Account?.Name ?? "null"} requested invalid client {id}");
                return;
            }

            if (target.IsPlaying && target.Player != null && target.Player.ObjectState == GameObject.eObjectState.Active)
            {
                client.Out.SendPlayerCreate(target.Player);
                client.Out.SendLivingEquipmentUpdate(target.Player);
            }
        }
    }
}