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

using DOL.Database;
using DOL.Events;
using DOL.Language;
using DOL.GS.PacketHandler;
using DOL.GameEvents;
using DOL.GS.Geometry;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DOL.GS.Spells;

namespace DOL.GS
{
    /// <summary>
    /// AbstractArea extend this if you wish to implement e new custom area.
    /// For examples see Area.Cricle, Area.Square
    /// </summary>
    public abstract class AbstractArea : IArea, ITranslatableObject
    {
        protected DBArea dbArea = null;
        protected readonly List<GameStaticItem> m_boundaryObjects = new List<GameStaticItem>();
        protected RegionTimer m_effectLoopTimer;
        protected bool m_lastEventState = false;
        protected bool m_canBroadcast = false;
        /// <summary>
        /// Variable holding whether or not players can broadcast in this area
        /// </summary>
        public bool CanBroadcast
        {
            get { return m_canBroadcast; }
            set { m_canBroadcast = value; }
        }

        protected bool m_checkLOS = false;
        /// <summary>
        /// Variable holding whether or not to check for LOS for spells in this area
        /// </summary>
        public bool CheckLOS
        {
            get { return m_checkLOS; }
            set { m_checkLOS = value; }
        }

        protected bool m_displayMessage = true;
        /// <summary>
        /// Display entered message
        /// </summary>
        public virtual bool DisplayMessage
        {
            get { return m_displayMessage; }
            set { m_displayMessage = value; }
        }

        protected bool m_safeArea = false;
        /// <summary>
        /// Can players be attacked by other players in this area
        /// </summary>
        public virtual bool IsSafeArea
        {
            get { return m_safeArea; }
            set { m_safeArea = value; }
        }

        public bool IsPvP { get; set; } = false;

        /// <summary>
        /// Constant holding max number of areas per zone, increase if more ares are needed,
        /// this will slightly increase memory usage on server
        /// </summary>
        public const ushort MAX_AREAS_PER_ZONE = 50;

        /// <summary>
        /// The ID of the Area eg. 15 ( == index in Region.m_areas array)
        /// </summary>
        protected ushort m_ID;

        /// <summary>
        /// Holds the translation id
        /// </summary>
        protected string m_translationId;

        /// <summary>
        /// The description of the Area eg. "Camelot Hills"
        /// </summary>
        protected string m_Description;

        /// <summary>
        /// The area sound to play on enter/leave events
        /// </summary>
        protected byte m_sound;

        /// <summary>
        /// Constructs a new AbstractArea
        /// </summary>
        /// <param name="desc"></param>
        public AbstractArea(string desc)
        {
            m_Description = desc;
        }

        public AbstractArea()
            : base()
        {
        }

        /// <summary>
        /// Returns the ID of this Area
        /// </summary>
        public ushort ID
        {
            get { return m_ID; }
            set { m_ID = value; }
        }

        public int RealmPoints { get; set; }

        public virtual LanguageDataObject.eTranslationIdentifier TranslationIdentifier
        {
            get { return LanguageDataObject.eTranslationIdentifier.eArea; }
        }

        /// <summary>
        /// Gets or sets the translation id
        /// </summary>
        public string TranslationId
        {
            get { return m_translationId; }
            set { m_translationId = (value == null ? "" : value); }
        }

        /// <summary>
        /// Return the description of this Area
        /// </summary>
        public virtual string Description
        {
            get { return m_Description; }
        }

        /// <summary>
        /// Gets or sets the area sound
        /// </summary>
        public byte Sound
        {
            get { return m_sound; }
            set { m_sound = value; }
        }

        public bool IsRadioactive { get; set; } = false;

        public static int GetBoundarySpacing(DBArea dbArea)
        {
            if (dbArea == null || dbArea.BoundarySpacing <= 0) return 250;
            return dbArea.BoundarySpacing;
        }

        public virtual string GetDescriptionForPlayer(GamePlayer player)
        {
            var translation = LanguageMgr.GetTranslation(player, this) as DBLanguageArea;
            if (translation != null && !string.IsNullOrEmpty(translation.Description))
            {
                return translation.Description;
            }
            else
            {
                return Description;
            }
        }

        public virtual string GetDescriptionForPlayer(GamePlayer player, out string screenDescription)
        {
            var translation = LanguageMgr.GetTranslation(player, this) as DBLanguageArea;
            if (translation != null && !string.IsNullOrEmpty(translation.Description))
            {
                screenDescription = string.IsNullOrEmpty(translation.ScreenDescription) ? translation.Description : translation.ScreenDescription;
                return translation.Description;
            }
            else
            {
                screenDescription = Description;
                return Description;
            }
        }

        /// <summary>
        /// Whether an area is temporary (will not be saved to database)
        /// </summary>
        public bool IsTemporary { get; init; } = false;

        public Zone ZoneIn { get; set; }
        public Region Region
        {
            get; set;
        }

        #region Event handling

        public void UnRegisterPlayerEnter(DOLEventHandler callback)
        {
            GameEventMgr.RemoveHandler(this, AreaEvent.PlayerEnter, callback);
        }

        public void UnRegisterPlayerLeave(DOLEventHandler callback)
        {
            GameEventMgr.RemoveHandler(this, AreaEvent.PlayerLeave, callback);
        }

        public void RegisterPlayerEnter(DOLEventHandler callback)
        {
            GameEventMgr.AddHandler(this, AreaEvent.PlayerEnter, callback);
        }

        public void RegisterPlayerLeave(DOLEventHandler callback)
        {
            GameEventMgr.AddHandler(this, AreaEvent.PlayerLeave, callback);
        }
        #endregion

        /// <summary>
        /// Checks wether area intersects with given zone
        /// </summary>
        /// <param name="zone"></param>
        /// <returns></returns>
        public abstract bool IsIntersectingZone(Zone zone);

        /// <summary>
        /// Checks wether given spot is within areas boundaries or not
        /// </summary>
        /// <param name="spot"></param>
        /// <param name="checkZ"></param>
        /// <returns></returns>
        public abstract bool IsContaining(Coordinate spot, bool checkZ);

        public bool IsContaining(Coordinate spot) => IsContaining(spot, true);

        [Obsolete("Use .IsContaining(Coordinate[,bool]) instead!")]
        public bool IsContaining(int x, int y, int z, bool checkZ) => IsContaining(Coordinate.Create(x, y, z), checkZ);

        [Obsolete("Use .IsContaining(Coordinate[,bool]) instead!")]
        public bool IsContaining(int x, int y, int z) => IsContaining(Coordinate.Create(x, y, z), true);

        /// <summary>
        /// Get the distance to the closest edge of this area from a point
        /// </summary>
        /// <param name="position">Position to calculate the distance from</param>
        /// <param name="checkZ">Whether to take Z into account</param>
        /// <returns></returns>
        public abstract float DistanceSquared(Coordinate position, bool checkZ);

        public bool CanVol { get; protected set; }

        public DBArea DbArea { get => dbArea; set => dbArea = value; }

        private List<GamePlayer> _players = new();

        public IReadOnlyList<GamePlayer> Players
        {
            get
            {
                lock (_players)
                {
                    return new List<GamePlayer>(_players).AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Called whenever a player leaves the given area
        /// </summary>
        /// <param name="player"></param>
        public virtual void OnPlayerLeave(GamePlayer player)
        {
            lock (_players)
            {
                _players.Remove(player);
            }
            if (m_displayMessage || player.IsGM)
            {
                string description = GetDescriptionForPlayer(player);
                if (!String.IsNullOrEmpty(description))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AbstractArea.Left", description),
                                           eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }

            if (IsRadioactive)
            {
                player.OnLeaveRadioactiveArea();
            }

            player.IsAllowToVolInThisArea = true;
            GameEventManager.Instance.PlayerLeavesArea(player, this);
            player.Notify(AreaEvent.PlayerLeave, this, new AreaEventArgs(this, player));
        }

        /// <summary>
        /// Called whenever a player enters the given area
        /// </summary>
        /// <param name="player"></param>
        public virtual void OnPlayerEnter(GamePlayer player)
        {
            lock (_players)
            {
                _players.Add(player);
            }
            
            if (m_displayMessage || player.IsGM)
            {
                string description = GetDescriptionForPlayer(player, out string screenDescription);

                if (!String.IsNullOrEmpty(description))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AbstractArea.Entered", description),
                                           eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }

                //Changed by Apo 9. August 2010: Areas never send an screen description, but we will support it with a server property
                if (ServerProperties.Properties.DISPLAY_AREA_ENTER_SCREEN_DESC && !String.IsNullOrEmpty(screenDescription))
                    player.Out.SendMessage(screenDescription, eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
            }
            if (Sound != 0)
            {
                player.Out.SendRegionEnterSound(Sound);
            }

            if (IsRadioactive)
            {
                player.OnEnterRadioactiveArea();
            }

            player.IsAllowToVolInThisArea = this.CanVol;
            player.Notify(AreaEvent.PlayerEnter, this, new AreaEventArgs(this, player));
            GameEventManager.Instance.PlayerEntersArea(player, this);
        }

        #region Event Status
        /// <summary>
        /// Checks if any event listed in the Area's EventList is currently running.
        /// </summary>
        protected bool IsEventActive()
        {
            if (DbArea == null || string.IsNullOrEmpty(DbArea.EventList))
                return false;

            var eventIds = DbArea.EventList.Split(';');
            foreach (var id in eventIds)
            {
                var ev = GameEventManager.Instance.GetEventByID(id.Trim());
                if (ev != null && ev.IsRunning)
                    return true;
            }
            return false;
        }
        #endregion

        #region Boundary Logic
        public virtual void ClearBoundary()
        {
            foreach (var obj in m_boundaryObjects) obj.Delete();
            m_boundaryObjects.Clear();
        }

        public virtual void SpawnBoundary()
        {
            ClearBoundary();
            if (DbArea == null || Region == null) return;

            // Determine which model to use based on Event Status
            int model = DbArea.Boundary;
            if (IsEventActive() && DbArea.BoundaryEvent > 0)
            {
                model = DbArea.BoundaryEvent;
            }

            if (model <= 0) return;

            int radius = DbArea.Radius;
            if (radius <= 0) return;

            int spacing = GetBoundarySpacing(DbArea);
            double k = spacing / (2.0 * Math.PI * 1000.0);
            int count = (int)Math.Round((2.0 * Math.PI * radius) * k);
            count = Math.Max(6, Math.Min(128, count));

            for (int i = 0; i < count; i++)
            {
                double t = (2.0 * Math.PI * i) / count;
                Angle outward = Angle.Radians(t);
                Vector offset = Vector.Create(outward, radius);

                var mini = new GameStaticItem
                {
                    Model = (ushort)model,
                    Name = "Boundary",
                    Position = Position.Create(Region.ID, DbArea.X + offset.X, DbArea.Y + offset.Y, DbArea.Z, outward.InHeading)
                };
                mini.AddToWorld();
                m_boundaryObjects.Add(mini);
            }
        }
        #endregion

        #region Effect Loop Logic
        public virtual void StartEffectLoop()
        {
            StopEffectLoop();
            if (DbArea == null || Region == null) return;

            if (DbArea.SpellID <= 0 && DbArea.SpellIDEvent <= 0 && string.IsNullOrEmpty(DbArea.EventList))
                return;

            GameNPC timerOwner = new GameNPC();
            timerOwner.Name = "AreaEffectController";
            timerOwner.Flags |= GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME;
            timerOwner.Model = 667;
            timerOwner.Position = Position.Create(Region.ID, DbArea.X, DbArea.Y, DbArea.Z, 0);
            timerOwner.AddToWorld();

            int freq = DbArea.EffectFrequency > 0 ? DbArea.EffectFrequency : 2000;
            m_effectLoopTimer = new RegionTimer(timerOwner, EffectLoopCallback);
            m_effectLoopTimer.Start(freq);
        }

        public virtual void StopEffectLoop()
        {
            if (m_effectLoopTimer != null)
            {
                m_effectLoopTimer.Stop();
                if (m_effectLoopTimer.Owner != null) m_effectLoopTimer.Owner.Delete();
                m_effectLoopTimer = null;
            }
        }

        protected virtual int EffectLoopCallback(RegionTimer timer)
        {
            if (DbArea == null || Region == null) return 0;

            bool currentEventState = IsEventActive();
            if (currentEventState != m_lastEventState)
            {
                m_lastEventState = currentEventState;
                SpawnBoundary();
            }

            int activeSpellID = (currentEventState && DbArea.SpellIDEvent > 0) ? DbArea.SpellIDEvent : DbArea.SpellID;
            if (activeSpellID <= 0) return Math.Max(1000, DbArea.EffectFrequency > 0 ? DbArea.EffectFrequency : 5000);

            int baseAmount = DbArea.EffectAmount;
            double varFactor = DbArea.EffectVariance / 100.0;

            int actualAmount = baseAmount;
            int actualLevel = DbArea.StormLevel > 0 ? DbArea.StormLevel : 1;

            if (varFactor > 0)
            {
                int rangeAmt = (int)Math.Round(baseAmount * varFactor);
                actualAmount = Util.Random(baseAmount - rangeAmt, baseAmount + rangeAmt);

                int rangeLvl = (int)Math.Round(actualLevel * varFactor);
                actualLevel = Util.Random(actualLevel - rangeLvl, actualLevel + rangeLvl);
            }
            actualAmount = Math.Max(1, actualAmount);
            actualLevel = Math.Max(1, Math.Min(255, actualLevel));

            for (int i = 0; i < actualAmount; i++)
            {
                Coordinate randomPoint = GetRandomPointInside();
                if (randomPoint.Equals(Coordinate.Nowhere)) continue;

                GameNPC stormPoint = new GameNPC();
                stormPoint.Model = 667;
                stormPoint.Name = "Storm";
                stormPoint.Flags = GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME;
                stormPoint.Position = Position.Create(Region.ID, randomPoint.X, randomPoint.Y, randomPoint.Z, 0);
                stormPoint.Level = (byte)actualLevel;
                stormPoint.Size = DbArea.StormSize > 0 ? DbArea.StormSize : (byte)50;
                stormPoint.AddToWorld();

                new RegionTimer(stormPoint, (t) =>
                {
                    if (stormPoint.ObjectState != GameObject.eObjectState.Active) return 0;

                    Spell spell = SkillBase.GetSpellByID(activeSpellID);
                    ushort effectID = (ushort)(spell != null ? spell.ClientEffect : activeSpellID);

                    foreach (GamePlayer player in Region.GetPlayersInRadius(randomPoint, (ushort)WorldMgr.VISIBILITY_DISTANCE, false, false))
                    {
                        player.Out.SendSpellEffectAnimation(stormPoint, stormPoint, effectID, 0, false, 1);
                    }

                    if (spell != null)
                    {
                        SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);
                        ISpellHandler handler = ScriptMgr.CreateSpellHandler(stormPoint, spell, line);
                        if (handler != null)
                        {
                            handler.StartSpell(stormPoint);
                        }
                    }
                    return 0;
                }).Start(250);

                new RegionTimer(stormPoint, t => {
                    if (stormPoint.ObjectState == GameObject.eObjectState.Active) stormPoint.Delete();
                    return 0;
                }).Start(3000);
            }

            int baseFreq = DbArea.EffectFrequency;
            int nextInterval = baseFreq;
            if (varFactor > 0)
            {
                int varMs = (int)(baseFreq * varFactor);
                nextInterval = Util.Random(baseFreq - varMs, baseFreq + varMs);
            }
            return Math.Max(500, nextInterval);
        }

        protected virtual Coordinate GetRandomPointInside()
        {
            if (DbArea == null) return Coordinate.Nowhere;

            double angle = Util.RandomDouble() * Math.PI * 2;
            double dist;

            if (this is Area.Tore)
            {
                int inner = DbArea.Radius;
                int outer = DbArea.MaxRadius > inner ? DbArea.MaxRadius : inner * 2;
                dist = Math.Sqrt(Util.RandomDouble() * (outer * outer - inner * inner) + (inner * inner));
            }
            else if (this is Area.Ellipse)
            {
                double r = Math.Sqrt(Util.RandomDouble());
                int newX = DbArea.X + (int)(r * DbArea.Radius * Math.Cos(angle));
                int newY = DbArea.Y + (int)(r * DbArea.MaxRadius * Math.Sin(angle));
                return Coordinate.Create(newX, newY, DbArea.Z);
            }
            else if (this is Area.Polygon)
            {
                int randX = Util.Random(0, DbArea.Radius);
                int randY = Util.Random(0, DbArea.Radius);
                return Coordinate.Create(DbArea.X + randX, DbArea.Y + randY, DbArea.Z);
            }
            else
            {
                int radius = DbArea.Radius;
                dist = Math.Sqrt(Util.RandomDouble()) * radius;
            }

            int finalX = DbArea.X + (int)(Math.Cos(angle) * dist);
            int finalY = DbArea.Y + (int)(Math.Sin(angle) * dist);
            return Coordinate.Create(finalX, finalY, DbArea.Z);
        }
        #endregion

        public abstract void LoadFromDatabase(DBArea area);
    }
}
