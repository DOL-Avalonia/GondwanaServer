using DOL;
using DOL.Database;
using DOL.GS;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS
{
    [NPCGuildScript("Douanier")]
    public class Douanier : Scripts.TeleportNPC
    {
        public long Price { get; set; }

        public string GetPayString(string language)
        {
            return LanguageMgr.GetTranslation(language, "TeleportNPC.Douanier.PayString", Money.GetString(Price, language));
        }

        public Douanier()
        {
            LoadedFromScript = false;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            string lang = player.Client.Account.Language;
            string payStr = GetPayString(lang);

            string message = LanguageMgr.GetTranslation(lang, "TeleportNPC.Douanier.Interact", payStr);

            player.Out.SendMessage(message, eChatType.CT_Say, eChatLoc.CL_PopupWindow);

            return true;
        }

        private void SendReply(GamePlayer target, string msg)
        {
            target.Client.Out.SendMessage(
               msg,
               eChatType.CT_Say, eChatLoc.CL_PopupWindow);
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            var player = source as GamePlayer;
            if (player == null)
                return false;

            string lang = player.Client.Account.Language;

            if (str == GetPayString(lang))
            {
                if (player.CopperBalance < Price)
                {
                    foreach (GamePlayer emoteplayer in this.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        emoteplayer.Out.SendEmoteAnimation(this, eEmote.Laugh);

                    Say(LanguageMgr.GetTranslation(lang, "TeleportNPC.Douanier.NotEnoughMoney"));
                }
                else
                {
                    if (m_busy)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "TeleportNPC.Douanier.Busy"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return true;
                    }

                    player.RemoveMoney(Currency.Copper.Mint(Price));
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "TeleportNPC.Douanier.GiveMoney", Money.GetString(Price, lang)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    Say(LanguageMgr.GetTranslation(lang, "TeleportNPC.Douanier.Pass"));

                    //TeleportClass tc = new TeleportClass(this, player, 393786,616663,9025,3501, 163);        

                    ECSGameTimer TimerTL = new ECSGameTimer(this, Teleportation);
                    TimerTL.Properties.setProperty("TP", new JumpPos("Drium Ligen", 393786, 616663, 9025, 3501, 163));
                    TimerTL.Properties.setProperty("player", player);
                    TimerTL.Start(3000);

                    foreach (GamePlayer players in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        players.Out.SendSpellCastAnimation(this, 1, 20);
                        players.Out.SendEmoteAnimation(player, eEmote.Bind);
                    }
                    m_busy = true;
                }
            }
            return true;
        }

        public override void SaveIntoDatabase()
        {
            base.SaveIntoDatabase();
            db.Price = this.Price;
            GameServer.Database.SaveObject(db);
        }

        public override void LoadFromDatabase(DataObject mobobject)
        {
            base.LoadFromDatabase(mobobject);
            this.Price = db.Price;
        }
    }
}