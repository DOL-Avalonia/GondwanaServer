namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.DisbandFromGroup, "Disband From Group Request Handler", eClientStatus.PlayerInGame)]
    public class DisbandFromGroupHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            GamePlayer player = client.Player;
            if (player?.Group == null)
                return;

            player.Group.MemberDisband(player);
        }
    }
}