using DOL.GS.Geometry;
using DOL.Language;
using System.Numerics;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.PlayerGroundTarget, "Handles Player Ground Target Settings", eClientStatus.PlayerInGame)]
    public class PlayerGroundTargetHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            int groundX = (int)packet.ReadInt();
            int groundY = (int)packet.ReadInt();
            int groundZ = (int)packet.ReadInt();
            ushort flag = packet.ReadShort();
            //			ushort unk2 = packet.ReadShort();

            GamePlayer player = client.Player;
            if (player == null)
                return;

            HandleGroundTargetChange(player, groundX, groundY, groundZ, flag);
        }

        /// <summary>
        /// Handles ground target changes. Runs on the game loop.
        /// </summary>
        private static void HandleGroundTargetChange(GamePlayer player, int groundX, int groundY, int groundZ, ushort flag)
        {
            player.GroundTargetInView = ((flag & 0x100) != 0);
            player.GroundTargetPosition = Position.Create(player.Position.RegionID, groundX, groundY, groundZ);

            if (!player.GroundTargetInView)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player, "PlayerGroundTargetHandler.GroundTargetNotVisible"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            if (player.SiegeWeapon != null)
            {
                player.SiegeWeapon.Move();
                return;
            }

            if (player.Steed != null && player.Steed.MAX_PASSENGERS >= 1)
            {
                if (player.Steed is GameTaxiBoat) return;

                if (player.Steed is GameBoat)
                // Ichi - && player.GroundTarget.Z > player.CurrentZone.ZoneRegion.WaterLevel) return;
                {
                    if (player.Steed.OwnerID == player.InternalID)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player, "PlayerGroundTargetHandler.BoatMoveOrder"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        player.Steed.PathTo(player.GroundTargetPosition.Coordinate, player.Steed.MaxSpeed);
                        return;
                    }
                }

                if (player.Steed.MAX_PASSENGERS > 8 && player.Steed.CurrentRiders.Length < player.Steed.REQUIRED_PASSENGERS)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player, "PlayerGroundTargetHandler.NotEnoughPassengers", player.Steed.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                player.Steed.PathTo(player.GroundTargetPosition.Coordinate, player.Steed.MaxSpeed);
                return;
            }
        }
    }
}