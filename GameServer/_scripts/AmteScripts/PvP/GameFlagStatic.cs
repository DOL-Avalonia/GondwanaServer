using System;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.GS.Geometry;
using log4net;
using AmteScripts.Managers;
using System.Linq;
using DOL.Language;

namespace AmteScripts.PvP.CTF
{
    /// <summary>
    /// Static item version of the flag, placed on the base pad or dropped on ground or in a TempAreaFlagPad.
    /// </summary>
    public class GameFlagStatic : GamePvPStaticItem
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GameFlagStatic));
        public GameFlagBasePad BasePad { get; private set; }
        public GameCTFTempPad CurrentTempPad { get; set; }

        private RegionTimer _returnTimer;
        private const int RETURN_TIMEOUT_MS = 25000;
        private bool _isDroppedOnGround = false;

        public GameFlagStatic(GameFlagBasePad basePad)
        {
            BasePad = basePad;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;


            if (CurrentTempPad == null)
            {
                // Presumed neutral, i.e. on base pad or ground
                return PlayerPickupFlag(player);
            }
            
            if (OwnerPlayer == player)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.AlreadyOwnFlag"),
                                       eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (IsOwner(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.GroupOwnsFlag"),
                                       eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            else
            {
                CaptureByEnemy(player);
                return true;
            }
        }

        public bool PlayerPickupFlag(GamePlayer player)
        {
            if (!player.IsAlive)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupDead"),
                                       eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsStealthed)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupStealthed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsDamned)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupDamned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (SpellHandler.FindEffectOnTarget(player, "Petrify") != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupPtrified"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            var wsd = SpellHandler.FindEffectOnTarget(player, "WarlockSpeedDecrease");
            if (wsd != null && wsd.Spell != null && wsd.Spell.AmnesiaChance == 1)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupFrog"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.InCombat)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsOnHorse)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupMounted"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.PlayerAfkMessage != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupAFK"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.DuelTarget != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupDuel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.ActiveBanner != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.CannotPickupGuildBanner"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            var invFlag = new FlagInventoryItem(this);
            if (!player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, invFlag))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.InventoryFull"),
                                       eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            try
            {
                if (CurrentTempPad != null)
                {
                    CurrentTempPad.StopOwnership();
                    CurrentTempPad = null;
                }

                RemoveFromWorld();
                if (_returnTimer != null)
                {
                    _returnTimer.Stop();
                    _returnTimer = null;
                }

                if (BasePad != null)
                {
                    BasePad.NotifyFlagLeft();
                }

                SetOwnership(player);

                ushort effectID = (ushort)Util.Random(5811, 5815);
                foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).OfType<GamePlayer>())
                {
                    plr.Out.SendSpellEffectAnimation(player, player, effectID, 0, false, 0x01);
                }

                var banner = new PvPFlagBanner()
                {
                    OwningPlayer = player,
                    Item = invFlag,
                };
                if (player.IsInPvP)
                    banner.Emblem = PvpManager.Instance.GetEmblemForPlayer(player);

                player.ActiveBanner = banner;
                _isDroppedOnGround = false;

                foreach (var area in player.CurrentAreas)
                {
                    if (area is TempPadArea padArea)
                    {
                        // Flag picked up in our pad, possible if we just killed a thief
                        padArea.Pad.TryDropFlagIfCarried(player);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                // Something went wrong, abort & reset
                player.Inventory.RemoveItem(invFlag);
                log.Error(e);
                BasePad.SpawnFlag();
                return false;
            }
        }

        public void CaptureByEnemy(GamePlayer captor)
        {
            if (CurrentTempPad != null)
            {
                CurrentTempPad.StopOwnership();
                CurrentTempPad = null;
            }
            
            SetOwnership(null);

            bool success = PlayerPickupFlag(captor);
            if (success)
            {
                ushort effectID = (ushort)Util.Random(5811, 5815);
                foreach (var plr in captor.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).OfType<GamePlayer>())
                {
                    plr.Out.SendSpellEffectAnimation(captor, captor, effectID, 0, false, 0x01);
                }
            }
        }

        /// <summary>
        /// Called if the carrier was killed, or forcibly dropped the flag on the ground,
        /// to spawn this static item at the location of the kill.
        /// If no one picks it up for X seconds, it returns to base.
        /// </summary>
        public bool DropOnGround(int x, int y, int z, ushort heading, Region region)
        {
            this.Position = Position.Create(region.ID, x, y, z, heading);
            if (!this.AddToWorld())
                return false;
            
            SetOwnership(null, false);
            _isDroppedOnGround = true;
            if (_returnTimer != null)
            {
                _returnTimer.Stop();
            }

            _returnTimer = new RegionTimer(this, (t) => OnReturnTimerCallback(t));
            _returnTimer.Start(RETURN_TIMEOUT_MS);
            return true;
        }

        public bool Reset()
        {
            RemoveFromWorld();
            if (BasePad != null)
            {
                BasePad.RespawnFlag();
            }
            return true;
        }

        private int OnReturnTimerCallback(RegionTimer timer)
        {
            Reset();
            return 0;
        }
    }
}
