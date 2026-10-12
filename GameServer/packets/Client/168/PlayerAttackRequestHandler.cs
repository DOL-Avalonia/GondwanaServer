using DOL.Language;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.PlayerAttackRequest, "Handles Player Attack Request", eClientStatus.PlayerInGame)]
    public class PlayerAttackRequestHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            bool start = packet.ReadByte() != 0;
            bool userAction = packet.ReadByte() == 0;
            GamePlayer player = client.Player;
            if (player == null) return;
            GameLoopPost.Run(static s => Handle(s.player, s.start, s.userAction), (player, start, userAction));
        }

        private static void Handle(GamePlayer player, bool start, bool userAction)
        {
            if (player.ActiveWeaponSlot == GameLiving.eActiveWeaponSlot.Distance)
            {
                if (userAction) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PlayerAttackRequestHandler.CantEnterMelee"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }
            if (start)
            {
                player.StartAttack(player.TargetObject);
                if (player.AttackState && player.TargetObject != null && !player.StayStealth) player.Stealth(false);
            }
            else player.StopAttack(false);
        }
    }
}