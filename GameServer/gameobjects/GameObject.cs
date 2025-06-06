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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using DOL.Database;
using DOL.Events;
using DOL.GS.Geometry;
using DOL.Language;
using DOL.GS.Quests;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using System.Threading.Tasks;
using DOL.GS.Utils;

using log4net;
using Microsoft.VisualBasic;
using DOL.AI.Brain;
using DOL.GameEvents;
using DOL.GS.Spells;
using System.Collections.Specialized;

namespace DOL.GS
{
    /// <summary>
    /// This class holds all information that
    /// EVERY object in the game world needs!
    /// </summary>
    public abstract class GameObject
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #region State/Random/Type

        /// <summary>
        /// Holds the current state of the object
        /// </summary>
        public enum eObjectState : byte
        {
            /// <summary>
            /// Active, visibly in world
            /// </summary>
            Active,
            /// <summary>
            /// Inactive, currently being moved or stuff
            /// </summary>
            Inactive,
            /// <summary>
            /// Deleted, waiting to be cleaned up
            /// </summary>
            Deleted
        }

        /// <summary>
        /// The Object's state! This is needed because
        /// when we remove an object it isn't instantly
        /// deleted but the state is merely set to "Deleted"
        /// This prevents the object from vanishing when
        /// there still might be enumerations running over it.
        /// A timer will collect the deleted objects and free
        /// them at certain intervals.
        /// </summary>
        protected volatile eObjectState m_ObjectState;

        /// <summary>
        /// Returns the current state of the object.
        /// Object's with state "Deleted" should not be used!
        /// </summary>
        public virtual eObjectState ObjectState
        {
            get { return m_ObjectState; }
            set
            {
                if (log.IsDebugEnabled)
                    log.Debug("ObjectState: OID" + ObjectID + " " + Name + " " + m_ObjectState + " => " + value);
                m_ObjectState = value;
            }
        }

        #endregion

        #region Position

        public virtual Position Position
        {
            get;
            set;
        } = Position.Nowhere;

        public Coordinate Coordinate
        {
            get => Position.Coordinate;
        }

        public virtual Angle Orientation
        {
            get => Position.Orientation;
            set => Position = Position.With(value);
        }

        /// <summary>
        /// Current direction the Object is facing
        /// </summary>
        public virtual ushort Heading
        {
            get => Position.Orientation.InHeading;
            set => Orientation = Angle.Heading(value);
        }

        /// <summary>
        /// Gets or Sets the current Region of the Object
        /// </summary>
        public virtual Region CurrentRegion
        {
            get => Position.Region;
            set => Position = Position.With(regionID: value?.ID ?? 0);
        }

        /// <summary>
        /// Get's or sets the current Region by the ID
        /// </summary>
        public virtual ushort CurrentRegionID
        {
            get => Position.RegionID;
            set
            {
                Position = Position.With(regionID: value);
            }
        }

        /// <summary>
        /// Gets the current Zone of the Object
        /// </summary>
        public Zone CurrentZone
        {
            get => CurrentRegion?.GetZone(Coordinate);
        }

        /// <summary>
        /// Get distance to a point (with z-axis adjustment)
        /// </summary>
        /// <remarks>
        /// If either Z-value is zero, the z-axis is ignored
        /// </remarks>
        /// <param name="obj">Target</param>
        /// <param name="Zfactor"></param>
        /// <returns>Adjusted distance or int.MaxValue if distance cannot be calculated</returns>
        public float GetDistanceTo(GameObject obj, float zFactor = 1.0f) => GetDistanceTo(obj.Position, zFactor);

        public float GetDistanceTo(Position position, float zFactor)
        {
            if (Position.RegionID != position.RegionID)
            {
                return float.MaxValue;
            }
            var offset = position.Coordinate - Coordinate;
            var dz = offset.Z * zFactor;
            return (float)(offset.Length2D + Math.Sqrt(dz*dz));
        }

        public float GetDistance2DTo(Position position)
        {
            if (Position.RegionID != position.RegionID)
            {
                return float.MaxValue;
            }
            var offset = position.Coordinate - Coordinate;
            return (float)(offset.Length2D);
        }

        public float GetDistance2DTo(GameObject obj) => GetDistance2DTo(obj.Position);

        public float GetDistanceSquaredTo(Position position, float zFactor)
        {
            if (Position.RegionID != position.RegionID)
            {
                return float.MaxValue;
            }
            var offset = position.Coordinate - Coordinate;
            var dz = offset.Z * zFactor;
            return (float)(offset.X * offset.X + offset.Y * offset.Y + dz * dz);
        }

        public float GetDistanceSquaredTo(GameObject obj)
        {
            if (Position.RegionID != obj.CurrentRegionID)
            {
                return float.MaxValue;
            }
            var offset = obj.Coordinate - Coordinate;
            return (float)(offset.X * offset.X + offset.Y * offset.Y);
        }
        
        public bool IsWithinRadius(Position target, float distance) => Position.Region == this.CurrentRegion && GameMath.IsWithinRadius(Coordinate, target.Coordinate, distance);
        public bool IsWithinRadius(GameObject target, float distance) => GameMath.IsWithinRadius(this, target, distance);
        public bool IsWithinRadius(Coordinate target, float distance) => GameMath.IsWithinRadius(Position.Coordinate, target, distance);
        public bool IsWithinRadius2D(GameObject target, float distance) => GameMath.IsWithinRadius2D(this, target, distance);
        public ushort GetHeading(GameObject target) => GetHeading(target.Coordinate);
        public ushort GetHeading(Coordinate target) => Position.Coordinate.GetOrientationTo(target).InHeading;
        public Angle GetAngleTo(Coordinate coordinate)
            => Coordinate.GetOrientationTo(coordinate) - Orientation;
        
        /// <summary>
        /// determines wether a target object is front
        /// in front is defined as north +- viewangle/2
        /// </summary>
        /// <param name="target"></param>
        /// <param name="viewangle"></param>
        /// <param name="rangeCheck"></param>
        /// <returns></returns>
        public virtual bool IsObjectInFront(GameObject target, double viewangle, bool rangeCheck = true)
        {
            if (target == null)
                return false;
            var angle = GetAngleTo(target.Coordinate);
            var isInFront = angle.InDegrees >= 360 - viewangle / 2 || angle.InDegrees < viewangle / 2;
            if (isInFront) return true;

            // Dre: WTF?! This check has nothing to do here!
            // if target is closer than 32 units it is considered always in view
            // tested and works this way for normal evade, parry, block (in 1.69)
            if (rangeCheck)
                return GetDistanceSquaredTo(target) <= 32 * 32;

            return false;
        }
        
        private static readonly ((int X, int Y) BottomLeft, (int X, int Y) TopRight)[] hardcodedUnderWaterAreas = new[]
        {
            // Mount Collory
            ((479000,664000),(488000,670000)),
            ((472000,656000),(488000,664000)),
            ((468500,624000),(488000,654000)),
            ((431000,659000),(466000,683000)),
            ((431000,646000),(460000,659001)),
            ((431000,624000),(455000,646001)),
            ((431000,671000),(471000,683000)),
            // Breifine
            ((456000,558000),(479000,618000)),
            // Cruachan Gorge
            ((360000,586000),(424000,618000)),
            ((360000,563000),(424000,578000)),
            // Emain Macha
            ((428000,505000),(444000,555000)),
            // Hadrian's Wall
            ((603000,500000),(620000,553000)),
            // Snowdonia
            ((592000,633000),(617000,678000)),
            ((581000,662000),(617000,678000)),
            // Sauvage Forrest
            ((626000,584000),(681000,615000)),
            // Uppland
            ((610000,297000),(652000,353000)),
            // Yggdra
            ((671000,408000),(693000,421000)),
            ((674000,364000),(716000,394000))
        };

        /// <summary>
        /// Checks if object is underwater
        /// </summary>
        public virtual bool IsUnderwater
        {
            get
            {
                if (CurrentRegion == null || CurrentZone == null)
                    return false;
                // Special land areas below the waterlevel in NF
                if (CurrentRegion.ID == 163)
                {
                    foreach(var area in hardcodedUnderWaterAreas)
                    {
                        if(Coordinate.X > area.BottomLeft.X 
                           && Coordinate.Y > area.BottomLeft.Y
                           && Coordinate.X < area.TopRight.X
                           && Coordinate.Y > area.TopRight.Y)
                        {
                            return false;
                        }
                    }
                }

                return Position.Z < CurrentZone.Waterlevel;
            }
        }

        /// <summary>
        /// Holds all areas this object is currently within
        /// </summary>
        public virtual IList<IArea> CurrentAreas
        {
            get
            {
                return CurrentZone?.GetAreasOfSpot(Coordinate) ?? new List<IArea>();
            }
            set { }
        }


        protected House m_currentHouse;
        /// <summary>
        /// Either the house an object is in or working on (player editing a house)
        /// </summary>
        public virtual House CurrentHouse
        {
            get { return m_currentHouse; }
            set { m_currentHouse = value; }
        }

        public virtual bool InHouse { get; set; }

        /// <summary>
        /// Is this object visible to another?
        /// This does not check for stealth.
        /// </summary>
        /// <param name="checkObject"></param>
        /// <returns></returns>
        public virtual bool IsVisibleTo(GameObject checkObject)
        {
            if (checkObject == null ||
                CurrentRegion != checkObject.CurrentRegion ||
                (!InHouse && checkObject.InHouse) ||
                (InHouse && (!checkObject.InHouse || CurrentHouse != checkObject.CurrentHouse)))
            {
                return false;
            }

            if (checkObject is GameNPC { Event: { InstancedConditionType: not InstancedConditionTypes.All } ev })
            {
                if (!ev.IsVisibleTo(this))
                    return false;
            }

            return true;
        }

        #endregion
        
        /// <summary>
        /// Holds the realm of this object
        /// </summary>
        protected eRealm m_Realm;

        /// <summary>
        /// Gets or Sets the current Realm of the Object
        /// </summary>
        public virtual eRealm Realm
        {
            get { return m_Realm; }
            set { m_Realm = value; }
        }

        protected string m_ownerID;

        /// <summary>
        /// Gets or sets the owner ID for this object
        /// </summary>
        public virtual string OwnerID
        {
            get { return m_ownerID; }
            set
            {
                m_ownerID = value;
            }
        }

        protected GameLiving m_owner;

        /// <summary>
        /// Gets or sets the owner for this object
        /// This will be used to determine the framework of reference for example for ServerRules.IsAllowedToAttack
        /// </summary>
        public virtual GameLiving Owner
        {
            get => m_owner;
            set
            {
                m_owner = value;
                OwnerID = value?.InternalID;
            }
        }

        /// <summary>
        /// Find the player owner of the pets at the top of the tree
        /// </summary>
        /// <returns>Player owner at the top of the tree.  If there was no player, then return null.</returns>
        public GamePlayer GetPlayerOwner()
        {
            return GetLivingOwner() as GamePlayer;
        }

        public GameNPC GetNPCOwner()
        {
            return GetLivingOwner() as GameNPC;
        }

        public virtual GameLiving GetLivingOwner()
        {
            GameLiving owner = Owner;
            GameLiving leaf = null;
            while (owner is not null)
            {
                leaf = owner;
                owner = owner.Owner;
                if (owner == this)
                    throw new Exception("GetLivingOwner() from " + this + " caused a cyclical loop.");
            }
            return leaf;
        }

        public virtual bool IsOwner(GameLiving other)
        {
            if (Owner == null)
            {
                return string.IsNullOrEmpty(OwnerID) ? false : string.Equals(OwnerID, other.InternalID);
            }
            var owner = Owner;
            while (owner != null)
            {
                if (owner == other)
                    return true;
                if (owner == this)
                    throw new Exception($"IsOwner for {this} caused a cyclic loop");
                owner = owner.Owner;
            }
            return false;
        }

        public bool CanRespawnWithinEvent
        {
            get;
            set;
        }

        #region Level/Name/Model/GetName/GetPronoun/GetExamineMessage

        /// <summary>
        /// The level of the Object
        /// </summary>
        protected byte m_level = 0; // Default to 0 to force AutoSetStats() to be called when level set

        /// <summary>
        /// The name of the Object
        /// </summary>
        protected string m_name;

        /// <summary>
        /// The guild name of the Object
        /// </summary>
        protected string m_guildName;

        /// <summary>
        /// The model of the Object
        /// </summary>
        protected ushort m_model;


        /// <summary>
        /// Gets or Sets the current level of the Object
        /// </summary>
        public virtual byte Level
        {
            get { return m_level; }
            set { m_level = value; }
        }

        /// <summary>
        /// Gets or Sets the effective level of the Object
        /// </summary>
        public virtual int EffectiveLevel
        {
            get { return Level; }
            set { }
        }

        /// <summary>
        /// What level is displayed to another player
        /// </summary>
        public virtual byte GetDisplayLevel(GamePlayer player)
        {
            return Level;
        }

        /// <summary>
        /// Gets or Sets the current Name of the Object
        /// </summary>
        public virtual string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        public virtual string GuildName
        {
            get { return m_guildName; }
            set { m_guildName = value; }
        }

        /// <summary>
        /// Gets or Sets the current Model of the Object
        /// </summary>
        public virtual ushort Model
        {
            get { return m_model; }
            set { m_model = value; }
        }

        /// <summary>
        /// Whether or not the object can be attacked.
        /// </summary>
        public virtual bool IsAttackable
        {
            get
            {
                return false;
            }
        }

        public virtual bool IsAttackableDoor
        {
            get
            {
                if (this.Realm == eRealm.None)
                    return true;

                return false;
            }
        }

        protected int m_health;
        public virtual int Health
        {
            get { return m_health; }
            set
            {

                int maxhealth = MaxHealth;
                if (value >= maxhealth)
                {
                    m_health = maxhealth;
                }
                else if (value > 0)
                {
                    m_health = value;
                }
                else
                {
                    m_health = 0;
                }

                /*    if (IsAlive && m_health < maxhealth)
                    {
                        StartHealthRegeneration();
                    }*/
            }
        }

        /// <summary>
        /// Gets/sets the maximum amount of health
        /// </summary>
        protected int m_maxHealth;
        public virtual int MaxHealth
        {
            get { return m_maxHealth; }
            set
            {
                m_maxHealth = value;
                //Health = Health; //cut extra hit points if there are any or start regeneration
            }
        }

        /// <summary>
        /// Gets the Health in percent 0..100
        /// </summary>
        public virtual byte HealthPercent
        {
            get
            {
                return (byte)(MaxHealth <= 0 ? 0 : Health * 100 / MaxHealth);
            }
        }

        /// <summary>
        /// Health as it should display in the group window.
        /// </summary>
        public virtual byte HealthPercentGroupWindow
        {
            get { return HealthPercent; }
        }

        public virtual string GetName(int article, bool firstLetterUppercase, string lang, ITranslatableObject obj)
        {
            switch (lang)
            {
                case "EN":
                    {
                        return GetName(article, firstLetterUppercase);
                    }
                default:
                    {
                        if (obj is GameNPC)
                        {
                            var translation = (DBLanguageNPC)LanguageMgr.GetTranslation(lang, obj);
                            if (translation != null) return translation.Name;
                        }

                        return GetName(article, firstLetterUppercase); ;
                    }
            }
        }

        private const string m_vowels = "aeuio";

        /// <summary>
        /// Returns name with article for nouns
        /// </summary>
        /// <param name="article">0=definite, 1=indefinite</param>
        /// <param name="firstLetterUppercase">Forces the first letter of the returned string to be upper case</param>
        /// <returns>name of this object (includes article if needed)</returns>
        public virtual string GetName(int article, bool firstLetterUppercase)
        {
            /*
             * http://www.camelotherald.com/more/888.shtml
             * - All monsters names whose names begin with a vowel should now use the article 'an' instead of 'a'.
             * 
             * http://www.camelotherald.com/more/865.shtml
             * - Instances where objects that began with a vowel but were prefixed by the article "a" (a orb of animation) have been corrected.
             */

            if (Name.Length < 1)
                return "";

            // actually this should be only for Named mobs (like dragon, legion) but there is no way to find that out
            if (char.IsUpper(Name[0]) && this is GameLiving) // proper noun
            {
                return Name;
            }

            if (article == 0)
            {
                if (firstLetterUppercase)
                    return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetName.Article1", Name);
                else
                    return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetName.Article2", Name);
            }
            else
            {
                // if first letter is a vowel
                if (m_vowels.IndexOf(Name[0]) != -1)
                {
                    if (firstLetterUppercase)
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetName.Article3", Name);
                    else
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetName.Article4", Name);
                }
                else
                {
                    if (firstLetterUppercase)
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetName.Article5", Name);
                    else
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetName.Article6", Name);
                }
            }
        }

        public String Capitalize(bool capitalize, String text)
        {
            if (!capitalize) return text;

            string result = "";
            if (text == null || text.Length <= 0) return result;
            result = text[0].ToString().ToUpper();
            if (text.Length > 1) result += text.Substring(1, text.Length - 1);
            return result;
        }

        /// <summary>
        /// Pronoun of this object in case you need to refer it in 3rd person
        /// http://webster.commnet.edu/grammar/cases.htm
        /// </summary>
        /// <param name="firstLetterUppercase"></param>
        /// <param name="form">0=Subjective, 1=Possessive, 2=Objective</param>
        /// <returns>pronoun of this object</returns>
        public virtual string GetPronoun(int form, bool firstLetterUppercase)
        {
            switch (form)
            {
                default: // Subjective
                    if (firstLetterUppercase)
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetPronoun.Pronoun1");
                    else
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetPronoun.Pronoun2");
                case 1: // Possessive
                    if (firstLetterUppercase)
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetPronoun.Pronoun3");
                    else
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetPronoun.Pronoun4");
                case 2: // Objective
                    if (firstLetterUppercase)
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetPronoun.Pronoun5");
                    else
                        return LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "GameObject.GetPronoun.Pronoun6");
            }
        }

        /// <summary>
        /// Adds messages to ArrayList which are sent when object is targeted
        /// </summary>
        /// <param name="player">GamePlayer that is examining this object</param>
        /// <returns>list with string messages</returns>
        public virtual IList GetExamineMessages(GamePlayer player)
        {
            IList list = new ArrayList(4);
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObject.GetExamineMessages.YouTarget", player.GetPersonalizedName(this)));
            return list;
        }

        #endregion

        #region IDs/Database

        /// <summary>
        /// True if this object is saved in the DB
        /// </summary>
        protected bool m_saveInDB;

        /// <summary>
        /// The objectID. This is -1 as long as the object is not added to a region!
        /// </summary>
        protected int m_ObjectID = -1;

        /// <summary>
        /// The internalID. This is the unique ID of the object in the DB!
        /// </summary>
        protected string m_InternalID;

        /// <summary>
        /// Gets or Sets the current ObjectID of the Object
        /// This is done automatically by the Region and should
        /// not be done manually!!!
        /// </summary>
        public int ObjectID
        {
            get { return m_ObjectID; }
            set
            {
                if (log.IsDebugEnabled)
                    log.Debug("ObjectID: " + Name + " " + m_ObjectID + " => " + value);
                m_ObjectID = value;
            }
        }

        /// <summary>
        /// Gets or Sets the internal ID (DB ID) of the Object
        /// </summary>
        public virtual string InternalID
        {
            get { return m_InternalID; }
            set { m_InternalID = value; }
        }

        /// <summary>
        /// Sets the state for this object on whether or not it is saved in the database
        /// </summary>
        public bool SaveInDB
        {
            get { return m_saveInDB; }
            set { m_saveInDB = value; }
        }

        /// <summary>
        /// Saves an object into the database
        /// </summary>
        public virtual void SaveIntoDatabase()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        public virtual void LoadFromDatabase(DataObject obj)
        {
            InternalID = obj.ObjectId;
        }

        /// <summary>
        /// Deletes a character from the DB
        /// </summary>
        public virtual void DeleteFromDatabase()
        {
            GameBoat boat = BoatMgr.GetBoatByOwner(InternalID);
            if (boat != null)
                boat.DeleteFromDatabase();
        }

        #endregion

        #region Add-/Remove-/Create-/Move-

        /// <summary>
        /// Creates this object in the gameworld
        /// </summary>
        /// <param name="regionID">region target</param>
        /// <param name="x">x target</param>
        /// <param name="y">y target</param>
        /// <param name="z">z target</param>
        /// <param name="heading">heading</param>
        /// <returns>true if created successfully</returns>
        public virtual bool Create(ushort regionID, int x, int y, int z, ushort heading)
        {
            if (m_ObjectState == eObjectState.Active)
                return false;
            Position = Position.Create(regionID, x, y, z, heading);
            return AddToWorld();
        }

        /// <summary>
        /// Creates the item in the world
        /// </summary>
        /// <returns>true if object was created</returns>
        public virtual bool AddToWorld()
        {
            if (!float.IsNormal(Position.X) || !float.IsNormal(Position.Y) || !float.IsNormal(Position.Z))
            {
                log.Error($"AddToWorld: Invalid position ({this.GetType()} ID: {InternalID} Coordinate: {Coordinate} Region: {CurrentRegionID})");
                return false;
            }

            /****** MODIFIED BY KONIK & WITCHKING *******/
            // CurrentZone checks for null Region.
            // Should it be the case, currentZone will be null as well.
            if (CurrentZone == null)
            {
                log.Error($"AddToWorld: Zone does not exist ({this.GetType()} ID: {InternalID} Coordinate: {Coordinate} Region: {CurrentRegionID})");
                return false;
            }

            if (m_ObjectState == eObjectState.Active)
            {
                log.Warn($"AddToWorld: Object already in world ({this.GetType()} ID: {InternalID}))");
                return false;
            }

            if (!CurrentRegion.AddObject(this))
            {
                return false;
            }

            Notify(GameObjectEvent.AddToWorld, this);
            ObjectState = eObjectState.Active;

            CurrentZone.ObjectEnterZone(this);
            /*********** END OF MODIFICATION ***********/

            m_spawnTick = CurrentRegion.Time;

            return true;
        }

        /// <summary>
        /// Removes the item from the world
        /// </summary>
        public virtual bool RemoveFromWorld()
        {
            if (CurrentRegion == null || ObjectState != eObjectState.Active)
                return false;
            Notify(GameObjectEvent.RemoveFromWorld, this);
            ObjectState = eObjectState.Inactive;
            CurrentRegion.RemoveObject(this);
            return true;
        }

        [Obsolete("Use MoveTo(Position) instead!")]
        public bool MoveTo(GameLocation loc)
        {
            return MoveTo(loc.RegionID, loc.Position.X, loc.Position.Y, loc.Position.Z, loc.Heading);
        }

        /// <summary>
        /// Moves the item from one spot to another spot, possible even
        /// over region boundaries
        /// </summary>
        /// <param name="regionID">new regionid</param>
        /// <param name="x">new x</param>
        /// <param name="y">new y</param>
        /// <param name="z">new z</param>
        /// <param name="heading">new heading</param>
        /// <returns>true if moved</returns>
        [Obsolete("Use MoveTo(Position) instead!")]
        public bool MoveTo(ushort regionID, float x, float y, float z, ushort heading) => MoveTo(Position.Create(regionID, (int)x, (int)y, (int)z, heading));
        

        /// <summary>
        /// Moves the item from one spot to another spot, possible even
        /// over region boundaries
        /// </summary>
        /// <param name="position">New position</param>
        /// <returns>true if moved</returns>
        public virtual bool MoveTo(Position position)
        {
            Region? rgn = WorldMgr.GetRegion(position.RegionID);
            if (rgn == null)
                return false;
            if (rgn.GetZone(position.Coordinate) == null)
                return false;

            if (m_ObjectState == eObjectState.Active)
            {
                Notify(GameObjectEvent.MoveTo, this, new MoveToEventArgs(position.RegionID, position.X, position.Y, position.Z, position.Orientation.InHeading));
                if (!RemoveFromWorld())
                    return false;
            }
            
            Position = position;
            return AddToWorld();
        }

        /// <summary>
        /// Marks this object as deleted!
        /// </summary>
        public virtual void Delete()
        {
            Notify(GameObjectEvent.Delete, this);
            RemoveFromWorld();
            ObjectState = eObjectState.Deleted;
            GameEventMgr.RemoveAllHandlersForObject(this);
        }

        /// <summary>
        /// Holds the GameTick of when this object was added to the world
        /// </summary>
        protected long m_spawnTick = 0;

        /// <summary>
        /// Gets the GameTick of when this object was added to the world
        /// </summary>
        public long SpawnTick
        {
            get { return m_spawnTick; }
        }

        #endregion

        #region Interact

        /// <summary>
        /// The distance this object can be interacted with
        /// </summary>
        public virtual int InteractDistance
        {
            get { return WorldMgr.INTERACT_DISTANCE; }
        }

        /// <summary>
        /// This function is called from the ObjectInteractRequestHandler
        /// </summary>
        /// <param name="player">GamePlayer that interacts with this object</param>
        /// <returns>false if interaction is prevented</returns>
        public virtual bool Interact(GamePlayer player)
        {
            if (player.Client.Account.PrivLevel == 1 && !this.IsWithinRadius(player, InteractDistance))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObject.Interact.TooFarAway", GetName(0, true)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            return true;
        }

        #endregion

        #region Combat

        /// <summary>
        /// This living takes damage
        /// </summary>
        /// <param name="ad">AttackData containing damage details</param>
        public virtual void TakeDamage(AttackData ad)
        {
            TakeDamage(ad.Attacker, ad.DamageType, ad.Damage, ad.CriticalDamage);
        }

        /// <summary>
        /// This method is called whenever this living 
        /// should take damage from some source
        /// </summary>
        /// <param name="source">the damage source</param>
        /// <param name="damageType">the damage type</param>
        /// <param name="damageAmount">the amount of damage</param>
        /// <param name="criticalAmount">the amount of critical damage</param>
        public virtual void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            Notify(GameObjectEvent.TakeDamage, this, new TakeDamageEventArgs(source, damageType, damageAmount, criticalAmount));
        }

        #endregion

        #region ConLevel/DurLevel

        /// <summary>
        /// Calculate con-level against other object
        /// &lt;=-3 = grey
        /// -2 = green
        /// -1 = blue
        /// 0 = yellow (same level)
        /// 1 = orange
        /// 2 = red
        /// &gt;=3 = violet
        /// </summary>
        /// <returns>conlevel</returns>
        public double GetConLevel(GameObject compare)
        {
            return GetConLevel(EffectiveLevel, compare.EffectiveLevel);
            //			return (compare.Level - Level) / ((double)(Level / 10 + 1));
        }

        /// <summary>
        /// Calculate con-level against other compareLevel
        /// -3- = grey
        /// -2 = green
        /// -1 = blue  (compareLevel is 1 con lower)
        /// 0 = yellow (same level)
        /// 1 = orange (compareLevel is 1 con higher)
        /// 2 = red
        /// 3+ = violet
        /// </summary>
        /// <returns>conlevel</returns>
        public static double GetConLevel(int level, int compareLevel)
        {
            int constep = Math.Max(1, (level + 9) / 10);
            double stepping = 1.0 / constep;
            int leveldiff = level - compareLevel;
            return 0 - leveldiff * stepping;
        }

        /// <summary>
        /// Calculate a level based on source level and a con level
        /// </summary>
        /// <param name="level"></param>
        /// <param name="con"></param>
        /// <returns></returns>
        public static int GetLevelFromCon(int level, double con)
        {
            int constep = Math.Max(1, (level + 10) / 10);
            return Math.Max((int)0, (int)(level + constep * con));
        }

        #endregion

        #region Notify

        public virtual void Notify(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.Notify(e, sender, args);
        }

        public virtual void Notify(DOLEvent e, object sender)
        {
            Notify(e, sender, null);
        }

        public virtual void Notify(DOLEvent e)
        {
            Notify(e, null, null);
        }

        public virtual void Notify(DOLEvent e, EventArgs args)
        {
            Notify(e, null, args);
        }

        #endregion

        #region ObjectsInRadius

        /// <summary>
        /// Gets all players close to this object inside a certain radius
        /// </summary>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <returns>An enumerator</returns>
        public virtual IEnumerable GetPlayersInRadius(ushort radiusToCheck)
        {
            return GetPlayersInRadius(false, radiusToCheck, false, false);
        }


        public IEnumerable GetPlayersInRadius(ushort radiusToCheck, bool ignoreZ)
        {
            return GetPlayersInRadius(false, radiusToCheck, false, ignoreZ);
        }

        public IEnumerable GetPlayersInRadius(bool useCache, ushort radiusToCheck)
        {
            return GetPlayersInRadius(useCache, radiusToCheck, false, false);
        }

        /// <summary>
        /// Gets all players close to this object inside a certain radius
        /// </summary>
        /// <param name="useCache">true may return a cached result, false not.</param>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetPlayersInRadius(bool useCache, ushort radiusToCheck, bool ignoreZ)
        {
            return GetPlayersInRadius(useCache, radiusToCheck, false, ignoreZ);
        }



        /// <summary>
        /// Gets all players close to this object inside a certain radius
        /// </summary>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <param name="withDistance">if the objects are to be returned with distance</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetPlayersInRadius(ushort radiusToCheck, bool withDistance, bool ignoreZ)
        {
            return GetPlayersInRadius(true, radiusToCheck, withDistance, ignoreZ);
        }

        /// <summary>
        /// Gets all players close to this object inside a certain radius
        /// </summary>
        /// <param name="useCache">true may return a cached result, false not.</param>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <param name="withDistance">if the objects are to be returned with distance</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetPlayersInRadius(bool useCache, ushort radiusToCheck, bool withDistance, bool ignoreZ)
        {
            if (CurrentRegion != null)
            {
                //Eden - avoid server freeze
                if (CurrentZone == null)
                {
                    if (this is GamePlayer player && player.TempProperties.getProperty("isbeingbanned", false))
                    {
                        player.TempProperties.setProperty("isbeingbanned", true);
                        player.MoveToBind();
                    }
                }
                else
                {
                    return CurrentRegion.GetPlayersInRadius(Coordinate, radiusToCheck, withDistance, ignoreZ);
                }
            }
            return new Region.EmptyEnumerator();
        }

        /// <summary>
        /// Gets all npcs close to this object inside a certain radius
        /// </summary>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetNPCsInRadius(ushort radiusToCheck)
        {
            return GetNPCsInRadius(true, radiusToCheck, false, false);
        }

        public IEnumerable GetNPCsInRadius(ushort radiusToCheck, bool ignoreZ)
        {
            return GetNPCsInRadius(true, radiusToCheck, false, ignoreZ);
        }

        /// <summary>
        /// Gets all npcs close to this object inside a certain radius
        /// </summary>
        /// <param name="useCache">use the cache</param>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetNPCsInRadius(bool useCache, ushort radiusToCheck)
        {
            return GetNPCsInRadius(useCache, radiusToCheck, false, false);
        }

        public IEnumerable GetNPCsInRadius(bool useCache, ushort radiusToCheck, bool ignoreZ)
        {
            return GetNPCsInRadius(useCache, radiusToCheck, false, ignoreZ);
        }

        /// <summary>
        /// Gets all npcs close to this object inside a certain radius
        /// </summary>
        /// <param name="useCache">use the cache</param>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <param name="withDistance">if the objects are to be returned with distance</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetNPCsInRadius(bool useCache, ushort radiusToCheck, bool withDistance, bool ignoreZ)
        {
            if (CurrentRegion != null)
            {
                //Eden - avoid server freeze
                if (CurrentZone == null)
                {
                    if (this is GamePlayer player && player.TempProperties.getProperty("isbeingbanned", false))
                    {
                        player.TempProperties.setProperty("isbeingbanned", true);
                        player.MoveToBind();
                    }
                }
                else
                {
                    IEnumerable result = CurrentRegion.GetNPCsInRadius(Coordinate, radiusToCheck, withDistance, ignoreZ);
                    return result;
                }
            }

            return new Region.EmptyEnumerator();
        }

        /// <summary>
        /// Gets all items close to this object inside a certain radius
        /// </summary>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetItemsInRadius(ushort radiusToCheck)
        {
            /******* MODIFIED BY KONIK & WITCHKING FOR NEW ZONE SYSTEM *********/
            return GetItemsInRadius(radiusToCheck, false);
            /***************************************************************/
        }

        /// <summary>
        /// Gets all items close to this object inside a certain radius
        /// </summary>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <param name="withDistance">if the objects are to be returned with distance</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetItemsInRadius(ushort radiusToCheck, bool withDistance)
        {
            /******* MODIFIED BY KONIK & WITCHKING FOR NEW ZONE SYSTEM *********/
            if (CurrentRegion != null)
            {
                //Eden - avoid server freeze
                if (CurrentZone == null)
                {
                    if (this is GamePlayer player && player.TempProperties.getProperty("isbeingbanned", false))
                    {
                        player.TempProperties.setProperty("isbeingbanned", true);
                        player.MoveToBind();
                    }
                }
                else
                {
                    return CurrentRegion.GetItemsInRadius(Coordinate, radiusToCheck, withDistance);
                }
            }
            return new Region.EmptyEnumerator();
            /***************************************************************/
        }

        /// <summary>
        /// Gets all doors close to this object inside a certain radius
        /// </summary>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetDoorsInRadius(ushort radiusToCheck)
        {
            return GetDoorsInRadius(radiusToCheck, false);
        }

        /// <summary>
        /// Gets all doors close to this object inside a certain radius
        /// </summary>
        /// <param name="radiusToCheck">the radius to check</param>
        /// <param name="withDistance">if the objects are to be returned with distance</param>
        /// <returns>An enumerator</returns>
        public IEnumerable GetDoorsInRadius(ushort radiusToCheck, bool withDistance)
        {
            if (CurrentRegion != null)
            {
                //Eden : avoid server freeze
                if (CurrentZone == null)
                {
                    if (this is GamePlayer player && player.TempProperties.getProperty("isbeingbanned", false))
                    {
                        player.TempProperties.setProperty("isbeingbanned", true);
                        player.MoveToBind();
                    }
                }
                else
                {
                    return CurrentRegion.GetDoorsInRadius(Coordinate, radiusToCheck, withDistance);
                }
            }
            return new Region.EmptyEnumerator();
        }
        #endregion

        #region Item/Money

        /// <summary>
        /// Called when the object is about to get an item from someone
        /// </summary>
        /// <param name="source">Source from where to get the item</param>
        /// <param name="item">Item to get</param>
        /// <returns>true if the item was successfully received</returns>
        public virtual bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (item == null || item.OwnerID == null)
            {
                // item was taken
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when the object is about to get an item from someone
        /// </summary>
        /// <param name="source">Source from where to get the item</param>
        /// <param name="templateID">templateID for item to add</param>
        /// <returns>true if the item was successfully received</returns>
        public virtual bool ReceiveItem(GameLiving source, string templateID, eInventoryActionType type)
        {
            ItemTemplate template = GameServer.Database.FindObjectByKey<ItemTemplate>(templateID);
            if (template == null)
            {
                if (log.IsErrorEnabled)
                    log.Error("Item Creation: ItemTemplate not found ID=" + templateID);
                return false;
            }

            return ReceiveItem(source, GameInventoryItem.Create(template));
        }

        /// <summary>
        /// Receive an item from a living
        /// </summary>
        /// <param name="source"></param>
        /// <param name="item"></param>
        /// <returns>true if player took the item</returns>
        public virtual bool ReceiveItem(GameLiving source, WorldInventoryItem item)
        {
            return ReceiveItem(source, item.Item);
        }

        /// <summary>
        /// Called when the object is about to get money from someone
        /// </summary>
        /// <param name="source">Source from where to get the money</param>
        /// <param name="money">array of money to get</param>
        /// <returns>true if the money was successfully received</returns>
        public virtual bool ReceiveMoney(GameLiving source, long money)
        {
            return false;
        }

        #endregion

        #region Spell Cast

        /// <summary>
        /// Returns true if the object has the spell effect,
        /// else false.
        /// </summary>
        /// <param name="spell"></param>
        /// <returns></returns>
        public virtual bool HasEffect(Spell spell)
        {
            return false;
        }

        /// <summary>
        /// Returns true if the object has a spell effect of a
        /// given type, else false.
        /// </summary>
        /// <param name="spell"></param>
        /// <returns></returns>
        public virtual bool HasEffect(Type effectType)
        {
            return false;
        }

        #endregion

        /// <summary>
        /// Returns the string representation of the GameObject
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            Region reg = CurrentRegion;
            return new StringBuilder(128)
                .Append(GetType().FullName)
                .Append(" name=").Append(Name)
                .Append(" DB_ID=").Append(InternalID)
                .Append(" oid=").Append(ObjectID.ToString())
                .Append(" state=").Append(ObjectState.ToString())
                .Append(" reg=").Append(reg == null ? "null" : reg.ID.ToString())
                .Append(" loc=").Append(Coordinate)
                .ToString();
        }

        /// <summary>
        /// All objects are neutral.
        /// </summary>
        public virtual eGender Gender
        {
            get { return eGender.Neutral; }
            set { }
        }

        /// <summary>
        /// Objects which can receive a reward from this object, for example loot
        /// </summary>
        /// <returns></returns>
        public virtual ICollection<GameObject> GetRewardCandidates()
        {
            return Array.Empty<GameObject>();
        }

        #region Broadcast Utils

        /// <summary>
        /// Broadcasts the Object Update to all players around
        /// </summary>
        public virtual void BroadcastUpdate()
        {
            if (ObjectState != eObjectState.Active)
                return;

            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null)
                    continue;

                player.Out.SendObjectUpdate(this);
            }
        }

        #endregion


        /// <summary>
        /// Constructs a new empty GameObject
        /// </summary>
        public GameObject()
        {
            //Objects should NOT be saved back to the DB
            //as standard! We want our mobs/items etc. at
            //the same startingspots when we restart!
            m_saveInDB = false;
            m_name = "";
            m_ObjectState = eObjectState.Inactive;
            m_boat_ownerid = "";
        }
        public static bool PlayerHasItem(GamePlayer player, string str)
        {
            InventoryItem item = player.Inventory.GetFirstItemByID(str, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv);
            if (item != null)
                return true;
            return false;
        }
        private static string m_boat_ownerid;
        public static string ObjectHasOwner()
        {
            if (m_boat_ownerid == "")
                return "";
            else
                return m_boat_ownerid;
        }

        public virtual void CustomCopy(GameObject source)
        {

        }
    }
}