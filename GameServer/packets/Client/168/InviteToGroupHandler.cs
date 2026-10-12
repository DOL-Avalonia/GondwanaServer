
using AmteScripts.Managers;
using DOL.Language;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.InviteToGroup, "Handle Invite to Group Request.", eClientStatus.PlayerInGame)]
    public class InviteToGroupHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            GamePlayer player = client.Player;
            if (player == null)
                return;

            if (player.TargetObject == null || player.TargetObject == player || player.TargetObject is not GamePlayer target)
            {
                ChatUtil.SendSystemMessage(player, LanguageMgr.GetTranslation(player.Client.Account.Language, "InviteToGroupHandler.InvalidTarget"));
                return;
            }

            if (player.Group != null && player.Group.Leader != player)
            {
                ChatUtil.SendSystemMessage(player, LanguageMgr.GetTranslation(player.Client.Account.Language, "InviteToGroupHandler.NotLeader"));
                return;
            }

            if (player.Group != null && player.Group.MemberCount >= ServerProperties.Properties.GROUP_MAX_MEMBER)
            {
                ChatUtil.SendSystemMessage(player, LanguageMgr.GetTranslation(player.Client.Account.Language, "InviteToGroupHandler.GroupFull"));
                return;
            }

            if (!GameServer.ServerRules.IsAllowedToGroup(player, target, false))
                return;

            if (!PvpManager.CanGroup(player, player, false))
                return;

            if (target.Group != null)
            {
                ChatUtil.SendSystemMessage(player, LanguageMgr.GetTranslation(player.Client.Account.Language, "InviteToGroupHandler.TargetInGroup"));
                return;
            }

            ChatUtil.SendSystemMessage(player, LanguageMgr.GetTranslation(player.Client.Account.Language, "InviteToGroupHandler.Invited", player.GetPersonalizedName(target)));
            target.Out.SendGroupInviteCommand(player,
                LanguageMgr.GetTranslation(target.Client.Account.Language, "InviteToGroupHandler.InvitePrompt1", target.GetPersonalizedName(player), player.GetPronoun(1, false))
                + "\n" + LanguageMgr.GetTranslation(target.Client.Account.Language, "InviteToGroupHandler.InvitePrompt2"));
            ChatUtil.SendSystemMessage(target, LanguageMgr.GetTranslation(target.Client.Account.Language, "InviteToGroupHandler.InviteNotification", target.GetPersonalizedName(player), player.GetPronoun(1, false)));
        }
    }
}