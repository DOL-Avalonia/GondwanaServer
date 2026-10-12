using DOL.GS;
using DOL.Database;
using AmteScripts.Managers;
using System.Linq;

namespace AmteScripts.PvP.CTF
{
    public class FlagInventoryItem : GameInventoryItem
    {
        public GameFlagStatic FlagReference { get; private set; }
        public int FlagID { get; private set; }
        public bool IsRemovalExpected { get; set; }

        public FlagInventoryItem() : base() { }
        public FlagInventoryItem(ItemTemplate template) : base(template) { }

        public FlagInventoryItem(GameFlagStatic flagStatic) : base(CreateTemplateFromFlag(flagStatic))
        {
            FlagReference = flagStatic;
            FlagID = flagStatic.FlagID;
        }

        public override bool CanPersist => false;

        private bool _isSilencing;
        public bool IsSilencing
        {
            get => _isSilencing;
            set
            {
                if (value == _isSilencing) return;
                _isSilencing = value;
                if (m_owner == null) return;
                m_owner.IsSilenced = value;
                m_owner.IsDisarmed = value;
            }
        }

        private static ItemTemplate CreateTemplateFromFlag(GameFlagStatic flagStatic) => new()
        {
            Name = flagStatic.Name,
            Id_nb = "CTF_Flag_" + flagStatic.FlagID,
            Level = 50,
            Model = flagStatic.Model,
            IsDropable = false,
            IsPickable = true,
            IsTradable = false,
            Weight = 100,
            Quality = 100,
            Object_Type = (int)eObjectType.GenericItem,
            ClassType = typeof(FlagInventoryItem).FullName
        };

        /// <summary>Puts the flag back into the world where the carrier stands (death, linkdeath, leaving PvP).</summary>
        public bool DropFlagOnGround(GamePlayer carrier, GameLiving killer)
        {
            if (carrier == null) return false;

            IsRemovalExpected = true;

            if (!carrier.Inventory.RemoveItem(this))
                return false;
            
            GameFlagStatic flag = FlagReference ?? new GameFlagStatic(null, FlagID);
            flag.Name = Name;
            flag.Model = (ushort)Model;

            if (!flag.DropOnGround(carrier.Position.X, carrier.Position.Y, carrier.Position.Z, carrier.Heading, carrier.CurrentRegion))
                return false;

            if (killer is GamePlayer killerPlayer)
                PvpManager.Instance.AwardCTFCarrierKill(killerPlayer, carrier);

            foreach (GamePlayer plr in carrier.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).OfType<GamePlayer>())
                plr.Out.SendSpellEffectAnimation(carrier, flag, 7043, 0, false, 0x01);

            return true;
        }

        public void OnJoinGroup(GamePlayer player, Group group) { if (player == m_owner) IsSilencing = true; }

        public void OnLeaveGroup(GamePlayer player, Group group) { if (player == m_owner) IsSilencing = false; }

        /// <inheritdoc />
        public override void OnReceive(GamePlayer player)
        {
            base.OnReceive(player);

            if (player.Group != null)
                IsSilencing = true;

            PvpManager.Instance.NotifyFlagCarrier(player, FlagID, true);
        }

        /// <summary>
        /// Called when this item is removed from a player's inventory.
        /// </summary>
        public override void OnLose(GamePlayer player)
        {
            IsSilencing = false;
            base.OnLose(player);
            PvpManager.Instance.NotifyFlagCarrier(player, FlagID, false);

            if (player.ActiveBanner?.Item == this)
            {
                player.ActiveBanner = null;
            }
            
            if (!IsRemovalExpected && PvpManager.Instance.IsOpen)
            {
                FlagReference?.BasePad?.RespawnFlag();
            }

            IsRemovalExpected = false;
        }

        /// <summary>
        /// Helper method to clear the flag reference.
        /// </summary>
        public void ClearFlagReference()
        {
            FlagReference = null;
        }

        public override bool CheckValid(GamePlayer player)
        {
            return true;
        }

        public override bool Use(GamePlayer player)
        {
            return false;
        }
    }
}