using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Scripts
{
    public class Geolier : GameNPC
    {
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;
            TurnTo(player);

            string message;
            if (JailMgr.IsPrisoner(player))
                message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameJail.Interact.Prisoner", player.Name);
            else
                message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameJail.Interact.NonPrisoner");
            player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_PopupWindow);
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str)) return false;
            if (!(source is GamePlayer)) return false;

            GamePlayer player = source as GamePlayer;
            TurnTo(player);

            switch (str)
            {
                case "peine":
                case "sentence":
                    Prisoner prison = JailMgr.GetPrisoner(player);
                    if (prison == null) return Interact(player);

                    string reason = string.Empty;
                    if (!string.IsNullOrEmpty(prison.Raison))
                    {
                        reason = LanguageMgr.GetTranslation(player!.Client.Account.Language, "GameJail.Reason", prison.Raison);
                    }

                    if (prison.RP)
                        player!.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameJail.RPWait", prison.Sortie.ToShortDateString(), prison.Sortie.Hour) + "\n\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "GameJail.RPReason", reason), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    else
                        player!.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameJail.NonRPWait", prison.Sortie.ToShortDateString(), prison.Sortie.Hour), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;

                default: return Interact(player);
            }
            return true;
        }
    }
}
