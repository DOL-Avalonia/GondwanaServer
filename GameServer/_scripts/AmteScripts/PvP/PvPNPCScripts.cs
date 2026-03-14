using DOL.GS;
using DOL.GS.Scripts;
using AmteScripts.Managers;
using DOL.Language;
using DOL.GS.PacketHandler;
using DOL.Database;

namespace DOL.GS.Scripts
{
    public class CoreCollectorNPC : TextNPC
    {
        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            GamePlayer player = source as GamePlayer;
            if (player == null) return false;

            if (PvpManager.Instance.CurrentSessionType != PvpManager.eSessionTypes.CoreRun)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvpNPC.CoreRunNotActive"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (item is PvPTreasure treasure)
            {
                int points = (int)(treasure.Condition / 4.0);
                if (points < 1) points = 1;

                int condition = treasure.Condition;
                if (condition < 80) condition = 80;
                long realmPointsToAward = 200 + (((condition - 80) / 4) * 65);

                player.Inventory.RemoveItem(item);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvpNPC.CoreAccepted", points), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);

                PvpManager.Instance.HandleCoreDelivery(player, points);
                player.GainRealmPoints(realmPointsToAward);
                player.Out.SendSpellEffectAnimation(this, player, 106, 0, false, 1);

                return true;
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvpNPC.OnlyPowerCores"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return false;
        }
    }

    public class BiohazardResearcherNPC : TextNPC
    {
        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            GamePlayer player = source as GamePlayer;
            if (player == null) return false;

            if (PvpManager.Instance.CurrentSessionType != PvpManager.eSessionTypes.Biohazard)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvpNPC.BiohazardNotActive"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsDamned)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvpNPC.StayAwayMonster"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (item is PvPTreasure treasure && (item.Id_nb == "pvp_pure_toxic_sample" || item.Id_nb == "pvp_raw_toxic_sample"))
            {
                int points = (int)(treasure.Condition / 4.0);
                if (points < 1) points = 1;

                int condition = treasure.Condition;
                if (condition < 80) condition = 80;
                long realmPointsToAward = 150 + (((condition - 80) / 4) * 40);

                player.Inventory.RemoveItem(item);

                string sampleTypeKey = item.Id_nb == "pvp_pure_toxic_sample" ? "PvpNPC.SamplePure" : "PvpNPC.SampleRaw";
                string sampleType = LanguageMgr.GetTranslation(player.Client.Account.Language, sampleTypeKey);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvpNPC.SampleSecured", sampleType, points), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);

                PvpManager.Instance.HandleBiohazardDelivery(player, points);
                player.GainRealmPoints(realmPointsToAward);
                player.Out.SendSpellEffectAnimation(this, player, 106, 0, false, 1);

                return true;
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvpNPC.OnlyToxicSamples"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return false;
        }
    }

    public class BiohazardChest : GameCoffre
    {
        private bool _isLooted = false;

        public override void SaveIntoDatabase() {}

        public override bool Interact(GamePlayer player)
        {
            if (_isLooted) return false;

            if (PvpManager.Instance.CurrentSessionType != PvpManager.eSessionTypes.Biohazard)
                return false;

            this.RespawnInterval = 0;
            bool result = base.Interact(player);

            if (result)
            {
                _isLooted = true;

                // Immediately remove visual object from world
                this.Delete();
                PvpManager.Instance.RespawnSingleBiohazardChest(this);
            }

            return result;
        }
    }
}