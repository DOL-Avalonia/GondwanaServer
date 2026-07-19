using DOL.Database;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using static DOL.GS.Zone;

namespace DOL.GS
{
    public abstract class GameDoorBase : GameLiving, IDoor
    {
        //public override eGameObjectType GameObjectType => eGameObjectType.Door; --> TO DO from OPENDAOC
        private int m_doorId;
        protected eDoorState m_state = eDoorState.Closed;
        protected readonly object m_stateLock = new object();

        public DBDoor DbDoor { get; set; }
        public virtual bool CanBeOpenedViaInteraction => false;

        /// <summary>
        /// door index which is unique
        /// </summary>
        public virtual int DoorID
        {
            get => DbDoor == null ? m_doorId : DbDoor.InternalID;
            set
            {
                if (DbDoor == null)
                    m_doorId = value;
                else
                    DbDoor.InternalID = value;
            }
        }

        /// <summary>
        /// Get the ZoneID of this door
        /// </summary>
        public virtual ushort ZoneID
        {
            get
            {
                if (DoorID >= 700000000 && DoorID < 800000000)
                {
                    return CurrentZone != null ? CurrentZone.ID : (ushort)0;
                }
                return (ushort)(DoorID / 1000000);
            }
        }

        /// <summary>
        /// The state of door (open or close)
        /// </summary>
        public virtual eDoorState State
        {
            get => m_state;
            set
            {
                if (m_state == value) return;

                lock (m_stateLock)
                {
                    if (m_state == value) return;
                    m_state = value;

                    if (PathingMgr.Instance != null)
                        PathingMgr.Instance.UpdateDoorFlags(this);

                    BroadcastDoorStatus();
                }
            }
        }

        /// <summary>
        /// door open = 0 / lock = 1 
        /// </summary>
        public virtual int Locked
        {
            get => DbDoor != null ? DbDoor.Locked : 0;
            set { if (DbDoor != null) DbDoor.Locked = value; }
        }

        /// <summary>
        /// This is used to identify what sound a door makes when open / close
        /// </summary>
        public virtual uint Flag
        {
            get => DbDoor == null ? 0 : DbDoor.Flags;
            set { if (DbDoor != null) DbDoor.Flags = value; }
        }

        /// <summary>
        /// Door Type
        /// </summary>
        public virtual int Type
        {
            get => DbDoor == null ? 0 : DbDoor.Type;
            set { if (DbDoor != null) DbDoor.Type = value; }
        }

        public virtual void Close(GameLiving closer = null) { }
        public virtual void Open(GameLiving opener = null) { }

        /// <summary>
        /// boradcast the door status to all player near the door
        /// </summary>
        public virtual void BroadcastDoorStatus()
        {
            foreach (GamePlayer player in this.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                player.SendDoorUpdate(this);
        }

        public virtual void NPCManipulateDoorRequest(GameNPC npc, bool open)
        {
            npc.TurnTo(this.Coordinate);
            if (open && State != eDoorState.Open)
                this.Open();
            else if (!open && State != eDoorState.Closed)
                this.Close();
        }

        /// <summary>
        /// This function is called from the ObjectInteractRequestHandler
        /// It teleport player in the keep if player and keep have the same realm
        /// </summary>
        /// <param name="player">GamePlayer that interacts with this object</param>
        /// <returns>false if interaction is prevented</returns>
        public override bool Interact(GamePlayer player)
        {
            if (!player.IsAlive) return false;

            if (player.Client.Account.PrivLevel == 1 && !player.IsWithinRadius(this, Properties.WORLD_PICKUP_DISTANCE * 2))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.OnTick.TooFarAway", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsMezzed)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.YouMezzed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsStunned)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DoorRequestHandler.YouStunned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            return base.Interact(player);
        }

        /// <summary>
        /// Loads main parameters of this door from a door table slot
        /// </summary>
        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);

            if (obj is not DBDoor dbDoor)
                return;

            DbDoor = dbDoor;
            Zone curZone = WorldMgr.GetZone((ushort)(dbDoor.InternalID / 1000000));

            if (curZone != null)
                CurrentRegion = curZone.ZoneRegion;

            m_name = dbDoor.Name;
            Position = Position.Create(CurrentRegion != null ? CurrentRegion.ID : (ushort)0, dbDoor.X, dbDoor.Y, dbDoor.Z, (ushort)dbDoor.Heading);
            m_guildName = dbDoor.Guild;
            m_level = dbDoor.Level;
            Realm = (eRealm)dbDoor.Realm;
            m_health = dbDoor.Health;
            m_maxHealth = dbDoor.MaxHealth;
            m_model = 0xFFFF;
            m_state = eDoorState.Closed;

            AddToWorld();
            StartHealthRegeneration(); // Safe native GameLiving health regeneration
        }

        /// <summary>
        /// save the main parameters of this door to a door table slot
        /// </summary>
        public override void SaveIntoDatabase()
        {
            DBDoor obj = DbDoor;
            obj ??= new DBDoor();

            obj.Name = Name;
            obj.Type = DoorID / 100000000;
            obj.Z = Position.Z;
            obj.Y = Position.Y;
            obj.X = Position.X;
            obj.Heading = Orientation.InHeading;
            obj.InternalID = DoorID;
            obj.Guild = GuildName;
            obj.Level = Level;
            obj.Realm = (byte)Realm;
            obj.Flags = Flag;
            obj.Locked = Locked;
            obj.Health = Health;
            obj.MaxHealth = MaxHealth;

            if (InternalID == null)
            {
                GameServer.Database.AddObject(obj);
                InternalID = obj.ObjectId;
                DbDoor = obj;
            }
            else
                GameServer.Database.SaveObject(obj);
        }
    }
}