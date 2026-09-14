
namespace DOL.GS.PacketHandler.Client.v168
{
    /// <summary>
    /// Client answer (packet 0xD0) to a CheckLOSRequest. Despite the enum name this IS the response.
    /// Everything is dispatched by the player's LosCheckHandler: no TempProperties, no string keys, no RegionAction.
    /// </summary>
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.CheckLOSRequest, "Handles a LoS Check Response", eClientStatus.PlayerInGame)]
    public class CheckLOSResponseHandler : IPacketHandler
    {
        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            ushort checkerOID = packet.ReadShort();
            ushort targetOID = packet.ReadShort();
            ushort response = packet.ReadShort();
            packet.ReadShort(); // unused

            GamePlayer player = client?.Player;
            if (player?.LosCheckHandler == null) return;

            LosCheckResponse result = (response & 0x100) == 0x100 ? LosCheckResponse.True : LosCheckResponse.False;
            player.LosCheckHandler.HandleLosResponse(checkerOID, targetOID, result);
        }
    }
}