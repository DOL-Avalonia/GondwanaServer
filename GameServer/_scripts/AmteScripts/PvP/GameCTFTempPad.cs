using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Geometry;
using AmteScripts.Managers;
using DOL.Language;

namespace AmteScripts.PvP.CTF
{
    public class GameCTFTempPad : GamePvPStaticItem
    {
        private ECSGameTimer _ownershipTimer;
        private int _pointsInterval = 20_000;
        private ECSGameTimer _maxPadTimer;
        private const int MAX_TIME_ON_TEMP_PAD = 600_000;
        private AbstractArea _padArea;

        public GameFlagStatic OwnedFlag { get; private set; }

        public override int Emblem
        {
            get => base.Emblem;
            set
            {
                base.Emblem = value;
                if (OwnedFlag != null)
                    OwnedFlag.Emblem = value;
            }
        }

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;
            
            _padArea = new TempPadArea(this, 90);
            CurrentRegion.AddArea(_padArea);
            return true;
        }

        /// <inheritdoc />
        public override bool RemoveFromWorld(int respawnSeconds)
        {
            if (!base.RemoveFromWorld(respawnSeconds))
                return false;

            if (_padArea != null)
            {
                CurrentRegion?.RemoveArea(_padArea);
                _padArea = null;
            }
            return true;
        }

        /// <inheritdoc />
        public override void SetOwnership(GamePlayer player, bool updateEmblem = true)
        {
            base.SetOwnership(player, updateEmblem);
            if (OwnedFlag != null)
                OwnedFlag.SetOwnership(player, updateEmblem);
        }

        /// <summary>
        /// Called by FlagInventoryItem once the carrier enters the pad radius 
        /// to place the flag as a static item here.
        /// </summary>
        public void StartOwnershipTimer(GameFlagStatic flag, GamePlayer whoDroppedIt)
        {
            OwnedFlag = flag;
            flag.CurrentTempPad = this;
            // Start awarding points
            _ownershipTimer?.Stop();
            _ownershipTimer = new ECSGameTimer(this, TimerCallback);
            _ownershipTimer.Start(_pointsInterval);
            _maxPadTimer?.Stop();
            _maxPadTimer = new ECSGameTimer(this, MaxPadTimeCallback);
            _maxPadTimer.Start(MAX_TIME_ON_TEMP_PAD);
        }

        private int TimerCallback(ECSGameTimer timer)
        {
            // Award "Flag_OwnershipPoints" to the pad’s owner each interval
            if (OwnedFlag == null)
            {
                return 0;
            }
            
            if (OwnerGuild != null)
            {
                PvpManager.Instance.AwardCTFOwnershipPoints(OwnerGuild, 1);
            }
            else if (OwnerPlayer != null)
            {
                PvpManager.Instance.AwardCTFOwnershipPoints(OwnerPlayer, 1);
            }

            return _pointsInterval;
        }

        private int MaxPadTimeCallback(ECSGameTimer t)
        {
            if (OwnedFlag != null)
            {
                var oldFlag = OwnedFlag;
                StopOwnership();
                oldFlag.RemoveFromWorld();

                oldFlag.BasePad?.RespawnFlag();
            }
            return 0;
        }

        public void TryDropFlagIfCarried(GamePlayer player)
        {
            if (!IsOwner(player)) return;

            // Search backpack for a FlagInventoryItem
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                var item = player.Inventory.GetItem(slot);
                if (item is FlagInventoryItem flagInv)
                {
                    var oldFlagRef = flagInv.FlagReference;
                    GameFlagBasePad originalPad = oldFlagRef?.BasePad;

                    flagInv.IsRemovalExpected = true;
                    flagInv.ClearFlagReference();
                    player.Inventory.RemoveItem(flagInv);

                    GameFlagStatic staticFlag = new(originalPad, flagInv.FlagID)
                    {
                        Name = flagInv.Name,
                        Model = (ushort)flagInv.Model,
                        Position = Position.Create(CurrentRegionID, Position.X, Position.Y, Position.Z, Heading),
                        CurrentTempPad = this
                    };
                    staticFlag.SetOwnership(player);
                    staticFlag.AddToWorld();
                    OwnedFlag = staticFlag;
                    StartOwnershipTimer(staticFlag, player);

                    PvpManager.Instance.AwardCTFCapturePoints(player);

                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPGameFlag.DepositedFlag"),
                                           eChatType.CT_System, eChatLoc.CL_SystemWindow);

                    return;
                }
            }
        }

        public override void Delete()
        {
            if (_ownershipTimer != null)
            {
                _ownershipTimer.Stop();
                _ownershipTimer = null;
            }
            if (OwnedFlag != null)
            {
                OwnedFlag.Reset();
                OwnedFlag = null;
            }
            base.Delete();
        }

        public void StopOwnership()
        {
            if (_ownershipTimer != null) _ownershipTimer.Stop();
            if (_maxPadTimer != null) _maxPadTimer.Stop();
            _ownershipTimer = null;
            _maxPadTimer = null;
            OwnedFlag = null;
        }
    }

    public class TempPadArea : Area.Circle
    {
        private GameCTFTempPad _pad;

        public GameCTFTempPad Pad => _pad;

        public TempPadArea(GameCTFTempPad pad, int radius)
            : base($"{pad.Name}_Area", pad.Position.X, pad.Position.Y, pad.Position.Z, radius)
        {
            _pad = pad;
            m_displayMessage = false;
        }

        public override void OnPlayerEnter(GamePlayer player)
        {
            base.OnPlayerEnter(player);
            _pad.TryDropFlagIfCarried(player);
        }
    }
}