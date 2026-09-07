using DOL.AI.Brain;
using System;
using System.Reflection;
using System.Linq;

using DOL.Database;
using DOL.Events;
using log4net;
using System.Timers;
using GameServerScripts.Utils;
using System.Numerics;

namespace DOL.GS
{
    public class FeuDeCamp : GameNPC
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        public static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private const int PROXIMITY_CHECK_INTERVAL = 4 * 1000;
        private ECSGameTimer m_pulseTimer;
        private long m_expireTick;
        private GameStaticItem m_RealFeu;

        public FeuDeCamp() : base() { LoadedFromScript = true; }

        public string Template_ID { get; set; }
        public ushort Radius { get; set; }
        public double Lifetime { get; set; }
        public int EndurancePercentRate { get; set; }
        public int HealthPercentRate { get; set; }
        public int ManaPercentRate { get; set; }
        public bool IsHealthType => HealthPercentRate > 0;
        public bool IsManaType => ManaPercentRate > 0;
        public bool IsHealthTrapType => HealthTrapDamagePercent > 0;
        public bool IsManaTrapType => ManaTrapDamagePercent > 0;
        public bool IsEnduranceType => EndurancePercentRate > 0;
        public int HealthTrapDamagePercent { get; set; }
        public int ManaTrapDamagePercent { get; set; }
        public new int Realm { get; set; }
        public bool OwnerImmuneToTrap { get; set; }

        public override string OwnerID
        {
            get => base.OwnerID;
            set
            {
                base.OwnerID = value;

                if (m_RealFeu != null)
                {
                    m_RealFeu.OwnerID = value;
                }
            }
        }

        public override bool AddToWorld()
        {
            Level = 0;
            Flags = GameNPC.eFlags.PEACE | GameNPC.eFlags.CANTTARGET;

            if (double.IsNaN(Lifetime))
                Lifetime = 2;
            long lifeMs = (long)(Lifetime * 60 * 1000);
            m_expireTick = lifeMs > 0 ? GameLoop.GameLoopTime + lifeMs : long.MaxValue;

            m_RealFeu = new GameStaticItem
            {
                Name = "Feu de Camp",
                Position = Position,
                Model = Model,
                Realm = (eRealm)Realm,
                OwnerID = OwnerID
            };
            Name = "Feu de Camp";
            m_RealFeu.AddToWorld();

            m_pulseTimer = new ECSGameTimer(this, PulseTick, PROXIMITY_CHECK_INTERVAL);
            return base.AddToWorld();
        }

        private int PulseTick(ECSGameTimer timer)
        {
            if (GameLoop.GameLoopTime >= m_expireTick || ObjectState != eObjectState.Active)
            {
                Cleanup();
                return 0;
            }

            foreach (GamePlayer p in WorldMgr.GetPlayersCloseToSpot(Position, Radius))
            {
                if (p.IsSitting)
                {
                    if (IsHealthType) p.Health += (HealthPercentRate * p.MaxHealth) / 100;
                    if (IsEnduranceType) p.Endurance += (EndurancePercentRate * p.MaxEndurance) / 100;
                    if (IsManaType) p.Mana += (ManaPercentRate * p.MaxMana) / 100;
                }

                if (IsImmune(p))
                    continue;

                if (GameServer.ServerRules.ShouldAOEHitTarget(null, Owner, p))
                {
                    AttackData ad = new AttackData
                    {
                        Attacker = Owner,
                        AttackResult = eAttackResult.HitUnstyled,
                        AttackType = AttackData.eAttackType.Spell,
                        CausesCombat = false,
                        Target = p
                    };
                    if (IsHealthTrapType) ad.Damage = (HealthTrapDamagePercent * p.MaxHealth) / 100;
                    if (IsManaTrapType) p.Mana -= (ManaTrapDamagePercent * p.MaxMana) / 100;
                    p.TakeDamage(ad);
                }
            }
            return PROXIMITY_CHECK_INTERVAL;
        }

        public bool IsImmune(GameLiving living)
        {
            if (living == null)
            {
                return true;
            }

            if (living.GetLivingOwner() is GameLiving owner)
                living = owner;

            if (this.IsOwner(living))
                return true;

            var myOwner = GetLivingOwner();

            if (myOwner == null)
                return false;

            if (myOwner.Group?.IsInTheGroup(living) == true)
            {
                return true;
            }

            if (myOwner is GamePlayer ownerPlayer)
            {
                if (living is GamePlayer targetPlayer)
                {
                    if (targetPlayer.Guild == ownerPlayer.Guild)
                    {
                        return true;
                    }

                    if (ownerPlayer.BattleGroup != null && targetPlayer.BattleGroup == ownerPlayer.BattleGroup)
                    {
                        return true;
                    }
                }
                else if (living is GameNPC targetNpc)
                {
                    if (targetNpc.CurrentTerritory?.IsOwnedBy(ownerPlayer) == true)
                    {
                        return true;
                    }

                    if (!string.IsNullOrEmpty(targetNpc.GuildName) && string.Equals(targetNpc.GuildName, ownerPlayer.Guild.Name))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        void ProximityCheck(object sender, ElapsedEventArgs e)
        {
            foreach (GamePlayer Player in WorldMgr.GetPlayersCloseToSpot(this.Position, Radius))
            {
                if (Player.IsSitting)
                {
                    if (IsHealthType)
                    {
                        Player.Health += (HealthPercentRate * Player.MaxHealth) / 100;
                    }

                    if (IsEnduranceType)
                    {
                        Player.Endurance += (EndurancePercentRate * Player.MaxEndurance) / 100;
                    }

                    if (IsManaType)
                    {
                        Player.Mana += (ManaPercentRate * Player.MaxMana) / 100;
                    }
                }

                if (IsImmune(Player))
                {
                    continue;
                }

                if (GameServer.ServerRules.ShouldAOEHitTarget(null, Owner, Player))
                {
                    AttackData ad = new AttackData
                    {
                        Attacker = Owner,
                        AttackResult = eAttackResult.HitUnstyled,
                        AttackType = AttackData.eAttackType.Spell,
                        CausesCombat = false,
                        Target = Player
                    };

                    if (IsHealthTrapType)
                    {
                        ad.Damage = (HealthTrapDamagePercent * Player.MaxHealth) / 100;
                    }

                    if (IsManaTrapType)
                    {
                        Player.Mana -= (ManaTrapDamagePercent * Player.MaxMana) / 100;
                    }
                    Player.TakeDamage(ad);
                }
            }
        }

        private void Cleanup()
        {
            m_pulseTimer?.Stop();
            m_pulseTimer = null;
            m_RealFeu?.Delete();
            Delete();
        }

        public override void Delete()
        {
            m_pulseTimer?.Stop();
            m_pulseTimer = null;
            base.Delete();
        }
    }

    public class FeuDeCampEvent
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        public static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        [ScriptLoadedEvent]
        public static void OnScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(PlayerInventoryEvent.ItemDropped,
                new DOLEventHandler(EventPlayerDropItem));
            log.Info("FeuDeCamp chargé.");
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(PlayerInventoryEvent.ItemDropped,
                new DOLEventHandler(EventPlayerDropItem));
        }

        protected static ItemTemplate m_Feu;
        public static ItemTemplate Feu
        {
            get
            {
                m_Feu = (ItemTemplate)GameServer.Database.FindObjectByKey<ItemTemplate>("tif_s_feu");
                if (m_Feu == null)
                {
                    m_Feu = new ItemTemplate();
                    m_Feu.CanDropAsLoot = true;
                    m_Feu.Charges = 1;
                    m_Feu.Id_nb = "tif_s_feu";
                    m_Feu.IsDropable = true;
                    m_Feu.IsPickable = false;
                    m_Feu.IsTradable = true;
                    m_Feu.Item_Type = 41;
                    m_Feu.Level = 0;
                    m_Feu.Model = 3470;
                    m_Feu.Name = "Necessaire à Feu de Camp";
                    m_Feu.Object_Type = (int)eObjectType.GenericItem;
                    m_Feu.Realm = 0;
                    m_Feu.Quality = 100;
                    m_Feu.Price = 10000;

                    GameServer.Database.AddObject(m_Feu);
                }
                return m_Feu;
            }
        }

        public static void EventPlayerDropItem(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GamePlayer player)
                return;

            if (args is not ItemDroppedEventArgs { SourceItem: not null } dropArgs)
                return;

            var feu = FeuxCampMgr.Instance.m_firecamps.Values.FirstOrDefault(f => f.Template_ID == dropArgs.SourceItem.Id_nb);

            if (feu == null)
                return;

            ItemTemplate itemTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(dropArgs.SourceItem.Id_nb);

            var firecamp = new FeuDeCamp()
            {
                Template_ID = feu.Template_ID,
                Realm = itemTemplate.Realm,
                Model = feu.Model,
                Radius = feu.Radius,
                Lifetime = feu.Lifetime,
                EndurancePercentRate = feu.EndurancePercentRate,
                ManaPercentRate = feu.ManaPercentRate,
                ManaTrapDamagePercent = feu.ManaTrapDamagePercent,
                HealthTrapDamagePercent = feu.HealthTrapDamagePercent,
                HealthPercentRate = feu.HealthPercentRate,
                Position = player.Position,
                Owner = player,
                OwnerImmuneToTrap = feu.OwnerImmuneToTrap
            };

            firecamp.AddToWorld();
            dropArgs.GroundItem.Delete();
        }
    }
}