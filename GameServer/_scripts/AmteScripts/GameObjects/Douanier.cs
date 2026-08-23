using DOL;
using DOL.Database;
using DOL.GS;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    [NPCGuildScript("Douanier")]
    public class Douanier : Scripts.TeleportNPC
    {
        public long Price { get; set; }

        public string paystring
        {
            get
            {
                return "payer la somme de: " + Money.GetString(Price) + " pour les éviter";
            }
        }

        public Douanier()
        {
            LoadedFromScript = false;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            player.Out.SendMessage("Et toi ? Tu choisis quoi ? Tu veux ramasser tout mon clan dans ta face ou [" + paystring + "] ?", eChatType.CT_Say, eChatLoc.CL_PopupWindow);

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

            if (str == paystring)
            {
                if (player.CopperBalance < Price)
                {
                    foreach (GamePlayer emoteplayer in this.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        emoteplayer.Out.SendEmoteAnimation(this, eEmote.Laugh);
                    Say("Tchakkkk, tu passe pas ! t'es fauché !");
                }
                else
                {
                    if (m_busy)
                    {
                        player.Out.SendMessage("Je suis occupé pour le moment.", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return true;
                    }

                    player.RemoveMoney(Currency.Copper.Mint(Price));
                    player.Out.SendMessage("Vous donnez " + Money.GetString(Price, player.Client.Account.Language) + " au douanier.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    Say("Bien, tu peux passer toi !");

                    //TeleportClass tc = new TeleportClass(this, player, 393786,616663,9025,3501, 163);		

                    RegionTimer TimerTL = new RegionTimer(this, Teleportation);
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