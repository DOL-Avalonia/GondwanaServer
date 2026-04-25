/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.Geometry;
using DOL.GS.Keeps;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Collections;
using System.Numerics;

namespace DOL.GS
{
    #region GameSiegeweapon
    public class GameSiegeWeapon : GameMovingObject
    {
        public GameSiegeWeapon()
        {
            SetOwnBrain(new BlankBrain());
            this.Realm = 0;
            Level = 1;
            CurrentState = eState.Inactive;
            m_ammo = new ArrayList();

            m_ammoSlot = 0x14;
            ActionDelay = new int[]
                {
                    0,//none
					10000,//aiming
					15000,//arming
					5000,//loading
					0//fireing
				};//en ms
            m_enableToMove = true;
            MaxSpeedBase = 100;
        }
        public const int SIEGE_WEAPON_CONTROLE_DISTANCE = 256;
        public const int TIME_TO_DECAY = 60 * 1000 * 3; //3 min
        public const int DECAYPERIOD = 240000; //ms
        #region enum
        public enum eState : byte
        {
            Inactive = 0x0,
            Armed = 0x01,
            Aimed = 0x02,
            Ready = 0x03,//armed+aimed
        }

        public enum eCommand : byte
        {
            None = 0x00,
            PutAmmo = 0x01,
            Aim = 0x03,
            Fire = 0x04,
            Move = 0x05,
            Repair = 0x06,
            Salvage = 0x07,
            Release = 0x08,
        }
        #endregion
        #region properties
        private GameKeepHookPoint m_hookPoint;
        public GameKeepHookPoint HookPoint
        {
            get { return m_hookPoint; }
            set { m_hookPoint = value; }
        }

        public override Region CurrentRegion
        {
            get
            {
                return base.CurrentRegion;
            }
            set
            {
                base.CurrentRegion = value;
                SiegeWeaponTimer = new SiegeTimer(this);
            }
        }

        private byte m_ammoType;
        public byte AmmoType
        {
            get { return m_ammoType; }
            set { m_ammoType = value; }
        }
        
        private eState m_currentState;
        public eState CurrentState
        {
            get { return m_currentState; }
            set { m_currentState = value; }
        }

        public virtual ushort Effect
        {
            get { return m_effect; }
            set { m_effect = value; }
        }
        protected ArrayList m_ammo;
        public virtual ArrayList Ammo
        {
            get { return m_ammo; }
            set { m_ammo = value; }
        }
        protected SiegeTimer m_siegeTimer;
        public virtual SiegeTimer SiegeWeaponTimer
        {
            get { return m_siegeTimer; }
            set { m_siegeTimer = value; }
        }
        public override int MaxHealth
        {
            get
            {
                return 10000;
            }
        }

        public override int Mana
        {
            get { return 50000; }
        }

        private int m_timesrepaired;
        public int TimesRepaired
        {
            get { return m_timesrepaired; }
            set { m_timesrepaired = value; }
        }

        protected RegionTimer m_decayTimer;
        /// <summary>
        /// The lock object for lazy regen timers initialization
        /// </summary>
        protected readonly object m_decayTimerLock = new object();

        private ushort m_ammoSlot;
        public ushort AmmoSlot
        {
            get { return m_ammoSlot; }
            set { m_ammoSlot = value; }
        }

        public int DecayedHp
        {
            get { return 3 * (this.MaxHealth / 10); }
        }

        public int DeductHp
        {
            get { return -this.MaxHealth / 10; }
        }

        private string m_itemId;
        public string ItemId
        {
            get { return m_itemId; }
            set { m_itemId = value; }
        }
        
        public Coordinate AimCoordinate
        {
            get
            {
                var targetPosition = Position.Zero;
                if(TargetObject != null) targetPosition = TargetObject.Position;
                else if(GroundTargetPosition != Position.Nowhere) targetPosition = GroundTargetPosition;
                return targetPosition.Coordinate;
            }
        }
        
        #endregion
        
        #region public methode
        public void TakeControl(GamePlayer player)
        {
            if (Owner != null && Owner != player)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameSiegeWeapon.AlreadyControlled", GetName(0, true)), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                return;
            }
            if (player.SiegeWeapon != null && player.SiegeWeapon != this)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameSiegeWeapon.AlreadyHaveWeapon"), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                return;
            }
            if (IsMoving)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameSiegeWeapon.CantControlMoving"), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                return;
            }
            Owner = player;
            player.SiegeWeapon = this;
            player.Out.SendSiegeWeaponInterface(this, SiegeWeaponTimer.TimeUntilElapsed / 100);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameSiegeWeapon.TakeControl", GetName(0, false)), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
            if ((CurrentState & GameSiegeWeapon.eState.Armed) != GameSiegeWeapon.eState.Armed)
                Arm();

        }
        public virtual void ReleaseControl()
        {
            if (Owner is GamePlayer player)
            {
                player.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameSiegeWeapon.NoLongerControlling", GetName(0, false)), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                player.Out.SendSiegeWeaponCloseInterface();
                player.SiegeWeapon = null;
            }
            Owner = null;
            StopMove();
        }

        public override void Die(GameObject killer)
        {
            StopDecay();
            ReleaseControl();
            Delete();
        }

        public virtual void Aim()
        {
            if (!CanUse()) return;
            //The trebuchet isn't ready to be aimed yet!
            if (Owner.TargetObject == null) return;
            if (!GameServer.ServerRules.IsAllowedToAttack(Owner, ((GameLiving)Owner.TargetObject), true)) return;
            CurrentState &= ~eState.Aimed;
            GroundTargetPosition = Owner.TargetObject.Position;
            TargetObject = Owner.TargetObject;
            SiegeWeaponTimer.CurrentAction = SiegeTimer.eAction.Aiming;
            TurnTo(GroundTargetPosition.Coordinate);
            PreAction();
            var ownerPlayer = Owner as GamePlayer;
            if (Owner != null)
            {
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.TurningToTarget", GetName(0, true)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        public virtual void Arm()
        {
            if (!CanUse()) return;
            CurrentState &= ~eState.Armed;
            SiegeWeaponTimer.CurrentAction = SiegeTimer.eAction.Arming;
            PreAction();
            var ownerPlayer = Owner as GamePlayer;
            if (Owner != null)
            {//You prepare the cauldron of boiling oil for firing. (15.0s until armed)
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.PrepareFiring", GetName(0, false), (GetActionDelay(SiegeTimer.eAction.Arming) / 1000).ToString("N")), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

        }
        public void Move()
        {
            var ownerPlayer = Owner as GamePlayer;
            if (!CanUse()) return;
            if (!m_enableToMove) return;
            if (Owner == null || Owner.GroundTargetPosition == Position.Nowhere) return;
            if (Owner == null || Owner.GroundTargetPosition == Position.Nowhere) return;
            if (Coordinate.DistanceTo(Owner.GroundTargetPosition) > 1000)
            {
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.TargetTooFarToMove"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (!Owner.GroundTargetInView)
            {
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.TargetOutOfSight"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            //let's check if we are trying to move too close to a door, if we are, don't move
            foreach (IDoor door in Owner.CurrentRegion.GetDoorsInRadius(Owner.GroundTargetPosition.Coordinate, (ushort)(AttackRange - 50), false))
            {
                if (door is GameKeepDoor)
                {
                    Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.RamTooCloseToDoor"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }

            //unarmed siege weapon
            CurrentState &= ~eState.Armed;
            PathTo(Owner.GroundTargetPosition.Coordinate, 100);
        }

        public void StopMove()
        {
            StopMoving();
        }

        public void Load(int ammo)
        {
            AmmoSlot = (ushort)ammo;
        }
        public virtual void Aimed()
        {
            var ownerPlayer = Owner as GamePlayer;
            if (!CanUse()) return;
            CurrentState |= eState.Aimed;
            if (Owner != null)
            {
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.WeaponAimed", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
        public virtual void Armed()
        {
            var ownerPlayer = Owner as GamePlayer;
            if (!CanUse()) return;
            CurrentState |= eState.Armed;
            if (Owner != null)
            {
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.WeaponArmed", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
        public virtual void Fire()
        {
            var ownerPlayer = Owner as GamePlayer;
            if (!CanUse()) return;
            if (CurrentState != eState.Ready)
            {
                if (Owner != null)
                {
                    Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.NotReadyToFire", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                return;
            }
            if (TargetObject != null) GroundTargetPosition = TargetObject.Position;
            if (GroundTargetPosition == Position.Nowhere) return;
            new RegionTimer(this, new RegionTimerCallback(MakeDelayedDamage), GetActionDelay(SiegeTimer.eAction.Fire));
            BroadcastFireAnimation(GetActionDelay(SiegeTimer.eAction.Fire));
            if (Owner != null)
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.YouFireWeapon", GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            Arm();
        }

        private int MakeDelayedDamage(RegionTimer callingTimer)
        {
            DoDamage();
            return 0;
        }

        public virtual void DoDamage()
        {
        }
        /*	slot:48 level:90 value1:0x00 value2:0x00 hand:0x00 damageType:0x00 objectType:0x29 weight:2    con:100 dur:0   qual:0   bonus:0  model:0x0A3C color:0x0000 effect:0x00 unk1_172:1 "2 greek fire"
			slot:49 level:90 value1:0x00 value2:0x00 hand:0x00 damageType:0x00 objectType:0x29 weight:2    con:100 dur:0   qual:0   bonus:0  model:0x0A3D color:0x0000 effect:0x00 unk1_172:1 "2 ice ball"
			index:0  unk1:0x5A00 unk2:0x0000 unk3:0x2900 unk4:0x0264 unk5:0x00 unk6:0x0000 model:0x0A3D unk7:0x0001 unk7:0x0000 name:"2 ice ball")
			index level value1  value2 hand  objecttype damagetype weight conc dur qual bonnus model color effect
		*/

        public void Repair()
        {
            if (TimesRepaired <= 3)
            {
                if ((Owner as GamePlayer)?.GetCraftingSkillValue(eCraftingSkill.WoodWorking) < 301)
                {
                    if (Owner is GamePlayer player)
                        Owner.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameSiegeWeapon.NeedWoodworkingToRepair"), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                    return;
                }
                TimesRepaired = TimesRepaired + 1;
                Health += (int)(this.MaxHealth * 0.15);
            }
            else
            {
                if (Owner is GamePlayer player)
                    this.Owner.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameSiegeWeapon.DecayedBeyondRepair"), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
            }
        }

        public void salvage()
        {
            if ((Owner as GamePlayer)?.GetCraftingSkillValue(eCraftingSkill.SiegeCrafting) == -1)
            {
                if (Owner is GamePlayer player)
                    Owner.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameSiegeWeapon.NeedCrafterToSalvage"), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                return;
            }
            (Owner as GamePlayer)?.SalvageSiegeWeapon(this);
        }
        #endregion
        #region private methods
        private void BroadcastAnimation()
        {
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendSiegeWeaponAnimation(this);
            }
        }
        private void BroadcastFireAnimation(int timer)
        {
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendSiegeWeaponFireAnimation(this, timer);
                //				player.Out.SendEmoteAnimation(this, (eEmote)201); // on trebuchet fire external ammo ?
            }
        }
        protected int[] ActionDelay;
        private ushort m_effect;
        private bool m_enableToMove;

        /// <summary>
        /// delay to do action in Ms
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        private int GetActionDelay(SiegeTimer.eAction action)
        {
            if (action == SiegeTimer.eAction.Fire && TargetObject != null)
                return (int)(ActionDelay[(int)action] * 0.001 * this.GetDistanceTo(TargetObject));

            int delay = ActionDelay[(int)action];
            //TODO: better to use a property here - discuss to implement one? dunnow if siegespeed is used at another place.
            if (Owner != null && Owner.EffectList.CountOfType<BannerOfBesiegingEffect>() > 0)
            {
                BannerOfBesiegingEffect eff = Owner.EffectList.GetOfType<BannerOfBesiegingEffect>();
                if (eff != null)
                    delay = (int)(delay * (1 - eff.Value / 100));

            }
            return delay;
        }

        private Boolean CanUse()
        {
            var ownerPlayer = Owner as GamePlayer;

            if (Owner == null)
                return false;
            Owner.Stealth(false);
            if (!Owner.IsAlive || Owner.IsMezzed || Owner.IsStunned)
            {
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.CantUseWeaponNow"), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (Health <= DecayedHp)
            {
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.NeedsRepair"), eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (!this.IsWithinRadius(this.Owner, SIEGE_WEAPON_CONTROLE_DISTANCE))
            {
                Owner.SendMessage(LanguageMgr.GetTranslation(ownerPlayer!.Client.Account.Language, "GameSiegeWeapon.TooFarToControl"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            return true;
        }

        private void PreAction()
        {
            if (SiegeWeaponTimer.IsAlive)
            {
                SiegeWeaponTimer.Stop();
                (Owner as GamePlayer)?.Out?.SendSiegeWeaponCloseInterface();
            }
            SiegeWeaponTimer.Start(GetActionDelay(SiegeWeaponTimer.CurrentAction));
            if (Owner != null)
            {
                (Owner as GamePlayer)?.Out?.SendSiegeWeaponInterface(this, GetActionDelay(SiegeWeaponTimer.CurrentAction) / 100);
            }
            BroadcastAnimation();
        }
        #endregion
        #region override function
        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            if (source is GamePlayer)
            {
                damageAmount /= 30;
                criticalAmount /= 30;
            }
            base.TakeDamage(source, damageType, damageAmount, criticalAmount);
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            if (Owner == player)
                return false;

            TakeControl(player);
            return true;
        }
        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);
            if (!(obj is ItemTemplate)) return;
            ItemTemplate item = (ItemTemplate)obj;
            this.TranslationId = item.TranslationId;
            this.Name = item.Name;
            this.ExamineArticle = item.ExamineArticle;
            this.MessageArticle = item.MessageArticle;
            this.Model = (ushort)item.Model;
        }

        public bool EnableToMove
        {
            set { m_enableToMove = value; }
            get { return m_enableToMove; }
        }

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;
            StartDecay();
            TimesRepaired = 0;
            return true;
        }
        public override bool RemoveFromWorld()
        {
            if (!base.RemoveFromWorld()) return false;
            StopDecay();
            return true;
        }

        #endregion
        #region decay
        public override void StartHealthRegeneration()
        {
            //don't regenerate health
        }

        private void StartDecay()
        {
            if (ObjectState != eObjectState.Active)
                return;
            lock (m_decayTimerLock)
            {
                if (m_decayTimer == null)
                {
                    m_decayTimer = new RegionTimer(this);
                    m_decayTimer.Callback = new RegionTimerCallback(DecayTimerCallback);
                }
                else if (m_decayTimer.IsAlive)
                    return;
                m_decayTimer.Start(DECAYPERIOD);
            }
        }

        private void StopDecay()
        {
            lock (m_decayTimerLock)
            {
                if (m_decayTimer == null)
                    return;
                m_decayTimer.Stop();
                m_decayTimer = null;
            }
        }

        private int DecayTimerCallback(RegionTimer callingTimer)
        {
            TakeDamage(this, eDamageType.Natural, DeductHp, 0);
            return DECAYPERIOD;
        }

        #endregion

        private static SpellLine m_SiegeSpellLine;
        public static SpellLine SiegeSpellLine
        {
            get
            {
                if (m_SiegeSpellLine == null)
                    m_SiegeSpellLine = new SpellLine("SiegeSpellLine", "Siege Weapon Spells", "unknown", false);

                return m_SiegeSpellLine;
            }
        }
    }
    #endregion
    #region siegeTimer
    public class SiegeTimer : RegionAction
    {
        public enum eAction : byte
        {
            None = 0x00,
            Aiming = 0x01,
            Arming = 0x02,
            Loading = 0x03,
            Fire = 0x04,
        }

        /// <summary>
        /// Constructs a new UseSlotAction
        /// </summary>
        /// <param name="siegeWeapon">The siege weapon</param>
        public SiegeTimer(GameSiegeWeapon siegeWeapon)
            : base(siegeWeapon)
        {
            m_siegeWeapon = siegeWeapon;
        }

        private eAction m_currentAction;
        private GameSiegeWeapon m_siegeWeapon;

        public eAction CurrentAction
        {
            get { return m_currentAction; }
            set { m_currentAction = value; }
        }
        public GameSiegeWeapon SiegeWeapon
        {
            get { return m_siegeWeapon; }
            set { m_siegeWeapon = value; }
        }

        public override void OnTick()
        {
            if (SiegeWeapon.Owner is GamePlayer player)
                player.SendMessage("Action = " + CurrentAction, eChatType.CT_Say, eChatLoc.CL_SystemWindow);
            else return;
            switch (CurrentAction)
            {
                case eAction.Arming:
                    {
                        SiegeWeapon.Armed();
                        break;
                    }
                case eAction.Aiming:
                    {
                        SiegeWeapon.Aimed();
                        break;
                    }
                case eAction.Loading:
                    {
                        //todo set ammo
                        break;
                    }
                case eAction.Fire:
                    {
                        SiegeWeapon.DoDamage();
                        break;
                    }
                default: break;
            }

            if (SiegeWeapon.Owner != null)
            {
                player.Out.SendSiegeWeaponInterface(this.SiegeWeapon, 0);
            }
            if ((SiegeWeapon.CurrentState & GameSiegeWeapon.eState.Armed) != GameSiegeWeapon.eState.Armed)
                SiegeWeapon.Arm();
        }
    }
    #endregion
}
/* messages:

You are too far from your siege equipment to control it any longer!
You can't salvage the trebuchet!
The trebuchet's target is too far away to reach!
Your target has moved out of range!
You put the ice ball into the field catapult.
The trebuchet is fully repaired!
That object isn't carryable...
*/