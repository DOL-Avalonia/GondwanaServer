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

namespace DOL.GS
{
    /// <summary>
    /// AbstractArea extend this if you wish to implement e new custom area.
    /// For examples see Area.Cricle, Area.Square
    /// </summary>
    public abstract class AbstractArea : IArea, ITranslatableObject
    {
        protected DBArea dbArea = null;
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

            player.IsAllowToVolInThisArea = this.CanVol;
            player.Notify(AreaEvent.PlayerEnter, this, new AreaEventArgs(this, player));
            GameEventManager.Instance.PlayerEntersArea(player, this);
        }

        public abstract void LoadFromDatabase(DBArea area);
    }
}
