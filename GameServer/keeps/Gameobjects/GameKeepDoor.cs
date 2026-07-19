using Discord;
using DOL.Database;
using DOL.Events;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Collections;
using System.Numerics;
using static DOL.GS.GameLiving;
using static DOL.GS.GameSiegeWeapon;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Vector = DOL.GS.Geometry.Vector;

namespace DOL.GS.Keeps
{
    /// <summary>
    /// keep door in world
    /// </summary>
    public class GameKeepDoor : GameDoorBase, IKeepItem
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        #region properties
        private const int DOOR_CLOSE_THRESHOLD = 15;
        private const int REPAIR_INTERVAL = 30 * 60 * 1000;

        protected int m_oldMaxHealth;
        protected byte m_oldHealthPercent;

        private bool _relicMessage75, _relicMessage50, _relicMessage25;

        public int OwnerKeepID => DoorID / 100000 % 1000;
        public int TowerNum => DoorID / 10000 % 10;
        public int KeepID => OwnerKeepID + TowerNum * 256;
        public int ComponentID => DoorID / 100 % 100;
        public int DoorIndex => DoorID % 10;

        /// <summary>
        /// This flag is send in packet(keep door = 4, regular door = 0)
        /// </summary>
        public override uint Flag
        {
            get => 4;
            set { }
        }

        /// <summary>
        /// Get the realm of the keep door from keep owner
        /// </summary>
        public override eRealm Realm
        {
            get
            {
                if (DbDoor != null && DbDoor.Realm != 0 && DbDoor.Realm != 6)
                    return (eRealm)DbDoor.Realm;
                return Component == null || Component.Keep == null ? base.Realm : Component.Keep.Realm;
            }
            set
            {
                if (DbDoor != null) DbDoor.Realm = (byte)value;
                base.Realm = value;
            }
        }

        /// <summary>
        /// The level of door is keep level now
        /// </summary>
        public override byte Level
        {
            get
            {
                if (DbDoor != null && DbDoor.Level != 0) return DbDoor.Level;
                return Component == null || Component.Keep == null ? base.Level : Component.Keep.Level;
            }
            set
            {
                base.Level = value;
                if (DbDoor != null) DbDoor.Level = value;
            }
        }

        public bool IsRelic => Component != null && Component.Keep != null && Component.Keep.DBKeep != null && Component.Keep.DBKeep.SkinType == 99;

        /// <summary>
        /// Get the ZoneID of this door
        /// </summary>
        public override ushort ZoneID
        {
            get
            {
                if (Component?.Keep?.CurrentZone != null)
                    return Component.Keep.CurrentZone.ID;
                if (CurrentZone != null)
                    return CurrentZone.ID;

                return 0;
            }
        }

        public void UpdateLevel()
        {
            if (MaxHealth != m_oldMaxHealth)
            {
                if (m_oldMaxHealth > 0)
                    Health = (int)Math.Ceiling(Health * MaxHealth / (double)m_oldMaxHealth);
                else
                    Health = MaxHealth;

                m_oldMaxHealth = MaxHealth;
            }
            SaveIntoDatabase();
        }

        public override bool IsAttackableDoor
        {
            get
            {
                if (Component == null || Component.Keep == null)
                    return false;

                if (Component.Keep is GameKeepTower)
                {
                    if (DoorIndex == 1) return true;
                }
                else if (Component.Keep is GameKeep)
                {
                    if (Component.Skin == 10 || Component.Skin == 30) // inner keep
                    {
                        if (DoorIndex == 1) return true;
                    }
                    if (Component.Skin == 0 || Component.Skin == 24) // main gate
                    {
                        if (DoorIndex == 1 || DoorIndex == 2) return true;
                    }
                }
                return false;
            }
        }

        public override int Health
        {
            get => !IsAttackableDoor ? 0 : base.Health;
            set
            {
                base.Health = value;

                if (HealthPercent > DOOR_CLOSE_THRESHOLD && State == eDoorState.Open)
                    CloseDoor();
            }
        }

        public override int RealmPointsValue => 0;
        public override long ExperienceValue => 0;

        public override string Name
        {
            get
            {
                string name = IsAttackableDoor ? (IsRelic ? "Relic Gate" : "Keep Door") : "Postern Door";

                if (Properties.ENABLE_DEBUG)
                    name += $" ( C:{ComponentID} T:{TemplateID})";

                return name;
            }
        }

        protected string m_templateID;
        public string TemplateID => m_templateID;

        protected GameKeepComponent m_component;
        public GameKeepComponent Component
        {
            get { return m_component; }
            set { m_component = value; }
        }

        protected DBKeepPosition m_position;
        public DBKeepPosition DBPosition
        {
            get { return m_position; }
            set { m_position = value; }
        }
        #endregion

        #region function override

        /// <summary>
        /// Procs don't normally fire on game keep components
        /// </summary>
        /// <param name="ad"></param>
        /// <param name="weapon"></param>
        /// <returns></returns>
        public override bool AllowWeaponMagicalEffect(AttackData ad, InventoryItem weapon, Spell weaponSpell)
        {
            return weapon.Flags == 10;
        }

        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            if (damageAmount > 0 && IsAlive)
            {
                Component.Keep.LastAttackedByEnemyTick = CurrentRegion.Time;
                base.TakeDamage(source, damageType, damageAmount, criticalAmount);

                if (m_oldHealthPercent != HealthPercent)
                {
                    m_oldHealthPercent = HealthPercent;
                    foreach (GameClient client in WorldMgr.GetClientsOfRegion(CurrentRegionID))
                        client.Out.SendObjectUpdate(this);
                }

                if (IsRelic)
                {
                    if (HealthPercent <= 25 && !_relicMessage25) { _relicMessage25 = true; BroadcastGateDamage(); }
                    else if (HealthPercent <= 50 && HealthPercent > 25 && !_relicMessage50) { _relicMessage50 = true; BroadcastGateDamage(); }
                    else if (HealthPercent <= 75 && HealthPercent > 50 && !_relicMessage75) { _relicMessage75 = true; BroadcastGateDamage(); }
                }
            }
        }

        /// <summary>
        /// boradcast gate beeing attacked to all players in the world
        /// </summary>
        private void BroadcastGateDamage()
        {
            foreach (GameClient client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player != null && client.Player.Realm == Realm)
                {
                    string msg = LanguageMgr.GetTranslation(client.Account.Language, "DoorRequestHandler.GameKeepDoor.KeepUnderAttack", Component.Keep.Name);
                    client.Player.Out.SendMessage(msg, eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
                    client.Player.Out.SendMessage(msg, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
        }

        public override void ModifyAttack(AttackData attackData)
        {
            if (attackData.DamageType == eDamageType.GM)
                return;

            int toughness = Component.Keep is GameKeepTower ? Properties.SET_TOWER_DOOR_TOUGHNESS : Properties.SET_KEEP_DOOR_TOUGHNESS;
            GameLiving source = attackData.Attacker;

            int baseDamage = attackData.Damage;
            int styleDamage = attackData.StyleDamage;
            int criticalDamage = 0;

            if (source is GamePlayer)
            {
                baseDamage = GetAdjustedDamage(baseDamage, toughness, Component.Keep.Level);
                styleDamage = GetAdjustedDamage(styleDamage, toughness, Component.Keep.Level);
            }
            else if (source is GameNPC npcSource)
            {
                if (!Properties.DOORS_ALLOWPETATTACK)
                {
                    attackData.AttackResult = eAttackResult.NotAllowed_ServerRules;
                    baseDamage = 0;
                    styleDamage = 0;
                }
                else
                {
                    baseDamage = GetAdjustedDamage(baseDamage, toughness, Component.Keep.Level);
                    styleDamage = GetAdjustedDamage(styleDamage, toughness, Component.Keep.Level);

                    if (npcSource.Brain is AI.Brain.IControlledBrain brain && brain.Owner is GamePlayer player)
                    {
                        double multiplier = (player.CharacterClass.ID == (int)eCharacterClass.Theurgist || player.CharacterClass.ID == (int)eCharacterClass.Animist) ?
                            Properties.PET_SPAM_DAMAGE_MULTIPLIER : Properties.PET_DAMAGE_MULTIPLIER;

                        baseDamage = (int)(baseDamage * multiplier);
                        styleDamage = (int)(styleDamage * multiplier);
                    }
                }
            }

            attackData.Damage = baseDamage;
            attackData.StyleDamage = styleDamage;
            attackData.CriticalDamage = criticalDamage;

            static int GetAdjustedDamage(int damage, int toughnessVal, int level)
            {
                return (damage - damage * 5 * level / 100) * toughnessVal / 100;
            }
        }

        /// <summary>
        /// This function is called from the ObjectInteractRequestHandler
        /// It teleport player in the keep if player and keep have the same realm
        /// </summary>
        /// <param name="player">GamePlayer that interacts with this object</param>
        /// <returns>false if interaction is prevented</returns>
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            bool isEnemy = false;

            if (DbDoor != null && DbDoor.Realm != 0 && DbDoor.Realm != 6)
            {
                isEnemy = player.Realm != (eRealm)DbDoor.Realm;
            }
            else if (Component != null && Component.Keep != null)
            {
                isEnemy = GameServer.KeepManager.IsEnemy(this, player);
            }
            else
            {
                isEnemy = (player.Realm != this.Realm && this.Realm != eRealm.None && this.Realm != eRealm.Door);
            }

            // If it's an enemy door and the player is not a GM, it is locked.
            if (isEnemy && player.Client.Account.PrivLevel == 1)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.GameKeepDoor.DoorLocked", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            int keepz = Position.Z, distance = 0;

            if (Component!.Skin == 10 || Component.Skin == 30)
            {
                distance = 190;
            }
            else
            {
                if (DoorIndex == 1) distance = 150;
                else if (DoorIndex >= 3 && DoorIndex <= 6) distance = 175;
                else distance = 100;
            }

            if (Component.Keep is GameKeepTower && !Component.Keep.IsPortalKeep)
            {
                if (IsObjectInFront(player, 180, false))
                {
                    if (DoorID == 1) keepz = Position.Z + 83;
                    else distance = 150;
                }
            }
            else
            {
                if (IsObjectInFront(player, 180, false))
                {
                    var keepdistance = float.MaxValue;
                    var gatedistance = float.MaxValue;
                    foreach (GameKeepComponent c in Component.Keep.KeepComponents)
                    {
                        if ((GameKeepComponent.eComponentSkin)c.Skin == GameKeepComponent.eComponentSkin.Keep)
                            keepdistance = (float)Coordinate.DistanceTo(c.Position);
                        if ((GameKeepComponent.eComponentSkin)c.Skin == GameKeepComponent.eComponentSkin.Gate)
                            gatedistance = (float)Coordinate.DistanceTo(c.Position);

                        if (keepdistance != float.MaxValue && gatedistance != float.MaxValue) break;
                    }
                    if (DoorIndex == 1 && keepdistance < gatedistance)
                        keepz = Position.Z + 92;
                }
            }

            Position keepPoint = IsObjectInFront(player, 180, false)
                ? Position + Vector.Create(Orientation, -distance)
                : Position + Vector.Create(Orientation, distance);

            player.MoveTo(keepPoint.With(z: keepz).With(player.Orientation));

            return true;
        }

        public override IList GetExamineMessages(GamePlayer player)
        {
            IList list = base.GetExamineMessages(player);
            string lang = player.Client.Account.Language;
            string text = LanguageMgr.GetTranslation(lang, "DoorRequestHandler.GameKeepDoor.YouSelect", Name);

            bool isEnemy = false;

            if (DbDoor != null && DbDoor.Realm != 0 && DbDoor.Realm != 6)
            {
                isEnemy = player.Realm != (eRealm)DbDoor.Realm;
            }
            else if (Component != null && Component.Keep != null)
            {
                isEnemy = GameServer.KeepManager.IsEnemy(this, player);
            }
            else
            {
                isEnemy = (player.Realm != this.Realm && this.Realm != eRealm.None && this.Realm != eRealm.Door);
            }

            if (!isEnemy)
                text += LanguageMgr.GetTranslation(lang, "DoorRequestHandler.GameKeepDoor.BelongsYourRealm");
            else
                text += IsAttackableDoor ? LanguageMgr.GetTranslation(lang, "DoorRequestHandler.GameKeepDoor.BelongsEnemyAttackable") : LanguageMgr.GetTranslation(lang, "DoorRequestHandler.GameKeepDoor.BelongsEnemy");

            list.Add(text);

            if (IsAttackableDoor)
            {
                if (Health <= 0 && State != eDoorState.Open)
                {
                    State = eDoorState.Open;
                    BroadcastDoorStatus();
                }
                else if (State == eDoorState.Open)
                {
                    player.SendDoorUpdate(this, true);
                }
            }

            return list;
        }

        public override string GetName(int article, bool firstLetterUppercase)
        {
            return "the " + base.GetName(article, firstLetterUppercase);
        }

        /// <summary>
        /// Starts the health regeneration
        /// </summary>
        public override void StartHealthRegeneration()
        {
            if (!IsAttackableDoor || m_healthRegenerationTimer != null && m_healthRegenerationTimer.IsAlive || Health >= MaxHealth)
                return;

            m_healthRegenerationTimer = new RegionTimer(this, new RegionTimerCallback(HealthRegenerationTimerCallback), REPAIR_INTERVAL);
        }

        protected override int HealthRegenerationTimerCallback(RegionTimer timer)
        {
            if (Component?.Keep == null || HealthPercent >= 100)
            {
                timer.Stop();
                return 0;
            }

            if (!Component.Keep.InCombat)
                Repair(MaxHealth / 100 * 5);

            return REPAIR_INTERVAL;
        }

        public void DeleteObject()
        {
            StopHealthRegeneration();
            if (Component != null)
            {
                if (Component.Keep != null)
                    Component.Keep.Doors.Remove(ObjectID.ToString());
                Component.Delete();
            }
            Component = null;
            DBPosition = null;
            base.Delete();
            CurrentRegion = null;
        }
        #endregion

        #region Save/load DB
        public override void SaveIntoDatabase()
        {
            // Allow DB save only if the door has been manually added to the DB (without beeing spawned by Keepcomponents)
            if (DbDoor != null)
            {
                DbDoor.Health = Health;
                DbDoor.MaxHealth = MaxHealth;
                GameServer.Database.SaveObject(DbDoor);
            }
        }

        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);
            if (obj is not DBDoor dbDoor) return;

            foreach (AbstractArea area in CurrentAreas)
            {
                if (area is KeepArea keepArea)
                {
                    string sKey = dbDoor.InternalID.ToString();
                    if (!keepArea.Keep.Doors.ContainsKey(sKey))
                    {
                        Component = new GameKeepComponent { Keep = keepArea.Keep };
                        keepArea.Keep.Doors.Add(sKey, this);
                        Realm = (eRealm)keepArea.Keep.Realm;
                    }
                    break;
                }
            }

            if (!IsAttackableDoor || HealthPercent > DOOR_CLOSE_THRESHOLD)
                State = eDoorState.Closed;

            StartHealthRegeneration();
            DoorMgr.RegisterDoor(this);
        }

        public virtual void LoadFromPosition(DBKeepPosition pos, GameKeepComponent component)
        {
            m_templateID = pos.TemplateID;
            m_component = component;

            PositionMgr.LoadKeepItemPosition(pos, this);
            component.Keep.Doors[m_templateID] = this;

            DoorID = GenerateDoorID();

            var dbDoor = GameServer.Database.SelectObject<DBDoor>(DB.Column("InternalID").IsEqualTo(DoorID));
            if (dbDoor != null)
            {
                DbDoor = dbDoor;
                InternalID = dbDoor.ObjectId;
                m_oldMaxHealth = dbDoor.MaxHealth > 0 ? dbDoor.MaxHealth : MaxHealth;
                Health = dbDoor.Health > 0 ? dbDoor.Health : MaxHealth;
                Locked = dbDoor.Locked;
            }
            else
            {
                m_oldMaxHealth = MaxHealth;
                Health = MaxHealth;
            }

            m_oldHealthPercent = HealthPercent;
            Model = 0xFFFF;
            State = eDoorState.Closed;

            if (AddToWorld())
            {
                StartHealthRegeneration();
                DoorMgr.RegisterDoor(this);
            }
            else
            {
                log.Error($"Failed to load keep door from keepposition_id ={pos.ObjectId}. Component SkinID={component.Skin}. KeepID={component.Keep.KeepID}");
            }
        }

        public void MoveToPosition(DBKeepPosition position) { }

        public int GenerateDoorID()
        {
            int doortype = 7;
            int ownerKeepID = Component.Keep is GameKeepTower tower ? (tower.Keep != null ? tower.Keep.KeepID : tower.OwnerKeepID) : Component.Keep.KeepID;
            int towerIndex = Component.Keep is GameKeepTower ? Component.Keep.KeepID >> 8 : 0;
            return (doortype * 100000000) + (ownerKeepID * 100000) + (towerIndex * 10000) + (Component.ID * 100) + DBPosition.TemplateType;
        }
        #endregion

        /// <summary>
        /// call when player try to open door
        /// </summary>
        public override void Open(GameLiving opener = null)
        {
            if (opener is GamePlayer player && player.Client.Account.PrivLevel >= 1 && State == eDoorState.Closed)
            {
                State = eDoorState.Open;
            }
        }

        /// <summary>
        /// call when player try to close door
        /// </summary>
        public override void Close(GameLiving closer = null)
        {
            if (closer is GamePlayer player && player.Client.Account.PrivLevel >= 1 && State == eDoorState.Open)
            {
                CloseDoor();
            }
        }

        public override void Die(GameObject killer)
        {
            base.Die(killer);

            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.GameKeepDoor.DoorBroken", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            State = eDoorState.Open;
            BroadcastDoorStatus();
            SaveIntoDatabase();
        }

        /// <summary>
        /// This method is called when door is repair or keep is reset
        /// </summary>
        public virtual void CloseDoor()
        {
            State = eDoorState.Closed;
        }

        /// <summary>
        /// This Function is called when door has been repaired
        /// </summary>
        /// <param name="amount">how many HP is repaired</param>
        public void Repair(int amount)
        {
            Health = Math.Min(Health + amount, MaxHealth);

            if (HealthPercent > 25) _relicMessage25 = false;
            if (HealthPercent > 50) _relicMessage50 = false;
            if (HealthPercent > 75) _relicMessage75 = false;

            BroadcastDoorStatus();
            SaveIntoDatabase();
        }

        /// <summary>
        /// This Function is called when keep is taken to repair door
        /// </summary>
        /// <param name="realm">new realm of keep taken</param>
        public void Reset(eRealm realm)
        {
            Realm = realm;
            Health = MaxHealth;
            m_oldHealthPercent = HealthPercent;
            _relicMessage25 = false;
            _relicMessage50 = false;
            _relicMessage75 = false;
            CloseDoor();
            SaveIntoDatabase();
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str) || source is not GamePlayer player) return false;

            str = str.ToLower();
            if (player.Client.Account.PrivLevel > 1)
            {
                if (str.Contains("open")) { Open(player); return true; }
                if (str.Contains("close")) { Close(player); return true; }
            }

            if (str.Contains("enter") || str.Contains("exit")) Interact(player);

            return true;
        }

        public override bool SayReceive(GameLiving source, string str)
        {
            if (!base.SayReceive(source, str) || source is not GamePlayer player) return false;

            str = str.ToLower();
            if (player.Client.Account.PrivLevel > 1)
            {
                if (str.Contains("open")) { Open(player); return true; }
                if (str.Contains("close")) { Close(player); return true; }
            }

            if (str.Contains("enter") || str.Contains("exit")) Interact(player);

            return true;
        }
    }
}