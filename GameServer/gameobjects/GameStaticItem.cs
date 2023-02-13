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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.Database;
using DOL.Events;
using DOL.GameEvents;
using DOL.Language;

namespace DOL.GS
{
    /// <summary>
    /// This class represents a static Item in the gameworld
    /// </summary>
    public class GameStaticItem : GameObject, ITranslatableObject
    {
        /// <summary>
        /// The emblem of the Object
        /// </summary>
        protected int m_Emblem;

        /// <summary>
        /// The respawn interval of this world object
        /// </summary>
        protected int m_respawnInterval = 0;

        public int RespawnInterval
        {
            get { return m_respawnInterval; }
            set { m_respawnInterval = value; }
        }

        /// <summary>
        /// Constructs a new GameStaticItem
        /// </summary>
        public GameStaticItem() : base()
        {
            m_owners = new ArrayList(1);
        }

        public GameStaticItem Copy()
        {
            GameStaticItem item = new GameStaticItem();
            item.Model = Model;
            item.RespawnInterval = RespawnInterval;
            item.Emblem = Emblem;
            item.TranslationId = TranslationId;
            item.EventID = EventID;
            item.Name = Name;
            item.ExamineArticle = ExamineArticle;
            item.Heading = Heading;
            item.Level = Level;
            item.Realm = Realm;
            item.Position = Position;
            return item;
        }

        #region Name/Model/GetName/GetExamineMessages
        /// <summary>
        /// gets or sets the model of this Item
        /// </summary>
        public override ushort Model
        {
            get { return base.Model; }
            set
            {
                base.Model = value;
                if (ObjectState == eObjectState.Active)
                {
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        player.Out.SendObjectCreate(this);
                }
            }
        }

        /// <summary>
        /// Gets or Sets the current Emblem of the Object
        /// </summary>
        public virtual int Emblem
        {
            get { return m_Emblem; }
            set
            {
                m_Emblem = value;
                if (ObjectState == eObjectState.Active)
                {
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        player.Out.SendObjectCreate(this);
                }
            }
        }

        public virtual LanguageDataObject.eTranslationIdentifier TranslationIdentifier
        {
            get { return LanguageDataObject.eTranslationIdentifier.eObject; }
        }

        /// <summary>
        /// The translation id
        /// </summary>
        protected string m_translationId = "";

        /// <summary>
        /// Gets or sets the translation id
        /// </summary>
        public string TranslationId
        {
            get { return m_translationId; }
            set { m_translationId = (value == null ? "" : value); }
        }

        public string EventID
        {
            get;
            set;
        }

        public virtual bool IsCoffre => false;


        /// <summary>
        /// Gets or sets the name of this item
        /// </summary>
        public override string Name
        {
            get
            {
                return base.Name;
            }
            set
            {
                base.Name = value;
                if (ObjectState == eObjectState.Active)
                {
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        player.Out.SendObjectCreate(this);
                }
            }
        }

        /// <summary>
        /// Holds the examine article
        /// </summary>
        private string m_examineArticle = "";
        /// <summary>
        /// Gets or sets the examine article
        /// </summary>
        public string ExamineArticle
        {
            get { return m_examineArticle; }
            set { m_examineArticle = (value == null ? "" : value); }
        }

        private bool m_loadedFromScript = true;
        public bool LoadedFromScript
        {
            get { return m_loadedFromScript; }
            set { m_loadedFromScript = value; }
        }



        /// <summary>
        /// Returns name with article for nouns
        /// </summary>
        /// <param name="article">0=definite, 1=indefinite</param>
        /// <param name="firstLetterUppercase">Forces the first letter of the returned string to be uppercase</param>
        /// <returns>name of this object (includes article if needed)</returns>
        public override string GetName(int article, bool firstLetterUppercase)
        {
            if (Name == "")
                return "";

            if (char.IsUpper(Name[0]))
            {
                // proper name

                if (firstLetterUppercase)
                    return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameStaticItem.GetName.Article1", Name);
                else
                    return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameStaticItem.GetName.Article2", Name);
            }
            else
            {
                // common noun
                return base.GetName(article, firstLetterUppercase);
            }
        }

        /// <summary>
        /// Adds messages to ArrayList which are sent when object is targeted
        /// </summary>
        /// <param name="player">GamePlayer that is examining this object</param>
        /// <returns>list with string messages</returns>
        public override IList GetExamineMessages(GamePlayer player)
        {
            IList list = base.GetExamineMessages(player);
            list.Insert(0, "You select " + GetName(0, false) + ".");
            return list;
        }
        #endregion

        public override void LoadFromDatabase(DataObject obj)
        {
            WorldObject item = obj as WorldObject;
            base.LoadFromDatabase(obj);

            m_loadedFromScript = false;
            CurrentRegionID = item.Region;
            TranslationId = item.TranslationId;
            Name = item.Name;
            ExamineArticle = item.ExamineArticle;
            Model = item.Model;
            Emblem = item.Emblem;
            Realm = (eRealm)item.Realm;
            Heading = item.Heading;
            Position = new Vector3(item.X, item.Y, item.Z);
            RespawnInterval = item.RespawnInterval;
        }

        /// <summary>
        /// Gets or sets the heading of this item
        /// </summary>
        public override ushort Heading
        {
            get { return base.Heading; }
            set
            {
                base.Heading = value;
                if (ObjectState == eObjectState.Active)
                {
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        player.Out.SendObjectCreate(this);
                }
            }
        }

        /// <summary>
        /// Gets or sets the level of this item
        /// </summary>
        public override byte Level
        {
            get { return base.Level; }
            set
            {
                base.Level = value;
                if (ObjectState == eObjectState.Active)
                {
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        player.Out.SendObjectCreate(this);
                }
            }
        }

        /// <summary>
        /// Gets or sets the realm of this item
        /// </summary>
        public override eRealm Realm
        {
            get { return base.Realm; }
            set
            {
                base.Realm = value;
            }
        }

        /// <summary>
        /// Saves this Item in the WorldObject DB
        /// </summary>
        public override void SaveIntoDatabase()
        {
            WorldObject obj = null;
            if (InternalID != null)
            {
                obj = (WorldObject)GameServer.Database.FindObjectByKey<WorldObject>(InternalID);
            }
            if (obj == null)
            {
                if (LoadedFromScript == false)
                {
                    obj = new WorldObject();
                }
                else
                {
                    return;
                }
            }
            obj.TranslationId = TranslationId;
            obj.Name = Name;
            obj.ExamineArticle = ExamineArticle;
            obj.Model = Model;
            obj.Emblem = Emblem;
            obj.Realm = (byte)Realm;
            obj.Heading = Heading;
            obj.Region = CurrentRegionID;
            obj.X = (int)Position.X;
            obj.Y = (int)Position.Y;
            obj.Z = (int)Position.Z;
            obj.ClassType = this.GetType().ToString();
            obj.RespawnInterval = RespawnInterval;

            if (InternalID == null)
            {
                GameServer.Database.AddObject(obj);
                InternalID = obj.ObjectId;
            }
            else
            {
                GameServer.Database.SaveObject(obj);
            }
        }

        public override void Delete()
        {
            Notify(GameObjectEvent.Delete, this);
            RemoveFromWorld(0); // will not respawn
            ObjectState = eObjectState.Deleted;
        }

        /// <summary>
        /// Deletes this item from the WorldObject DB
        /// </summary>
        public override void DeleteFromDatabase()
        {
            if (InternalID != null)
            {
                WorldObject obj = (WorldObject)GameServer.Database.FindObjectByKey<WorldObject>(InternalID);
                if (obj != null)
                    GameServer.Database.DeleteObject(obj);
            }
            InternalID = null;
        }

        /// <summary>
        /// Called to create an item in the world
        /// </summary>
        /// <returns>true when created</returns>
        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                player.Out.SendObjectCreate(this);
            return true;
        }

        /// <summary>
        /// Called to remove the item from the world
        /// </summary>
        /// <returns>true if removed</returns>
        public override bool RemoveFromWorld()
        {
            return RemoveFromWorld(RespawnInterval);
        }


        /// <summary>
        /// Get Coffres and their new EventIds from Db
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<Tuple<GameStaticItem, string>> GetCoffresUsedInEventsInDb(ushort region)
        {
            return null;
        }


        /// <summary>
        /// Temporarily remove this static item from the world.
        /// Used mainly for quest interaction
        /// </summary>
        /// <param name="respawnSeconds"></param>
        /// <returns></returns>
        public virtual bool RemoveFromWorld(int respawnSeconds)
        {
            if (ObjectState == eObjectState.Active)
            {
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.OBJ_UPDATE_DISTANCE))
                    player.Out.SendObjectRemove(this);
            }

            if (base.RemoveFromWorld())
            {
                if (respawnSeconds > 0)
                {
                    StartRespawn(Math.Max(1, respawnSeconds));
                }

                return true;
            }

            return false;
        }


        /// <summary>
        /// Timer used to respawn this object
        /// </summary>
        protected RegionTimer m_respawnTimer = null;

        /// <summary>
        /// The sync object for respawn timer modifications
        /// </summary>
        protected readonly object m_respawnTimerLock = new object();

        /// <summary>
        /// Starts the Respawn Timer
        /// </summary>
        protected virtual void StartRespawn(int respawnSeconds)
        {
            lock (m_respawnTimerLock)
            {
                if (m_respawnTimer == null)
                {
                    m_respawnTimer = new RegionTimer(this);
                    m_respawnTimer.Callback = new RegionTimerCallback(RespawnTimerCallback);
                    m_respawnTimer.Start(respawnSeconds * 1000);
                }
            }
        }

        /// <summary>
        /// The callback that will respawn this object
        /// </summary>
        /// <param name="respawnTimer">the timer calling this callback</param>
        /// <returns>the new interval</returns>
        protected virtual int RespawnTimerCallback(RegionTimer respawnTimer)
        {
            lock (m_respawnTimerLock)
            {
                if (m_respawnTimer != null)
                {
                    m_respawnTimer.Stop();
                    m_respawnTimer = null;
                    AddToWorld();
                }
            }

            return 0;
        }


        /// <summary>
        /// Holds the owners of this item, can be more than 1 person
        /// </summary>
        private readonly ArrayList m_owners;
        /// <summary>
        /// Adds an owner to this item
        /// </summary>
        /// <param name="player">the object that is an owner</param>
        public void AddOwner(GameObject player)
        {
            lock (m_owners)
            {
                foreach (WeakReference weak in m_owners)
                    if (weak.Target == player) return;
                m_owners.Add(new WeakRef(player));
            }
        }
        /// <summary>
        /// Tests if a specific gameobject owns this item
        /// </summary>
        /// <param name="testOwner">the owner to test for</param>
        /// <returns>true if this object owns this item</returns>
        public bool IsOwner(GameObject testOwner)
        {
            lock (m_owners)
            {
                //No owner ... return true
                if (m_owners.Count == 0) return true;

                foreach (WeakReference weak in m_owners)
                    if (weak.Target == testOwner) return true;
                return false;
            }
        }

        /// <summary>
        /// Returns an array of owners
        /// </summary>
        public GameObject[] Owners
        {
            get
            {
                ArrayList activeOwners = new ArrayList();
                foreach (WeakReference weak in m_owners)
                    if (weak.Target != null)
                        activeOwners.Add(weak.Target);
                return (GameObject[])activeOwners.ToArray(typeof(GameObject));
            }
        }

        /// <summary>
        /// Is this object visible to another?
        /// </summary>
        /// <param name="checkObject"></param>
        /// <returns></returns>
        public override bool IsVisibleTo(GameObject checkObject)
        {
            if (base.IsVisibleTo(checkObject))
            {
                if (EventID != null && EventID != "" && checkObject is GamePlayer player)
                {
                    var gameEvents = GameEventManager.Instance.Events.Where(e => e.ID.Equals(EventID));
                    switch (gameEvents.FirstOrDefault().InstancedConditionType)
                    {
                        case InstancedConditionTypes.All:
                            return true;
                        case InstancedConditionTypes.Player:
                            return gameEvents.Where(e => e.Owner != null && e.Owner == player && e.Coffres.Contains(this)).Any();
                        case InstancedConditionTypes.Group:
                            return gameEvents.Where(e => e.Owner != null && e.Owner.Group != null && e.Owner.Group.IsInTheGroup(player) && e.Coffres.Contains(this)).Any();
                        case InstancedConditionTypes.Guild:
                            return gameEvents.Where(e => e.Owner != null && e.Owner.Guild != null && e.Owner.Guild == player.Guild && e.Coffres.Contains(this)).Any();
                        case InstancedConditionTypes.Battlegroup:
                            return gameEvents.Where(e => e.Owner != null && e.Owner.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null) != null &&
                            e.Owner.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null) ==
                            player.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null) && e.Coffres.Contains(this)).Any();
                        default:
                            break;
                    }
                }
                else
                {
                    return true;
                }

            }
            return false;
        }
    }
}
