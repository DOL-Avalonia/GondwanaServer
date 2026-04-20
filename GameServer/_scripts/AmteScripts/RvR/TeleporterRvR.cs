using AmteScripts.Managers;
using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.GS.Spells;
using System;

namespace DOL.GS.Scripts
{
    public class TeleporterRvR : GameNPC
    {
        private bool _isBusy;

        private string[] rvrs => new string[]
        {
            "RvR Débutant (Lv 20-28) : Lion's Den",
            "RvR Standard (Lv 29-37) : Hills of Claret",
            "RvR Expert (Lv 38-45) : Molvik",
            $"RvR Master (Lv 46-50) : {GetMasterMapName()}"
        };

        private string GetMasterMapName()
        {
            switch (RvrManager.Instance.CurrentMasterMap)
            {
                case RvrManager.MapType.Master01: return "Braemar";
                case RvrManager.MapType.Master02: return "Wilton";
                case RvrManager.MapType.Master03: return "Thidranki";
                default: return "Thidranki";
            }
        }

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;
            SetOwnBrain(new BlankBrain());
            return true;
        }

        private bool _BaseSay(GamePlayer player, string str = "Partir")
        {
            if (_isBusy)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.Busy"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            TurnTo(player);

            if (player.HasTerritoryRelic())
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.RelicRefusal1"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.RelicRefusal2"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (SpellHandler.FindEffectOnTarget(player, "Damnation") != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.DamnationRefusal1", player.Name), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.DamnationRefusal2"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (!RvrManager.Instance.IsInRvr(player) &&
                (!RvrManager.Instance.IsOpen || player.Level < 20 || (str != "Pret" && str != "Prêt" && str != "Partir" && str != "Ready" && str != "Leave")))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.CannotHelpPart1", player.Name), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.CannotHelpPart2"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }
            return false;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            if (_BaseSay(player)) return true;

            if (!RvrManager.Instance.IsOpen && RvrManager.Instance.IsRvRRegion(player.CurrentRegionID))
            {
                _Teleport(player);
                return true;
            }

            if (RvrManager.Instance.IsInRvr(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.ChickenOutPart1"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.ChickenOutPart2"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
            else
            {
                player.Out.SendMessage(string.Concat(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.SendToCombat", player.Name), "\n\n - ", string.Join("\n - ", rvrs)), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str) || !(source is GamePlayer)) return false;
            GamePlayer player = source as GamePlayer;

            if (_BaseSay(player, str)) return true;

            _Teleport(player);
            return true;
        }

        private void _Teleport(GamePlayer player)
        {
            _isBusy = true;
            RegionTimer TimerTL = new RegionTimer(this, _Teleportation);
            TimerTL.Properties.setProperty("player", player);
            TimerTL.Start(3000);
            foreach (GamePlayer players in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                players.Out.SendSpellCastAnimation(this, 1, 20);
                players.Out.SendEmoteAnimation(player, eEmote.Bind);
            }
        }

        private int _Teleportation(RegionTimer timer)
        {
            _isBusy = false;
            GamePlayer player = timer.Properties.getProperty<GamePlayer>("player", null);
            if (player == null) return 0;
            if (player.InCombat)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleporterRvR.InCombat"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            else
            {
                if (!RvrManager.Instance.IsOpen || RvrManager.Instance.IsInRvr(player))
                    RvrManager.Instance.RemovePlayer(player, true);
                else
                    RvrManager.Instance.AddPlayer(player);
            }
            return 0;
        }
    }
}