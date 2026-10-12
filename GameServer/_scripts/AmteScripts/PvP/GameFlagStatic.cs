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
        private const int RETURN_TIMEOUT_MS = 25000;

        private ECSGameTimer _returnTimer;

        /// <summary>
        /// Stable identity of the flag for the whole session, independent of BasePad.
        /// </summary>
        public int FlagID { get; }
        public GameFlagBasePad BasePad { get; private set; }
        public GameCTFTempPad CurrentTempPad { get; set; }
        public bool IsDroppedOnGround { get; private set; }

        public GameFlagStatic(GameFlagBasePad basePad) : this(basePad, basePad?.FlagID ?? 0) { }

        public GameFlagStatic(GameFlagBasePad basePad, int flagId)
        {
            BasePad = basePad;
            FlagID = flagId;
        }

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;
            PvpManager.Instance.NotifyFlagObject(this, true);
            return true;
        }

        public override bool RemoveFromWorld(int respawnSeconds)
        {
            if (!base.RemoveFromWorld(respawnSeconds)) return false;
            PvpManager.Instance.NotifyFlagObject(this, false);
            return true;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            if (CurrentTempPad == null)
                return PlayerPickupFlag(player);

            if (OwnerPlayer == player)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.AlreadyOwnFlag"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (IsOwner(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.GroupOwnsFlag"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            CaptureByEnemy(player);
            return true;
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
                IsDroppedOnGround = false;

                _returnTimer?.Stop();
                _returnTimer = null;

                RemoveFromWorld();
                BasePad?.NotifyFlagLeft();
                SetOwnership(player);

                ushort effectID = (ushort)Util.Random(5811, 5815);
                foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).OfType<GamePlayer>())
                    plr.Out.SendSpellEffectAnimation(player, player, effectID, 0, false, 0x01);

                PvPFlagBanner banner = new() { OwningPlayer = player, Item = invFlag };
                if (player.IsInPvP) banner.Emblem = PvpManager.Instance.GetEmblemForPlayer(player);
                player.ActiveBanner = banner;

                foreach (IArea area in player.CurrentAreas)
                    if (area is TempPadArea padArea)
                        padArea.Pad.TryDropFlagIfCarried(player);

                return true;
            }
            catch (Exception e)
            {
                log.Error(e);
                invFlag.IsRemovalExpected = true;
                player.Inventory.RemoveItem(invFlag);
                BasePad?.RespawnFlag();
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
            PlayerPickupFlag(captor);
        }

        /// <summary>
        /// Called if the carrier was killed, or forcibly dropped the flag on the ground,
        /// to spawn this static item at the location of the kill.
        /// If no one picks it up for X seconds, it returns to base.
        /// </summary>
        public bool DropOnGround(int x, int y, int z, ushort heading, Region region)
        {
            CurrentTempPad = null;
            IsDroppedOnGround = true;
            Position = Position.Create(region.ID, x, y, z, heading);

            if (!AddToWorld())
            {
                IsDroppedOnGround = false;
                return false;
            }

            SetOwnership(null, false);

            _returnTimer?.Stop();
            _returnTimer = new ECSGameTimer(this, OnReturnTimerCallback);
            _returnTimer.Start(RETURN_TIMEOUT_MS);
            return true;
        }

        public bool Reset()
        {
            _returnTimer?.Stop();
            _returnTimer = null;
            IsDroppedOnGround = false;
            RemoveFromWorld();
            BasePad?.RespawnFlag();
            return true;
        }

        private int OnReturnTimerCallback(ECSGameTimer timer)
        {
            _returnTimer = null;
            Reset();
            return 0;
        }
    }
}