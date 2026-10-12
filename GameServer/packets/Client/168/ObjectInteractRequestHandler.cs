namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.ObjectInteractRequest, "Handles Client Interact Request", eClientStatus.PlayerInGame)]
    public class ObjectInteractRequestHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            packet.ReadInt();   // playerX
            packet.ReadInt();   // playerY
            packet.ReadShort(); // sessionId
            ushort targetOid = packet.ReadShort();

            //TODO: utilize these client-sent coordinates to possibly check for exploits which are spoofing position packets but not spoofing them everywhere
            GamePlayer player = client.Player;
            Region region = player?.CurrentRegion;
            if (region == null) return;

            GameObject obj = region.GetObject(targetOid);
            if (obj == null || !player.IsWithinRadius(obj, WorldMgr.OBJ_UPDATE_DISTANCE))
                player.Out.SendObjectDelete(targetOid);
            else
                player.InteractWith(obj);
        }
    }
}