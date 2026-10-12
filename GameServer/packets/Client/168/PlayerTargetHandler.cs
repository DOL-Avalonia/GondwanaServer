using DOL.Events;
using DOL.Language;

namespace DOL.GS.PacketHandler.Client.v168
{
    /// <summary>
    /// Handles player target changes
    /// </summary>
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.PlayerTarget, "Handle Player Target Change.", eClientStatus.PlayerInGame)]
    public class PlayerTargetHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            ushort targetId = packet.ReadShort();
            ushort flags = packet.ReadShort();

            /*
			 * 0x8000 = 'examine' bit
			 * 0x4000 = LOS1 bit; is 0 if no LOS
			 * 0x2000 = LOS2 bit; is 0 if no LOS
			 * 0x0001 = players attack mode bit (not targets!)
			 */

            GamePlayer player = client.Player;
            if (player?.CurrentRegion == null) return;

            bool targetInView = (flags & (0x4000 | 0x2000)) != 0;
            bool examineTarget = (flags & 0x8000) != 0;

            GameObject target = player.CurrentRegion.GetObject(targetId);
            if (target != null && !player.IsWithinRadius(target, WorldMgr.OBJ_UPDATE_DISTANCE))
            {
                player.Out.SendObjectDelete(targetId);
                target = null;
            }

            player.TargetObject = target;
            player.TargetInView = targetInView;

            if (target != null)
            {
                if (examineTarget)
                    foreach (string message in target.GetExamineMessages(player))
                        player.Out.SendMessage(message, eChatType.CT_Missed, eChatLoc.CL_SystemWindow);

                // Then no LOS message; not sure which bit to use so use both :)
                // should be sent if targeted is using group panel to change the target
                if (!targetInView)
                    player.Out.SendMessage(LanguageMgr.GetTranslation(client, "SpellHandler.TargetNotInView"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                if (target is not GamePlayer)
                    player.Out.SendObjectUpdate(target);
            }

            if (player.IsPraying && (target is not GameGravestone gravestone || !gravestone.InternalID.Equals(player.InternalID)))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(client, "PlayerTargetHandler.Gravestone"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                player.PrayTimerStop();
            }

            GameEventMgr.Notify(GamePlayerEvent.ChangeTarget, player, null);
        }
    }
}