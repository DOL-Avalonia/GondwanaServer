
namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.PlayerSitRequest, "Handles Player Sit Request.", eClientStatus.PlayerInGame)]
    public class PlayerSitRequestHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            bool sit = packet.ReadByte() != 0x00;
            GamePlayer player = client.Player;
            if (player == null)
                return;

            GameLoopPost.Run(static s => s.player.Sit(s.sit), (player, sit));
        }
    }
}