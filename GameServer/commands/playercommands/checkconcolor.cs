using System.Text;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    /// <summary>
    /// Debug/help command: shows how the SERVER rates your target's level relative to you.
    /// (The client computes the colour it displays on its own; this shows what the server uses for XP, loot and tasks.)
    /// </summary>
    [CmdAttribute("&checkconcolor", ePrivLevel.Player,
        "Shows the server-side con level of your target (XP/loot/task rules).",
        "/checkconcolor")]
    public class CheckConColorCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client?.Player;
            if (player == null) return;
            if (IsSpammingCommand(player, "checkconcolor")) return;

            GameObject target = player.TargetObject;
            if (target == null)
            {
                client!.Out.SendMessage("This command requires a target.", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return;
            }

            int myLevel = player.EffectiveLevel;
            int targetLevel = target.EffectiveLevel;

            double legacyCon = LegacyConLevelGrid.GetConLevel(myLevel, targetLevel);
            ConColor legacyColor = LegacyConLevelGrid.GetConColor(myLevel, targetLevel);
            ConColor liveColor = CharacterConLevelGrid.GetConColor(CharacterConLevelGrid.GetConLevel(myLevel, targetLevel));
            bool grey = LegacyConLevelGrid.IsGreyCon(myLevel, targetLevel);

            StringBuilder sb = new();
            sb.AppendLine($"[{target.Name}] level {targetLevel} vs you level {myLevel}");
            sb.AppendLine($"Gondwana con value: {legacyCon:0.00} -> {legacyColor} (used by combat / XP / task math)");
            sb.AppendLine($"Live-server colour: {liveColor} (for comparison only)");
            sb.AppendLine($"Grey (no XP, no loot, no task credit): {(grey ? "YES" : "no")}");

            if (target is GameLiving living)
            {
                int soloTier = living is GamePlayer
                    ? LegacyConLevelGrid.GetPvPTaskTier(myLevel, targetLevel)
                    : LegacyConLevelGrid.GetNpcTaskTier(myLevel, targetLevel);
                sb.AppendLine($"Task tier (solo): {soloTier} {TierLabel(soloTier)}");

                if (player.Group != null)
                {
                    int highestLevel = myLevel;
                    foreach (GamePlayer member in player.Group.GetPlayersInTheGroup())
                        if (member.EffectiveLevel > highestLevel) highestLevel = member.EffectiveLevel;

                    int groupTier = living is GamePlayer
                        ? LegacyConLevelGrid.GetPvPTaskTier(highestLevel, targetLevel)
                        : LegacyConLevelGrid.GetNpcTaskTier(highestLevel, targetLevel);
                    sb.AppendLine($"Task tier (group of {player.Group.MemberCount}, highest level {highestLevel}): {groupTier} {TierLabel(groupTier)}");
                }
            }

            client!.Out.SendMessage(sb.ToString(), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        private static string TierLabel(int tier) => tier switch
        {
            0 => "(grey - never counts)",
            1 => "(green - counts solo only)",
            2 => "(blue - counts solo / small group)",
            _ => "(yellow or better - always counts)"
        };
    }
}