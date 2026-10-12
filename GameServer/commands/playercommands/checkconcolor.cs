using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System.Collections.Generic;

namespace DOL.GS.Commands
{
    /// <summary>
    /// Debug/help command: shows how the SERVER rates your target's level relative to you.
    /// (The client computes the colour it displays on its own; this shows what the server uses for XP, loot and tasks.)
    /// </summary>
    [CmdAttribute(
        "&checkconcolor",
        ePrivLevel.Player,
        "Commands.GM.Checkconcolor.Description",
        "Commands.GM.Checkconcolor.Usage")]
    public class CheckConColorCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client?.Player;
            string serverName = string.IsNullOrEmpty(Properties.ROG_SERVER_NAME) ? "Commands.GM.Checkconcolor.ServerName" : Properties.ROG_SERVER_NAME;
            string serverPrefix = $"{serverName}";

            if (player == null) return;
            if (IsSpammingCommand(player, "checkconcolor")) return;

            string lang = client!.Account.Language;
            GameObject target = player.TargetObject;

            if (target == null)
            {
                client!.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.NeedTarget"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return;
            }

            int myLevel = player.EffectiveLevel;
            int targetLevel = target.EffectiveLevel;

            double legacyCon = LegacyConLevelGrid.GetConLevel(myLevel, targetLevel);
            ConColor legacyColor = LegacyConLevelGrid.GetConColor(myLevel, targetLevel);
            ConColor liveColor = CharacterConLevelGrid.GetConColor(CharacterConLevelGrid.GetConLevel(myLevel, targetLevel));
            bool grey = LegacyConLevelGrid.IsGreyCon(myLevel, targetLevel);

            string yesTrans = LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.Yes");
            string noTrans = LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.No");

            List<string> lines = new List<string>
            {
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.TargetVsYou", target.Name, targetLevel, myLevel),
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.ServerCon", legacyCon, legacyColor.ToString(), serverPrefix),
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.LiveCon", liveColor.ToString()),
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.Grey", grey ? yesTrans : noTrans)
            };

            if (target is GameLiving living)
            {
                int soloTier = living is GamePlayer
                    ? LegacyConLevelGrid.GetPvPTaskTier(myLevel, targetLevel)
                    : LegacyConLevelGrid.GetNpcTaskTier(myLevel, targetLevel);

                lines.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.TaskSolo", soloTier, TierLabel(lang, soloTier)));

                if (player.Group != null)
                {
                    int highestLevel = myLevel;
                    foreach (GamePlayer member in player.Group.GetPlayersInTheGroup())
                    {
                        if (member.EffectiveLevel > highestLevel)
                            highestLevel = member.EffectiveLevel;
                    }

                    int groupTier = living is GamePlayer
                        ? LegacyConLevelGrid.GetPvPTaskTier(highestLevel, targetLevel)
                        : LegacyConLevelGrid.GetNpcTaskTier(highestLevel, targetLevel);

                    lines.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.TaskGroup", player.Group.MemberCount, highestLevel, groupTier, TierLabel(lang, groupTier)));
                }
            }

            client!.Out.SendMessage(string.Join("\n", lines), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        private static string TierLabel(string lang, int tier) => tier switch
        {
            0 => LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.Tier.Grey"),
            1 => LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.Tier.Green"),
            2 => LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.Tier.Blue"),
            _ => LanguageMgr.GetTranslation(lang, "Commands.GM.Checkconcolor.Tier.Yellow")
        };
    }
}