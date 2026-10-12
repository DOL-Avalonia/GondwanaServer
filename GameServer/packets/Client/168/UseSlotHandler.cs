using DOL.GS.Geometry;

namespace DOL.GS.PacketHandler.Client.v168
{
    /// <summary>
    /// Handles spell cast requests from client
    /// </summary>
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.UseSlot, "Handle Player Use Slot Request.", eClientStatus.PlayerInGame)]
    public class UseSlotHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            GamePlayer player = client.Player;
            if (player == null) return;

            if (client.Version >= GameClient.eClientVersion.Version1124)
            {
                int x = (int)packet.ReadFloatLowEndian();
                int y = (int)packet.ReadFloatLowEndian();
                int z = (int)packet.ReadFloatLowEndian();
                player.CurrentSpeed = (short)packet.ReadFloatLowEndian();
                ushort heading = packet.ReadShort();
                player.Position = Position.Create(player.Position.RegionID, x, y, z, heading);
            }

            int flagSpeedData = packet.ReadShort();
            int slot = packet.ReadByte();
            int type = packet.ReadByte();

            player.SetCurrentSpeed((flagSpeedData & 0x200) != 0 ? (short)-(flagSpeedData & 0x1ff) : (short)(flagSpeedData & 0x1ff));
            player.IsStrafing = (flagSpeedData & 0x4000) != 0;
            player.TargetInView = (flagSpeedData & 0xa000) != 0;
            player.GroundTargetInView = (flagSpeedData & 0x1000) != 0;
            player.UseSlot(slot, type);
        }
    }
}