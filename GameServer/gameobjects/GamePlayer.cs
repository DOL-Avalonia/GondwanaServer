/*
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
using System.Reflection;
using System.Text;

using DOL.AI;
using DOL.AI.Brain;
using System.Timers;
using AmteScripts.Managers;
using DOL.Database;
using DOL.events.gameobjects;
using DOL.Events;
using DOL.gameobjects.CustomNPC;
using DOL.GS.Effects;
using DOL.GS.Finance;
using DOL.GS.Housing;
using DOL.GS.Keeps;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.GS.PlayerTitles;
using DOL.GS.PropertyCalc;
using DOL.GS.Quests;
using DOL.GS.RealmAbilities;
using DOL.GS.ServerProperties;
using DOL.GS.SkillHandler;
using DOL.GS.Spells;
using DOL.GS.Styles;
using DOL.GS.Utils;
using DOL.Language;
using DOL.Territories;
using DOL.GameEvents;
using DOL.GS.GameEvents;
using DOL.GS.Realm;
using DOL.GS.Scripts;
using log4net;
using System.Collections.Immutable;
using DOL.GS.Geometry;
using AmteScripts.PvP.CTF;
using Discord;
using System.Drawing.Drawing2D;

namespace DOL.GS
{

    /// <summary>
    /// This class represents a player inside the game
    /// </summary>
    public class GamePlayer : GameLiving
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        private static long reputationDaysDurationInSeconds = Properties.REPUTATION_DAYS_INTERVAL * 86400;

        private readonly object m_LockObject = new object();

        private Timer afkXpTimer;

        private Timer kickoutTimer;

        private Timer reputationRecoveryTimer;

        private Timer afkDelayTimer;

        private bool stayStealth = false;

        private ShadowNPC shadowNPC;

        private TaskXPlayer taskXPlayer;

        #region Client/Character/VariousFlags

        /// <summary>
        /// This is our gameclient!
        /// </summary>
        protected readonly GameClient m_client;

        /// <summary>
        /// This holds the character this player is
        /// based on!
        /// (renamed and private, cause if derive is needed overwrite PlayerCharacter)
        /// </summary>
        protected DOLCharacters m_dbCharacter;

        /// <summary>
        /// The guild id this character belong to
        /// </summary>
        protected string m_guildId;

        /// <summary>
        /// Char spec points checked on load
        /// </summary>
        protected bool SpecPointsOk = true;

        /// <summary>
        /// Has this player entered the game, will be
        /// true after the first time the char enters
        /// the world
        /// </summary>
        protected bool m_enteredGame;

        /// <summary>
        /// Is this player being 'jumped' to a new location?
        /// </summary>
        public bool IsJumping { get; set; }

        /// <summary>
        /// true if the targetObject is visible
        /// </summary>
        protected bool m_targetInView;

        /// <summary>
        /// Property for the optional away from keyboard message.
        /// </summary>
        public static readonly string AFK_MESSAGE = "afk_message";

        /// <summary>
        /// Property for the optional away from keyboard message.
        /// </summary>
        public static readonly string QUICK_CAST_CHANGE_TICK = "quick_cast_change_tick";

        /// <summary>
        /// Last spell cast from a used item
        /// </summary>
        public static readonly string LAST_USED_ITEM_SPELL = "last_used_item_spell";

        /// <summary>
        /// Array that stores ML step completition
        /// </summary>
        private ArrayList m_mlSteps = new ArrayList();


        /// <summary>
        /// Can this living accept any item regardless of tradable or droppable?
        /// </summary>
        public override bool CanTradeAnyItem { get { return Client.Account.PrivLevel > (int)ePrivLevel.Player; } }

        /// <summary>
        /// Gets/sets the characters option to receive ROGs /eventrog
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool ReceiveROG
        {
            get { return DBCharacter != null ? DBCharacter.ReceiveROG : true; }
            set { if (DBCharacter != null) DBCharacter.ReceiveROG = value; }
        }
        
        protected int m_OutOfClassROGPercent = 0;

        public int OutOfClassROGPercent
        {
            get { return m_OutOfClassROGPercent; }
            set { m_OutOfClassROGPercent = value; }
        }


        /// <summary>
        /// Gets or sets the targetObject's visibility
        /// </summary>
        public override bool TargetInView
        {
            get { return m_targetInView; }
            set { m_targetInView = value; }
        }

        public Timer AfkXpTimer
        {
            get => this.afkXpTimer ?? (this.afkXpTimer = new Timer());
        }

        public Timer AfkDelayTimer
        {
            get => this.afkDelayTimer ?? (this.afkDelayTimer = new Timer());
        }

        public Timer KickoutTimer
        {
            get => this.kickoutTimer ?? (this.kickoutTimer = new Timer());
        }

        public bool IsAfkDelayElapsed
        {
            get;
            private set;
        }

        /// <summary>
        /// Holds the ground target visibility flag
        /// </summary>
        protected bool m_groundtargetInView;

        /// <summary>
        /// Gets or sets the GroundTargetObject's visibility
        /// </summary>
        public override bool GroundTargetInView
        {
            get { return m_groundtargetInView; }
            set { m_groundtargetInView = value; }
        }

        public BattleGroup BattleGroup
        {
            get;
            set;
        }

        public bool isInBG
        {
            get => BattleGroup != null;
        }

        public bool IsInPvP
        {
            get;
            set;
        }

        public bool IsInRvR
        {
            get;
            set;
        }

        /// <summary>
        /// Current warmap page
        /// </summary>
        private volatile byte m_warmapPage = 1;
        public byte WarMapPage
        {
            get { return m_warmapPage; }
            set { m_warmapPage = value; }
        }

        /// <summary>
        /// Returns the GameClient of this Player
        /// </summary>
        public virtual GameClient Client
        {
            get { return m_client; }
        }

        /// <summary>
        /// Returns the PacketSender for this player
        /// </summary>
        public virtual IPacketLib Out
        {
            get { return Client.Out; }
        }
        
        public bool IsGM => (Client?.Account?.PrivLevel >= (int)ePrivLevel.GM) == true;

        /// <summary>
        /// The character the player is based on
        /// </summary>
        internal DOLCharacters DBCharacter
        {
            get { return m_dbCharacter; }
        }

        /// <summary>
        /// Has this player entered the game for the first
        /// time after logging on (not Zoning!)
        /// </summary>
        public bool EnteredGame
        {
            get { return m_enteredGame; }
            set { m_enteredGame = value; }
        }

        protected DateTime m_previousLoginDate = DateTime.MinValue;
        /// <summary>
        /// What was the last time this player logged in?
        /// </summary>
        public DateTime PreviousLoginDate
        {
            get { return m_previousLoginDate; }
            set { m_previousLoginDate = value; }
        }

        /// <summary>
        /// Gets or sets the anonymous flag for this player
        /// (delegate to property in PlayerCharacter)
        /// </summary>
        public bool IsAnonymous
        {
            get { return DBCharacter != null ? DBCharacter.IsAnonymous && (ServerProperties.Properties.ANON_MODIFIER != -1) : false; }
            set
            {
                var old = IsAnonymous;
                if (DBCharacter != null)
                    DBCharacter.IsAnonymous = value;

                if (old != IsAnonymous)
                    GameEventMgr.Notify(GamePlayerEvent.ChangeAnonymous, this);
            }
        }

        /// <summary>
        /// Whether or not the player can be attacked.
        /// </summary>
        public override bool IsAttackable { get { return (Client.Account.PrivLevel <= (uint)ePrivLevel.Player && base.IsAttackable); } }

        /// <summary>
        /// Is this player PvP enabled
        /// </summary>
        public virtual bool IsPvP { get { return GameServer.Instance.Configuration.ServerType == eGameServerType.GST_PvP; } }

        /// <summary>
        /// Can this player use cross realm items
        /// </summary>
        public virtual bool CanUseCrossRealmItems { get { return ServerProperties.Properties.ALLOW_CROSS_REALM_ITEMS; } }

        protected bool m_canUseSlashLevel = false;
        public bool CanUseSlashLevel { get { return m_canUseSlashLevel; } }

        /// <summary>
        /// if player uses debug before (to prevent hack client fly mode for players using debug and then turning it off)
        /// </summary>
        protected bool m_canFly;

        /// <summary>
        /// Is this player allowed to fly?
        /// This should only be set in debug command handler.  If player is flying but this flag is false then fly hack is detected
        /// </summary>
        public bool IsAllowedToFly
        {
            get { return m_canFly; }
            set { m_canFly = value; }
        }

        public bool IsAllowToVolInThisArea
        {
            get;
            set;
        }

        private bool m_statsAnon = false;

        /// <summary>
        /// Gets or sets the stats anon flag for the command /statsanon
        /// (delegate to property in PlayerCharacter)
        /// </summary>
        public bool StatsAnonFlag
        {
            get { return m_statsAnon; }
            set { m_statsAnon = value; }
        }

        public string PlayerAfkMessage
        {
            get;
            set;
        }

        public TaskXPlayer TaskXPlayer
        {
            get { return taskXPlayer; }
            set { taskXPlayer = value; }
        }

        #region DoorCache
        protected Dictionary<int, eDoorState> m_doorUpdateList = null;

        protected ushort m_doorUpdateRegionID;

        /// <summary>
        /// Send a door state to this client
        /// </summary>
        /// <param name="door">the door</param>
        /// <param name="forceUpdate">force a send of the door state regardless of status</param>
        public void SendDoorUpdate(IDoor door, bool forceUpdate = false)
        {
            Out.SendObjectCreate(door as GameObject);

            if (m_doorUpdateList == null || m_doorUpdateRegionID != CurrentRegionID)
            {
                m_doorUpdateList = new Dictionary<int, eDoorState>();
                m_doorUpdateRegionID = CurrentRegionID;
                m_doorUpdateList.Add(door.ObjectID, door.State);
                Out.SendDoorState(CurrentRegion, door);
            }
            else if (forceUpdate || m_doorUpdateList.ContainsKey(door.ObjectID) == false || m_doorUpdateList[door.ObjectID] != door.State)
            {
                Out.SendDoorState(CurrentRegion, door);
                m_doorUpdateList[door.ObjectID] = door.State;
            }

            Out.SendObjectUpdate(door as GameObject);
        }
        #endregion

        #region Database Accessor

        /// <summary>
        /// Gets or sets the Database ObjectId for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public string ObjectId
        {
            get { return DBCharacter != null ? DBCharacter.ObjectId : InternalID; }
            set { if (DBCharacter != null) DBCharacter.ObjectId = value; }
        }

        /// <summary>
        /// Gets or sets the no help flag for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool NoHelp
        {
            get { return DBCharacter != null ? DBCharacter.NoHelp : false; }
            set { if (DBCharacter != null) DBCharacter.NoHelp = value; }
        }

        /// <summary>
        /// Gets or sets the show guild logins flag for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool ShowGuildLogins
        {
            get { return DBCharacter != null ? DBCharacter.ShowGuildLogins : false; }
            set { if (DBCharacter != null) DBCharacter.ShowGuildLogins = value; }
        }

        /// <summary>
        /// Gets or sets the gain XP flag for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool GainXP
        {
            get { return DBCharacter != null ? DBCharacter.GainXP : true; }
            set { if (DBCharacter != null) DBCharacter.GainXP = value; }
        }

        /// <summary>
        /// Gets or sets the gain RP flag for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool GainRP
        {
            get { return (DBCharacter != null ? DBCharacter.GainRP : true); }
            set { if (DBCharacter != null) DBCharacter.GainRP = value; }
        }

        /// <summary>
        /// Gets or sets the roleplay flag for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool RPFlag
        {
            get { return (DBCharacter != null ? DBCharacter.RPFlag : true); }
            set { if (DBCharacter != null) DBCharacter.RPFlag = value; }
        }

        /// <summary>
        /// gets or sets the guildnote for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public string GuildNote
        {
            get { return DBCharacter != null ? DBCharacter.GuildNote : String.Empty; }
            set { if (DBCharacter != null) DBCharacter.GuildNote = value; }
        }

        /// <summary>
        /// Gets or sets the autoloot flag for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool Autoloot
        {
            get { return DBCharacter != null ? DBCharacter.Autoloot : true; }
            set { if (DBCharacter != null) DBCharacter.Autoloot = value; }
        }

        /// <summary>
        /// Gets or sets the advisor flag for this player
        /// (delegate to property in PlayerCharacter)
        /// </summary>
        public bool Advisor
        {
            get { return DBCharacter != null ? DBCharacter.Advisor : false; }
            set { if (DBCharacter != null) DBCharacter.Advisor = value; }
        }

        /// <summary>
        /// Gets or sets the SerializedFriendsList for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public string[] SerializedFriendsList
        {
            get { return DBCharacter != null ? DBCharacter.SerializedFriendsList.Split(',').Select(name => name.Trim()).Where(name => !string.IsNullOrEmpty(name)).ToArray() : Array.Empty<string>(); }
            set { if (DBCharacter != null) DBCharacter.SerializedFriendsList = string.Join(",", value.Select(name => name.Trim()).Where(name => !string.IsNullOrEmpty(name))); }
        }

        /// <summary>
        /// Gets or sets the SerializedAskNameList for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public string[] SerializedAskNameList
        {
            get { return DBCharacter != null ? DBCharacter.SerializedAskNameList.Split(',').Select(name => name.Trim()).Where(name => !string.IsNullOrEmpty(name)).ToArray() : Array.Empty<string>(); }
            set { if (DBCharacter != null) DBCharacter.SerializedAskNameList = string.Join(",", value.Select(name => name.Trim()).Where(name => !string.IsNullOrEmpty(name))); }
        }

        /// <summary>
        /// Gets or sets the NotDisplayedInHerald for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public byte NotDisplayedInHerald
        {
            get { return DBCharacter != null ? DBCharacter.NotDisplayedInHerald : (byte)0; }
            set { if (DBCharacter != null) DBCharacter.NotDisplayedInHerald = value; }
        }

        /// <summary>
        /// Gets or sets the LastFreeLevel for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public int LastFreeLevel
        {
            get { return DBCharacter != null ? DBCharacter.LastFreeLevel : 0; }
            set { if (DBCharacter != null) DBCharacter.LastFreeLevel = value; }
        }

        /// <summary>
        /// Gets or sets the LastFreeLeveled for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public DateTime LastFreeLeveled
        {
            get { return DBCharacter != null ? DBCharacter.LastFreeLeveled : DateTime.MinValue; }
            set { if (DBCharacter != null) DBCharacter.LastFreeLeveled = value; }
        }

        /// <summary>
        /// Gets or sets the SerializedIgnoreList for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public string[] SerializedIgnoreList
        {
            get { return DBCharacter != null ? DBCharacter.SerializedIgnoreList.Split(',').Select(name => name.Trim()).Where(name => !string.IsNullOrEmpty(name)).ToArray() : Array.Empty<string>(); }
            set { if (DBCharacter != null) DBCharacter.SerializedIgnoreList = string.Join(",", value.Select(name => name.Trim()).Where(name => !string.IsNullOrEmpty(name))); }
        }

        /// <summary>
        /// Gets or sets the UsedLevelCommand for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool UsedLevelCommand
        {
            get { return DBCharacter != null ? DBCharacter.UsedLevelCommand : false; }
            set { if (DBCharacter != null) DBCharacter.UsedLevelCommand = value; }
        }

        public Position BindHousePosition
        {
            get
            {
                if (DBCharacter == null) return Position.Zero;

                return Position.Create(
                    (ushort)DBCharacter.BindHouseRegion,
                    DBCharacter.BindHouseXpos,
                    DBCharacter.BindHouseYpos,
                    DBCharacter.BindHouseZpos,
                    (ushort)DBCharacter.BindHouseHeading);
            }
            set
            {
                DBCharacter.BindHouseRegion = value.RegionID;
                DBCharacter.BindHouseXpos = value.X;
                DBCharacter.BindHouseYpos = value.Y;
                DBCharacter.BindHouseZpos = value.Z;
                DBCharacter.BindHouseHeading = value.Orientation.InHeading;
            }
        }

        /// <summary>
        /// Gets or sets the BindHouseRegion for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        [Obsolete("Use BindHousePosition instead!")]
        public int BindHouseRegion
        {
            get => BindHousePosition.RegionID;
            set => BindHousePosition.With(regionID: (ushort)value);
        }
        
        [Obsolete("Use BindHousePosition instead!")]
        public int BindHouseXpos
        {
            get => BindHousePosition.X;
            set => BindHousePosition.With(x: value);
        }
        
        [Obsolete("Use BindHousePosition instead!")]
        public int BindHouseYpos
        {
            get => BindHousePosition.Y;
            set => BindHousePosition.With(y: value);
        }

        [Obsolete("Use BindHousePosition instead!")]
        public int BindHouseZpos
        {
            get => BindHousePosition.Z;
            set => BindHousePosition.With(z: value);
        }

        [Obsolete("Use BindHousePosition instead!")]
        public int BindHouseHeading
        {
            get => BindHousePosition.Orientation.InHeading;
            set => BindHousePosition.With(Angle.Heading(value));
        }

        /// <summary>
        /// Gets or sets the CustomisationStep for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public byte CustomisationStep
        {
            get { return DBCharacter != null ? DBCharacter.CustomisationStep : (byte)0; }
            set { if (DBCharacter != null) DBCharacter.CustomisationStep = value; }
        }

        /// <summary>
        /// Gets or sets the IgnoreStatistics for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool IgnoreStatistics
        {
            get { return DBCharacter != null ? DBCharacter.IgnoreStatistics : false; }
            set { if (DBCharacter != null) DBCharacter.IgnoreStatistics = value; }
        }

        /// <summary>
        /// Gets or sets the DeathTime for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public long DeathTime
        {
            get { return DBCharacter != null ? DBCharacter.DeathTime : 0; }
            set { if (DBCharacter != null) DBCharacter.DeathTime = value; }
        }

        /// <summary>
        /// Gets or sets the ShowXFireInfo for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public bool ShowXFireInfo
        {
            get { return DBCharacter != null ? DBCharacter.ShowXFireInfo : false; }
            set { if (DBCharacter != null) DBCharacter.ShowXFireInfo = value; }
        }

        public Position BindPosition
        {
            get
            {
                if(DBCharacter == null) return Position.Zero;

                return DBCharacter.GetBindPosition();
            }
            set
            {
                if (DBCharacter == null) return;

                DBCharacter.BindRegion = value.RegionID;
                DBCharacter.BindXpos = value.X;
                DBCharacter.BindYpos = value.Y;
                DBCharacter.BindZpos = value.Z;
                DBCharacter.BindHeading = value.Orientation.InHeading;
            }
        }

        [Obsolete("Use BindPosition instead!")]
        public int BindRegion
        {
            get { return DBCharacter != null ? DBCharacter.BindRegion : 0; }
            set { if (DBCharacter != null) DBCharacter.BindRegion = value; }
        }

        /// <summary>
        /// Gets or sets the BindXpos for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        [Obsolete("Use BindPosition instead!")]
        public int BindXpos
        {
            get { return DBCharacter != null ? DBCharacter.BindXpos : 0; }
            set { if (DBCharacter != null) DBCharacter.BindXpos = value; }
        }

        /// <summary>
        /// Gets or sets the BindYpos for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        [Obsolete("Use BindPosition instead!")]
        public int BindYpos
        {
            get { return DBCharacter != null ? DBCharacter.BindYpos : 0; }
            set { if (DBCharacter != null) DBCharacter.BindYpos = value; }
        }

        /// <summary>
        /// Gets or sets the BindZpos for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        [Obsolete("Use BindPosition instead!")]
        public int BindZpos
        {
            get { return DBCharacter != null ? DBCharacter.BindZpos : 0; }
            set { if (DBCharacter != null) DBCharacter.BindZpos = value; }
        }

        /// <summary>
        /// Gets or sets the BindHeading for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        [Obsolete("Use BindPosition instead!")]
        public int BindHeading
        {
            get { return DBCharacter != null ? DBCharacter.BindHeading : 0; }
            set { if (DBCharacter != null) DBCharacter.BindHeading = value; }
        }

        /// <summary>
        /// Gets or sets the Database MaxEndurance for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public int DBMaxEndurance
        {
            get { return DBCharacter != null ? DBCharacter.MaxEndurance : 100; }
            set { if (DBCharacter != null) DBCharacter.MaxEndurance = value; }
        }

        /// <summary>
        /// Gets AccountName for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public string AccountName
        {
            get { return DBCharacter != null ? DBCharacter.AccountName : string.Empty; }
        }

        /// <summary>
        /// Gets CreationDate for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public DateTime CreationDate
        {
            get { return DBCharacter != null ? DBCharacter.CreationDate : DateTime.MinValue; }
        }

        /// <summary>
        /// Gets LastPlayed for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public DateTime LastPlayed
        {
            get { return DBCharacter != null ? DBCharacter.LastPlayed : DateTime.MinValue; }
        }

        /// <summary>
        /// Gets or sets the BindYpos for this player
        /// (delegate to property in DBCharacter)
        /// </summary>
        public byte DeathCount
        {
            get { return DBCharacter != null ? DBCharacter.DeathCount : (byte)0; }
            set { if (DBCharacter != null) DBCharacter.DeathCount = value; }
        }

        /// <summary>
        /// Temporary Consignment Merchant for trading ability
        /// </summary>
        public TemporaryConsignmentMerchant TemporaryConsignmentMerchant
        {
            get; set;
        }


        #endregion

        #endregion

        #region Player Quitting
        /// <summary>
        /// quit timer
        /// </summary>
        protected RegionTimer m_quitTimer;

        /// <summary>
        /// Timer callback for quit
        /// </summary>
        /// <param name="callingTimer">the calling timer</param>
        /// <returns>the new intervall</returns>
        protected virtual int QuitTimerCallback(RegionTimer callingTimer)
        {
            if (!IsAlive || ObjectState != eObjectState.Active)
            {
                m_quitTimer = null;
                return 0;
            }

            bool bInstaQuit = false;

            if (Client.Account.PrivLevel > 1) // GMs can always insta quit
                bInstaQuit = true;
            else if (ServerProperties.Properties.DISABLE_QUIT_TIMER && Client.Player.InCombat == false)  // Players can only insta quit if they aren't in combat
                bInstaQuit = true;

            if (bInstaQuit == false)
            {
                if (IsCrafting)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.CantQuitCrafting"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    m_quitTimer = null;
                    return 0;
                }

                long lastCombatAction = LastAttackedByEnemyTick;
                if (lastCombatAction < LastAttackTick)
                {
                    lastCombatAction = LastAttackTick;
                }
                long secondsleft = 60 - (CurrentRegion.Time - lastCombatAction + 500) / 1000; // 500 is for rounding
                if (secondsleft > 0)
                {
                    if (secondsleft == 15 || secondsleft == 10 || secondsleft == 5)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.YouWillQuit1", secondsleft), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    return 1000;
                }
            }
            
            Out.SendPlayerQuit(false);
            Quit(true);
            SaveIntoDatabase();
            m_quitTimer = null;
            return 0;
        }

        /// <summary>
        /// Gets the amount of time the player must wait before quit, in seconds
        /// </summary>
        public virtual int QuitTime
        {
            get
            {
                if (m_quitTimer == null)
                {
                    // dirty trick ;-) (20sec min quit time)
                    if (CurrentRegion.Time - LastAttackTickPvP > 40000)
                        LastAttackTickPvP = CurrentRegion.Time - 40000;
                    if (CurrentRegion.Time - LastAttackTickPvE > 40000)
                        LastAttackTickPvE = CurrentRegion.Time - 40000;
                }
                long lastCombatAction = LastAttackTick;
                if (lastCombatAction < LastAttackedByEnemyTick)
                {
                    lastCombatAction = LastAttackedByEnemyTick;
                }
                return (int)(60 - (CurrentRegion.Time - lastCombatAction + 500) / 1000); // 500 is for rounding
            }
            set
            { }
        }

        #endregion

        #region Player Linking Dead
        /// <summary>
        /// Callback method, called when the player went linkdead and now he is
        /// allowed to be disconnected
        /// </summary>
        /// <param name="callingTimer">the timer</param>
        /// <returns>0</returns>
        protected int LinkdeathTimerCallback(RegionTimer callingTimer)
        {
            //If we died during our callback time we release
            try
            {
                if (!IsAlive)
                {
                    //Check if player should go to jail
                    if (Reputation < 0 && GameServer.ServerRules.CanPutPlayersInJail(LastKiller) && !GameServer.ServerRules.IsInPvPArea(this))
                    {
                        m_lastHeadDropTime = 0;
                        Release(eReleaseType.Jail, true);
                    }
                    else
                    {
                        Release(m_releaseType, true);
                    }
                    if (log.IsInfoEnabled)
                        log.InfoFormat("Linkdead player {0}({1}) was auto-released from death!", Name, Client.Account.Name);
                }

                SaveIntoDatabase();
            }
            finally
            {
                Client.Quit();
            }

            return 0;
        }

        public void OnLinkdeath()
        {
            if (log.IsInfoEnabled)
                log.InfoFormat("Player {0}({1}) went linkdead!", Name, Client.Account.Name);

            // LD Necros need to be "Unshaded"
            if (Client.Player.CharacterClass.Player.IsShade)
            {
                Client.Player.CharacterClass.Player.Shade(false);
            }

            // Dead link-dead players release on live servers
            if (!IsAlive)
            {
                Release(m_releaseType, true);
                if (log.IsInfoEnabled)
                    log.InfoFormat("Linkdead player {0}({1}) was auto-released from death!", Name, Client.Account.Name);
                SaveIntoDatabase();
                Client.Quit();
                return;
            }

            //Stop player if he's running....
            CurrentSpeed = 0;
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player.ObjectState != eObjectState.Active || player == null || player == this)
                    continue;
                //Maybe there is a better solution?
                player.Out.SendObjectRemove(this);
                player.Out.SendPlayerCreate(this);
            }

            UpdateEquipmentAppearance();

            LeaveHouse();

            if (m_quitTimer != null)
            {
                m_quitTimer.Stop();
                m_quitTimer = null;
            }

            if (this.IsInPvP)
            {
                PvpManager.Instance.PlayerLinkDeath(this);
            }
            else
            {
                int secondsToQuit = QuitTime;
                if (log.IsInfoEnabled)
                    log.InfoFormat("Linkdead player {0}({1}) will quit in {2}", Name, Client.Account.Name, secondsToQuit);
                RegionTimer timer = new RegionTimer(this); // make sure it is not stopped!
                timer.Callback = new RegionTimerCallback(LinkdeathTimerCallback);
                timer.Start(1 + secondsToQuit * 1000);
            }

            if (TradeWindow != null)
                TradeWindow.CloseTrade();

            //Notify players in close proximity!
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (player == null) continue;
                if (GameServer.ServerRules.IsAllowedToUnderstand(this, player))
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.OnLinkdeath.Linkdead", Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            //Notify other group members of this linkdead
            if (Group != null)
                Group.UpdateMember(this, false, false);

            CheckIfNearEnemyKeepAndAddToRvRLinkDeathListIfNecessary();

            //Notify our event handlers (if any)
            Notify(GamePlayerEvent.Linkdeath, this);
        }

        private void CheckIfNearEnemyKeepAndAddToRvRLinkDeathListIfNecessary()
        {
            AbstractGameKeep keep = GameServer.KeepManager.GetKeepCloseToSpot(Position, WorldMgr.VISIBILITY_DISTANCE);
            if (keep != null && this.Client.Account.PrivLevel == 1 && GameServer.KeepManager.IsEnemy(keep, this))
            {
                if (WorldMgr.RvRLinkDeadPlayers.ContainsKey(this.m_InternalID))
                {
                    WorldMgr.RvRLinkDeadPlayers.Remove(this.m_InternalID);
                }
                WorldMgr.RvRLinkDeadPlayers.Add(this.m_InternalID, DateTime.Now);
            }
        }

        private static List<string> registered_temprop = null;
        /// <summary>
        /// Stop all timers, events and remove player from everywhere (group/guild/chat)
        /// </summary>
        protected virtual void CleanupOnDisconnect()
        {
            StopAttack();
            // remove all stealth handlers
            Stealth(false);
            if (IsOnHorse)
                IsOnHorse = false;

            GameEventMgr.RemoveAllHandlersForObject(m_inventory);

            StopCrafting();

            if (QuestActionTimer != null)
            {
                QuestActionTimer.Stop();
                QuestActionTimer = null;
            }

            if (reputationRecoveryTimer != null)
            {
                reputationRecoveryTimer.Stop();
                reputationRecoveryTimer = null;
            }

            if (Group != null)
                Group.RemoveMember(this);

            BattleGroup mybattlegroup = (BattleGroup)this.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null);
            if (mybattlegroup != null)
                mybattlegroup.RemoveBattlePlayer(this);

            if (TradeWindow != null)
                TradeWindow.CloseTrade();

            if (m_guild != null)
                m_guild.RemoveOnlineMember(this);

            // RR4: expire personal mission, if any
            if (Mission != null)
                Mission.ExpireMission();

            ChatGroup mychatgroup = (ChatGroup)TempProperties.getProperty<object>(ChatGroup.CHATGROUP_PROPERTY, null);
            if (mychatgroup != null)
                mychatgroup.RemovePlayer(this);

            if (this.ControlledBrain != null)
                CommandNpcRelease();

            if (SiegeWeapon != null)
                SiegeWeapon.ReleaseControl();

            if (InHouse)
                LeaveHouse();

            if (ShadowNPC != null)
                ShadowNPC.Delete();

            // Dinberg: this will eventually need to be changed so that it moves them to the location they TP'ed in.
            // DamienOphyr: Overwrite current position with Bind position in database, MoveTo() is inoperant
            if (CurrentRegion.IsInstance)
            {
                Position = BindPosition;
            }

            if (!this.CurrentRegion.IsRvR)
            {
                //check for battleground caps
                Battleground bg = GameServer.KeepManager.GetBattleground(CurrentRegionID);
                if (bg != null)
                {
                    if (Level > bg.MaxLevel || RealmLevel >= bg.MaxRealmLevel)
                    {
                        // Only kick players out
                        if (Client.Account.PrivLevel == (int)ePrivLevel.Player)
                        {
                            GameServer.KeepManager.ExitBattleground(this);
                        }
                    }
                }
            }

            // cancel all effects until saving of running effects is done
            try
            {
                EffectList.SaveAllEffects();
                CancelAllConcentrationEffects();
                EffectList.CancelAll();
            }
            catch (Exception e)
            {
                log.ErrorFormat("Cannot cancel all effects - {0}", e);
            }
            #region TempPropertiesManager LookUp

            if (ServerProperties.Properties.ACTIVATE_TEMP_PROPERTIES_MANAGER_CHECKUP)
            {
                try
                {
                    foreach (string p in TempProperties.getAllProperties())
                    {

                        if (p == "")
                            continue;

                        int occurences = 0;

                        //List<string> registered_temprop = new List<string>;

                        registered_temprop = Util.SplitCSV(ServerProperties.Properties.TEMPPROPERTIES_TO_REGISTER).ToList();

                        occurences = (from j in registered_temprop
                                      where p.Contains(j)
                                      select j).Count();
                        if (occurences == 0)
                            continue;

                        object v = TempProperties.getProperty<object>(p, null);

                        if (v == null)
                            continue;

                        long longresult = 0;
                        if (long.TryParse(v.ToString(), out longresult))
                        {
                            if (ServerProperties.Properties.ACTIVATE_TEMP_PROPERTIES_MANAGER_CHECKUP_DEBUG)
                                log.Debug("On Disconnection found and was saved: " + p + " with value: " + v.ToString() + " for player: " + Name);

                            TempPropertiesManager.TempPropContainerList.Add(new TempPropertiesManager.TempPropContainer(DBCharacter.ObjectId, p, v.ToString()));
                            TempProperties.removeProperty(p);
                        }
                        else
                        {
                            if (ServerProperties.Properties.ACTIVATE_TEMP_PROPERTIES_MANAGER_CHECKUP_DEBUG)
                                log.Debug("On Disconnection found but was not saved (not a long value): " + p + " with value: " + v.ToString() + " for player: " + Name);
                        }
                    }
                }
                catch (Exception e)
                {
                    log.Debug("Error in TempProproperties Manager when saving TempProp: " + e.ToString());
                }
            }

            #endregion TempPropertiesManager LookUp
        }

        /// <summary>
        /// This function saves the character and sends a message to all others
        /// that the player has quit the game!
        /// </summary>
        /// <param name="forced">true if Quit can not be prevented!</param>
        public virtual bool Quit(bool forced)
        {
            if (!forced)
            {
                if (!IsAlive)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.CantQuitDead"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (Steed != null || IsOnHorse)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.CantQuitMount"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (IsMoving && !ServerProperties.Properties.DISABLE_QUIT_TIMER)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.CantQuitStanding"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (IsCrafting)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.CantQuitCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                if (CurrentRegion.IsInstance)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.CantQuitInInstance"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                if (IsDamned)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.CanTQuitDamned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                if (IsStunned)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.CanTQuitStunned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                if (IsMezzed)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.CanTQuitMezzed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                if (Statistics != null)
                {
                    string stats = Statistics.GetStatisticsMessage();
                    if (stats != "")
                    {
                        Out.SendMessage(stats, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }

                if (!IsSitting)
                {
                    Sit(true);
                }
                int secondsleft = QuitTime;

                if (m_quitTimer == null)
                {
                    m_quitTimer = new RegionTimer(this);
                    m_quitTimer.Callback = new RegionTimerCallback(QuitTimerCallback);
                    m_quitTimer.Start(1);
                }

                if (secondsleft > 20)
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.RecentlyInCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Quit.YouWillQuit2", secondsleft), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else
            {
                //Notify our event handlers (if any)
                Notify(GamePlayerEvent.Quit, this);

                // log quit
                AuditMgr.AddAuditEntry(Client, AuditType.Character, AuditSubtype.CharacterLogout, "", Name);
                
                // Remove from PvP
                PvpManager.Instance.OnPlayerQuit(this);

                //Cleanup stuff
                Delete();
            }

            if (PlayerAfkMessage != null)
            {
                ResetAFK(true);
            }

            return true;
        }

        #endregion

        #region Combat timer
        /// <summary>
        /// gets the DamageRvR Memory of this player
        /// </summary>
        public override long DamageRvRMemory
        {
            get { return m_damageRvRMemory; }
            set { m_damageRvRMemory = value; }
        }

        /// <summary>
        /// Override For Combat Timer Update
        /// </summary>
        public override long LastAttackedByEnemyTickPvE
        {
            set
            {
                bool wasInCombat = InCombat;
                base.LastAttackedByEnemyTickPvE = value;
                if (!wasInCombat && InCombat)
                    Out.SendUpdateMaxSpeed();

                ResetInCombatTimer();
            }
        }

        /// <summary>
        /// Override For Combat Timer Update
        /// </summary>
        public override long LastAttackTickPvE
        {
            set
            {
                bool wasInCombat = InCombat;
                base.LastAttackTickPvE = value;
                if (!wasInCombat && InCombat)
                    Out.SendUpdateMaxSpeed();

                ResetInCombatTimer();
            }
        }

        /// <summary>
        /// Expire Combat Timer Interval
        /// </summary>
        protected virtual int CombatTimerInterval { get { return 11000; } }

        /// <summary>
        /// Combat Timer Lock
        /// </summary>
        private object m_CombatTimerLock = new object();

        /// <summary>
        /// Combat Timer
        /// </summary>
        private RegionTimerAction<GamePlayer> m_CombatTimer = null;

        /// <summary>
        /// Reset and Restart Combat Timer
        /// </summary>
        protected virtual void ResetInCombatTimer()
        {
            lock (m_CombatTimerLock)
            {
                if (m_CombatTimer == null)
                {
                    m_CombatTimer = new RegionTimerAction<GamePlayer>(this, p => p.Out.SendUpdateMaxSpeed());
                }
                m_CombatTimer.Stop();
                m_CombatTimer.Start(CombatTimerInterval);
            }
        }
        #endregion

        #region release/bind/pray
        #region Binding
        /// <summary>
        /// Property that holds tick when the player bind last time
        /// </summary>
        public const string LAST_BIND_TICK = "LastBindTick";

        /// <summary>
        /// Min Allowed Interval Between Player Bind
        /// </summary>
        public virtual int BindAllowInterval { get { return 60000; } }

        /// <summary>
        /// Binds this player to a location
        /// </summary>
        /// <param name="forced">if true, can bind anywhere</param>
        public virtual void Bind(Position pos)
        {
            BindPosition = pos;
            if (DBCharacter != null)
                GameServer.Database.SaveObject(DBCharacter);
        }

        /// <summary>
        /// Binds this player to the current location
        /// </summary>
        /// <param name="forced">if true, can bind anywhere</param>
        public void Bind(bool forced)
        {
            if (forced)
            {
                Bind(this.Position);
                return;
            }
            
            if (CurrentRegion.IsInstance)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Bind.CantHere"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!IsAlive)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Bind.CantBindDead"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            //60 second rebind timer
            long lastBindTick = TempProperties.getProperty<long>(LAST_BIND_TICK, 0);
            long changeTime = CurrentRegion.Time - lastBindTick;
            if (Client.Account.PrivLevel <= (uint)ePrivLevel.Player && changeTime < BindAllowInterval)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Bind.MustWait", (1 + (BindAllowInterval - changeTime) / 1000)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            var spotDescription = WorldMgr.GetRegion(BindPosition.RegionID).GetTranslatedSpotDescription(Client, BindPosition.Coordinate);
            string description = string.Format("in {0}", spotDescription);
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Bind.LastBindPoint", description), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            bool bound = false;

            var bindarea = CurrentAreas.OfType<Area.BindArea>().FirstOrDefault(ar => GameServer.ServerRules.IsAllowedToBind(this, ar.BindPoint));
            if (bindarea != null)
            {
                Bind(Position);
                bound = true;
            }
            
            //if we are not bound yet lets check if we are in a house where we can bind
            if (!bound && InHouse && CurrentHouse != null)
            {
                var house = CurrentHouse;
                bool canbindhere;
                try
                {
                    canbindhere = house.HousepointItems.Any(kv => ((GameObject)kv.Value.GameObject).GetName(0, false).EndsWith("bindstone", StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    canbindhere = false;
                }

                if (canbindhere)
                {
                    // make sure we can actually use the bindstone
                    if (!house.CanBindInHouse(this))
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Bind.CantHere"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }
                    else
                    {
                        bound = true;
                        Angle houseAngle = Angle.Heading(house.Orientation);
                        double angle = houseAngle.InDegrees * ((Math.PI * 2) / 360); // angle in radians
                        int outsideX = (int)(house.Position.X + (0 * Math.Cos(angle) + 500 * Math.Sin(angle)));
                        int outsideY = (int)(house.Position.Y - (500 * Math.Cos(angle) - 0 * Math.Sin(angle)));
                        ushort outsideHeading = (ushort)((houseAngle.InDegrees < 180 ? houseAngle.InDegrees + 180 : houseAngle.InDegrees - 180) / 0.08789);
                        Bind(house.OutdoorJumpPosition);
                    }
                }
            }

            if (bound)
            {
                if (!IsMoving)
                {
                    eEmote bindEmote = eEmote.Bind;
                    switch (Realm)
                    {
                        case eRealm.Albion: bindEmote = eEmote.BindAlb; break;
                        case eRealm.Midgard: bindEmote = eEmote.BindMid; break;
                        case eRealm.Hibernia: bindEmote = eEmote.BindHib; break;
                    }

                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (player == null)
                            return;

                        if ((int)player.Client.Version < (int)GameClient.eClientVersion.Version187)
                            player.Out.SendEmoteAnimation(this, eEmote.Bind);
                        else
                            player.Out.SendEmoteAnimation(this, bindEmote);
                    }
                }

                TempProperties.setProperty(LAST_BIND_TICK, CurrentRegion.Time);
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Bind.Bound"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Bind.CantHere"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
        #endregion

        #region Releasing
        /// <summary>
        /// tick when player is died
        /// </summary>
        protected uint m_deathTick;

        /// <summary>
        /// choosed the player to release as soon as possible?
        /// </summary>
        protected bool m_automaticRelease = false;

        /// <summary>
        /// The release timer for this player
        /// </summary>
        protected RegionTimer m_releaseTimer;

        /// <summary>
        /// Stops release timer and closes timer window
        /// </summary>
        public void StopReleaseTimer()
        {
            Out.SendCloseTimerWindow();
            if (m_releaseTimer != null)
            {
                m_releaseTimer.Stop();
                m_releaseTimer = null;
            }
        }

        /// <summary>
        /// minimum time to wait before release is possible in seconds
        /// </summary>
        protected const int RELEASE_MINIMUM_WAIT = 10;

        /// <summary>
        /// max time before auto release in seconds
        /// </summary>
        protected const int RELEASE_TIME = 900;

        /// <summary>
        /// The property name that is set when releasing to another region
        /// </summary>
        public const string RELEASING_PROPERTY = "releasing";

        /// <summary>
        /// The current after-death player release type
        /// </summary>
        public enum eReleaseType
        {
            /// <summary>
            /// Normal release to the bind point using /release command and 10sec delay after death
            /// </summary>
            Normal,
            /// <summary>
            /// Release to the players home city
            /// </summary>
            City,
            /// <summary>
            /// Release to the current location
            /// </summary>
            Duel,
            /// <summary>
            /// Release to your bind point
            /// </summary>
            Bind,
            /// <summary>
            /// Release in a battleground or the frontiers
            /// </summary>
            RvR,
            /// <summary>
            /// Release to players house
            /// </summary>
            House,
            /// <summary>
            /// Release To Jail
            /// </summary>
            Jail
        }

        /// <summary>
        /// The current release type
        /// </summary>
        protected eReleaseType m_releaseType = eReleaseType.Normal;

        /// <summary>
        /// Gets the player's current release type.
        /// </summary>
        public eReleaseType ReleaseType
        {
            get { return m_releaseType; }
        }

        /// <summary>
        /// Releases this player after death ... subtracts xp etc etc...
        /// </summary>
        /// <param name="releaseCommand">The type of release used for this player</param>
        /// <param name="forced">if true, will release even if not dead</param>
        public virtual void Release(eReleaseType releaseCommand, bool forced)
        {
            DOLCharacters character = DBCharacter;
            if (character == null) return;

            // check if valid housebind
            if (releaseCommand == eReleaseType.House && character.BindHouseRegion < 1)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.NoValidBindpoint"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                releaseCommand = eReleaseType.Bind;
            }

            //battlegrounds caps
            Battleground bg = GameServer.KeepManager.GetBattleground(CurrentRegionID);
            if (bg != null && releaseCommand == eReleaseType.RvR)
            {
                if (Level > bg.MaxLevel)
                    releaseCommand = eReleaseType.Normal;
            }

            if (IsAlive)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.NotDead"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!forced)
            {
                if (m_releaseType == eReleaseType.Duel)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.CantReleaseDuel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
                m_releaseType = releaseCommand;
                // we use realtime, because timer window is realtime
                var diff = m_deathTick - GameTimer.GetTickCount() + RELEASE_MINIMUM_WAIT * 1000;
                if (diff >= 1000)
                {
                    if (m_automaticRelease)
                    {
                        m_automaticRelease = false;
                        m_releaseType = eReleaseType.Normal;
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.NoLongerReleaseAuto", diff / 1000), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    m_automaticRelease = true;
                    switch (releaseCommand)
                    {
                        default:
                            {
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.WillReleaseAuto", diff / 1000), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }
                        case eReleaseType.City:
                            {
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.WillReleaseAutoCity", diff / 1000), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }
                        case eReleaseType.RvR:
                            {
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.ReleaseToPortalKeep", diff / 1000), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }
                        case eReleaseType.House:
                            {
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.ReleaseToHouse", diff / 1000), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }
                    }
                }
            }
            else
            {
                m_releaseType = releaseCommand;
            }
            
            var releasePosition = Position.Zero;
            switch (m_releaseType)
            {
                case eReleaseType.Duel:
                    {
                        releasePosition = character.GetPosition().With(Angle.Degrees(180));
                        break;
                    }
                case eReleaseType.House:
                    {
                        releasePosition = BindHousePosition;
                        break;
                    }

                case eReleaseType.City:
                    {
                        if (Realm == eRealm.Hibernia)
                        {
                            // Hibernia
                            releasePosition = Position.Create(51, 426733, 416856, 5712, 2605);
                        }
                        else if (Realm == eRealm.Midgard)
                        {
                            // Midgard
                            releasePosition = Position.Create(51, 403717, 503227, 4680, 434);
                        }
                        else
                        {
                            // Albion
                            releasePosition = Position.Create(51, 526529, 541975, 3168, 1861);
                        }
                        break;
                    }
                case eReleaseType.RvR:
                    {
                        foreach (AbstractGameKeep keep in GameServer.KeepManager.GetKeepsOfRegion(CurrentRegionID))
                        {
                            if (keep.IsPortalKeep && keep.OriginalRealm == Realm)
                            {
                                releasePosition = Position.Create(keep.CurrentRegion.ID, keep.X, keep.Y, keep.Z);
                            }
                        }

                        //if we aren't releasing anywhere, release to the border keeps
                        if (releasePosition.Coordinate == Coordinate.Zero)
                        {
                            releasePosition = GameServer.KeepManager.GetBorderKeepPosition(((byte)Realm * 2) / 1);
                        }
                        break;
                    }
                default:
                    {
                        if (!ServerProperties.Properties.DISABLE_TUTORIAL)
                        {
                            //Tutorial
                            if (BindPosition.RegionID == 27)
                            {
                                switch (Realm)
                                {
                                    case eRealm.Albion:
                                        {
                                            // Cotswold
                                            releasePosition = Position.Create(regionID: 1, x: 8192 + 553251, y: 8192 + 502936, z: 2280);
                                            break;
                                        }
                                    case eRealm.Midgard:
                                        {
                                            // Mularn
                                            releasePosition = Position.Create(regionID: 100, x: 8192 + 795621, y: 8192 + 719590, z: 4680);
                                            break;
                                        }
                                    case eRealm.Hibernia:
                                        {
                                            // MagMell
                                            releasePosition = Position.Create(regionID: 200, x: 8192 + 338652, y: 8192 + 482335, z: 5200);
                                            break;
                                        }
                                }
                                break;
                            }
                        }
                        switch (CurrentRegionID)
                        {
                            //battlegrounds
                            case 234:
                            case 235:
                            case 236:
                            case 237:
                            case 238:
                            case 239:
                            case 240:
                            case 241:
                            case 242:
                                {
                                    //get the bg cap
                                    byte cap = 50;
                                    foreach (AbstractGameKeep keep in GameServer.KeepManager.GetKeepsOfRegion(CurrentRegionID))
                                    {
                                        if (keep.DBKeep.BaseLevel < cap)
                                        {
                                            cap = keep.DBKeep.BaseLevel;
                                            break;
                                        }
                                    }
                                    //get the portal location
                                    foreach (AbstractGameKeep keep in GameServer.KeepManager.GetKeepsOfRegion(CurrentRegionID))
                                    {
                                        if (keep.DBKeep.BaseLevel > 50 && keep.Realm == Realm)
                                        {
                                            releasePosition = Position.Create((ushort)keep.Region, x: keep.X, y: keep.Y, z: keep.Z);
                                            break;
                                        }
                                    }
                                    break;
                                }
                            //nf
                            case 163:
                                {
                                    if (BindPosition.RegionID != 163)
                                    {
                                        switch (Realm)
                                        {
                                            case eRealm.Albion:
                                                {
                                                    releasePosition = GameServer.KeepManager.GetBorderKeepPosition(1);
                                                    break;
                                                }
                                            case eRealm.Midgard:
                                                {
                                                    releasePosition = GameServer.KeepManager.GetBorderKeepPosition(3);
                                                    break;
                                                }
                                            case eRealm.Hibernia:
                                                {
                                                    releasePosition = GameServer.KeepManager.GetBorderKeepPosition(5);
                                                    break;
                                                }
                                        }
                                        break;
                                    }
                                    else
                                    {
                                        releasePosition = BindPosition;
                                    }
                                    break;
                                }/*
                                //bg45-49
                            case 165:
                                {
                                    break;
                                }*/
                            default:
                                {
                                    releasePosition = BindPosition;
                                    break;
                                }
                        }
                        break;
                    }
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.YouRelease"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
            Out.SendCloseTimerWindow();
            if (m_releaseTimer != null)
            {
                m_releaseTimer.Stop();
                m_releaseTimer = null;
            }

            if (Realm != eRealm.None)
            {
                if (Level >= ServerProperties.Properties.PVE_EXP_LOSS_LEVEL)
                {
                    // actual lost exp, needed for 2nd stage deaths
                    long lostExp = Experience;
                    long lastDeathExpLoss = TempProperties.getProperty<long>(DEATH_EXP_LOSS_PROPERTY);
                    TempProperties.removeProperty(DEATH_EXP_LOSS_PROPERTY);

                    GainExperience(GameLiving.eXPSource.Other, -lastDeathExpLoss);
                    lostExp -= Experience;

                    // raise only the gravestone if xp has to be stored in it
                    if (lostExp > 0)
                    {
                        // find old gravestone of player and remove it
                        if (character.HasGravestone)
                        {
                            Region reg = WorldMgr.GetRegion((ushort)character.GravestoneRegion);
                            if (reg != null)
                            {
                                GameGravestone oldgrave = reg.FindGraveStone(this);
                                if (oldgrave != null)
                                {
                                    oldgrave.Delete();
                                }
                            }
                            character.HasGravestone = false;
                        }

                        GameGravestone gravestone = new GameGravestone(this, lostExp);
                        gravestone.AddToWorld();
                        character.GravestoneRegion = gravestone.CurrentRegionID;
                        character.HasGravestone = true;
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.GraveErected"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.ReturnToPray"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                    }
                }
            }

            if (Level >= ServerProperties.Properties.PVE_CON_LOSS_LEVEL)
            {
                int deathConLoss = TempProperties.getProperty<int>(DEATH_CONSTITUTION_LOSS_PROPERTY); // get back constitution lost at death
                if (deathConLoss > 0)
                {
                    TotalConstitutionLostAtDeath += deathConLoss;
                    Out.SendCharStatsUpdate();
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.LostConstitution"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                }
            }

            //Update health&sit state first!
            Health = MaxHealth;
            StartPowerRegeneration();
            StartEnduranceRegeneration();

            Region region = null;
            if ((region = WorldMgr.GetRegion(BindPosition.RegionID)) != null && region.GetZone(BindPosition.Coordinate) != null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.SurroundingChange"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
            }
            else
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.NoValidBindpoint"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                Bind(true);
            }

            int oldRegion = CurrentRegionID;
            
            //It is enough if we revive the player on this client only here
            //because for other players the player will be removed in the MoveTo
            //method and added back again (if in view) with full health ... so no
            //revive needed for others...
            if (oldRegion != releasePosition.RegionID)
            {
                Out.SendPlayerRevive(this);
                Out.SendUpdatePoints();
                if (m_releaseType != eReleaseType.Jail)
                    MoveTo(releasePosition);
            }
            else
            {
                //Call MoveTo after new GameGravestone(this...
                //or the GraveStone will be located at the player's bindpoint		

                if (m_releaseType != eReleaseType.Jail)
                {
                    MoveTo(releasePosition);
                }

                Out.SendPlayerRevive(this);
                //			Out.SendUpdatePlayer();
                Out.SendUpdatePoints();
            }
            //Set property indicating that we are releasing to another region; used for Released event
            if (oldRegion != CurrentRegionID)
                TempProperties.setProperty(RELEASING_PROPERTY, true);
            else
            {
                // fire the player revive event
                Notify(GamePlayerEvent.Revive, this);
                Notify(GamePlayerEvent.Released, this);
            }

            TempProperties.removeProperty(DEATH_CONSTITUTION_LOSS_PROPERTY);

            if (m_releaseType == eReleaseType.Jail)
            {
                GameEventMgr.Notify(GamePlayerEvent.SendToJail, new SendToJailEventArgs() { GamePlayer = this, OriginalReputation = this.Reputation });
                this.Reputation = 0;
            }
        }

        /// <summary>
        /// helper state var for different release phases
        /// </summary>
        private byte m_releasePhase = 0;

        /// <summary>
        /// callback every second to control realtime release
        /// </summary>
        /// <param name="callingTimer"></param>
        /// <returns></returns>
        protected virtual int ReleaseTimerCallback(RegionTimer callingTimer)
        {
            if (IsAlive)
                return 0;
            var diffToRelease = GameTimer.GetTickCount() - m_deathTick;
            if (m_automaticRelease && diffToRelease > RELEASE_MINIMUM_WAIT * 1000)
            {
                Release(m_releaseType, true);
                return 0;
            }
            diffToRelease = (RELEASE_TIME * 1000 - diffToRelease) / 1000;
            if (diffToRelease <= 0)
            {
                Release(m_releaseType, true);
                return 0;
            }
            if (m_releasePhase <= 1 && diffToRelease <= 10 && diffToRelease >= 8)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.WillReleaseIn", 10), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                m_releasePhase = 2;
            }
            if (m_releasePhase == 0 && diffToRelease <= 30 && diffToRelease >= 28)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Release.WillReleaseIn", 30), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                m_releasePhase = 1;
            }
            return 1000;
        }

        /// <summary>
        /// Death types ingame
        /// </summary>
        public enum eDeathType
        {
            PvE,
            RvR,
            PvP,
            None,
        }

        /// <summary>
        /// The current death type
        /// </summary>
        protected eDeathType m_deathtype;

        /// <summary>
        /// Gets the player's current death type.
        /// </summary>
        public eDeathType DeathType
        {
            get { return m_deathtype; }
            set { m_deathtype = value; }
        }
        /// <summary>
        /// Called when player revive
        /// </summary>
        public virtual void OnRevive(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = (GamePlayer)sender;

            if (player.IsUnderwater && player.CanBreathUnderWater == false)
                player.Diving(waterBreath.Holding);
            //We need two different sickness spells because RvR sickness is not curable by Healer NPC -Unty
            if (player.Level >= ServerProperties.Properties.RESS_SICKNESS_LEVEL)
                switch (DeathType)
                {
                    case eDeathType.RvR:
                        SpellLine rvrsick = SkillBase.GetSpellLine(GlobalSpellsLines.Realm_Spells);
                        if (rvrsick == null) return;
                        Spell rvrillness = SkillBase.FindSpell(8181, rvrsick);
                        player.CastSpell(rvrillness, rvrsick);
                        break;
                    case eDeathType.PvP: //PvP sickness is the same as PvE sickness - Curable
                    case eDeathType.PvE:
                        SpellLine pvesick = SkillBase.GetSpellLine(GlobalSpellsLines.Realm_Spells);
                        if (pvesick == null) return;
                        Spell pveillness = SkillBase.FindSpell(2435, pvesick);
                        player.CastSpell(pveillness, pvesick);
                        break;
                }

            GameEventMgr.RemoveHandler(this, GamePlayerEvent.Revive, new DOLEventHandler(OnRevive));
            m_deathtype = eDeathType.None;
        }

        /// <summary>
        /// Property that saves experience lost on last death
        /// </summary>
        public const string DEATH_EXP_LOSS_PROPERTY = "death_exp_loss";
        /// <summary>
        /// Property that saves condition lost on last death
        /// </summary>
        public const string DEATH_CONSTITUTION_LOSS_PROPERTY = "death_con_loss";
        /// <summary>
        /// Property that save mezzed state from other player to determine if player was mezz
        /// </summary>
        public const string PLAYER_MEZZED_BY_OTHER_PLAYER_ID = "player_mezzed_by_other_player";
        #endregion

        #region Praying
        /// <summary>
        /// The timer that will be started when the player wants to pray
        /// </summary>
        protected RegionTimerAction<GameGravestone> m_prayAction;
        /// <summary>
        /// The delay to wait until xp is regained, in milliseconds
        /// </summary>
        protected virtual ushort PrayDelay { get { return 5000; } }
        /// <summary>
        /// Gets the praying-state of this living
        /// </summary>
        public virtual bool IsPraying
        {
            get { return m_prayAction != null && m_prayAction.IsAlive; }
        }

        /// <summary>
        /// Prays on a gravestone for XP!
        /// </summary>
        public virtual void Pray()
        {
            string cantPrayMessage = string.Empty;
            GameGravestone gravestone = TargetObject as GameGravestone;

            if (!IsAlive)
                cantPrayMessage = "GameObjects.GamePlayer.Pray.CantPrayNow";
            else if (IsRiding)
                cantPrayMessage = "GameObjects.GamePlayer.Pray.CantPrayRiding";
            else if (gravestone == null)
                cantPrayMessage = "GameObjects.GamePlayer.Pray.NeedTarget";
            else if (!gravestone.InternalID.Equals(InternalID))
                cantPrayMessage = "GameObjects.GamePlayer.Pray.SelectGrave";
            else if (!IsWithinRadius(gravestone, 2000))
                cantPrayMessage = "GameObjects.GamePlayer.Pray.MustGetCloser";
            else if (IsMoving)
                cantPrayMessage = "GameObjects.GamePlayer.Pray.MustStandingStill";
            else if (IsPraying)
                cantPrayMessage = "GameObjects.GamePlayer.Pray.AlreadyPraying";

            if (cantPrayMessage != string.Empty)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, cantPrayMessage), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (m_prayAction != null)
                m_prayAction.Stop();

            m_prayAction = new RegionTimerAction<GameGravestone>(gravestone, stn =>
            {
                if (stn.XPValue > 0)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Pray.GainBack"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    GainExperience(eXPSource.Praying, stn.XPValue);
                }
                stn.XPValue = 0;
                stn.Delete();
            });
            m_prayAction.Start(PrayDelay);

            Sit(true);
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Pray.Begin"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null) continue;
                player.Out.SendEmoteAnimation(this, eEmote.Pray);
            }
        }

        /// <summary>
        /// Stop praying; used when player changes target
        /// </summary>
        public void PrayTimerStop()
        {
            if (!IsPraying)
                return;
            m_prayAction.Stop();
            m_prayAction = null;
        }
        #endregion

        #endregion

        #region Name/LastName/GuildName/Model

        /// <summary>
        /// The lastname of this player
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual string LastName
        {
            get { return DBCharacter != null ? DBCharacter.LastName : string.Empty; }
            set
            {
                if (DBCharacter == null) return;
                DBCharacter.LastName = value;
                //update last name for all players if client is playing
                if (ObjectState == eObjectState.Active)
                {
                    Out.SendUpdatePlayer();
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (player == null) continue;
                        if (player != this)
                        {
                            player.Out.SendObjectRemove(this);
                            player.Out.SendPlayerCreate(this);
                            player.Out.SendLivingEquipmentUpdate(this);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the guildname of this player
        /// (delegate to PlayerCharacter)
        /// </summary>
        public override string GuildName
        {
            get
            {
                if (m_guild == null)
                    return "";

                return m_guild.Name;
            }
            set
            { }
        }

        /// <summary>
        /// Checks another player modified name for this player
        /// (delegate to PlayerCharacter)
        /// </summary>
        /// <param name="player">the player to check</param>
        /// <returns>string personalized name to show to the player</returns>
        public override string GetPersonalizedName(GameObject gameObject)
        {
            if (gameObject == null)
                return "";

            if (Client == null)
            {
                return gameObject.Name;
            }

            if (gameObject is GameNPC npc)
                return npc.GetName(0, true, Client.Account.Language, npc);

            if (!(gameObject is GamePlayer player) || player == this)
                return gameObject.Name;

            if (!DOL.GS.ServerProperties.Properties.HIDE_PLAYER_NAME || player.Client.Account.PrivLevel > 1 || Client.Account.PrivLevel > 1)
                return player.Name;

            if (SerializedAskNameList.Contains(player.Name) || SerializedFriendsList.Contains(player.Name))
                return player.Name;
            // get row position in db by CreationDate
            return "(" + this.RaceToTranslatedName(player.Race, player.Gender) + " " + Client.SessionID + ")";
        }

        /// <summary>
        /// Gets or sets the name of the player
        /// (delegate to PlayerCharacter)
        /// </summary>
        public override string Name
        {
            get { return DBCharacter != null ? DBCharacter.Name : base.Name; }
            set
            {
                var oldname = base.Name;
                base.Name = value;

                if (DBCharacter != null)
                    DBCharacter.Name = value;

                if (oldname != value)
                {
                    //update name for all players if client is playing
                    if (ObjectState == eObjectState.Active)
                    {
                        Out.SendUpdatePlayer();
                        if (Group != null)
                            Out.SendGroupWindowUpdate();
                        foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        {
                            if (player == null) continue;
                            if (player != this)
                            {
                                player.Out.SendObjectRemove(this);
                                player.Out.SendPlayerCreate(this);
                                player.Out.SendLivingEquipmentUpdate(this);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets or gets the model of the player. If the player is
        /// active in the world, the modelchange will be visible
        /// (delegate to PlayerCharacter)
        /// </summary>
        /// <remarks>
        /// The model of a GamePlayer is a 16-bit unsigned integer.
        /// The leftmost 3 bits are related to hair color.
        /// The next 2 bits are for the size: 01 = short, 10 = average, 11 = tall (00 appears to be average as well)
        /// The remaining 11 bits are for the model (see monsters.csv in gamedata.mpk)
        /// </remarks>
        public override ushort Model
        {
            get
            {
                return base.Model;
            }
            set
            {
                if (base.Model == value)
                    return;
                
                base.Model = value;

                // Only GM's can persist model changes - Tolakram
                if (Client.Account.PrivLevel > (int)ePrivLevel.Player && DBCharacter != null && DBCharacter.CurrentModel != base.Model)
                {
                    DBCharacter.CurrentModel = base.Model;
                }

                if (ObjectState == eObjectState.Active)
                {
                    Notify(GamePlayerEvent.ModelChanged, this);

                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (player == null) continue;
                        player.Out.SendModelChange(this, Model);
                    }

                    RefreshQuestNPCs();
                }
            }
        }

        /// <summary>
        /// Male or Female (from DBCharacter)
        /// Note: DB Gender is 0=male, 1=female while enum is 0=neutral, 1=male, 2=female
        /// </summary>
        public override eGender Gender
        {
            get
            {
                if (DBCharacter.Gender == 0)
                {
                    return eGender.Male;
                }

                return eGender.Female;
            }
            set
            {
            }
        }


        public enum eSize : ushort
        {
            Short = 0x800,
            Average = 0x1000,
            Tall = 0x1800
        }


        public eSize Size
        {
            get
            {
                ushort size = (ushort)(Model & (ushort)eSize.Tall);

                switch (size)
                {
                    case 0x800: return eSize.Short;
                    case 0x1800: return eSize.Tall;
                    default: return eSize.Average;
                }
            }

            set
            {
                if (value != Size)
                {
                    ushort modelID = (ushort)(Model & 0x7FF);

                    Model = (ushort)(modelID | (ushort)value);
                }
            }
        }

        #endregion

        #region Stats

        /// <summary>
        /// Holds if the player can gain a FreeLevel
        /// </summary>
        public virtual byte FreeLevelState
        {
            get
            {
                int freelevel_days = 7;
                switch (Realm)
                {
                    case eRealm.Albion:
                        if (ServerProperties.Properties.FREELEVEL_DAYS_ALBION == -1)
                            return 1;
                        else
                            freelevel_days = ServerProperties.Properties.FREELEVEL_DAYS_ALBION;
                        break;
                    case eRealm.Midgard:
                        if (ServerProperties.Properties.FREELEVEL_DAYS_MIDGARD == -1)
                            return 1;
                        else
                            freelevel_days = ServerProperties.Properties.FREELEVEL_DAYS_MIDGARD;
                        break;
                    case eRealm.Hibernia:
                        if (ServerProperties.Properties.FREELEVEL_DAYS_HIBERNIA == -1)
                            return 1;
                        else
                            freelevel_days = ServerProperties.Properties.FREELEVEL_DAYS_HIBERNIA;
                        break;
                }

                //flag 1 = above level, 2 = elligable, 3= time until, 4 = level and time until, 5 = level until
                if (Level >= 48)
                    return 1;

                TimeSpan t = new TimeSpan((long)(DateTime.Now.Ticks - LastFreeLeveled.Ticks));
                if (t.Days >= freelevel_days)
                {
                    if (Level >= LastFreeLevel + 2)
                        return 2;
                    else return 5;
                }
                else
                {
                    if (Level >= LastFreeLevel + 2)
                        return 3;
                    else return 4;
                }
            }
        }

        /// <summary>
        /// Gets/sets the player efficacy percent
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual int TotalConstitutionLostAtDeath
        {
            get { return DBCharacter != null ? DBCharacter.ConLostAtDeath : 0; }
            set { if (DBCharacter != null) DBCharacter.ConLostAtDeath = value; }
        }

        /// <summary>
        /// Change a stat value
        /// (delegate to PlayerCharacter)
        /// </summary>
        /// <param name="stat">The stat to change</param>
        /// <param name="val">The new value</param>
        public override void ChangeBaseStat(eStat stat, short val)
        {
            int oldstat = GetBaseStat(stat);
            base.ChangeBaseStat(stat, val);
            int newstat = GetBaseStat(stat);
            DOLCharacters character = DBCharacter; // to call it only once, if in future there will be some special code to get the character
                                                   // Graveen: always positive and not null. This allows /player stats to substract values safely
            if (newstat < 1) newstat = 1;
            if (character != null && oldstat != newstat)
            {
                switch (stat)
                {
                    case eStat.STR: character.Strength = newstat; break;
                    case eStat.DEX: character.Dexterity = newstat; break;
                    case eStat.CON: character.Constitution = newstat; break;
                    case eStat.QUI: character.Quickness = newstat; break;
                    case eStat.INT: character.Intelligence = newstat; break;
                    case eStat.PIE: character.Piety = newstat; break;
                    case eStat.EMP: character.Empathy = newstat; break;
                    case eStat.CHR: character.Charisma = newstat; break;
                }
            }
        }

        /// <summary>
        /// Gets player's constitution
        /// </summary>
        public int Constitution
        {
            get { return GetModified(eProperty.Constitution); }
        }

        /// <summary>
        /// Gets player's dexterity
        /// </summary>
        public int Dexterity
        {
            get { return GetModified(eProperty.Dexterity); }
        }

        /// <summary>
        /// Gets player's strength
        /// </summary>
        public int Strength
        {
            get { return GetModified(eProperty.Strength); }
        }

        /// <summary>
        /// Gets player's quickness
        /// </summary>
        public int Quickness
        {
            get { return GetModified(eProperty.Quickness); }
        }

        /// <summary>
        /// Gets player's intelligence
        /// </summary>
        public int Intelligence
        {
            get { return GetModified(eProperty.Intelligence); }
        }

        /// <summary>
        /// Gets player's piety
        /// </summary>
        public int Piety
        {
            get { return GetModified(eProperty.Piety); }
        }

        /// <summary>
        /// Gets player's empathy
        /// </summary>
        public int Empathy
        {
            get { return GetModified(eProperty.Empathy); }
        }

        /// <summary>
        /// Gets player's charisma
        /// </summary>
        public int Charisma
        {
            get { return GetModified(eProperty.Charisma); }
        }

        protected IPlayerStatistics m_statistics = null;

        /// <summary>
        /// Get the statistics for this player
        /// </summary>
        public virtual IPlayerStatistics Statistics
        {
            get { return m_statistics; }
        }

        /// <summary>
        /// Create played statistics for this player
        /// </summary>
        public virtual void CreateStatistics()
        {
            m_statistics = new PlayerStatistics(this);
        }

        /// <summary>
        /// Formats this players statistics.
        /// </summary>
        /// <returns>List of strings.</returns>
        public virtual IList<string> FormatStatistics()
        {
            return GameServer.ServerRules.FormatPlayerStatistics(this);
        }

        #endregion

        #region Health/Mana/Endurance/Regeneration

        /// <summary>
        /// Starts the health regeneration.
        /// Overriden. No lazy timers for GamePlayers.
        /// </summary>
        public override void StartHealthRegeneration()
        {
            if (!IsAlive || ObjectState != eObjectState.Active) return;
            if (m_healthRegenerationTimer.IsAlive) return;
            m_healthRegenerationTimer.Start(m_healthRegenerationPeriod);
        }
        /// <summary>
        /// Starts the power regeneration.
        /// Overriden. No lazy timers for GamePlayers.
        /// </summary>
        public override void StartPowerRegeneration()
        {
            if (!IsAlive || ObjectState != eObjectState.Active) return;
            if (m_powerRegenerationTimer.IsAlive) return;
            m_powerRegenerationTimer.Start(m_powerRegenerationPeriod);
        }
        /// <summary>
        /// Starts the endurance regeneration.
        /// Overriden. No lazy timers for GamePlayers.
        /// </summary>
        public override void StartEnduranceRegeneration()
        {
            if (!IsAlive || ObjectState != eObjectState.Active) return;
            if (m_enduRegenerationTimer.IsAlive) return;
            m_enduRegenerationTimer.Start(m_enduranceRegenerationPeriod);
        }
        /// <summary>
        /// Stop the health regeneration.
        /// Overriden. No lazy timers for GamePlayers.
        /// </summary>
        public override void StopHealthRegeneration()
        {
            if (m_healthRegenerationTimer == null) return;
            m_healthRegenerationTimer.Stop();
        }
        /// <summary>
        /// Stop the power regeneration.
        /// Overriden. No lazy timers for GamePlayers.
        /// </summary>
        public override void StopPowerRegeneration()
        {
            if (m_powerRegenerationTimer == null) return;
            m_powerRegenerationTimer.Stop();
        }
        /// <summary>
        /// Stop the endurance regeneration.
        /// Overriden. No lazy timers for GamePlayers.
        /// </summary>
        public override void StopEnduranceRegeneration()
        {
            if (m_enduRegenerationTimer == null) return;
            m_enduRegenerationTimer.Stop();
        }

        /// <summary>
        /// Override HealthRegenTimer because if we are not connected anymore
        /// we DON'T regenerate health, even if we are not garbage collected yet!
        /// </summary>
        /// <param name="callingTimer">the timer</param>
        /// <returns>the new time</returns>
        protected override int HealthRegenerationTimerCallback(RegionTimer callingTimer)
        {
            // I'm not sure what the point of this is.
            if (Client.ClientState != GameClient.eClientState.Playing)
                return HealthRegenerationPeriod;

            // adjust timer based on Live testing of player
            if (Health < MaxHealth)
            {
                int regenRate = GetModified(eProperty.HealthRegenerationRate) + GetModified(eProperty.MythicalOmniRegen);

                if (IsSitting && !InCombat)
                    regenRate = (int)Math.Round(regenRate * 1.6);

                regenRate = (int)Math.Round(regenRate * Properties.HEALTH_REGEN_RATE);

                ChangeHealth(this, eHealthChangeType.Regenerate, regenRate);

                #region PVP DAMAGE

                if (DamageRvRMemory > 0 && GetLivingOwner() != null)
                    DamageRvRMemory -= (long)Math.Max(regenRate, 0);

                #endregion PVP DAMAGE
            }

            //If we are fully healed, we stop the timer
            if (Health >= MaxHealth)
            {

                #region PVP DAMAGE

                // Fully Regenerated, Set DamageRvRMemory to 0
                if (DamageRvRMemory > 0)
                    DamageRvRMemory = 0;

                #endregion PVP DAMAGE

                //We clean all damagedealers if we are fully healed,
                //no special XP calculations need to be done
                XPGainers.Clear();

                return 0;
            }

            if (InCombat)
            {
                // in combat each tic is aprox 6 seconds - tolakram
                return HealthRegenerationPeriod * 2;
            }

            //Heal at standard rate
            return HealthRegenerationPeriod;
        }

        /// <summary>
        /// Override PowerRegenTimer because if we are not connected anymore
        /// we DON'T regenerate mana, even if we are not garbage collected yet!
        /// </summary>
        /// <param name="selfRegenerationTimer">the timer</param>
        /// <returns>the new time</returns>
        protected override int PowerRegenerationTimerCallback(RegionTimer selfRegenerationTimer)
        {
            if (Client.ClientState != GameClient.eClientState.Playing)
                return PowerRegenerationPeriod;

            int interval = base.PowerRegenerationTimerCallback(selfRegenerationTimer);
            return interval;
        }

        /// <summary>
        /// Override EnduranceRegenTimer because if we are not connected anymore
        /// we DON'T regenerate endurance, even if we are not garbage collected yet!
        /// </summary>
        /// <param name="selfRegenerationTimer">the timer</param>
        /// <returns>the new time</returns>
        protected override int EnduranceRegenerationTimerCallback(RegionTimer selfRegenerationTimer)
        {
            if (Client.ClientState != GameClient.eClientState.Playing)
                return EnduranceRegenerationPeriod;

            bool sprinting = IsSprinting;

            if (Endurance < MaxEndurance || sprinting)
            {
                int regen = GetModified(eProperty.EnduranceRegenerationRate) + GetModified(eProperty.MythicalOmniRegen);  //default is 1
                int endchant = GetModified(eProperty.FatigueConsumption);      //Pull chant/buff value

                int longwind = 5;
                if (sprinting && IsMoving)
                {
                    //TODO : cache LongWind level when char is loaded and on train ability
                    LongWindAbility ra = GetAbility<LongWindAbility>();
                    if (ra != null)
                        longwind = 5 - (ra.GetAmountForLevel(CalculateSkillLevel(ra)) * 5 / 100);

                    regen -= longwind;

                    if (endchant > 1) regen = (int)Math.Ceiling(regen * endchant * 0.01);
                    if (Endurance + regen > MaxEndurance - longwind)
                    {
                        regen -= (Endurance + regen) - (MaxEndurance - longwind);
                    }
                }

                if (regen != 0)
                {
                    if (IsSitting && !InCombat)
                        regen *= 3;

                    regen = (int)Math.Round(regen * Properties.ENDURANCE_REGEN_RATE);

                    ChangeEndurance(this, eEnduranceChangeType.Regenerate, regen);
                }
            }
            if (!sprinting)
            {
                if (Endurance >= MaxEndurance) return 0;
            }
            else
            {
                long lastmove = TempProperties.getProperty<long>(PlayerPositionUpdateHandler.LASTMOVEMENTTICK);
                if ((lastmove > 0 && lastmove + 5000 < CurrentRegion.Time) //cancel sprint after 5sec without moving?
                    || Endurance - 5 <= 0)
                    Sprint(false);
            }

            return 500 + Util.Random(EnduranceRegenerationPeriod);
        }

        /// <summary>
        /// Gets/sets the object health
        /// </summary>
        public override int Health
        {
            get { return DBCharacter != null ? DBCharacter.Health : base.Health; }
            set
            {
                value = value.Clamp(0, MaxHealth);
                //If it is already set, don't do anything
                if (Health == value)
                {
                    base.Health = value; //needed to start regeneration
                    return;
                }

                int oldPercent = HealthPercent;
                if (DBCharacter != null)
                    DBCharacter.Health = value;
                base.Health = value;
                if (oldPercent != HealthPercent)
                {
                    if (Group != null)
                        Group.UpdateMember(this, false, false);
                    UpdatePlayerStatus();
                }
            }
        }

        /// <summary>
        /// Calculates the maximum health for a specific playerlevel and constitution
        /// </summary>
        /// <param name="level">The level of the player</param>
        /// <param name="constitution">The constitution of the player</param>
        /// <returns></returns>
        public virtual int CalculateMaxHealth(int level, int constitution)
        {
            constitution -= 50;
            if (constitution < 0)
                constitution *= 2;

            // hp1 : from level
            // hp2 : from constitution
            // hp3 : from champions level
            // hp4 : from artifacts such Spear of Kings charge
            int hp1 = CharacterClass.BaseHP * level;
            int hp2 = hp1 * constitution / 10000;
            int hp3 = 0;
            if (ChampionLevel >= 1)
                hp3 = ServerProperties.Properties.HPS_PER_CHAMPIONLEVEL * ChampionLevel;
            double hp4 = 20 + hp1 / 50 + hp2 + hp3;
            var extra = GetModified(eProperty.ExtraHP);
            if (extra > 0)
                hp4 += Math.Round(hp4 * extra / 100);

            return Math.Max(1, (int)hp4);
        }

        public override byte HealthPercentGroupWindow
        {
            get
            {
                return CharacterClass.HealthPercentGroupWindow;
            }
        }

        /// <summary>
        /// Calculate max mana for this player based on level and mana stat level
        /// </summary>
        /// <param name="level"></param>
        /// <param name="manaStat"></param>
        /// <returns></returns>
        public virtual int CalculateMaxMana(int level, int manaStat)
        {
            int maxpower = 0;

            //Special handling for Vampiirs:
            /* There is no stat that affects the Vampiir's power pool or the damage done by its power based spells.
             * The Vampiir is not a focus based class like, say, an Enchanter.
             * The Vampiir is a lot more cut and dried than the typical casting class.
             * EDIT, 12/13/04 - I was told today that this answer is not entirely accurate.
             * While there is no stat that affects the damage dealt (in the way that intelligence or piety affects how much damage a more traditional caster can do),
             * the Vampiir's power pool capacity is intended to be increased as the Vampiir's strength increases.
             *
             * This means that strength ONLY affects a Vampiir's mana pool
             * 
             * http://www.camelotherald.com/more/1913.shtml
             * Strength affects the amount of damage done by spells in all of the Vampiir's spell lines.
             * The amount of said affecting was recently increased slightly (fixing a bug), and that minor increase will go live in 1.74 next week.
             * 
             * Strength ALSO affects the size of the power pool for a Vampiir sort of.
             * Your INNATE strength (the number of attribute points your character has for strength) has no effect at all.
             * Extra points added through ITEMS, however, does increase the size of your power pool.

             */
            if (CharacterClass.ManaStat != eStat.UNDEFINED || CharacterClass.ID == (int)eCharacterClass.Vampiir)
            {
                maxpower = Math.Max(5, (level * 5) + (manaStat - 50));
            }
            else if (CharacterClass.ManaStat == eStat.UNDEFINED && Champion && ChampionLevel > 0)
            {
                maxpower = 100; // This is a guess, need feedback
            }

            if (maxpower < 0)
                maxpower = 0;

            return maxpower;
        }


        /// <summary>
        /// Gets/sets the object mana
        /// </summary>
        public override int Mana
        {
            get { return DBCharacter != null ? DBCharacter.Mana : base.Mana; }
            set
            {
                value = Math.Min(value, MaxMana);
                value = Math.Max(value, 0);
                //If it is already set, don't do anything
                if (Mana == value)
                {
                    base.Mana = value; //needed to start regeneration
                    return;
                }
                int oldPercent = ManaPercent;
                base.Mana = value;
                if (DBCharacter != null)
                    DBCharacter.Mana = value;
                if (oldPercent != ManaPercent)
                {
                    if (Group != null)
                        Group.UpdateMember(this, false, false);
                    UpdatePlayerStatus();
                }
            }
        }

        /// <summary>
        /// Gets/sets the object max mana
        /// </summary>
        public override int MaxMana
        {
            get { return GetModified(eProperty.MaxMana); }
        }

        /// <summary>
        /// Gets/sets the object endurance
        /// </summary>
        public override int Endurance
        {
            get { return DBCharacter != null ? DBCharacter.Endurance : base.Endurance; }
            set
            {
                value = Math.Min(value, MaxEndurance);
                value = Math.Max(value, 0);
                //If it is already set, don't do anything
                if (Endurance == value)
                {
                    base.Endurance = value; //needed to start regeneration
                    return;
                }
                else if (IsAlive && value < MaxEndurance)
                    StartEnduranceRegeneration();
                int oldPercent = EndurancePercent;
                base.Endurance = value;
                if (DBCharacter != null)
                    DBCharacter.Endurance = value;
                if (oldPercent != EndurancePercent)
                {
                    //ogre: 1.69+ endurance is displayed on group window
                    if (Group != null)
                        Group.UpdateMember(this, false, false);
                    //end ogre
                    UpdatePlayerStatus();
                }
            }
        }

        /// <summary>
        /// Gets/sets the objects maximum endurance
        /// </summary>
        public override int MaxEndurance
        {
            get { return GetModified(eProperty.Fatigue); }
            set
            {
                //If it is already set, don't do anything
                if (MaxEndurance == value)
                    return;
                base.MaxEndurance = value;
                DBMaxEndurance = value;
                UpdatePlayerStatus();
            }
        }

        /// <inheritdoc />
        public override int Tension
        {
            get => DBCharacter != null ? DBCharacter.Tension : base.Tension;
            set
            {
                if (base.Tension == value) // No change, ignore
                {
                    return;
                }
                base.Tension = value;
                DBCharacter.Tension = base.Tension;
            }
        }

        public bool IsPlayerGreyCon(GamePlayer target)
        {
            int killerLevel = this.Level;
            int targetLevel = target.Level;

            if (killerLevel < GameLiving.NoXPForLevel.Length)
            {
                return targetLevel <= GameLiving.NoXPForLevel[killerLevel];
            }
            else
            {
                return this.GetConLevel(target) <= -3;
            }
        }

        protected override void GainTension(AttackData ad)
        {
            if (ad.Attacker == null || MaxTension <= 0)
            {
                return;
            }

            if (ad.Attacker is GamePlayer attackerPlayer && IsPlayerGreyCon(attackerPlayer))
            {
                return;
            }

            lock (EffectList)
            {
                // Players under Resurrection sickness, Damnation or Reanimate Corpse cannot gain tension
                if (EffectList.Any(e => e is GameSpellEffect { SpellHandler: AbstractIllnessSpellHandler or DamnationSpellHandler or SummonMonster }))
                {
                    return;
                }
            }

            int conLevel = (int)GetConLevel(ad.Attacker as GameLiving);
            float tension;
            float server_rate;

            if (ad.Attacker is GamePlayer)
            {
                tension = conLevel switch
                {
                    <= -3 => 0,
                    <= -2 => 15,
                    <= -1 => 35,
                    <= 0 => 75,
                    <= 1 => 100,
                    <= 2 => 150,
                    >= 3 => 200
                };

                server_rate = (float)Properties.PVP_TENSION_RATE;
            }
            else
            {
                tension = conLevel switch
                {
                    <= -3 => 0,
                    <= -2 => 30,
                    <= -1 => 70,
                    <= 0 => 150,
                    <= 1 => 200,
                    <= 2 => 300,
                    >= 3 => 400
                };

                server_rate = (float)Properties.PVE_TENSION_RATE;
            }

            float rate = (1.00f + ((float)GetModified(eProperty.MythicalTension)) / 100);

            if (rate < 0.0f)
            {
                return;
            }

            if (IsRenaissance)
            {
                rate *= 1.10f;
            }

            double guildBuffMultiplier = Guild?.GetBonusMultiplier(Guild.eBonusType.Tension) ?? 1.0;

            eArmorSlot hitLocation = ad.ArmorHitLocation;
            float armorMultiplier = hitLocation switch
            {
                eArmorSlot.TORSO => 0.9f,
                eArmorSlot.LEGS => 0.8f,
                eArmorSlot.ARMS => 0.6f,
                eArmorSlot.HEAD => 0.7f,
                eArmorSlot.HAND => 0.4f,
                eArmorSlot.FEET => 0.5f,
                _ => 1.0f
            };

            Tension += (int)Math.Round(server_rate * tension * armorMultiplier * ad.TensionRate * rate * CurrentZone.TensionRate * guildBuffMultiplier);
        }

        /// <summary>
        /// Gets the concentration left
        /// </summary>
        public override int Concentration
        {
            get { return MaxConcentration - ConcentrationEffects.UsedConcentration; }
        }

        /// <summary>
        /// Gets the maximum concentration for this player
        /// </summary>
        public override int MaxConcentration
        {
            get { return GetModified(eProperty.MaxConcentration); }
        }

        #endregion

        #region Class/Race

        /// <summary>
        /// Gets/sets the player's race name
        /// </summary>
        public virtual string RaceName
        {
            get { return this.RaceToTranslatedName(Race, Gender); }
        }

        /// <summary>
        /// Gets or sets this player's race id
        /// (delegate to DBCharacter)
        /// </summary>
        public override short Race
        {
            get { return (short)(DBCharacter != null ? DBCharacter.Race : 0); }
            set { if (DBCharacter != null) DBCharacter.Race = value; }
        }

        /// <summary>
        /// Players class
        /// </summary>
        protected ICharacterClass m_characterClass;

        /// <summary>
        /// Gets the player's character class
        /// </summary>
        public virtual ICharacterClass CharacterClass
        {
            get { return m_characterClass; }
        }

        /// <summary>
        /// Set the character class to a specific one
        /// </summary>
        /// <param name="id">id of the character class</param>
        /// <returns>success</returns>
        public virtual bool SetCharacterClass(int id)
        {
            ICharacterClass cl = ScriptMgr.FindCharacterClass(id);

            if (cl == null)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("No CharacterClass with ID {0} found", id);
                return false;
            }

            m_characterClass = cl;
            m_characterClass.Init(this);

            DBCharacter.Class = m_characterClass.ID;

            float maxTension = Properties.PLAYER_BASE_MAXTENSION;
            Race race = GameServer.Database.SelectObject<Race>(DB.Column(nameof(Database.Race.ID)).IsEqualTo(Race));
            if (race?.MaxTensionFactor != null)
            {
                maxTension *= race.MaxTensionFactor.Value;
            }
            maxTension *= CharacterClass.MaxTensionFactor;
            DBCharacter.MaxTension = (int)maxTension;
            MaxTension = DBCharacter.MaxTension;
            AdrenalineSpell = CharacterClass.AdrenalineSpell;

            CounterAttackStyle = SkillBase.GetStyleList("CounterAttack", m_characterClass.ID).FirstOrDefault();
            if (CounterAttackStyle == null)
            {
                var whereClause = DB.Column("SpecKeyName").IsEqualTo("CounterAttack").And(DB.Column("ClassId").IsEqualTo(m_characterClass.ID));
                var dbStyle = GameServer.Database.SelectObject<DBStyle>(whereClause);
                CounterAttackStyle = dbStyle == null ? null : new Style(dbStyle);
            }

            if (Group != null)
            {
                Group.UpdateMember(this, false, true);
            }
            return true;
        }

        /// <summary>
        /// Hold all player face custom attibutes
        /// </summary>
        protected byte[] m_customFaceAttributes = new byte[(int)eCharFacePart._Last + 1];

        /// <summary>
        /// Get the character face attribute you want
        /// </summary>
        /// <param name="part">face part</param>
        /// <returns>attribute</returns>
        public byte GetFaceAttribute(eCharFacePart part)
        {
            return m_customFaceAttributes[(int)part];
        }

        #endregion

        #region Spells/Skills/Abilities/Effects

        /// <summary>
        /// Holds the player choosen list of Realm Abilities.
        /// </summary>
        protected readonly ReaderWriterList<RealmAbility> m_realmAbilities = new ReaderWriterList<RealmAbility>();

        /// <summary>
        /// Holds the player specializable skills and style lines
        /// (KeyName -> Specialization)
        /// </summary>
        protected readonly Dictionary<string, Specialization> m_specialization = new Dictionary<string, Specialization>();

        /// <summary>
        /// Holds the Spell lines the player can use
        /// </summary>
        protected readonly List<SpellLine> m_spellLines = new List<SpellLine>();

        /// <summary>
        /// Object to use when locking the SpellLines list
        /// </summary>
        protected readonly Object lockSpellLinesList = new Object();

        /// <summary>
        /// Holds all styles of the player
        /// </summary>
        protected readonly Dictionary<int, Style> m_styles = new Dictionary<int, Style>();

        /// <summary>
        /// Used to lock the style list
        /// </summary>
        protected readonly Object lockStyleList = new Object();

        /// <summary>
        /// Temporary Stats Boni
        /// </summary>
        protected readonly int[] m_statBonus = new int[8];

        /// <summary>
        /// Temporary Stats Boni in percent
        /// </summary>
        protected readonly int[] m_statBonusPercent = new int[8];

        /// <summary>
        /// Gets/Sets amount of full skill respecs
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual int RespecAmountAllSkill
        {
            get { return DBCharacter != null ? DBCharacter.RespecAmountAllSkill : 0; }
            set { if (DBCharacter != null) DBCharacter.RespecAmountAllSkill = value; }
        }

        /// <summary>
        /// Gets/Sets amount of single-line respecs
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual int RespecAmountSingleSkill
        {
            get { return DBCharacter != null ? DBCharacter.RespecAmountSingleSkill : 0; }
            set { if (DBCharacter != null) DBCharacter.RespecAmountSingleSkill = value; }
        }

        /// <summary>
        /// Gets/Sets amount of realm skill respecs
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual int RespecAmountRealmSkill
        {
            get { return DBCharacter != null ? DBCharacter.RespecAmountRealmSkill : 0; }
            set { if (DBCharacter != null) DBCharacter.RespecAmountRealmSkill = value; }
        }

        /// <summary>
        /// Gets/Sets amount of DOL respecs
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual int RespecAmountDOL
        {
            get { return DBCharacter != null ? DBCharacter.RespecAmountDOL : 0; }
            set { if (DBCharacter != null) DBCharacter.RespecAmountDOL = value; }
        }

        /// <summary>
        /// Gets/Sets level respec usage flag
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual bool IsLevelRespecUsed
        {
            get { return DBCharacter != null ? DBCharacter.IsLevelRespecUsed : true; }
            set { if (DBCharacter != null) DBCharacter.IsLevelRespecUsed = value; }
        }


        protected static readonly int[] m_numRespecsCanBuyOnLevel =
        {
            1,1,1,1,1, //1-5
            2,2,2,2,2,2,2, //6-12
            3,3,3,3, //13-16
            4,4,4,4,4,4, //17-22
            5,5,5,5,5, //23-27
            6,6,6,6,6,6, //28-33
            7,7,7,7,7, //34-38
            8,8,8,8,8,8, //39-44
            9,9,9,9,9, //45-49
            10 //50
        };


        /// <summary>
        /// Can this player buy a respec?
        /// </summary>
        public virtual bool CanBuyRespec
        {
            get
            {
                return (RespecBought < m_numRespecsCanBuyOnLevel[Level - 1]);
            }
        }

        /// <summary>
        /// Gets/Sets amount of bought respecs
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual int RespecBought
        {
            get { return DBCharacter != null ? DBCharacter.RespecBought : 0; }
            set { if (DBCharacter != null) DBCharacter.RespecBought = value; }
        }


        protected static readonly int[] m_respecCost =
        {
            1,2,3, //13
            2,5,9, //14
            3,9,17, //15
            6,16,30, //16
            10,26,48,75, //17
            16,40,72,112, //18
            22,56,102,159, //19
            31,78,140,218, //20
            41,103,187,291, //21
            54,135,243,378, //22
            68,171,308,480,652, //23
            85,214,385,600,814, //24
            105,263,474,738,1001, //25
            128,320,576,896,1216, //26
            153,383,690,1074,1458, //27
            182,455,820,1275,1731,2278, //28
            214,535,964,1500,2036,2679, //29
            250,625,1125,1750,2375,3125, //30
            289,723,1302,2025,2749,3617, //31
            332,831,1497,2329,3161,4159, //32
            380,950,1710,2661,3612,4752, //33
            432,1080,1944,3024,4104,5400,6696, //34
            488,1220,2197,3417,4638,6103,7568, //35
            549,1373,2471,3844,5217,6865,8513, //36
            615,1537,2767,4305,5843,7688,9533, //37
            686,1715,3087,4802,6517,8575,10633, //38
            762,1905,3429,5335,7240,9526,11813,14099, //39
            843,2109,3796,5906,8015,10546,13078,15609, //40
            930,2327,4189,6516,8844,11637,14430,17222, //41
            1024,2560,4608,7168,9728,1280,15872,18944, //42
            1123,2807,5053,7861,10668,14037,17406,20776, //43
            1228,3070,5527,8597,11668,15353,19037,22722, //44
            1339,3349,6029,9378,12725,16748,20767,24787,28806, //45
            1458,3645,6561,10206,13851,18225,22599,26973,31347, //46
            1582,3957,7123,11080,15037,19786,24535,29283,34032, //47
            1714,4286,7716,12003,16290,21434,26578,31722,36867, //48
            1853,4634,8341,12976,17610,23171,28732,34293,39854, //49
            2000,5000,9000,14000,19000,25000,31000,37000,43000,50000 //50
        };


        /// <summary>
        /// How much does this player have to pay for a respec?
        /// </summary>
        public virtual long RespecCost
        {
            get
            {
                if (Level <= 12) //1-12
                    return m_respecCost[0];

                if (CanBuyRespec)
                {
                    int t = 0;
                    for (int i = 13; i < Level; i++)
                    {
                        t += m_numRespecsCanBuyOnLevel[i - 1];
                    }

                    return m_respecCost[t + RespecBought];
                }

                return -1;
            }
        }

        /// <summary>
        /// give player a new Specialization or improve existing one
        /// </summary>
        /// <param name="skill"></param>
        public void AddSpecialization(Specialization skill)
        {
            AddSpecialization(skill, true);
        }


        /// <summary>
        /// give player a new Specialization or improve existing one
        /// </summary>
        /// <param name="skill"></param>
        protected virtual void AddSpecialization(Specialization skill, bool notify)
        {
            if (skill == null)
                return;

            lock (((ICollection)m_specialization).SyncRoot)
            {
                // search for existing key
                if (!m_specialization.ContainsKey(skill.KeyName))
                {
                    // Adding
                    m_specialization.Add(skill.KeyName, skill);

                    if (skill.KeyName == Specs.Stealth)
                        CanStealth = true;

                    if (notify)
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.AddSpecialisation.YouLearn", skill.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                }
                else
                {
                    // Updating
                    m_specialization[skill.KeyName].Level = skill.Level;
                }
            }
        }

        /// <summary>
        /// Removes the existing specialization from the player
        /// </summary>
        /// <param name="specKeyName">The spec keyname to remove</param>
        /// <returns>true if removed</returns>
        public virtual bool RemoveSpecialization(string specKeyName)
        {
            Specialization playerSpec = null;

            lock (((ICollection)m_specialization).SyncRoot)
            {
                if (!m_specialization.TryGetValue(specKeyName, out playerSpec))
                    return false;

                m_specialization.Remove(specKeyName);

                if (specKeyName == Specs.Stealth)
                    CanStealth = false;
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RemoveSpecialization.YouLose", playerSpec.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            return true;
        }

        /// <summary>
        /// Removes the existing spellline from the player, the line instance should be called with GameObjects.GamePlayer.GetSpellLine ONLY and NEVER SkillBase.GetSpellLine!!!!!
        /// </summary>
        /// <param name="line">The spell line to remove</param>
        /// <returns>true if removed</returns>
        protected virtual bool RemoveSpellLine(SpellLine line)
        {
            lock (lockSpellLinesList)
            {
                if (!m_spellLines.Contains(line))
                {
                    return false;
                }

                m_spellLines.Remove(line);
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RemoveSpellLine.YouLose", line.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            return true;
        }

        /// <summary>
        /// Removes the existing specialization from the player
        /// </summary>
        /// <param name="lineKeyName">The spell line keyname to remove</param>
        /// <returns>true if removed</returns>
        public virtual bool RemoveSpellLine(string lineKeyName)
        {
            SpellLine line = GetSpellLine(lineKeyName);
            if (line == null)
                return false;

            return RemoveSpellLine(line);
        }

        /// <summary>
        /// Reset this player to level 1, respec all skills, remove all spec points, and reset stats
        /// </summary>
        public virtual void Reset()
        {
            byte originalLevel = Level;
            Level = 1;
            Experience = 0;
            RespecAllLines();

            // Increase the concentration for class: Cleric, Minstrel, Healer, Shaman, Druid and Bard
            DBCharacter.Concentration = 100;
            switch (DBCharacter.Class)
            {
                case 6:
                case 26:
                case 47:
                    DBCharacter.Concentration = (int)(DBCharacter.Concentration * 1.25);
                    break;
                case 4:
                case 28:
                case 48:
                    DBCharacter.Concentration = (int)(DBCharacter.Concentration * 1.1);
                    break;
                default:
                    break;
            }

            if (Level < originalLevel && originalLevel > 5)
            {
                for (int i = 6; i <= originalLevel; i++)
                {
                    if (CharacterClass.PrimaryStat != eStat.UNDEFINED)
                    {
                        ChangeBaseStat(CharacterClass.PrimaryStat, -1);
                    }
                    if (CharacterClass.SecondaryStat != eStat.UNDEFINED && ((i - 6) % 2 == 0))
                    {
                        ChangeBaseStat(CharacterClass.SecondaryStat, -1);
                    }
                    if (CharacterClass.TertiaryStat != eStat.UNDEFINED && ((i - 6) % 3 == 0))
                    {
                        ChangeBaseStat(CharacterClass.TertiaryStat, -1);
                    }
                }
            }

            CharacterClass.OnLevelUp(this, originalLevel);
        }

        public virtual bool RespecAll()
        {
            if (RespecAllLines())
            {
                // Wipe skills and styles.
                RespecAmountAllSkill--; // Decriment players respecs available.
                if (Level == 5)
                    IsLevelRespecUsed = true;

                return true;
            }

            return false;
        }

        public virtual bool RespecDOL()
        {
            if (RespecAllLines()) // Wipe skills and styles.
            {
                RespecAmountDOL--; // Decriment players respecs available.
                return true;
            }

            return false;
        }

        public virtual int RespecSingle(Specialization specLine)
        {
            int specPoints = RespecSingleLine(specLine); // Wipe skills and styles.
            if (!ServerProperties.Properties.FREE_RESPEC)
                RespecAmountSingleSkill--; // Decriment players respecs available.
            if (Level == 20 || Level == 40)
            {
                IsLevelRespecUsed = true;
            }
            return specPoints;
        }

        public virtual bool RespecRealm()
        {
            bool any = m_realmAbilities.Count > 0;

            foreach (Ability ab in m_realmAbilities)
                RemoveAbility(ab.KeyName);

            m_realmAbilities.Clear();
            if (!ServerProperties.Properties.FREE_RESPEC)
                RespecAmountRealmSkill--;
            return any;
        }

        protected virtual bool RespecAllLines()
        {
            bool ok = false;
            IList<Specialization> specList = GetSpecList().Where(e => e.Trainable).ToList();
            foreach (Specialization cspec in specList)
            {
                if (cspec.Level < 2)
                    continue;
                RespecSingleLine(cspec);
                ok = true;
            }
            return ok;
        }

        /// <summary>
        /// Respec single line
        /// </summary>
        /// <param name="specLine">spec line being respec'd</param>
        /// <returns>Amount of points spent in that line</returns>
        protected virtual int RespecSingleLine(Specialization specLine)
        {
            int specPoints = (specLine.Level * (specLine.Level + 1) - 2) / 2;
            // Graveen - autotrain 1.87
            specPoints -= GetAutoTrainPoints(specLine, 0);

            //setting directly the autotrain points in the spec
            if (GetAutoTrainPoints(specLine, 4) == 1 && Level >= 8)
            {
                specLine.Level = (int)Math.Floor((double)Level / 4);
            }
            else specLine.Level = 1;

            // If BD subpet spells scaled and capped by BD spec, respecing a spell line
            //	requires re-scaling the spells for all subpets from that line.
            if (CharacterClass is CharacterClassBoneDancer
                && DOL.GS.ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL > 0
                && DOL.GS.ServerProperties.Properties.PET_CAP_BD_MINION_SPELL_SCALING_BY_SPEC
                && ControlledBody is GamePet pet && pet.ControlledNpcList != null)
                foreach (ABrain subBrain in pet.ControlledNpcList)
                    if (subBrain != null && subBrain.Body is BDSubPet subPet && subPet.PetSpecLine == specLine.KeyName)
                        subPet.SortSpells();

            return specPoints;
        }

        /// <summary>
        /// Send this players trainer window
        /// </summary>
        public virtual void SendTrainerWindow()
        {
            Out.SendTrainerWindow();
        }

        /// <summary>
        /// returns a list with all specializations
        /// in the order they were added
        /// </summary>
        /// <returns>list of Spec's</returns>
        public virtual IList<Specialization> GetSpecList()
        {
            List<Specialization> list;

            lock (((ICollection)m_specialization).SyncRoot)
            {
                // sort by Level and ID to simulate "addition" order... (try to sort your DB if you want to change this !)
                list = m_specialization.Select(item => item.Value).OrderBy(it => it.LevelRequired).ThenBy(it => it.ID).ToList();
            }

            return list;
        }

        /// <summary>
        /// returns a list with all non trainable skills without styles
        /// This is a copy of Ability until any unhandled Skill subclass needs to go in there...
        /// </summary>
        /// <returns>list of Skill's</returns>
        public virtual IList GetNonTrainableSkillList()
        {
            return GetAllAbilities();
        }

        /// <summary>
        /// Retrives a specific specialization by name
        /// </summary>
        /// <param name="name">the name of the specialization line</param>
        /// <param name="caseSensitive">false for case-insensitive compare</param>
        /// <returns>found specialization or null</returns>
        public virtual Specialization GetSpecializationByName(string name, bool caseSensitive = false)
        {
            Specialization spec = null;

            lock (((ICollection)m_specialization).SyncRoot)
            {

                if (caseSensitive && m_specialization.ContainsKey(name))
                    spec = m_specialization[name];

                foreach (KeyValuePair<string, Specialization> entry in m_specialization)
                {
                    if (entry.Key.ToLower().Equals(name.ToLower()))
                    {
                        spec = entry.Value;
                        break;
                    }
                }
            }

            return spec;
        }

        /// <summary>
        /// The best armor level this player can use.
        /// </summary>
        public virtual int BestArmorLevel
        {
            get
            {
                int bestLevel = -1;
                bestLevel = Math.Max(bestLevel, GetAbilityLevel(Abilities.AlbArmor));
                bestLevel = Math.Max(bestLevel, GetAbilityLevel(Abilities.HibArmor));
                bestLevel = Math.Max(bestLevel, GetAbilityLevel(Abilities.MidArmor));
                return bestLevel;
            }
        }

        #region Abilities

        /// <summary>
        /// Adds a new Ability to the player
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="sendUpdates"></param>
        public override void AddAbility(Ability ability, bool sendUpdates)
        {
            if (ability == null)
                return;

            base.AddAbility(ability, sendUpdates);
        }

        /// <summary>
        /// Adds a Realm Ability to the player
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="sendUpdates"></param>
        public virtual void AddRealmAbility(RealmAbility ability, bool sendUpdates)
        {
            if (ability == null)
                return;

            m_realmAbilities.FreezeWhile(list =>
            {
                int index = list.FindIndex(ab => ab.KeyName == ability.KeyName);
                if (index > -1)
                {
                    list[index].Level = ability.Level;
                }
                else
                {
                    list.Add(ability);
                }
            });

            RefreshSpecDependantSkills(true);
        }

        #endregion Abilities

        public virtual void RemoveAllAbilities()
        {
            lock (m_lockAbilities)
            {
                m_abilities.Clear();
            }
        }

        public virtual void RemoveAllSpecs()
        {
            lock (((ICollection)m_specialization).SyncRoot)
            {
                m_specialization.Clear();
            }
        }

        public virtual void RemoveAllSpellLines()
        {
            lock (lockSpellLinesList)
            {
                m_spellLines.Clear();
            }
        }

        public virtual void RemoveAllStyles()
        {
            lock (lockStyleList)
            {
                m_styles.Clear();
            }
        }

        public virtual void AddStyle(Style st, bool notify)
        {
            lock (lockStyleList)
            {
                if (m_styles.ContainsKey(st.ID))
                {
                    m_styles[st.ID].Level = st.Level;
                }
                else
                {
                    m_styles.Add(st.ID, st);

                    // Verbose
                    if (notify)
                    {
                        Style style = st;
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.YouLearn", style.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        string message = null;

                        if (Style.eOpening.Offensive == style.OpeningRequirementType)
                        {
                            switch (style.AttackResultRequirement)
                            {
                                case Style.eAttackResultRequirement.Style:
                                case Style.eAttackResultRequirement.Hit: // TODO: make own message for hit after styles DB is updated

                                    Style reqStyle = SkillBase.GetStyleByID(style.OpeningRequirementValue, CharacterClass.ID);

                                    if (reqStyle == null)
                                        message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.AfterStyle", "(style " + style.OpeningRequirementValue + " not found)");

                                    else message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.AfterStyle", reqStyle.Name);

                                    break;
                                case Style.eAttackResultRequirement.Miss:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.AfterMissed");
                                    break;
                                case Style.eAttackResultRequirement.Parry:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.AfterParried");
                                    break;
                                case Style.eAttackResultRequirement.Block:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.AfterBlocked");
                                    break;
                                case Style.eAttackResultRequirement.Evade:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.AfterEvaded");
                                    break;
                                case Style.eAttackResultRequirement.Fumble:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.AfterFumbles");
                                    break;
                            }
                        }
                        else if (Style.eOpening.Defensive == style.OpeningRequirementType)
                        {
                            switch (style.AttackResultRequirement)
                            {
                                case Style.eAttackResultRequirement.Miss:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.TargetMisses");
                                    break;
                                case Style.eAttackResultRequirement.Hit:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.TargetHits");
                                    break;
                                case Style.eAttackResultRequirement.Parry:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.TargetParried");
                                    break;
                                case Style.eAttackResultRequirement.Block:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.TargetBlocked");
                                    break;
                                case Style.eAttackResultRequirement.Evade:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.TargetEvaded");
                                    break;
                                case Style.eAttackResultRequirement.Fumble:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.TargetFumbles");
                                    break;
                                case Style.eAttackResultRequirement.Style:
                                    message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.RefreshSpec.TargetStyle");
                                    break;
                            }
                        }

                        if (!Util.IsEmpty(message))
                            Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve this player Realm Abilities.
        /// </summary>
        /// <returns></returns>
        public virtual List<RealmAbility> GetRealmAbilities()
        {
            return m_realmAbilities.ToList();
        }

        /// <summary>
        /// Asks for existance of specific specialization
        /// </summary>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public virtual bool HasSpecialization(string keyName)
        {
            bool hasit = false;

            lock (((ICollection)m_specialization).SyncRoot)
            {
                hasit = m_specialization.ContainsKey(keyName);
            }

            return hasit;
        }

        /// <summary>
        /// Checks whether Living has ability to use lefthanded weapons
        /// </summary>
        public override bool CanUseLefthandedWeapon
        {
            get
            {
                return CharacterClass.CanUseLefthandedWeapon;
            }
        }

        /// <summary>
        /// Calculates how many times left hand swings
        /// </summary>
        public override int CalculateLeftHandSwingCount()
        {
            if (CanUseLefthandedWeapon == false)
                return 0;

            if (GetBaseSpecLevel(Specs.Left_Axe) > 0)
                return 1; // always use left axe

            int specLevel = Math.Max(GetModifiedSpecLevel(Specs.Celtic_Dual), GetModifiedSpecLevel(Specs.Dual_Wield));
            specLevel = Math.Max(specLevel, GetModifiedSpecLevel(Specs.Fist_Wraps));

            int bonus = GetModified(eProperty.OffhandChanceBonus) + GetModified(eProperty.OffhandDamageAndChanceBonus);
            if (specLevel > 0)
            {
                return Util.Chance(25 + bonus + (specLevel - 1) * 68 / 100) ? 1 : 0;
            }

            // HtH chance
            specLevel = GetModifiedSpecLevel(Specs.HandToHand);
            InventoryItem attackWeapon = AttackWeapon;
            InventoryItem leftWeapon = (Inventory == null) ? null : Inventory.GetItem(eInventorySlot.LeftHandWeapon);
            if (specLevel > 0 && ActiveWeaponSlot == eActiveWeaponSlot.Standard
                && attackWeapon != null && attackWeapon.Object_Type == (int)eObjectType.HandToHand &&
                leftWeapon != null && leftWeapon.Object_Type == (int)eObjectType.HandToHand)
            {
                specLevel--;
                int randomChance = Util.Random(99);
                int hitChance = specLevel >> 1;
                if (randomChance < hitChance + bonus)
                    return 1; // 1 hit = spec/2

                hitChance += specLevel >> 2;
                if (randomChance < hitChance + bonus / 2)
                    return 2; // 2 hits = spec/4

                hitChance += specLevel >> 4;
                if (randomChance < hitChance + bonus / 4)
                    return 3; // 3 hits = spec/16

                return 0;
            }

            return 0;
        }

        /// <summary>
        /// Returns a multiplier to adjust left hand damage
        /// </summary>
        /// <returns></returns>
        public override double CalculateLeftHandEffectiveness(InventoryItem mainWeapon, InventoryItem leftWeapon)
        {
            double effectiveness = 1.0;

            if (CanUseLefthandedWeapon && leftWeapon != null && mainWeapon != null &&
                (mainWeapon.Item_Type == Slot.RIGHTHAND || mainWeapon.Item_Type == Slot.LEFTHAND))
            {
                if (leftWeapon.Object_Type == (int)eObjectType.LeftAxe)
                {
                    int LASpec = GetModifiedSpecLevel(Specs.Left_Axe);
                    if (LASpec > 0)
                    {
                        effectiveness = 0.625 + 0.0034 * LASpec;
                    }
                }
                effectiveness += GetModified(eProperty.OffhandDamageAndChanceBonus) * 0.01;
            }

            return effectiveness;
        }

        /// <summary>
        /// Returns a multiplier to adjust right hand damage
        /// </summary>
        /// <param name="leftWeapon"></param>
        /// <returns></returns>
        public override double CalculateMainHandEffectiveness(InventoryItem mainWeapon, InventoryItem leftWeapon)
        {
            double effectiveness = 1.0;

            if (CanUseLefthandedWeapon && leftWeapon != null && leftWeapon.Object_Type == (int)eObjectType.LeftAxe && mainWeapon != null &&
                (mainWeapon.Item_Type == Slot.RIGHTHAND || mainWeapon.Item_Type == Slot.LEFTHAND))
            {
                int LASpec = GetModifiedSpecLevel(Specs.Left_Axe);
                if (LASpec > 0)
                {
                    effectiveness = 0.625 + 0.0034 * LASpec;
                }
            }

            return effectiveness;
        }


        /// <summary>
        /// returns the level of a specialization
        /// if 0 is returned, the spec is non existent on player
        /// </summary>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public override int GetBaseSpecLevel(string keyName)
        {
            Specialization spec = null;
            int level = 0;

            lock (((ICollection)m_specialization).SyncRoot)
            {
                if (m_specialization.TryGetValue(keyName, out spec))
                    level = m_specialization[keyName].Level;
            }

            return level;
        }

        /// <summary>
        /// returns the level of a specialization + bonuses from RR and Items
        /// if 0 is returned, the spec is non existent on the player
        /// </summary>
        /// <param name="keyName"></param>
        /// <returns></returns>
        public override int GetModifiedSpecLevel(string keyName)
        {
            if (keyName == null)
                return 0;
            if (keyName.StartsWith(GlobalSpellsLines.Champion_Lines_StartWith))
                return 50;

            Specialization spec = null;
            int level = 0;
            lock (((ICollection)m_specialization).SyncRoot)
            {
                if (!m_specialization.TryGetValue(keyName, out spec))
                {
                    if (keyName == GlobalSpellsLines.Combat_Styles_Effect)
                    {
                        if (CharacterClass.ID == (int)eCharacterClass.Reaver || CharacterClass.ID == (int)eCharacterClass.Heretic)
                            level = GetModifiedSpecLevel(Specs.Flexible);
                        if (CharacterClass.ID == (int)eCharacterClass.Valewalker)
                            level = GetModifiedSpecLevel(Specs.Scythe);
                        if (CharacterClass.ID == (int)eCharacterClass.Savage)
                            level = GetModifiedSpecLevel(Specs.Savagery);
                    }

                    level = 0;
                }
            }

            if (spec != null)
            {
                level = spec.Level;
                // TODO: should be all in calculator later, right now
                // needs specKey -> eProperty conversion to find calculator and then
                // needs eProperty -> specKey conversion to find how much points player has spent
                eProperty skillProp = SkillBase.SpecToSkill(keyName);
                if (skillProp != eProperty.Undefined)
                    level += GetModified(skillProp);
            }

            return level;
        }

        /// <summary>
        /// Adds a spell line to the player
        /// </summary>
        /// <param name="line"></param>
        public virtual void AddSpellLine(SpellLine line)
        {
            AddSpellLine(line, true);
        }

        /// <summary>
        /// updates the levels of all spell lines
        /// specialized spell lines depend from spec levels
        /// base lines depend from player level
        /// </summary>
        /// <param name="sendMessages">sends "You gain power" messages if true</param>
        public virtual void UpdateSpellLineLevels(bool sendMessages)
        {
            lock (lockSpellLinesList)
            {
                foreach (SpellLine line in m_spellLines)
                {
                    if (line.IsBaseLine)
                    {
                        line.Level = Level;
                    }
                    else
                    {
                        int newSpec = GetBaseSpecLevel(line.Spec);
                        if (newSpec > 0)
                        {
                            if (sendMessages && line.Level < newSpec)
                                Out.SendMessage(LanguageMgr.GetTranslation(Client, "GameObjects.GamePlayer.UpdateSpellLine.GainPower", line.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            line.Level = newSpec;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a spell line to the player
        /// </summary>
        /// <param name="line"></param>
        public virtual void AddSpellLine(SpellLine line, bool notify)
        {
            if (line == null)
                return;

            SpellLine oldline = GetSpellLine(line.KeyName);
            if (oldline == null)
            {
                lock (lockSpellLinesList)
                {
                    m_spellLines.Add(line);
                }

                if (notify)
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.AddSpellLine.YouLearn", line.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else
            {
                // message to player
                if (notify && oldline.Level < line.Level)
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UpdateSpellLine.GainPower", line.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                oldline.Level = line.Level;
            }
        }

        /// <summary>
        /// return a list of spell lines in the order they were added
        /// this is a copy only.
        /// </summary>
        /// <returns></returns>
        public virtual List<SpellLine> GetSpellLines()
        {
            List<SpellLine> list = new List<SpellLine>();
            lock (lockSpellLinesList)
            {
                list = new List<SpellLine>(m_spellLines);
            }

            return list;
        }

        /// <summary>
        /// find a spell line on player and return them
        /// </summary>
        /// <param name="keyname"></param>
        /// <returns></returns>
        public virtual SpellLine GetSpellLine(string keyname)
        {
            lock (lockSpellLinesList)
            {
                foreach (SpellLine line in m_spellLines)
                {
                    if (line.KeyName == keyname)
                        return line;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a list of available styles
        /// This creates a copy
        /// </summary>
        public virtual IList GetStyleList()
        {
            List<Style> list = new List<Style>();
            lock (lockStyleList)
            {
                list = m_styles.Values.OrderBy(x => x.SpecLevelRequirement).ThenBy(y => y.ID).ToList();
            }
            return list;
        }

        /// <summary>
        /// Skill cache, maintained for network order on "skill use" request...
        /// Second item is for "Parent" Skill if applicable
        /// </summary>
        protected ReaderWriterList<Tuple<Skill, Skill>> m_usableSkills = new ReaderWriterList<Tuple<Skill, Skill>>();


        /// <summary>
        /// Add ability to a list of usable skills
        /// </summary>
        public virtual void AddUsableSkill(Ability ab)
        {
            if (ab == null)
                return;

            m_usableSkills.Add(new Tuple<Skill, Skill>(ab, ab));
            UpdateAbilities();
            return;
        }


        /// <summary>
        /// List Cast cache, maintained for network order on "spell use" request...
        /// Second item is for "Parent" SpellLine if applicable
        /// </summary>
        protected ReaderWriterList<Tuple<SpellLine, List<Skill>>> m_usableListSpells = new ReaderWriterList<Tuple<SpellLine, List<Skill>>>();

        /// <summary>
        /// Get All Usable Spell for a list Caster.
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        public virtual List<Tuple<SpellLine, List<Skill>>> GetAllUsableListSpells(bool update = false)
        {
            List<Tuple<SpellLine, List<Skill>>> results = new List<Tuple<SpellLine, List<Skill>>>();

            if (!update)
            {
                if (m_usableListSpells.Count > 0)
                    results = new List<Tuple<SpellLine, List<Skill>>>(m_usableListSpells);

                // return results if cache is valid.
                if (results.Count > 0)
                    return results;

            }

            // lock during all update, even if replace only take place at end...
            m_usableListSpells.FreezeWhile(innerList =>
            {

                List<Tuple<SpellLine, List<Skill>>> finalbase = new List<Tuple<SpellLine, List<Skill>>>();
                List<Tuple<SpellLine, List<Skill>>> finalspec = new List<Tuple<SpellLine, List<Skill>>>();

                // Add Lists spells ordered.
                foreach (Specialization spec in GetSpecList().Where(item => !item.HybridSpellList))
                {
                    var spells = spec.GetLinesSpellsForLiving(this);

                    foreach (SpellLine sl in spec.GetSpellLinesForLiving(this))
                    {
                        List<Tuple<SpellLine, List<Skill>>> working;
                        if (sl.IsBaseLine)
                        {
                            working = finalbase;
                        }
                        else
                        {
                            working = finalspec;
                        }

                        List<Skill> sps = new List<Skill>();
                        SpellLine key = spells.Keys.FirstOrDefault(el => el.KeyName == sl.KeyName);

                        if (key != null && spells.ContainsKey(key))
                        {
                            foreach (Skill sp in spells[key])
                            {
                                sps.Add(sp);
                            }
                        }

                        working.Add(new Tuple<SpellLine, List<Skill>>(sl, sps));
                    }
                }

                // Linq isn't used, we need to keep order ! (SelectMany, GroupBy, ToDictionary can't be used !)
                innerList.Clear();
                foreach (var tp in finalbase)
                {
                    innerList.Add(tp);
                    results.Add(tp);
                }

                foreach (var tp in finalspec)
                {
                    innerList.Add(tp);
                    results.Add(tp);
                }
            });

            return results;
        }

        /// <summary>
        /// Get All Player Usable Skill Ordered in Network Order (usefull to check for useskill)
        /// This doesn't get player's List Cast Specs...
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        public virtual List<Tuple<Skill, Skill>> GetAllUsableSkills(bool update = false)
        {
            List<Tuple<Skill, Skill>> results = new List<Tuple<Skill, Skill>>();

            if (!update)
            {

                if (m_usableSkills.Count > 0)
                    results = new List<Tuple<Skill, Skill>>(m_usableSkills);

                // return results if cache is valid.
                if (results.Count > 0)
                    return results;
            }

            // need to lock for all update.
            m_usableSkills.FreezeWhile(innerList =>
            {

                IList<Specialization> specs = GetSpecList();
                List<Tuple<Skill, Skill>> copylist = new List<Tuple<Skill, Skill>>(innerList);

                // Add Spec
                foreach (Specialization spec in specs.Where(item => item.Trainable))
                {
                    int index = innerList.FindIndex(e => (e.Item1 is Specialization) && ((Specialization)e.Item1).KeyName == spec.KeyName);

                    if (index < 0)
                    {
                        // Specs must be appended to spec list
                        innerList.Insert(innerList.Count(e => e.Item1 is Specialization), new Tuple<Skill, Skill>(spec, spec));
                    }
                    else
                    {
                        copylist.Remove(innerList[index]);
                        // Replace...
                        innerList[index] = new Tuple<Skill, Skill>(spec, spec);
                    }
                }

                // Add Abilities (Realm ability should be a custom spec)
                // Abilities order should be saved to db and loaded each time								
                foreach (Specialization spec in specs)
                {
                    List<Ability> abilities = spec.GetAbilitiesForLiving(this);
                    foreach (var ab in innerList)
                    {
                        if (ab.Item1 is Ability && !abilities.Contains(ab.Item1))
                        {
                            abilities.Add((Ability)ab.Item1);
                        }
                    }
                    foreach (Ability abv in abilities)
                    {
                        // We need the Instantiated Ability Object for Displaying Correctly According to Player "Activation" Method (if Available)
                        Ability ab = GetAbility(abv.KeyName);

                        if (ab == null)
                            ab = abv;

                        int index = innerList.FindIndex(k => (k.Item1 is Ability) && ((Ability)k.Item1).KeyName == ab.KeyName);
                        if (index < 0)
                        {
                            // add
                            innerList.Add(new Tuple<Skill, Skill>(ab, spec));
                        }
                        else
                        {
                            copylist.Remove(innerList[index]);
                            // replace
                            innerList[index] = new Tuple<Skill, Skill>(ab, spec);
                        }
                    }
                }

                // Add Hybrid spell
                foreach (Specialization spec in specs.Where(item => item.HybridSpellList))
                {
                    int index = -1;
                    foreach (KeyValuePair<SpellLine, List<Skill>> sl in spec.GetLinesSpellsForLiving(this))
                    {
                        foreach (Spell sp in sl.Value.Where(it => (it is Spell) && !((Spell)it).NeedInstrument).Cast<Spell>())
                        {
                            if (index < innerList.Count)
                                index = innerList.FindIndex(index + 1, e => ((e.Item2 is SpellLine) && ((SpellLine)e.Item2).Spec == sl.Key.Spec) && (e.Item1 is Spell) && !((Spell)e.Item1).NeedInstrument);

                            if (index < 0 || index >= innerList.Count)
                            {
                                // add
                                innerList.Add(new Tuple<Skill, Skill>(sp, sl.Key));
                                // disable replace
                                index = innerList.Count;
                            }
                            else
                            {
                                copylist.Remove(innerList[index]);
                                // replace
                                innerList[index] = new Tuple<Skill, Skill>(sp, sl.Key);
                            }
                        }
                    }
                }

                // Add Songs
                int songIndex = -1;
                foreach (Specialization spec in specs.Where(item => item.HybridSpellList))
                {
                    foreach (KeyValuePair<SpellLine, List<Skill>> sl in spec.GetLinesSpellsForLiving(this))
                    {
                        foreach (Spell sp in sl.Value.Where(it => (it is Spell) && ((Spell)it).NeedInstrument).Cast<Spell>())
                        {
                            if (songIndex < innerList.Count)
                                songIndex = innerList.FindIndex(songIndex + 1, e => (e.Item1 is Spell) && ((Spell)e.Item1).NeedInstrument);

                            if (songIndex < 0 || songIndex >= innerList.Count)
                            {
                                // add
                                innerList.Add(new Tuple<Skill, Skill>(sp, sl.Key));
                                // disable replace
                                songIndex = innerList.Count;
                            }
                            else
                            {
                                copylist.Remove(innerList[songIndex]);
                                // replace
                                innerList[songIndex] = new Tuple<Skill, Skill>(sp, sl.Key);
                            }
                        }
                    }
                }

                // Add Styles
                foreach (Specialization spec in specs)
                {
                    foreach (Style st in spec.GetStylesForLiving(this))
                    {
                        int index = innerList.FindIndex(e => (e.Item1 is Style) && e.Item1.ID == st.ID);
                        if (index < 0)
                        {
                            // add
                            innerList.Add(new Tuple<Skill, Skill>(st, spec));
                        }
                        else
                        {
                            copylist.Remove(innerList[index]);
                            // replace
                            innerList[index] = new Tuple<Skill, Skill>(st, spec);
                        }
                    }
                }

                // clean all not re-enabled skills
                foreach (Tuple<Skill, Skill> item in copylist)
                {
                    innerList.Remove(item);
                }

                foreach (Tuple<Skill, Skill> el in innerList)
                    results.Add(el);
            });

            return results;
        }

        /// <summary>
        /// updates the list of available skills (dependent on caracter specs)
        /// </summary>
        /// <param name="sendMessages">sends "you learn" messages if true</param>
        public virtual void RefreshSpecDependantSkills(bool sendMessages)
        {
            // refresh specs
            LoadClassSpecializations(sendMessages);

            // lock specialization while refreshing...
            lock (((ICollection)m_specialization).SyncRoot)
            {
                foreach (Specialization spec in m_specialization.Values)
                {
                    // check for new Abilities
                    foreach (Ability ab in spec.GetAbilitiesForLiving(this))
                    {
                        if (!HasAbility(ab.KeyName) || GetAbility(ab.KeyName).Level < ab.Level)
                            AddAbility(ab, sendMessages);
                    }

                    // check for new Styles
                    foreach (Style st in spec.GetStylesForLiving(this))
                    {
                        AddStyle(st, sendMessages);
                    }

                    // check for new SpellLine
                    foreach (SpellLine sl in spec.GetSpellLinesForLiving(this))
                    {
                        AddSpellLine(sl, sendMessages);
                    }

                }
            }
        }

        /// <summary>
        /// Called by trainer when specialization points were added to a skill
        /// </summary>
        /// <param name="skill"></param>
        public void OnSkillTrained(Specialization skill)
        {
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnSkillTrained.YouSpend", skill.Level, skill.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnSkillTrained.YouHave", SkillSpecialtyPoints), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (!(this == player))
                {
                    player.MessageFromArea(this, LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.OnSkillTrained.TrainsInVarious",
                        player.GetPersonalizedName(this)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
            CharacterClass.OnSkillTrained(this, skill);
            RefreshSpecDependantSkills(true);

            Out.SendUpdatePlayerSkills();
        }

        /// <summary>
        /// effectiveness of the player (resurrection illness)
        /// Effectiveness is used in physical/magic damage (exept dot), in weapon skill and max concentration formula
        /// </summary>
        protected double m_playereffectiveness = 1.0;

        /// <summary>
        /// get / set the player's effectiveness.
        /// Effectiveness is used in physical/magic damage (exept dot), in weapon skill and max concentration
        /// </summary>
        public override double Effectiveness
        {
            get { return m_playereffectiveness; }
            set { m_playereffectiveness = value; }
        }

        /// <summary>
        /// Creates new effects list for this living.
        /// </summary>
        /// <returns>New effects list instance</returns>
        protected override GameEffectList CreateEffectsList()
        {
            return new GameEffectPlayerList(this);
        }

        #endregion

        #region Realm-/Region-/Bount-/Skillpoints...
        /// <summary>
        /// Gets/sets player realm points
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual long RealmPoints
        {
            get { return DBCharacter != null ? DBCharacter.RealmPoints : 0; }
            set { if (DBCharacter != null) DBCharacter.RealmPoints = value; }
        }

        /// <summary>
        /// Gets/sets player skill specialty points
        /// </summary>
        public virtual int SkillSpecialtyPoints
        {
            get { return VerifySpecPoints(); }
        }

        /// <summary>
        /// Gets/sets player realm specialty points
        /// </summary>
        public virtual int RealmSpecialtyPoints
        {
            get
            {
                return GameServer.ServerRules.GetPlayerRealmPointsTotal(this)
                    - GetRealmAbilities().Where(ab => !(ab is RR5RealmAbility))
                    .Sum(ab => Enumerable.Range(0, ab.Level).Sum(i => ab.CostForUpgrade(i)));
            }
        }

        /// <summary>
        /// Retrieves a specific specialization by key name
        /// </summary>
        /// <param name="keyName">the key name</param>
        /// <returns>the found specialization or null</returns>
        public virtual Specialization GetSpecialization(string keyName)
        {
            Specialization spec = null;

            m_specialization.TryGetValue(keyName, out spec);

            if (spec == null)
            {
                // try case insensitive search
                foreach (Specialization sp in m_specialization.Values)
                {
                    if (sp.KeyName.ToLower() == keyName.ToLower())
                    {
                        spec = sp;
                        break;
                    }
                }
            }

            return spec;
        }

        /// <summary>
        /// Gets/sets player realm rank
        /// </summary>
        public virtual int RealmLevel
        {
            get { return DBCharacter != null ? DBCharacter.RealmLevel : 0; }
            set
            {
                if (DBCharacter != null)
                    DBCharacter.RealmLevel = value;
                CharacterClass.OnRealmLevelUp(this);
            }
        }

        /// <summary>
        /// Returns the translated realm rank title of the player.
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public virtual string RealmRankTitle(string language)
        {
            string translationId = string.Empty;

            if (Realm != eRealm.None && Realm != eRealm.Door)
            {
                int RR = 0;

                if (RealmLevel > 1)
                    RR = RealmLevel / 10 + 1;

                string realm = string.Empty;
                if (Realm == eRealm.Albion)
                    realm = "Albion";
                else if (Realm == eRealm.Midgard)
                    realm = "Midgard";
                else
                    realm = "Hibernia";

                string gender = Gender == eGender.Female ? "Female" : "Male";

                translationId = string.Format("{0}.RR{1}.{2}", realm, RR, gender);
            }
            else
            {
                translationId = "UnknownRealm";
            }

            string translation;
            if (!LanguageMgr.TryGetTranslation(out translation, language, string.Format("GameObjects.GamePlayer.RealmTitle.{0}", translationId)))
                translation = RealmTitle;

            return translation;
        }

        /// <summary>
        /// Gets player realm rank name
        /// sirru mod 20.11.06
        /// </summary>
        public virtual string RealmTitle
        {
            get
            {
                if (Realm == eRealm.None)
                    return "Unknown Realm";

                try
                {
                    return GlobalConstants.REALM_RANK_NAMES[(int)Realm - 1, (int)Gender - 1, (RealmLevel / 10)];
                }
                catch
                {
                    return "Unknown Rank"; // why aren't all the realm ranks defined above?
                }
            }
        }

        /// <summary>
        /// Called when this player gains realm points
        /// </summary>
        /// <param name="amount">The amount of realm points gained</param>
        public override void GainRealmPoints(long amount)
        {
            GainRealmPoints(amount, true, true);
        }

        /// <summary>
        /// Called when this living gains realm points
        /// </summary>
        /// <param name="amount">The amount of realm points gained</param>
        public void GainRealmPoints(long amount, bool modify)
        {
            GainRealmPoints(amount, modify, true);
        }

        /// <summary>
        /// Called when this player gains realm points
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="modify"></param>
        /// <param name="sendMessage"></param>
        public void GainRealmPoints(long amount, bool modify, bool sendMessage)
        {
            GainRealmPoints(amount, modify, true, true);
        }

        /// <summary>
        /// Called when this player gains realm points
        /// </summary>
        /// <param name="amount">The amount of realm points gained</param>
        /// <param name="modify">Should we apply the rp modifer</param>
        /// <param name="sendMessage">Wether to send a message like "You have gained N realmpoints"</param>
        /// <param name="notify"></param>
        public virtual void GainRealmPoints(long amount, bool modify, bool sendMessage, bool notify, long territoryBonus = 0)
        {
            if (!GainRP)
                return;
            
            //rp rate modifier
            double modifier = ServerProperties.Properties.RP_RATE;
            if (territoryBonus > 0)
            {
                if (modify && modifier != -1)
                    territoryBonus = (long)(territoryBonus * ServerProperties.Properties.RP_RATE);

                SendTranslatedMessage("GameObjects.GamePlayer.GainRealmPoints.TerritoryBonusRP", eChatType.CT_Important, eChatLoc.CL_SystemWindow, territoryBonus);
            }

            if (modify)
            {
                //rp rate modifier
                if (modifier != -1)
                    amount = (long)(amount * modifier);
            }

            amount += territoryBonus;
            
            if (modify)
            {
                //Zone Bonus Support
                if (Properties.ENABLE_ZONE_BONUSES)
                {
                    int zoneBonus = (int)(amount * ZoneBonus.GetRPBonus(this));
                    if (zoneBonus > 0)
                    {
                        Out.SendMessage(ZoneBonus.GetBonusMessage(this, zoneBonus, ZoneBonus.eZoneBonusType.RP),
                                        eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        GainRealmPoints((long)(zoneBonus), false, false, false);
                    }
                }

                //Zone Bonus Support factor
                if (Properties.ENABLE_AREA_BONUSES)
                {
                    //Area RP Bonus
                    int areapoints = 0;
                    foreach (var area in this.CurrentAreas)
                    {
                        areapoints += area.RealmPoints;
                    }

                    int areaBonus = (int)(amount * areapoints);
                    if (areaBonus > 0)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.AreaBonusRP", areaBonus), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        GainRealmPoints((long)(areaBonus), false, false, false);
                    }
                }


                //[Freya] Nidel: ToA Rp Bonus
                long rpBonus = GetModified(eProperty.RealmPoints);
                if (rpBonus > 0)
                {
                    amount += (amount * rpBonus) / 100;
                }
            }

            if (notify)
                base.GainRealmPoints(amount);

            RealmPoints += amount;

            if (m_guild != null && Client.Account.PrivLevel == 1)
            {
                if (IsInRvR)
                {
                    if (RealGuild is { GuildType: Guild.eGuildType.PlayerGuild })
                    {
                        RealGuild.RealmPoints += amount;
                    }
                    // do not give realm points to RvR guilds
                }
                else
                {
                    m_guild.RealmPoints += amount;
                }
            }

            if (sendMessage == true && amount > 0)
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.YouGet", amount.ToString()), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

            while (RealmPoints >= CalculateRPsFromRealmLevel(RealmLevel + 1) && RealmLevel < (REALMPOINTS_FOR_LEVEL.Length - 1))
            {
                RealmLevel++;
                Out.SendUpdatePlayer();
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.GainedLevel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                if (RealmLevel % 10 == 0)
                {
                    Out.SendUpdatePlayerSkills();
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.GainedRank"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.ReachedRank", (RealmLevel / 10) + 1), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.NewRealmTitle", RealmRankTitle(Client.Account.Language)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.GainBonus", RealmLevel / 10), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    foreach (GamePlayer plr in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        plr.Out.SendLivingDataUpdate(this, true);
                    Notify(GamePlayerEvent.RRLevelUp, this);
                }
                else
                    Notify(GamePlayerEvent.RLLevelUp, this);
                if (CanGenerateNews && ((RealmLevel >= 40 && RealmLevel % 10 == 0) || RealmLevel >= 60))
                {
                    string message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.ReachedRankNews", Name, RealmLevel + 10, LastPositionUpdateZone.Description);
                    string newsmessage = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.ReachedRankNews", Name, RealmLevel + 10, LastPositionUpdateZone.Description);
                    NewsMgr.CreateNews(newsmessage, this.Realm, eNewsType.RvRLocal, true);
                }
                if (CanGenerateNews && RealmPoints >= 1000000 && RealmPoints - amount < 1000000)
                {
                    string message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.Earned", Name, LastPositionUpdateZone.Description);
                    string newsmessage = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.Earned", Name, LastPositionUpdateZone.Description);
                    NewsMgr.CreateNews(newsmessage, this.Realm, eNewsType.RvRLocal, true);
                }
            }
            Out.SendUpdatePoints();
        }

        /// <summary>
        /// Get the total RP factor 
        /// </summary>
        /// <returns></returns>
        
        public double GetRPFactor(bool enableZone = true)
        {
            double factor = 1.0d;

            if (enableZone && ServerProperties.Properties.ENABLE_ZONE_BONUSES && CurrentZone != null)
            {
                factor *= 1.0d + (ZoneBonus.GetRPBonus(this) / 100.0d);
            }

            if (enableZone && Properties.ENABLE_AREA_BONUSES)
            {
                //Area RP Bonus
                double areapoints = 0;
                foreach (var area in this.CurrentAreas)
                {
                    areapoints += area.RealmPoints;
                }
                areapoints /= 100.0d;
                factor *= 1.0d + areapoints;
            }

            factor *= ServerProperties.Properties.RP_RATE;

            var toaBonus = GetModified(eProperty.RealmPoints);
            factor *= toaBonus != 0 ? 1.0d + toaBonus / 100.0d : 1.0d;

            return factor;
        }

        public long BountyPointCap => Properties.BOUNTY_POINT_CAP;

        /// <summary>
        /// Player gains bounty points
        /// </summary>
        /// <param name="amount">The amount of bounty points</param>
        public override void GainBountyPoints(long amount)
        {
            GainBountyPoints(amount, true, true);
        }

        /// <summary>
        /// Player gains bounty points
        /// </summary>
        /// <param name="amount">The amount of bounty points</param>
        public void GainBountyPoints(long amount, bool modify)
        {
            GainBountyPoints(amount, modify, true);
        }

        /// <summary>
        /// Called when player gains bounty points
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="modify"></param>
        /// <param name="sendMessage"></param>
        public void GainBountyPoints(long amount, bool modify, bool sendMessage)
        {
            GainBountyPoints(amount, modify, true, true);
        }


        /// <summary>
        /// Called when player gains bounty points
        /// </summary>
        /// <param name="amount">The amount of bounty points gained</param>
        /// <param name="multiply">Should this amount be multiplied by the BP Rate</param>
        /// <param name="sendMessage">Wether to send a message like "You have gained N bountypoints"</param>
        public virtual void GainBountyPoints(long amount, bool modify, bool sendMessage, bool notify)
        {
            if (modify)
            {
                //bp rate modifier
                double modifier = ServerProperties.Properties.BP_RATE;
                if (modifier != -1)
                    amount = (long)(amount * modifier);

                //[StephenxPimente]: Zone Bonus Support
                if (ServerProperties.Properties.ENABLE_ZONE_BONUSES)
                {
                    int zoneBonus = (((int)amount * ZoneBonus.GetBPBonus(this)) / 100);
                    if (zoneBonus > 0)
                    {
                        Out.SendMessage(ZoneBonus.GetBonusMessage(this, (int)(zoneBonus * ServerProperties.Properties.BP_RATE), ZoneBonus.eZoneBonusType.BP),
                                        eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        GainBountyPoints((long)(zoneBonus * ServerProperties.Properties.BP_RATE), false, false, false);
                    }
                }

                //[Freya] Nidel: ToA Bp Bonus
                long bpBonus = GetModified(eProperty.BountyPoints);

                if (bpBonus > 0)
                {
                    amount += (amount * bpBonus) / 100;
                }
            }

            long currentBPs = BountyPointBalance;

            if (currentBPs + amount > BountyPointCap)
            {
                amount = BountyPointCap - currentBPs;
                if (amount < 0) amount = 0;
            }

            if (notify)
                base.GainBountyPoints(amount);

            Wallet.AddMoney(Currency.BountyPoints.Mint(amount));

            if (m_guild is { IsSystemGuild: false } && Client.Account.PrivLevel == 1)
                m_guild.BountyPoints += amount;

            if (sendMessage == true)
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainBountyPoints.YouGet", amount.ToString()), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        public void AddBountyPoints(long amount)
        {
            Wallet.AddMoney(Currency.BountyPoints.Mint(amount));
        }

        /// <summary>
        /// Removes bounty points from the player
        /// </summary>
        /// <param name="amount">The amount of bounty points to remove</param>
        public void RemoveBountyPoints(long amount)
        {
            if (amount <= 0)
                return;

            // Ensure we don't go below zero
            long currentBPs = this.BountyPointBalance;
            if (amount > currentBPs)
                amount = currentBPs;

            Wallet.RemoveMoney(Currency.BountyPoints.Mint(amount));

            Out.SendMessage($"You have lost {amount} bounty points.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            SaveIntoDatabase();
        }

        /// <summary>
        /// Holds realm points needed for special realm level
        /// </summary>
        public static readonly long[] REALMPOINTS_FOR_LEVEL =
        {
            0,	// for level 0
            0,	// for level 1
            25,	// for level 2
            125,	// for level 3
            350,	// for level 4
            750,	// for level 5
            1375,	// for level 6
            2275,	// for level 7
            3500,	// for level 8
            5100,	// for level 9
            7125,	// for level 10
            9625,	// for level 11
            12650,	// for level 12
            16250,	// for level 13
            20475,	// for level 14
            25375,	// for level 15
            31000,	// for level 16
            37400,	// for level 17
            44625,	// for level 18
            52725,	// for level 19
            61750,	// for level 20
            71750,	// for level 21
            82775,	// for level 22
            94875,	// for level 23
            108100,	// for level 24
            122500,	// for level 25
            138125,	// for level 26
            155025,	// for level 27
            173250,	// for level 28
            192850,	// for level 29
            213875,	// for level 30
            236375,	// for level 31
            260400,	// for level 32
            286000,	// for level 33
            313225,	// for level 34
            342125,	// for level 35
            372750,	// for level 36
            405150,	// for level 37
            439375,	// for level 38
            475475,	// for level 39
            513500,	// for level 40
            553500,	// for level 41
            595525,	// for level 42
            639625,	// for level 43
            685850,	// for level 44
            734250,	// for level 45
            784875,	// for level 46
            837775,	// for level 47
            893000,	// for level 48
            950600,	// for level 49
            1010625,	// for level 50
            1073125,	// for level 51
            1138150,	// for level 52
            1205750,	// for level 53
            1275975,	// for level 54
            1348875,	// for level 55
            1424500,	// for level 56
            1502900,	// for level 57
            1584125,	// for level 58
            1668225,	// for level 59
            1755250,	// for level 60
            1845250,	// for level 61
            1938275,	// for level 62
            2034375,	// for level 63
            2133600,	// for level 64
            2236000,	// for level 65
            2341625,	// for level 66
            2450525,	// for level 67
            2562750,	// for level 68
            2678350,	// for level 69
            2797375,	// for level 70
            2919875,	// for level 71
            3045900,	// for level 72
            3175500,	// for level 73
            3308725,	// for level 74
            3445625,	// for level 75
            3586250,	// for level 76
            3730650,	// for level 77
            3878875,	// for level 78
            4030975,	// for level 79
            4187000,	// for level 80
            4347000,	// for level 81
            4511025,	// for level 82
            4679125,	// for level 83
            4851350,	// for level 84
            5027750,	// for level 85
            5208375,	// for level 86
            5393275,	// for level 87
            5582500,	// for level 88
            5776100,	// for level 89
            5974125,	// for level 90
            6176625,	// for level 91
            6383650,	// for level 92
            6595250,	// for level 93
            6811475,	// for level 94
            7032375,	// for level 95
            7258000,	// for level 96
            7488400,	// for level 97
            7723625,	// for level 98
            7963725,	// for level 99
            8208750,	// for level 100
            9111713,	// for level 101
            10114001,	// for level 102
            11226541,	// for level 103
            12461460,	// for level 104
            13832221,	// for level 105
            15353765,	// for level 106
            17042680,	// for level 107
            18917374,	// for level 108
            20998286,	// for level 109
            23308097,	// for level 110
            25871988,	// for level 111
            28717906,	// for level 112
            31876876,	// for level 113
            35383333,	// for level 114
            39275499,	// for level 115
            43595804,	// for level 116
            48391343,	// for level 117
            53714390,	// for level 118
            59622973,	// for level 119
            66181501,	// for level 120
            73461466,	// for level 121
            81542227,	// for level 122
            90511872,	// for level 123
            100468178,	// for level 124
            111519678,	// for level 125
            123786843,	// for level 126
            137403395,	// for level 127
            152517769,	// for level 128
            169294723,	// for level 129
            187917143,	// for level 130

        };

        /// <summary>
        /// Calculates amount of RealmPoints needed for special realm level
        /// </summary>
        /// <param name="realmLevel">realm level</param>
        /// <returns>amount of realm points</returns>
        public virtual long CalculateRPsFromRealmLevel(int realmLevel)
        {
            if (realmLevel < REALMPOINTS_FOR_LEVEL.Length)
                return REALMPOINTS_FOR_LEVEL[realmLevel];

            // thanks to Linulo from http://daoc.foren.4players.de/viewtopic.php?t=40839&postdays=0&postorder=asc&start=0
            return (long)(25.0 / 3.0 * (realmLevel * realmLevel * realmLevel) - 25.0 / 2.0 * (realmLevel * realmLevel) + 25.0 / 6.0 * realmLevel);
        }
        
        public long CalculateRPsToGainRealmRank()
        {
            int currentRealmRank = RealmLevel < 10 ? 1 : RealmLevel / 10 + 1;
            
            return CalculateRPsFromRealmLevel((currentRealmRank + 1) * 10) - CalculateRPsFromRealmLevel(currentRealmRank * 10);
        }
        
        public long CalculateRPsToGainRealmRank(int percentage)
        {
            return (long) Math.Ceiling(percentage / 100.0d * CalculateRPsToGainRealmRank());
        }

        /// <summary>
        /// Calculates realm level from realm points. SLOW.
        /// </summary>
        /// <param name="realmPoints">amount of realm points</param>
        /// <returns>realm level: RR5L3 = 43, RR1L2 = 2</returns>
        protected virtual int CalculateRealmLevelFromRPs(long realmPoints)
        {
            if (realmPoints == 0)
                return 0;

            int i;

            for (i = REALMPOINTS_FOR_LEVEL.Length - 1; i > 0; i--)
            {
                if (REALMPOINTS_FOR_LEVEL[i] <= realmPoints)
                    break;
            }

            return i;
        }

        /// <summary>
        /// Realm point value of this player
        /// </summary>
        public override int RealmPointsValue
        {
            get
            {
                // http://www.camelotherald.com/more/2275.shtml
                // new 1.81D formula
                // Realm point value = (level - 20)squared + (realm rank level x 5) + (champion level x 10) + (master level (squared)x 5)
                //we use realm level 1L0 = 0, mythic uses 1L0 = 10, so we + 10 the realm level
                int level = Math.Max(0, Level - 20);
                if (level == 0)
                    return Math.Max(1, (RealmLevel + 10) * 5);

                return Math.Max(1, level * level + (RealmLevel + 10) * 5);
            }
        }

        /// <summary>
        /// Bounty point value of this player
        /// </summary>
        public override int BountyPointsValue
        {
            // TODO: correct formula!
            get
            {
                if (RvrManager.Instance.IsInRvr(this))
                    return (int)(1 + Level * 0.6);
                return 0;
            }
        }

        /// <summary>
        /// Returns the amount of experience this player is worth
        /// </summary>
        public override long ExperienceValue
        {
            get
            {
                return base.ExperienceValue * 4;
            }
        }

        public static readonly int[] prcRestore =
        {
            // http://www.silicondragon.com/Gaming/DAoC/Misc/XPs.htm
            1,//0
            3,//1
            6,//2
            10,//3
            15,//4
            21,//5
            33,//6
            53,//7
            82,//8
            125,//9
            188,//10
            278,//11
            352,//12
            443,//13
            553,//14
            688,//15
            851,//16
            1048,//17
            1288,//18
            1578,//19
            1926,//20
            2347,//21
            2721,//22
            3146,//23
            3633,//24
            4187,//25
            4820,//26
            5537,//27
            6356,//28
            7281,//29
            8337,//30
            9532,//31 - from logs
            10886,//32 - from logs
            12421,//33 - from logs
            14161,//34
            16131,//35
            18360,//36 - recheck
            19965,//37 - guessed
            21857,//38
            23821,//39
            25928,//40 - guessed
            28244,//41
            30731,//42
            33411,//43
            36308,//44
            39438,//45
            42812,//46
            46454,//47
            50385,//48
            54625,//49
            59195,//50
        };

        /// <summary>
        /// Money value of this player
        /// </summary>
        public override long MoneyValue
        {
            get
            {
                return 3 * prcRestore[Level < GamePlayer.prcRestore.Length ? Level : GamePlayer.prcRestore.Length - 1];
            }
        }

        #endregion

        #region Level/Experience

        /// <summary>
        /// What is the maximum level a player can achieve?
        /// To alter this in a custom GamePlayer class you must override this method and
        /// provide your own XPForLevel array with MaxLevel + 1 entries
        /// </summary>
        public virtual byte MaxLevel
        {
            get { return 50; }
        }

        public byte SpellMaxLevel
        {
            get { return 55; }
        }

        /// <summary>
        /// How much experience is needed for a given level?
        /// </summary>
        public virtual long GetExperienceNeededForLevel(int level)
        {
            if (level > MaxLevel)
                return GetExperienceAmountForLevel(MaxLevel);

            if (level <= 0)
                return GetExperienceAmountForLevel(0);

            return GetExperienceAmountForLevel(level - 1);
        }

        public virtual long GetExperienceNeededForNextLevel()
        {
            int currentLevel = Math.Min(MaxLevel - 1, Level);

            return GetExperienceNeededForLevel(currentLevel + 1) - GetExperienceNeededForLevel(currentLevel);
        }

        public virtual long GetExperienceNeededForNextLevel(int percentage)
        {
            return (long)Math.Ceiling(percentage / 100.0d * GetExperienceNeededForNextLevel());
        }

        /// <summary>
        /// How Much Experience Needed For Level
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public static long GetExperienceAmountForLevel(int level)
        {
            try
            {
                return XPForLevel[level];
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// A table that holds the required XP/Level
        /// This must include a final entry for MaxLevel + 1
        /// </summary>
        private static readonly long[] XPForLevel =
        {
            0, // xp to level 1
            50, // xp to level 2
            250, // xp to level 3
            850, // xp to level 4
            2300, // xp to level 5
            6350, // xp to level 6
            15950, // xp to level 7
            37950, // xp to level 8
            88950, // xp to level 9
            203950, // xp to level 10
            459950, // xp to level 11
            839950, // xp to level 12
            1399950, // xp to level 13
            2199950, // xp to level 14
            3399950, // xp to level 15
            5199950, // xp to level 16
            7899950, // xp to level 17
            11799950, // xp to level 18
            17499950, // xp to level 19
            25899950, // xp to level 20
            38199950, // xp to level 21
            54699950, // xp to level 22
            76999950, // xp to level 23
            106999950, // xp to level 24
            146999950, // xp to level 25
            199999950, // xp to level 26
            269999950, // xp to level 27
            359999950, // xp to level 28
            479999950, // xp to level 29
            639999950, // xp to level 30
            849999950, // xp to level 31
            1119999950, // xp to level 32
            1469999950, // xp to level 33
            1929999950, // xp to level 34
            2529999950, // xp to level 35
            3319999950, // xp to level 36
            4299999950, // xp to level 37
            5499999950, // xp to level 38
            6899999950, // xp to level 39
            8599999950, // xp to level 40
            12899999950, // xp to level 41
            20699999950, // xp to level 42
            29999999950, // xp to level 43
            40799999950, // xp to level 44
            53999999950, // xp to level 45
            69599999950, // xp to level 46
            88499999950, // xp to level 47
            110999999950, // xp to level 48
            137999999950, // xp to level 49
            169999999950, // xp to level 50
            999999999950, // xp to level 51
        };

        /// <summary>
        /// Gets or sets the current xp of this player
        /// </summary>
        public virtual long Experience
        {
            get { return DBCharacter != null ? DBCharacter.Experience : 0; }
            set
            {
                if (DBCharacter != null)
                    DBCharacter.Experience = value;
            }
        }

        /// <summary>
        /// Returns the xp that are needed for the next level
        /// </summary>
        public virtual long ExperienceForNextLevel
        {
            get
            {
                return GetExperienceNeededForLevel(Level + 1);
            }
        }

        /// <summary>
        /// Returns the xp that were needed for the current level
        /// </summary>
        public virtual long ExperienceForCurrentLevel
        {
            get
            {
                return GetExperienceNeededForLevel(Level);
            }
        }

        /// <summary>
        /// Returns the xp that is needed for the second stage of current level
        /// </summary>
        public virtual long ExperienceForCurrentLevelSecondStage
        {
            get { return 1 + ExperienceForCurrentLevel + (ExperienceForNextLevel - ExperienceForCurrentLevel) / 2; }
        }

        /// <summary>
        /// Returns how far into the level we have progressed
        /// A value between 0 and 1000 (1 bubble = 100)
        /// </summary>
        public virtual ushort LevelPermill
        {
            get
            {
                //No progress if we haven't even reached current level!
                if (Experience < ExperienceForCurrentLevel)
                    return 0;
                //No progess after maximum level
                if (Level > MaxLevel)
                    return 0;
                return (ushort)(1000 * (Experience - ExperienceForCurrentLevel) / (ExperienceForNextLevel - ExperienceForCurrentLevel));
            }
        }

        /// <summary>
        /// Called whenever this player gains experience
        /// </summary>
        /// <param name="expTotal"></param>
        /// <param name="expCampBonus"></param>
        /// <param name="expGroupBonus"></param>
        /// <param name="expOutpostBonus"></param>
        /// <param name="sendMessage"></param>
        public void GainExperience(eXPSource xpSource, long expTotal, long expCampBonus, long expGroupBonus, long expOutpostBonus, bool sendMessage, int eventMultiplicator)
        {
            GainExperience(xpSource, expTotal, expCampBonus, expGroupBonus, expOutpostBonus, sendMessage, true, eventMultiplicator);
        }

        /// <summary>
        /// Called whenever this player gains experience
        /// </summary>
        /// <param name="expTotal"></param>
        /// <param name="expCampBonus"></param>
        /// <param name="expGroupBonus"></param>
        /// <param name="expOutpostBonus"></param>
        /// <param name="sendMessage"></param>
        /// <param name="allowMultiply"></param>
        public void GainExperience(eXPSource xpSource, long expTotal, long expCampBonus, long expGroupBonus, long expOutpostBonus, bool sendMessage, bool allowMultiply, int eventMultiplicator)
        {
            GainExperience(xpSource, expTotal, expCampBonus, expGroupBonus, expOutpostBonus, sendMessage, allowMultiply, true, eventMultiplicator);
        }

        /// <summary>
        /// Called whenever this player gains experience
        /// </summary>
        /// <param name="expTotal"></param>
        /// <param name="expCampBonus"></param>
        /// <param name="expGroupBonus"></param>
        /// <param name="expOutpostBonus"></param>
        /// <param name="sendMessage"></param>
        /// <param name="allowMultiply"></param>
        /// <param name="notify"></param>
        public override void GainExperience(eXPSource xpSource, long expTotal, long expCampBonus, long expGroupBonus, long expOutpostBonus, bool sendMessage, bool allowMultiply, bool notify, int eventMultiplicator)
        {
            if (!GainXP && expTotal > 0)
                return;

            //xp rate modifier
            if (allowMultiply)
            {
                //we only want to modify the base rate, not the group or camp bonus
                expTotal -= expGroupBonus;
                expTotal -= expCampBonus;
                expTotal -= expOutpostBonus;

                //[StephenxPimentel] - Zone Bonus XP Support
                if (ServerProperties.Properties.ENABLE_ZONE_BONUSES)
                {
                    long zoneBonus = (long)Math.Round(expTotal * (ZoneBonus.GetXPBonus(this) / 100.0d));
                    if (zoneBonus > 0)
                    {
                        Out.SendMessage(ZoneBonus.GetBonusMessage(this, (long)(zoneBonus * ServerProperties.Properties.XP_RATE), ZoneBonus.eZoneBonusType.XP),
                                        eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        GainExperience(eXPSource.Other, (long)(zoneBonus * ServerProperties.Properties.XP_RATE), 0, 0, 0, false, false, false, 1);
                    }
                }


                if (this.CurrentRegion.IsRvR)
                    expTotal = (long)(expTotal * ServerProperties.Properties.RvR_XP_RATE);
                else
                    expTotal = (long)(expTotal * ServerProperties.Properties.XP_RATE);

                // [Freya] Nidel: ToA Xp Bonus
                long xpBonus = GetModified(eProperty.XpPoints);
                if (xpBonus != 0)
                {
                    expTotal += (expTotal * xpBonus) / 100;
                }

                long hardXPCap = (long)(GameServer.ServerRules.GetExperienceForLiving(Level) * ServerProperties.Properties.XP_HARDCAP_PERCENT / 100);

                if (expTotal > hardXPCap)
                    expTotal = hardXPCap;

                expTotal += expOutpostBonus;
                expTotal += expGroupBonus;
                expTotal += expCampBonus;

            }

            long bonusRenaissance = 0;
            long territoryExp = 0;

            if (this.IsRenaissance)
            {
                if (this.Level < 40)
                {
                    bonusRenaissance = (long)Math.Round(expTotal * 1.5D);
                }
                else
                {
                    //Remove 50% points from levl > 40
                    bonusRenaissance = (long)Math.Round(expTotal / 2D) * -1;
                }

                expTotal += bonusRenaissance;
            }

            long eventBonus = 0;
            if (eventMultiplicator > 1)
            {
                eventBonus = expTotal * eventMultiplicator;
                expTotal += eventBonus;
            }

            if (Guild is { GuildType: Guild.eGuildType.PlayerGuild, TerritoryBonusExperienceFactor: not 0.0d })
            {
                territoryExp = (long)Math.Round(expTotal * Guild.TerritoryBonusExperienceFactor);
                expTotal += territoryExp;
            }

            // Get Champion Experience too
            GainChampionExperience(expTotal);

            //catacombs characters get 50% boost if they are elligable for slash level
            switch ((eCharacterClass)CharacterClass.ID)
            {
                case eCharacterClass.Heretic:
                case eCharacterClass.Valkyrie:
                case eCharacterClass.Warlock:
                case eCharacterClass.Bainshee:
                case eCharacterClass.Vampiir:
                case eCharacterClass.MaulerAlb:
                case eCharacterClass.MaulerHib:
                case eCharacterClass.MaulerMid:
                    {
                        //we don't want to allow catacombs classes to use free levels and
                        //have a 50% bonus
                        if (!ServerProperties.Properties.ALLOW_CATA_SLASH_LEVEL && CanUseSlashLevel && Level < 20)
                        {
                            expTotal = (long)Math.Round(expTotal * 1.5);
                        }
                        break;
                    }
            }

            base.GainExperience(xpSource, expTotal, expCampBonus, expGroupBonus, expOutpostBonus, sendMessage, allowMultiply, notify, eventMultiplicator);

            if (IsLevelSecondStage)
            {
                if (Experience + expTotal < ExperienceForCurrentLevelSecondStage)
                {
                    expTotal = ExperienceForCurrentLevelSecondStage - Experience;
                }
            }
            else if (Experience + expTotal < ExperienceForCurrentLevel)
            {
                expTotal = ExperienceForCurrentLevel - Experience;
            }

            if (sendMessage && expTotal > 0)
            {
                System.Globalization.NumberFormatInfo format = System.Globalization.NumberFormatInfo.InvariantInfo;
                string totalExpStr = expTotal.ToString("F0", format);
                string expCampBonusStr = "";
                string expGroupBonusStr = "";
                string expOutpostBonusStr = "";

                if (expCampBonus > 0)
                {
                    expCampBonusStr = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainExperience.CampBonus", expCampBonus.ToString("F0", format));
                }
                if (expGroupBonus > 0)
                {
                    expGroupBonusStr = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainExperience.GroupBonus", expGroupBonus.ToString("F0", format));
                }
                if (expOutpostBonus > 0)
                {
                    expOutpostBonusStr = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainExperience.OutpostBonus", expOutpostBonus.ToString("F0", format));
                }

                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainExperience.YouGet", totalExpStr) + expCampBonusStr + expGroupBonusStr, eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                if (bonusRenaissance > 0)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainExperience.BonusRenaissance", bonusRenaissance), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
                else if (bonusRenaissance < 0)
                {
                    Out.SendMessage("dont un malus renaissance de: " + Math.Abs(bonusRenaissance), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }

                if (territoryExp > 0)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainExperience.BonusTerritory", territoryExp), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }

                if (eventBonus > 0)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainExperience.BonusGameEvent", eventBonus), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }

            Experience += expTotal;

            if (expTotal >= 0)
            {
                //Level up
                if (Level >= 5 && !CharacterClass.HasAdvancedFromBaseClass())
                {
                    if (expTotal > 0)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainExperience.CannotRaise"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainExperience.TalkToTrainer"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                }
                else if (Level >= 40 && Level < MaxLevel && !IsLevelSecondStage && Experience >= ExperienceForCurrentLevelSecondStage)
                {
                    OnLevelSecondStage();
                    Notify(GamePlayerEvent.LevelSecondStage, this);
                }
                else if (Level < MaxLevel && Experience >= ExperienceForNextLevel)
                {
                    Level++;
                }
            }
            Out.SendUpdatePoints();
        }

        /// <summary>
        /// Get the total XP factor
        /// </summary>
        /// <returns></returns>
        public double GetXPFactor(bool enableZone = true, bool enableRenaissance = true)
        {
            double factor = 1.0d;
            
            if (enableZone && ServerProperties.Properties.ENABLE_ZONE_BONUSES && CurrentZone != null)
            {
                factor += (ZoneBonus.GetXPBonus(this) / 100.0d);
            }
            
            factor *= IsInRvR ? ServerProperties.Properties.RvR_XP_RATE : ServerProperties.Properties.XP_RATE;
            

            var toaBonus = GetModified(eProperty.XpPoints);
            factor += toaBonus != 0 ? toaBonus / 100.0d : 0;
            
            if (enableRenaissance && this.IsRenaissance)
            {
                factor *= Level < 40 ? 1.5d : 0.5d;
            }
            
            if (Guild != null)
            {
                factor += Guild.TerritoryBonusExperienceFactor * 100;
            }

            //catacombs characters get 50% boost if they are elligable for slash level
            switch ((eCharacterClass)CharacterClass.ID)
            {
                case eCharacterClass.Heretic:
                case eCharacterClass.Valkyrie:
                case eCharacterClass.Warlock:
                case eCharacterClass.Bainshee:
                case eCharacterClass.Vampiir:
                case eCharacterClass.MaulerAlb:
                case eCharacterClass.MaulerHib:
                case eCharacterClass.MaulerMid:
                    {
                        //we don't want to allow catacombs classes to use free levels and
                        //have a 50% bonus
                        if (!ServerProperties.Properties.ALLOW_CATA_SLASH_LEVEL && CanUseSlashLevel && Level < 20)
                        {
                            factor *= 1.5;
                        }
                        break;
                    }
            }
            
            return factor;
        }

        /// <summary>
        /// Gets or sets the level of the player
        /// (delegate to PlayerCharacter)
        /// </summary>
        public override byte Level
        {
            get { return DBCharacter != null ? (byte)DBCharacter.Level : base.Level; }
            set
            {
                int oldLevel = Level;
                base.Level = value;
                if (DBCharacter != null)
                    DBCharacter.Level = value;
                if (oldLevel > 0)
                {
                    if (value > oldLevel)
                    {
                        OnLevelUp(oldLevel);
                        Notify(GamePlayerEvent.LevelUp, this);
                    }
                    else
                    {
                        //update the mob colours
                        Out.SendLevelUpSound();
                    }
                }
            }
        }

        /// <summary>
        /// What is the base, unmodified level of this character.
        /// </summary>
        public override byte BaseLevel
        {
            get { return DBCharacter != null ? (byte)DBCharacter.Level : base.BaseLevel; }
        }

        /// <summary>
        /// What level is displayed to another player
        /// </summary>
        public override byte GetDisplayLevel(GamePlayer player)
        {
            return Math.Min((byte)50, Level);
        }

        /// <summary>
        /// Is this player in second stage of current level
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual bool IsLevelSecondStage
        {
            get { return DBCharacter != null ? DBCharacter.IsLevelSecondStage : false; }
            set { if (DBCharacter != null) DBCharacter.IsLevelSecondStage = value; }
        }

        /// <summary>
        /// Called when this player levels
        /// </summary>
        /// <param name="previouslevel"></param>
        public virtual void OnLevelUp(int previouslevel)
        {
            IsLevelSecondStage = false;

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.YouRaise", Level), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.YouAchieved", Level), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
            Out.SendPlayerFreeLevelUpdate();
            if (FreeLevelState == 2)
            {
                Out.SendDialogBox(eDialogCode.SimpleWarning, 0, 0, 0, 0, eDialogType.Ok, true, LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.FreeLevelEligible"));
            }

            if (Level == MaxLevel)
            {
                if (CanGenerateNews)
                {
                    string message = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.Reached", Name, Level, LastPositionUpdateZone.Description);
                    string newsmessage = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.Reached", Name, Level, LastPositionUpdateZone.Description);
                    NewsMgr.CreateNews(newsmessage, Realm, eNewsType.PvE, true);
                }
            }

            //level 20 changes realm title and gives 1 realm skill point
            if (Level == 20)
                GainRealmPoints(0);

            // Adjust stats
            bool statsChanged = false;
            // stat increases start at level 6
            if (Level > 5)
            {
                for (int i = Level; i > Math.Max(previouslevel, 5); i--)
                {
                    if (CharacterClass.PrimaryStat != eStat.UNDEFINED)
                    {
                        ChangeBaseStat(CharacterClass.PrimaryStat, 1);
                        statsChanged = true;
                    }
                    if (CharacterClass.SecondaryStat != eStat.UNDEFINED && ((i - 6) % 2 == 0))
                    { // base level to start adding stats is 6
                        ChangeBaseStat(CharacterClass.SecondaryStat, 1);
                        statsChanged = true;
                    }
                    if (CharacterClass.TertiaryStat != eStat.UNDEFINED && ((i - 6) % 3 == 0))
                    { // base level to start adding stats is 6
                        ChangeBaseStat(CharacterClass.TertiaryStat, 1);
                        statsChanged = true;
                    }
                }
            }

            if (statsChanged)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.StatRaise"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            CharacterClass.OnLevelUp(this, previouslevel);
            GameServer.ServerRules.OnPlayerLevelUp(this, previouslevel);
            RefreshSpecDependantSkills(true);

            // Echostorm - Code for display of new title on level up
            // Get old and current rank titles
            string currenttitle = CharacterClass.GetTitle(this, Level);

            // check for difference
            if (CharacterClass.GetTitle(this, previouslevel) != currenttitle)
            {
                // Inform player of new title.
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.AttainedRank", currenttitle), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            // spec points
            int specpoints = 0;
            for (int i = Level; i > previouslevel; i--)
            {
                if (i <= 5) specpoints += i; //start levels
                else specpoints += CharacterClass.SpecPointsMultiplier * i / 10; //spec levels
            }
            if (specpoints > 0)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.YouGetSpec", specpoints), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            // old hp
            int oldhp = CalculateMaxHealth(previouslevel, GetBaseStat(eStat.CON));

            // old power
            int oldpow = 0;
            if (CharacterClass.ManaStat != eStat.UNDEFINED)
            {
                oldpow = CalculateMaxMana(previouslevel, GetBaseStat(CharacterClass.ManaStat));
            }

            // hp upgrade
            int newhp = CalculateMaxHealth(Level, GetBaseStat(eStat.CON));
            if (oldhp > 0 && oldhp < newhp)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.HitsRaise", (newhp - oldhp)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            // power upgrade
            if (CharacterClass.ManaStat != eStat.UNDEFINED)
            {
                int newpow = CalculateMaxMana(Level, GetBaseStat(CharacterClass.ManaStat));
                if (newpow > 0 && oldpow < newpow)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.PowerRaise", (newpow - oldpow)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }

            if (IsAlive)
            {
                // workaround for starting regeneration
                StartHealthRegeneration();
                StartPowerRegeneration();
            }

            DeathCount = 0;

            if (Group != null)
            {
                Group.UpdateGroupWindow();
            }
            Out.SendUpdatePlayer(); // Update player level
            Out.SendCharStatsUpdate(); // Update Stats and MaxHitpoints
            Out.SendCharResistsUpdate();
            Out.SendUpdatePlayerSkills();
            Out.SendUpdatePoints();
            UpdatePlayerStatus();
            RefreshItemBonuses();
            UpdateEquipmentAppearance();
            Out.SendUpdateWeaponAndArmorStats();
            Out.SendStatusUpdate();

            // not sure what package this is, but it triggers the mob color update
            Out.SendLevelUpSound();

            // update color on levelup
            if (ObjectState == eObjectState.Active)
            {
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player == null) continue;
                    player.Out.SendEmoteAnimation(this, eEmote.LvlUp);
                }
            }

            // Reset taskDone per level.
            if (Task != null)
            {
                Task.TasksDone = 0;
                Task.SaveIntoDatabase();
            }
            
            //refresh npc quests according to new level
            this.RefreshQuestNPCs();
            
            // Level up pets and subpets
            if (DOL.GS.ServerProperties.Properties.PET_LEVELS_WITH_OWNER &&
                ControlledBrain is ControlledNpcBrain brain && brain.Body is GamePet pet)
            {
                if (pet.SetPetLevel())
                {
                    if (DOL.GS.ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL > 0 && pet.Spells.Count > 0)
                        pet.SortSpells();

                    brain.UpdatePetWindow();
                }

                // subpets
                if (pet.ControlledNpcList != null)
                    foreach (ABrain subBrain in pet.ControlledNpcList)
                        if (subBrain != null && subBrain.Body is GamePet subPet)
                            if (subPet.SetPetLevel()) // Levels up subpet
                                if (DOL.GS.ServerProperties.Properties.PET_SCALE_SPELL_MAX_LEVEL > 0)
                                    subPet.SortSpells();
            }

            // save player to database
            SaveIntoDatabase();
        }


        public void ApplyRenaissance()
        {
            IsLevelSecondStage = false;

            IsRenaissance = true;
            Experience = 0;

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnRenaissance"), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
            //Out.SendPlayerFreeLevelUpdate();	

            // stat increases start at level 6
            byte previouslevel = Level;
            this.RealmPoints = 0;
            Level = 1;
            this.TotalConstitutionLostAtDeath = 0;

            for (int i = previouslevel; i > 5; i--)
            {
                if (CharacterClass.PrimaryStat != eStat.UNDEFINED)
                {
                    ChangeBaseStat(CharacterClass.PrimaryStat, -1);
                }
                if (CharacterClass.SecondaryStat != eStat.UNDEFINED && ((i - 6) % 2 == 0))
                { // base level to start adding stats is 6
                    ChangeBaseStat(CharacterClass.SecondaryStat, -1);
                }
                if (CharacterClass.TertiaryStat != eStat.UNDEFINED && ((i - 6) % 3 == 0))
                { // base level to start adding stats is 6
                    ChangeBaseStat(CharacterClass.TertiaryStat, -1);
                }
            }

            RemoveAllStyles();
            RemoveAllSpecs();
            RemoveAllSpellLines();

            foreach (byte resist in Enum.GetValues(typeof(eResist)))
            {
                this.AbilityBonus[resist] = 0;
            }

            this.GetModified(eProperty.Strength);

            //CharacterClass.OnLevelUp(this, previouslevel);
            //GameServer.ServerRules.OnPlayerLevelUp(this, previouslevel);
            RefreshSpecDependantSkills(true);

            // Echostorm - Code for display of new title on level up
            // Get old and current rank titles
            string currenttitle = CharacterClass.GetTitle(this, Level);

            // check for difference
            if (CharacterClass.GetTitle(this, previouslevel) != currenttitle)
            {
                // Inform player of new title.
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.AttainedRank", currenttitle), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            // spec points
            int specpoints = 0;
            for (int i = Level; i < previouslevel; i++)
            {
                if (i <= 5) specpoints += i; //start levels
                else specpoints += CharacterClass.SpecPointsMultiplier * i / 10; //spec levels
            }

            if (IsAlive)
            {
                // workaround for starting regeneration
                StartHealthRegeneration();
                StartPowerRegeneration();
            }

            GainGuildMeritPoints(500);


            this.RealmLevel = 0;

            DeathCount = 0;

            if (Group != null)
            {
                Group.UpdateGroupWindow();
            }
            Out.SendUpdatePlayer(); // Update player level
            Out.SendCharStatsUpdate(); // Update Stats and MaxHitpoints
            Out.SendCharResistsUpdate();
            Out.SendUpdatePlayerSkills();
            Out.SendUpdatePoints();
            UpdatePlayerStatus();
            RefreshItemBonuses();
            UpdateEquipmentAppearance();
            Out.SendUpdateWeaponAndArmorStats();
            Out.SendStatusUpdate();

            // not sure what package this is, but it triggers the mob color update
            Out.SendLevelUpSound();

            // update color on levelup
            if (ObjectState == eObjectState.Active)
            {
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player == null) continue;
                    player.Out.SendEmoteAnimation(this, eEmote.LvlUp);
                }
            }

            // Expire Running Task
            if (Task != null)
            {
                Task.ExpireTask();
                Task.DeleteFromDatabase();
            }

            //Expire running Mission
            if (Mission != null)
            {
                Mission.ExpireMission();
            }

            //Abort all Quests
            this.QuestList.ForEach(q => q.AbortQuest());

            // save player to database
            SaveIntoDatabase();
        }

        /// <summary>
        /// Called when this player reaches second stage of the current level
        /// </summary>
        public virtual void OnLevelSecondStage()
        {
            IsLevelSecondStage = true;

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.SecondStage", Level), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

            // spec points
            int specpoints = CharacterClass.SpecPointsMultiplier * Level / 20;
            if (specpoints > 0)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnLevelUp.YouGetSpec", specpoints), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            //death penalty reset on mini-ding
            DeathCount = 0;

            if (Group != null)
            {
                Group.UpdateGroupWindow();
            }
            Out.SendUpdatePlayer(); // Update player level
            Out.SendCharStatsUpdate(); // Update Stats and MaxHitpoints
            Out.SendCharResistsUpdate();
            Out.SendUpdatePlayerSkills();
            Out.SendUpdatePoints();
            UpdatePlayerStatus();
            RefreshItemBonuses();
            UpdateEquipmentAppearance();
            Out.SendUpdateWeaponAndArmorStats();
            Out.SendStatusUpdate();
            // save player to database
            SaveIntoDatabase();
        }

        /// <summary>
        /// Calculate the Autotrain points.
        /// </summary>
        /// <param name="spec">Specialization</param>
        /// <param name="mode">various AT related calculations (amount of points, level of AT...)</param>
        public virtual int GetAutoTrainPoints(Specialization spec, int Mode)
        {
            int max_autotrain = Level / 4;
            if (max_autotrain == 0) max_autotrain = 1;

            foreach (string autotrainKey in CharacterClass.GetAutotrainableSkills())
            {
                if (autotrainKey == spec.KeyName)
                {
                    switch (Mode)
                    {
                        case 0:// return sum of all AT points in the spec
                            {
                                int pts_to_refund = Math.Min(max_autotrain, spec.Level);
                                return ((pts_to_refund * (pts_to_refund + 1) - 2) / 2);
                            }
                        case 1: // return max AT level + message
                            {
                                if (Level % 4 == 0)
                                    if (spec.Level >= max_autotrain)
                                        return max_autotrain;
                                    else
                                        Out.SendDialogBox(eDialogCode.SimpleWarning, 0, 0, 0, 0, eDialogType.Ok, true, LanguageMgr.GetTranslation(Client.Account.Language, "PlayerClass.OnLevelUp.Autotrain", spec.Name, max_autotrain));
                                return 0;
                            }
                        case 2: // return next free points due to AT change on levelup
                            {
                                if (spec.Level < max_autotrain)
                                    return (spec.Level + 1);
                                else
                                    return 0;
                            }
                        case 3: // return sum of all free AT points
                            {
                                if (spec.Level < max_autotrain)
                                    return (((max_autotrain * (max_autotrain + 1) - 2) / 2) - ((spec.Level * (spec.Level + 1) - 2) / 2));
                                else
                                    return ((max_autotrain * (max_autotrain + 1) - 2) / 2);
                            }
                        case 4: // spec is autotrainable
                            {
                                return 1;
                            }
                    }
                }
            }
            return 0;
        }
        #endregion

        #region Combat
        /// <summary>
        /// The time someone can hold a ranged attack before tiring
        /// </summary>
        internal const string RANGE_ATTACK_HOLD_START = " RangeAttackHoldStart";
        /// <summary>
        /// Endurance used for normal range attack
        /// </summary>
        public const int RANGE_ATTACK_ENDURANCE = 5;
        /// <summary>
        /// Endurance used for critical shot
        /// </summary>
        public const int CRITICAL_SHOT_ENDURANCE = 10;

        public bool CombatInfo = false;

        /// <summary>
        /// Holds the cancel style flag
        /// </summary>
        protected bool m_cancelStyle;

        /// <summary>
        /// Gets or Sets the cancel style flag
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual bool CancelStyle
        {
            get { return DBCharacter != null ? DBCharacter.CancelStyle : false; }
            set { if (DBCharacter != null) DBCharacter.CancelStyle = value; }
        }
        /// <summary>
        /// Decides which style living will use in this moment
        /// </summary>
        /// <returns>Style to use or null if none</returns>
        protected override Style GetStyleToUse(GameObject target)
        {
            InventoryItem weapon;
            if (NextCombatStyle == null) return null;
            if (NextCombatStyle.WeaponTypeRequirement == (int)eObjectType.Shield)
                weapon = Inventory.GetItem(eInventorySlot.LeftHandWeapon);
            else weapon = AttackWeapon;

            if (StyleProcessor.CanUseStyle(this, target, NextCombatStyle, weapon))
                return NextCombatStyle;

            if (NextCombatBackupStyle == null) return NextCombatStyle;

            return NextCombatBackupStyle;
        }

        /// <summary>
        /// Gets/Sets safety flag
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual bool SafetyFlag
        {
            get { return DBCharacter != null ? DBCharacter.SafetyFlag : false; }
            set { if (DBCharacter != null) DBCharacter.SafetyFlag = value; }
        }

        /// <summary>
        /// Sets/gets the living's cloak hood state
        /// (delegate to PlayerCharacter)
        /// </summary>
        public override bool IsCloakHoodUp
        {
            get { return DBCharacter != null ? DBCharacter.IsCloakHoodUp : base.IsCloakHoodUp; }
            set
            {
                //base.IsCloakHoodUp = value; // only needed if some special code will be added in base-property in future
                DBCharacter.IsCloakHoodUp = value;

                Out.SendInventoryItemsUpdate(null);
                UpdateEquipmentAppearance();

                if (value)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsCloakHoodUp.NowWear"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsCloakHoodUp.NoLongerWear"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
        }

        /// <summary>
        /// Sets/gets the living's cloak visible state
        /// (delegate to PlayerCharacter)
        /// </summary>
        public override bool IsCloakInvisible
        {
            get
            {
                return DBCharacter != null ? DBCharacter.IsCloakInvisible : base.IsCloakInvisible;
            }
            set
            {
                DBCharacter.IsCloakInvisible = value;

                Out.SendInventoryItemsUpdate(null);
                UpdateEquipmentAppearance();

                if (value)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsCloakInvisible.Invisible"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsCloakInvisible.Visible"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
        }

        /// <summary>
        /// Sets/gets the living's helm visible state
        /// (delegate to PlayerCharacter)
        /// </summary>
        public override bool IsHelmInvisible
        {
            get
            {
                return DBCharacter != null ? DBCharacter.IsHelmInvisible : base.IsHelmInvisible;
            }
            set
            {
                DBCharacter.IsHelmInvisible = value;

                Out.SendInventoryItemsUpdate(null);
                UpdateEquipmentAppearance();

                if (value)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsHelmInvisible.Invisible"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsHelmInvisible.Visible"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
        }

        /// <summary>
        /// Gets or sets the players SpellQueue option
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual bool SpellQueue
        {
            get { return DBCharacter != null ? DBCharacter.SpellQueue : false; }
            set { if (DBCharacter != null) DBCharacter.SpellQueue = value; }
        }


        /// <summary>
        /// Switches the active weapon to another one
        /// </summary>
        /// <param name="slot">the new eActiveWeaponSlot</param>
        public override void SwitchWeapon(eActiveWeaponSlot slot)
        {
            //When switching weapons, attackmode is removed!
            if (AttackState)
                StopAttack();

            if (CurrentSpellHandler != null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SwitchWeapon.SpellCancelled"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                StopCurrentSpellcast();
            }

            switch (slot)
            {
                case eActiveWeaponSlot.Standard:
                    // remove endurance remove handler
                    if (ActiveWeaponSlot == eActiveWeaponSlot.Distance)
                        GameEventMgr.RemoveHandler(this, GameLivingEvent.AttackFinished, new DOLEventHandler(RangeAttackHandler));
                    break;

                case eActiveWeaponSlot.TwoHanded:
                    // remove endurance remove handler
                    if (ActiveWeaponSlot == eActiveWeaponSlot.Distance)
                        GameEventMgr.RemoveHandler(this, GameLivingEvent.AttackFinished, new DOLEventHandler(RangeAttackHandler));
                    break;

                case eActiveWeaponSlot.Distance:
                    // add endurance remove handler
                    if (ActiveWeaponSlot != eActiveWeaponSlot.Distance)
                        GameEventMgr.AddHandler(this, GameLivingEvent.AttackFinished, new DOLEventHandler(RangeAttackHandler));
                    break;
            }



            InventoryItem[] oldActiveSlots = new InventoryItem[4];
            InventoryItem[] newActiveSlots = new InventoryItem[4];
            InventoryItem rightHandSlot = Inventory.GetItem(eInventorySlot.RightHandWeapon);
            InventoryItem leftHandSlot = Inventory.GetItem(eInventorySlot.LeftHandWeapon);
            InventoryItem twoHandSlot = Inventory.GetItem(eInventorySlot.TwoHandWeapon);
            InventoryItem distanceSlot = Inventory.GetItem(eInventorySlot.DistanceWeapon);

            // save old active weapons
            // simple active slot logic:
            // 0=right hand, 1=left hand, 2=two-hand, 3=range, F=none
            switch (VisibleActiveWeaponSlots & 0x0F)
            {
                case 0: oldActiveSlots[0] = rightHandSlot; break;
                case 2: oldActiveSlots[2] = twoHandSlot; break;
                case 3: oldActiveSlots[3] = distanceSlot; break;
            }
            if ((VisibleActiveWeaponSlots & 0xF0) == 0x10) oldActiveSlots[1] = leftHandSlot;


            base.SwitchWeapon(slot);


            // save new active slots
            switch (VisibleActiveWeaponSlots & 0x0F)
            {
                case 0: newActiveSlots[0] = rightHandSlot; break;
                case 2: newActiveSlots[2] = twoHandSlot; break;
                case 3: newActiveSlots[3] = distanceSlot; break;
            }
            if ((VisibleActiveWeaponSlots & 0xF0) == 0x10) newActiveSlots[1] = leftHandSlot;

            // unequip changed items
            for (int i = 0; i < 4; i++)
            {
                if (oldActiveSlots[i] != null && newActiveSlots[i] == null)
                    Notify(PlayerInventoryEvent.ItemUnequipped, Inventory, new ItemUnequippedArgs(oldActiveSlots[i], oldActiveSlots[i].SlotPosition));
            }

            // equip new active items
            for (int i = 0; i < 4; i++)
            {
                if (newActiveSlots[i] != null && oldActiveSlots[i] == null)
                    Notify(PlayerInventoryEvent.ItemEquipped, Inventory, new ItemEquippedArgs(newActiveSlots[i], newActiveSlots[i].SlotPosition));
            }

            if (ObjectState == eObjectState.Active)
            {
                //Send new wield info, no items updated
                Out.SendInventorySlotsUpdate(null);
                // Update active weapon appearence (has to be done with all
                // equipment in the packet else player is naked)
                UpdateEquipmentAppearance();
                //Send new weapon stats
                Out.SendUpdateWeaponAndArmorStats();
            }
        }

        /// <summary>
        /// Removes ammo and endurance on range attack
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        protected virtual void RangeAttackHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            AttackFinishedEventArgs args = arguments as AttackFinishedEventArgs;
            if (args == null) return;

            switch (args.AttackData.AttackResult)
            {
                case eAttackResult.HitUnstyled:
                case eAttackResult.Missed:
                case eAttackResult.Blocked:
                case eAttackResult.Parried:
                case eAttackResult.Evaded:
                case eAttackResult.HitStyle:
                case eAttackResult.Fumbled:
                    // remove an arrow and endurance
                    var recoveryChance = args.AttackData.Attacker.GetModified(eProperty.ArrowRecovery);
                    if (recoveryChance <= 0 || !Util.Chance(recoveryChance))
                    {
                        InventoryItem ammo = RangeAttackAmmo;
                        Inventory.RemoveCountFromStack(ammo, 1);
                    }

                    if (RangedAttackType == eRangedAttackType.Critical)
                        Endurance -= CRITICAL_SHOT_ENDURANCE;
                    else if (RangedAttackType == eRangedAttackType.RapidFire && GetAbilityLevel(Abilities.RapidFire) == 1)
                        Endurance -= 2 * RANGE_ATTACK_ENDURANCE;
                    else Endurance -= RANGE_ATTACK_ENDURANCE;
                    break;
            }
        }

        /// <summary>
        /// Starts a melee attack with this player
        /// </summary>
        /// <param name="attackTarget">the target to attack</param>
        public override void StartAttack(GameObject attackTarget)
        {
            if (CharacterClass.StartAttack(attackTarget) == false)
            {
                return;
            }

            if (!IsAlive)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.YouCantCombat"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            // Necromancer with summoned pet cannot attack
            if (ControlledBody is NecromancerPet)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.CantInShadeMode"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            if (this.PlayerAfkMessage != null)
            {
                this.ResetAFK(false);
            }

            if (IsStunned)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.CantAttackStunned"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }
            if (IsMezzed)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.CantAttackmesmerized"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            long vanishTimeout = TempProperties.getProperty<long>(VanishEffect.VANISH_BLOCK_ATTACK_TIME_KEY);
            if (vanishTimeout > 0 && vanishTimeout > CurrentRegion.Time)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.YouMustWaitAgain", (vanishTimeout - CurrentRegion.Time + 1000) / 1000), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            long VanishTick = this.TempProperties.getProperty<long>(VanishEffect.VANISH_BLOCK_ATTACK_TIME_KEY);
            long changeTime = this.CurrentRegion.Time - VanishTick;
            if (changeTime < 30000 && VanishTick > 0)
            {
                this.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.YouMustWait", ((30000 - changeTime) / 1000).ToString()), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            if (IsOnHorse)
                IsOnHorse = false;

            if (IsDisarmed)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.CantDisarmed"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            if (IsSitting)
            {
                Sit(false);
            }
            if (AttackWeapon == null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.CannotWithoutWeapon"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }
            if (AttackWeapon.Object_Type == (int)eObjectType.Instrument)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.CannotMelee"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            if (ActiveWeaponSlot == eActiveWeaponSlot.Distance)
            {
                if (ServerProperties.Properties.ALLOW_OLD_ARCHERY == false)
                {
                    if ((eCharacterClass)CharacterClass.ID == eCharacterClass.Scout || (eCharacterClass)CharacterClass.ID == eCharacterClass.Hunter || (eCharacterClass)CharacterClass.ID == eCharacterClass.Ranger)
                    {
                        // There is no feedback on live when attempting to fire a bow with arrows
                        return;
                    }
                }

                // Check arrows for ranged attack
                if (RangeAttackAmmo == null)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.SelectQuiver"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                    return;
                }
                // Check if selected ammo is compatible for ranged attack
                if (!CheckRangedAmmoCompatibilityWithActiveWeapon())
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.CantUseQuiver"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                    return;
                }

                lock (EffectList)
                {
                    foreach (IGameEffect effect in EffectList) // switch to the correct range attack type
                    {
                        if (effect is SureShotEffect)
                        {
                            RangedAttackType = eRangedAttackType.SureShot;
                            break;
                        }

                        if (effect is RapidFireEffect)
                        {
                            RangedAttackType = eRangedAttackType.RapidFire;
                            break;
                        }

                        if (effect is TrueshotEffect)
                        {
                            RangedAttackType = eRangedAttackType.Long;
                            break;
                        }
                    }
                }

                if (RangedAttackType == eRangedAttackType.Critical && Endurance < CRITICAL_SHOT_ENDURANCE)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.TiredShot"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (Endurance < RANGE_ATTACK_ENDURANCE)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.TiredUse", AttackWeapon.Name), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (IsStealthed)
                {
                    // -Chance to unstealth while nocking an arrow = stealth spec / level
                    // -Chance to unstealth nocking a crit = stealth / level  0.20
                    int stealthSpec = GetModifiedSpecLevel(Specs.Stealth);
                    int stayStealthed = stealthSpec * 100 / Level;
                    if (RangedAttackType == eRangedAttackType.Critical)
                        stayStealthed -= 20;

                    if (!Util.Chance(stayStealthed))
                        Stealth(false);
                }
            }
            else
            {
                if (attackTarget == null)
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.CombatNoTarget"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                else
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.CombatTarget", GetPersonalizedName(attackTarget)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                }
            }

            if (CharacterClass is PlayerClass.ClassVampiir)
            {
                GameSpellEffect removeEffect = SpellHandler.FindEffectOnTarget(this, "VampiirSpeedEnhancement");
                if (removeEffect != null)
                    removeEffect.Cancel(false);
            }
            else
            {
                // Bard RR5 ability must drop when the player starts a melee attack
                IGameEffect DreamweaverRR5 = EffectList.GetOfType<DreamweaverEffect>();
                if (DreamweaverRR5 != null)
                    DreamweaverRR5.Cancel(false);
            }
            base.StartAttack(attackTarget);

            if (IsCasting && !m_runningSpellHandler.Spell.Uninterruptible && m_runningSpellHandler is not StyleHandler)
            {
                StopCurrentSpellcast();
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.SpellCancelled"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
            }

            //Clear styles
            NextCombatStyle = null;
            NextCombatBackupStyle = null;

            if (ActiveWeaponSlot != eActiveWeaponSlot.Distance)
            {
                Out.SendAttackMode(AttackState);
            }
            else
            {
                TempProperties.setProperty(RANGE_ATTACK_HOLD_START, 0L);

                string typeMsg = "shot";
                if (AttackWeapon.Object_Type == (int)eObjectType.Thrown)
                    typeMsg = "throw";

                string targetMsg = "";
                if (attackTarget != null)
                {
                    if (this.IsWithinRadius(attackTarget, AttackRange))
                        targetMsg = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.TargetInRange");
                    else
                        targetMsg = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.TargetOutOfRange");
                }

                int speed = AttackSpeed(AttackWeapon) / 100;
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.StartAttack.YouPrepare", typeMsg, speed / 10, speed % 10, targetMsg), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
            }
        }

        /// <summary>
        /// Stops all attacks this player is making
        /// </summary>
        /// <param name="forced">Is this a forced stop or is the client suggesting we stop?</param>
        public override void StopAttack(bool forced)
        {
            NextCombatStyle = null;
            NextCombatBackupStyle = null;
            base.StopAttack(forced);
            if (IsAlive)
            {
                Out.SendAttackMode(AttackState);
            }
        }


        /// <summary>
        /// Switches the active quiver slot to another one
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="forced"></param>
        public virtual void SwitchQuiver(eActiveQuiverSlot slot, bool forced)
        {
            if (slot != eActiveQuiverSlot.None)
            {
                eInventorySlot updatedSlot = eInventorySlot.Invalid;
                if ((slot & eActiveQuiverSlot.Fourth) > 0)
                    updatedSlot = eInventorySlot.FourthQuiver;
                else if ((slot & eActiveQuiverSlot.Third) > 0)
                    updatedSlot = eInventorySlot.ThirdQuiver;
                else if ((slot & eActiveQuiverSlot.Second) > 0)
                    updatedSlot = eInventorySlot.SecondQuiver;
                else if ((slot & eActiveQuiverSlot.First) > 0)
                    updatedSlot = eInventorySlot.FirstQuiver;

                if (Inventory.GetItem(updatedSlot) != null && (ActiveQuiverSlot != slot || forced))
                {
                    ActiveQuiverSlot = slot;
                    //GameObjects.GamePlayer.SwitchQuiver.ShootWith:		You will shoot with: {0}.
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SwitchQuiver.ShootWith", Inventory.GetItem(updatedSlot).GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    ActiveQuiverSlot = eActiveQuiverSlot.None;
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SwitchQuiver.NoMoreAmmo"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }

                Out.SendInventorySlotsUpdate(new int[] { (int)updatedSlot });
            }
            else
            {
                if (Inventory.GetItem(eInventorySlot.FirstQuiver) != null)
                    SwitchQuiver(eActiveQuiverSlot.First, true);
                else if (Inventory.GetItem(eInventorySlot.SecondQuiver) != null)
                    SwitchQuiver(eActiveQuiverSlot.Second, true);
                else if (Inventory.GetItem(eInventorySlot.ThirdQuiver) != null)
                    SwitchQuiver(eActiveQuiverSlot.Third, true);
                else if (Inventory.GetItem(eInventorySlot.FourthQuiver) != null)
                    SwitchQuiver(eActiveQuiverSlot.Fourth, true);
                else
                {
                    ActiveQuiverSlot = eActiveQuiverSlot.None;
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SwitchQuiver.NotUseQuiver"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                Out.SendInventorySlotsUpdate(null);
            }
        }

        /// <summary>
        /// Check the selected range ammo and decides if it's compatible with select weapon
        /// </summary>
        /// <returns>True if compatible, false if not</returns>
        protected virtual bool CheckRangedAmmoCompatibilityWithActiveWeapon()
        {
            InventoryItem weapon = AttackWeapon;
            if (weapon != null)
            {
                switch ((eObjectType)weapon.Object_Type)
                {
                    case eObjectType.Crossbow:
                    case eObjectType.Longbow:
                    case eObjectType.CompositeBow:
                    case eObjectType.RecurvedBow:
                    case eObjectType.Fired:
                        {
                            if (ActiveQuiverSlot != eActiveQuiverSlot.None)
                            {
                                InventoryItem ammo = null;
                                switch (ActiveQuiverSlot)
                                {
                                    case eActiveQuiverSlot.Fourth: ammo = Inventory.GetItem(eInventorySlot.FourthQuiver); break;
                                    case eActiveQuiverSlot.Third: ammo = Inventory.GetItem(eInventorySlot.ThirdQuiver); break;
                                    case eActiveQuiverSlot.Second: ammo = Inventory.GetItem(eInventorySlot.SecondQuiver); break;
                                    case eActiveQuiverSlot.First: ammo = Inventory.GetItem(eInventorySlot.FirstQuiver); break;
                                }

                                if (ammo == null) return false;

                                if (weapon.Object_Type == (int)eObjectType.Crossbow)
                                    return ammo.Object_Type == (int)eObjectType.Bolt;
                                return ammo.Object_Type == (int)eObjectType.Arrow;
                            }
                        }
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Holds the arrows for next range attack
        /// </summary>
        protected WeakReference m_rangeAttackAmmo;

        /// <summary>
        /// Gets/Sets the item that is used for ranged attack
        /// </summary>
        /// <returns>Item that will be used for range/accuracy/damage modifications</returns>
        protected override InventoryItem RangeAttackAmmo
        {
            get
            {
                //TODO: ammo should be saved on start of every range attack and used here
                InventoryItem ammo = null;//(InventoryItem)m_rangeAttackArrows.Target;

                InventoryItem weapon = AttackWeapon;
                if (weapon != null)
                {
                    switch (weapon.Object_Type)
                    {
                        case (int)eObjectType.Thrown: ammo = Inventory.GetItem(eInventorySlot.DistanceWeapon); break;
                        case (int)eObjectType.Crossbow:
                        case (int)eObjectType.Longbow:
                        case (int)eObjectType.CompositeBow:
                        case (int)eObjectType.RecurvedBow:
                        case (int)eObjectType.Fired:
                            {
                                switch (ActiveQuiverSlot)
                                {
                                    case eActiveQuiverSlot.First: ammo = Inventory.GetItem(eInventorySlot.FirstQuiver); break;
                                    case eActiveQuiverSlot.Second: ammo = Inventory.GetItem(eInventorySlot.SecondQuiver); break;
                                    case eActiveQuiverSlot.Third: ammo = Inventory.GetItem(eInventorySlot.ThirdQuiver); break;
                                    case eActiveQuiverSlot.Fourth: ammo = Inventory.GetItem(eInventorySlot.FourthQuiver); break;
                                    case eActiveQuiverSlot.None:
                                        eObjectType findType = eObjectType.Arrow;
                                        if (weapon.Object_Type == (int)eObjectType.Crossbow)
                                            findType = eObjectType.Bolt;

                                        ammo = Inventory.GetFirstItemByObjectType((int)findType, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);

                                        break;
                                }
                            }
                            break;
                    }
                }

                return ammo;
            }
            set { m_rangeAttackAmmo.Target = value; }
        }

        /// <summary>
        /// Holds the target for next range attack
        /// </summary>
        protected WeakReference m_rangeAttackTarget;

        /// <summary>
        /// Gets/Sets the target for current ranged attack
        /// </summary>
        /// <returns></returns>
        protected override GameObject RangeAttackTarget
        {
            get
            {
                GameObject target = (GameObject)m_rangeAttackTarget.Target;
                if (target == null || target.ObjectState != eObjectState.Active)
                    target = TargetObject;
                return target;
            }
            set { m_rangeAttackTarget.Target = value; }
        }

        /// <summary>
        /// Check the range attack state and decides what to do
        /// Called inside the AttackTimerCallback
        /// </summary>
        /// <returns></returns>
        protected override eCheckRangeAttackStateResult CheckRangeAttackState(GameObject target)
        {
            long holdStart = TempProperties.getProperty<long>(RANGE_ATTACK_HOLD_START);
            if (holdStart == 0)
            {
                holdStart = CurrentRegion.Time;
                TempProperties.setProperty(RANGE_ATTACK_HOLD_START, holdStart);
            }
            if ((CurrentRegion.Time - holdStart) > 15000 && AttackWeapon.Object_Type != (int)eObjectType.Crossbow)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.TooTired"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return eCheckRangeAttackStateResult.Stop; //Stop the attack
            }

            //This state is set when the player wants to fire!
            if (RangedAttackState == eRangedAttackState.Fire
                || RangedAttackState == eRangedAttackState.AimFire
                || RangedAttackState == eRangedAttackState.AimFireReload)
            {
                RangeAttackTarget = null; // clean the RangeAttackTarget at the first shot try even if failed

                if (target == null || !(target is GameLiving))
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "System.MustSelectTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else if (!this.IsWithinRadius(target, AttackRange))
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.TooFarAway", GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else if (!TargetInView)  // TODO : wrong, must be checked with the target parameter and not with the targetObject
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.CantSeeTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else if (!IsObjectInFront(target, 90))
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.NotInView", GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else if (RangeAttackAmmo == null)
                {
                    //another check for ammo just before firing
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.MustSelectQuiver"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                }
                else if (!CheckRangedAmmoCompatibilityWithActiveWeapon())
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.CantUseQuiver"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                }
                else if (GameServer.ServerRules.IsAllowedToAttack(this, (GameLiving)target, false))
                {
                    GameLiving living = target as GameLiving;
                    if (RangedAttackType == eRangedAttackType.Critical && living != null
                        && (living.CurrentSpeed > 90 //walk speed == 85, hope that's what they mean
                            || (living.AttackState && living.InCombat) //maybe not 100% correct
                            || SpellHandler.FindEffectOnTarget(living, "Mesmerize") != null
                           ))
                    {
                        /*
                         * http://rothwellhome.org/guides/archery.htm
                         * Please note that critical shot will work against targets that are:
                         * sitting, standing still (which includes standing in combat mode but
                         * not actively swinging at something), walking, moving backwards,
                         * strafing, or casting a spell. Critical shot will not work against
                         * targets that are: running, in active combat (swinging at something),
                         * or mezzed. Stunned targets may be critical shot once any timers from
                         * active combat have expired if they are not yet free to act; i.e.:
                         * they may not be critical shot until their weapon delay timer has run
                         * out after their last attack, they may be critical shot during the
                         * period between the weapon delay running out and the stun wearing off,
                         * and they may not be critical shot once they have begun swinging again.
                         * If the target was in melee with an archer, the critical shot may not
                         * be drawn against them until after their weapon delay has run out or it
                         * will be interrupted.  This means that the scout's shield stun is much
                         * less effective against large weapon wielders (who have longer weapon
                         * delays) than against fast piercing/thrusting weapon wielders.
                         */

                        // TODO: more checks?
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.CantCritical"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        RangedAttackType = eRangedAttackType.Normal;
                    }
                    return eCheckRangeAttackStateResult.Fire;
                }

                RangedAttackState = eRangedAttackState.ReadyToFire;
                return eCheckRangeAttackStateResult.Hold;
            }

            //Player is aiming
            if (RangedAttackState == eRangedAttackState.Aim)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.ReadyToFire"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                RangedAttackState = eRangedAttackState.ReadyToFire;
                return eCheckRangeAttackStateResult.Hold;
            }
            else if (RangedAttackState == eRangedAttackState.ReadyToFire)
            {
                return eCheckRangeAttackStateResult.Hold; //Hold the shot
            }
            return eCheckRangeAttackStateResult.Fire;
        }

        /// <summary>
        /// Send the messages to the GamePlayer
        /// </summary>
        /// <param name="ad"></param>
        protected override void SendAttackingCombatMessages(AttackData ad)
        {
            base.SendAttackingCombatMessages(ad);
            GameObject target = ad.Target;
            InventoryItem weapon = ad.Weapon;
            if (ad.Target is GameNPC)
            {
                switch (ad.AttackResult)
                {
                    case eAttackResult.TargetNotVisible:
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.NotInView",
                        ad.Target.GetName(0, true, Client.Account.Language, (ad.Target as GameNPC))), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.OutOfRange:
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.TooFarAway",
                        ad.Target.GetName(0, true, Client.Account.Language, (ad.Target as GameNPC))), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.TargetDead:
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.AlreadyDead",
                        ad.Target.GetName(0, true, Client.Account.Language, (ad.Target as GameNPC))), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Blocked:
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Blocked",
                        ad.Target.GetName(0, true, Client.Account.Language, (ad.Target as GameNPC))), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Parried:
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Parried",
                        ad.Target.GetName(0, true, Client.Account.Language, (ad.Target as GameNPC))), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Evaded:
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Evaded",
                        ad.Target.GetName(0, true, Client.Account.Language, (ad.Target as GameNPC))), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.NoTarget: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.NeedTarget"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.NoValidTarget: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.CantBeAttacked"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Missed: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Miss"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Fumbled: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Fumble"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.HitStyle:
                    case eAttackResult.HitUnstyled:
                        string modmessage = "";
                        if (ad.Modifier > 0) modmessage = " (+" + ad.Modifier + ")";
                        if (ad.Modifier < 0) modmessage = " (" + ad.Modifier + ")";

                        string hitWeapon = "";

                        switch (Client.Account.Language)
                        {
                            case "DE":
                                if (weapon != null)
                                    hitWeapon = weapon.Name;
                                break;
                            default:
                                if (weapon != null)
                                    hitWeapon = GlobalConstants.NameToShortName(weapon.Name);
                                break;
                        }

                        if (hitWeapon.Length > 0)
                            hitWeapon = " " + LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.WithYour") + " " + hitWeapon;

                        string attackTypeMsg = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.YouAttack");
                        if (ActiveWeaponSlot == eActiveWeaponSlot.Distance)
                            attackTypeMsg = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.YouShot");

                        // intercept messages
                        if (target != null && target != ad.Target)
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Intercepted", GetPersonalizedName(ad.Target), GetPersonalizedName(target)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.InterceptedHit", attackTypeMsg, target.GetName(0, false), hitWeapon, GetPersonalizedName(ad.Target), ad.Damage, modmessage), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                        }
                        else
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.InterceptHit", attackTypeMsg,
                                ad.Target.GetName(0, false, Client.Account.Language, (ad.Target as GameNPC)), hitWeapon, ad.Damage, modmessage), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);

                        // critical hit
                        if (ad.CriticalDamage > 0)
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Critical",
                                ad.Target.GetName(0, false, Client.Account.Language, (ad.Target as GameNPC)), ad.CriticalDamage), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                        break;
                }
            }
            else
            {
                switch (ad.AttackResult)
                {
                    case eAttackResult.TargetNotVisible: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.NotInView", GetPersonalizedName(ad.Target)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.OutOfRange: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.TooFarAway", GetPersonalizedName(ad.Target)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.TargetDead: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.AlreadyDead", GetPersonalizedName(ad.Target)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Blocked: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Blocked", GetPersonalizedName(ad.Target)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Parried: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Parried", GetPersonalizedName(ad.Target)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Evaded: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Evaded", GetPersonalizedName(ad.Target)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.NoTarget: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.NeedTarget"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.NoValidTarget: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.CantBeAttacked"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Missed: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Miss"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.Fumbled: Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Fumble"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow); break;
                    case eAttackResult.HitStyle:
                    case eAttackResult.HitUnstyled:
                        string modmessage = "";
                        if (ad.Modifier > 0) modmessage = " (+" + ad.Modifier + ")";
                        if (ad.Modifier < 0) modmessage = " (" + ad.Modifier + ")";

                        string hitWeapon = "";

                        switch (Client.Account.Language)
                        {
                            case "DE":
                                if (weapon != null)
                                    hitWeapon = weapon.Name;
                                break;
                            default:
                                if (weapon != null)
                                    hitWeapon = GlobalConstants.NameToShortName(weapon.Name);
                                break;
                        }

                        if (hitWeapon.Length > 0)
                            hitWeapon = " " + LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.WithYour") + " " + hitWeapon;

                        string attackTypeMsg = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.YouAttack");
                        if (ActiveWeaponSlot == eActiveWeaponSlot.Distance)
                            attackTypeMsg = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.YouShot");

                        // intercept messages
                        if (target != null && target != ad.Target)
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Intercepted", GetPersonalizedName(ad.Target), GetPersonalizedName(target)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.InterceptedHit", attackTypeMsg, GetPersonalizedName(target), hitWeapon, GetPersonalizedName(ad.Target), ad.Damage, modmessage), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                        }
                        else
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.InterceptHit", attackTypeMsg, GetPersonalizedName(ad.Target), hitWeapon, ad.Damage, modmessage), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);

                        // critical hit
                        if (ad.CriticalDamage > 0)
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Critical", GetPersonalizedName(ad.Target), ad.CriticalDamage), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                        break;
                }
            }

            if (Client.Account.PrivLevel > 1 && ServerProperties.Properties.ENABLE_DEBUG)
            {
                SendAttackDetails(ad);
            }
        }

        /// <summary>
        /// Called whenever a single attack strike is made
        /// </summary>
        /// <param name="target"></param>
        /// <param name="weapon"></param>
        /// <param name="style"></param>
        /// <param name="effectiveness"></param>
        /// <param name="interruptDuration"></param>
        /// <param name="dualWield"></param>
        /// <returns></returns>
        protected override AttackData MakeAttack(GameObject target, InventoryItem weapon, Style style, double effectiveness, int interruptDuration, bool dualWield, bool ignoreLOS, bool isCounterAttack)
        {
            if (IsCrafting)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.InterruptedCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                StopCrafting();
            }

            AttackData ad = base.MakeAttack(target, weapon, style, effectiveness, interruptDuration, dualWield, ignoreLOS, isCounterAttack);

            if (CombatInfo)
                Out.SendMessage($"CombatInfo: result:{ad.AttackResult} dmg={ad.Damage} crits={ad.CriticalDamage} style dmg={ad.StyleDamage} uncap dmg={ad.UncappedDamage} weapon dmg={ad.weaponDamage:F1} mod={ad.dmgMod:F1}", eChatType.CT_System, eChatLoc.CL_SystemWindow);

            //Clear the styles for the next round!
            NextCombatStyle = null;
            NextCombatBackupStyle = null;

            switch (ad.AttackResult)
            {
                case eAttackResult.HitStyle:
                case eAttackResult.HitUnstyled:
                    {
                        //keep component
                        if ((ad.Target is GameKeepComponent || ad.Target is GameKeepDoor || ad.Target is GameSiegeWeapon) && ad.Attacker is GamePlayer && ad.Attacker.GetModified(eProperty.KeepDamage) > 0)
                        {
                            int keepdamage = (int)Math.Floor((double)ad.Damage * ((double)ad.Attacker.GetModified(eProperty.KeepDamage) / 100));
                            int keepstyle = (int)Math.Floor((double)ad.StyleDamage * ((double)ad.Attacker.GetModified(eProperty.KeepDamage) / 100));
                            ad.Damage += keepdamage;
                            ad.StyleDamage += keepstyle;
                        }
                        // vampiir
                        if (CharacterClass is PlayerClass.ClassVampiir
                            && target is GameKeepComponent == false
                            && target is GameKeepDoor == false
                            && target is GameSiegeWeapon == false)
                        {
                            int perc = Convert.ToInt32(((double)(ad.Damage + ad.CriticalDamage) / 100) * (55 - this.Level));
                            perc = (perc < 1) ? 1 : ((perc > 15) ? 15 : perc);
                            this.Mana += Convert.ToInt32(Math.Ceiling(((Decimal)(perc * this.MaxMana) / 100)));
                        }

                        //only miss when strafing when attacking a player
                        //30% chance to miss
                        if (IsStrafing && ad.Target is GamePlayer && Util.Chance(30))
                        {
                            ad.MissChance = 30;
                            ad.AttackResult = eAttackResult.Missed;
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.StrafMiss"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                            break;
                        }
                        // if hit add effect for ranged
                        if (ActiveWeaponSlot == eActiveWeaponSlot.Distance)
                        {
                            // for weapon
                            if (AttackWeapon != null)
                                CheckWeaponMagicalEffect(ad, AttackWeapon);
                            // for ammo
                            if (RangeAttackAmmo != null)
                                CheckWeaponMagicalEffect(ad, RangeAttackAmmo);
                        }
                        break;
                    }
            }

            switch (ad.AttackResult)
            {
                case eAttackResult.Blocked:
                case eAttackResult.Fumbled:
                case eAttackResult.HitStyle:
                case eAttackResult.HitUnstyled:
                case eAttackResult.Missed:
                case eAttackResult.Parried:
                    //Condition percent can reach 70%
                    //durability percent can reach zero
                    // if item durability reachs 0, item is useless and become broken item

                    if (weapon != null && weapon is GameInventoryItem)
                    {
                        (weapon as GameInventoryItem)!.OnStrikeTarget(this, target);
                    }
                    //Camouflage - Camouflage will be disabled only when attacking a GamePlayer or ControlledNPC of a GamePlayer.
                    if (HasAbility(Abilities.Camouflage) && target is GamePlayer || (target.GetPlayerOwner() != null))
                    {
                        CamouflageEffect camouflage = EffectList.GetOfType<CamouflageEffect>();

                        if (camouflage != null)// Check if Camo is active, if true, cancel ability.
                        {
                            camouflage.Cancel(false);
                        }
                        Skill camo = SkillBase.GetAbility(Abilities.Camouflage); // now we find the ability
                        DisableSkill(camo, CamouflageSpecHandler.DISABLE_DURATION); // and here we disable it.
                    }

                    // Multiple Hit check
                    if (ad.AttackResult == eAttackResult.HitStyle)
                    {
                        byte numTargetsCanHit = 0;
                        int random;
                        IList extraTargets = new ArrayList();
                        IList listAvailableTargets = new ArrayList();
                        InventoryItem attackWeapon = AttackWeapon;
                        InventoryItem leftWeapon = (Inventory == null) ? null : Inventory.GetItem(eInventorySlot.LeftHandWeapon);
                        switch (style.ID)
                        {
                            case 374: numTargetsCanHit = 1; break; //Tribal Assault:   Hits 2 targets
                            case 377: numTargetsCanHit = 1; break; //Clan's Might:      Hits 2 targets
                            case 379: numTargetsCanHit = 2; break; //Totemic Wrath:      Hits 3 targets
                            case 384: numTargetsCanHit = 3; break; //Totemic Sacrifice:   Hits 4 targets
                            case 600: numTargetsCanHit = 255; break; //Shield Swipe: No Cap on Targets
                            default: numTargetsCanHit = 0; break; //For others;
                        }
                        if (numTargetsCanHit > 0)
                        {
                            if (style.ID != 600) // Not Shield Swipe
                            {
                                foreach (GamePlayer pl in GetPlayersInRadius(false, (ushort)AttackRange))
                                {
                                    if (pl == null) continue;
                                    if (GameServer.ServerRules.IsAllowedToAttack(this, pl, true))
                                    {
                                        listAvailableTargets.Add(pl);
                                    }
                                }
                                foreach (GameNPC npc in GetNPCsInRadius(false, (ushort)AttackRange))
                                {
                                    if (GameServer.ServerRules.IsAllowedToAttack(this, npc, true))
                                    {
                                        listAvailableTargets.Add(npc);
                                    }
                                }

                                // remove primary target
                                listAvailableTargets.Remove(target);
                                numTargetsCanHit = (byte)Math.Min(numTargetsCanHit, listAvailableTargets.Count);

                                if (listAvailableTargets.Count > 1)
                                {
                                    while (extraTargets.Count < numTargetsCanHit)
                                    {
                                        random = Util.Random(listAvailableTargets.Count - 1);
                                        if (!extraTargets.Contains(listAvailableTargets[random]))
                                            extraTargets.Add(listAvailableTargets[random] as GameObject);
                                    }
                                    foreach (GameObject obj in extraTargets)
                                    {
                                        if (obj is GamePlayer && ((GamePlayer)obj).IsSitting)
                                        {
                                            effectiveness *= 2;
                                        }
                                        new WeaponOnTargetAction(this, obj as GameObject, attackWeapon, leftWeapon, effectiveness, AttackSpeed(attackWeapon), null).Start(1);  // really start the attack
                                    }
                                }
                            }
                            else // shield swipe
                            {
                                foreach (GameNPC npc in GetNPCsInRadius(false, (ushort)AttackRange))
                                {
                                    if (GameServer.ServerRules.IsAllowedToAttack(this, npc, true))
                                    {
                                        listAvailableTargets.Add(npc);
                                    }
                                }

                                listAvailableTargets.Remove(target);
                                numTargetsCanHit = (byte)Math.Min(numTargetsCanHit, listAvailableTargets.Count);

                                if (listAvailableTargets.Count > 1)
                                {
                                    while (extraTargets.Count < numTargetsCanHit)
                                    {
                                        random = Util.Random(listAvailableTargets.Count - 1);
                                        if (!extraTargets.Contains(listAvailableTargets[random]))
                                        {
                                            extraTargets.Add(listAvailableTargets[random] as GameObject);
                                        }
                                    }
                                    foreach (GameNPC obj in extraTargets)
                                    {
                                        if (obj != ad.Target)
                                        {
                                            this.MakeAttack(obj, attackWeapon, null, 1, ServerProperties.Properties.SPELL_INTERRUPT_DURATION, false, false);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
            }
            return ad;
        }


        /// <summary>
        /// Try and execute a weapon style
        /// </summary>
        /// <param name="style"></param>
        public virtual void ExecuteWeaponStyle(Style style)
        {
            StyleProcessor.TryToUseStyle(this, TargetObject, style);
        }

        /// <summary>
        /// Calculates melee critical damage of this player
        /// </summary>
        /// <param name="ad">The attack data</param>
        /// <param name="weapon">The weapon used</param>
        /// <returns>The amount of critical damage</returns>
        public override int GetMeleeCriticalDamage(AttackData ad, InventoryItem weapon)
        {
            if (Util.Chance(ad.criticalChance))
            {
                // triple wield prevents critical hits
                if (ad.Target.EffectList.GetOfType<TripleWieldEffect>() != null) return 0;

                int critMin;
                int critMax;
                BerserkEffect berserk = EffectList.GetOfType<BerserkEffect>();

                if (berserk != null)
                {
                    int level = GetAbilityLevel(Abilities.Berserk);
                    // According to : http://daoc.catacombs.com/forum.cfm?ThreadKey=10833&DefMessage=922046&forum=37
                    // Zerk 1 = 1-25%
                    // Zerk 2 = 1-50%
                    // Zerk 3 = 1-75%
                    // Zerk 4 = 1-99%
                    critMin = (int)(0.01 * ad.Damage);
                    critMax = (int)(Math.Min(0.99, (level * 0.25)) * ad.Damage);
                }
                else
                {
                    //think min crit dmage is 10% of damage
                    critMin = ad.Damage / 10;
                    // Critical damage to players is 50%, low limit should be around 20% but not sure
                    // zerkers in Berserk do up to 99%
                    if (ad.Target is GamePlayer)
                        critMax = ad.Damage >> 1;
                    else
                        critMax = ad.Damage;
                }
                critMin = Math.Max(critMin, 0);
                critMax = Math.Max(critMin, critMax);

                return Util.Random(critMin, critMax);
            }
            return 0;
        }

        /// <inheritdoc />
        protected override void CounterAttack(GameLiving attacker)
        {
            if (!IsAlive || IsStunned || IsMezzed || IsDisarmed || IsFrozen || IsCrafting || IsClimbing || IsStrafing || (Steed != null))
                return;

            if (CounterAttackStyle == null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client, "GameObjects.GamePlayer.Attack.CounterAttackNoStyle"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
                
            InventoryItem weapon = AttackWeapon;
                
            if (!StyleProcessor.CanUseStyle(this, attacker, CounterAttackStyle, weapon))
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client, "GameObjects.GamePlayer.Attack.CounterAttackInvalidWeap"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!AttackState && StyleProcessor.CanUseStyle(this, attacker, CounterAttackStyle, weapon) && CounterAttackStyle != null)
            {
                StartAttack(attacker);
            }

            TurnTo(attacker.Coordinate);
            Out.SendPlayerPositionAndObjectID();
            new WeaponOnTargetAction(this, attacker, weapon, null, 1.0, 0, CounterAttackStyle).OnTick();

            if (attacker is GamePlayer targetPlayer)
            {
                targetPlayer.Out.SendMessage(LanguageMgr.GetTranslation(targetPlayer.Client, "GameObjects.GamePlayer.Attack.CounterAttackByEnemy", GetPersonalizedName(attacker)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
            Out.SendMessage(LanguageMgr.GetTranslation(Client, "GameObjects.GamePlayer.Attack.CounterAttackSuccess", CounterAttackStyle!.Name), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
        }

        /// <summary>
        /// This method is called at the end of the attack sequence to
        /// notify objects if they have been attacked/hit by an attack
        /// </summary>
        /// <param name="ad">information about the attack</param>
        public override void OnAttackedByEnemy(AttackData ad)
        {
            if (IsOnHorse && ad.IsHit)
                IsOnHorse = false;
            base.OnAttackedByEnemy(ad);

            switch (ad.AttackResult)
            {
                // is done in game living because of guard
                //case eAttackResult.Blocked : Out.SendMessage(ad.Attacker.GetName(0, true) + " attacks you and you block the blow!", eChatType.CT_Missed, eChatLoc.CL_SystemWindow); break;
                case eAttackResult.Parried:
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Parry", GetPersonalizedName(ad.Attacker)), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
                    break;
                case eAttackResult.Evaded:
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Evade", GetPersonalizedName(ad.Attacker)), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
                    break;
                case eAttackResult.Fumbled:
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Fumbled", GetPersonalizedName(ad.Attacker)), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
                    break;
                case eAttackResult.Missed:
                    if (ad.AttackType == AttackData.eAttackType.Spell)
                        break;
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Missed", GetPersonalizedName(ad.Attacker))
                                    + (ad.MissChance > 0 ? " (" + (int)(ad.MissChance * 100) + "%)" : ""), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
                    break;
                case eAttackResult.HitStyle:
                case eAttackResult.HitUnstyled:
                    {
                        if (ad is { Damage: -1 } )
                            break;

                        if (ad.AttackType is AttackData.eAttackType.Spell)
                        {
                            MythicalSpellReflectHandler.ApplyEffect(GameLivingEvent.AttackedByEnemy, this, new AttackedByEnemyEventArgs(ad));
                            ItemSpellShieldHandler.ApplyEffect(GameLivingEvent.AttackedByEnemy, this, new AttackedByEnemyEventArgs(ad));
                        }

                        if (ad is { Damage: 0, CriticalDamage: 0, Modifier: 0 })
                        {
                            break;
                        }
                        
                        #region Messages

                        string hitLocName = null;
                        switch (ad.ArmorHitLocation)
                        {
                            //GamePlayer.Attack.Location.Feet:	feet
                            // LanguageMgr.GetTranslation(Client.Account.Language, "", ad.Attacker.GetName(0, true))
                            case eArmorSlot.TORSO: hitLocName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Location.Torso"); break;
                            case eArmorSlot.ARMS: hitLocName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Location.Arm"); break;
                            case eArmorSlot.HEAD: hitLocName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Location.Head"); break;
                            case eArmorSlot.LEGS: hitLocName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Location.Leg"); break;
                            case eArmorSlot.HAND: hitLocName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Location.Hand"); break;
                            case eArmorSlot.FEET: hitLocName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Location.Foot"); break;
                        }
                        string modmessage = "";

                        if (ad.Attacker is not GamePlayer) // if attacked by player, don't show resists (?)
                        {
                            if (ad.Modifier > 0) modmessage = " (+" + ad.Modifier + ")";
                            if (ad.Modifier < 0) modmessage = " (" + ad.Modifier + ")";
                        }

                        if (ad.Attacker is GameNPC)
                        {
                            if (hitLocName != null)
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.HitsYour",
                                    ad.Attacker.GetName(0, true, Client.Account.Language, (ad.Attacker as GameNPC)), hitLocName, ad.Damage, modmessage), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                            else
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.HitsYou",
                                    ad.Attacker.GetName(0, true, Client.Account.Language, (ad.Attacker as GameNPC)), ad.Damage, modmessage), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);

                            if (ad.CriticalDamage > 0)
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.HitsYouCritical",
                                    ad.Attacker.GetName(0, true, Client.Account.Language, (ad.Attacker as GameNPC)), ad.CriticalDamage), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            if (hitLocName != null)
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.HitsYour", GetPersonalizedName(ad.Attacker), hitLocName, ad.Damage, modmessage), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                            else
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.HitsYou", ad.Attacker.IsAlive ? GetPersonalizedName(ad.Attacker) : "A dead enemy", ad.Damage, modmessage), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);

                            if (ad.CriticalDamage > 0)
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.HitsYouCritical", GetPersonalizedName(ad.Attacker), ad.CriticalDamage), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                        }


                        #endregion

                        // decrease condition of hitted armor piece
                        if (ad.ArmorHitLocation != eArmorSlot.NOTSET)
                        {
                            InventoryItem item = Inventory.GetItem((eInventorySlot)ad.ArmorHitLocation);

                            if (item != null)
                            {
                                TryReactiveEffect(item, ad.Attacker);

                                if (item is GameInventoryItem)
                                {
                                    (item as GameInventoryItem)!.OnStruckByEnemy(this, ad.Attacker);
                                }
                            }
                        }

                        if (ad.AblativeEffectApplied)
                        {
                            if (!this.isInBG && !this.IsInPvP && !this.IsInRvR)
                            {
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.AblativeArmor.Target", ad.AblativeDamageAbsorbed), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                                if (ad.Attacker is GamePlayer attackerPlayer)
                                {
                                    attackerPlayer.Out.SendMessage(LanguageMgr.GetTranslation(attackerPlayer.Client, "GameObjects.GamePlayer.AblativeArmor.Attacker", GetPersonalizedName(ad.Target), ad.AblativeDamageAbsorbed), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                                }
                            }
                        }

                        break;
                    }
                case eAttackResult.Blocked:
                    {
                        InventoryItem reactiveItem = Inventory.GetItem(eInventorySlot.LeftHandWeapon);
                        if (reactiveItem != null && reactiveItem.Object_Type == (int)eObjectType.Shield)
                        {
                            TryReactiveEffect(reactiveItem, ad.Attacker);

                            if (reactiveItem is GameInventoryItem)
                            {
                                (reactiveItem as GameInventoryItem)!.OnStruckByEnemy(this, ad.Attacker);
                            }
                        }
                        break;
                    }
            }
            // vampiir
            if (CharacterClass is PlayerClass.ClassVampiir)
            {
                GameSpellEffect removeEffect = SpellHandler.FindEffectOnTarget(this, "VampiirSpeedEnhancement");
                if (removeEffect != null)
                    removeEffect.Cancel(false);
            }

            if (IsCrafting)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.InterruptedCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                StopCrafting();
            }

            if (Client.Account.PrivLevel > 1 && ServerProperties.Properties.ENABLE_DEBUG)
            {
                SendAttackDetails(ad);
            }
        }

        private void SendAttackDetails(AttackData ad)
        {
            Client.Out.SendMessage($"---- Attack ({ad.AttackType}) {ad.Attacker?.Name} => {ad.Target?.Name} ----", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            Client.Out.SendMessage($"DEBUG: Damage {ad.Damage} {ad.DamageType} (uncapped {ad.UncappedDamage}) - Critical {ad.CriticalDamage} ({ad.criticalChance}%)", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            Client.Out.SendMessage($"DEBUG: Miss {(ad.MissChance ?? ad.Target?.ChanceToBeMissed ?? 0) * 100:0.}% - Parry {(ad.ParryChance ?? 0) * 100:0.}% - Evade {(ad.EvadeChance ?? 0) * 100:0.}% - Block {(ad.BlockChance ?? 0) * 100:0.}% - Fumble {(ad.FumbleChance ?? ad.Attacker!.ChanceToFumble) * 100:0.}%", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            Client.Out.SendMessage($"DEBUG: Weapon damage {ad.weaponDamage} - Style damage {ad.StyleDamage}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            Client.Out.SendMessage($"DEBUG: Wep stat {ad.weaponStat} Wep skill {ad.weaponSkillFactor} AF {ad.enemyAF:0.##} ABS {ad.enemyABS:0.##} Res {ad.enemyResist:0.##} => Damage * {ad.dmgMod}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            Client.Out.SendMessage($"DEBUG: Tension rate {ad.TensionRate:0.##}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }


        /// <summary>
        /// Launch any reactive effect on an item
        /// </summary>
        /// <param name="reactiveItem"></param>
        /// <param name="target"></param>
        protected virtual void TryReactiveEffect(InventoryItem reactiveItem, GameLiving target)
        {
            if (reactiveItem != null)
            {
                int requiredLevel = reactiveItem.Template.LevelRequirement > 0 ? reactiveItem.Template.LevelRequirement : Math.Min(MaxLevel, reactiveItem.Level);

                if (requiredLevel <= Level)
                {
                    SpellLine reactiveEffectLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);

                    if (reactiveEffectLine != null)
                    {
                        if (reactiveItem.ProcSpellID != 0 && ((GameInventoryItem)reactiveItem).IsBonusAllowed(nameof(reactiveItem.ProcSpellID), this))
                        {
                            Spell spell = SkillBase.FindSpell(reactiveItem.ProcSpellID, reactiveEffectLine);

                            if (spell != null)
                            {
                                int chance = reactiveItem.ProcChance > 0 ? reactiveItem.ProcChance : 10;

                                if (Util.Chance(chance))
                                {
                                    ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(this, spell, reactiveEffectLine);
                                    if (spellHandler != null)
                                    {
                                        spellHandler.StartSpell(target, reactiveItem);
                                    }
                                }
                            }
                        }

                        if (reactiveItem.ProcSpellID1 != 0 && ((GameInventoryItem)reactiveItem).IsBonusAllowed(nameof(reactiveItem.ProcSpellID1), this))
                        {
                            Spell spell = SkillBase.FindSpell(reactiveItem.ProcSpellID1, reactiveEffectLine);

                            if (spell != null)
                            {
                                int chance = reactiveItem.ProcChance > 0 ? reactiveItem.ProcChance : 10;

                                if (Util.Chance(chance))
                                {
                                    ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(this, spell, reactiveEffectLine);
                                    if (spellHandler != null)
                                    {
                                        spellHandler.StartSpell(target, reactiveItem);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Does needed interrupt checks and interrupts if needed
        /// </summary>
        /// <param name="attacker">the attacker that is interrupting</param>
        /// <param name="attackType">The attack type</param>
        /// <returns>true if interrupted successfully</returns>
        protected override bool OnInterruptTick(GameLiving attacker, AttackData.eAttackType attackType)
        {
            if (base.OnInterruptTick(attacker, attackType))
            {
                if (ActiveWeaponSlot == eActiveWeaponSlot.Distance)
                {
                    string attackTypeMsg = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Type.Shot");
                    if (AttackWeapon != null && AttackWeapon.Object_Type == (int)eObjectType.Thrown)
                        attackTypeMsg = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Type.Throw");
                    if (attacker is GameNPC)
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Interrupted", attacker.GetName(0, true, Client.Account.Language, (attacker as GameNPC)), attackTypeMsg), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                    else
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.Interrupted", GetPersonalizedName(attacker), attackTypeMsg), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                }
                return true;
            }
            return false;
        }

        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            #region PVP DAMAGE

            if (source is GamePlayer || (source?.GetPlayerOwner() != null))
            {
                if (Realm != source.Realm && source.Realm != 0)
                    DamageRvRMemory += (long)(damageAmount + criticalAmount);
            }

            #endregion PVP DAMAGE

            base.TakeDamage(source, damageType, damageAmount, criticalAmount);
            if (this.HasAbility(Abilities.DefensiveCombatPowerRegeneration))
            {
                this.Mana += (int)((damageAmount + criticalAmount) * 0.25);
            }
        }

        /// <inheritdoc />
        public override void ModifyAttack(AttackData attackData)
        {
            base.ModifyAttack(attackData);

            if (attackData.AttackResult is eAttackResult.HitStyle or eAttackResult.HitUnstyled && Guild != null)
            {
                switch (attackData.AttackType)
                {
                    case AttackData.eAttackType.MeleeOneHand:
                    case AttackData.eAttackType.MeleeDualWield:
                    case AttackData.eAttackType.MeleeTwoHand:
                        if (Guild.TerritoryMeleeAbsorption != 0)
                        {
                            double absorption = Math.Min(100, Guild.TerritoryMeleeAbsorption);
                            int absorbedDamage = (int)((double)attackData.Damage * (absorption / 100) + 0.5);
                            int absorbedCrit = (int)((double)attackData.CriticalDamage * (absorption / 100) + 0.5);
                            attackData.Damage -= absorbedDamage;
                            attackData.CriticalDamage -= absorbedCrit;
                            attackData.Modifier -= absorbedDamage + absorbedCrit;
                        }
                        break;

                    case AttackData.eAttackType.Spell:
                        if (Guild.TerritorySpellAbsorption != 0)
                        {
                            double absorption = Math.Min(100, Guild.TerritorySpellAbsorption);
                            int absorbedDamage = (int)((double)attackData.Damage * (absorption / 100) + 0.5);
                            int absorbedCrit = (int)((double)attackData.CriticalDamage * (absorption / 100) + 0.5);
                            attackData.Damage -= absorbedDamage;
                            attackData.CriticalDamage -= absorbedCrit;
                            attackData.Modifier -= absorbedDamage + absorbedCrit;
                        }
                        break;

                    case AttackData.eAttackType.DoT:
                        if (Guild.TerritoryDotAbsorption != 0)
                        {
                            double absorption = Math.Min(100, Guild.TerritoryDotAbsorption);
                            int absorbedDamage = (int)((double)attackData.Damage * (absorption / 100) + 0.5);
                            int absorbedCrit = (int)((double)attackData.CriticalDamage * (absorption / 100) + 0.5);
                            attackData.Damage -= absorbedDamage;
                            attackData.CriticalDamage -= absorbedCrit;
                            attackData.Modifier -= absorbedDamage + absorbedCrit;
                        }
                        break;
                }

                eArmorSlot hitLocation = attackData.ArmorHitLocation;
                if (hitLocation != eArmorSlot.TORSO &&
                    hitLocation != eArmorSlot.LEGS &&
                    hitLocation != eArmorSlot.ARMS &&
                    hitLocation != eArmorSlot.HEAD &&
                    hitLocation != eArmorSlot.HAND &&
                    hitLocation != eArmorSlot.FEET)
                    return;

                InventoryItem item = Inventory.GetItem((eInventorySlot)hitLocation);
                if (item == null)
                    return;

                if (!(GameServer.ServerRules.IsPveOnlyBonus(eProperty.PieceAblative) && GameServer.ServerRules.IsPvPAction(attackData.Attacker, attackData.Target)))
                    return;

                int chanceToAblate = GetModified(eProperty.PieceAblative);
                if (chanceToAblate <= 0 || !Util.Chance(chanceToAblate))
                    return;

                double absorbPercent;
                switch ((eObjectType)item.Object_Type)
                {
                    case eObjectType.Cloth:
                        absorbPercent = 30;
                        break;
                    case eObjectType.Leather:
                        absorbPercent = 45;
                        break;
                    case eObjectType.Reinforced:
                        absorbPercent = 55;
                        break;
                    case eObjectType.Studded:
                        absorbPercent = 65;
                        break;
                    case eObjectType.Scale:
                        absorbPercent = 70;
                        break;
                    case eObjectType.Chain:
                        absorbPercent = 80;
                        break;
                    case eObjectType.Plate:
                        absorbPercent = 100;
                        break;
                    default:
                        absorbPercent = 0;
                        break;
                }

                double conditionLossPercent = 100.0 - item.ConditionPercent;
                double qualityLossPercent = 100.0 - item.Quality;
                double totalLossPercent = conditionLossPercent + qualityLossPercent;
                double absorbPercentAdjusted = absorbPercent - (absorbPercent * totalLossPercent / 100.0);

                if (absorbPercentAdjusted < 0)
                    absorbPercentAdjusted = 0;

                int ablativeHp = item.Level * 10;

                int damageAbsorbed = (int)(0.01 * absorbPercentAdjusted * (attackData.Damage + attackData.CriticalDamage));
                if (damageAbsorbed > ablativeHp)
                    damageAbsorbed = ablativeHp;

                attackData.Damage -= damageAbsorbed;
                attackData.AblativeEffectApplied = true;
                attackData.AblativeDamageAbsorbed = damageAbsorbed;
            }
        }

        /// <summary>
        /// Gets the effective AF of this living.  This is used for the overall AF display
        /// on the character but not used in any damage equations.
        /// </summary>
        public override int EffectiveOverallAF
        {
            get
            {
                int eaf = 0;
                int abs = 0;
                foreach (InventoryItem item in Inventory.VisibleItems)
                {
                    double factor = 0;
                    switch (item.Item_Type)
                    {
                        case Slot.TORSO:
                            factor = 2.2;
                            break;
                        case Slot.LEGS:
                            factor = 1.3;
                            break;
                        case Slot.ARMS:
                            factor = 0.75;
                            break;
                        case Slot.HELM:
                            factor = 0.5;
                            break;
                        case Slot.HANDS:
                            factor = 0.25;
                            break;
                        case Slot.FEET:
                            factor = 0.25;
                            break;
                    }

                    int itemAFCap = Level << 1;
                    if (RealmLevel > 39)
                        itemAFCap += 2;
                    switch ((eObjectType)item.Object_Type)
                    {
                        case eObjectType.Cloth:
                            abs = 0;
                            itemAFCap >>= 1;
                            break;
                        case eObjectType.Leather:
                            abs = 10;
                            break;
                        case eObjectType.Reinforced:
                            abs = 19;
                            break;
                        case eObjectType.Studded:
                            abs = 19;
                            break;
                        case eObjectType.Scale:
                            abs = 27;
                            break;
                        case eObjectType.Chain:
                            abs = 27;
                            break;
                        case eObjectType.Plate:
                            abs = 34;
                            break;
                    }

                    if (factor > 0)
                    {
                        int af = item.DPS_AF;
                        if (af > itemAFCap)
                            af = itemAFCap;
                        double piece_eaf = af * item.Quality / 100.0 * item.ConditionPercent / 100.0 * (1 + abs / 100.0);
                        eaf += (int)(piece_eaf * factor);
                    }
                }

                // Overall AF CAP = 10 * level * (1 + abs%/100)
                int bestLevel = -1;
                bestLevel = Math.Max(bestLevel, GetAbilityLevel(Abilities.AlbArmor));
                bestLevel = Math.Max(bestLevel, GetAbilityLevel(Abilities.HibArmor));
                bestLevel = Math.Max(bestLevel, GetAbilityLevel(Abilities.MidArmor));
                switch (bestLevel)
                {
                    default: abs = 0; break; // cloth etc
                    case ArmorLevel.Leather: abs = 10; break;
                    case ArmorLevel.Studded: abs = 19; break;
                    case ArmorLevel.Chain: abs = 27; break;
                    case ArmorLevel.Plate: abs = 34; break;
                }

                eaf += BaseBuffBonusCategory[(int)eProperty.ArmorFactor]; // base buff before cap
                int eafcap = (int)(10 * Level * (1 + abs * 0.01));
                if (eaf > eafcap)
                    eaf = eafcap;
                eaf += (int)Math.Min(Level * 1.875, SpecBuffBonusCategory[(int)eProperty.ArmorFactor])
                    - DebuffCategory[(int)eProperty.ArmorFactor]
                    + BuffBonusCategory4[(int)eProperty.ArmorFactor]
                    + Math.Min(Level, ItemBonus[(int)eProperty.ArmorFactor]);

                eaf = (int)(eaf * BuffBonusMultCategory1.Get((int)eProperty.ArmorFactor));

                return eaf;
            }
        }
        /// <summary>
        /// Calc Armor hit location when player is hit by enemy
        /// </summary>
        /// <returns>slotnumber where enemy hits</returns>
        /// attackdata(ad) changed
        public virtual eArmorSlot CalculateArmorHitLocation(AttackData ad)
        {
            if (ad.Style != null)
            {
                if (ad.Style.ArmorHitLocation != eArmorSlot.NOTSET)
                    return ad.Style.ArmorHitLocation;
            }
            int chancehit = Util.Random(1, 100);
            if (chancehit <= 40)
            {
                return eArmorSlot.TORSO;
            }
            else if (chancehit <= 65)
            {
                return eArmorSlot.LEGS;
            }
            else if (chancehit <= 80)
            {
                return eArmorSlot.ARMS;
            }
            else if (chancehit <= 90)
            {
                return eArmorSlot.HEAD;
            }
            else if (chancehit <= 95)
            {
                return eArmorSlot.HAND;
            }
            else
            {
                return eArmorSlot.FEET;
            }
        }

        /// <summary>
        /// determines current weaponspeclevel
        /// </summary>
        public override int WeaponSpecLevel(InventoryItem weapon)
        {
            if (weapon == null)
                return 0;
            // use axe spec if left hand axe is not in the left hand slot
            if (weapon.Object_Type == (int)eObjectType.LeftAxe && weapon.SlotPosition != Slot.LEFTHAND)
                return GameServer.ServerRules.GetObjectSpecLevel(this, eObjectType.Axe);
            // use left axe spec if axe is in the left hand slot
            if (weapon.SlotPosition == Slot.LEFTHAND
                && (weapon.Object_Type == (int)eObjectType.Axe
                    || weapon.Object_Type == (int)eObjectType.Sword
                    || weapon.Object_Type == (int)eObjectType.Hammer))
                return GameServer.ServerRules.GetObjectSpecLevel(this, eObjectType.LeftAxe);
            return GameServer.ServerRules.GetObjectSpecLevel(this, (eObjectType)weapon.Object_Type);
        }

        public virtual String GetWeaponSpec(InventoryItem weapon)
        {
            if (weapon == null)
                return null;
            // use axe spec if left hand axe is not in the left hand slot
            if (weapon.Object_Type == (int)eObjectType.LeftAxe && weapon.SlotPosition != Slot.LEFTHAND)
                return SkillBase.ObjectTypeToSpec(eObjectType.Axe);
            // use left axe spec if axe is in the left hand slot
            if (weapon.SlotPosition == Slot.LEFTHAND
                && (weapon.Object_Type == (int)eObjectType.Axe
                    || weapon.Object_Type == (int)eObjectType.Sword
                    || weapon.Object_Type == (int)eObjectType.Hammer))
                return SkillBase.ObjectTypeToSpec(eObjectType.LeftAxe);
            return SkillBase.ObjectTypeToSpec((eObjectType)weapon.Object_Type);
        }

        /// <summary>
        /// determines current weaponspeclevel
        /// </summary>
        public int WeaponBaseSpecLevel(InventoryItem weapon)
        {
            if (weapon == null)
                return 0;
            // use axe spec if left hand axe is not in the left hand slot
            if (weapon.Object_Type == (int)eObjectType.LeftAxe && weapon.SlotPosition != Slot.LEFTHAND)
                return GameServer.ServerRules.GetBaseObjectSpecLevel(this, eObjectType.Axe);
            // use left axe spec if axe is in the left hand slot
            if (weapon.SlotPosition == Slot.LEFTHAND
                && (weapon.Object_Type == (int)eObjectType.Axe
                    || weapon.Object_Type == (int)eObjectType.Sword
                    || weapon.Object_Type == (int)eObjectType.Hammer))
                return GameServer.ServerRules.GetBaseObjectSpecLevel(this, eObjectType.LeftAxe);
            return GameServer.ServerRules.GetBaseObjectSpecLevel(this, (eObjectType)weapon.Object_Type);
        }

        /// <summary>
        /// Gets the weaponskill of weapon
        /// </summary>
        /// <param name="weapon"></param>
        public override double GetWeaponSkill(InventoryItem weapon)
        {
            if (weapon == null)
            {
                return 0;
            }
            double classbase =
                (weapon.SlotPosition == (int)eInventorySlot.DistanceWeapon
                 ? CharacterClass.WeaponSkillRangedBase
                 : CharacterClass.WeaponSkillBase);

            //added for WS Poisons
            double preBuff = ((Level * classbase * 0.02 * (1 + (GetWeaponStat(weapon) - 50) * 0.005)) * Effectiveness);

            //return ((Level * classbase * 0.02 * (1 + (GetWeaponStat(weapon) - 50) * 0.005)) * PlayerEffectiveness);
            return Math.Max(0, preBuff * GetModified(eProperty.WeaponSkill) * 0.01);
        }

        /// <summary>
        /// calculates weapon stat
        /// </summary>
        /// <param name="weapon"></param>
        /// <returns></returns>
        public override int GetWeaponStat(InventoryItem weapon)
        {
            if (weapon != null)
            {
                switch ((eObjectType)weapon.Object_Type)
                {
                    // DEX modifier
                    case eObjectType.Staff:
                    case eObjectType.Fired:
                    case eObjectType.Longbow:
                    case eObjectType.Crossbow:
                    case eObjectType.CompositeBow:
                    case eObjectType.RecurvedBow:
                    case eObjectType.Thrown:
                    case eObjectType.Shield:
                        return GetModified(eProperty.Dexterity) / 2;

                    // STR+DEX modifier
                    case eObjectType.ThrustWeapon:
                    case eObjectType.Piercing:
                    case eObjectType.Spear:
                    case eObjectType.Flexible:
                    case eObjectType.HandToHand:
                        return (GetModified(eProperty.Strength) + GetModified(eProperty.Dexterity)) / 4;
                }
            }
            // STR modifier for others
            return GetModified(eProperty.Strength) / 2;
        }

        /// <summary>
        /// calculate item armor factor influenced by quality, con and duration
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public override double GetArmorAF(eArmorSlot slot)
        {
            if (slot == eArmorSlot.NOTSET) return 20;
            InventoryItem item = Inventory.GetItem((eInventorySlot)slot);
            if (item == null) return 20;
            double eaf = item.DPS_AF + BaseBuffBonusCategory[(int)eProperty.ArmorFactor]; // base AF buff

            int itemAFcap = Level;
            if (RealmLevel > 39)
                itemAFcap++;
            if (item.Object_Type != (int)eObjectType.Cloth)
            {
                itemAFcap <<= 1;
            }

            eaf = eaf.Clamp(0, itemAFcap);

            // my test shows that qual is added after AF buff
            eaf *= item.Quality * 0.01 * item.Condition / item.MaxCondition;

            eaf += GetModified(eProperty.ArmorFactor) / 5;

            /*GameSpellEffect effect = SpellHandler.FindEffectOnTarget(this, typeof(VampiirArmorDebuff));
            if (effect != null && slot == (effect.SpellHandler as VampiirArmorDebuff).Slot)
            {
                eaf -= (int)(effect.SpellHandler as VampiirArmorDebuff).Spell.Value;
            }*/
            return 20 + eaf;
        }

        /// <summary>
        /// Calculates armor absorb level
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public override double GetArmorAbsorb(eArmorSlot slot)
        {
            if (slot == eArmorSlot.NOTSET) return 0;
            InventoryItem item = Inventory.GetItem((eInventorySlot)slot);
            if (item == null) return 0;
            // vampiir random armor debuff change ~
            double eaf = (item.SPD_ABS + GetModified(eProperty.ArmorAbsorption)) * 0.01;
            return eaf;
        }

        /// <summary>
        /// Weaponskill thats shown to the player
        /// </summary>
        public virtual int DisplayedWeaponSkill
        {
            get
            {
                return (int)GetWeaponSkill(AttackWeapon);
            }
        }

        /// <summary>
        /// Gets the weapondamage of currently used weapon
        /// Used to display weapon damage in stats, 16.5dps = 1650
        /// </summary>
        /// <param name="weapon">the weapon used for attack</param>
        public override double WeaponDamage(InventoryItem weapon)
        {
            if (weapon == null)
                return 1.0;

            var dps = weapon.DPS_AF * 0.1;
            var cap = 1.2 + 0.3 * Level;
            if (RealmLevel > 39)
                cap += 0.3;
            dps = dps.Clamp(0.1, cap);
            dps *= 1.0 + (GetModified(eProperty.DPS) * 0.01);
            // beware to use always ConditionPercent, because Condition is abolute value
            dps *= weapon.Quality * 0.01 * weapon.ConditionPercent * 0.01;

            var twohand_bonus = 1.0;
            if (weapon.Item_Type == Slot.TWOHAND)
            {
                var wp_spec = GetModifiedSpecLevel(GetWeaponSpec(weapon));
                twohand_bonus = 1.1 + 0.005 * wp_spec;
            }
            var weapon_dps = dps * weapon.SPD_ABS * 0.1
                * (0.94 + weapon.SPD_ABS * 0.1 * 0.03)
                * twohand_bonus
                * (1 + 0.01 * GetModified(eProperty.MeleeDamage));

            return weapon_dps;
        }

        /// <summary>
        /// Max. Damage possible without style
        /// </summary>
        /// <param name="weapon">attack weapon</param>
        public override double UnstyledDamageCap(InventoryItem weapon)
        {
            if (weapon != null)
            {
                int DPS = weapon.DPS_AF;
                int cap = 12 + 3 * Level;
                if (RealmLevel > 39)
                    cap += 3;
                if (DPS > cap)
                    DPS = cap;

                double result = DPS * (1 + GetModified(eProperty.DPS) * 0.01) * weapon.SPD_ABS * 0.03 * (0.94 + 0.003 * weapon.SPD_ABS);
                
                double effectiveness = 1.0;
                if (weapon.Hand == 1) //2h
                {
                    effectiveness += 1.1 + (WeaponSpecLevel(weapon) - 1) * 0.005;
                    if (weapon.Item_Type == Slot.RANGED)
                    {
                        // http://home.comcast.net/~shadowspawn3/bowdmg.html
                        //ammo damage bonus
                        if (RangeAttackAmmo != null)
                        {
                            switch ((RangeAttackAmmo.SPD_ABS) & 0x3)
                            {
                                case 0: effectiveness += -0.15; break;  //Blunt       (light) -15%
                                case 1: break;     //Bodkin     (medium)   0%
                                case 2: effectiveness += 0.15; break;  //doesn't exist on live
                                case 3: effectiveness += 0.25; break;  //Broadhead (X-heavy) +25%
                            }
                        }
                    }
                }

                if (weapon.Item_Type == Slot.RANGED && (weapon.Object_Type == (int)eObjectType.Longbow || weapon.Object_Type == (int)eObjectType.RecurvedBow || weapon.Object_Type == (int)eObjectType.CompositeBow))
                {
                    if (ServerProperties.Properties.ALLOW_OLD_ARCHERY == true)
                    {
                        effectiveness += GetModified(eProperty.RangedDamage) * 0.01;
                    }
                    else if (ServerProperties.Properties.ALLOW_OLD_ARCHERY == false)
                    {
                        effectiveness += GetModified(eProperty.SpellDamage) * 0.01;
                        effectiveness += GetModified(eProperty.RangedDamage) * 0.01;
                    }
                }
                else if (weapon.Item_Type == Slot.RANGED)
                {
                    //Ranged damage buff,debuff,Relic,RA
                    effectiveness += GetModified(eProperty.RangedDamage) * 0.01;
                }
                else if (weapon.Item_Type == Slot.RIGHTHAND || weapon.Item_Type == Slot.LEFTHAND || weapon.Item_Type == Slot.TWOHAND)
                {
                    effectiveness += GetModified(eProperty.MeleeDamage) * 0.01;
                }

                return effectiveness * result;
            }
            else
            { // TODO: whats the damage cap without weapon?
                return
                    (AttackDamage(weapon) * 3 * (1 + (AttackSpeed(weapon) * 0.001 - 2) * .03))
                    * (1 + GetModified(eProperty.DPS) * 0.01);
            }
        }

        /// <summary>
        /// Can this player cast the given spell while in combat?
        /// </summary>
        /// <param name="spell"></param>
        /// <returns></returns>
        public override bool CanCastInCombat(Spell spell)
        {
            if (spell == null || spell.IsInstantCast)
                return true;

            switch (CharacterClass)
            {
                case PlayerClass.ClassVampiir vampiir:
                case PlayerClass.ClassMaulerAlb maulerAlb:
                case PlayerClass.ClassMaulerMid maulerMid:
                case PlayerClass.ClassMaulerHib maulerHib:
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The chance for a critical hit
        /// </summary>
        /// <param name="weapon">attack weapon</param>
        public override int AttackCriticalChance(InventoryItem weapon)
        {
            if (weapon != null && weapon.Item_Type == Slot.RANGED && RangedAttackType == eRangedAttackType.Critical)
                return 0; // no crit damage for crit shots

            // check for melee attack
            if (weapon != null && weapon.Item_Type != Slot.RANGED)
            {
                return GetModified(eProperty.CriticalMeleeHitChance);
            }

            // check for ranged attack
            if (weapon != null && weapon.Item_Type == Slot.RANGED)
            {
                return GetModified(eProperty.CriticalArcheryHitChance);
            }

            // base 10% chance of critical for all with melee weapons
            return 10;
        }

        /// <summary>
        /// Returns the damage type of the current attack
        /// </summary>
        /// <param name="weapon">attack weapon</param>
        public override eDamageType AttackDamageType(InventoryItem weapon)
        {
            if (weapon == null)
                return eDamageType.Natural;
            switch ((eObjectType)weapon.Object_Type)
            {
                case eObjectType.Crossbow:
                case eObjectType.Longbow:
                case eObjectType.CompositeBow:
                case eObjectType.RecurvedBow:
                case eObjectType.Fired:
                    InventoryItem ammo = RangeAttackAmmo;
                    if (ammo == null)
                        return (eDamageType)weapon.Type_Damage;
                    return (eDamageType)ammo.Type_Damage;
                case eObjectType.Shield:
                    return eDamageType.Crush; // TODO: shields do crush damage (!) best is if Type_Damage is used properly
                default:
                    return (eDamageType)weapon.Type_Damage;
            }
        }

        /// <summary>
        /// Returns the AttackRange of this living
        /// </summary>
        public override int AttackRange
        {
            /* tested with:
            staff					= 125-130
            sword			   		= 126-128.06
            shield (Numb style)		= 127-129
            polearm	(Impale style)	= 127-130
            mace (Daze style)		= 127.5-128.7

            Think it's safe to say that it never changes; different with mobs. */

            get
            {
                GameLiving livingTarget = TargetObject as GameLiving;

                //TODO change to real distance of bows!
                if (ActiveWeaponSlot == eActiveWeaponSlot.Distance)
                {
                    InventoryItem weapon = AttackWeapon;
                    if (weapon == null)
                        return 0;

                    double range;
                    InventoryItem ammo = RangeAttackAmmo;

                    switch ((eObjectType)weapon.Object_Type)
                    {
                        case eObjectType.Longbow: range = 1760; break;
                        case eObjectType.RecurvedBow: range = 1680; break;
                        case eObjectType.CompositeBow: range = 1600; break;
                        default: range = 1200; break; // shortbow, xbow, throwing
                    }

                    range = Math.Max(32, range * GetModified(eProperty.ArcheryRange) * 0.01);

                    if (ammo != null)
                        switch ((ammo.SPD_ABS >> 2) & 0x3)
                        {
                            case 0: range *= 0.85; break; //Clout -15%
                                                          //						case 1:                break; //(none) 0%
                            case 2: range *= 1.15; break; //doesn't exist on live
                            case 3: range *= 1.25; break; //Flight +25%
                        }
                    if (livingTarget != null) range += Math.Min((Position.Z - livingTarget.Position.Z) / 2.0, 500);
                    if (range < 32) range = 32;

                    return (int)(range);
                }

                int meleerange = 128;
                GameKeepComponent keepcomponent = livingTarget as GameKeepComponent; // TODO better component melee attack range check
                if (keepcomponent != null)
                    meleerange += 150;
                else
                {
                    if (livingTarget != null && livingTarget.IsMoving)
                        meleerange += 32;
                    if (IsMoving)
                        meleerange += 32;
                }
                return meleerange;
            }
            set { }
        }

        /// <summary>
        /// Gets the current attackspeed of this living in milliseconds
        /// </summary>
        /// <param name="weapons">attack weapons</param>
        /// <returns>effective speed of the attack. average if more than one weapon.</returns>
        public override int AttackSpeed(params InventoryItem[] weapons)
        {
            if (weapons == null || weapons.Length < 1)
                return 0;

            int count = 0;
            double speed = 0;
            bool bowWeapon = true;

            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null)
                {
                    speed += weapons[i].SPD_ABS;
                    count++;

                    switch (weapons[i].Object_Type)
                    {
                        case (int)eObjectType.Fired:
                        case (int)eObjectType.Longbow:
                        case (int)eObjectType.Crossbow:
                        case (int)eObjectType.RecurvedBow:
                        case (int)eObjectType.CompositeBow:
                            break;
                        default:
                            bowWeapon = false;
                            break;
                    }
                }
            }

            if (count < 1)
                return 0;

            speed /= count;

            int qui = Math.Min(250, Quickness); //250 soft cap on quickness

            if (bowWeapon)
            {
                if (ServerProperties.Properties.ALLOW_OLD_ARCHERY)
                {
                    //Draw Time formulas, there are very many ...
                    //Formula 2: y = iBowDelay * ((100 - ((iQuickness - 50) / 5 + iMasteryofArcheryLevel * 3)) / 100)
                    //Formula 1: x = (1 - ((iQuickness - 60) / 500 + (iMasteryofArcheryLevel * 3) / 100)) * iBowDelay
                    //Table a: Formula used: drawspeed = bowspeed * (1-(quickness - 50)*0.002) * ((1-MoA*0.03) - (archeryspeedbonus/100))
                    //Table b: Formula used: drawspeed = bowspeed * (1-(quickness - 50)*0.002) * (1-MoA*0.03) - ((archeryspeedbonus/100 * basebowspeed))

                    //For now use the standard weapon formula, later add ranger haste etc.
                    speed *= (1.0 - (qui - 60) * 0.002);
                    double percent = 0;
                    // Calcul ArcherySpeed bonus to substract
                    percent = speed * 0.01 * GetModified(eProperty.ArcherySpeed);
                    // Apply RA difference
                    speed -= percent;
                    //log.Debug("speed = " + speed + " percent = " + percent + " eProperty.archeryspeed = " + GetModified(eProperty.ArcherySpeed));
                    if (RangedAttackType == eRangedAttackType.Critical)
                        speed = speed * 2 - (GetAbilityLevel(Abilities.Critical_Shot) - 1) * speed / 10;
                }
                else
                {
                    // no archery bonus
                    speed *= (1.0 - (qui - 60) * 0.002);
                }
            }
            else
            {
                // TODO use haste
                //Weapon Speed*(1-(Quickness-60)/500]*(1-Haste)
                speed *= (1.0 - (qui - 60) * 0.002) * 0.01 * GetModified(eProperty.MeleeSpeed);
            }


            // apply speed cap
            if (IsInRvR)
            {
                if (speed < 15)
                {
                    speed = 15;
                }
            }
            else if (speed < 9)
            {
                speed = 9;
            }
            return (int)(speed * 100);
        }

        /// <summary>
        /// Gets the attack damage
        /// </summary>
        /// <param name="weapon">the weapon used for attack</param>
        /// <returns>the weapon damage</returns>
        public override double AttackDamage(InventoryItem weapon)
        {
            if (weapon == null)
                return 0;

            double effectiveness = 1.00;
            double damage = WeaponDamage(weapon) * weapon.SPD_ABS * 0.1;

            if (weapon.Hand == 1) // two-hand
            {
                // twohanded used weapons get 2H-Bonus = 10% + (Skill / 2)%
                int spec = WeaponSpecLevel(weapon) - 1;
                effectiveness += 0.1 + spec * 0.005;
            }

            if (weapon.Item_Type == Slot.RANGED)
            {
                //ammo damage bonus
                if (RangeAttackAmmo != null)
                {
                    switch ((RangeAttackAmmo.SPD_ABS) & 0x3)
                    {
                        case 0: effectiveness += -0.15; break; //Blunt       (light) -15%
                                                       //case 1: damage *= 1;	break; //Bodkin     (medium)   0%
                        case 2: effectiveness += 0.15; break; //doesn't exist on live
                        case 3: effectiveness += 0.25; break; //Broadhead (X-heavy) +25%
                    }
                }
                //Ranged damage buff,debuff,Relic,RA
                effectiveness += GetModified(eProperty.RangedDamage) * 0.01;
            }
            else if (weapon.Item_Type == Slot.RIGHTHAND || weapon.Item_Type == Slot.LEFTHAND || weapon.Item_Type == Slot.TWOHAND)
            {
                //Melee damage buff,debuff,Relic,RA
                effectiveness += GetModified(eProperty.MeleeDamage) * 0.01;
            }
            damage *= effectiveness;
            return damage;
        }

        /// <summary>
        /// Stores the amount of realm points gained by other players on last death
        /// </summary>
        protected long m_lastDeathRealmPoints;

        /// <summary>
        /// Gets/sets the amount of realm points gained by other players on last death
        /// </summary>
        public long LastDeathRealmPoints
        {
            get { return m_lastDeathRealmPoints; }
            set { m_lastDeathRealmPoints = value; }
        }

        /// <summary>
        /// Check if the player lost Constitution
        /// Player can't lost cons if he is in a territory and the killer is a territory mob
        /// </summary>
        /// <param name="killer">The player killer</param>
        /// <returns></returns>
        public virtual bool CheckIfLostConstitution(GameObject killer)
        {
            GameNPC mob = killer as GameNPC;
            if (mob == null)
                return !IsInPvP && !IsInRvR;
            string classType = killer.GetType().Name;
            Territory territory = TerritoryManager.GetCurrentTerritory(this);
            return classType != "GuardNPC" && classType != "GuardOutlaw" && territory == null && !mob.IsInTerritory && !IsInPvP && !IsInRvR;
        }

        /// <summary>
        /// Last killer of this player. Can be null if killed by environment (e.g. falling)
        /// </summary>
        public GameObject LastKiller { get; set; }

        public bool IsInSafeArea()
        {
            return CurrentAreas.Any(a => a is AbstractArea { IsSafeArea: true });
        }

        public bool IsInPvPArea()
        {
            return CurrentAreas.Any(area => area.IsPvP);
        }

        public bool HasAdrenalineBuff()
        {
            return EffectList.GetOfType<AdrenalineSpellEffect>() != null;
        }

        /// <summary>
        /// Called when the player dies
        /// </summary>
        /// <param name="killer">the killer</param>
        public override void Die(GameObject killer)
        {
            // ambiant talk
            if (killer is GameNPC)
                (killer as GameNPC)!.FireAmbientSentence(GameNPC.eAmbientTrigger.killing, this);

            CharacterClass.Die(killer);

            bool realmDeath = killer != null && killer.Realm != eRealm.None;

            TargetObject = null;
            Diving(waterBreath.Normal);
            if (IsOnHorse)
                IsOnHorse = false;

            // cancel task if active
            if (Task != null && Task.TaskActive)
                Task.ExpireTask();

            string publicMessage;
            ushort messageDistance = WorldMgr.DEATH_MESSAGE_DISTANCE;
            m_releaseType = eReleaseType.Normal;

            string location = "";
            if (CurrentAreas.Count > 0 && (CurrentAreas[0] is Area.BindArea) == false)
                location = (CurrentAreas[0] as AbstractArea)!.Description;
            else
                location = CurrentZone.Description;

            if (killer == null)
            {
                LastKiller = null;
                if (realmDeath)
                {
                    publicMessage = "GameObjects.GamePlayer.Die.KilledLocation";
                }
                else
                {
                    publicMessage = "GameObjects.GamePlayer.Die.Killed";
                }
            }
            else
            {
                LastKiller = killer;
                if (DuelTarget == killer)
                {
                    m_releaseType = eReleaseType.Duel;
                    messageDistance = WorldMgr.YELL_DISTANCE;
                    publicMessage = "GameObjects.GamePlayer.Die.DuelDefeated";
                }
                else
                {
                    messageDistance = 0;
                    if (realmDeath)
                    {
                        publicMessage = "GameObjects.GamePlayer.Die.KilledByLocation";
                    }
                    else
                    {
                        publicMessage = "GameObjects.GamePlayer.Die.KilledBy";
                    }
                }
            }

            DuelStop();

            eChatType messageType;
            if (m_releaseType == eReleaseType.Duel)
                messageType = eChatType.CT_Emote;
            else if (killer == null)
            {
                messageType = eChatType.CT_PlayerDied;
            }
            else
            {
                switch ((eRealm)killer.Realm)
                {
                    case eRealm.Albion: messageType = eChatType.CT_KilledByAlb; break;
                    case eRealm.Midgard: messageType = eChatType.CT_KilledByMid; break;
                    case eRealm.Hibernia: messageType = eChatType.CT_KilledByHib; break;
                    default: messageType = eChatType.CT_PlayerDied; break; // killed by mob
                }
            }

            if (killer is GamePlayer killerPlayer && killer != this)
            {
                GameSpellEffect damnationEffect = SpellHandler.FindEffectOnTarget(killerPlayer, "Damnation");
                bool targetIsNotDamned = SpellHandler.FindEffectOnTarget(this, "Damnation") == null;

                if (!IsInRvR && !IsInPvP && Reputation < 0 && !IsInPvPArea() && !killerPlayer.IsInSafeArea())
                {
                    if (!killerPlayer.IsPlayerGreyCon(this))
                    {
                        TaskManager.UpdateTaskProgress(killerPlayer, "OutlawPlayersSentToJail", 1);
                    }
                }
                else if (this.CurrentRegion.IsRvR || this.IsInPvP || IsInPvPArea())
                {
                    if (!killerPlayer.IsInSafeArea())
                    {
                        if (!killerPlayer.IsPlayerGreyCon(this))
                        {
                            if (killerPlayer.Group != null)
                            {
                                TaskManager.UpdateTaskProgress(killerPlayer, "KillEnemyPlayersGroup", 1);
                            }
                            else
                            {
                                TaskManager.UpdateTaskProgress(killerPlayer, "KillEnemyPlayersAlone", 1);
                            }

                            if (killerPlayer.IsInPvP)
                            {
                                PvpManager.Instance.HandlePlayerKill(killerPlayer, this);
                            }
                        }
                    }
                }

                if (killerPlayer.HasAdrenalineBuff())
                {
                    if (!killerPlayer.IsPlayerGreyCon(this))
                    {
                        TaskManager.UpdateTaskProgress(killerPlayer, "EnemiesKilledInAdrenalineMode", 1);
                    }
                }

                killerPlayer.Out.SendMessage(LanguageMgr.GetTranslation(killerPlayer.Client.Account.Language, "GameObjects.GamePlayer.Die.YouKilled", killerPlayer.GetPersonalizedName(this)), eChatType.CT_PlayerDied, eChatLoc.CL_SystemWindow);

                if (damnationEffect != null && targetIsNotDamned)
                {
                    double conLevel = killerPlayer.GetConLevel(this);

                    float healPercentage;
                    float durationPercentage;
                    switch (conLevel)
                    {
                        case <= -3:
                            durationPercentage = 0.0f;
                            healPercentage = 0.0f;
                            break;
                        case <= -2:
                            durationPercentage = 0.12f;
                            healPercentage = 0.05f;
                            break;
                        case <= -1:
                            durationPercentage = 0.25f;
                            healPercentage = 0.10f;
                            break;
                        case <= 0:
                            durationPercentage = 0.50f;
                            healPercentage = 0.20f;
                            break;
                        case <= 1:
                            durationPercentage = 0.85f;
                            healPercentage = 0.35f;
                            break;
                        case <= 2:
                            durationPercentage = 1.25f;
                            healPercentage = 0.50f;
                            break;
                        default: // ConLevel >= 3
                            durationPercentage = 1.75f;
                            healPercentage = 0.75f;
                            break;
                    }

                    int baseDuration = damnationEffect.Spell.Duration;
                    int additionalDuration = (int)(baseDuration * durationPercentage);
                    int damnationEnhancement = killerPlayer.GetModified(eProperty.DamnationEffectEnhancement);

                    if (damnationEnhancement > 0)
                    {
                        int enhancementBonus = (int)(additionalDuration * damnationEnhancement / 100.0);
                        additionalDuration += enhancementBonus;
                    }

                    int actualAddedDuration = damnationEffect.AddRemainingTime(additionalDuration);

                    if (actualAddedDuration != 0)
                    {
                        damnationEffect.AddRemainingTime(additionalDuration);
                        killerPlayer.Out.SendMessage(LanguageMgr.GetTranslation(killerPlayer.Client.Account.Language, "Damnation.Kill.DurationExtended", (additionalDuration / 1000)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                    }

                    int healAmount = (int)(killerPlayer.MaxHealth * healPercentage);

                    if (damnationEnhancement > 0)
                    {
                        int healEnhancementBonus = (int)(healAmount * damnationEnhancement / 100.0);
                        healAmount += healEnhancementBonus;
                    }

                    if (healAmount != 0)
                    {
                        int healthBeforeHeal = killerPlayer.Health;
                        killerPlayer.ChangeHealth(killerPlayer, GameLiving.eHealthChangeType.Spell, healAmount);
                        int actualHealAmount = killerPlayer.Health - healthBeforeHeal;

                        if (actualHealAmount != 0)
                        {
                            killerPlayer.Out.SendMessage(LanguageMgr.GetTranslation(killerPlayer.Client.Account.Language, "Damnation.Kill.SelfHeal", actualHealAmount), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                        }
                        killerPlayer.Out.SendSpellEffectAnimation(killerPlayer, killerPlayer, 7277, 0, false, 1);
                    }
                }
            }

            ArrayList players = new ArrayList();
            if (messageDistance == 0)
            {
                foreach (GameClient client in WorldMgr.GetClientsOfRegion(CurrentRegionID))
                {
                    players.Add(client.Player);
                }
            }
            else
            {
                foreach (GamePlayer player in GetPlayersInRadius(messageDistance))
                {
                    if (player == null) continue;
                    players.Add(player);
                }
            }
            foreach (GamePlayer player in players)
            {
                // on normal server type send messages only to the killer and dead players realm
                // check for gameplayer is needed because killers realm don't see deaths by guards
                if (
                    (player != killer) && (
                        (killer != null && killer is GamePlayer && GameServer.ServerRules.IsSameRealm((GamePlayer)killer, player, true))
                        || (GameServer.ServerRules.IsSameRealm(this, player, true))
                        || ServerProperties.Properties.DEATH_MESSAGES_ALL_REALMS)
                )
                {
                    if (publicMessage == "GameObjects.GamePlayer.Die.KilledLocation")
                        player.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, publicMessage,
                            player.GetPersonalizedName(this), location), messageType, eChatLoc.CL_SystemWindow);
                    else if (publicMessage == "GameObjects.GamePlayer.Die.KilledByLocation")
                        player.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, publicMessage,
                            player.GetPersonalizedName(this), player.GetPersonalizedName(killer), location), messageType, eChatLoc.CL_SystemWindow);
                    else if (publicMessage == "GameObjects.GamePlayer.Die.Killed")
                        player.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, publicMessage,
                            player.GetPersonalizedName(this)), messageType, eChatLoc.CL_SystemWindow);
                    else
                        player.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, publicMessage,
                            player.GetPersonalizedName(this), player.GetPersonalizedName(killer)), messageType, eChatLoc.CL_SystemWindow);
                }
            }

            //Dead ppl. dismount ...
            if (Steed != null)
                DismountSteed(true);
            //Dead ppl. don't sit ...
            if (IsSitting)
            {
                IsSitting = false;
                UpdatePlayerStatus();
            }

            // Lose 40% tension
            int lossReduction = GetModified(eProperty.TensionConservationBonus);
            if (lossReduction <= 100)
            {
                Tension -= (int)(Tension * (0.40 * (100 - lossReduction) / 100) + 0.5);
            }

            // then buffs drop messages
            base.Die(killer);

            lock (m_LockObject)
            {
                if (m_releaseTimer != null)
                {
                    m_releaseTimer.Stop();
                    m_releaseTimer = null;
                }

                if (m_quitTimer != null)
                {
                    m_quitTimer.Stop();
                    m_quitTimer = null;
                }
                m_automaticRelease = m_releaseType == eReleaseType.Duel;
                m_releasePhase = 0;
                m_deathTick = GameTimer.GetTickCount(); // we use realtime, because timer window is realtime

                Out.SendTimerWindow(LanguageMgr.GetTranslation(Client.Account.Language, "System.ReleaseTimer"), (m_automaticRelease ? RELEASE_MINIMUM_WAIT : RELEASE_TIME));
                m_releaseTimer = new RegionTimer(this);
                m_releaseTimer.Callback = new RegionTimerCallback(ReleaseTimerCallback);
                m_releaseTimer.Start(1000);

                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Die.ReleaseToReturn"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);

                // clear target object so no more actions can used on this target, spells, styles, attacks...
                TargetObject = null;

                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player == null) continue;
                    player.Out.SendPlayerDied(this, killer);
                }

                // first penalty is 5% of expforlevel, second penalty comes from release
                int xpLossPercent;
                if (Level < 40)
                {
                    xpLossPercent = MaxLevel - Level;
                }
                else
                {
                    xpLossPercent = MaxLevel - 40;
                }

                if (realmDeath) //Live PvP servers have 3 con loss on pvp death, can be turned off in server properties -Unty
                {
                    int conpenalty = 0;
                    switch (GameServer.Instance.Configuration.ServerType)
                    {
                        case eGameServerType.GST_Normal:
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Die.DeadRVR"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                            xpLossPercent = 0;
                            m_deathtype = eDeathType.RvR;
                            break;

                        case eGameServerType.GST_PvP:
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Die.DeadRVR"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                            xpLossPercent = 0;
                            m_deathtype = eDeathType.PvP;
                            if (ServerProperties.Properties.PVP_DEATH_CON_LOSS && CheckIfLostConstitution(killer))
                            {
                                conpenalty = 3;
                                TempProperties.setProperty(DEATH_CONSTITUTION_LOSS_PROPERTY, conpenalty);
                            }
                            break;
                    }

                }
                else
                {
                    if (Level >= ServerProperties.Properties.PVE_EXP_LOSS_LEVEL)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Die.LoseExperience"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                        // if this is the first death in level, you lose only half the penalty
                        switch (DeathCount)
                        {
                            case 0:
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Die.DeathN1"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                                xpLossPercent /= 3;
                                break;
                            case 1:
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Die.DeathN2"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                                xpLossPercent = xpLossPercent * 2 / 3;
                                break;
                        }

                        DeathCount++;
                        m_deathtype = eDeathType.PvE;
                    }

                    if (Level >= ServerProperties.Properties.PVE_CON_LOSS_LEVEL && CheckIfLostConstitution(killer))
                    {
                        int conLoss = DeathCount;
                        if (conLoss > 3)
                            conLoss = 3;
                        else if (conLoss < 1)
                            conLoss = 1;
                        TempProperties.setProperty(DEATH_CONSTITUTION_LOSS_PROPERTY, conLoss);
                    }
                }
                if (xpLossPercent > 0)
                {
                    int modifier = GetModified(eProperty.DeathExpLoss);
                    xpLossPercent = (int)Math.Round(xpLossPercent * modifier / 100.0);
                    long xpLoss = (ExperienceForNextLevel - ExperienceForCurrentLevel) * xpLossPercent / 1000;
                    GainExperience(eXPSource.Other, -xpLoss, 0, 0, 0, false, true, 1);
                    TempProperties.setProperty(DEATH_EXP_LOSS_PROPERTY, xpLoss);
                }
                GameEventMgr.AddHandler(this, GamePlayerEvent.Revive, new DOLEventHandler(OnRevive));
            }

            if (this.ControlledBrain != null)
                CommandNpcRelease();

            if (this.SiegeWeapon != null)
                SiegeWeapon.ReleaseControl();

            // sent after buffs drop
            // GamePlayer.Die.CorpseLies:		{0} just died. {1} corpse lies on the ground.
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                player.MessageFromArea(this, LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Die.CorpseLies", player.GetPersonalizedName(this), GetPronoun(this.Client, 1, true)), eChatType.CT_PlayerDied, eChatLoc.CL_SystemWindow);
            }

            if (m_releaseType == eReleaseType.Duel)
            {
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(this == player))
                    {
                        player.MessageFromArea(this, player.GetPersonalizedName(killer) + LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.OnSkillTrained.TrainsInVarious"
                            ), eChatType.CT_Emote, eChatLoc.CL_SystemWindow);
                    }
                }

                foreach (GamePlayer player in this.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    string message = string.Format(player.GetPersonalizedName(killer) + "GameObjects.GamePlayer.Die.DuelWinner");
                    player.MessageFromArea(killer, message, eChatType.CT_OthersCombat, eChatLoc.CL_SystemWindow);
                }
            }

            // deal out exp and realm points based on server rules
            // no other way to keep correct message order...
            GameServer.ServerRules.OnPlayerKilled(this, killer);
            if (m_releaseType != eReleaseType.Duel)
                DeathTime = PlayedTime;

            IsSwimming = false;

            if (RvrManager.Instance.IsInRvr(this))
            {
                GamePlayer killerAsPlayer = killer as GamePlayer;
                if (killerAsPlayer != null && killerAsPlayer.IsInRvR)
                {
                    killerAsPlayer.Guild.GainMeritPoints(25); // 25 flat on player kill to RvR guild
                    if (RvrManager.Instance.Kills.ContainsKey(killerAsPlayer))
                        RvrManager.Instance.Kills[killerAsPlayer]++;
                    else
                        RvrManager.Instance.Kills.Add(killerAsPlayer, 1);
                }
                return;
            }

            if (killer is GamePlayer playerKiller)
            {
                PlayerKilledByPlayer(playerKiller);
            }

            for (int slot = (int)eInventorySlot.FirstBackpack; slot <= (int)eInventorySlot.LastBackpack; slot++)
            {
                base.Die(killer);
                InventoryItem item = Inventory.GetItem((eInventorySlot)slot);

                if (item is DieTriggerSpell dieTrigger)
                {
                    dieTrigger.OnPlayerDie(this, killer);
                    break; // stop looking, only use the first item we find
                }
            }

            DropPvPTreasuresOnDeath(killer);
            DropFlagsOnDeath((GameLiving)killer);
        }

        protected virtual void DropPvPTreasuresOnDeath(GameObject killer)
        {
            if (!IsInPvP) return;

            var pm = PvpManager.Instance;
            if (pm == null || !pm.IsOpen) return;
            // if (pm.CurrentSession?.SessionType != 3) return;

            List<InventoryItem> treasureStacks = new List<InventoryItem>();
            for (eInventorySlot slot = eInventorySlot.FirstBackpack;
                 slot <= eInventorySlot.LastBackpack; slot++)
            {
                var item = Inventory.GetItem(slot);
                if (item is PvPTreasure)
                {
                    treasureStacks.Add(item);
                }
            }
            if (treasureStacks.Count == 0)
                return;

            int totalCarried = treasureStacks.Sum(t => t.Count);
            if (totalCarried < 1) return; // none to drop
            int dropCount = Util.Random(2, 10);
            dropCount = Math.Min(dropCount, totalCarried);

            int droppedSoFar = 0;
            for (int i = 0; i < dropCount; i++)
            {
                int idx = Util.Random(treasureStacks.Count - 1);
                InventoryItem chosenStack = treasureStacks[idx];

                chosenStack.Count--;
                if (chosenStack.Count <= 0)
                {
                    Inventory.RemoveItem(chosenStack);
                    treasureStacks.RemoveAt(idx);

                    if (treasureStacks.Count == 0 && i < dropCount - 1)
                    {
                        break;
                    }
                }
                else
                {
                    // optional: 
                    //   player.Out.SendInventorySlotsUpdate(...) 
                    // or  GameServer.Database.SaveObject(chosenStack);
                }

                var singleTreasure = new PvPTreasure(chosenStack)
                {
                    Count = 1
                };
                var worldItem = new WorldInventoryItem(singleTreasure);

                double angle = Util.RandomDouble() * 2 * Math.PI;
                double distance = Util.Random(100);
                int xoff = (int)(distance * Math.Cos(angle));
                int yoff = (int)(distance * Math.Sin(angle));

                worldItem.Position = Position.Create(
                    CurrentRegionID,
                    Position.X + xoff,
                    Position.Y + yoff,
                    Position.Z,
                    Heading
                );

                worldItem.AddToWorld();
                droppedSoFar++;
            }

            if (droppedSoFar > 0)
            {
                Out.SendMessage($"You have dropped {droppedSoFar} of your treasure item(s) upon dying!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        private void DropFlagsOnDeath(GameObject killer)
        {
            GameLiving livingKiller = killer as GameLiving;
            if (livingKiller == null)
            {
                return;
            }

            if (!PvpManager.Instance.IsOpen || PvpManager.Instance.CurrentSessionType is not PvpManager.eSessionTypes.CaptureTheFlag)
                return;

            var toRemove = new List<FlagInventoryItem>();
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                var item = Inventory.GetItem(slot);
                if (item is FlagInventoryItem flagItem)
                    toRemove.Add(flagItem);
            }

            if (toRemove.Count == 0) return;

            foreach (var flagItem in toRemove)
            {
                flagItem.DropFlagOnGround(this, livingKiller);
            }
        }

        /// <summary>
        /// Player head as an item template
        /// </summary>
        protected ItemTemplate m_headTemplate = null;

        /// <summary>
        /// Player head as an item template
        /// </summary>
        public ItemTemplate HeadTemplate
        {
            get
            {
                m_headTemplate ??= (GameServer.Database.FindObjectByKey<ItemTemplate>("head_blacklist") ?? new ItemTemplate());
                return m_headTemplate;
            }
        }

        public long m_lastHeadDropTime = 0;

        private void PlayerKilledByPlayer(GamePlayer killer)
        {
            if (this == killer)
                return;

            //old system
            //var inBL = IsBlacklisted(victim);
            if (GameServer.ServerRules.IsInPvPArea(this) || TerritoryManager.GetCurrentTerritory(this) != null)
            {
                return;
            }

            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (Reputation < 0 && now - m_lastHeadDropTime >= Properties.PLAYER_HEAD_DROP_COOLDOWN_SECONDS)
            {
                ItemUnique iu = new ItemUnique(HeadTemplate)
                {
                    Name = "Tête de " + Name,
                    MessageArticle = InternalID + ";" + Reputation,
                    CanDropAsLoot = true,
                    MaxCondition = (int)DateTime.Now.Subtract(new DateTime(2000, 1, 1)).TotalSeconds
                };
                GameServer.Database.AddObject(iu);
                m_lastHeadDropTime = now;
                if (killer.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, GameInventoryItem.Create(iu)))
                    killer.SendMessage("Vous avez récupéré la tête de " + Name + ".", eChatType.CT_Loot);
                else
                    killer.SendMessage("Vous n'avez pas pu récupérer la tête de " + Name + ", votre inventaire est plein!", eChatType.CT_Loot);
            }
        }


        public override void EnemyKilled(GameLiving enemy)
        {
            if (Group != null)
            {
                foreach (GamePlayer player in Group.GetPlayersInTheGroup())
                {
                    if (player == this) continue;
                    if (enemy.Attackers.Contains(player)) continue;
                    if (this.IsWithinRadius(player, WorldMgr.MAX_EXPFORKILL_DISTANCE))
                    {
                        Notify(GameLivingEvent.EnemyKilled, player, new EnemyKilledEventArgs(enemy));
                    }

                    if (player.Attackers.Contains(enemy))
                        player.RemoveAttacker(enemy);

                    if (player.ControlledBrain != null && player.ControlledBrain.Body.Attackers.Contains(enemy))
                        player.ControlledBrain.Body.RemoveAttacker(enemy);
                }
            }

            if (ControlledBrain != null && ControlledBrain.Body.Attackers.Contains(enemy))
                ControlledBrain.Body.RemoveAttacker(enemy);

            base.EnemyKilled(enemy);
        }


        /// <summary>
        /// Check this flag to see wether this living is involved in combat
        /// </summary>
        public override bool InCombat
        {
            get
            {
                IControlledBrain npc = ControlledBrain;
                if (npc != null && npc.Body.InCombat)
                    return true;
                return base.InCombat;
            }
        }

        /// <summary>
        /// Easy method to get the resist of a certain damage type
        /// Good for when we add RAs
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public override int GetDamageResist(eProperty property)
        {
            int res = 0;
            int classResist = 0;
            int secondResist = 0;

            //Q: Do the Magic resist bonuses from Bedazzling Aura and Empty Mind stack with each other?
            //A: Nope.
            switch ((eResist)property)
            {
                case eResist.Body:
                case eResist.Cold:
                case eResist.Energy:
                case eResist.Heat:
                case eResist.Matter:
                case eResist.Spirit:
                    res += BaseBuffBonusCategory[(int)eProperty.MagicAbsorption];
                    break;
                default:
                    break;
            }
            return (int)((res + classResist) - 0.01 * secondResist * (res + classResist) + secondResist);
        }

        #endregion

        #region Duel
        /// <summary>
        /// Gets the duel target of this player
        /// </summary>
        public GamePlayer DuelTarget { get { return Duel != null ? Duel.Target : null; } }

        /// <summary>
        /// Get the GameDuel of this player
        /// </summary>
        protected GameDuel Duel { get; set; }

        /// <summary>
        /// Starts the duel
        /// </summary>
        /// <param name="duelTarget">The duel target</param>
        public virtual void DuelStart(GamePlayer duelTarget)
        {
            if (Duel != null)
                return;

            Duel = new GameDuel(this, duelTarget);
            Duel.Start();
        }

        /// <summary>
        /// Stops the duel if it is running
        /// </summary>
        public void DuelStop()
        {
            if (Duel == null)
                return;

            Duel.Stop();
            Duel = null;
        }
        #endregion

        #region Spell cast

        /// <summary>
        /// The time someone can not cast
        /// </summary>
        protected long m_disabledCastingTimeout = 0;
        /// <summary>
        /// Time when casting is allowed again (after interrupt from enemy attack)
        /// </summary>
        public long DisabledCastingTimeout
        {
            get { return m_disabledCastingTimeout; }
            set { m_disabledCastingTimeout = value; }
        }

        /// <summary>
        /// Grey out some skills on client for specified duration
        /// </summary>
        /// <param name="skill">the skill to disable</param>
        /// <param name="duration">duration of disable in milliseconds</param>
        public override void DisableSkill(Skill skill, int duration)
        {
            if (this.Client.Account.PrivLevel > 1)
                return;

            base.DisableSkill(skill, duration);

            var disables = new List<Tuple<Skill, int>>();
            disables.Add(new Tuple<Skill, int>(skill, duration));

            Out.SendDisableSkill(disables);
        }

        /// <summary>
        /// Grey out collection of skills on client for specified duration
        /// </summary>
        /// <param name="skill">the skill to disable</param>
        /// <param name="duration">duration of disable in milliseconds</param>
        public override void DisableSkill(ICollection<Tuple<Skill, int>> skills)
        {
            if (this.Client.Account.PrivLevel > 1)
                return;

            base.DisableSkill(skills);

            Out.SendDisableSkill(skills);
        }

        /// <summary>
        /// The next spell
        /// </summary>
        protected Spell m_nextSpell;
        /// <summary>
        /// The next spell line
        /// </summary>
        protected SpellLine m_nextSpellLine;
        /// <summary>
        /// The next spell target
        /// </summary>
        protected GameLiving m_nextSpellTarget;
        /// <summary>
        /// A lock for the spellqueue
        /// </summary>
        protected object m_spellQueueAccessMonitor = new object();

        /// <summary>
        /// Clears the spell queue when a player is interrupted
        /// </summary>
        public void ClearSpellQueue()
        {
            lock (m_spellQueueAccessMonitor)
            {
                m_nextSpell = null;
                m_nextSpellLine = null;
                m_nextSpellTarget = null;
            }
        }

        private void FinalizeItemCast(ISpellHandler handler)
        {
            InventoryItem useItem = null;
            TempProperties.removeAndGetProperty(LAST_USED_ITEM_SPELL, out useItem);
            if (handler is SpellHandler { Item: { } spellItem } sHandler)
            {
                if (sHandler.Status != SpellHandler.eStatus.Success)
                    return;
                useItem = spellItem;
            }

            if (useItem == null)
                return;
            
            if (handler.StartReuseTimer)
            {
                useItem.CanUseAgainIn = useItem.CanUseEvery;
            }

            if (useItem.Count > 1)
            {
                Inventory.RemoveCountFromStack(useItem, 1);
                InventoryLogging.LogInventoryAction(this, "", "(potion)", eInventoryActionType.Other, useItem, 1);
            }
            else
            {
                useItem.Charges--;
                if (useItem.Charges < 1)
                {
                    Inventory.RemoveCountFromStack(useItem, 1);
                    InventoryLogging.LogInventoryAction(this, "", "(potion)", eInventoryActionType.Other, useItem, 1);
                }
            }
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.Used", useItem.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            if (useItem.IsPotion())
                TempProperties.setProperty(NEXT_POTION_AVAIL_TIME + "_Type" + (handler.Spell.SharedTimerGroup), useItem.CanUseEvery * 1000 + CurrentRegion.Time);
            else if (useItem.IsParchment())
                TempProperties.setProperty(NEXT_PARCH_AVAIL_TIME + "_Type" + (handler.Spell.SharedTimerGroup), useItem.CanUseEvery * 1000 + CurrentRegion.Time);
        }

        /// <summary>
        /// Callback after spell execution finished and next spell can be processed
        /// </summary>
        /// <param name="handler"></param>
        public override void OnAfterSpellCastSequence(ISpellHandler handler)
        {
            FinalizeItemCast(handler);

            lock (m_spellQueueAccessMonitor)
            {
                Spell nextSpell = m_nextSpell;
                SpellLine nextSpellLine = m_nextSpellLine;
                GameLiving nextSpellTarget = m_nextSpellTarget;
                // warlock
                if (nextSpell != null)
                {
                    if (nextSpell.IsSecondary)
                    {
                        GameSpellEffect effect = SpellHandler.FindEffectOnTarget(this, "Powerless");
                        if (effect == null)
                            effect = SpellHandler.FindEffectOnTarget(this, "Range");
                        if (effect == null)
                            effect = SpellHandler.FindEffectOnTarget(this, "Uninterruptable");

                        if (effect != null)
                            effect.Cancel(false);
                    }

                }
                m_runningSpellHandler = null;
                m_nextSpell = null;         // avoid restarting nextspell by reentrance from spellhandler
                m_nextSpellLine = null;
                m_nextSpellTarget = null;

                if (nextSpell != null)
                    m_runningSpellHandler = ScriptMgr.CreateSpellHandler(this, nextSpell, nextSpellLine);
            }
            if (m_runningSpellHandler != null)
            {
                m_runningSpellHandler.CastingCompleteEvent += new CastingCompleteCallback(OnAfterSpellCastSequence);
                if (m_nextSpellTarget != null)
                    m_runningSpellHandler.CastSpell(m_nextSpellTarget);
                else
                    m_runningSpellHandler.CastSpell();
            }
        }
        

        /// <summary>
        /// Cast a specific spell from given spell line
        /// </summary>
        /// <param name="spell">spell to cast</param>
        /// <param name="line">Spell line of the spell (for bonus calculations)</param>
        /// <param name="useItem">Item used (may be null)</param>
        /// <returns>Whether the spellcast started successfully</returns>
        public bool CastSpell(Spell spell, SpellLine line, InventoryItem useItem)
        {
            bool casted = false;

            if (IsCrafting)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.InterruptedCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                StopCrafting();
            }

            if (spell.SpellType == "BodyguardHandler")
            {
                Ability ab = SkillBase.GetAbility("Bodyguard");
                IAbilityActionHandler handler = SkillBase.GetAbilityActionHandler(ab.KeyName);
                if (handler != null)
                {
                    handler.Execute(ab, this);
                    return true;
                }
            }
            else
            {
                if (spell.SpellType != "StyleHandler" && spell.SpellType != "MLStyleHandler")
                {
                    if (IsStunned)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CantCastStunned"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                        return false;
                    }
                    if (IsMezzed)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CantCastMezzed"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                        return false;
                    }

                    if (IsSilenced)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CantCastFumblingWords"), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                        return false;
                    }

                    double fumbleChance = GetModified(eProperty.SpellFumbleChance);
                    fumbleChance *= 0.01;
                    if (fumbleChance > 0)
                    {
                        if (Util.ChanceDouble(fumbleChance))
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CantCastFumblingWords"), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                            return false;
                        }
                    }

                    if (spell.SpellType == "Anguish" || spell.SpellType == "Agony" || spell.SpellType == "Doom")
                    {
                        GameLiving castTarget = TargetObject as GameLiving;
                        if (castTarget == null)
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "Spell.CharmSpell.InvalidTarget"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                            return false;
                        }

                        bool hasDread = false;
                        bool hasAnguish = false;
                        bool hasAgony = false;

                        foreach (GameSpellEffect eff in castTarget.EffectList)
                        {
                            if (eff.SpellHandler != null)
                            {
                                string effType = eff.SpellHandler.Spell.SpellType;
                                if (effType == "Dread") hasDread = true;
                                if (effType == "Anguish") hasAnguish = true;
                                if (effType == "Agony") hasAgony = true;
                            }
                        }

                        if (spell.SpellType == "Anguish" && !hasDread)
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CantSpellDirectly"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                            return false;
                        }

                        if (spell.SpellType == "Agony" && (!hasDread || !hasAnguish))
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "SpellDescription.Agony.ConditionDescription"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                            return false;
                        }

                        if (spell.SpellType == "Doom" && (!hasDread || !hasAnguish || !hasAgony))
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "SpellDescription.Doom.ConditionDescription"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                            return false;
                        }
                    }
                }

                lock (m_spellQueueAccessMonitor)
                {
                    if (m_runningSpellHandler != null)
                    {
                        if (m_runningSpellHandler.CanQueue == false)
                        {
                            m_runningSpellHandler.CasterMoves();
                            return false;
                        }

                        if (spell.CastTime > 0 && !(m_runningSpellHandler is ChamberSpellHandler) && spell.SpellType != "Chamber")
                        {
                            if (m_runningSpellHandler.Spell.InstrumentRequirement != 0)
                            {
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.AlreadyPlaySong"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                                return false;
                            }
                            if (SpellQueue)
                            {
                                if (spell.SpellType.ToLower() == "archery")
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.FollowSpell", spell.Name), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                                }
                                else
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.AlreadyCastFollow"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                                }

                                m_nextSpell = spell;
                                m_nextSpellLine = line;
                                m_nextSpellTarget = TargetObject as GameLiving;
                                return true;
                            }
                            else Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.AlreadyCastNoQueue"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);

                            return false;
                        }
                        else if (m_runningSpellHandler is PrimerSpellHandler)
                        {
                            if (!spell.IsSecondary)
                            {
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.OnlyASecondarySpell"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                if (SpellQueue && !(m_runningSpellHandler is ChamberSpellHandler))
                                {
                                    Spell cloneSpell = null;
                                    if (m_runningSpellHandler is PowerlessSpellHandler)
                                    {
                                        cloneSpell = spell.Copy();
                                        cloneSpell.CostPower = false;
                                        m_nextSpell = cloneSpell;
                                        m_nextSpellLine = line;
                                        casted = true;
                                    }
                                    else if (m_runningSpellHandler is RangeSpellHandler)
                                    {
                                        cloneSpell = spell.Copy();
                                        cloneSpell.CostPower = false;
                                        cloneSpell.OverrideRange = m_runningSpellHandler.Spell.Range;
                                        m_nextSpell = cloneSpell;
                                        m_nextSpellLine = line;
                                        casted = true;
                                    }
                                    else if (m_runningSpellHandler is UninterruptableSpellHandler)
                                    {
                                        cloneSpell = spell.Copy();
                                        cloneSpell.CostPower = false;
                                        m_nextSpell = cloneSpell;
                                        m_nextSpellLine = line;
                                        casted = true;
                                    }
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.PrepareSecondarySpell"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                                }
                                return casted;
                            }
                        }
                        else if (m_runningSpellHandler is ChamberSpellHandler)
                        {
                            ChamberSpellHandler chamber = (ChamberSpellHandler)m_runningSpellHandler;
                            if (IsMoving || IsStrafing)
                            {
                                m_runningSpellHandler = null;
                                return false;
                            }
                            if (spell.IsPrimary)
                            {
                                if (spell.SpellType == "Bolt" && !chamber.Spell.AllowBolt)
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.SpellNotInChamber"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                                    return false;
                                }
                                if (chamber.PrimarySpell == null)
                                {
                                    Spell cloneSpell = spell.Copy();
                                    cloneSpell.InChamber = true;
                                    cloneSpell.CostPower = false;
                                    chamber.PrimarySpell = cloneSpell;
                                    chamber.PrimarySpellLine = line;
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.SpellInChamber", spell.Name, ((ChamberSpellHandler)m_runningSpellHandler).Spell.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.SelectSecondSpell", ((ChamberSpellHandler)m_runningSpellHandler).Spell.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                }
                                else
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.SpellNotInChamber"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                                }
                            }
                            else if (spell.IsSecondary)
                            {
                                if (chamber.PrimarySpell != null)
                                {
                                    if (chamber.SecondarySpell == null)
                                    {
                                        Spell cloneSpell = spell.Copy();
                                        cloneSpell.CostPower = false;
                                        cloneSpell.InChamber = true;
                                        cloneSpell.OverrideRange = chamber.PrimarySpell.Range;
                                        chamber.SecondarySpell = cloneSpell;
                                        chamber.SecondarySpellLine = line;

                                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.SpellInChamber", spell.Name, ((ChamberSpellHandler)m_runningSpellHandler).Spell.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    }
                                    else
                                    {
                                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.AlreadyChosenSpells"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                                    }
                                }
                                else
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.PrimarySpellFirst"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                                }
                            }

                        }
                        else if (!(m_runningSpellHandler is ChamberSpellHandler) && spell.SpellType == "Chamber")
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.NotAFollowSpell"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                            return false;
                        }
                    }
                }
                ISpellHandler spellhandler = ScriptMgr.CreateSpellHandler(this, spell, line);
                if (spellhandler == null)
                {
                    Out.SendMessage(spell.Name + " not implemented yet (" + spell.SpellType + ")", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (spell.CastTime > 0)
                {
                    GameSpellEffect effect = SpellHandler.FindEffectOnTarget(this, "Chamber", spell.Name);

                    if (effect != null && spell.Name == effect.Spell.Name)
                    {
                        casted = spellhandler.CastSpell(useItem);
                    }
                    else
                    {
                        if (spellhandler is ChamberSpellHandler && m_runningSpellHandler == null)
                        {
                            ((ChamberSpellHandler)spellhandler).EffectSlot = ChamberSpellHandler.GetEffectSlot(spellhandler.Spell.Name);
                            m_runningSpellHandler = spellhandler;
                            m_runningSpellHandler.CastingCompleteEvent += new CastingCompleteCallback(OnAfterSpellCastSequence);
                            casted = spellhandler.CastSpell(useItem);
                        }
                        else if (m_runningSpellHandler == null)
                        {
                            m_runningSpellHandler = spellhandler;
                            m_runningSpellHandler.CastingCompleteEvent += new CastingCompleteCallback(OnAfterSpellCastSequence);
                            casted = spellhandler.CastSpell(useItem);
                        }
                    }
                    return casted;
                }
                if (spell.IsSecondary)
                {
                    GameSpellEffect effect = SpellHandler.FindEffectOnTarget(this, "Powerless");
                    if (effect == null)
                        effect = SpellHandler.FindEffectOnTarget(this, "Range");
                    if (effect == null)
                        effect = SpellHandler.FindEffectOnTarget(this, "Uninterruptable");

                    if (m_runningSpellHandler == null && effect == null)
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CantSpellDirectly"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    else if (m_runningSpellHandler != null)
                    {
                        if (m_runningSpellHandler.Spell.IsPrimary)
                        {
                            lock (m_spellQueueAccessMonitor)
                            {
                                if (SpellQueue && !(m_runningSpellHandler is ChamberSpellHandler))
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.PrepareSecondarySpell"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                                    m_nextSpell = spell;
                                    spell.OverrideRange = m_runningSpellHandler.Spell.Range;
                                    m_nextSpellLine = line;
                                    casted = true;
                                }
                            }
                        }
                        else if (!(m_runningSpellHandler is ChamberSpellHandler))
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.CantSpellDirectly"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    }
                    else if (effect != null)
                    {
                        Spell cloneSpell = null;
                        if (effect.SpellHandler is PowerlessSpellHandler)
                        {
                            cloneSpell = spell.Copy();
                            cloneSpell.CostPower = false;
                            spellhandler = ScriptMgr.CreateSpellHandler(this, cloneSpell, line);
                            casted = spellhandler.CastSpell(useItem);
                            effect.Cancel(false);
                        }
                        else if (effect.SpellHandler is RangeSpellHandler)
                        {
                            cloneSpell = spell.Copy();
                            cloneSpell.CostPower = false;
                            cloneSpell.OverrideRange = effect.Spell.Range;
                            spellhandler = ScriptMgr.CreateSpellHandler(this, cloneSpell, line);
                            casted = spellhandler.CastSpell(useItem);
                            effect.Cancel(false);
                        }
                        else if (effect.SpellHandler is UninterruptableSpellHandler)
                        {
                            cloneSpell = spell.Copy();
                            cloneSpell.CostPower = false;
                            spellhandler = ScriptMgr.CreateSpellHandler(this, cloneSpell, line);
                            casted = spellhandler.CastSpell(useItem);
                            effect.Cancel(false);
                        }
                    }
                }
                else
                    casted = spellhandler.CastSpell(useItem);
                if (casted)
                {
                    // Instant spells: call OnAfterSpellCastSequence immediately
                    OnAfterSpellCastSequence(spellhandler);
                }
            }
            return casted;
        }

        /// <summary>
        /// Cast a specific spell from given spell line
        /// </summary>
        /// <param name="spell">spell to cast</param>
        /// <param name="line">Spell line of the spell (for bonus calculations)</param>
        /// <returns>Whether the spellcast started successfully</returns>
        public override bool CastSpell(Spell spell, SpellLine line)
        {
            return CastSpell(spell, line, null);
        }

        public override bool CastSpell(ISpellCastingAbilityHandler ab)
        {
            bool casted = false;

            if (IsCrafting)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.InterruptedCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                StopCrafting();
            }

            ISpellHandler spellhandler = ScriptMgr.CreateSpellHandler(this, ab.Spell, ab.SpellLine);
            if (spellhandler != null)
            {
                // Instant cast abilities should not interfere with the spell queue
                if (spellhandler.Spell.CastTime > 0)
                {
                    m_runningSpellHandler = spellhandler;
                    m_runningSpellHandler.CastingCompleteEvent += new CastingCompleteCallback(OnAfterSpellCastSequence);
                }

                spellhandler.Ability = ab;
                casted = spellhandler.CastSpell();
            }
            else
            {
                Out.SendMessage(ab.Spell.Name + " not implemented yet (" + ab.Spell.SpellType + ")", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            return casted;
        }

        /// <summary>
        /// Calculate how fast this player can cast a given spell
        /// </summary>
        /// <param name="spell"></param>
        /// <returns></returns>
        public override int CalculateCastingTime(SpellLine line, Spell spell)
        {
            int ticks = spell.CastTime;

            if (spell.InstrumentRequirement != 0 ||
                line.KeyName == GlobalSpellsLines.Item_Spells ||
                line.KeyName.StartsWith(GlobalSpellsLines.Champion_Lines_StartWith))
            {
                return ticks;
            }

            if (CharacterClass.CanChangeCastingSpeed(line, spell) == false)
                return ticks;

            if (EffectList.GetOfType<QuickCastEffect>() != null)
            {
                // Most casters have access to the Quickcast ability (or the Necromancer equivalent, Facilitate Painworking).
                // This ability will allow you to cast a spell without interruption.
                // http://support.darkageofcamelot.com/kb/article.php?id=022

                // A: You're right. The answer I should have given was that Quick Cast reduces the time needed to cast to a flat two seconds,
                // and that a spell that has been quick casted cannot be interrupted. ...
                // http://www.camelotherald.com/news/news_article.php?storyid=1383

                return 2000;
            }


            double percent = DexterityCastTimeReduction;

            percent *= 1.0 - GetModified(eProperty.CastingSpeed) * 0.01;

            ticks = (int)(ticks * Math.Max(CastingSpeedReductionCap, percent));
            if (ticks < MinimumCastingSpeed)
                ticks = MinimumCastingSpeed;

            return ticks;
        }


        #endregion

        #region Realm Abilities
        /// <summary>
        /// This is the timer used to count time when a player casts a RA
        /// </summary>
        private RegionTimer m_realmAbilityCastTimer;

        /// <summary>
        /// Get and set the RA cast timer
        /// </summary>
        public RegionTimer RealmAbilityCastTimer
        {
            get { return m_realmAbilityCastTimer; }
            set { m_realmAbilityCastTimer = value; }
        }

        /// <summary>
        /// Does the player is casting a realm ability
        /// </summary>
        public bool IsCastingRealmAbility
        {
            get { return (m_realmAbilityCastTimer != null && m_realmAbilityCastTimer.IsAlive); }
        }
        #endregion

        #region Vault/Money/Items/Trading/UseSlot/ApplyPoison

        private IGameInventoryObject m_activeInventoryObject;

        /// <summary>
        /// The currently active InventoryObject
        /// This is new and will probably replace the above Active methods in time.
        /// </summary>
        public IGameInventoryObject ActiveInventoryObject
        {
            get { return m_activeInventoryObject; }
            set { m_activeInventoryObject = value; }
        }

        /// <summary>
        /// Property that holds tick when charged item was used last time
        /// </summary>
        public const string LAST_CHARGED_ITEM_USE_TICK = "LastChargedItemUsedTick";
        public const string ITEM_USE_DELAY = "ItemUseDelay";
        public const string NEXT_POTION_AVAIL_TIME = "LastPotionItemUsedTick";
        public const string NEXT_PARCH_AVAIL_TIME = "LastParchItemUsedTick";
        public const string NEXT_SPELL_AVAIL_TIME_BECAUSE_USE_POTION = "SpellAvailableTime";

        /// <summary>
        /// Called when this player receives a trade item
        /// </summary>
        /// <param name="source">the source of the item</param>
        /// <param name="item">the item</param>
        /// <returns>true to accept, false to deny the item</returns>
        public virtual bool ReceiveTradeItem(GamePlayer source, InventoryItem item)
        {
            if (source == null || item == null || source == this)
                return false;

            lock (m_LockObject)
            {
                lock (source)
                {
                    if (item.IsTradable == false && source.CanTradeAnyItem == false && CanTradeAnyItem == false)
                    {
                        source.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ReceiveTradeItem.CantTrade"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }

                    if (source.TradeWindow != null && source.TradeWindow.Partner != this)
                    {
                        GamePlayer sourceTradePartner = source.TradeWindow.Partner;
                        if (sourceTradePartner == null)
                        {
                            source.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ReceiveTradeItem.StillSelfcrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            source.Out.SendMessage("You are still trading with " + source.GetPersonalizedName(sourceTradePartner) + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        return false;
                    }

                    if (TradeWindow == null)
                    {
                        if (!OpenTrade(source))
                        {
                            source.Out.SendMessage("An error occured while trading with " + source.GetPersonalizedName(this), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return false;
                        }
                        if (!source.TradeWindow!.AddItemToTrade(item))
                        {
                            source.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ReceiveTradeItem.CantTrade"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            TradeWindow!.CloseTrade();
                            return false;
                        }
                        return true;
                    }

                    if (source != TradeWindow.Partner)
                    {
                        GamePlayer partner = TradeWindow.Partner;
                        if (partner == null)
                        {
                            source.Out.SendMessage(source.GetPersonalizedName(this) + " is still selfcrafting.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            source.Out.SendMessage(source.GetPersonalizedName(this) + " is still trading with " + source.GetPersonalizedName(partner) + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        return false;
                    }

                    if (!source.TradeWindow!.AddItemToTrade(item))
                    {
                        source.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ReceiveTradeItem.CantTrade"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }
                    return true;
                }
            }
        }

        /// <summary>
        /// Called when the player receives trade money
        /// </summary>
        /// <param name="source">the source</param>
        /// <param name="money">the money value</param>
        /// <returns>true to accept, false to deny</returns>
        public virtual bool ReceiveTradeMoney(GamePlayer source, long money)
        {
            if (source == null || source == this || money == 0)
                return false;

            lock (m_LockObject)
            {
                lock (source)
                {
                    if ((TradeWindow != null && source != TradeWindow.Partner) || (TradeWindow == null && !OpenTrade(source)))
                    {
                        if (TradeWindow != null)
                        {
                            GamePlayer partner = TradeWindow.Partner;
                            if (partner == null)
                            {
                                source.Out.SendMessage(source.GetPersonalizedName(this) + " is still selfcrafting.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                source.Out.SendMessage(source.GetPersonalizedName(this) + " is still trading with " + source.GetPersonalizedName(partner) + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                        }
                        else if (source.TradeWindow != null)
                        {
                            GamePlayer sourceTradePartner = source.TradeWindow.Partner;
                            if (sourceTradePartner == null)
                            {
                                source.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ReceiveTradeItem.StillSelfcrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                source.Out.SendMessage("You are still trading with " + source.GetPersonalizedName(sourceTradePartner) + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                        }
                        return false;
                    }

                    source.TradeWindow.AddMoneyToTrade(money);
                    return true;
                }
            }
        }

        #region Money
        [Obsolete("Use DOL.GS.Money.GetMithril(long) instead.")]
        public virtual int Mithril => Money.GetMithril(CopperBalance);
        [Obsolete("Use DOL.GS.Money.GetPlatinum(long) instead.")]
        public virtual int Platinum => Money.GetPlatinum(CopperBalance);
        [Obsolete("Use DOL.GS.Money.GetGold(long) instead.")]
        public virtual int Gold => Money.GetGold(CopperBalance);
        [Obsolete("Use DOL.GS.Money.GetSilver(long) instead.")]
        public virtual int Silver => Money.GetSilver(CopperBalance);
        [Obsolete("Use DOL.GS.Money.GetCopper(long) instead.")]
        public virtual int Copper => Money.GetCopper(CopperBalance);

        private Wallet Wallet { get; }

        public DOL.GS.Finance.Money GetBalance(Currency currency) => Wallet.GetBalance(currency);
        public long CopperBalance => GetBalance(Currency.Copper).Amount;
        public long BountyPointBalance => GetBalance(Currency.BountyPoints).Amount;

        public void AddMoney(DOL.GS.Finance.Money money) => Wallet.AddMoney(money);
        public bool RemoveMoney(DOL.GS.Finance.Money money) => Wallet.RemoveMoney(money);

        [Obsolete("Use CopperBalance instead.")]
        public virtual long GetCurrentMoney() => CopperBalance;

        [Obsolete("Use AddMoney(Money) instead.")]
        public virtual void AddMoney(long copperAmount)
        {
            if (copperAmount >= 0) AddMoney(Currency.Copper.Mint(copperAmount));
            else RemoveMoney(Currency.Copper.Mint(-copperAmount));
        }

        [Obsolete("Use AddMoney(Money) and SendSystemMessage(string) instead.")]
        public virtual void AddMoney(long copperAmount, string message)
        {
            AddMoney(copperAmount);
            if (message != null) SendSystemMessage(string.Format(message, Money.GetString(copperAmount)));
        }

        [Obsolete("Use AddMoney(Money) and SendMessage(string,eChatType,eChatLoc) instead.")]
        public virtual void AddMoney(long copperAmount, string messageFormat, eChatType ct, eChatLoc cl)
        {
            AddMoney(copperAmount);
            if (messageFormat != null) SendMessage(string.Format(messageFormat, Money.GetString(copperAmount)), ct, cl);
        }

        [Obsolete("Use RemoveMoney(Money) and SendSystemMessage(string) instead.")]
        public virtual bool RemoveMoney(long copperAmount)
        {
            if (copperAmount >= 0) return RemoveMoney(Currency.Copper.Mint(copperAmount));
            else
            {
                AddMoney(Currency.Copper.Mint(-copperAmount));
                return true;
            }
        }

        [Obsolete("Use RemoveMoney(Money) and SendSystemMessage(string) instead.")]
        public virtual bool RemoveMoney(long money, string messageFormat)
        {
            var hasEnoughMoney = RemoveMoney(money);
            if (hasEnoughMoney && messageFormat != null && money != 0) SendSystemMessage(messageFormat);
            return hasEnoughMoney;
        }

        [Obsolete("Use RemoveMoney(Money) and SendMessage(string,eChatType,eChatLoc) instead.")]
        public virtual bool RemoveMoney(long money, string messageFormat, eChatType ct, eChatLoc cl)
        {
            var hasEnoughMoney = RemoveMoney(money);
            if (hasEnoughMoney && messageFormat != null && money != 0) SendMessage(messageFormat, ct, cl);
            return hasEnoughMoney;
        }
        #endregion
        private InventoryItem m_useItem;

        /// <summary>
        /// The item the player is trying to use.
        /// </summary>
        public InventoryItem UseItem
        {
            get { return m_useItem; }
            set { m_useItem = value; }
        }

        /// <summary>
        /// Called when the player uses an inventory in a slot
        /// eg. by clicking on the icon in the qickbar dragged from a slot
        /// </summary>
        /// <param name="slot">inventory slot used</param>
        /// <param name="type">type of slot use (0=simple click on icon, 1=use, 2=/use2)</param>
        public virtual void UseSlot(eInventorySlot slot, eUseType type)
        {
            UseSlot((int)slot, (int)type);
        }

        public virtual void UseSlot(int slot, int type)
        {
            if (JailMgr.IsPrisoner(this) || !IsAlive)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantFire"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (PlayerAfkMessage != null)
            {
                this.ResetAFK(false);
            }

            lock (Inventory)
            {
                InventoryItem useItem = Inventory.GetItem((eInventorySlot)slot);
                UseItem = useItem;

                if (useItem == null)
                {
                    if ((slot >= Slot.FIRSTQUIVER) && (slot <= Slot.FOURTHQUIVER))
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.QuiverSlotEmpty", (slot - (Slot.FIRSTQUIVER) + 1)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        // don't allow using empty slots
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.IllegalSourceObject", slot), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    return;
                }

                if (useItem is IGameInventoryItem)
                {
                    if ((useItem as IGameInventoryItem)!.Use(this))
                    {
                        return;
                    }
                }

                if (useItem.Item_Type >= (int)eInventorySlot.LeftFrontSaddleBag && useItem.Item_Type <= (int)eInventorySlot.RightRearSaddleBag)
                {
                    UseSaddleBag(useItem);
                    return;
                }

                if (useItem.Item_Type != Slot.RANGED && (slot != Slot.HORSE || type != 0))
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.AttemptToUse", useItem.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }

                bool unauthorized = false;
                //Check if Item Can be used in RvR
                if ((this.isInBG || this.IsInPvP || this.IsInRvR) && !useItem.CanUseInRvR)
                {
                    unauthorized = true;
                }
                //prevent magical item use in not authorized zones
                else if (useItem.Object_Type == (int)eObjectType.Magical && !this.CurrentZone.AllowMagicalItem)
                {
                    unauthorized = true;
                }

                if (unauthorized)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantUseHere"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                #region Non-backpack/vault slots

                switch (slot)
                {
                    case Slot.HORSEARMOR:
                    case Slot.HORSEBARDING:
                        return;
                    case Slot.HORSE:
                        if (type == 0)
                        {
                            if (IsOnHorse)
                                IsOnHorse = false;
                            else
                            {
                                if (Level < useItem.Level)
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.SummonHorseLevel", useItem.Level), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }

                                string reason = GameServer.ServerRules.ReasonForDisallowMounting(this);
                                if (!String.IsNullOrEmpty(reason))
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, reason), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    StopWhistleTimers();
                                    return;
                                }

                                if (IsSummoningMount)
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.StopCallingMount"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    StopWhistleTimers();
                                    return;
                                }
                                if (IsAttacking)
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantMountCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    StopWhistleTimers();
                                    return;
                                }
                                if (IsCasting)
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantMountSpell"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    StopWhistleTimers();
                                    return;
                                }

                                if (HasEnabledAPulseSpell)
                                    PulseSpell.CancelPulsingSpell(this, PulseSpell.Spell.SpellType);

                                Out.SendTimerWindow("Summoning Mount", 5);
                                foreach (GamePlayer plr in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                                {
                                    if (plr == null) continue;
                                    plr.Out.SendEmoteAnimation(this, eEmote.Horse_whistle);
                                }
                                // vampiir ~
                                GameSpellEffect effects = SpellHandler.FindEffectOnTarget(this, "VampiirSpeedEnhancement");
                                GameSpellEffect effect = SpellHandler.FindEffectOnTarget(this, "SpeedEnhancement");
                                if (effects != null)
                                    effects.Cancel(false);
                                if (effect != null)
                                    effect.Cancel(false);
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.WhistleMount"), eChatType.CT_Emote, eChatLoc.CL_SystemWindow);
                                m_whistleMountTimer = new RegionTimer(this);
                                m_whistleMountTimer.Callback = new RegionTimerCallback(WhistleMountTimerCallback);
                                m_whistleMountTimer.Start(5000);
                            }
                        }
                        break;
                    case Slot.RIGHTHAND:
                    case Slot.LEFTHAND:
                        if (type != 0) break;
                        if (ActiveWeaponSlot == eActiveWeaponSlot.Standard)
                            break;
                        SwitchWeapon(eActiveWeaponSlot.Standard);
                        Notify(GamePlayerEvent.UseSlot, this, new UseSlotEventArgs(slot, type));
                        return;

                    case Slot.TWOHAND:
                        if (type != 0) break;
                        if (ActiveWeaponSlot == eActiveWeaponSlot.TwoHanded)
                            break;
                        SwitchWeapon(eActiveWeaponSlot.TwoHanded);
                        Notify(GamePlayerEvent.UseSlot, this, new UseSlotEventArgs(slot, type));
                        return;

                    case Slot.RANGED:
                        bool newAttack = false;
                        if (ActiveWeaponSlot != eActiveWeaponSlot.Distance)
                        {
                            SwitchWeapon(eActiveWeaponSlot.Distance);
                        }
                        else if (!AttackState)
                        {
                            StopCurrentSpellcast();
                            StartAttack(TargetObject);
                            newAttack = true;
                        }

                        //Clean up range attack state/type if we are not in combat mode
                        //anymore
                        if (!AttackState)
                        {
                            RangedAttackState = eRangedAttackState.None;
                            RangedAttackType = eRangedAttackType.Normal;
                        }
                        if (!newAttack && RangedAttackState != eRangedAttackState.None)
                        {
                            if (RangedAttackState == eRangedAttackState.ReadyToFire)
                            {
                                RangedAttackState = eRangedAttackState.Fire;
                                StopCurrentSpellcast();
                                m_attackAction.Start(1);
                            }
                            else if (RangedAttackState == eRangedAttackState.Aim)
                            {
                                if (!TargetInView)
                                {
                                    // Don't store last target if it's not visible
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantSeeTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                }
                                else
                                {
                                    if (m_rangeAttackTarget.Target == null)
                                    {
                                        //set new target only if there was no target before
                                        RangeAttackTarget = TargetObject;
                                    }

                                    RangedAttackState = eRangedAttackState.AimFire;
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.AutoReleaseShot"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                }
                            }
                            else if (RangedAttackState == eRangedAttackState.AimFire)
                            {
                                RangedAttackState = eRangedAttackState.AimFireReload;
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.AutoReleaseShotReload"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else if (RangedAttackState == eRangedAttackState.AimFireReload)
                            {
                                RangedAttackState = eRangedAttackState.Aim;
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.NoAutoReleaseShotReload"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                        }
                        break;
                    case Slot.FIRSTQUIVER: SwitchQuiver(eActiveQuiverSlot.First, false); break;
                    case Slot.SECONDQUIVER: SwitchQuiver(eActiveQuiverSlot.Second, false); break;
                    case Slot.THIRDQUIVER: SwitchQuiver(eActiveQuiverSlot.Third, false); break;
                    case Slot.FOURTHQUIVER: SwitchQuiver(eActiveQuiverSlot.Fourth, false); break;
                }

                #endregion

                if (useItem.SpellID != 0 || useItem.SpellID1 != 0 || useItem.PoisonSpellID != 0) // don't return without firing events
                {
                    if (IsSitting)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantUseWhileSitting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    // Item with a non-charge ability.

                    if (((useItem.Object_Type == (int)eObjectType.Magical
                        && useItem.Item_Type == (int)eInventorySlot.FirstBackpack)
                        || (useItem.Object_Type == (int)eObjectType.GenericItem && useItem.SpellID == 666999))
                        && useItem.SpellID > 0
                        && useItem.MaxCharges == 0)
                    {
                        UseMagicalItem(useItem, type);
                        return;
                    }

                    // Artifacts don't require charges.

                    if ((type < 2 && useItem.SpellID > 0 && useItem.Charges < 1 && useItem.MaxCharges > -1 && !(useItem is InventoryArtifact)) ||
                        (type == 2 && useItem.SpellID1 > 0 && useItem.Charges1 < 1 && useItem.MaxCharges1 > -1 && !(useItem is InventoryArtifact)) ||
                        (useItem.PoisonSpellID > 0 && useItem.PoisonCharges < 1))
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.OutOfCharges", useItem.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }
                    else
                    {
                        if (useItem.Object_Type == (int)eObjectType.Poison)
                        {
                            InventoryItem mainHand = AttackWeapon;
                            InventoryItem leftHand = Inventory.GetItem(eInventorySlot.LeftHandWeapon);
                            if (mainHand != null && mainHand.PoisonSpellID == 0)
                            {
                                ApplyPoison(useItem, mainHand);
                            }
                            else if (leftHand != null && leftHand.PoisonSpellID == 0)
                            {
                                ApplyPoison(useItem, leftHand);
                            }
                        }
                        else if (useItem.SpellID > 0 && useItem.Charges > 0 && useItem.Object_Type == (int)eObjectType.Magical && (useItem.Item_Type == (int)eInventorySlot.FirstBackpack || useItem.Item_Type == 41))
                        {
                            UseChargeMagicalItem(useItem, type);
                        }
                        else if (type > 0)
                        {
                            if (!Inventory.EquippedItems.Contains(useItem))
                            {
                                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantUseFromBackpack"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                long lastChargedItemUseTick = TempProperties.getProperty<long>(LAST_CHARGED_ITEM_USE_TICK + useItem.Id_nb);
                                long changeTime = CurrentRegion.Time - lastChargedItemUseTick;
                                //long delay = TempProperties.getProperty<long>(ITEM_USE_DELAY, 0L);
                                long itemdelay = TempProperties.getProperty<long>("ITEMREUSEDELAY" + useItem.Id_nb);
                                long itemreuse = (long)useItem.CanUseEvery * 1000;
                                if (itemdelay == 0) itemdelay = CurrentRegion.Time - itemreuse;

                                if ((IsStunned && !(Steed != null && Steed.Name == "Forceful Zephyr")) || IsMezzed || !IsAlive)
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantDischargeInState"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                }
                                else if (Client.Account.PrivLevel == 1 && (CurrentRegion.Time - itemdelay) < itemreuse) //2 minutes reuse timer
                                {
                                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.WaitBeforeDischarge", (itemreuse - (CurrentRegion.Time - itemdelay)) / 1000, useItem.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                                    return;
                                }
                                else
                                {
                                    if (type == 1) //use1
                                    {
                                        if (useItem.SpellID == 0)
                                            return;

                                        UseItemCharge(useItem, type);
                                    }
                                    else if (type == 2) //use2
                                    {
                                        if (useItem.SpellID1 == 0)
                                            return;

                                        UseItemCharge(useItem, type);
                                    }
                                }
                            }
                        }
                    }
                }
                // notify event handlers about used slot
                Notify(GamePlayerEvent.UseSlot, this, new UseSlotEventArgs(slot, type, useItem));
            }
        }

        /// <summary>
        /// Player is using a saddle bag to open up slots on a mount
        /// </summary>
        /// <param name="useItem"></param>
        protected virtual void UseSaddleBag(InventoryItem useItem)
        {
            eHorseSaddleBag bag = eHorseSaddleBag.None;

            switch ((eInventorySlot)useItem.Item_Type)
            {
                case eInventorySlot.LeftFrontSaddleBag:
                    if (ChampionLevel >= 2)
                    {
                        bag = eHorseSaddleBag.LeftFront;
                    }
                    else
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SaddleBag.CL2"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    }
                    break;
                case eInventorySlot.RightFrontSaddleBag:
                    if (ChampionLevel >= 3)
                    {
                        bag = eHorseSaddleBag.RightFront;
                    }
                    else
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SaddleBag.CL3"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    }
                    break;
                case eInventorySlot.LeftRearSaddleBag:
                    if (ChampionLevel >= 4)
                    {
                        bag = eHorseSaddleBag.LeftRear;
                    }
                    else
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SaddleBag.CL4"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    }
                    break;
                case eInventorySlot.RightRearSaddleBag:
                    if (ChampionLevel >= 5)
                    {
                        bag = eHorseSaddleBag.RightRear;
                    }
                    else
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SaddleBag.CL5"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    }
                    break;
            }

            if (bag != eHorseSaddleBag.None)
            {
                if ((ActiveSaddleBags & (byte)bag) == 0)
                {
                    if (Inventory.RemoveItem(useItem))
                    {
                        InventoryLogging.LogInventoryAction(this, "", "(HorseSaddleBag)", eInventoryActionType.Other, useItem, useItem.Count);
                        ActiveSaddleBags |= (byte)bag;
                        Out.SendSetControlledHorse(this);
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SaddleBag.Activated"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        SaveIntoDatabase();
                    }
                    else
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SaddleBag.ActivateError"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    }
                }
                else
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SaddleBag.AlreadyActivated"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                }
            }
        }

        private const int NUM_SLOTS_PER_SADDLEBAG = 4;

        /// <summary>
        /// Can player use this horse inventory slot
        /// </summary>
        /// <param name="slot"></param>
        /// <returns></returns>
        public virtual bool CanUseHorseInventorySlot(int slot)
        {
            if (Inventory.GetItem(eInventorySlot.Horse) == null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsOnHorse.NeedHorseEquipped"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (IsOnHorse == false)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsOnHorse.NeedToBeOnHorse"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (ChampionLevel == 0)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Champion.NeedToBeChampion"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (slot < (int)eInventorySlot.FirstBagHorse || slot > (int)eInventorySlot.LastBagHorse || (ChampionLevel >= 5 && ActiveSaddleBags == (byte)eHorseSaddleBag.All))
            {
                return true;
            }

            try
            {
                eHorseSaddleBag saddleBagRequired = (eHorseSaddleBag)Enum.GetValues(typeof(eHorseSaddleBag)).GetValue(((slot / NUM_SLOTS_PER_SADDLEBAG) - 19))!; // 1, 2, 3, or 4

                // ChatUtil.SendDebugMessage(this, string.Format("Check slot {0} if between {1} and {2}.  CL is {3}, ActiveSaddleBags is {4}, Required Bag is {5}", slot, (int)eInventorySlot.FirstBagHorse, (int)eInventorySlot.LastBagHorse, ChampionLevel, ActiveSaddleBags, saddleBagRequired));

                if ((ActiveSaddleBags & (byte)saddleBagRequired) > 0)
                {
                    if (ChampionLevel >= 2 && slot < (int)eInventorySlot.FirstBagHorse + NUM_SLOTS_PER_SADDLEBAG)
                    {
                        return true;
                    }
                    else if (ChampionLevel >= 3 && slot < (int)eInventorySlot.FirstBagHorse + NUM_SLOTS_PER_SADDLEBAG * 2)
                    {
                        return true;
                    }
                    else if (ChampionLevel >= 4 && slot < (int)eInventorySlot.FirstBagHorse + NUM_SLOTS_PER_SADDLEBAG * 3)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ChatUtil.SendDebugMessage(this, "CanSeeInventory: " + ex.Message);
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CannotUseInventory"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return false;
        }


        /// <summary>
        /// Use a charged ability on an item
        /// </summary>
        /// <param name="useItem"></param>
        /// <param name="type">1 == use1, 2 == use2</param>
        protected virtual void UseItemCharge(InventoryItem useItem, int type)
        {
            int requiredLevel = useItem.Template.LevelRequirement > 0 ? useItem.Template.LevelRequirement : Math.Min(MaxLevel, useItem.Level);

            if (requiredLevel > Level)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.NotPowerfulEnough"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            SpellLine chargeEffectLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);
            Spell spell = null;

            if (type == 1)
            {
                spell = SkillBase.FindSpell(useItem.SpellID, chargeEffectLine);
            }
            else
            {
                spell = SkillBase.FindSpell(useItem.SpellID1, chargeEffectLine);
            }

            if (spell != null)
            {
                ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(this, spell, chargeEffectLine);
                if (spellHandler != null)
                {
                    if (IsOnHorse && !spellHandler.HasPositiveEffect && spell.Target.ToLower() != "self")
                        IsOnHorse = false;

                    Stealth(false);

                    if (spellHandler.CastSpell())
                    {
                        bool castOk = spellHandler.StartReuseTimer;

                        if (spell.SubSpellID > 0)
                        {
                            Spell subspell = SkillBase.GetSpellByID(spell.SubSpellID);
                            if (subspell != null)
                            {
                                ISpellHandler subSpellHandler = ScriptMgr.CreateSpellHandler(this, subspell, chargeEffectLine);
                                if (subSpellHandler != null)
                                {
                                    subSpellHandler.CastSpell();
                                }
                            }
                        }
                        if (useItem.MaxCharges > 0 && !IsInvulnerableToAttack)
                        {
                            useItem.Charges--;
                        }

                        if (castOk)
                        {
                            TempProperties.setProperty(LAST_CHARGED_ITEM_USE_TICK + useItem.Id_nb, CurrentRegion.Time);
                            //TempProperties.setProperty(ITEM_USE_DELAY, (long)(60000 * 2));
                            TempProperties.setProperty("ITEMREUSEDELAY" + useItem.Id_nb, CurrentRegion.Time);
                        }
                    }
                }
                else
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.ChargeEffectNotImplemented", spell.ID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
            else
            {
                if (type == 1)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.ChargeEffectNotFound", useItem.SpellID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.ChargeEffectNotFound", useItem.SpellID1), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
        }


        /// <summary>
        /// Use a magical item's spell.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="type"></param>
        protected virtual bool UseMagicalItem(InventoryItem item, int type)
        {
            if (item == null)
                return false;

            int cooldown = item.CanUseAgainIn;
            if (cooldown > 0 && Client.Account.PrivLevel == (uint)ePrivLevel.Player)
            {
                int minutes = cooldown / 60;
                int seconds = cooldown % 60;
                string timeMessage = (minutes <= 0)
                         ? LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.Cooldown.Seconds", seconds)
                         : LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.Cooldown.MinutesAndSeconds", minutes, seconds);

                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.Cooldown", timeMessage), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                return false;
            }

            //Check if Zone allows to use Magical Item
            if (!this.CurrentZone.AllowMagicalItem)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.NoMagicItemZone"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            //Eden
            if (IsMezzed || (IsStunned && !(Steed != null && Steed.Name == "Forceful Zephyr")) || !IsAlive)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CannotUseInState"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (m_runningSpellHandler != null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CastSpell.AlreadyCastingSpell"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            SpellLine itemSpellLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);

            if (itemSpellLine == null)
                return false;

            if (type == 1 || type == 0)
            {
                Spell spell = SkillBase.FindSpell(item.SpellID, itemSpellLine);

                if (spell != null)
                {
                    int requiredLevel = item.Template.LevelRequirement > 0 ? item.Template.LevelRequirement : Math.Min(MaxLevel, item.Level);

                    if (requiredLevel > Level)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.NotPowerfulEnough"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }

                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.Used", item.GetName(0, false)), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);

                    ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(this, spell, itemSpellLine);
                    if (spellHandler == null)
                        return false;

                    if (IsOnHorse && !spellHandler.HasPositiveEffect)
                        IsOnHorse = false;

                    Stealth(false);

                    if (spellHandler.CheckBeginCast(TargetObject as GameLiving))
                    {
                        TempProperties.setProperty(LAST_USED_ITEM_SPELL, item);
                        m_runningSpellHandler = spellHandler;
                        m_runningSpellHandler.CastingCompleteEvent += new CastingCompleteCallback(OnAfterSpellCastSequence);
                        spellHandler.CastSpell(item);
                        return true;
                    }
                }
            }
            return false;
        }

        public bool UseChargeMagicalItem(InventoryItem useItem, int type)
        {
            SpellLine potionEffectLine = SkillBase.GetSpellLine(GlobalSpellsLines.Potions_Effects);
            if (useItem.Item_Type == 41)
            {
                potionEffectLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);
            }

            Spell spell = SkillBase.FindSpell(useItem.SpellID, potionEffectLine);
            if (spell == null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.PotionSpellNotFound", useItem.SpellID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            
            var handler = new ItemchargeXRacesHandler();
            double raceMultiplier = handler.GetRaceMultiplier(this, useItem.Id_nb);

            var (alternativeSpellID, effectiveMultiplier) = handler.GetAlternativeSpellIDAndMultiplier(raceMultiplier, spell.ID);
            if (alternativeSpellID != spell.ID)
            {
                spell = SkillBase.FindSpell(alternativeSpellID, potionEffectLine);
            }

            spell = (Spell)spell.Clone();
            spell.Value *= effectiveMultiplier;
            spell.Damage *= effectiveMultiplier;

            if (handler.IsDurationMultiplied(useItem.Id_nb))
            {
                spell.Duration = (int)(spell.Duration * effectiveMultiplier);
            }

            bool isParchment = useItem.Id_nb.Contains("PARCH", StringComparison.InvariantCultureIgnoreCase);
            bool isPotion = !isParchment;
            // For potions most can be used by any player level except a few higher level ones.
            // So for the case of potions we will only restrict the level of usage if LevelRequirement is >0 for the item
            long nextPotionAvailTime = isPotion ? TempProperties.getProperty<long>(NEXT_POTION_AVAIL_TIME + "_Type" + (spell.SharedTimerGroup)) : TempProperties.getProperty<long>(NEXT_PARCH_AVAIL_TIME + "_Type" + (spell.SharedTimerGroup));
            if (Client.Account.PrivLevel == 1 && nextPotionAvailTime > CurrentRegion.Time)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.MustWaitBeforeUse", (nextPotionAvailTime - CurrentRegion.Time) / 1000), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            
            if (potionEffectLine == null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.PotionLineNotFound"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            
            int requiredLevel = useItem.Template.LevelRequirement > 0 ? useItem.Template.LevelRequirement : Math.Min(MaxLevel, useItem.Level);
            if (requiredLevel > Level)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.NotEnouthPower"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            //Eden
            if ((IsStunned && !(Steed != null && Steed.Name == "Forceful Zephyr")) || IsMezzed || !IsAlive)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantUseState", useItem.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (spell.CastTime > 0 && IsCasting)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.CantUseCast", useItem.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            SpellHandler spellHandler = ScriptMgr.CreateSpellHandler(this, spell, potionEffectLine) as SpellHandler;
            if (spellHandler == null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UseSlot.PotionNotImplemented", spell.ID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            GameLiving target = TargetObject as GameLiving;
            if (useItem.Item_Type == (int)eInventorySlot.FirstBackpack && isPotion)
            {
                Stealth(false);
                Emote(eEmote.Drink);

                if (spell.CastTime > 0)
                    TempProperties.setProperty(NEXT_SPELL_AVAIL_TIME_BECAUSE_USE_POTION, 6 * 1000 + CurrentRegion.Time);
            }
            // Tobz: make sure we have the appropriate target for our charge spell,
            // otherwise don't waste a charge.
            // Mishura: this is important for e.g. StyleHandler which will succeed at CastSpell and then fail.
            if (spell.Target.ToLower() == "enemy")
            {
                // If target is null, let the spell send an error
                if (target != null && !GameServer.ServerRules.IsAllowedToAttack(this, target, false))
                {
                    return false;
                }
            }

            bool test = false;
            if (isParchment)
                test = CastSpell(spell, potionEffectLine, useItem);
            else
            {
                test = spellHandler!.StartSpell(target, useItem);
                if (test && spellHandler.Status == SpellHandler.eStatus.Success)
                {
                    FinalizeItemCast(spellHandler);
                }
            }
            return test;
        }

        /// <summary>
        /// Apply poison to weapon
        /// </summary>
        /// <param name="poisonPotion"></param>
        /// <param name="toItem"></param>
        /// <returns>true if applied</returns>
        public bool ApplyPoison(InventoryItem poisonPotion, InventoryItem toItem)
        {
            if (poisonPotion == null || toItem == null) return false;
            int envenomSpec = GetModifiedSpecLevel(Specs.Envenom);
            if (envenomSpec < 1)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ApplyPoison.CantUsePoisons"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (!GlobalConstants.IsWeapon(toItem.Object_Type))
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ApplyPoison.PoisonsAppliedWeapons"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (!HasAbilityToUseItem(toItem.Template))
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ApplyPoison.CantPoisonWeapon"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (envenomSpec < poisonPotion.Level)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ApplyPoison.CantUsePoison"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (InCombat)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ApplyPoison.CantApplyRecentCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (toItem.PoisonSpellID != 0)
            {
                bool canApply = false;
                SpellLine poisonLine = SkillBase.GetSpellLine(GlobalSpellsLines.Mundane_Poisons);
                if (poisonLine != null)
                {
                    List<Spell> spells = SkillBase.GetSpellList(poisonLine.KeyName);
                    foreach (Spell spl in spells)
                    {
                        if (spl.ID == toItem.PoisonSpellID)
                        {
                            canApply = true;
                            break;
                        }
                    }
                }
                if (canApply == false)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ApplyPoison.CannotPoison", toItem.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            // Apply poison effect to weapon
            if (poisonPotion.PoisonSpellID != 0)
            {
                toItem.PoisonCharges = poisonPotion.PoisonCharges;
                toItem.PoisonMaxCharges = poisonPotion.PoisonMaxCharges;
                toItem.PoisonSpellID = poisonPotion.PoisonSpellID;
            }
            else
            {
                toItem.PoisonCharges = poisonPotion.Template.PoisonCharges;
                toItem.PoisonMaxCharges = poisonPotion.Template.PoisonMaxCharges;
                toItem.PoisonSpellID = poisonPotion.Template.PoisonSpellID;
            }
            Inventory.RemoveCountFromStack(poisonPotion, 1);
            InventoryLogging.LogInventoryAction(this, "", "(poison)", eInventoryActionType.Other, poisonPotion.Template, 1);
            Out.SendMessage(string.Format("You apply {0} to {1}.", poisonPotion.GetName(0, false), toItem.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        #endregion

        #region Send/Say/Yell/Whisper/Messages

        public bool IsIgnoring(GameLiving source)
        {
            if (source is GamePlayer)
            {
                var sender = source as GamePlayer;
                foreach (string Name in IgnoreList)
                {
                    if (sender!.Name == Name && sender.Client.Account.PrivLevel < 2)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Delegate to be called when this player receives a text
        /// by someone sending something
        /// </summary>
        public delegate bool SendReceiveHandler(GamePlayer source, GamePlayer receiver, string str);

        /// <summary>
        /// Delegate to be called when this player is about to send a text
        /// </summary>
        public delegate bool SendHandler(GamePlayer source, GamePlayer receiver, string str);

        /// <summary>
        /// Event that is fired when the Player is about to send a text
        /// </summary>
        public event SendHandler OnSend;

        /// <summary>
        /// Event that is fired when the Player receives a Send text
        /// </summary>
        public event SendReceiveHandler OnSendReceive;

        /// <summary>
        /// Clears all send event handlers
        /// </summary>
        public void ClearOnSend()
        {
            OnSend = null;
        }

        /// <summary>
        /// Clears all OnSendReceive event handlers
        /// </summary>
        public void ClearOnSendReceive()
        {
            OnSendReceive = null;
        }

        /// <summary>
        /// This function is called when the Player receives a sent text
        /// </summary>
        /// <param name="source">GamePlayer that was sending</param>
        /// <param name="str">string that was sent</param>
        /// <returns>true if the string was received successfully, false if it was not received</returns>
        public virtual bool PrivateMessageReceive(GamePlayer source, string str)
        {
            var onSendReceive = OnSendReceive;
            if (onSendReceive != null && !onSendReceive(source, this, str))
                return false;

            if (IsIgnoring(source))
                return true;

            eChatType type = eChatType.CT_Send;
            if (source.Client.Account.PrivLevel > 1)
                type = eChatType.CT_Staff;

            if (GameServer.ServerRules.IsAllowedToUnderstand(source, this))
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SendReceive.Sends", GetPersonalizedName(source), str), type,
                                eChatLoc.CL_ChatWindow);
            else
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SendReceive.FalseLanguage", GetPersonalizedName(source)),
                                eChatType.CT_Send, eChatLoc.CL_ChatWindow);
                return true;
            }

            var afkmessage = TempProperties.getProperty<string>(AFK_MESSAGE);
            if (afkmessage != null)
            {
                if (afkmessage == "")
                {
                    source.Out.SendMessage(LanguageMgr.GetTranslation(source.Client.Account.Language, "GameObjects.GamePlayer.SendReceive.Afk", source.GetPersonalizedName(this)),
                                           eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    source.Out.SendMessage(
                        LanguageMgr.GetTranslation(source.Client.Account.Language, "GameObjects.GamePlayer.SendReceive.AfkMessage", source.GetPersonalizedName(this), afkmessage), eChatType.CT_Say,
                        eChatLoc.CL_ChatWindow);
                }
            }

            return true;
        }

        /// <summary>
        /// Sends a text to a target
        /// </summary>
        /// <param name="target">The target of the send</param>
        /// <param name="str">string to send (without any "xxx sends:" in front!!!)</param>
        /// <returns>true if text was sent successfully</returns>
        public virtual bool SendPrivateMessage(GamePlayer target, string str)
        {
            if (target == null || str == null)
                return false;

            SendHandler onSend = OnSend;
            if (onSend != null && !onSend(this, target, str))
                return false;

            if (!target.PrivateMessageReceive(this, str))
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Send.target.DontUnderstandYou", GetPersonalizedName(target)),
                                eChatType.CT_Send, eChatLoc.CL_ChatWindow);
                return false;
            }

            if (Client.Account.PrivLevel == 1 && target.Client.Account.PrivLevel > 1 && target.IsAnonymous)
                return true;

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Send.YouSendTo", str, GetPersonalizedName(target)), eChatType.CT_Send,
                            eChatLoc.CL_ChatWindow);

            return true;
        }

        /// <summary>
        /// This function is called when the Player receives a Say text!
        /// </summary>
        /// <param name="source">The source living saying something</param>
        /// <param name="str">the text that was said</param>
        /// <returns>true if received successfully</returns>
        public override bool SayReceive(GameLiving source, string str)
        {
            if (!base.SayReceive(source, str))
                return false;
            if (IsIgnoring(source))
                return true;
            if (GameServer.ServerRules.IsAllowedToUnderstand(source, this) || Properties.ENABLE_DEBUG)
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SayReceive.Says", GetPersonalizedName(source), str),
                                eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            else
                Out.SendMessage(
                    LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SayReceive.FalseLanguage", GetPersonalizedName(source)),
                    eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            return true;
        }

        /// <summary>
        /// Call this function to make the player say something
        /// </summary>
        /// <param name="str">string to say</param>
        /// <returns>true if said successfully</returns>
        public override bool Say(string str)
        {
            if (!GameServer.ServerRules.IsAllowedToSpeak(this, "talk"))
                return false;
            if (!base.Say(str))
                return false;
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Say.YouSay", str), eChatType.CT_Say,
                            eChatLoc.CL_ChatWindow);
            return true;
        }

        /// <summary>
        /// This function is called when the player hears a yell
        /// </summary>
        /// <param name="source">the source living yelling</param>
        /// <param name="str">string that was yelled</param>
        /// <returns>true if received successfully</returns>
        public override bool YellReceive(GameLiving source, string str)
        {
            if (!base.YellReceive(source, str))
                return false;
            if (IsIgnoring(source))
                return true;
            if (GameServer.ServerRules.IsAllowedToUnderstand(source, this))
                Out.SendMessage(GetPersonalizedName(source) + " yells, \"" + str + "\"", eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            else
                Out.SendMessage(GetPersonalizedName(source) + " yells something in a language you don't understand.", eChatType.CT_Say,
                                eChatLoc.CL_ChatWindow);
            return true;
        }

        /// <summary>
        /// Call this function to make the player yell something
        /// </summary>
        /// <param name="str">string to yell</param>
        /// <returns>true if yelled successfully</returns>
        public override bool Yell(string str)
        {
            if (!GameServer.ServerRules.IsAllowedToSpeak(this, "yell"))
                return false;
            if (!base.Yell(str))
                return false;
            Out.SendMessage("You yell, \"" + str + "\"", eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            return true;
        }

        /// <summary>
        /// This function is called when the player hears a whisper
        /// from some living
        /// </summary>
        /// <param name="source">Source that was living</param>
        /// <param name="str">string that was whispered</param>
        /// <returns>true if whisper was received successfully</returns>
        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str))
                return false;
            if (IsIgnoring(source))
                return true;
            if (GameServer.ServerRules.IsAllowedToUnderstand(source, this))
                Out.SendMessage(GetPersonalizedName(source) + " whispers to you, \"" + str + "\"", eChatType.CT_Say,
                                eChatLoc.CL_ChatWindow);
            else
                Out.SendMessage(GetPersonalizedName(source) + " whispers something in a language you don't understand.",
                                eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            return true;
        }

        /// <summary>
        /// Call this function to make the player whisper to someone
        /// </summary>
        /// <param name="target">GameLiving to whisper to</param>
        /// <param name="str">string to whisper</param>
        /// <returns>true if whispered successfully</returns>
        public override bool Whisper(GameObject target, string str)
        {
            if (target == null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Whisper.SelectTarget"), eChatType.CT_System,
                                eChatLoc.CL_ChatWindow);
                return false;
            }
            if (!GameServer.ServerRules.IsAllowedToSpeak(this, "whisper"))
                return false;
            if (!base.Whisper(target, str))
                return false;
            if (target is GamePlayer)
                Out.SendMessage("You whisper, \"" + str + "\" to " + GetPersonalizedName(target), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            return true;
        }

        /// <summary>
        /// A message to this player from some piece of code (message to ourself)
        /// </summary>
        /// <param name="message"></param>
        /// <param name="chatType"></param>
        public override void MessageToSelf(string message, eChatType chatType)
        {
            Out.SendMessage(message, chatType, eChatLoc.CL_SystemWindow);
        }

        /// <summary>
        /// A message from something we control, usually a pet
        /// </summary>
        /// <param name="message"></param>
        /// <param name="chatType"></param>
        public override void MessageFromControlled(string message, eChatType chatType)
        {
            MessageToSelf(message, chatType);
        }

        /// <summary>
        /// A general message from the area intended for this player.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="message"></param>
        /// <param name="chatType"></param>
        /// <param name="chatLocation"></param>
        public void MessageFromArea(GameObject source, string message, eChatType chatType, eChatLoc chatLocation)
        {
            Out.SendMessage(message, chatType, chatLocation);
        }

        #endregion

        #region Steed

        /// <summary>
        /// Holds the GameLiving that is the steed of this player as weakreference
        /// </summary>
        protected WeakReference m_steed;
        /// <summary>
        /// Holds the Steed of this player
        /// </summary>
        public GameNPC Steed
        {
            get { return m_steed.Target as GameNPC; }
            set { m_steed.Target = value; }
        }

        /// <summary>
        /// Delegate callback to be called when the player
        /// tries to mount a steed
        /// </summary>
        public delegate bool MountSteedHandler(GameLiving rider, GameNPC steed, bool forced);

        /// <summary>
        /// Event will be fired whenever the player tries to
        /// mount a steed
        /// </summary>
        public event MountSteedHandler OnMountSteed;
        /// <summary>
        /// Clears all MountSteed handlers
        /// </summary>
        public void ClearMountSteedHandlers()
        {
            OnMountSteed = null;
        }

        /// <summary>
        /// Mounts the player onto a steed
        /// </summary>
        /// <param name="steed">the steed to mount</param>
        /// <param name="forced">true if the mounting can not be prevented by handlers</param>
        /// <returns>true if mounted successfully or false if not</returns>
        public virtual bool MountSteed(GameNPC steed, bool forced)
        {
            // Sanity 'coherence' checks
            if (Steed != null)
                if (!DismountSteed(forced))
                    return false;

            if (this is GamePlayer { IsOnHorse: true } asPlayer)
                asPlayer.IsOnHorse = false;

            if (!steed.RiderMount(this, forced) && !forced)
                return false;

            if (OnMountSteed != null && !OnMountSteed(this, steed, forced) && !forced)
                return false;

            // Standard checks, as specified in rules
            if (GameServer.ServerRules.ReasonForDisallowMounting(this) != string.Empty && !forced)
                return false;

            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null) continue;
                player.Out.SendRiding(this, steed, false);
            }

            return true;
        }

        /// <summary>
        /// Delegate callback to be called whenever the player tries
        /// to dismount from a steed
        /// </summary>
        public delegate bool DismountSteedHandler(GameLiving rider, GameLiving steed, bool forced);

        /// <summary>
        /// Event will be fired whenever the player tries to dismount
        /// from a steed
        /// </summary>
        public event DismountSteedHandler OnDismountSteed;
        /// <summary>
        /// Clears all DismountSteed handlers
        /// </summary>
        public void ClearDismountSteedHandlers()
        {
            OnDismountSteed = null;
        }

        /// <summary>
        /// Dismounts the player from it's steed.
        /// </summary>
        /// <param name="forced">true if the dismounting should not be prevented by handlers</param>
        /// <returns>true if the dismount was successful, false if not</returns>
        public virtual bool DismountSteed(bool forced)
        {
            if (Steed == null)
                return false;
            if (Steed.Name == "Forceful Zephyr" && !forced) return false;
            if (OnDismountSteed != null && !OnDismountSteed(this, Steed, forced) && !forced)
                return false;
            GameObject steed = Steed;
            if (!Steed.RiderDismount(forced, this) && !forced)
                return false;

            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null) continue;
                player.Out.SendRiding(this, steed, true);
            }
            return true;
        }

        /// <summary>
        /// Returns if the player is riding or not
        /// </summary>
        /// <returns>true if on a steed, false if not</returns>
        public virtual bool IsRiding
        {
            get { return Steed != null; }
        }

        public void SwitchSeat(int slot)
        {
            if (Steed == null)
                return;

            if (Steed.Riders[slot] != null)
                return;

            if (this is GamePlayer asPlayer)
                asPlayer.Out.SendMessage("You switch to seat " + slot + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);

            GameNPC steed = Steed;
            steed.RiderDismount(true, this);
            steed.RiderMount(this, true, slot);
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null) continue;
                player.Out.SendRiding(this, steed, false);
            }
        }

        #endregion

        #region Add/Move/Remove

        /// <summary>
        /// Called to create an player in the world and send the other
        /// players around this player an update
        /// </summary>
        /// <returns>true if created, false if creation failed</returns>
        public override bool AddToWorld()
        {
            if (!base.AddToWorld())
            {
                log.Error("Failed to add player to world: " + Name);
                return false;
            }

            if (RvrManager.WinnerRealm == Realm)
            {
                BaseBuffBonusCategory[eProperty.MythicalCoin] += 5;
                BaseBuffBonusCategory[eProperty.XpPoints] += 10;
                BaseBuffBonusCategory[eProperty.RealmPoints] += 5;
            }

            IsJumping = false;
            m_invulnerabilityTick = 0;
            m_pve_invulnerabilityTick = 0;
            m_healthRegenerationTimer = new RegionTimer(this);
            m_powerRegenerationTimer = new RegionTimer(this);
            m_enduRegenerationTimer = new RegionTimer(this);
            m_healthRegenerationTimer.Callback = new RegionTimerCallback(HealthRegenerationTimerCallback);
            m_powerRegenerationTimer.Callback = new RegionTimerCallback(PowerRegenerationTimerCallback);
            m_enduRegenerationTimer.Callback = new RegionTimerCallback(EnduranceRegenerationTimerCallback);
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null) continue;
                if (player != this)
                    player.Out.SendPlayerCreate(this);
            }

            UpdateEquipmentAppearance();

            // display message
            if (SpecPointsOk == false)
            {
                log.Debug(Name + " is told spec points are incorrect!");
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.AddToWorld.SpecsPointsIncorrect"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                SpecPointsOk = true;
            }

            //Dinberg, instance change.
            if (CurrentRegion is BaseInstance)
                ((BaseInstance)CurrentRegion).OnPlayerEnterInstance(this);

            RefreshItemBonuses();

            return true;
        }

        /// <summary>
        /// Called to remove the item from the world. Also removes the
        /// player visibly from all other players around this one
        /// </summary>
        /// <returns>true if removed, false if removing failed</returns>
        public override bool RemoveFromWorld()
        {
            if (CharacterClass.RemoveFromWorld() == false)
            {
                return false;
            }

            if (ObjectState == eObjectState.Active)
            {
                DismountSteed(true);
                if (CurrentZone == null)
                {
                    if (this is GamePlayer && this.Client.Account.PrivLevel < 3 && !(this as GamePlayer).TempProperties.getProperty("isbeingbanned", false))
                    {
                        GamePlayer player = this as GamePlayer;
                        player.TempProperties.setProperty("isbeingbanned", true);
                        player.MoveToBind();
                    }
                }
                else foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (player == null) continue;
                        if (player != this)
                            player.Out.SendObjectRemove(this);
                    }
            }
            if (!base.RemoveFromWorld()) return false;

            IsJumping = false;

            if (m_invulnerabilityTimer != null)
            {
                m_invulnerabilityTimer.Stop();
                m_invulnerabilityTimer = null;
            }
            Diving(waterBreath.Normal);
            if (IsOnHorse)
                IsOnHorse = false;

            //Dinberg, instance change.
            if (CurrentRegion is BaseInstance)
                ((BaseInstance)CurrentRegion).OnPlayerLeaveInstance(this);

            return true;
        }

        /// <summary>
        /// Marks this player as deleted
        /// </summary>
        public override void Delete()
        {
            if (ObjectState == eObjectState.Deleted)
                return;

            // do some Cleanup
            CleanupOnDisconnect();

            if (Group != null)
            {
                Group.RemoveMember(this);
            }
            BattleGroup mybattlegroup = (BattleGroup)this.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null);
            if (mybattlegroup != null)
            {
                mybattlegroup.RemoveBattlePlayer(this);
            }
            if (m_guild != null)
            {
                m_guild.RemoveOnlineMember(this);
            }
            GroupMgr.RemovePlayerLooking(this);
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("({0}) player.Delete()", Name);
            }
            if (TemporaryConsignmentMerchant != null)
                TemporaryConsignmentMerchant.Delete();
            base.Delete();
        }

        /// <summary>
        /// The property to save debug mode on region change
        /// </summary>
        public const string DEBUG_MODE_PROPERTY = "Player.DebugMode";

        /// <summary>
        /// This function moves a player to a specific region and
        /// specific coordinates.
        /// </summary>
        /// <param name="regionID">RegionID to move to</param>
        /// <param name="x">X target coordinate</param>
        /// <param name="y">Y target coordinate</param>
        /// <param name="z">Z target coordinate (0 to put player on floor)</param>
        /// <param name="heading">Target heading</param>
        /// <returns>true if move succeeded, false if failed</returns>
        public override bool MoveTo(Position position)
        {
            //if we are jumping somewhere away from our house not using house.Exit
            //we need to make the server know we have left the house
            if ((CurrentHouse != null || InHouse) && CurrentHouse!.RegionID != position.RegionID)
            {
                InHouse = false;
                CurrentHouse = null;
            }
            //if we send a jump, we get off the horse
            if (IsOnHorse)
                IsOnHorse = false;
            //Get the destination region based on the ID
            Region rgn = WorldMgr.GetRegion(position.RegionID);
            //If the region doesn't exist, return false or if they aren't allowed to zone here
            if (rgn == null || !GameServer.ServerRules.IsAllowedToZone(this, rgn))
                return false;
            //If the x,y inside this region doesn't point to a zone
            //return false
            if (rgn.GetZone(position.Coordinate) == null)
                return false;

            Diving(waterBreath.Normal);

            if (SiegeWeapon != null)
                SiegeWeapon.ReleaseControl();
            
            var positionBeforePort = Position;

            if (position.RegionID != positionBeforePort.RegionID)
            {
                GameEventMgr.Notify(GamePlayerEvent.RegionChanging, this);
                if (!RemoveFromWorld())
                    return false;
                
                //notify event
                if (CurrentRegion != null)
                {
                    CurrentRegion.Notify(RegionEvent.PlayerLeave, CurrentRegion, new RegionPlayerEventArgs(this));
                }

                CancelAllConcentrationEffects(true);
                if (ControlledBrain != null)
                    CommandNpcRelease();
            }
            else
            {
                //Just remove the player visible, but leave his OID intact!
                //If player doesn't change region
                if (Steed != null)
                    DismountSteed(true);

                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player == null) continue;
                    if (player != this)
                    {
                        player.Out.SendObjectRemove(this);
                    }
                }

                IsJumping = true;
            }
            bool hasPetToMove = false;

            if (ControlledBrain != null && ControlledBrain.WalkState != eWalkState.Stay)
            {
                if (CharacterClass.ID != (int)eCharacterClass.Theurgist && CharacterClass.ID != (int)eCharacterClass.Animist)
                {
                    hasPetToMove = true;
                }
            }
            //Set the new destination
            //Current Speed = 0 when moved ... else X,Y,Z continue to be modified
            CurrentSpeed = 0;
            Position = position;

            //Remove the last update tick property, to prevent speedhack messages during zoning and teleporting!
            TempProperties.removeProperty(PlayerPositionUpdateHandler.LASTMOVEMENTTICK);
            //If the destination is in another region
            if (position.RegionID != positionBeforePort.RegionID)
            {
                //Send the region update packet, the rest will be handled
                //by the packethandlers
                Out.SendRegionChanged();
            }
            else
            {
                //Add the player to the new coordinates
                Out.SendPlayerJump(false);

                // are we jumping far enough to force a complete refresh?
                if (Coordinate.DistanceTo(positionBeforePort) > WorldMgr.REFRESH_DISTANCE)
                {
                    RefreshWorld();
                }
                else
                {
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (player != null && player != this)
                        {
                            if (IsStealthed == false || player.CanDetect(this))
                            {
                                player.Out.SendPlayerCreate(this);
                            }
                        }
                    }
                }

                UpdateEquipmentAppearance();

                if (this.IsUnderwater)
                    this.IsDiving = true;

                if (hasPetToMove)
                {
                    if (ControlledBody is GameNPC petBody)
                    {
                        var destination = Position.TurnedAround() + Vector.Create(Orientation, length: 64, z: 10);
                        petBody.MoveWithoutRemovingFromWorld(destination, false);

                        if (petBody.ControlledNpcList != null)
                            foreach (IControlledBrain icb in petBody.ControlledNpcList)
                                if (icb != null && icb.Body is GameNPC petBody2
                                    && petBody2.Coordinate.DistanceTo(positionBeforePort) < 500)
                                    petBody2.MoveWithoutRemovingFromWorld(destination, false);
                    }
                }
                shadowNPC.MoveToPlayer();
            }

            return true;
        }
        
        /// <summary>
        /// This function is called from the ObjectInteractRequestHandler
        /// </summary>
        /// <param name="obj">Object to interact with</param>
        /// <returns>false if interaction is prevented</returns>
        public void InteractWith(GameObject obj)
        {
            if (obj.Interact(this))
            {
                obj.Notify(GameObjectEvent.Interact, obj, new InteractEventArgs(this));
                this.Notify(GameObjectEvent.InteractWith, this, new InteractWithEventArgs(obj));
            }
            else
            {
                obj.Notify(GameObjectEvent.InteractFailed, obj, new InteractEventArgs(this));
            }
        }

        /// <summary>
        /// Refresh all objects around the player
        /// </summary>
        public virtual void RefreshWorld()
        {
            foreach (GameNPC npc in GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE * 2))
            {
                Out.SendNPCCreate(npc);
                if (npc.Inventory != null)
                    Out.SendLivingEquipmentUpdate(npc);
            }

            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player != null && player != this)
                {
                    if (IsStealthed == false || player.CanDetect(this))
                    {
                        player.Out.SendPlayerCreate(this);
                    }

                    if (player.IsStealthed == false || CanDetect(player))
                    {
                        Out.SendPlayerCreate(player);
                        Out.SendLivingEquipmentUpdate(player);
                    }
                }
            }
        }

        /// <summary>
        /// Refresh every mob in visibility susceptible of giving us a quest
        /// </summary>
        public void RefreshQuestNPCs()
        {
            foreach (GameNPC mob in GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE).Cast<GameNPC>())
            {
                mob.RefreshEffects(this);
            }
        }

        //Eden - Move to bind, and check if the loc is allowed
        public virtual bool MoveToBind()
        {
            Region rgn = WorldMgr.GetRegion(BindPosition.RegionID);
            if (rgn == null || rgn.GetZone(BindPosition.Coordinate) == null)
            {
                if (log.IsErrorEnabled)
                    log.Error("Player: " + Name + " unknown bind point : (R/X/Y) " + BindPosition.RegionID + "/" + BindPosition.X + "/" + BindPosition.Y);
                //Kick the player, avoid server freeze
                Client.Out.SendPlayerQuit(true);
                Quit(true);
                SaveIntoDatabase();
                //now ban him
                if (ServerProperties.Properties.BAN_HACKERS)
                {
                    DBBannedAccount b = new DBBannedAccount();
                    b.Author = "SERVER";
                    b.Ip = Client.TcpEndpointAddress;
                    b.Account = Client.Account.Name;
                    b.DateBan = DateTime.Now;
                    b.Type = "B";
                    b.Reason = "X/Y/RegionID : " + Position.X + "/" + Position.Y + "/" + Position.RegionID;
                    GameServer.Database.AddObject(b);
                    GameServer.Database.SaveObject(b);
                    string message = "Unknown bind point, your account is banned, contact a GM.";
                    Client.Out.SendMessage(message, eChatType.CT_Help, eChatLoc.CL_SystemWindow);
                    Client.Out.SendMessage(message, eChatType.CT_Help, eChatLoc.CL_ChatWindow);
                }
                return false;
            }

            if (GameServer.ServerRules.IsAllowedToMoveToBind(this))
                return MoveTo(BindPosition);

            return false;
        }

        #endregion

        #region Group/Friendlist/guild

        private Guild m_guild;
        private DBRank m_guildRank;

        /// <summary>
        /// Gets or sets the player's guild
        /// </summary>
        public Guild Guild
        {
            get { return m_guild; }
            set
            {
                if (value == null)
                {
                    // remove this player from the online list of their current guild
                    m_guild.RemoveOnlineMember(this);
                }

                m_guild = value;

                //update guild name for all players if client is playing
                if (ObjectState == eObjectState.Active)
                {
                    Out.SendUpdatePlayer();
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (player == null) continue;
                        if (player != this)
                        {
                            player.Out.SendObjectRemove(this);
                            player.Out.SendPlayerCreate(this);
                            player.Out.SendLivingEquipmentUpdate(this);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Real Guild of the player - used for RvR where the current guild will be the realm
        /// </summary>
        public Guild RealGuild
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the player's guild rank
        /// </summary>
        public DBRank GuildRank
        {
            get { return m_guildRank; }
            set
            {
                m_guildRank = value;
                if (value != null && DBCharacter != null)
                    DBCharacter.GuildRank = value.RankLevel;//maybe mistake here and need to change and make an index var
            }
        }

        /// <summary>
        /// Gets or sets the database guildid of this player
        /// (delegate to DBCharacter)
        /// </summary>
        public string GuildID
        {
            get { return DBCharacter != null ? DBCharacter.GuildID : string.Empty; }
            set { if (DBCharacter != null) DBCharacter.GuildID = value; }
        }

        /// <summary>
        /// Gets or sets the player's guild flag
        /// (delegate to DBCharacter)
        /// </summary>
        public bool ClassNameFlag
        {
            get { return DBCharacter != null ? DBCharacter.FlagClassName : false; }
            set { if (DBCharacter != null) DBCharacter.FlagClassName = value; }
        }

        /// <summary>
        /// true if this player is looking for a group
        /// </summary>
        protected bool m_lookingForGroup;
        /// <summary>
        /// true if this player want to receive loot with autosplit between members of group
        /// </summary>
        protected bool m_autoSplitLoot = true;

        /// <summary>
        /// Gets or sets the LookingForGroup flag in this player
        /// </summary>
        public bool LookingForGroup
        {
            get { return m_lookingForGroup; }
            set { m_lookingForGroup = value; }
        }
        /// <summary>
        /// Gets/sets the autosplit for loot
        /// </summary>
        public bool AutoSplitLoot
        {
            get { return m_autoSplitLoot; }
            set { m_autoSplitLoot = value; }
        }

        /// <summary>
        /// Apply loot chance from items & buffs
        /// </summary>
        public virtual int LootChance
        {
            get
            {
                int lootchance = 0;

                lootchance += BuffBonusCategory4[eProperty.LootChance];
                lootchance += ItemBonus[(int)eProperty.LootChance];

                return Math.Max(0, lootchance);
            }
        }

        /// <summary>
        /// Gets or sets the IgnoreList of a Player
        /// (delegate to PlayerCharacter)
        /// </summary>
        public ArrayList IgnoreList
        {
            get
            {
                if (SerializedIgnoreList.Length > 0)
                    return new ArrayList(SerializedIgnoreList);
                return new ArrayList(0);
            }
            set
            {
                if (value == null)
                    SerializedIgnoreList = Array.Empty<string>();
                else
                    SerializedIgnoreList = value.OfType<string>().ToArray();

                if (DBCharacter != null)
                    GameServer.Database.SaveObject(DBCharacter);
            }
        }

        /// <summary>
        /// Modifies the friend list of this player
        /// </summary>
        /// <param name="friendName">the friend name</param>
        /// <param name="remove">true to remove this friend, false to add it</param>
        public void ModifyIgnoreList(string Name, bool remove)
        {
            ArrayList currentIgnores = IgnoreList;
            if (remove && currentIgnores != null)
            {
                if (currentIgnores.Contains(Name))
                {
                    currentIgnores.Remove(Name);
                    IgnoreList = currentIgnores;
                }
            }
            else
            {
                if (!currentIgnores!.Contains(Name))
                {
                    currentIgnores.Add(Name);
                    IgnoreList = currentIgnores;
                }
            }
        }

        #endregion

        #region X/Y/Z/Region/Realm/Position...

        /// <summary>
        /// Holds all areas this player is currently within
        /// </summary>
        private ReaderWriterList<IArea> m_currentAreas = new ReaderWriterList<IArea>();

        /// <summary>
        /// Holds all areas this player is currently within
        /// </summary>
        public override IList<IArea> CurrentAreas
        {
            get { return m_currentAreas; }
            set { m_currentAreas.FreezeWhile(l => { l.Clear(); l.AddRange(value); }); }
        }

        /// <summary>
        /// Property that saves last maximum Z value
        /// </summary>
        public const string MAX_LAST_Z = "max_last_z";

        /// <summary>
        /// The base speed of the player
        /// </summary>
        public const int PLAYER_BASE_SPEED = 191;

        public long m_areaUpdateTick = 0;


        /// <summary>
        /// Gets the tick when the areas should be updated
        /// </summary>
        public long AreaUpdateTick
        {
            get { return m_areaUpdateTick; }
            set { m_areaUpdateTick = value; }
        }

        public uint LastNPCUpdateTick = 0;
        public uint LastNPCUpdateAllTick = 0;

        public override Position Position
        {
            set
            {
                base.Position = value;
                if(DBCharacter != null) DBCharacter.SetPosition(value);
            }
        }

        /// <summary>
        /// Gets or sets the current speed of this player
        /// </summary>
        public void SetCurrentSpeed(short speed) => CurrentSpeed = Math.Min(speed, MaxSpeed);

        public override void UpdateMaxSpeed()
        {
            Out.SendUpdateMaxSpeed();
        }

        /// <summary>
        /// Gets or sets the region of this player
        /// </summary>
        public override Region CurrentRegion
        {
            set
            {
                base.CurrentRegion = value;
                if (DBCharacter != null) DBCharacter.Region = CurrentRegionID;
            }
        }

        /// <summary>
        /// Holds the zone player was in after last position update
        /// </summary>
        private Zone m_lastPositionUpdateZone;

        /// <summary>
        /// Gets or sets the zone after last position update
        /// </summary>
        public Zone LastPositionUpdateZone
        {
            get { return m_lastPositionUpdateZone; }
            set { m_lastPositionUpdateZone = value; }
        }

        public Coordinate LastUpdateCoordinate => Motion.Start.Coordinate;
        
        /// <summary>
        /// Gets or sets the players max Z for fall damage
        /// </summary>
        public float MaxLastZ { get; set; }

        /// <summary>
        /// Gets or sets the realm of this player
        /// </summary>
        public override eRealm Realm
        {
            get { return DBCharacter != null ? (eRealm)DBCharacter.Realm : base.Realm; }
            set
            {
                base.Realm = value;
                if (DBCharacter != null) DBCharacter.Realm = (byte)value;
            }
        }

        public override Angle Orientation
        {
            set
            {
                base.Orientation = value;
                if (DBCharacter != null) DBCharacter.Direction = value.InHeading;

                if (AttackState && ActiveWeaponSlot != eActiveWeaponSlot.Distance)
                {
                    AttackData ad = TempProperties.getProperty<object>(LAST_ATTACK_DATA, null) as AttackData;
                    if (ad != null && ad.IsMeleeAttack && (ad.AttackResult == eAttackResult.TargetNotVisible || ad.AttackResult == eAttackResult.OutOfRange))
                    {
                        //Does the target can be attacked ?
                        if (ad.Target != null && IsObjectInFront(ad.Target, 120) && this.IsWithinRadius(ad.Target, AttackRange) && m_attackAction != null)
                        {
                            m_attackAction.Start(1);
                        }
                    }
                }
            }
        }

        protected bool m_climbing;
        /// <summary>
        /// Gets/sets the current climbing state
        /// </summary>
        public bool IsClimbing
        {
            get { return m_climbing; }
            set
            {
                if (value == m_climbing) return;
                m_climbing = value;
            }
        }

        protected bool m_swimming;
        /// <summary>
        /// Gets/sets the current swimming state
        /// </summary>
        public virtual bool IsSwimming
        {
            get { return m_swimming; }
            set
            {
                if (value == m_swimming) return;
                m_swimming = value;
                Notify(GamePlayerEvent.SwimmingStatus, this);
                //Handle Lava Damage
                if (m_swimming && CurrentZone.IsLava == true)
                {
                    if (m_lavaBurningTimer == null)
                    {
                        m_lavaBurningTimer = new RegionTimer(this);
                        m_lavaBurningTimer.Callback = new RegionTimerCallback(LavaBurnTimerCallback);
                        m_lavaBurningTimer.Interval = 2000;
                        m_lavaBurningTimer.Start(1);
                    }
                }
                if (!m_swimming && CurrentZone.IsLava == true && m_lavaBurningTimer != null)
                {
                    m_lavaBurningTimer.Stop();
                    m_lavaBurningTimer = null;
                }
            }
        }

        /// <summary>
        /// The stuck state of this player
        /// </summary>
        private bool m_stuckFlag = false;

        /// <summary>
        /// Gets/sets the current stuck state
        /// </summary>
        public virtual bool Stuck
        {
            get { return m_stuckFlag; }
            set
            {
                if (value == m_stuckFlag) return;
                m_stuckFlag = value;
            }
        }


        public enum waterBreath : byte
        {
            Normal = 0,
            Holding = 1,
            Drowning = 2,
        }

        protected long m_beginDrowningTick;
        protected waterBreath m_currentWaterBreathState;

        protected int DrowningTimerCallback(RegionTimer callingTimer)
        {
            if (!IsAlive || ObjectState != eObjectState.Active)
                return 0;
            if (this.Client.Account.PrivLevel == 1)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.DrowningTimerCallback.CannotBreath"), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.DrowningTimerCallback.Take5%Damage"), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                if (CurrentRegion.Time - m_beginDrowningTick > 15000) // 15 sec
                {
                    TakeDamage(null, eDamageType.Natural, MaxHealth, 0);
                    Out.SendCloseTimerWindow();
                    return 0;
                }
                else
                    TakeDamage(null, eDamageType.Natural, MaxHealth / 20, 0);
            }
            return 3000;
        }

        protected int HoldingBreathTimerCallback(RegionTimer callingTimer)
        {
            m_holdBreathTimer = null;
            Diving(waterBreath.Drowning);
            return 0;
        }

        protected RegionTimer m_drowningTimer;
        protected RegionTimer m_holdBreathTimer;
        protected RegionTimer m_lavaBurningTimer;
        /// <summary>
        /// The diving state of this player
        /// </summary>
        protected bool m_diving;
        /// <summary>
        /// Gets/sets the current diving state
        /// </summary>
        public bool IsDiving
        {
            get { return m_diving; }
            set
            {
                if (m_diving != value)
                    if (value && !CanBreathUnderWater)
                        Diving(waterBreath.Holding);
                    else
                        Diving(waterBreath.Normal);
                m_diving = value;
            }
        }

        protected bool m_canBreathUnderwater;
        public bool CanBreathUnderWater
        {
            get { return m_canBreathUnderwater; }
            set
            {
                m_canBreathUnderwater = value;
                if (!value && IsDiving)
                    Diving(waterBreath.Holding);
                else
                    Diving(waterBreath.Normal);
            }
        }

        public void Diving(waterBreath state)
        {
            //bool changeSpeed = false;
            if (m_currentWaterBreathState != state)
            {
                //changeSpeed = true;
                Out.SendCloseTimerWindow();
            }

            if (m_holdBreathTimer != null)
            {
                m_holdBreathTimer.Stop();
                m_holdBreathTimer = null;
            }
            if (m_drowningTimer != null)
            {
                m_drowningTimer.Stop();
                m_drowningTimer = null;
            }
            switch (state)
            {
                case waterBreath.Normal:
                    break;
                case waterBreath.Holding:
                    if (m_holdBreathTimer == null)
                    {
                        Out.SendTimerWindow("Holding Breath", 30);
                        m_holdBreathTimer = new RegionTimer(this);
                        m_holdBreathTimer.Callback = new RegionTimerCallback(HoldingBreathTimerCallback);
                        m_holdBreathTimer.Start(30001);
                    }
                    break;
                case waterBreath.Drowning:
                    m_beginDrowningTick = CurrentRegion.Time;
                    if (m_drowningTimer == null)
                    {
                        Out.SendTimerWindow("Drowning", 15);
                        m_drowningTimer = new RegionTimer(this);
                        m_drowningTimer.Callback = new RegionTimerCallback(DrowningTimerCallback);
                        m_drowningTimer.Start(1);
                    }
                    break;
            }
            m_currentWaterBreathState = state;
            //if (changeSpeed)
            //	Out.SendUpdateMaxSpeed();
        }

        protected int LavaBurnTimerCallback(RegionTimer callingTimer)
        {
            if (!IsAlive || ObjectState != eObjectState.Active || !IsSwimming)
                return 0;

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.LavaBurnTimerCallback.YourInLava"), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.LavaBurnTimerCallback.Take34%Damage"), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
            if (Client.Account.PrivLevel == 1)
            {
                TakeDamage(null, eDamageType.Natural, (int)(MaxHealth * 0.34), 0);

                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    player.Out.SendCombatAnimation(null, this, 0x0000, 0x0000, 0x00, 0x00, 0x14, HealthPercent);
            }
            return 2000;
        }

        /// <summary>
        /// The sitting state of this player
        /// </summary>
        protected bool m_sitting;
        /// <summary>
        /// Gets/sets the current sit state
        /// </summary>
        public override bool IsSitting
        {
            get { return m_sitting; }
            set
            {
                m_sitting = value;
                if (value)
                {
                    if (IsCasting)
                        m_runningSpellHandler.CasterMoves();
                    if (AttackState && ActiveWeaponSlot == eActiveWeaponSlot.Distance)
                    {
                        string attackTypeMsg = "shot";
                        if (AttackWeapon.Object_Type == (int)eObjectType.Thrown)
                            attackTypeMsg = "throw";
                        Out.SendMessage("You move and interrupt your " + attackTypeMsg + "!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        StopAttack();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the max speed of this player
        /// (delegate to PlayerCharacter)
        /// </summary>
        public override short MaxSpeedBase
        {
            get { return (short)(DBCharacter != null ? DBCharacter.MaxSpeed : base.MaxSpeedBase); }
            set
            {
                base.MaxSpeedBase = value;
                if (DBCharacter != null) DBCharacter.MaxSpeed = value;
            }
        }

        /// <summary>
        /// the moving state of this player
        /// </summary>
        public override bool IsMoving => base.IsMoving || IsStrafing;

        /// <summary>
        /// The sprint effect of this player
        /// </summary>
        protected SprintEffect m_sprintEffect = null;
        /// <summary>
        /// Gets sprinting flag
        /// </summary>
        public bool IsSprinting
        {
            get { return m_sprintEffect != null; }
        }
        /// <summary>
        /// Change sprint state of this player
        /// </summary>
        /// <param name="state">new state</param>
        /// <returns>sprint state after command</returns>
        public virtual bool Sprint(bool state)
        {
            if (state == IsSprinting)
                return state;

            if (state)
            {
                // can't start sprinting with 10 endurance on 1.68 server
                if (Endurance <= 10)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sprint.TooFatigued"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (IsStealthed)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sprint.CantSprintHidden"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (!IsAlive)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sprint.CantSprintDead"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                m_sprintEffect = new SprintEffect();
                m_sprintEffect.Start(this);
                Out.SendUpdateMaxSpeed();
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sprint.PrepareSprint"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }
            else
            {
                m_sprintEffect.Stop();
                m_sprintEffect = null;
                Out.SendUpdateMaxSpeed();
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sprint.NoLongerReady"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
        }

        /// <summary>
        /// The strafe state of this player
        /// </summary>
        protected bool m_strafing;
        /// <summary>
        /// Gets/sets the current strafing mode
        /// </summary>
        public override bool IsStrafing
        {
            set
            {
                m_strafing = value;
                if (value)
                {
                    OnPlayerMove();
                }
            }
            get { return m_strafing; }
        }
        
        /// <summary>
        /// Gets or sets the current speed of this player
        /// </summary>
        public override short CurrentSpeed
        {
            set
            {
                base.CurrentSpeed = value;
                if (value != 0)
                {
                    OnPlayerMove();
                }
            }
        }

        public virtual void OnPlayerMove()
        {
            if (IsSitting)
            {
                Sit(false);
            }
            if (IsCasting)
            {
                m_runningSpellHandler.CasterMoves();
            }
            if (IsCastingRealmAbility)
            {
                Out.SendInterruptAnimation(this);
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "SpellHandler.CasterMove"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                RealmAbilityCastTimer.Stop();
                RealmAbilityCastTimer = null;
            }
            if (IsCrafting)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnPlayerMove.InterruptCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                StopCrafting();
            }
            if (IsSummoningMount)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnPlayerMove.CannotCallMount"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                StopWhistleTimers();
            }
            if (AttackState)
            {
                if (ActiveWeaponSlot == eActiveWeaponSlot.Distance)
                {
                    string attackTypeMsg = (AttackWeapon.Object_Type == (int)eObjectType.Thrown ? "throw" : "shot");
                    Out.SendMessage("You move and interrupt your " + attackTypeMsg + "!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    StopAttack();
                }
                else
                {
                    AttackData ad = TempProperties.getProperty<object>(LAST_ATTACK_DATA, null) as AttackData;
                    if (ad != null && ad.IsMeleeAttack && (ad.AttackResult == eAttackResult.TargetNotVisible || ad.AttackResult == eAttackResult.OutOfRange))
                    {
                        //Does the target can be attacked ?
                        if (ad.Target != null && IsObjectInFront(ad.Target, 120) && this.IsWithinRadius(ad.Target, AttackRange) && m_attackAction != null)
                        {
                            m_attackAction.Start(1);
                        }
                    }
                }
            }
            //Notify the GameEventMgr of the moving player
            GameEventMgr.Notify(GamePlayerEvent.Moving, this);
        }

        /// <summary>
        /// Sits/Stands the player
        /// </summary>
        /// <param name="sit">True if sitting, otherwise false</param>
        public virtual void Sit(bool sit)
        {
            Sprint(false);
            if (IsSummoningMount)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.InterruptCallMount"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                StopWhistleTimers();
            }
            if (IsSitting == sit)
            {
                if (sit)
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.AlreadySitting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                if (!sit)
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.NotSitting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return; // already done
            }

            if (!IsAlive)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.CantSitDead"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (IsStunned)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.CantSitStunned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (IsMezzed)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.CantSitMezzed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (sit && (CurrentSpeed > 0 || IsStrafing))
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.MustStandingStill"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (Steed != null || IsOnHorse)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.MustDismount"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (sit)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.YouSitDown"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.YouStandUp"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            //Stop attack if you sit down while attacking
            if (sit && AttackState)
            {
                StopAttack();
            }

            if (!sit)
            {
                //Stop quit sequence if you stand up
                if (m_quitTimer != null)
                {
                    m_quitTimer.Stop();
                    m_quitTimer = null;
                    Stuck = false;
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Sit.NoLongerWaitingQuit"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                //Stop praying if the player stands up...
                if (IsPraying)
                {
                    m_prayAction.Stop();
                }
            }
            //Update the client
            if (sit && !IsSitting)
                Out.SendStatusUpdate(2);// unknown why on begin sit it send 2 in sitting flag like player begin sitting
            IsSitting = sit;
            UpdatePlayerStatus();
        }

        public override Position GroundTargetPosition
        {
            set
            {
                base.GroundTargetPosition = value;
                Out.SendMessage(String.Format("You ground-target {0},{1},{2}", value.X, value.Y, value.Z), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                if (SiegeWeapon != null) SiegeWeapon.GroundTargetPosition = value;
            }
        }

        public Position[] LastUniquePositions { get; } = new Position[4];

        /// <summary>
        /// Updates Health, Mana, Sitting, Endurance, Concentration and Alive status to client
        /// </summary>
        public void UpdatePlayerStatus()
        {
            Out.SendStatusUpdate();
        }
        #endregion

        #region Equipment/Encumberance

        /// <summary>
        /// Gets the total possible Encumberance
        /// </summary>
        public virtual int MaxEncumberance
        {
            get
            {
                double enc = (double)Strength;
                RAPropertyEnhancer ab = GetAbility<LifterAbility>();
                if (ab != null)
                    enc *= 1 + ((double)ab.Amount / 100);

                // Apply Sojourner ability
                if (this.GetSpellLine("Sojourner") != null)
                {
                    enc *= 1.25;
                }

                // Apply Walord effect
                GameSpellEffect iBaneLordEffect = SpellHandler.FindEffectOnTarget(this, "Oppression");
                if (iBaneLordEffect != null)
                    enc *= 1.00 - (iBaneLordEffect.Spell.Value * 0.01);

                // Apply Mythirian Bonus
                if (GetModified(eProperty.MythicalDiscumbering) > 0)
                {
                    enc += GetModified(eProperty.MythicalDiscumbering);
                }

                return (int)enc;
            }
        }

        /// <summary>
        /// Gets the current Encumberance
        /// </summary>
        public virtual int Encumberance
        {
            get { return Inventory.InventoryWeight; }
        }

        /// <summary>
        /// The Encumberance state of this player
        /// </summary>
        protected bool m_overencumbered = true;

        /// <summary>
        /// Gets/Set the players Encumberance state
        /// </summary>
        public bool IsOverencumbered
        {
            get { return m_overencumbered; }
            set { m_overencumbered = value; }
        }

        /// <summary>
        /// Updates the appearance of the equipment this player is using
        /// </summary>
        public virtual void UpdateEquipmentAppearance()
        {
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null) continue;
                if (player != this)
                    player.Out.SendLivingEquipmentUpdate(this);
            }
        }

        /// <summary>
        /// Updates Encumberance and its effects
        /// </summary>
        public void UpdateEncumberance()
        {
            if (Inventory.InventoryWeight > MaxEncumberance)
            {
                if (IsOverencumbered == false)
                {
                    IsOverencumbered = true;
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UpdateEncumberance.EncumberedMoveSlowly"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.UpdateEncumberance.Encumbered"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                Out.SendUpdateMaxSpeed();
            }
            else if (IsOverencumbered)
            {
                IsOverencumbered = false;
                Out.SendUpdateMaxSpeed();
            }
            Out.SendEncumberance();
        }

        public override void UpdateHealthManaEndu()
        {
            Out.SendCharStatsUpdate();
            Out.SendUpdateWeaponAndArmorStats();
            UpdateEncumberance();
            UpdatePlayerStatus();
            base.UpdateHealthManaEndu();
        }

        /// <summary>
        /// Get the bonus names
        /// </summary>
        public string ItemBonusName(int BonusType)
        {
            string BonusName = "";

            if (BonusType == 1) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus1");//Strength
            if (BonusType == 2) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus2");//Dexterity
            if (BonusType == 3) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus3");//Constitution
            if (BonusType == 4) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus4");//Quickness
            if (BonusType == 5) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus5");//Intelligence
            if (BonusType == 6) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus6");//Piety
            if (BonusType == 7) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus7");//Empathy
            if (BonusType == 8) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus8");//Charisma
            if (BonusType == 9) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus9");//Power
            if (BonusType == 10) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus10");//Hits
            if (BonusType == 11) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus11");//Body
            if (BonusType == 12) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus12");//Cold
            if (BonusType == 13) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus13");//Crush
            if (BonusType == 14) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus14");//Energy
            if (BonusType == 15) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus15");//Heat
            if (BonusType == 16) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus16");//Matter
            if (BonusType == 17) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus17");//Slash
            if (BonusType == 18) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus18");//Spirit
            if (BonusType == 19) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus19");//Thrust
            if (BonusType == 119) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus119");//Tension Bonus
            if (BonusType == 147) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus147");//Max Concentration
            if (BonusType == 148) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus148");//Armor Factor
            if (BonusType == 149) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus149");//Armor Absorption
            if (BonusType == 150) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus150");//Health Regeneration
            if (BonusType == 151) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus151");//Power Regeneration
            if (BonusType == 152) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus152");//Endurance Regeneration
            if (BonusType == 153) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus153");//Spell Range
            if (BonusType == 154) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus154");//Archery Range
            if (BonusType == 155) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus155");//Melee Speed
            if (BonusType == 156) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus156");//Acuity
            if (BonusType == 169) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus169");//Evade Chance
            if (BonusType == 170) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus170");//Block Chance
            if (BonusType == 171) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus171");//Parry Chance
            if (BonusType == 175) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus175");//Fumble Chance
            if (BonusType == 181) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus181");//Spell Fumble Chance
            if (BonusType == 197) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus197");//Resist Piercing
            if (BonusType == 249) BonusName = LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ItemBonusName.Bonus249");//Essence Resist
            return BonusName;
        }

        /// <summary>
        /// Adds magical bonuses whenever item was equipped
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender">inventory</param>
        /// <param name="arguments"></param>
        protected virtual void OnItemEquipped(DOLEvent e, object sender, EventArgs arguments)
        {
            if (arguments is ItemEquippedArgs == false) return;
            InventoryItem item = ((ItemEquippedArgs)arguments).Item;
            if (item == null) return;

            if (item is IGameInventoryItem)
            {
                (item as IGameInventoryItem)!.OnEquipped(this);
            }

            if (item.Item_Type >= Slot.RIGHTHAND && item.Item_Type <= Slot.RANGED)
            {
                if (item.Hand == 1) // 2h
                {
                    Out.SendMessage(string.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnItemEquipped.WieldBothHands", item.GetName(0, false))), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else if (item.SlotPosition == Slot.LEFTHAND)
                {
                    Out.SendMessage(string.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnItemEquipped.WieldLeftHand", item.GetName(0, false))), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    Out.SendMessage(string.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnItemEquipped.WieldRightHand", item.GetName(0, false))), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }

            if (item.Item_Type == (int)eInventorySlot.Horse)
            {
                if (item.SlotPosition == Slot.HORSE)
                {
                    ActiveHorse.ID = (byte)(item.SPD_ABS == 0 ? 1 : item.SPD_ABS);
                    ActiveHorse.Name = item.Creator;
                }
                return;
            }
            else if (item.Item_Type == (int)eInventorySlot.HorseArmor)
            {
                if (item.SlotPosition == Slot.HORSEARMOR)
                {
                    //					Out.SendDebugMessage("Try apply horse armor.");
                    ActiveHorse.Saddle = (byte)(item.DPS_AF);
                }
                return;
            }
            else if (item.Item_Type == (int)eInventorySlot.HorseBarding)
            {
                if (item.SlotPosition == Slot.HORSEBARDING)
                {
                    //					Out.SendDebugMessage("Try apply horse barding.");
                    ActiveHorse.Barding = (byte)(item.DPS_AF);
                }
                return;
            }

            if (!item.IsMagical) return;

            if (this.Level < item.BonusLevel)
            {
                Out.SendMessage(
                    $"You are not high enough level to benefit from {item.Name}'s bonuses. (Need level {item.BonusLevel})",
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            Out.SendMessage(string.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnItemEquipped.Magic", item.GetName(0, false))), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);

            if (item is GameInventoryItem gameItem)
            {
                if (item.Bonus1 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus1), this))
                {
                    ItemBonus[item.Bonus1Type] += item.Bonus1;
                    ShowIncreasedMessage(item.Bonus1Type);
                }
                if (item.Bonus2 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus2), this))
                {
                    ItemBonus[item.Bonus2Type] += item.Bonus2;
                    ShowIncreasedMessage(item.Bonus2Type);
                }
                if (item.Bonus3 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus3), this))
                {
                    ItemBonus[item.Bonus3Type] += item.Bonus3;
                    ShowIncreasedMessage(item.Bonus3Type);
                }
                if (item.Bonus4 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus4), this))
                {
                    ItemBonus[item.Bonus4Type] += item.Bonus4;
                    ShowIncreasedMessage(item.Bonus4Type);
                }
                if (item.Bonus5 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus5), this))
                {
                    ItemBonus[item.Bonus5Type] += item.Bonus5;
                    ShowIncreasedMessage(item.Bonus5Type);
                }
                if (item.Bonus6 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus6), this))
                {
                    ItemBonus[item.Bonus6Type] += item.Bonus6;
                    ShowIncreasedMessage(item.Bonus6Type);
                }
                if (item.Bonus7 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus7), this))
                {
                    ItemBonus[item.Bonus7Type] += item.Bonus7;
                    ShowIncreasedMessage(item.Bonus7Type);
                }
                if (item.Bonus8 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus8), this))
                {
                    ItemBonus[item.Bonus8Type] += item.Bonus8;
                    ShowIncreasedMessage(item.Bonus8Type);
                }
                if (item.Bonus9 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus9), this))
                {
                    ItemBonus[item.Bonus9Type] += item.Bonus9;
                    ShowIncreasedMessage(item.Bonus9Type);
                }
                if (item.Bonus10 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus10), this))
                {
                    ItemBonus[item.Bonus10Type] += item.Bonus10;
                    ShowIncreasedMessage(item.Bonus10Type);
                }
                if (item.ExtraBonus != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.ExtraBonus), this))
                {
                    ItemBonus[item.ExtraBonusType] += item.ExtraBonus;
                    ShowIncreasedMessage(item.ExtraBonusType);
                }
            }

            //Check null on client.player bypass region change
            if (Client.Account.PrivLevel == (uint)ePrivLevel.Player && Client.Player != null && Client.Player.ObjectState == eObjectState.Active)
                if (item.SpellID > 0 || item.SpellID1 > 0)
                    TempProperties.setProperty("ITEMREUSEDELAY" + item.Id_nb, CurrentRegion.Time);

            if (ObjectState == eObjectState.Active)
            {
                // TODO: remove when properties system is finished
                Out.SendCharStatsUpdate();
                Out.SendCharResistsUpdate();
                Out.SendUpdateWeaponAndArmorStats();
                Out.SendUpdateMaxSpeed();
                Out.SendEncumberance();
                Out.SendUpdatePlayerSkills();
                UpdatePlayerStatus();


                if (IsAlive)
                {
                    if (Health < MaxHealth) StartHealthRegeneration();
                    else if (Health > MaxHealth) Health = MaxHealth;

                    if (Mana < MaxMana) StartPowerRegeneration();
                    else if (Mana > MaxMana) Mana = MaxMana;

                    if (Endurance < MaxEndurance) StartEnduranceRegeneration();
                    else if (Endurance > MaxEndurance) Endurance = MaxEndurance;
                }
            }
        }

        /// <summary>
        /// Helper to show "Your X has increased!" messages.
        /// </summary>
        private void ShowIncreasedMessage(int bonusType)
        {
            if (bonusType < 20 || bonusType == 119
                || (bonusType >= 147 && bonusType <= 156)
                || (bonusType >= 169 && bonusType <= 171)
                || bonusType == 175 || bonusType == 181
                || bonusType == 197 || bonusType == 249)
            {
                Out.SendMessage(
                    string.Format(LanguageMgr.GetTranslation(Client.Account.Language,
                    "GameObjects.GamePlayer.OnItemEquipped.Increased",
                    ItemBonusName(bonusType))),
                    eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
            }
        }

        /// <summary>
        /// Removes magical bonuses whenever item was unequipped
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender">inventory</param>
        /// <param name="arguments"></param>
        protected virtual void OnItemUnequipped(DOLEvent e, object sender, EventArgs arguments)
        {
            if (arguments is ItemUnequippedArgs == false) return;
            InventoryItem item = ((ItemUnequippedArgs)arguments).Item;
            int prevSlot = (int)((ItemUnequippedArgs)arguments).PreviousSlotPosition;
            if (item == null) return;

            if (item.Item_Type >= Slot.RIGHTHAND && item.Item_Type <= Slot.RANGED)
            {
                if (item.Hand == 1) // 2h
                {
                    Out.SendMessage(string.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnItemUnequipped.BothHandsFree", item.GetName(0, false))), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
                }
                else if (prevSlot == Slot.LEFTHAND)
                {
                    Out.SendMessage(string.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnItemUnequipped.LeftHandFree", item.GetName(0, false))), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    Out.SendMessage(string.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OnItemUnequipped.RightHandFree", item.GetName(0, false))), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
                }
            }

            if (prevSlot == Slot.MYTHICAL && item.Item_Type == (int)eInventorySlot.Mythical && item is GameMythirian)
            {
                (item as GameMythirian)!.OnUnEquipped(this);
            }

            if (item.Item_Type == (int)eInventorySlot.Horse)
            {
                if (IsOnHorse)
                    IsOnHorse = false;
                ActiveHorse.ID = 0;
                ActiveHorse.Name = "";
                //				Out.SendDebugMessage("Try unapply horse.");
                return;
            }
            else if (item.Item_Type == (int)eInventorySlot.HorseArmor)
            {
                ActiveHorse.Saddle = 0;
                //				Out.SendDebugMessage("Try unapply saddle.");
                return;
            }
            else if (item.Item_Type == (int)eInventorySlot.HorseBarding)
            {
                ActiveHorse.Barding = 0;
                //				Out.SendDebugMessage("Try unapply barding.");
                return;
            }

            if (!item.IsMagical) return;

            if (this.Level < item.BonusLevel)
            {
                return;
            }

            if (item is GameInventoryItem gameItem)
            {
                if (item.Bonus1 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus1), this))
                {
                    ItemBonus[item.Bonus1Type] -= item.Bonus1;
                    ShowDecreasedMessage(item.Bonus1Type);
                }
                if (item.Bonus2 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus2), this))
                {
                    ItemBonus[item.Bonus2Type] -= item.Bonus2;
                    ShowDecreasedMessage(item.Bonus2Type);
                }
                if (item.Bonus3 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus3), this))
                {
                    ItemBonus[item.Bonus3Type] -= item.Bonus3;
                    ShowDecreasedMessage(item.Bonus3Type);
                }
                if (item.Bonus4 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus4), this))
                {
                    ItemBonus[item.Bonus4Type] -= item.Bonus4;
                    ShowDecreasedMessage(item.Bonus4Type);
                }
                if (item.Bonus5 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus5), this))
                {
                    ItemBonus[item.Bonus5Type] -= item.Bonus5;
                    ShowDecreasedMessage(item.Bonus5Type);
                }
                if (item.Bonus6 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus6), this))
                {
                    ItemBonus[item.Bonus6Type] -= item.Bonus6;
                    ShowDecreasedMessage(item.Bonus6Type);
                }
                if (item.Bonus7 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus7), this))
                {
                    ItemBonus[item.Bonus7Type] -= item.Bonus7;
                    ShowDecreasedMessage(item.Bonus7Type);
                }
                if (item.Bonus8 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus8), this))
                {
                    ItemBonus[item.Bonus8Type] -= item.Bonus8;
                    ShowDecreasedMessage(item.Bonus8Type);
                }
                if (item.Bonus9 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus9), this))
                {
                    ItemBonus[item.Bonus9Type] -= item.Bonus9;
                    ShowDecreasedMessage(item.Bonus9Type);
                }
                if (item.Bonus10 != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.Bonus10), this))
                {
                    ItemBonus[item.Bonus10Type] -= item.Bonus10;
                    ShowDecreasedMessage(item.Bonus10Type);
                }
                if (item.ExtraBonus != 0 && gameItem != null && gameItem.IsBonusAllowed(nameof(item.ExtraBonus), this))
                {
                    ItemBonus[item.ExtraBonusType] -= item.ExtraBonus;
                    ShowDecreasedMessage(item.ExtraBonusType);
                }
            }

            if (item is IGameInventoryItem)
            {
                (item as IGameInventoryItem)!.OnUnEquipped(this);
            }

            if (ObjectState == eObjectState.Active)
            {
                // TODO: remove when properties system is finished
                Out.SendCharStatsUpdate();
                Out.SendCharResistsUpdate();
                Out.SendUpdateWeaponAndArmorStats();
                Out.SendUpdateMaxSpeed();
                Out.SendEncumberance();
                Out.SendUpdatePlayerSkills();
                UpdatePlayerStatus();

                if (IsAlive)
                {
                    if (Health < MaxHealth) StartHealthRegeneration();
                    else if (Health > MaxHealth) Health = MaxHealth;

                    if (Mana < MaxMana) StartPowerRegeneration();
                    else if (Mana > MaxMana) Mana = MaxMana;

                    if (Endurance < MaxEndurance) StartEnduranceRegeneration();
                    else if (Endurance > MaxEndurance) Endurance = MaxEndurance;
                }
            }
        }

        /// <summary>
        /// Helper to show "Your X has decreased!" messages.
        /// </summary>
        private void ShowDecreasedMessage(int bonusType)
        {
            if (bonusType < 20 || bonusType == 119
                || (bonusType >= 147 && bonusType <= 156)
                || (bonusType >= 169 && bonusType <= 171)
                || bonusType == 175 || bonusType == 181
                || bonusType == 197 || bonusType == 249)
            {
                Out.SendMessage(
                    string.Format(LanguageMgr.GetTranslation(Client.Account.Language,
                    "GameObjects.GamePlayer.OnItemUnequipped.Decreased",
                    ItemBonusName(bonusType))),
                    eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
            }
        }

        public virtual void RefreshItemBonuses()
        {
            m_itemBonus = new PropertyIndexer();
            string slotToLoad = "";
            switch (VisibleActiveWeaponSlots)
            {
                case 16: slotToLoad = "rightandleftHandSlot"; break;
                case 18: slotToLoad = "leftandtwoHandSlot"; break;
                case 31: slotToLoad = "leftHandSlot"; break;
                case 34: slotToLoad = "twoHandSlot"; break;
                case 51: slotToLoad = "distanceSlot"; break;
                case 240: slotToLoad = "righttHandSlot"; break;
                case 242: slotToLoad = "twoHandSlot"; break;
                default: break;
            }

            //log.Debug("VisibleActiveWeaponSlots= " + VisibleActiveWeaponSlots);
            foreach (InventoryItem item in Inventory.EquippedItems)
            {
                if (item == null)
                    continue;
                // skip weapons. only active weapons should fire equip event, done in player.SwitchWeapon
                bool add = true;

                if (slotToLoad != "")
                {
                    switch (item.SlotPosition)
                    {

                        case Slot.TWOHAND:
                            if (slotToLoad.Contains("twoHandSlot") == false)
                            {
                                add = false;
                            }
                            break;

                        case Slot.RIGHTHAND:
                            if (slotToLoad.Contains("right") == false)
                            {
                                add = false;
                            }
                            break;
                        case Slot.SHIELD:
                        case Slot.LEFTHAND:
                            if (slotToLoad.Contains("left") == false)
                            {
                                add = false;
                            }
                            break;
                        case Slot.RANGED:
                            if (slotToLoad != "distanceSlot")
                            {
                                add = false;
                            }
                            break;
                        default: break;
                    }
                }

                if (!add) continue;
                if (item is GameInventoryItem gameItem)
                {
                    gameItem.CheckValid(this);

                    if (this.Level < item.BonusLevel)
                    {
                        continue;
                    }

                    if (item.IsMagical)
                    {
                        if (item.Bonus1 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus1), this))
                        {
                            ItemBonus[item.Bonus1Type] += item.Bonus1;
                        }
                        if (item.Bonus2 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus2), this))
                        {
                            ItemBonus[item.Bonus2Type] += item.Bonus2;
                        }
                        if (item.Bonus3 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus3), this))
                        {
                            ItemBonus[item.Bonus3Type] += item.Bonus3;
                        }
                        if (item.Bonus4 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus4), this))
                        {
                            ItemBonus[item.Bonus4Type] += item.Bonus4;
                        }
                        if (item.Bonus5 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus5), this))
                        {
                            ItemBonus[item.Bonus5Type] += item.Bonus5;
                        }
                        if (item.Bonus6 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus6), this))
                        {
                            ItemBonus[item.Bonus6Type] += item.Bonus6;
                        }
                        if (item.Bonus7 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus7), this))
                        {
                            ItemBonus[item.Bonus7Type] += item.Bonus7;
                        }
                        if (item.Bonus8 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus8), this))
                        {
                            ItemBonus[item.Bonus8Type] += item.Bonus8;
                        }
                        if (item.Bonus9 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus9), this))
                        {
                            ItemBonus[item.Bonus9Type] += item.Bonus9;
                        }
                        if (item.Bonus10 != 0 && gameItem.IsBonusAllowed(nameof(item.Bonus10), this))
                        {
                            ItemBonus[item.Bonus10Type] += item.Bonus10;
                        }
                        if (item.ExtraBonus != 0 && gameItem.IsBonusAllowed(nameof(item.ExtraBonus), this))
                        {
                            ItemBonus[item.ExtraBonusType] += item.ExtraBonus;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles a bonus change on an item.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected virtual void OnItemBonusChanged(DOLEvent e, object sender, EventArgs args)
        {
            ItemBonusChangedEventArgs changeArgs = args as ItemBonusChangedEventArgs;
            if (changeArgs == null || changeArgs.BonusType == 0 || changeArgs.BonusAmount == 0)
                return;

            ItemBonus[changeArgs.BonusType] += changeArgs.BonusAmount;

            if (ObjectState == eObjectState.Active)
            {
                Out.SendCharStatsUpdate();
                Out.SendCharResistsUpdate();
                Out.SendUpdateWeaponAndArmorStats();
                Out.SendUpdatePlayerSkills();
                UpdatePlayerStatus();

                if (IsAlive)
                {
                    if (Health < MaxHealth) StartHealthRegeneration();
                    else if (Health > MaxHealth) Health = MaxHealth;

                    if (Mana < MaxMana) StartPowerRegeneration();
                    else if (Mana > MaxMana) Mana = MaxMana;

                    if (Endurance < MaxEndurance) StartEnduranceRegeneration();
                    else if (Endurance > MaxEndurance) Endurance = MaxEndurance;
                }
            }
        }

        #endregion

        #region ReceiveItem/DropItem/PickupObject
        /// <summary>
        /// Receive an item from another living
        /// </summary>
        /// <param name="source"></param>
        /// <param name="item"></param>
        /// <returns>true if player took the item</returns>
        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (item == null) return false;

            if (!Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, item))
                return false;

            if (source == null)
            {
                Out.SendMessage(String.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ReceiveItem.Receive", item.GetName(0, false))), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
            }
            else
            {
                if (source is GameNPC)
                    Out.SendMessage(String.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ReceiveItem.ReceiveFrom", item.GetName(0, false), source.GetName(0, false, Client.Account.Language, (source as GameNPC)))), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
                else
                    Out.SendMessage(String.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.ReceiveItem.ReceiveFrom", item.GetName(0, false), GetPersonalizedName(source))), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
            }

            if (source is GamePlayer)
            {
                GamePlayer sourcePlayer = source as GamePlayer;
                if (sourcePlayer != null)
                {
                    uint privLevel1 = Client.Account.PrivLevel;
                    uint privLevel2 = sourcePlayer.Client.Account.PrivLevel;
                    if (privLevel1 != privLevel2
                        && (privLevel1 > 1 || privLevel2 > 1)
                        && (privLevel1 == 1 || privLevel2 == 1))
                    {
                        GameServer.Instance.LogGMAction("   Item: " + source.Name + "(" + sourcePlayer.Client.Account.Name + ") -> " + Name + "(" + Client.Account.Name + ") : " + item.Name + "(" + item.Id_nb + ")");
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Called to drop an Item from the Inventory to the floor
        /// </summary>
        /// <param name="slot_pos">SlotPosition to drop</param>
        /// <returns>true if dropped</returns>
        public virtual bool DropItem(eInventorySlot slot_pos)
        {
            WorldInventoryItem tempItem;
            return DropItem(slot_pos, out tempItem);
        }

        /// <summary>
        /// Called to drop an item from the Inventory to the floor
        /// and return the GameInventoryItem that is created on the floor
        /// </summary>
        /// <param name="slot_pos">SlotPosition to drop</param>
        /// <param name="droppedItem">out GameItem that was created</param>
        /// <returns>true if dropped</returns>
        public virtual bool DropItem(eInventorySlot slot_pos, out WorldInventoryItem droppedItem)
        {
            droppedItem = null;
            if (slot_pos >= eInventorySlot.FirstBackpack && slot_pos <= eInventorySlot.LastBackpack)
            {
                lock (Inventory)
                {
                    InventoryItem item = Inventory.GetItem(slot_pos);
                    bool unauthorized = false;
                    if (!item.IsDropable)
                    {
                        unauthorized = true;
                    }
                    //Check if Item Can be used in RvR
                    else if ((this.isInBG || this.IsInPvP || this.IsInRvR) && !item.CanUseInRvR)
                    {
                        unauthorized = true;
                    }
                    //prevent magical item use in not authorized zones
                    else if (item.Object_Type == (int)eObjectType.Magical && !this.CurrentZone.AllowMagicalItem)
                    {
                        unauthorized = true;
                    }

                    if (unauthorized)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.CannotDepositHere", item.GetName(0, true)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }

                    if (!Inventory.RemoveItem(item)) return false;
                    InventoryLogging.LogInventoryAction(this, "", "(ground)", eInventoryActionType.Other, item, item.Count);

                    droppedItem = CreateItemOnTheGround(item);

                    if (droppedItem != null)
                    {
                        Notify(PlayerInventoryEvent.ItemDropped, this, new ItemDroppedEventArgs(item, droppedItem));
                    }

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// called to make an item on the ground with owner is player
        /// </summary>
        /// <param name="item">the item to create on the ground</param>
        /// <returns>the GameInventoryItem on the ground</returns>
        public virtual WorldInventoryItem CreateItemOnTheGround(InventoryItem item)
        {
            WorldInventoryItem gameItem = null;

            if (item is IGameInventoryItem)
            {
                gameItem = (item as IGameInventoryItem)!.Drop(this);
            }
            else
            {
                gameItem = new WorldInventoryItem(item);
                gameItem.Position = Position + Vector.Create(Orientation, length: 30);

                gameItem.AddOwner(this);
                gameItem.AddToWorld();
            }

            return gameItem;
        }
        /// <summary>
        /// Called when the player picks up an item from the ground
        /// </summary>
        /// <param name="floorObject"></param>
        /// <param name="checkRange"></param>
        /// <returns></returns>
        public virtual bool PickupObject(GameObject floorObject, bool checkRange)
        {
            if (floorObject == null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.MustHaveTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (floorObject.ObjectState != eObjectState.Active)
                return false;

            if (floorObject is GameStaticItemTimed && ((GameStaticItemTimed)floorObject).IsOwner(this) == false && Client.Account.PrivLevel == (int)ePrivLevel.Player)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.LootDoesntBelongYou"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if ((floorObject is GameBoat == false) && !checkRange && !floorObject.IsWithinRadius2D(this, GS.ServerProperties.Properties.WORLD_PICKUP_DISTANCE))
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.ObjectTooFarAway", floorObject.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                try
                {
                    log.DebugFormat("Pickup error: {0}  object x{1}, y{2}, z{3}, r{4} - player x{5}, y{6}, z{7}, r{8}",
                                    Name,
                                    floorObject.Position.X, floorObject.Position.Y, floorObject.Position.Z, floorObject.Position.RegionID,
                                    Position.X, Position.Y, Position.Z, Position.RegionID);
                }
                catch
                {
                }
                return false;
            }

            if (floorObject is WorldInventoryItem)
            {
                WorldInventoryItem floorItem = floorObject as WorldInventoryItem;

                lock (floorItem!)
                {
                    if (floorItem.ObjectState != eObjectState.Active)
                        return false;

                    if (floorItem.Item == null || floorItem.Item.IsPickable == false)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.CantGetThat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }
                    if (floorItem.GetPickupTime > 0)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.WaitToPickup", floorItem.GetPickupTime / 1000, floorItem.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }

                    Group group = Group;
                    BattleGroup mybattlegroup = (BattleGroup)TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                    if (mybattlegroup != null && mybattlegroup.GetBGLootType() == true && mybattlegroup.GetBGTreasurer() != null)
                    {
                        GamePlayer theTreasurer = mybattlegroup.GetBGTreasurer();
                        if (theTreasurer.CanSeeObject(floorObject))
                        {
                            bool good = false;
                            if (floorItem.Item.IsStackable)
                                good = theTreasurer.Inventory.AddTemplate(floorItem.Item, floorItem.Item.Count, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                            else
                                good = theTreasurer.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, floorItem.Item);

                            if (!good)
                            {
                                theTreasurer.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.BackpackFull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return false;
                            }
                            theTreasurer.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.YouGet", floorItem.Item.GetName(1, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                            {
                                if (!(this == player))
                                {
                                    player.MessageFromArea(this, LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.PickupObject.GroupMemberPicksUp",
                                        player.GetPersonalizedName(this), floorItem.Item.GetName(1, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                }
                            }
                            InventoryLogging.LogInventoryAction("", "(ground)", this, eInventoryActionType.Loot, floorItem.Item, floorItem.Item.IsStackable ? floorItem.Item.Count : 1);
                        }
                        else
                        {
                            mybattlegroup.SendMessageToBattleGroupMembers(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.NoOneWantsThis", floorObject.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                    else if (group != null && group.AutosplitLoot)
                    {
                        List<GameObject> owners = new List<GameObject>((GameObject[])floorItem.Owners);
                        List<GamePlayer> eligibleMembers = new List<GamePlayer>(8);
                        foreach (GamePlayer ply in group.GetPlayersInTheGroup())
                        {
                            if (ply.IsAlive
                                && ply.CanSeeObject(floorObject)
                                && this.IsWithinRadius(ply, WorldMgr.MAX_EXPFORKILL_DISTANCE)
                                && (ply.ObjectState == eObjectState.Active)
                                && (ply.AutoSplitLoot)
                                && (owners.Contains(ply) || owners.Count == 0)
                                && (ply.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack) != eInventorySlot.Invalid))
                            {
                                eligibleMembers.Add(ply);
                            }
                        }

                        if (eligibleMembers.Count <= 0)
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.NoOneWantsThis", floorObject.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return false;
                        }

                        int i = Util.Random(0, eligibleMembers.Count - 1);
                        GamePlayer eligibleMember = eligibleMembers[i];
                        if (eligibleMember != null)
                        {
                            bool good = false;
                            if (floorItem.Item.IsStackable) // poison ID is lost here
                                good = eligibleMember.Inventory.AddTemplate(floorItem.Item, floorItem.Item.Count, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                            else
                                good = eligibleMember.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, floorItem.Item);

                            if (!good)
                            {
                                eligibleMember.Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.BackpackFull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return false;
                            }
                            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                            {
                                if (!(this == player))
                                {
                                    player.MessageFromArea(this, LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.PickupObject.GroupMemberPicksUp",
                                        player.GetPersonalizedName(this), floorItem.Item.GetName(1, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                }
                            }
                            group.SendMessageToGroupMembers(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.Autosplit", floorItem.Item.GetName(1, true), eligibleMember.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            InventoryLogging.LogInventoryAction("", "(ground)", this, eInventoryActionType.Loot, floorItem.Item, floorItem.Item.IsStackable ? floorItem.Item.Count : 1);
                        }
                    }
                    else
                    {
                        bool good = false;
                        if (floorItem.Item.IsStackable)
                            good = Inventory.AddTemplate(GameInventoryItem.Create(floorItem.Item), floorItem.Item.Count, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                        else
                            good = Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, floorItem.Item);

                        if (!good)
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.BackpackFull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return false;
                        }
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.YouGet", floorItem.Item.GetName(1, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                        {
                            if (!(this == player))
                            {
                                player.MessageFromArea(this, LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.PickupObject.GroupMemberPicksUp",
                                    player.GetPersonalizedName(this), floorItem.Item.GetName(1, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                        }
                        InventoryLogging.LogInventoryAction("", "(ground)", this, eInventoryActionType.Loot, floorItem.Item, floorItem.Item.IsStackable ? floorItem.Item.Count : 1);
                    }
                    floorItem.RemoveFromWorld();
                }
                return true;
            }
            else if (floorObject is GameMoney)
            {
                GameMoney moneyObject = floorObject as GameMoney;
                lock (moneyObject!)
                {
                    if (moneyObject.ObjectState != eObjectState.Active)
                        return false;

                    if (Group != null && Group.AutosplitCoins)
                    {
                        //Spread the money in the group
                        var eligibleMembers = from p in Group.GetPlayersInTheGroup()
                                              where p.IsAlive && p.CanSeeObject(floorObject) && p.ObjectState == eObjectState.Active
                                              select p;
                        if (!eligibleMembers.Any())
                        {
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.NoOneGroupWantsMoney"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return false;
                        }

                        long moneyToPlayer = moneyObject.TotalCopper / eligibleMembers.Count();
                        foreach (GamePlayer eligibleMember in eligibleMembers)
                        {
                            if (eligibleMember.Guild != null && eligibleMember.Guild.IsGuildDuesOn())
                            {
                                long moneyToGuild = moneyToPlayer * eligibleMember.Guild.GetGuildDuesPercent() / 100;
                                if (eligibleMember.Guild.GetGuildDuesPercent() != 100)
                                {
                                    eligibleMember.AddMoney(Currency.Copper.Mint(moneyToPlayer));
                                    eligibleMember.SendSystemMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.YourLootShare", Money.GetString(moneyToPlayer)));
                                }
                                else
                                    eligibleMember.AddMoney(Currency.Copper.Mint(moneyToPlayer));

                                InventoryLogging.LogInventoryAction("", "(ground)", eligibleMember, eInventoryActionType.Loot, moneyToPlayer);
                                eligibleMember.Guild.SetGuildBank(eligibleMember, moneyToGuild);
                            }
                            else
                            {

                                eligibleMember.AddMoney(Currency.Copper.Mint(moneyToPlayer));
                                eligibleMember.SendSystemMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.YourLootShare", Money.GetString(moneyToPlayer)));
                                InventoryLogging.LogInventoryAction("", "(ground)", eligibleMember, eInventoryActionType.Loot, moneyToPlayer);
                            }
                        }
                    }
                    else
                    {
                        // Add money only to picking player
                        if (Guild != null && Guild.IsGuildDuesOn() && moneyObject.TotalCopper > 0 && Guild.GetGuildDuesPercent() > 0)
                        {
                            long moneyToGuild = moneyObject.TotalCopper * Guild.GetGuildDuesPercent() / 100;
                            if (Guild.GetGuildDuesPercent() != 100)
                            {
                                AddMoney(Currency.Copper.Mint(moneyObject.TotalCopper));
                                SendSystemMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.YouPickUp", Money.GetString(moneyObject.TotalCopper)));
                            }
                            else
                            {
                                AddMoney(Currency.Copper.Mint(moneyObject.TotalCopper));
                            }
                            InventoryLogging.LogInventoryAction("", "(ground)", this, eInventoryActionType.Loot, moneyObject.TotalCopper);
                            Guild.SetGuildBank(this, moneyToGuild);
                            Out.SendMessage(LanguageMgr.GetTranslation(Client, "GameObjects.GamePlayer.PickupObject.GuildDueTax", Money.GetString(moneyToGuild)), eChatType.CT_Loot, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            AddMoney(Currency.Copper.Mint(moneyObject.TotalCopper));
                            SendSystemMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.YouPickUp", Money.GetString(moneyObject.TotalCopper)));
                            InventoryLogging.LogInventoryAction("", "(ground)", this, eInventoryActionType.Loot, moneyObject.TotalCopper);
                        }
                    }
                    moneyObject.Delete();
                    return true;
                }
            }
            else if (floorObject is GameBoat)
            {
                if (!this.IsWithinRadius(floorObject, 1000))
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.TooFarFromBoat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                if (!InCombat)
                    MountSteed(floorObject as GameBoat, false);

                return true;
            }
            else if (floorObject is GameHouseVault && floorObject.CurrentHouse != null)
            {
                GameHouseVault houseVault = floorObject as GameHouseVault;
                if (houseVault!.Detach(this))
                {
                    ItemTemplate template = GameServer.Database.FindObjectByKey<ItemTemplate>(houseVault.TemplateID);
                    Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, GameInventoryItem.Create(template));
                    InventoryLogging.LogInventoryAction(floorObject.CurrentHouse.DatabaseItem.ObjectId, "(HOUSE;" + floorObject.CurrentHouse.HouseNumber + ")", this, eInventoryActionType.Other, template);
                }
                return true;
            }
            else if ((floorObject is GameNPC || floorObject is GameStaticItem) && floorObject.CurrentHouse != null)
            {
                floorObject.CurrentHouse.EmptyHookpoint(this, floorObject);
                return true;
            }
            else if (floorObject is GameTradingTable)
            {
                GameTradingTable tradingObject = floorObject as GameTradingTable;
                if (ObjectId == tradingObject!.consignmentMerchant.OwnerID)
                    Out.SendDialogBox(eDialogCode.CloseMarket, 0, 0, 0, 0, eDialogType.YesNo, true,
                        LanguageMgr.GetTranslation(Client, "Commands.Players.Market.Confirm.Close"));
                return true;
            }
            else
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.PickupObject.CantGetThat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
        }

        /// <summary>
        /// Checks to see if an object is viewable from the players perspective
        /// </summary>
        /// <param name="obj">The Object to be seen</param>
        /// <returns>True/False</returns>
        public bool CanSeeObject(GameObject obj)
        {
            return IsWithinRadius(obj, WorldMgr.VISIBILITY_DISTANCE);
        }

        /// <summary>
        /// Checks to see if an object is viewable from the players perspective
        /// </summary>
        /// <param name="player">The Player that can see</param>
        /// <param name="obj">The Object to be seen</param>
        /// <returns>True/False</returns>
        public static bool CanSeeObject(GamePlayer player, GameObject obj)
        {
            return player.IsWithinRadius(obj, WorldMgr.VISIBILITY_DISTANCE);
        }

        #endregion

        #region Database

        /// <summary>
        /// Subtracts the current time from the last time the character was saved
        /// and adds it in the form of seconds to player.PlayedTime
        /// for the /played command.
        /// </summary>
        public long PlayedTime
        {
            get
            {
                DateTime rightNow = DateTime.Now;
                DateTime oldLast = LastPlayed;
                // Get the total amount of time played between now and lastplayed
                // This is safe as lastPlayed is updated on char load.
                TimeSpan playaPlayed = rightNow.Subtract(oldLast);
                TimeSpan newPlayed = playaPlayed + TimeSpan.FromSeconds(DBCharacter.PlayedTime);
                return (long)newPlayed.TotalSeconds;
            }
        }

        /// <summary>
        /// Saves the player's skills
        /// </summary>
        protected virtual void SaveSkillsToCharacter()
        {
            StringBuilder ab = new StringBuilder();
            StringBuilder sp = new StringBuilder();

            // Build Serialized Spec list
            List<Specialization> specs = null;
            lock (((ICollection)m_specialization).SyncRoot)
            {
                specs = m_specialization.Values.Where(s => s.AllowSave).ToList();
                foreach (Specialization spec in specs)
                {
                    if (sp.Length > 0)
                    {
                        sp.Append(";");
                    }
                    sp.AppendFormat("{0}|{1}", spec.KeyName, spec.GetSpecLevelForLiving(this));
                }
            }

            // Build Serialized Ability List to save Order
            foreach (Ability ability in m_usableSkills.Where(e => e.Item1 is Ability).Select(e => e.Item1).Cast<Ability>())
            {
                if (ability != null)
                {
                    if (ab.Length > 0)
                    {
                        ab.Append(";");
                    }
                    ab.AppendFormat("{0}|{1}", ability.KeyName, ability.Level);
                }
            }

            // Build Serialized disabled Spell/Ability
            StringBuilder disabledSpells = new StringBuilder();
            StringBuilder disabledAbilities = new StringBuilder();

            ICollection<Skill> disabledSkills = GetAllDisabledSkills();

            foreach (Skill skill in disabledSkills)
            {
                int duration = GetSkillDisabledDuration(skill);

                if (duration <= 0)
                    continue;

                if (skill is Spell)
                {
                    Spell spl = (Spell)skill;

                    if (disabledSpells.Length > 0)
                        disabledSpells.Append(";");

                    disabledSpells.AppendFormat("{0}|{1}", spl.ID, duration);
                }
                else if (skill is Ability)
                {
                    Ability ability = (Ability)skill;

                    if (disabledAbilities.Length > 0)
                        disabledAbilities.Append(";");

                    disabledAbilities.AppendFormat("{0}|{1}", ability.KeyName, duration);
                }
                else
                {
                    if (log.IsWarnEnabled)
                        log.WarnFormat("{0}: Can't save disabled skill {1}", Name, skill.GetType().ToString());
                }
            }

            StringBuilder sra = new StringBuilder();

            foreach (RealmAbility rab in m_realmAbilities)
            {
                if (sra.Length > 0)
                    sra.Append(";");

                sra.AppendFormat("{0}|{1}", rab.KeyName, rab.Level);
            }

            if (DBCharacter != null)
            {
                DBCharacter.SerializedAbilities = ab.ToString();
                DBCharacter.SerializedSpecs = sp.ToString();
                DBCharacter.SerializedRealmAbilities = sra.ToString();
                DBCharacter.DisabledSpells = disabledSpells.ToString();
                DBCharacter.DisabledAbilities = disabledAbilities.ToString();
            }
        }
        /// <summary>
        /// Loads the Skills from the Character
        /// Called after the default skills / level have been set!
        /// </summary>
        protected virtual void LoadSkillsFromCharacter()
        {
            DOLCharacters character = DBCharacter; // if its derived and filled with some code
            if (character == null) return; // no character => exit

            #region load class spec

            // first load spec's career
            LoadClassSpecializations(false);

            //Load Remaining spec and levels from Database (custom spec can still be added here...)
            string tmpStr = character.SerializedSpecs;
            if (tmpStr != null && tmpStr.Length > 0)
            {
                foreach (string spec in Util.SplitCSV(tmpStr))
                {
                    string[] values = spec.Split('|');
                    if (values.Length >= 2)
                    {
                        Specialization tempSpec = SkillBase.GetSpecialization(values[0], false);

                        if (tempSpec != null)
                        {
                            if (tempSpec.AllowSave)
                            {
                                int level;
                                if (int.TryParse(values[1], out level))
                                {
                                    if (HasSpecialization(tempSpec.KeyName))
                                    {
                                        GetSpecializationByName(tempSpec.KeyName).Level = level;
                                    }
                                    else
                                    {
                                        tempSpec.Level = level;
                                        AddSpecialization(tempSpec, false);
                                    }
                                }
                                else if (log.IsErrorEnabled)
                                {
                                    log.ErrorFormat("{0} : error in loading specs => '{1}'", Name, tmpStr);
                                }
                            }
                        }
                        else if (log.IsErrorEnabled)
                        {
                            log.ErrorFormat("{0}: can't find spec '{1}'", Name, values[0]);
                        }
                    }
                }
            }

            // Add Serialized Abilities to keep Database Order
            // Custom Ability will be disabled as soon as they are not in any specs...
            tmpStr = character.SerializedAbilities;
            if (tmpStr != null && tmpStr.Length > 0 && m_usableSkills.Count == 0)
            {
                foreach (string abilities in Util.SplitCSV(tmpStr))
                {
                    string[] values = abilities.Split('|');
                    if (values.Length >= 2)
                    {
                        int level;
                        if (int.TryParse(values[1], out level))
                        {
                            Ability ability = SkillBase.GetAbility(values[0], level);
                            if (ability != null)
                            {
                                // this is for display order only
                                m_usableSkills.Add(new Tuple<Skill, Skill>(ability, ability));
                            }
                        }
                    }
                }
            }

            // Retrieve Realm Abilities From Database to be handled by Career Spec
            tmpStr = character.SerializedRealmAbilities;
            if (tmpStr != null && tmpStr.Length > 0)
            {
                foreach (string abilities in Util.SplitCSV(tmpStr))
                {
                    string[] values = abilities.Split('|');
                    if (values.Length >= 2)
                    {
                        int level;
                        if (int.TryParse(values[1], out level))
                        {
                            Ability ability = SkillBase.GetAbility(values[0], level);
                            if (ability != null && ability is RealmAbility)
                            {
                                // this enable realm abilities for Career Computing.
                                m_realmAbilities.Add((RealmAbility)ability);
                            }
                        }
                    }
                }
            }

            // Load dependent skills
            RefreshSpecDependantSkills(false);

            #endregion

            #region disable ability
            //Since we added all the abilities that this character has, let's now disable the disabled ones!
            tmpStr = character.DisabledAbilities;
            if (tmpStr != null && tmpStr.Length > 0)
            {
                foreach (string str in Util.SplitCSV(tmpStr))
                {
                    string[] values = str.Split('|');
                    if (values.Length >= 2)
                    {
                        string keyname = values[0];
                        int duration;
                        if (HasAbility(keyname) && int.TryParse(values[1], out duration))
                        {
                            DisableSkill(GetAbility(keyname), duration);
                        }
                        else if (log.IsErrorEnabled)
                        {
                            log.ErrorFormat("{0}: error in loading disabled abilities => '{1}'", Name, tmpStr);
                        }
                    }
                }
            }

            #endregion

            //Load the disabled spells
            tmpStr = character.DisabledSpells;
            if (!string.IsNullOrEmpty(tmpStr))
            {
                foreach (string str in Util.SplitCSV(tmpStr))
                {
                    string[] values = str.Split('|');
                    int spellid;
                    int duration;
                    if (values.Length >= 2 && int.TryParse(values[0], out spellid) && int.TryParse(values[1], out duration))
                    {
                        Spell sp = SkillBase.GetSpellByID(spellid);
                        // disable
                        if (sp != null)
                            DisableSkill(sp, duration);
                    }
                    else if (log.IsErrorEnabled)
                    {
                        log.ErrorFormat("{0}: error in loading disabled spells => '{1}'", Name, tmpStr);
                    }
                }
            }

            CharacterClass.OnLevelUp(this, Level); // load all skills from DB first to keep the order
            CharacterClass.OnRealmLevelUp(this);
        }

        /// <summary>
        /// Load this player Classes Specialization.
        /// </summary>
        public virtual void LoadClassSpecializations(bool sendMessages)
        {
            // Get this Attached Class Specialization from SkillBase.
            IDictionary<Specialization, int> careers = SkillBase.GetSpecializationCareer(CharacterClass.ID);

            // Remove All Trainable Specialization or "Career Spec" that aren't managed by This Data Career anymore
            var speclist = GetSpecList();
            var careerslist = careers.Keys.Select(k => k.KeyName.ToLower());
            foreach (var spec in speclist.Where(sp => sp.Trainable || !sp.AllowSave))
            {
                if (!careerslist.Contains(spec.KeyName.ToLower()))
                    RemoveSpecialization(spec.KeyName);
            }

            // sort ML Spec depending on ML Line
            byte mlindex = 0;
            foreach (KeyValuePair<Specialization, int> constraint in careers)
            {
                if (constraint.Key is IMasterLevelsSpecialization)
                {
                    if (mlindex != MLLine)
                    {
                        if (HasSpecialization(constraint.Key.KeyName))
                            RemoveSpecialization(constraint.Key.KeyName);

                        mlindex++;
                        continue;
                    }

                    mlindex++;

                    if (!MLGranted || MLLevel < 1)
                    {
                        continue;
                    }
                }

                // load if the spec doesn't exists
                if (Level >= constraint.Value)
                {
                    if (!HasSpecialization(constraint.Key.KeyName))
                        AddSpecialization(constraint.Key, sendMessages);
                }
                else
                {
                    if (HasSpecialization(constraint.Key.KeyName))
                        RemoveSpecialization(constraint.Key.KeyName);
                }
            }
        }

        /// <summary>
        /// Verify this player has the correct number of spec points for the players level
        /// </summary>
        public virtual int VerifySpecPoints()
        {
            // calc normal spec points for the level & classe
            int allpoints = -1;
            for (int i = 1; i <= Level; i++)
            {
                if (i <= 5) allpoints += i; //start levels
                if (i > 5) allpoints += CharacterClass.SpecPointsMultiplier * i / 10; //normal levels
                if (i > 40) allpoints += CharacterClass.SpecPointsMultiplier * (i - 1) / 20; //half levels
            }
            if (IsLevelSecondStage && Level != MaxLevel)
                allpoints += CharacterClass.SpecPointsMultiplier * Level / 20; // add current half level

            // calc spec points player have (autotrain is not anymore processed here - 1.87 livelike)
            int usedpoints = 0;
            foreach (Specialization spec in GetSpecList().Where(e => e.Trainable))
            {
                usedpoints += (spec.Level * (spec.Level + 1) - 2) / 2;
                usedpoints -= GetAutoTrainPoints(spec, 0);
            }

            allpoints -= usedpoints;

            // check if correct, if not respec. Not applicable to GMs
            if (allpoints < 0)
            {
                if (Client.Account.PrivLevel == 1)
                {
                    log.WarnFormat("Spec points total for player {0} incorrect: {1} instead of {2}.", Name, usedpoints, allpoints + usedpoints);
                    RespecAllLines();
                    return allpoints + usedpoints;
                }
            }

            return allpoints;
        }
        /// <summary>
        /// Update abilities 
        /// </summary>
        public virtual void UpdateAbilities()
        {
            foreach (var ab in m_usableSkills)
            {
                if (ab.Item1 is Ability && !HasAbility(ab.Item1.Name))
                    AddAbility(SkillBase.GetAbility(ab.Item1.Name), true);
            }
        }
        /// <summary>
        /// Loads this player from a character table slot
        /// </summary>
        /// <param name="obj">DOLCharacter</param>
        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);
            if (!(obj is DOLCharacters))
                return;
            m_dbCharacter = (DOLCharacters)obj;

            Wallet.InitializeFromDatabase();

            Model = (ushort)DBCharacter.CurrentModel;
            IsRenaissance = DBCharacter.IsRenaissance;
            OutlawTimeStamp = DBCharacter.OutlawTimeStamp;
            m_reputation = DBCharacter.Reputation;
            m_wanted = DBCharacter.IsWanted;

            //update previous version by setting timestamp
            if (Reputation < 0 && OutlawTimeStamp == 0)
            {
                OutlawTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            }

            m_customFaceAttributes[(int)eCharFacePart.EyeSize] = DBCharacter.EyeSize;
            m_customFaceAttributes[(int)eCharFacePart.LipSize] = DBCharacter.LipSize;
            m_customFaceAttributes[(int)eCharFacePart.EyeColor] = DBCharacter.EyeColor;
            m_customFaceAttributes[(int)eCharFacePart.HairColor] = DBCharacter.HairColor;
            m_customFaceAttributes[(int)eCharFacePart.FaceType] = DBCharacter.FaceType;
            m_customFaceAttributes[(int)eCharFacePart.HairStyle] = DBCharacter.HairStyle;
            m_customFaceAttributes[(int)eCharFacePart.MoodType] = DBCharacter.MoodType;

            #region guild handling
            //TODO: overwork guild handling (VaNaTiC)
            m_guildId = DBCharacter.GuildID;
            if (m_guildId != null)
                m_guild = GuildMgr.GetGuildByGuildID(m_guildId);
            else
                m_guild = null;

            if (m_guild != null)
            {
                foreach (DBRank rank in m_guild.Ranks)
                {
                    if (rank == null) continue;
                    if (rank.RankLevel == DBCharacter.GuildRank)
                    {
                        m_guildRank = rank;
                        break;
                    }
                }

                m_guildName = m_guild.Name;
                m_guild.AddOnlineMember(this);
            }
            #endregion

            #region setting world-init-position (delegate to PlayerCharacter dont make sense)
            Position = DBCharacter.GetPosition();

            //important, use CurrentRegion property
            //instead because it sets the Region too
            CurrentRegionID = (ushort)DBCharacter.Region;
            if (CurrentRegion == null || CurrentRegion.GetZone(Coordinate) == null)
            {
                log.WarnFormat("Invalid region/zone on char load ({0}): x={1} y={2} z={3} reg={4}; moving to bind point."
                               , DBCharacter.Name, Coordinate.X, Coordinate.Y, Coordinate.Z, DBCharacter.Region);
                Position = DBCharacter.GetBindPosition();
            }

            for (int i = 0; i < LastUniquePositions.Length; i++)
            {
                LastUniquePositions[i] = Position;
            }
            #endregion

            // stats first
            m_charStat[eStat.STR - eStat._First] = (short)DBCharacter.Strength;
            m_charStat[eStat.DEX - eStat._First] = (short)DBCharacter.Dexterity;
            m_charStat[eStat.CON - eStat._First] = (short)DBCharacter.Constitution;
            m_charStat[eStat.QUI - eStat._First] = (short)DBCharacter.Quickness;
            m_charStat[eStat.INT - eStat._First] = (short)DBCharacter.Intelligence;
            m_charStat[eStat.PIE - eStat._First] = (short)DBCharacter.Piety;
            m_charStat[eStat.EMP - eStat._First] = (short)DBCharacter.Empathy;
            m_charStat[eStat.CHR - eStat._First] = (short)DBCharacter.Charisma;

            SetCharacterClass(DBCharacter.Class);

            m_currentSpeed = 0;
            if (MaxSpeedBase == 0)
                MaxSpeedBase = PLAYER_BASE_SPEED;

            m_inventory.LoadFromDatabase(InternalID);

            SwitchQuiver((eActiveQuiverSlot)(DBCharacter.ActiveWeaponSlot & 0xF0), false);
            SwitchWeapon((eActiveWeaponSlot)(DBCharacter.ActiveWeaponSlot & 0x0F));


            if (DBCharacter.PlayedTime < 1) //added to make character start with 100% Health and Mana/Endurance when DB Start Lvl >1 :Loki
            {
                Health = MaxHealth;
                Mana = MaxMana;
                Endurance = MaxEndurance;
            }
            else
            {
                Health = DBCharacter.Health;
                Mana = DBCharacter.Mana;
                Endurance = DBCharacter.Endurance; // has to be set after max, same applies to other values with max properties
            }



            if (Health <= 0)
            {
                Health = 1;
            }

            if (RealmLevel == 0)
                RealmLevel = CalculateRealmLevelFromRPs(RealmPoints);


            //Need to load the skills at the end, so the stored values modify the
            //existing skill levels for this player
            LoadSkillsFromCharacter();
            UpdateAbilities();
            LoadCraftingSkills();

            VerifySpecPoints();

            LoadQuests();

            // Load Task object of player ...
            var tasks = DOLDB<DBTask>.SelectObjects(DB.Column(nameof(DBTask.Character_ID)).IsEqualTo(InternalID));
            if (tasks.Count == 1)
            {
                m_task = AbstractTask.LoadFromDatabase(this, tasks[0]);
            }
            else if (tasks.Count > 1)
            {
                if (log.IsErrorEnabled)
                    log.Error("More than one DBTask Object found for player " + Name);
            }

            // Load ML steps of player ...
            var mlsteps = DOLDB<DBCharacterXMasterLevel>.SelectObjects(DB.Column(nameof(DBCharacterXMasterLevel.Character_ID)).IsEqualTo(InternalID));
            if (mlsteps.Count > 0)
            {
                foreach (DBCharacterXMasterLevel mlstep in mlsteps)
                    m_mlSteps.Add(mlstep);
            }

            m_previousLoginDate = DBCharacter.LastPlayed;

            // Has to be updated on load to ensure time offline isn't added to character /played.
            DBCharacter.LastPlayed = DateTime.Now;

            TaskTitleFlags = (TaskTitle.eTitleFlags)DBCharacter.TaskTitleFlags;
            m_titles.Clear();
            foreach (IPlayerTitle ttl in PlayerTitleMgr.GetPlayerTitles(this))
                m_titles.Add(ttl);

            IPlayerTitle t = PlayerTitleMgr.GetTitleByTypeName(DBCharacter.CurrentTitleType);
            if (t == null)
                t = PlayerTitleMgr.ClearTitle;
            m_currentTitle = t;
            m_currentTitle.OnTitleSelect(this);

            //let's only check if we can use /level once shall we,
            //this is nice because i want to check the property often for the new catacombs classes

            //find all characters in the database
            foreach (DOLCharacters plr in Client.Account.Characters)
            {
                //where the level of one of the characters if 50
                if (plr.Level == ServerProperties.Properties.SLASH_LEVEL_REQUIREMENT && GameServer.ServerRules.CountsTowardsSlashLevel(plr))
                {
                    m_canUseSlashLevel = true;
                    break;
                }
            }

            // check the account for the Muted flag
            if (Client.Account.IsMuted)
                IsMuted = true;

            // Ensure TaskXPlayer data is loaded
            TaskXPlayer = TaskManager.EnsureTaskData(this);
        }

        /// <summary>
        /// Save the player into the database
        /// </summary>
        public override void SaveIntoDatabase()
        {
            try
            {
                // Ff this player is a GM always check and set the IgnoreStatistics flag
                if (Client.Account.PrivLevel > (uint)ePrivLevel.Player && DBCharacter.IgnoreStatistics == false)
                {
                    DBCharacter.IgnoreStatistics = true;
                }

                SaveSkillsToCharacter();
                SaveCraftingSkills();
                DBCharacter.PlayedTime = PlayedTime;  //We have to set the PlayedTime on the character before setting the LastPlayed
                DBCharacter.LastPlayed = DateTime.Now;
                DBCharacter.IsRenaissance = IsRenaissance;
                DBCharacter.Reputation = Reputation;
                DBCharacter.IsWanted = Wanted;
                DBCharacter.TaskTitleFlags = (ulong)TaskTitleFlags;

                DBCharacter.ActiveWeaponSlot = (byte)((byte)ActiveWeaponSlot | (byte)ActiveQuiverSlot);
                if (m_stuckFlag)
                {
                    lock (LastUniquePositions)
                    {
                        DBCharacter.SetPosition(LastUniquePositions[LastUniquePositions.Length - 1]);
                    }
                }
                GameServer.Database.SaveObject(DBCharacter);
                Inventory.SaveIntoDatabase(InternalID);
                var questsToSave = new List<PlayerQuest>();
                questsToSave.AddRange(QuestList);
                questsToSave.AddRange(QuestListFinished);
                foreach (var quest in questsToSave)
                    quest.SaveIntoDatabase();

                DOLCharacters cachedCharacter = null;

                foreach (DOLCharacters accountChar in Client.Account.Characters)
                {
                    if (accountChar.ObjectId == InternalID)
                    {
                        cachedCharacter = accountChar;
                        break;
                    }
                }

                if (cachedCharacter != null)
                {
                    cachedCharacter = DBCharacter;
                }


                if (m_mlSteps != null)
                    GameServer.Database.SaveObject(m_mlSteps.OfType<DBCharacterXMasterLevel>());

                if (log.IsInfoEnabled)
                    log.InfoFormat("{0} saved!", DBCharacter.Name);
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.SaveIntoDatabase.CharacterSaved"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                // Save TaskXPlayer data
                if (TaskXPlayer != null)
                {
                    GameServer.Database.SaveObject(TaskXPlayer);
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error saving player {0}! - {1}", Name, e);
            }
        }

        #endregion

        #region CustomDialog

        /// <summary>
        /// Holds the delegates that calls
        /// </summary>
        public CustomDialogResponse m_customDialogCallback;

        /// <summary>
        /// Gets/sets the custom dialog callback
        /// </summary>
        public CustomDialogResponse CustomDialogCallback
        {
            get { return m_customDialogCallback; }
            set { m_customDialogCallback = value; }
        }

        #endregion

        #region GetPronoun/GetExamineMessages

        /// <summary>
        /// Pronoun of this player in case you need to refer it in 3rd person
        /// http://webster.commnet.edu/grammar/cases.htm
        /// </summary>
        /// <param name="firstLetterUppercase"></param>
        /// <param name="form">0=Subjective, 1=Possessive, 2=Objective</param>
        /// <returns>pronoun of this object</returns>
        public override string GetPronoun(int form, bool firstLetterUppercase)
        {
            if (Gender == eGender.Male) // male
                switch (form)
                {
                    default:
                        // Subjective
                        if (firstLetterUppercase)
                            return "He";
                        else
                            return "he";
                    case 1:
                        // Possessive
                        if (firstLetterUppercase)
                            return "His";
                        else
                            return "his";
                    case 2:
                        // Objective
                        if (firstLetterUppercase)
                            return "Him";
                        else
                            return "him";
                }
            else
                // female
                switch (form)
                {
                    default:
                        // Subjective
                        if (firstLetterUppercase)
                            return "She";
                        else
                            return "she";
                    case 1:
                        // Possessive
                        if (firstLetterUppercase)
                            return "Her";
                        else
                            return "her";
                    case 2:
                        // Objective
                        if (firstLetterUppercase)
                            return "Her";
                        else
                            return "her";
                }
        }

        public string GetPronoun(GameClient Client, int form, bool capitalize)
        {
            if (Gender == eGender.Male)
                switch (form)
                {
                    default:
                        return Capitalize(capitalize, LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Pronoun.Male.Subjective"));
                    case 1:
                        return Capitalize(capitalize, LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Pronoun.Male.Possessive"));
                    case 2:
                        return Capitalize(capitalize, LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Pronoun.Male.Objective"));
                }
            else
                switch (form)
                {
                    default:
                        return Capitalize(capitalize, LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Pronoun.Female.Subjective"));
                    case 1:
                        return Capitalize(capitalize, LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Pronoun.Female.Possessive"));
                    case 2:
                        return Capitalize(capitalize, LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Pronoun.Female.Objective"));
                }
        }

        public string GetName(GamePlayer target)
        {
            return GameServer.ServerRules.GetPlayerName(this, target);
        }

        /// <summary>
        /// Adds messages to ArrayList which are sent when object is targeted
        /// </summary>
        /// <param name="player">GamePlayer that is examining this object</param>
        /// <returns>list with string messages</returns>
        public override IList GetExamineMessages(GamePlayer player)
        {
            // TODO: PvP & PvE messages
            IList list = base.GetExamineMessages(player);

            string message = "";
            switch (GameServer.Instance.Configuration.ServerType)
            {//FIXME: Better extract this to a new function in ServerRules !!! (VaNaTiC)
                case eGameServerType.GST_Normal:
                    {
                        if (Realm == player.Realm || Client.Account.PrivLevel > 1 || player.Client.Account.PrivLevel > 1)
                            message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.GetExamineMessages.RealmMember", player.GetPersonalizedName(this), GetPronoun(Client, 0, true), CharacterClass.Name);
                        else
                            message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.GetExamineMessages.EnemyRealmMember", player.GetPersonalizedName(this), GetPronoun(Client, 0, true));
                        break;
                    }

                case eGameServerType.GST_PvP:
                    {
                        if (Client.Account.PrivLevel > 1 || player.Client.Account.PrivLevel > 1)
                            message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.GetExamineMessages.YourGuildMember", player.GetPersonalizedName(this), GetPronoun(Client, 0, true), CharacterClass.Name);
                        else if (Guild == null)
                            message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.GetExamineMessages.NeutralMember", player.GetPersonalizedName(this), GetPronoun(Client, 0, true));
                        else if (Guild == player.Guild || Client.Account.PrivLevel > 1 || player.Client.Account.PrivLevel > 1)
                            message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.GetExamineMessages.YourGuildMember", player.GetPersonalizedName(this), GetPronoun(Client, 0, true), CharacterClass.Name);
                        else
                            message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.GetExamineMessages.OtherGuildMember", player.GetPersonalizedName(this), GetPronoun(Client, 0, true), GuildName);
                        break;
                    }

                default:
                    {
                        message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.GetExamineMessages.YouExamine", player.GetPersonalizedName(this));
                        break;
                    }
            }

            list.Add(message);
            return list;
        }

        public override void SendSystemMessage(string message)
            => Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_SystemWindow);

        public override void SendMessage(string message, eChatType chatType = eChatType.CT_System, eChatLoc chatLocation = eChatLoc.CL_SystemWindow)
            => Out.SendMessage(message, chatType, chatLocation);

        public override void SendTranslatedMessage(string key, eChatType chatType = eChatType.CT_System, eChatLoc chatLocation = eChatLoc.CL_SystemWindow, params object[] args)
            => Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, key, args), chatType, chatLocation);
        #endregion

        #region Stealth / Wireframe

        bool m_isWireframe = false;

        /// <summary>
        /// Player is drawn as a Wireframe.  Not sure why or when this is used.  -- Tolakram
        /// </summary>
        public bool IsWireframe
        {
            get { return m_isWireframe; }
            set
            {
                bool needUpdate = m_isWireframe != value;
                m_isWireframe = value;
                if (needUpdate && ObjectState == eObjectState.Active)
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (player == null) continue;
                        player.Out.SendPlayerModelTypeChange(this, (byte)(value ? 1 : 0));
                    }
            }
        }

        private bool m_isTorchLighted = false;

        /// <summary>
        /// Is player Torch lighted ?
        /// </summary>
        public bool IsTorchLighted
        {
            get { return m_isTorchLighted; }
            set { m_isTorchLighted = value; }
        }

        /// <summary>
        /// Property that holds tick when stealth state was changed last time
        /// </summary>
        public const string STEALTH_CHANGE_TICK = "StealthChangeTick";
        /// <summary>
        /// Holds the stealth effect
        /// </summary>
        protected StealthEffect m_stealthEffect = null;
        /// <summary>
        /// The stealth state of this player
        /// </summary>
        public override bool IsStealthed
        {
            get { return m_stealthEffect != null; }
        }
        public static void Unstealth(DOLEvent ev, object sender, EventArgs args)
        {
            AttackedByEnemyEventArgs atkArgs = args as AttackedByEnemyEventArgs;
            GamePlayer player = sender as GamePlayer;
            if (player == null || atkArgs == null) return;
            if (atkArgs.AttackData.AttackResult != eAttackResult.HitUnstyled && atkArgs.AttackData.AttackResult != eAttackResult.HitStyle) return;
            if (atkArgs.AttackData.Damage == -1) return;

            player.Stealth(false);
        }
        /// <summary>
        /// Set player's stealth state
        /// </summary>
        /// <param name="goStealth">true is stealthing, false if unstealthing</param>
        public override void Stealth(bool goStealth)
        {
            if (IsStealthed == goStealth)
                return;

            if (goStealth && IsCrafting)
            {
                Out.SendMessage("You can't stealth while crafting!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (IsOnHorse || IsSummoningMount)
                IsOnHorse = false;

            UncoverStealthAction action = (UncoverStealthAction)TempProperties.getProperty<object>(UNCOVER_STEALTH_ACTION_PROP, null);
            if (goStealth)
            {
                //start the uncover timer
                if (action == null)
                    action = new UncoverStealthAction(this);
                action.Interval = 2000;
                action.Start(2000);
                TempProperties.setProperty(UNCOVER_STEALTH_ACTION_PROP, action);

                if (ObjectState == eObjectState.Active)
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Stealth.NowHidden"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Out.SendPlayerModelTypeChange(this, 3);
                m_stealthEffect = new StealthEffect();
                m_stealthEffect.Start(this);
                Sprint(false);
                GameEventMgr.AddHandler(this, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(Unstealth));
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player == null) continue;
                    if (player == this) continue;
                    if (!player.CanDetect(this))
                        player.Out.SendObjectDelete(this);
                }
            }
            else
            {
                //stop the uncover timer
                if (action != null)
                {
                    action.Stop();
                    TempProperties.removeProperty(UNCOVER_STEALTH_ACTION_PROP);
                }

                if (ObjectState == eObjectState.Active)
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Stealth.NoLongerHidden"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                CamouflageEffect cam = EffectList.GetOfType<CamouflageEffect>();
                if (cam != null)
                {
                    cam.Stop();
                }
                //Andraste
                try
                {
                    GameSpellEffect effect = SpellHandler.FindEffectOnTarget(this, "BlanketOfCamouflage");
                    if (effect != null) effect.Cancel(false);
                }
                catch (Exception) { }

                Out.SendPlayerModelTypeChange(this, 2);
                if (m_stealthEffect != null) m_stealthEffect.Stop();
                m_stealthEffect = null;
                GameEventMgr.RemoveHandler(this, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(Unstealth));
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player == null) continue;
                    //TODO: more correct way to do it
                    if (player == this) continue;
                    //if a player could see us stealthed, we just update our model to avoid untargetting.
                    if (player.CanDetect(this))
                        player.Out.SendPlayerModelTypeChange(this, 2);
                    else
                        player.Out.SendPlayerCreate(this);
                    player.Out.SendLivingEquipmentUpdate(this);
                }
            }
            Notify(GamePlayerEvent.StealthStateChanged, this, null);
            Out.SendUpdateMaxSpeed();
        }

        /// <summary>
        /// The temp property that stores the uncover stealth action
        /// </summary>
        protected const string UNCOVER_STEALTH_ACTION_PROP = "UncoverStealthAction";

        /// <summary>
        /// Uncovers the player if a mob is too close
        /// </summary>
        protected class UncoverStealthAction : RegionAction
        {
            /// <summary>
            /// Constructs a new uncover stealth action
            /// </summary>
            /// <param name="actionSource">The action source</param>
            public UncoverStealthAction(GamePlayer actionSource)
                : base(actionSource)
            {
            }

            /// <summary>
            /// Called on every timer tick
            /// </summary>
            public override void OnTick()
            {
                GamePlayer player = (GamePlayer)m_actionSource;
                if (player.Client.Account.PrivLevel > 1) return;

                bool checklos = false;
                foreach (AbstractArea area in player.CurrentAreas)
                {
                    if (area.CheckLOS)
                    {
                        checklos = true;
                        break;
                    }
                }

                foreach (GameNPC npc in player.GetNPCsInRadius(1024))
                {
                    // Friendly mobs do not uncover stealthed players
                    if (!GameServer.ServerRules.IsAllowedToAttack(npc, player, true)) continue;

                    // Npc with player owner don't uncover
                    if (npc.GetPlayerOwner() != null) continue;

                    double npcLevel = Math.Max(npc.Level, 1.0);
                    double stealthLevel = player.GetModifiedSpecLevel(Specs.Stealth);
                    double detectRadius = 125.0 + ((npcLevel - stealthLevel) * 20.0);

                    // we have detect hidden and enemy don't = higher range
                    if (npc.HasAbility(Abilities.DetectHidden) && player.EffectList.GetOfType<CamouflageEffect>() == null)
                    {
                        detectRadius += 125;
                    }

                    if (detectRadius < 126) detectRadius = 126;

                    double distanceToPlayer = npc.GetDistanceTo(player);

                    if (distanceToPlayer > detectRadius)
                        continue;

                    double fieldOfView = 90.0;  //90 degrees  = standard FOV
                    double fieldOfListen = 120.0; //120 degrees = standard field of listening
                    if (npc.Level > 50)
                    {
                        fieldOfListen += (npc.Level - player.Level) * 3;
                    }

                    var angle = npc.GetAngleTo(player.Coordinate);

                    //player in front
                    fieldOfView /= 2.0;
                    bool canSeePlayer = (angle.InDegrees >= 360 - fieldOfView || angle.InDegrees < fieldOfView);

                    //If npc can not see nor hear the player, continue the loop
                    fieldOfListen /= 2.0;
                    if (canSeePlayer == false &&
                        !(angle.InDegrees >= (45 + 60) - fieldOfListen && angle.InDegrees < (45 + 60) + fieldOfListen) &&
                        !(angle.InDegrees >= (360 - 45 - 60) - fieldOfListen && angle.InDegrees < (360 - 45 - 60) + fieldOfListen))
                        continue;

                    double chanceMod = 1.0;

                    //Chance to detect player decreases after 125 coordinates!
                    if (distanceToPlayer > 125)
                        chanceMod = 1f - (distanceToPlayer - 125.0) / (detectRadius - 125.0);

                    double chanceToUncover = 0.1 + (npc.Level - stealthLevel) * 0.01 * chanceMod;
                    if (chanceToUncover < 0.01) chanceToUncover = 0.01;

                    if (Util.ChanceDouble(chanceToUncover))
                    {
                        if (canSeePlayer)
                        {
                            if (checklos)
                            {
                                player.Out.SendCheckLOS(player, npc, new CheckLOSResponse(player.UncoverLOSHandler));
                            }
                            else
                            {
                                player.Out.SendMessage(npc.GetName(0, true) + " uncovers you!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                player.Stealth(false);
                                break;
                            }
                        }
                        else
                        {
                            npc.TurnTo(player, 10000);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// This handler is called by the unstealth check of mobs
        /// </summary>
        public void UncoverLOSHandler(GamePlayer player, ushort response, ushort targetOID)
        {
            GameObject target = CurrentRegion.GetObject(targetOID);

            if ((target == null) || (player.IsStealthed == false)) return;

            if ((response & 0x100) == 0x100)
            {
                player.Out.SendMessage(player.GetPersonalizedName(target) + " uncovers you!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                player.Stealth(false);
            }
        }

        /// <summary>
        /// Checks whether this player can detect stealthed enemy
        /// </summary>
        /// <param name="enemy"></param>
        /// <returns>true if enemy can be detected</returns>
        public virtual bool CanDetect(GamePlayer enemy)
        {
            if (enemy.CurrentRegionID != CurrentRegionID)
                return false;
            if (!IsAlive)
                return false;
            if (enemy.EffectList.GetOfType<VanishEffect>() != null)
                return false;
            if (this.Client.Account.PrivLevel > 1)
                return true;
            if (enemy.Client.Account.PrivLevel > 1)
                return false;

            //everyone can see your own group stealthed
            if (enemy.Group != null && Group != null && enemy.Group == Group)
            {
                return this.IsWithinRadius(enemy, 2500);
            }

            /*
             * http://www.critshot.com/forums/showthread.php?threadid=3142
             * The person doing the looking has a chance to find them based on their level, minus the stealthed person's stealth spec.
             *
             * -Normal detection range = (enemy lvl  your stealth spec) * 20 + 125
             * -Detect Hidden Range = (enemy lvl  your stealth spec) * 50 + 250
             * -See Hidden range = 2700 - (38 * your stealth spec)
             */

            int EnemyStealthLevel = enemy.GetModifiedSpecLevel(Specs.Stealth);
            if (EnemyStealthLevel > 50)
                EnemyStealthLevel = 50;
            int levelDiff = this.Level - EnemyStealthLevel;
            if (levelDiff < 0) levelDiff = 0;

            int range;
            bool enemyHasCamouflage = enemy.EffectList.GetOfType<CamouflageEffect>() != null;
            if (HasAbility(Abilities.DetectHidden) && !enemy.HasAbility(Abilities.DetectHidden) && !enemyHasCamouflage)
            {
                // we have detect hidden and enemy don't = higher range
                range = levelDiff * 50 + 250; // Detect Hidden advantage
            }
            else
            {
                range = levelDiff * 20 + 125; // Normal detection range
            }

            // Mastery of Stealth Bonus
            RAPropertyEnhancer mos = GetAbility<MasteryOfStealthAbility>();
            if (mos != null && !enemyHasCamouflage)
                if (!HasAbility(Abilities.DetectHidden) || !enemy.HasAbility(Abilities.DetectHidden))
                    range += mos.GetAmountForLevel(CalculateSkillLevel(mos));

            range += BaseBuffBonusCategory[(int)eProperty.Skill_Stealth];

            //Buff (Stealth Detection)
            //Increases the target's ability to detect stealthed players and monsters.
            GameSpellEffect iVampiirEffect = SpellHandler.FindEffectOnTarget((GameLiving)this, "VampiirStealthDetection");
            if (iVampiirEffect != null)
                range += (int)iVampiirEffect.Spell.Value;

            //Infill Only - Greater Chance to Detect Stealthed Enemies for 1 minute
            //after executing a klling blow on a realm opponent.
            GameSpellEffect HeightenedAwareness = SpellHandler.FindEffectOnTarget((GameLiving)this, "HeightenedAwareness");
            if (HeightenedAwareness != null)
                range += (int)HeightenedAwareness.Spell.Value;

            //Nightshade Only - Greater chance of remaining hidden while stealthed for 1 minute
            //after executing a killing blow on a realm opponent.
            GameSpellEffect SubtleKills = SpellHandler.FindEffectOnTarget((GameLiving)enemy, "SubtleKills");
            if (SubtleKills != null)
            {
                range -= (int)SubtleKills.Spell.Value;
                if (range < 0) range = 0;
            }

            // Apply Blanket of camouflage effect
            GameSpellEffect iSpymasterEffect1 = SpellHandler.FindEffectOnTarget((GameLiving)enemy, "BlanketOfCamouflage");
            if (iSpymasterEffect1 != null)
            {
                range -= (int)iSpymasterEffect1.Spell.Value;
                if (range < 0) range = 0;
            }

            // Apply Lookout effect
            GameSpellEffect iSpymasterEffect2 = SpellHandler.FindEffectOnTarget((GameLiving)this, "Lookout");
            if (iSpymasterEffect2 != null)
                range += (int)iSpymasterEffect2.Spell.Value;

            // Apply Prescience node effect
            GameSpellEffect iConvokerEffect = SpellHandler.FindEffectOnTarget((GameLiving)enemy, "Prescience");
            if (iConvokerEffect != null)
                range += (int)iConvokerEffect.Spell.Value;

            int modifier = GetModified(eProperty.StealthDetectionBonus) - enemy.GetModified(eProperty.StealthEffectivenessBonus);
            if (modifier != 0)
            {
                range = (int)(range * (modifier / 100.0f));
            }

            //Hard cap is 1900
            if (range > 1900)
                range = 1900;

            // Fin
            // vampiir stealth range, uncomment when add eproperty stealthrange i suppose
            return this.IsWithinRadius(enemy, range);
        }

        #endregion

        #region Task

        /// <summary>
        /// Holding tasks of player
        /// </summary>
        AbstractTask m_task = null;

        /// <summary>
        /// Gets the tasklist of this player
        /// </summary>
        public AbstractTask Task
        {
            get { return m_task; }
            set { m_task = value; }
        }

        #endregion

        #region Mission

        private AbstractMission m_mission = null;

        /// <summary>
        /// Gets the personal mission
        /// </summary>
        public AbstractMission Mission
        {
            get { return m_mission; }
            set
            {
                m_mission = value;
                this.Out.SendQuestListUpdate();
                if (value != null) Out.SendMessage(m_mission.Description, eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        #endregion

        #region Quest
        /// <summary>
        /// Load all the ongoing or completed quests for this player
        /// </summary>
        public virtual void LoadQuests()
        {
            m_questList.Clear();
            m_questListFinished.Clear();

            // Scripted quests
            var quests = DOLDB<DBQuest>.SelectObjects(DB.Column(nameof(DBQuest.Character_ID)).IsEqualTo(InternalID));
            foreach (DBQuest dbquest in quests)
            {
                var quest = new PlayerQuest(this, dbquest);
                if (quest.Quest?.Name != "ERROR")
                {
                    lock (m_questList)
                        if (quest.Status == eQuestStatus.Done)
                            m_questListFinished.Add(quest);
                        else
                            m_questList.Add(quest);
                }
            }
        }

        /// <summary>
        /// Holds all the quests currently active on this player
        /// </summary>
        protected readonly List<PlayerQuest> m_questList = new();

        /// <summary>
        /// Holds all already finished quests off this player
        /// </summary>
        protected readonly List<PlayerQuest> m_questListFinished = new();

        protected RegionTimer m_questActionTimer = null;

        public RegionTimer QuestActionTimer
        {
            get { return m_questActionTimer; }
            set { m_questActionTimer = value; }
        }

        /// <summary>
        /// Gets the questlist of this player
        /// </summary>
        public IReadOnlyList<PlayerQuest> QuestList
        {
            get
            {
                lock (m_questList)
                    return m_questList.ToImmutableList();
            }
        }

        /// <summary>
        /// Gets the finished quests of this player
        /// </summary>
        public IReadOnlyList<PlayerQuest> QuestListFinished
        {
            get
            {
                lock(m_questListFinished)
                    return m_questListFinished.ToImmutableList();
            }
        }

        /// <summary>
        /// Add a quest to the players finished list
        /// </summary>
        /// <param name="quest"></param>
        public void AddFinishedQuest(PlayerQuest quest)
        {
            lock (m_questListFinished)
            {
                m_questListFinished.Add(quest);
            }
            lock (m_questList)
            {
                m_questList.Remove(quest);
            }
        }

        /// <summary>
        /// Adds a quest to the players questlist
        /// Can be used by both scripted quests and data quests
        /// </summary>
        /// <param name="quest">The quest to add</param>
        /// <returns>true if added, false if player is already doing the quest!</returns>
        public bool AddQuest(PlayerQuest quest)
        {
            lock (m_questList)
            {
                if (IsDoingQuest(quest.Quest) != null)
                    return false;

                m_questList.Add(quest);
                quest.Quest.OnQuestAssigned(this);
            }
            Out.SendQuestUpdate(quest);
            return true;
        }

        /// <summary>
        /// Taken from Breamor
        /// https://github.com/jeremv42/DOLSharp/commit/7e62188960a20ead501661e2702935e7f2db0762#diff-e628a30c076896a273c9bbd15e30726eec02461ac6d6de285e30fe5e961e51aaR13298
        /// </summary>
        /// <param name="quest"></param>
        /// <returns></returns>
        public bool RemoveQuest(PlayerQuest quest)
        {
            lock (m_questList)
                return m_questList.Remove(quest);
            Out.SendQuestListUpdate();
        }

        /// <summary>
        /// Taken from Breamor
        /// https://github.com/jeremv42/DOLSharp/commit/7e62188960a20ead501661e2702935e7f2db0762#diff-e628a30c076896a273c9bbd15e30726eec02461ac6d6de285e30fe5e961e51aaR13298
        /// </summary>
        /// <param name="quest"></param>
        /// <returns></returns>
        public bool RemoveQuestFinished(PlayerQuest quest)
        {
            lock (m_questListFinished)
                return m_questListFinished.Remove(quest);
        }

        /// <summary>
        /// Checks if a player has done a specific quest type
        /// This is used for scripted quests
        /// </summary>
        /// <param name="quest">The quest type</param>
        /// <returns>the number of times the player did this quest</returns>
        public int HasFinishedQuest(DataQuestJson quest)
        {
            lock (m_questListFinished)
                return m_questListFinished.Count(q => q.Quest == quest);
        }

        /// <summary>
        /// Checks if this player is currently doing the specified quest
        /// Can be used by scripted and data quests
        /// </summary>
        /// <param name="questType">The quest type</param>
        /// <returns>the quest if player is doing the quest or null if not</returns>
        public PlayerQuest IsDoingQuest(DataQuestJson quest)
        {
            lock (m_questList)
                return m_questList.Find(q => q.Quest == quest);
        }
        #endregion

        #region Notify
        public override void Notify(DOLEvent e, object sender, EventArgs args)
        {
            CharacterClass.Notify(e, sender, args);
            base.Notify(e, sender, args);

            // player forwards every single notify message to all active quests
            foreach (var q in QuestList)
                q.Notify(e, sender, args);

            if (Task != null)
                Task.Notify(e, sender, args);

            if (Mission != null)
                Mission.Notify(e, sender, args);

            if (Group != null && Group.Mission != null)
                Group.Mission.Notify(e, sender, args);

            //Realm mission will be handled on the capture event args
        }

        public override void Notify(DOLEvent e, object sender)
        {
            Notify(e, sender, null);
        }

        public override void Notify(DOLEvent e)
        {
            Notify(e, null, null);
        }

        public override void Notify(DOLEvent e, EventArgs args)
        {
            Notify(e, null, args);
        }
        #endregion

        #region Crafting

        public readonly Object CraftingLock = new Object();

        /// <summary>
        /// Store all player crafting skill and their value (eCraftingSkill => Value)
        /// </summary>
        protected Dictionary<eCraftingSkill, int> m_craftingSkills = new Dictionary<eCraftingSkill, int>();

        /// <summary>
        /// Store the player primary crafting skill
        /// </summary>
        protected eCraftingSkill m_craftingPrimarySkill = 0;

        /// <summary>
        /// Get all player crafting skill and their value
        /// </summary>
        public Dictionary<eCraftingSkill, int> CraftingSkills
        {
            get { return m_craftingSkills; }
        }

        /// <summary>
        /// Store the player primary crafting skill
        /// </summary>
        public eCraftingSkill CraftingPrimarySkill
        {
            get { return m_craftingPrimarySkill; }
            set { m_craftingPrimarySkill = value; }
        }

        /// <summary>
        /// Get the specified player crafting skill value
        /// </summary>
        /// <param name="skill">The crafting skill to get value</param>
        /// <returns>the level in the specified crafting if valid and -1 if not</returns>
        public virtual int GetCraftingSkillValue(eCraftingSkill skill)
        {
            lock (CraftingLock)
            {
                if (!m_craftingSkills.ContainsKey(skill)) return -1;
                return m_craftingSkills[skill];
            }
        }

        public virtual double CraftingSkillGain
        {
            get
            {
                double gainMultiplier = 1.0;

                gainMultiplier += BuffBonusCategory4[eProperty.CraftingSkillGain] * 0.01;
                gainMultiplier += ItemBonus[(int)eProperty.CraftingSkillGain] * 0.01;

                return gainMultiplier;
            }
        }

        /// <summary>
        /// Increase the specified player crafting skill
        /// </summary>
        /// <param name="skill">Crafting skill to increase</param>
        /// <param name="count">How much increase or decrase</param>
        /// <returns>true if the skill is valid and -1 if not</returns>
        public virtual bool GainCraftingSkill(eCraftingSkill skill, int count, bool fromMaster = false)
        {
            if (skill == eCraftingSkill.NoCrafting) return false;

            lock (CraftingLock)
            {
                AbstractCraftingSkill craftingSkill = CraftingMgr.getSkillbyEnum(skill);
                count = CalculGainCraftingSkill(skill, count, fromMaster);
                if (craftingSkill != null && count > 0)
                {
                    count = (int)(count * CraftingSkillGain);
                    m_craftingSkills[skill] = count + m_craftingSkills[skill];
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainCraftingSkill.GainSkill", craftingSkill.Name, m_craftingSkills[skill]), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    int currentSkillLevel = GetCraftingSkillValue(skill);
                    if (HasPlayerReachedNewCraftingTitle(currentSkillLevel))
                    {
                        GameEventMgr.Notify(GamePlayerEvent.NextCraftingTierReached, this, new NextCraftingTierReachedEventArgs(skill, currentSkillLevel));
                    }
                    if (CanGenerateNews && currentSkillLevel >= 1000 && currentSkillLevel - count < 1000)
                    {
                        string message = string.Format(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.GainCraftingSkill.ReachedSkill", Name, craftingSkill.Name));
                        NewsMgr.CreateNews(message, Realm, eNewsType.PvE, true);
                    }
                }
                return true;
            }
        }

        private int CalculGainCraftingSkill(eCraftingSkill skill, int count, bool fromMaster)
        {
            if (fromMaster)
                return 1;
            if (skill != CraftingPrimarySkill && (m_craftingSkills[skill] + count) > m_craftingSkills[CraftingPrimarySkill])
                count = m_craftingSkills[CraftingPrimarySkill] - m_craftingSkills[skill];
            if (CraftingPrimarySkill == eCraftingSkill.BasicCrafting && skill != eCraftingSkill.BasicCrafting && (m_craftingSkills[skill] + count) > 99)
                return 99 - m_craftingSkills[skill];
            if (skill != CraftingPrimarySkill)
            {
                if (CraftingPrimarySkill == eCraftingSkill.WeaponCrafting && new List<eCraftingSkill>(new eCraftingSkill[] { eCraftingSkill.ArmorCrafting, eCraftingSkill.Tailoring, eCraftingSkill.Alchemy, eCraftingSkill.Fletching, eCraftingSkill.SpellCrafting }).Contains(skill) && (m_craftingSkills[skill] + count) > 1050)
                    return 1050 - m_craftingSkills[skill];
                if (CraftingPrimarySkill == eCraftingSkill.ArmorCrafting && new List<eCraftingSkill>(new eCraftingSkill[] { eCraftingSkill.WeaponCrafting, eCraftingSkill.Tailoring, eCraftingSkill.Alchemy, eCraftingSkill.Fletching, eCraftingSkill.SpellCrafting }).Contains(skill) && (m_craftingSkills[skill] + count) > 1050)
                    return 1050 - m_craftingSkills[skill];
                if (CraftingPrimarySkill == eCraftingSkill.Tailoring && new List<eCraftingSkill>(new eCraftingSkill[] { eCraftingSkill.WeaponCrafting, eCraftingSkill.ArmorCrafting, eCraftingSkill.Alchemy, eCraftingSkill.Fletching, eCraftingSkill.SpellCrafting }).Contains(skill) && (m_craftingSkills[skill] + count) > 1050)
                    return 1050 - m_craftingSkills[skill];
                if (CraftingPrimarySkill == eCraftingSkill.Fletching && new List<eCraftingSkill>(new eCraftingSkill[] { eCraftingSkill.WeaponCrafting, eCraftingSkill.Tailoring, eCraftingSkill.Alchemy, eCraftingSkill.ArmorCrafting, eCraftingSkill.SpellCrafting }).Contains(skill) && (m_craftingSkills[skill] + count) > 1050)
                    return 1050 - m_craftingSkills[skill];
                if (CraftingPrimarySkill == eCraftingSkill.Alchemy && new List<eCraftingSkill>(new eCraftingSkill[] { eCraftingSkill.WeaponCrafting, eCraftingSkill.Tailoring, eCraftingSkill.ArmorCrafting, eCraftingSkill.Fletching, eCraftingSkill.SpellCrafting }).Contains(skill) && (m_craftingSkills[skill] + count) > 1050)
                    return 1050 - m_craftingSkills[skill];
                if (CraftingPrimarySkill == eCraftingSkill.SpellCrafting && new List<eCraftingSkill>(new eCraftingSkill[] { eCraftingSkill.WeaponCrafting, eCraftingSkill.Tailoring, eCraftingSkill.Alchemy, eCraftingSkill.Fletching, eCraftingSkill.ArmorCrafting }).Contains(skill) && (m_craftingSkills[skill] + count) > 1050)
                    return 1050 - m_craftingSkills[skill];
                return count;
            }
            else if (m_craftingSkills[CraftingPrimarySkill] > 1099)
                return count;
            else
            {
                int rest = m_craftingSkills[CraftingPrimarySkill] % 100;
                if (rest + count > 99)
                    return 99 - rest;
                else
                    return count;
            }
        }

        protected bool m_isEligibleToGiveMeritPoints = true;

        /// <summary>
        /// Can actions done by this player reward merit points to the players guild?
        /// </summary>
        public virtual bool IsEligibleToGiveMeritPoints
        {
            get
            {
                if (Guild == null || Guild.GuildType == Guild.eGuildType.ServerGuild || m_isEligibleToGiveMeritPoints == false || Client.Account.PrivLevel > 1)
                    return false;

                return true;
            }

            set { m_isEligibleToGiveMeritPoints = value; }
        }

        /// <summary>
        /// Gain Merit Points for the guild
        /// </summary>
        public void GainGuildMeritPoints(int amount, bool quiet = false)
        {
            if (!IsEligibleToGiveMeritPoints)
            {
                return;
            }

            Guild.GainMeritPoints(amount);
            if (RealGuild != null)
            {
                RealGuild.GainMeritPoints(amount);
            }
            if (!quiet)
            {
                Out.SendMessage("You have earned " + amount + " merit points for your guild!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        protected bool m_canGenerateNews = true;

        /// <summary>
        /// Can this player generate news items?
        /// </summary>
        public virtual bool CanGenerateNews
        {
            get { return m_canGenerateNews && GameServer.ServerRules.CanGenerateNews(this); }
            set { m_canGenerateNews = value; }
        }

        /// <summary>
        /// Get the crafting speed multiplier for this player
        /// This might be modified by region or equipment
        /// </summary>
        public virtual double CraftingSpeed
        {
            get
            {
                double speed = Properties.CRAFTING_SPEED;

                if (speed <= 0)
                    speed = 1.0;

                speed *= (1.0 + BuffBonusCategory4[eProperty.CraftingSpeed] * .01);
                speed *= (1.0 + ItemBonus[(int)eProperty.CraftingSpeed] * .01);

                if (Guild != null && Guild.BonusType == Guild.eBonusType.CraftingHaste)
                {
                    double guildCraftBonus = Properties.GUILD_BUFF_CRAFTING;
                    int guildLevel = (int)Guild.GuildLevel;

                    if (guildLevel >= 8 && guildLevel <= 15)
                    {
                        guildCraftBonus *= 1.5;
                    }
                    else if (guildLevel > 15)
                    {
                        guildCraftBonus *= 2.0;
                    }

                    speed *= (1.0 + guildCraftBonus * .01);
                }

                if (CurrentRegion.IsCapitalCity && Properties.CAPITAL_CITY_CRAFTING_SPEED_BONUS > 0)
                {
                    return speed * Properties.CAPITAL_CITY_CRAFTING_SPEED_BONUS;
                }

                return speed;
            }
        }

        /// <summary>
        /// Get the crafting skill bonus for this player.
        /// This might be modified by region or equipment
        /// Values represents a percent; 0 - 100
        /// </summary>
        public virtual int CraftingSkillBonus
        {
            get
            {
                if (CurrentRegion.IsCapitalCity)
                {
                    return Properties.CAPITAL_CITY_CRAFTING_SKILL_GAIN_BONUS;
                }

                return 0;
            }
        }

        protected virtual bool HasPlayerReachedNewCraftingTitle(int skillLevel)
        {
            // no titles after 1000 any more, checked in 1.97
            if (skillLevel <= 1100)
            {
                if (skillLevel % 100 == 99)
                {
                    return true;
                }
            }
            return false;
        }


        /// <summary>
        /// Add a new crafting skill to the player
        /// </summary>
        /// <param name="skill"></param>
        /// <param name="startValue"></param>
        /// <returns></returns>
        public virtual bool AddCraftingSkill(eCraftingSkill skill, int startValue)
        {
            if (skill == eCraftingSkill.NoCrafting) return false;

            if (CraftingPrimarySkill == eCraftingSkill.NoCrafting)
                CraftingPrimarySkill = eCraftingSkill.BasicCrafting;

            lock (CraftingLock)
            {
                // fix error when the player already contains the skill
                if (!m_craftingSkills.ContainsKey(skill))
                {
                    AbstractCraftingSkill craftingSkill = CraftingMgr.getSkillbyEnum(skill);
                    if (craftingSkill != null)
                    {
                        m_craftingSkills.Add(skill, startValue);
                        Out.SendMessage("You gain skill in " + craftingSkill.Name + "! (" + startValue + ").", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// This is the timer used to count time when a player uses a special crafting skill, for example repairing or salvaging
        /// </summary>
        private RegionTimer m_specialCraftTimer;

        /// <summary>
        /// Get and set the craft timer
        /// </summary>
        public RegionTimer CraftTimer
        {
            get { return m_specialCraftTimer; }
            set { m_specialCraftTimer = value; }
        }

        /// <summary>
        /// Get and set the craft action
        /// </summary>
        public CraftAction CurrentCraft
        {
            get;
            set;
        }

        /// <summary>
        /// Is the player crafting
        /// </summary>
        public bool IsCrafting
        {
            get { return ((CraftTimer != null && CraftTimer.IsAlive) || (CurrentCraft != null && CurrentCraft.IsAlive)); }
        }

        /// <summary>
        /// Stop crafting.
        /// </summary>
        public void StopCrafting()
        {
            if (CurrentCraft != null)
            {
                CurrentCraft.Stop();
                CurrentCraft = null;
            }

            if (CraftTimer != null)
            {
                if (CraftTimer.IsAlive)
                {
                    CraftTimer.Stop();
                    Out.SendCloseTimerWindow();
                }
                CraftTimer = null;
            }
        }

        /// <summary>
        /// Get the craft title string of the player
        /// </summary>
        public virtual IPlayerTitle CraftTitle
        {
            get
            {
                var title = m_titles.FirstOrDefault(ttl => ttl is CraftTitle);
                if (title != null && title.IsSuitable(this))
                    return title;

                return PlayerTitleMgr.ClearTitle;
            }
        }

        /// <summary>
        /// This function saves all player crafting skill in the db
        /// </summary>
        protected void SaveCraftingSkills()
        {
            if (DBCharacter == null)
                return;

            DBCharacter.CraftingPrimarySkill = (byte)CraftingPrimarySkill;

            string cs = "";

            if (CraftingPrimarySkill != eCraftingSkill.NoCrafting)
            {
                lock (CraftingLock)
                {
                    foreach (KeyValuePair<eCraftingSkill, int> de in m_craftingSkills)
                    {
                        if (cs.Length > 0) cs += ";";

                        cs += Convert.ToInt32(de.Key) + "|" + Convert.ToInt32(de.Value);
                    }
                }
            }

            DBCharacter.SerializedCraftingSkills = cs;
        }

        /// <summary>
        /// This function load all player crafting skill from the db
        /// </summary>
        protected void LoadCraftingSkills()
        {
            if (DBCharacter == null)
                return;

            if (DBCharacter.SerializedCraftingSkills == "" || DBCharacter.CraftingPrimarySkill == 0)
            {
                AddCraftingSkill(eCraftingSkill.BasicCrafting, 1);
                SaveCraftingSkills();
                Out.SendUpdateCraftingSkills();
                return;
            }
            try
            {
                lock (CraftingLock)
                {
                    CraftingPrimarySkill = (eCraftingSkill)DBCharacter.CraftingPrimarySkill;

                    foreach (string skill in Util.SplitCSV(DBCharacter.SerializedCraftingSkills))
                    {
                        string[] values = skill.Split('|');
                        //Load by crafting skill name
                        if (values[0].Length > 3)
                        {
                            int i = 0;
                            switch (values[0])
                            {
                                case "WeaponCrafting": i = 1; break;
                                case "ArmorCrafting": i = 2; break;
                                case "SiegeCrafting": i = 3; break;
                                case "Alchemy": i = 4; break;
                                case "MetalWorking": i = 6; break;
                                case "LeatherCrafting": i = 7; break;
                                case "ClothWorking": i = 8; break;
                                case "GemCutting": i = 9; break;
                                case "HerbalCrafting": i = 10; break;
                                case "Tailoring": i = 11; break;
                                case "Fletching": i = 12; break;
                                case "SpellCrafting": i = 13; break;
                                case "WoodWorking": i = 14; break;
                                case "BasicCrafting": i = 15; break;

                            }
                            if (!m_craftingSkills.ContainsKey((eCraftingSkill)i))
                            {
                                if (IsCraftingSkillDefined(Convert.ToInt32(values[0])))
                                {
                                    if (DOL.GS.ServerProperties.Properties.CRAFTING_MAX_SKILLS)
                                        m_craftingSkills.Add((eCraftingSkill)i, AbstractCraftingSkill.subSkillCap);
                                    else
                                        m_craftingSkills.Add((eCraftingSkill)i, Convert.ToInt32(values[1]));
                                }
                                else
                                {
                                    log.Error("Tried to load invalid CraftingSkill :" + values[0]);
                                }
                            }
                        }
                        //Load by number
                        else if (!m_craftingSkills.ContainsKey((eCraftingSkill)Convert.ToInt32(values[0])))
                        {
                            if (IsCraftingSkillDefined(Convert.ToInt32(values[0])))
                            {
                                if (DOL.GS.ServerProperties.Properties.CRAFTING_MAX_SKILLS)
                                    m_craftingSkills.Add((eCraftingSkill)Convert.ToInt32(values[0]), AbstractCraftingSkill.subSkillCap);
                                else
                                    m_craftingSkills.Add((eCraftingSkill)Convert.ToInt32(values[0]), Convert.ToInt32(values[1]));
                            }
                            else
                            {
                                log.Error("Tried to load invalid CraftingSkill :" + values[0]);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error(Name + ": error in loading playerCraftingSkills => " + DBCharacter.SerializedCraftingSkills, e);
            }
        }

        public void ResetCraftingSkills()
        {
            lock (CraftingLock)
            {
                if (ServerProperties.Properties.PLAYERCREATION_PRIMARY_CRAFTINGSKILL)
                {
                    // Remove all crafting skills and restore BasicCrafting
                    int basicCraftingSkill;
                    if (!m_craftingSkills.TryGetValue(eCraftingSkill.BasicCrafting, out basicCraftingSkill))
                    {
                        basicCraftingSkill = 1;
                    }
                    m_craftingSkills.Clear();
                    m_craftingSkills[eCraftingSkill.BasicCrafting] = basicCraftingSkill;
                    CraftingPrimarySkill = eCraftingSkill.BasicCrafting;
                }
                else
                {
                    // Reset every skill except BasicCrafting to 1
                    foreach (eCraftingSkill craftingSkillId in Enum.GetValues(typeof(eCraftingSkill)))
                    {
                        if (m_craftingSkills.ContainsKey(craftingSkillId) && craftingSkillId != eCraftingSkill.BasicCrafting)
                        {
                            m_craftingSkills[craftingSkillId] = 1;
                        }
                    }
                    CraftingPrimarySkill = eCraftingSkill.BasicCrafting;
                }
            }
            SaveCraftingSkills();
            Out.SendUpdateCraftingSkills();
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "Crafting.Respec.SkillsReset"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        private bool IsCraftingSkillDefined(int craftingSkillToCheck)
        {
            return Enum.IsDefined(typeof(eCraftingSkill), craftingSkillToCheck);
        }

        /// <summary>
        /// This function is called each time a player tries to make a item
        /// </summary>
        public virtual void CraftItem(ushort itemID)
        {
            if (JailMgr.IsPrisoner(this))
            {
                return;
            }

            var recipe = RecipeDB.FindBy(itemID);

            AbstractCraftingSkill skill = CraftingMgr.getSkillbyEnum(recipe.RequiredCraftingSkill);
            if (skill != null)
            {
                skill.CraftItem(this, recipe);
            }
            else
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CraftItem.DontHaveAbilityMake"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        /// <summary>
        /// This function is called each time a player try to salvage a item
        /// </summary>
        public virtual void SalvageItem(InventoryItem item)
        {
            Salvage.BeginWork(this, item);
        }

        /// <summary>
        /// This function is called each time a player try to repair a item
        /// </summary>
        public virtual void RepairItem(InventoryItem item)
        {
            Repair.BeginWork(this, item);
        }

        #endregion

        #region Housing

        /// <summary>
        /// Jumps the player out of the house he is in
        /// </summary>
        public void LeaveHouse()
        {
            if (CurrentHouse == null || (!InHouse))
            {
                InHouse = false;
                return;
            }

            House house = CurrentHouse;
            InHouse = false;
            CurrentHouse = null;

            house.Exit(this, false);
        }


        #endregion

        #region Trade

        /// <summary>
        /// Holds the trade window object
        /// </summary>
        protected ITradeWindow m_tradeWindow;

        /// <summary>
        /// Gets or sets the player trade windows
        /// </summary>
        public ITradeWindow TradeWindow
        {
            get { return m_tradeWindow; }
            set { m_tradeWindow = value; }
        }

        /// <summary>
        /// Opens the trade between two players
        /// </summary>
        /// <param name="tradePartner">GamePlayer to trade with</param>
        /// <returns>true if trade has started</returns>
        public bool OpenTrade(GamePlayer tradePartner)
        {
            lock (m_LockObject)
            {
                lock (tradePartner)
                {
                    if (tradePartner.TradeWindow != null)
                        return false;

                    object sync = new object();
                    PlayerTradeWindow initiantWindow = new PlayerTradeWindow(tradePartner, false, sync);
                    PlayerTradeWindow recipientWindow = new PlayerTradeWindow(this, true, sync);

                    tradePartner.TradeWindow = initiantWindow;
                    TradeWindow = recipientWindow;

                    initiantWindow.PartnerWindow = recipientWindow;
                    recipientWindow.PartnerWindow = initiantWindow;

                    return true;
                }
            }
        }

        /// <summary>
        /// Opens the trade window for myself (combine, repair)
        /// </summary>
        /// <param name="item">The item to spell craft</param>
        /// <returns>true if trade has started</returns>
        public bool OpenSelfCraft(InventoryItem item)
        {
            if (item == null) return false;

            lock (m_LockObject)
            {
                if (TradeWindow != null)
                {
                    GamePlayer sourceTradePartner = TradeWindow.Partner;
                    if (sourceTradePartner == null)
                    {
                        Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OpenSelfCraft.AlreadySelfCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        Out.SendMessage("You are still trading with " + sourceTradePartner.Name + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    return false;
                }

                if (item.SlotPosition < (int)eInventorySlot.FirstBackpack || item.SlotPosition > (int)eInventorySlot.LastBackpack)
                {
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.OpenSelfCraft.CanOnlyCraftBackpack"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                TradeWindow = new SelfCraftWindow(this, item);
                TradeWindow.TradeUpdate();

                return true;
            }
        }

        /// <summary>
        /// Internal Method use ResetAFK Instead
        /// </summary>
        private void ResetAfkTimers()
        {
            AfkXpTimer.Stop();
            AfkXpTimer.Elapsed -= (object sender, ElapsedEventArgs e) => this.OnAfkXpTick();
            AfkXpTimer.Close();
            KickoutTimer.Elapsed -= (object sender, ElapsedEventArgs e) => this.OnAfkTimerTimeout();
            KickoutTimer.Close();
        }

        public void ResetAFK(bool isSilent)
        {
            this.PlayerAfkMessage = null;
            this.ResetAfkTimers();

            if (!isSilent)
                this.Out.SendMessage("Vous n'etes désormais plus afk", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        public void InitAfkTimers()
        {
            //Delay Timer
            this.AfkDelayTimer.AutoReset = true;
            this.AfkDelayTimer.Interval = 30 * 1000; //30s
            this.AfkDelayTimer.Stop();
            this.AfkDelayTimer.Elapsed += (object send, ElapsedEventArgs e) => this.IsAfkDelayElapsed = true;
            this.AfkDelayTimer.Start();
            this.IsAfkDelayElapsed = false;

            //Kickout timer
            int kickoutMilliseconds = Properties.AFK_TIMEOUT * 60 * 1000;

            if (kickoutMilliseconds < 0)
            {
                kickoutMilliseconds = int.MaxValue;
            }

            KickoutTimer.AutoReset = false;
            kickoutTimer.Stop();
            KickoutTimer.Interval = kickoutMilliseconds;
            KickoutTimer.Elapsed += (object sender, ElapsedEventArgs e) => this.OnAfkTimerTimeout();
            KickoutTimer.Start();

            //xp timer
            int experienceMilliseconds = Properties.AFK_XP_INTERVAL * 60 * 1000;

            if (experienceMilliseconds < 0)
            {
                experienceMilliseconds = int.MaxValue;
            }
            AfkXpTimer.Stop();
            AfkXpTimer.Interval = experienceMilliseconds;
            AfkXpTimer.Elapsed += (object sender, ElapsedEventArgs e) => OnAfkXpTick();
            AfkXpTimer.Start();
        }

        public void OnAfkXpTick()
        {
            long bonusXP = Experience * Properties.AFK_XP_PERCENTAGE / 100;
            GainExperience(GameLiving.eXPSource.Other, bonusXP, 0, 0, 0, false, 1);

            if (bonusXP > 0)
            {
                Out.SendMessage("Vous gagnez " + bonusXP + " points d'experiences grâce à votre statut AFK!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }


        private void OnAfkTimerTimeout()
        {
            this.Client.Out.SendPlayerQuit(true);
            this.Client.Disconnect();
        }

        #endregion

        #region ControlledNpc

        /// <summary>
        /// Sets the controlled object for this player
        /// (delegates to CharacterClass)
        /// </summary>
        /// <param name="controlledNpc"></param>
        public override void SetControlledBrain(IControlledBrain controlledBrain)
        {
            CharacterClass.SetControlledBrain(controlledBrain);
        }

        /// <summary>
        /// Releases controlled object
        /// (delegates to CharacterClass)
        /// </summary>
        public virtual void CommandNpcRelease()
        {
            CharacterClass.CommandNpcRelease();
        }

        /// <summary>
        /// Commands controlled object to attack
        /// </summary>
        public virtual void CommandNpcAttack()
        {
            IControlledBrain npc = ControlledBrain;
            if (npc == null || !GameServer.ServerRules.IsAllowedToAttack(this, TargetObject as GameLiving, false))
                return;

            if (npc.Body.IsConfused)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.IsConfused", npc.Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!IsWithinRadius(TargetObject, 2000))
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.TooFarAwayForPet"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!TargetInView)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Attack.CantSeeTarget"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return;
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.KillTarget", npc.Body.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            npc.Attack(TargetObject);
        }

        /// <summary>
        /// Commands controlled object to follow
        /// </summary>
        public virtual void CommandNpcFollow()
        {
            IControlledBrain npc = ControlledBrain;
            if (npc == null)
                return;

            if (npc.Body.IsConfused)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.IsConfused", npc.Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.FollowYou", npc.Body.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            npc.Follow(this);
        }

        /// <summary>
        /// Commands controlled object to stay where it is
        /// </summary>
        public virtual void CommandNpcStay()
        {
            IControlledBrain npc = ControlledBrain;
            if (npc == null)
                return;

            if (npc.Body.IsConfused)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.IsConfused", npc.Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.Stay", npc.Body.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            npc.Stay();
        }

        /// <summary>
        /// Commands controlled object to go to players location
        /// </summary>
        public virtual void CommandNpcComeHere()
        {
            IControlledBrain npc = ControlledBrain;
            if (npc == null)
                return;

            if (npc.Body.IsConfused)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.IsConfused", npc.Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.ComeHere", npc.Body.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            npc.ComeHere();
        }

        /// <summary>
        /// Commands controlled object to go to target
        /// </summary>
        public virtual void CommandNpcGoTarget()
        {
            IControlledBrain npc = ControlledBrain;
            if (npc == null)
                return;

            if (npc.Body.IsConfused)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.IsConfused", npc.Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            GameObject target = TargetObject;
            if (target == null)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcGoTarget.MustSelectDestination"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.GoToTarget", npc.Body.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            npc.Goto(target);
        }

        /// <summary>
        /// Changes controlled object state to passive
        /// </summary>
        public virtual void CommandNpcPassive()
        {
            IControlledBrain npc = ControlledBrain;
            if (npc == null)
                return;

            if (npc.Body.IsConfused)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.IsConfused", npc.Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.Passive", npc.Body.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            npc.SetAggressionState(eAggressionState.Passive);
            npc.Body.StopAttack();
            npc.Body.StopCurrentSpellcast();

            if (npc.WalkState == eWalkState.Follow)
                npc.FollowOwner();
        }

        /// <summary>
        /// Changes controlled object state to aggressive
        /// </summary>
        public virtual void CommandNpcAgressive()
        {
            IControlledBrain npc = ControlledBrain;
            if (npc == null)
                return;

            if (npc.Body.IsConfused)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.IsConfused", npc.Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.Aggressive", npc.Body.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            npc.SetAggressionState(eAggressionState.Aggressive);
        }

        /// <summary>
        /// Changes controlled object state to defensive
        /// </summary>
        public virtual void CommandNpcDefensive()
        {
            IControlledBrain npc = ControlledBrain;
            if (npc == null)
                return;

            if (npc.Body.IsConfused)
            {
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.IsConfused", npc.Body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CommandNpcAttack.Denfensive", npc.Body.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            npc.SetAggressionState(eAggressionState.Defensive);
        }
        #endregion

        #region Shade

        protected ShadeEffect m_ShadeEffect = null;

        /// <summary>
        /// The shade effect of this player
        /// </summary>
        public ShadeEffect ShadeEffect
        {
            get { return m_ShadeEffect; }
            set { m_ShadeEffect = value; }
        }

        /// <summary>
        /// Gets flag indication whether player is in shade mode
        /// </summary>
        public bool IsShade
        {
            get
            {
                bool shadeModel = Model == ShadeModel;
                return m_ShadeEffect != null ? true : shadeModel;
            }
        }

        public bool IsRenaissance
        {
            get;
            set;
        }

        public long OutlawTimeStamp
        {
            get;
            set;
        }


        public int Reputation
        {
            get { lock (m_LockObject) { return m_reputation; } }
            set
            {
                lock (m_LockObject)
                {
                    int oldReputation = m_reputation;
                    int difference = value - m_reputation;
                    m_reputation = value;

                    if (difference == 0)
                    {
                        return;
                    }

                    if (difference > 0) // adding
                    {
                        if (m_reputation >= 0)
                        {
                            OutlawTimeStamp = 0;
                            WantedUnsafe = false;
                        }
                    }
                    else // substracting
                    {
                        if (m_reputation < 0)
                        {
                            if (oldReputation >= 0) // becoming outlaw
                            {
                                if (Properties.IS_REPUTATION_RECOVERY_ACTIVATED)
                                {
                                    this.ConfigureReputationTimer();
                                }

                                OutlawTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                            }
                        }

                        if (ServerProperties.Properties.REPUTATION_THRESHOLD_AUTOMATIC_WANTED < 0 &&
                            m_reputation <= ServerProperties.Properties.REPUTATION_THRESHOLD_AUTOMATIC_WANTED && !WantedUnsafe)
                        {
                            WantedUnsafe = true;
                        }
                    }
                }

                if (CurrentRegionID != 0)
                {
                    //refresh npc quests according to new reputation
                    foreach (GameNPC mob in GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE, true))
                    {
                        Out.SendNPCsQuestEffect(mob, mob.GetQuestIndicator(this));
                    }
                }
            }
        }

        /// <summary>
        /// Factor to apply to NPC trades
        /// </summary>
        public double NPCTradeFactor
        {
            get;
            set;
        } = 1.0d;

        /// <summary>
        /// Whether the player is wanted or not, thread-unsafe
        /// </summary>
        private bool WantedUnsafe
        {
            get { return m_wanted; }
            set
            {
                bool wasWanted;
                wasWanted = m_wanted;
                m_wanted = value;
                if (wasWanted != value)
                {
                    if (value) // Becoming wanted
                    {
                        NewsMgr.CreateNews("GameObjects.GamePlayer.Wanted", Realm, eNewsType.RvRGlobal, false, true, Name);
                        if (Properties.DISCORD_ACTIVE)
                        {
                            var hook = new DolWebHook(Properties.DISCORD_WEBHOOK_ID);
                            hook.SendMessage(LanguageMgr.GetTranslation(Client?.Account?.Language ?? Properties.SERV_LANGUAGE, "GameObjects.GamePlayer.Wanted", Name));
                        }
                    }
                    SaveIntoDatabase(); // This is necessary because guards pull wanted list from the DB
                }
            }
        }

        /// <summary>
        /// Whether the player is wanted or not, thread-safe
        /// </summary>
        public bool Wanted
        {
            get { lock (m_LockObject) { return WantedUnsafe; } }
            set
            {
                lock (m_LockObject)
                {
                    WantedUnsafe = value;
                }
            }
        }

        /// <summary>
        /// Create a shade effect for this player.
        /// </summary>
        /// <returns></returns>
        protected virtual ShadeEffect CreateShadeEffect()
        {
            return CharacterClass.CreateShadeEffect();
        }

        /// <summary>
        /// The model ID used on character creation.
        /// </summary>
        public ushort CreationModel
        {
            get
            {
                return (ushort)m_client.Account.Characters[m_client.ActiveCharIndex].CreationModel;
            }
        }

        /// <summary>
        /// The model ID used for shade morphs.
        /// </summary>
        public ushort ShadeModel
        {
            get
            {
                // Aredhel: Bit fishy, necro in caster from could use
                // Traitor's Dagger... FIXME!

                if (CharacterClass.ID == (int)eCharacterClass.Necromancer)
                    return 822;

                switch (Race)
                {
                    // Albion Models.
                    case (int)eRace.Inconnu: return (ushort)(DBCharacter.Gender + 1351);
                    case (int)eRace.Briton: return (ushort)(DBCharacter.Gender + 1353);
                    case (int)eRace.Avalonian: return (ushort)(DBCharacter.Gender + 1359);
                    case (int)eRace.Highlander: return (ushort)(DBCharacter.Gender + 1355);
                    case (int)eRace.Saracen: return (ushort)(DBCharacter.Gender + 1357);
                    case (int)eRace.HalfOgre: return (ushort)(DBCharacter.Gender + 1361);

                    // Midgard Models.
                    case (int)eRace.Troll: return (ushort)(DBCharacter.Gender + 1363);
                    case (int)eRace.Dwarf: return (ushort)(DBCharacter.Gender + 1369);
                    case (int)eRace.Norseman: return (ushort)(DBCharacter.Gender + 1365);
                    case (int)eRace.Kobold: return (ushort)(DBCharacter.Gender + 1367);
                    case (int)eRace.Valkyn: return (ushort)(DBCharacter.Gender + 1371);
                    case (int)eRace.Frostalf: return (ushort)(DBCharacter.Gender + 1373);

                    // Hibernia Models.
                    case (int)eRace.Firbolg: return (ushort)(DBCharacter.Gender + 1375);
                    case (int)eRace.Celt: return (ushort)(DBCharacter.Gender + 1377);
                    case (int)eRace.Lurikeen: return (ushort)(DBCharacter.Gender + 1379);
                    case (int)eRace.Elf: return (ushort)(DBCharacter.Gender + 1381);
                    case (int)eRace.Sylvan: return (ushort)(DBCharacter.Gender + 1383);
                    case (int)eRace.Shar: return (ushort)(DBCharacter.Gender + 1385);

                    default: return Model;
                }
            }
        }

        /// <summary>
        /// Changes shade state of the player.
        /// </summary>
        /// <param name="state">The new state.</param>
        public virtual void Shade(bool state)
        {
            CharacterClass.Shade(state);
        }
        #endregion

        #region Siege Weapon
        private GameSiegeWeapon m_siegeWeapon;

        public GameSiegeWeapon SiegeWeapon
        {
            get { return m_siegeWeapon; }
            set { m_siegeWeapon = value; }
        }
        public void SalvageSiegeWeapon(GameSiegeWeapon siegeWeapon)
        {
            Salvage.BeginWork(this, siegeWeapon);
        }
        #endregion

        #region Invulnerability

        /// <summary>
        /// The delegate for invulnerability expire callbacks
        /// </summary>
        public delegate void InvulnerabilityExpiredCallback(GamePlayer player);
        /// <summary>
        /// Holds the invulnerability timer
        /// </summary>
        protected InvulnerabilityTimer m_invulnerabilityTimer;
        /// <summary>
        /// Holds the invulnerability expiration tick
        /// </summary>
        protected long m_invulnerabilityTick;

        /// <summary>
        /// Starts the Invulnerability Timer
        /// </summary>
        /// <param name="duration">The invulnerability duration in milliseconds</param>
        /// <param name="callback">
        /// The callback for when invulnerability expires;
        /// not guaranteed to be called if overwriten by another invulnerability
        /// </param>
        /// <returns>true if invulnerability was set (smaller than old invulnerability)</returns>
        public virtual bool StartInvulnerabilityTimer(int duration, InvulnerabilityExpiredCallback callback)
        {
            if (GameServer.Instance.Configuration.ServerType == eGameServerType.GST_PvE)
                return false;

            if (duration < 1)
            {
                //throw new ArgumentOutOfRangeException("duration", duration, "Immunity duration cannot be less than 1ms"); This causes problems down the road, just log it instead.
                if (log.IsWarnEnabled)
                    log.Warn("GameObjects.GamePlayer.StartInvulnerabilityTimer(): Immunity duration cannot be less than 1ms");
                return false;
            }

            long newTick = CurrentRegion.Time + duration;
            if (newTick < m_invulnerabilityTick)
                return false;

            m_invulnerabilityTick = newTick;
            if (m_invulnerabilityTimer != null)
                m_invulnerabilityTimer.Stop();

            if (callback != null)
            {
                m_invulnerabilityTimer = new InvulnerabilityTimer(this, callback);
                m_invulnerabilityTimer.Start(duration);
            }
            else
            {
                m_invulnerabilityTimer = null;
            }

            return true;
        }

        /// <summary>
        /// Holds the pve invulnerability timer
        /// </summary>
        protected InvulnerabilityTimer m_pveinvulnerabilityTimer;
        /// <summary>
        /// Holds the pve invulnerability expiration tick
        /// </summary>
        protected long m_pve_invulnerabilityTick;

        /// <summary>
        /// Starts the PVE Invulnerability Timer
        /// </summary>
        /// <param name="duration">The invulnerability duration in milliseconds</param>
        /// <param name="callback">
        /// The callback for when invulnerability expires;
        /// not guaranteed to be called if overwriten by another invulnerability
        /// </param>
        /// <returns>true if invulnerability was set (smaller than old invulnerability)</returns>
        public virtual bool StartPVEInvulnerabilityTimer(int duration, InvulnerabilityExpiredCallback callback)
        {
            if (duration < 1)
            {
                if (log.IsWarnEnabled)
                    log.Warn("GameObjects.GamePlayer.StartPVEInvulnerabilityTimer(): Immunity duration cannot be less than 1ms");
                return false;
            }

            long newTick = CurrentRegion.Time + duration;
            if (newTick < m_pve_invulnerabilityTick)
                return false;

            m_pve_invulnerabilityTick = newTick;
            if (m_pveinvulnerabilityTimer != null)
                m_pveinvulnerabilityTimer.Stop();

            if (callback != null)
            {
                m_pveinvulnerabilityTimer = new InvulnerabilityTimer(this, callback);
                m_pveinvulnerabilityTimer.Start(duration);
            }
            else
            {
                m_pveinvulnerabilityTimer = null;
            }

            return true;
        }

        /// <summary>
        /// True if player is invulnerable to any attack
        /// </summary>
        public virtual bool IsInvulnerableToAttack
        {
            get { return m_invulnerabilityTick > CurrentRegion.Time; }
        }

        /// <summary>
        /// True if player is invulnerable to pve attack
        /// </summary>
        public virtual bool IsInvulnerableToPVEAttack
        {
            get { return m_pve_invulnerabilityTick > CurrentRegion.Time; }
        }

        /// <summary>
        /// The timer to call invulnerability expired callbacks
        /// </summary>
        protected class InvulnerabilityTimer : RegionAction
        {
            /// <summary>
            /// Defines a logger for this class.
            /// </summary>
            private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

            /// <summary>
            /// Holds the callback
            /// </summary>
            private readonly InvulnerabilityExpiredCallback m_callback;

            /// <summary>
            /// Constructs a new InvulnerabilityTimer
            /// </summary>
            /// <param name="actionSource"></param>
            /// <param name="callback"></param>
            public InvulnerabilityTimer(GamePlayer actionSource, InvulnerabilityExpiredCallback callback)
                : base(actionSource)
            {
                if (callback == null)
                    throw new ArgumentNullException("callback");
                m_callback = callback;
            }

            /// <summary>
            /// Called on every timer tick
            /// </summary>
            public override void OnTick()
            {
                try
                {
                    m_callback((GamePlayer)m_actionSource);
                }
                catch (Exception e)
                {
                    log.Error("InvulnerabilityTimer callback", e);
                }
            }
        }

        #endregion

        #region Player Titles

        /// <summary>
        /// Holds all players titles.
        /// </summary>
        protected readonly ReaderWriterHashSet<IPlayerTitle> m_titles = new ReaderWriterHashSet<IPlayerTitle>();

        /// <summary>
        /// Holds current selected title.
        /// </summary>
        protected IPlayerTitle m_currentTitle = PlayerTitleMgr.ClearTitle;

        /// <summary>
        /// Gets all player's titles.
        /// </summary>
        public virtual ISet<IPlayerTitle> Titles
        {
            get { return m_titles; }
        }

        /// <summary>
        /// Gets/sets currently selected/active player title.
        /// </summary>
        public virtual IPlayerTitle CurrentTitle
        {
            get { return m_currentTitle; }
            set
            {
                if (value == null)
                    value = PlayerTitleMgr.ClearTitle;
                var old = m_currentTitle;
                if (old != value)
                {
                    m_currentTitle = value;
                    
                    old.OnTitleDeselect(this);
                    value.OnTitleSelect(this);
                
                    if (DBCharacter != null) DBCharacter.CurrentTitleType = value.GetType().FullName;

                    //update newTitle for all players if client is playing
                    if (ObjectState == eObjectState.Active)
                    {
                        if (value == PlayerTitleMgr.ClearTitle)
                            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.CurrentTitle.TitleCleared"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        else
                            Out.SendMessage("Your title has been set to " + value.GetDescription(this) + '.', eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    UpdateCurrentTitle();
                }
                else
                    Out.SendMessage("Your title has been set to " + value.GetDescription(this) + '.', eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
        
        /// <summary>
        /// Bitflag representing all the task titles available to this player
        /// </summary>
        public TaskTitle.eTitleFlags TaskTitleFlags { get; set; }

        /// <summary>
        /// Adds the title to player.
        /// </summary>
        /// <param name="title">The title to add.</param>
        /// <returns>true if added.</returns>
        public virtual bool AddTitle(IPlayerTitle title)
        {
            if (m_titles.Contains(title))
                return false;
            m_titles.Add(title);
            title.OnTitleGained(this);
            return true;
        }

        /// <summary>
        /// Removes the title from player.
        /// </summary>
        /// <param name="title">The title to remove.</param>
        /// <returns>true if removed.</returns>
        public virtual bool RemoveTitle(IPlayerTitle title)
        {
            if (!m_titles.Contains(title))
                return false;
            if (CurrentTitle == title)
                CurrentTitle = PlayerTitleMgr.ClearTitle;
            m_titles.Remove(title);
            title.OnTitleLost(this);
            return true;
        }

        /// <summary>
        /// Updates player's current title to him and everyone around.
        /// </summary>
        public virtual void UpdateCurrentTitle()
        {
            if (ObjectState == eObjectState.Active)
            {
                foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player == null) continue;
                    if (player != this)
                    {
                        //						player.Out.SendRemoveObject(this);
                        //						player.Out.SendPlayerCreate(this);
                        //						player.Out.SendLivingEquipementUpdate(this);
                        player.Out.SendPlayerTitleUpdate(this);
                    }
                }
                Out.SendUpdatePlayer();
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Gets or sets the count of albion players killed.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsAlbionPlayers
        {
            get { return DBCharacter != null ? DBCharacter.KillsAlbionPlayers : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsAlbionPlayers = value;
                Notify(GamePlayerEvent.KillsAlbionPlayersChanged, this);
                Notify(GamePlayerEvent.KillsTotalPlayersChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of midgard players killed.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsMidgardPlayers
        {
            get { return DBCharacter != null ? DBCharacter.KillsMidgardPlayers : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsMidgardPlayers = value;
                Notify(GamePlayerEvent.KillsMidgardPlayersChanged, this);
                Notify(GamePlayerEvent.KillsTotalPlayersChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of hibernia players killed.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsHiberniaPlayers
        {
            get { return DBCharacter != null ? DBCharacter.KillsHiberniaPlayers : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsHiberniaPlayers = value;
                Notify(GamePlayerEvent.KillsHiberniaPlayersChanged, this);
                Notify(GamePlayerEvent.KillsTotalPlayersChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of death blows on albion players.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsAlbionDeathBlows
        {
            get { return DBCharacter != null ? DBCharacter.KillsAlbionDeathBlows : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsAlbionDeathBlows = value;
                Notify(GamePlayerEvent.KillsTotalDeathBlowsChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of death blows on midgard players.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsMidgardDeathBlows
        {
            get { return DBCharacter != null ? DBCharacter.KillsMidgardDeathBlows : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsMidgardDeathBlows = value;
                Notify(GamePlayerEvent.KillsTotalDeathBlowsChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of death blows on hibernia players.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsHiberniaDeathBlows
        {
            get { return DBCharacter != null ? DBCharacter.KillsHiberniaDeathBlows : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsHiberniaDeathBlows = value;
                Notify(GamePlayerEvent.KillsTotalDeathBlowsChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of killed solo albion players.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsAlbionSolo
        {
            get { return DBCharacter != null ? DBCharacter.KillsAlbionSolo : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsAlbionSolo = value;
                Notify(GamePlayerEvent.KillsTotalSoloChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of killed solo midgard players.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsMidgardSolo
        {
            get { return DBCharacter != null ? DBCharacter.KillsMidgardSolo : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsMidgardSolo = value;
                Notify(GamePlayerEvent.KillsTotalSoloChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of killed solo hibernia players.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsHiberniaSolo
        {
            get { return DBCharacter != null ? DBCharacter.KillsHiberniaSolo : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsHiberniaSolo = value;
                Notify(GamePlayerEvent.KillsTotalSoloChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of captured keeps.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int CapturedKeeps
        {
            get { return DBCharacter != null ? DBCharacter.CapturedKeeps : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.CapturedKeeps = value;
                Notify(GamePlayerEvent.CapturedKeepsChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of captured towers.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int CapturedTowers
        {
            get { return DBCharacter != null ? DBCharacter.CapturedTowers : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.CapturedTowers = value;
                Notify(GamePlayerEvent.CapturedTowersChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of captured relics.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int CapturedRelics
        {
            get { return DBCharacter != null ? DBCharacter.CapturedRelics : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.CapturedRelics = value;
                Notify(GamePlayerEvent.CapturedRelicsChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of dragons killed.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsDragon
        {
            get { return DBCharacter != null ? DBCharacter.KillsDragon : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsDragon = value;
                Notify(GamePlayerEvent.KillsDragonChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the pvp deaths
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int DeathsPvP
        {
            get { return DBCharacter != null ? DBCharacter.DeathsPvP : 0; }
            set { if (DBCharacter != null) DBCharacter.DeathsPvP = value; }
        }

        /// <summary>
        /// Gets or sets the count of killed Legions.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsLegion
        {
            get { return DBCharacter != null ? DBCharacter.KillsLegion : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsLegion = value;
                Notify(GamePlayerEvent.KillsLegionChanged, this);
            }
        }

        /// <summary>
        /// Gets or sets the count of killed Epic Boss.
        /// (delegate to DBCharacter)
        /// </summary>
        public virtual int KillsEpicBoss
        {
            get { return DBCharacter != null ? DBCharacter.KillsEpicBoss : 0; }
            set
            {
                if (DBCharacter != null) DBCharacter.KillsEpicBoss = value;
                Notify(GamePlayerEvent.KillsEpicBossChanged, this);
            }
        }
        #endregion

        #region Controlled Mount

        protected RegionTimer m_whistleMountTimer;
        protected ControlledHorse m_controlledHorse;

        public bool HasHorse
        {
            get
            {
                if (ActiveHorse == null || ActiveHorse.ID == 0)
                    return false;
                return true;
            }
        }

        public bool IsSummoningMount
        {
            get { return m_whistleMountTimer != null && m_whistleMountTimer.IsAlive; }
        }

        private SpellHandler m_pulseSpell;

        public bool HasEnabledAPulseSpell
        {
            get
            {
                return m_pulseSpell != null;
            }
        }

        protected bool m_isOnHorse;
        public virtual bool IsOnHorse
        {
            get { return m_isOnHorse; }
            set
            {
                if (m_whistleMountTimer != null)
                    StopWhistleTimers();
                if (value == m_isOnHorse)
                    return;
                m_isOnHorse = value;
                Out.SendControlledHorse(this, value); // fix very rare bug when this player not in GetPlayersInRadius;
                foreach (GamePlayer plr in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (plr == null) continue;
                    if (plr == this)
                        continue;
                    plr.Out.SendControlledHorse(this, value);
                }
                if (m_isOnHorse)
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsOnHorse.MountSteed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                else
                    Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.IsOnHorse.DismountSteed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Out.SendUpdateMaxSpeed();
            }
        }

        protected void StopWhistleTimers()
        {
            if (m_whistleMountTimer != null)
            {
                m_whistleMountTimer.Stop();
                Out.SendCloseTimerWindow();
            }
            m_whistleMountTimer = null;
        }

        protected int WhistleMountTimerCallback(RegionTimer callingTimer)
        {
            StopWhistleTimers();
            IsOnHorse = true;
            if (IsSitting)
                IsSitting = false;
            return 0;
        }

        public enum eHorseSaddleBag : byte
        {
            None = 0x00,
            LeftFront = 0x01,
            RightFront = 0x02,
            LeftRear = 0x04,
            RightRear = 0x08,
            All = 0x0F
        }

        /// <summary>
        /// What horse saddle bags are active on this player?
        /// </summary>
        public virtual byte ActiveSaddleBags
        {
            get { return DBCharacter != null ? DBCharacter.ActiveSaddleBags : (byte)0; }
            set { if (DBCharacter != null) DBCharacter.ActiveSaddleBags = value; }
        }

        public ControlledHorse ActiveHorse
        {
            get { return m_controlledHorse; }
        }

        public class ControlledHorse
        {
            protected byte m_id;
            protected byte m_bardingId;
            protected ushort m_bardingColor;
            protected byte m_saddleId;
            protected byte m_saddleColor;
            protected byte m_slots;
            protected byte m_armor;
            protected string m_name;
            protected int m_level;
            protected GamePlayer m_player;

            public ControlledHorse(GamePlayer player)
            {
                m_name = "";
                m_player = player;
            }

            public byte ID
            {
                get { return m_id; }
                set
                {
                    m_id = value;
                    InventoryItem item = m_player.Inventory.GetItem(eInventorySlot.Horse);
                    if (item != null)
                        m_level = item.Level;
                    else
                        m_level = 35;//base horse by default
                    m_player.Out.SendSetControlledHorse(m_player);
                }
            }

            public byte Barding
            {
                get
                {
                    InventoryItem barding = m_player.Inventory.GetItem(eInventorySlot.HorseBarding);
                    if (barding != null)
                        return (byte)barding.DPS_AF;
                    return m_bardingId;
                }
                set
                {
                    m_bardingId = value;
                    m_player.Out.SendSetControlledHorse(m_player);
                }
            }

            public ushort BardingColor
            {
                get
                {
                    InventoryItem barding = m_player.Inventory.GetItem(eInventorySlot.HorseBarding);
                    if (barding != null)
                        return (ushort)barding.Color;
                    return m_bardingColor;
                }
                set
                {
                    m_bardingColor = value;
                    m_player.Out.SendSetControlledHorse(m_player);
                }
            }

            public byte Saddle
            {
                get
                {
                    InventoryItem armor = m_player.Inventory.GetItem(eInventorySlot.HorseArmor);
                    if (armor != null)
                        return (byte)armor.DPS_AF;
                    return m_saddleId;
                }
                set
                {
                    m_saddleId = value;
                    m_player.Out.SendSetControlledHorse(m_player);
                }
            }

            public byte SaddleColor
            {
                get
                {
                    InventoryItem armor = m_player.Inventory.GetItem(eInventorySlot.HorseArmor);
                    if (armor != null)
                        return (byte)armor.Color;
                    return m_saddleColor;
                }
                set
                {
                    m_saddleColor = value;
                    m_player.Out.SendSetControlledHorse(m_player);
                }
            }

            public byte Slots
            {
                get { return m_player.ActiveSaddleBags; }
            }

            public byte Armor
            {
                get { return m_armor; }
            }

            public string Name
            {
                get { return m_name; }
                set
                {
                    m_name = value;
                    InventoryItem item = m_player.Inventory.GetItem(eInventorySlot.Horse);
                    if (item != null)
                        item.Creator = Name;
                    m_player.Out.SendSetControlledHorse(m_player);
                }
            }

            public short Speed
            {
                get
                {
                    if (m_level <= 35)
                        return ServerProperties.Properties.MOUNT_UNDER_LEVEL_35_SPEED;
                    else
                        return ServerProperties.Properties.MOUNT_OVER_LEVEL_35_SPEED;
                }
            }

            public bool IsSummonRvR
            {
                get
                {
                    if (m_level <= 35)
                        return false;
                    else
                        return true;
                }
            }

            public bool IsCombatHorse
            {
                get
                {
                    return false;
                }
            }
        }
        #endregion

        #region GuildBanner
        protected BannerVisual m_activeBanner = null;

        /// <summary>
        /// Gets/Sets the visibility of the carryable RvrGuildBanner. Wont work if the player has no guild.
        /// </summary>
        public BannerVisual ActiveBanner
        {
            get { return m_activeBanner; }
            set
            {
                if (m_activeBanner == value)
                    return;

                var prev = m_activeBanner;
                m_activeBanner = value;
                if (prev != null)
                    prev.CarryingPlayer = null;
                if (value != null)
                    value.CarryingPlayer = this;
                foreach (GamePlayer playerToUpdate in GetPlayersInRadius(WorldMgr.OBJ_UPDATE_DISTANCE))
                {
                    if (playerToUpdate?.Client.IsPlaying == true)
                    {
                        playerToUpdate.Out.SendRvRGuildBanner(this, value);
                    }
                }
            }
        }

        #endregion

        #region Champion Levels
        /// <summary>
        /// The maximum champion level a player can reach
        /// </summary>
        public const int CL_MAX_LEVEL = 10;

        /// <summary>
        /// A table that holds the required XP/Level
        /// </summary>
        public static readonly long[] CLXPLevel =
        {
            0, //xp tp level 0
            32000, //xp to level 1
            64000, // xp to level 2
            96000, // xp to level 3
            128000, // xp to level 4
            160000, // xp to level 5
            192000, // xp to level 6
            224000, // xp to level 7
            256000, // xp to level 8
            288000, // xp to level 9
            320000, // xp to level 10
            640000, // xp to level 11
            640000, // xp to level 12
            640000, // xp to level 13
            640000, // xp to level 14
            640000, // xp to level 15
        };

        /// <summary>
        /// Get the CL title string of the player
        /// </summary>
        public virtual IPlayerTitle CLTitle
        {
            get
            {
                var title = m_titles.FirstOrDefault(ttl => ttl is ChampionlevelTitle);

                if (title != null && title.IsSuitable(this))
                    return title;

                return PlayerTitleMgr.ClearTitle;
            }
        }

        /// <summary>
        /// Is Champion level activated
        /// </summary>
        public virtual bool Champion
        {
            get { return DBCharacter != null ? DBCharacter.Champion : false; }
            set
            {
                if (DBCharacter == null) return;
                
                DBCharacter.Champion = value;
                
                RefreshQuestNPCs();
            }
        }

        /// <summary>
        /// Champion level
        /// </summary>
        public virtual int ChampionLevel
        {
            get { return DBCharacter != null ? DBCharacter.ChampionLevel : 0; }
            set { if (DBCharacter != null) DBCharacter.ChampionLevel = value; }
        }

        /// <summary>
        /// Max champion level for the player
        /// </summary>
        public virtual int ChampionMaxLevel
        {
            get { return CL_MAX_LEVEL; }
        }

        /// <summary>
        /// Champion Experience
        /// </summary>
        public virtual long ChampionExperience
        {
            get { return DBCharacter != null ? DBCharacter.ChampionExperience : 0; }
            set { if (DBCharacter != null) DBCharacter.ChampionExperience = value; }
        }

        /// <summary>
        /// Champion Available speciality points
        /// </summary>
        public virtual int ChampionSpecialtyPoints
        {
            get { return ChampionLevel - GetSpecList().Where(sp => sp is LiveChampionsLineSpec).Sum(sp => sp.Level); }
        }

        /// <summary>
        /// Returns how far into the champion level we have progressed
        /// A value between 0 and 1000 (1 bubble = 100)
        /// </summary>
        public virtual ushort ChampionLevelPermill
        {
            get
            {
                //No progress if we haven't even reached current level!
                if (ChampionExperience <= ChampionExperienceForCurrentLevel)
                    return 0;
                //No progess after maximum level
                if (ChampionLevel > CL_MAX_LEVEL) // needed to get exp after 10
                    return 0;
                if ((ChampionExperienceForNextLevel - ChampionExperienceForCurrentLevel) > 0)
                    return (ushort)(1000 * (ChampionExperience - ChampionExperienceForCurrentLevel) / (ChampionExperienceForNextLevel - ChampionExperienceForCurrentLevel));
                else return 0;

            }
        }

        /// <summary>
        /// Returns the xp that are needed for the next level
        /// </summary>
        public virtual long ChampionExperienceForNextLevel
        {
            get { return GetChampionExperienceForLevel(ChampionLevel + 1); }
        }

        /// <summary>
        /// Returns the xp that were needed for the current level
        /// </summary>
        public virtual long ChampionExperienceForCurrentLevel
        {
            get { return GetChampionExperienceForLevel(ChampionLevel); }
        }

        /// <summary>
        /// Gets/Sets amount of champion skills respecs
        /// (delegate to PlayerCharacter)
        /// </summary>
        public virtual int RespecAmountChampionSkill
        {
            get { return DBCharacter != null ? DBCharacter.RespecAmountChampionSkill : 0; }
            set { if (DBCharacter != null) DBCharacter.RespecAmountChampionSkill = value; }
        }

        /// <summary>
        /// Returns the xp that are needed for the specified level
        /// </summary>
        public virtual long GetChampionExperienceForLevel(int level)
        {
            if (level > CL_MAX_LEVEL)
                return CLXPLevel[GamePlayer.CL_MAX_LEVEL]; // exp for level 51, needed to get exp after 50
            if (level <= 0)
                return CLXPLevel[0];
            return CLXPLevel[level];
        }

        public void GainChampionExperience(long experience)
        {
            GainChampionExperience(experience, eXPSource.Other);
        }

        /// <summary>
        /// The process that gains exp
        /// </summary>
        /// <param name="experience">Amount of Experience</param>
        public virtual void GainChampionExperience(long experience, eXPSource source)
        {
            if (ChampionExperience >= 320000)
            {
                ChampionExperience = 320000;
                return;
            }

            // Do not gain experience:
            // - if champion not activated
            // - if champion max level reached
            // - if experience is negative
            // - if praying at your grave
            if (!Champion || ChampionLevel == CL_MAX_LEVEL || experience <= 0 || IsPraying)
                return;

            if (source != eXPSource.GM && source != eXPSource.Quest)
            {
                double modifier = ServerProperties.Properties.CL_XP_RATE;

                // 1 CLXP point per 333K normal XP
                if (this.CurrentRegion.IsRvR)
                {
                    experience = (long)((double)experience * modifier / 333000);
                }
                else // 1 CLXP point per 2 Million normal XP
                {
                    experience = (long)((double)experience * modifier / 2000000);
                }
            }

            System.Globalization.NumberFormatInfo format = System.Globalization.NumberFormatInfo.InvariantInfo;
            Out.SendMessage("Vous gagnez " + experience.ToString("N0", format) + " points d'experience champion", eChatType.CT_Important, eChatLoc.CL_SystemWindow);

            ChampionExperience += experience;
            Out.SendUpdatePoints();

            if (ChampionExperience >= ChampionExperienceForNextLevel)
                ChampionLevelUp();
        }


        /// <summary>
        /// Reset all Champion skills for this player
        /// </summary>
        public virtual void RespecChampionSkills()
        {
            foreach (var spec in GetSpecList().Where(sp => sp is LiveChampionsLineSpec))
            {
                RemoveSpecialization(spec.KeyName);
            }

            RefreshSpecDependantSkills(false);
            Out.SendUpdatePlayer();
            Out.SendUpdatePoints();
            Out.SendUpdatePlayerSkills();
            UpdatePlayerStatus();
            RefreshItemBonuses();
            UpdateEquipmentAppearance();
            Out.SendUpdateWeaponAndArmorStats();
            Out.SendStatusUpdate();
        }


        /// <summary>
        /// Remove all Champion levels and XP from this character.
        /// </summary>
        public virtual void RemoveChampionLevels()
        {
            ChampionExperience = 0;
            ChampionLevel = 0;

            RespecChampionSkills();
        }

        /// <summary>
        /// Holds what happens when your champion level goes up;
        /// </summary>
        public virtual void ChampionLevelUp()
        {
            ChampionLevel++;

            // If this is a pure tank then give them full power when reaching champ level 1
            if (ChampionLevel == 1 && CharacterClass.ClassType == eClassType.PureTank)
            {
                Mana = CalculateMaxMana(Level, 0);
            }

            RefreshSpecDependantSkills(true);
            Out.SendUpdatePlayerSkills();

            Notify(GamePlayerEvent.ChampionLevelUp, this);
            Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Champion.LevelUp"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            Out.SendUpdatePlayer();
            Out.SendUpdatePoints();
            Out.SendCharStatsUpdate();
            Out.SendCharResistsUpdate();
            UpdatePlayerStatus();
            RefreshItemBonuses();
            UpdateEquipmentAppearance();
            Out.SendUpdateWeaponAndArmorStats();
            Out.SendStatusUpdate();
        }

        #endregion

        #region Master levels

        /// <summary>
        /// The maximum ML level a player can reach
        /// </summary>
        public const int ML_MAX_LEVEL = 10;

        /// <summary>
        /// Amount of MLXP required for ML validation. MLXP reset at every ML.
        /// </summary>
        private static readonly long[] MLXPLevel =
        {
            0, //xp tp level 0
            32000, //xp to level 1
            32000, // xp to level 2
            32000, // xp to level 3
            32000, // xp to level 4
            32000, // xp to level 5
            32000, // xp to level 6
            32000, // xp to level 7
            32000, // xp to level 8
            32000, // xp to level 9
            32000, // xp to level 10
        };

        /// <summary>
        /// Get the amount of XP needed for a ML
        /// </summary>
        /// <param name="ml"></param>
        /// <returns></returns>
        public virtual long GetXPForML(byte ml)
        {
            if (ml > MLXPLevel.Length - 1)
                return 0;

            return MLXPLevel[ml];
        }

        /// <summary>
        /// How many steps for each Master Level
        /// </summary>
        private static readonly byte[] MLStepsForLevel =
        {
            10, // ML0 == ML1
            10, // ML1
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            5, // ML10
        };

        /// <summary>
        /// Get the number of steps required for a ML
        /// </summary>
        /// <param name="ml"></param>
        /// <returns></returns>
        public virtual byte GetStepCountForML(byte ml)
        {
            if (ml > MLStepsForLevel.Length - 1)
                return 0;

            return MLStepsForLevel[ml];
        }

        /// <summary>
        /// True if player has started Master Levels
        /// </summary>
        public virtual bool MLGranted
        {
            get { return DBCharacter != null ? DBCharacter.MLGranted : false; }
            set { if (DBCharacter != null) DBCharacter.MLGranted = value; }
        }

        /// <summary>
        /// What ML line has this character chosen
        /// </summary>
        public virtual byte MLLine
        {
            get { return DBCharacter != null ? DBCharacter.ML : (byte)0; }
            set { if (DBCharacter != null) DBCharacter.ML = value; }
        }

        /// <summary>
        /// Gets and sets the last ML the player has completed.
        /// MLLevel is advanced once all steps are completed.
        /// </summary>
        public virtual int MLLevel
        {
            get { return DBCharacter != null ? DBCharacter.MLLevel : 0; }
            set { if (DBCharacter != null) DBCharacter.MLLevel = value; }
        }

        /// <summary>
        /// Gets and sets ML Experience for the current ML level
        /// </summary>
        public virtual long MLExperience
        {
            get { return DBCharacter != null ? DBCharacter.MLExperience : 0; }
            set { if (DBCharacter != null) DBCharacter.MLExperience = value; }
        }

        /// <summary>
        /// Get the number of steps completed for a ML
        /// </summary>
        /// <param name="ml"></param>
        /// <returns></returns>
        public virtual byte GetCountMLStepsCompleted(byte ml)
        {
            byte count = 0;
            int steps = GetStepCountForML(ml);

            for (byte i = 1; i <= steps; i++)
            {
                if (HasFinishedMLStep(ml, i))
                {
                    count++;
                    Out.SendCharStatsUpdate();
                    Out.SendCharResistsUpdate();
                    UpdatePlayerStatus();
                    RefreshItemBonuses();
                }
            }

            return count;
        }

        /// <summary>
        /// Check ML step completition.
        /// Arbiter checks this to see if player is eligible to advance to the next Master Level.
        /// </summary>
        public virtual bool HasFinishedMLStep(int mlLevel, int step)
        {
            // No steps registered so false
            if (m_mlSteps == null) return false;

            // Current ML Level >= required ML, so true
            if (MLLevel >= mlLevel) return true;

            // Check current registered steps
            foreach (DBCharacterXMasterLevel mlStep in m_mlSteps)
            {
                // Found so return value
                if (mlStep.MLLevel == mlLevel && mlStep.MLStep == step)
                    return mlStep.StepCompleted;
            }

            // Not found so false
            return false;
        }

        /// <summary>
        /// Sets an ML step to finished or clears it
        /// </summary>
        /// <param name="mlLevel"></param>
        /// <param name="step"></param>
        /// <param name="setFinished">(optional) false will remove the finished entry for this step</param>
        public virtual void SetFinishedMLStep(int mlLevel, int step, bool setFinished = true)
        {
            // Check current registered steps in case of previous GM rollback command
            if (m_mlSteps != null)
            {
                foreach (DBCharacterXMasterLevel mlStep in m_mlSteps)
                {
                    if (mlStep.MLLevel == mlLevel && mlStep.MLStep == step)
                    {
                        mlStep.StepCompleted = setFinished;
                        return;
                    }
                }
            }

            if (setFinished)
            {
                // Register new step
                DBCharacterXMasterLevel newStep = new DBCharacterXMasterLevel();
                newStep.Character_ID = InternalID;
                newStep.MLLevel = mlLevel;
                newStep.MLStep = step;
                newStep.StepCompleted = true;
                newStep.ValidationDate = DateTime.Now;
                m_mlSteps!.Add(newStep);

                // Add it in DB
                try
                {
                    GameServer.Database.AddObject(newStep);
                }
                catch (Exception e)
                {
                    if (log.IsErrorEnabled)
                        log.Error("Error adding player " + Name + " ml step!", e);
                }

                // Refresh Window
                Out.SendMasterLevelWindow((byte)mlLevel);
            }
        }

        /// <summary>
        /// Returns the xp that are needed for the specified level
        /// </summary>
        public virtual long GetMLExperienceForLevel(int level)
        {
            if (level >= ML_MAX_LEVEL)
                return MLXPLevel[GamePlayer.ML_MAX_LEVEL - 1]; // exp for level 9, needed to get exp after 9
            if (level <= 0)
                return MLXPLevel[0];
            return MLXPLevel[level];
        }

        /// <summary>
        /// Get the Masterlevel window text for a ML and Step
        /// </summary>
        /// <param name="ml"></param>
        /// <param name="step"></param>
        /// <returns></returns>
        public virtual string GetMLStepDescription(byte ml, int step)
        {
            string description = " ";

            if (HasFinishedMLStep(ml, step))
                description = LanguageMgr.GetTranslation(Client.Account.Language, String.Format("SendMasterLevelWindow.Complete.ML{0}.Step{1}", ml, step));
            else
                description = LanguageMgr.GetTranslation(Client.Account.Language, String.Format("SendMasterLevelWindow.Uncomplete.ML{0}.Step{1}", ml, step));

            return description;
        }

        /// <summary>
        /// Get the ML title string of the player
        /// </summary>
        public virtual IPlayerTitle MLTitle
        {
            get
            {
                var title = m_titles.FirstOrDefault(ttl => ttl is MasterlevelTitle);

                if (title != null && title.IsSuitable(this))
                    return title;

                return PlayerTitleMgr.ClearTitle;
            }
        }

        #endregion

        #region Minotaur Relics
        protected MinotaurRelic m_minoRelic = null;

        private int m_reputation;

        private bool m_wanted;

        /// <summary>
        /// sets or sets the Minotaur Relic of this Player
        /// </summary>
        public MinotaurRelic MinotaurRelic
        {
            get { return m_minoRelic; }
            set { m_minoRelic = value; }
        }
        #endregion

        #region Artifacts

        /// <summary>
        /// Checks if the player's class has at least one version of the artifact specified available to them.
        /// </summary>
        /// <param name="artifactID"></param>
        /// <returns>True when at least one version exists, false when no versions are available.</returns>
        public bool CanReceiveArtifact(string artifactID)
        {
            Dictionary<String, ItemTemplate> possibleVersions = ArtifactMgr.GetArtifactVersions(artifactID, (eCharacterClass)CharacterClass.ID, Realm);

            if (possibleVersions.Count == 0)
                return false;

            return true;
        }

        #endregion

        #region Constructors
        /// <summary>
        /// Returns the string representation of the GamePlayer
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return new StringBuilder(base.ToString())
                .Append(" class=").Append(CharacterClass.Name)
                .Append('(').Append(CharacterClass.ID.ToString()).Append(')')
                .ToString();
        }

        public static GamePlayer CreateDummy()
        {
            var player = new GamePlayer();
            player.m_characterClass = new CharacterClassBase();
            player.m_dbCharacter = new DOLCharacters();
            return player;
        }

        private GamePlayer() : base()
        {
            this.IsAfkDelayElapsed = true;
            this.IsAllowToVolInThisArea = true;
            this.ConfigureReputationTimer();
        }

        /// <summary>
        /// Creates a new player
        /// </summary>
        /// <param name="client">The GameClient for this player</param>
        /// <param name="dbChar">The character for this player</param>
        public GamePlayer(GameClient client, DOLCharacters dbChar)
            : base()
        {
            Wallet = new Wallet(this);
            IsJumping = false;
            m_steed = new WeakRef(null);
            m_rangeAttackAmmo = new WeakRef(null);
            m_rangeAttackTarget = new WeakRef(null);
            m_client = client;
            m_dbCharacter = dbChar;
            this.IsAfkDelayElapsed = true;
            this.IsAllowToVolInThisArea = true;
            m_controlledHorse = new ControlledHorse(this);
            m_buff1Bonus = new PropertyIndexer((int)eProperty.MaxProperty); // set up a fixed indexer for players
            m_buff2Bonus = new PropertyIndexer((int)eProperty.MaxProperty);
            m_debuffBonus = new PropertyIndexer((int)eProperty.MaxProperty);
            m_buff4Bonus = new PropertyIndexer((int)eProperty.MaxProperty);
            m_itemBonus = new PropertyIndexer((int)eProperty.MaxProperty);
            m_canFly = false;
            m_wanted = false;
            m_reputation = 0;

            CreateInventory();
            GameEventMgr.AddHandler(m_inventory, PlayerInventoryEvent.ItemEquipped, new DOLEventHandler(OnItemEquipped));
            GameEventMgr.AddHandler(m_inventory, PlayerInventoryEvent.ItemUnequipped, new DOLEventHandler(OnItemUnequipped));
            GameEventMgr.AddHandler(m_inventory, PlayerInventoryEvent.ItemBonusChanged, new DOLEventHandler(OnItemBonusChanged));

            m_enteredGame = false;
            m_customDialogCallback = null;
            m_sitting = false;
            m_isWireframe = false;
            m_characterClass = new CharacterClassBase();
            m_groupIndex = 0xFF;

            m_saveInDB = true;
            LoadFromDatabase(dbChar);

            CreateStatistics();
            this.ConfigureReputationTimer();
            shadowNPC = new ShadowNPC(this);
        }

        /// <summary>
        /// Create this players inventory
        /// </summary>
        protected virtual void CreateInventory()
        {
            m_inventory = new GamePlayerInventory(this);
        }
        #endregion

        #region Delving
        /// <summary>
        /// Player is delving an item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="delveInfo"></param>
        /// <returns>false if delve not handled</returns>
        public virtual bool DelveItem<T>(T item, List<string> delveInfo)
        {
            if (item is IGameInventoryItem)
            {
                (item as IGameInventoryItem)!.Delve(delveInfo, this);
            }
            else if (item is InventoryItem)
            {
                GameInventoryItem tempItem = GameInventoryItem.Create(item as InventoryItem);
                tempItem.Delve(delveInfo, this);
            }
            else if (item is ItemTemplate)
            {
                GameInventoryItem tempItem = GameInventoryItem.Create(item as ItemTemplate);
                tempItem.Delve(delveInfo, this);
            }
            else
            {
                delveInfo.Add("Error, unable to delve this item!");
                log.ErrorFormat("Error delving item of ClassType {0}", item.GetType().FullName);
            }

            return true;
        }


        /// <summary>
        /// Player is delving a spell
        /// </summary>
        /// <param name="output"></param>
        /// <param name="spell"></param>
        /// <param name="spellLine"></param>
        /// <returns>false if not handled here, use default delve</returns>
        public virtual bool DelveSpell(IList<string> output, Spell spell, SpellLine spellLine)
        {
            return false;
        }


        /// <summary>
        /// Delve a weapon style for this player
        /// </summary>
        /// <param name="delveInfo"></param>
        /// <param name="style"></param>
        /// <returns></returns>
        public virtual void DelveWeaponStyle(IList<string> delveInfo, Style style)
        {
            StyleProcessor.DelveWeaponStyle(delveInfo, style, this);
        }

        /// <summary>
        /// Get a list of bonuses that effect this player
        /// </summary>
        public virtual ICollection<string> GetBonuses()
        {
            return this.GetBonusesInfo();
        }
        #endregion

        #region Combat Calc (unused ?)
        public virtual double GetEvadeChance()
        {
            double evadeChance = 0;

            GameSpellEffect evade = SpellHandler.FindEffectOnTarget(this, "EvadeBuff");
            if (evade == null)
                evade = SpellHandler.FindEffectOnTarget(this, "SavageEvadeBuff");

            if (HasAbility(Abilities.Advanced_Evade) || EffectList.GetOfType<CombatAwarenessEffect>() != null || EffectList.GetOfType<RuneOfUtterAgilityEffect>() != null)
                evadeChance = GetModified(eProperty.EvadeChance);
            else if (evade != null || HasAbility(Abilities.Evade))
            {
                int res = GetModified(eProperty.EvadeChance);
                if (res > 0)
                    evadeChance = res;
            }

            if (evadeChance > 0)
            {
                evadeChance *= 0.001;
                if (evadeChance < 0.01)
                    evadeChance = 0.01;
                if (evadeChance > 0.5)
                    evadeChance = 0.5;
            }
            return Math.Round(evadeChance * 10000) / 100;
        }

        public virtual double GetBlockChance()
        {
            double blockChance = 0;
            InventoryItem lefthand = null;
            if (HasAbility(Abilities.Shield))
            {
                lefthand = Inventory.GetItem(eInventorySlot.LeftHandWeapon);
                if (lefthand != null && (AttackWeapon == null || AttackWeapon.Item_Type == Slot.RIGHTHAND || AttackWeapon.Item_Type == Slot.LEFTHAND))
                {
                    if (lefthand.Object_Type == (int)eObjectType.Shield)
                        blockChance = GetModified(eProperty.BlockChance) * lefthand.Quality * 0.01;
                }
            }
            if (blockChance > 0)
            {
                blockChance *= 0.001;
                if (blockChance > 0.99) blockChance = 0.99;
                if (blockChance < 0.01) blockChance = 0.01;

                int shieldSize = 0;
                if (lefthand != null)
                    shieldSize = lefthand.Type_Damage;
            }

            return Math.Round(blockChance * 10000) / 100;
        }

        public virtual double GetParryChance()
        {
            double parryChance = 0;

            GameSpellEffect parry = SpellHandler.FindEffectOnTarget(this, "ParryBuff");
            if (parry == null)
                parry = SpellHandler.FindEffectOnTarget(this, "SavageParryBuff");

            if ((HasSpecialization(Specs.Parry) || parry != null) && (AttackWeapon != null))
                parryChance = GetModified(eProperty.ParryChance);
            else if (EffectList.GetOfType<BladeBarrierEffect>() != null)
                parryChance = GetModified(eProperty.ParryChance);

            if (parryChance > 0)
            {
                parryChance *= 0.001;
                if (parryChance < 0.01) parryChance = 0.01;
                if (parryChance > 0.99) parryChance = 0.99;
            }

            return Math.Round(parryChance * 10000) / 100;
        }
        #endregion

        #region Bodyguard
        /// <summary>
        /// True, if the player has been standing still for at least 3 seconds,
        /// else false.
        /// </summary>
        private bool IsStandingStill
        {
            get
            {
                if (IsMoving)
                    return false;

                long lastMovementTick = TempProperties.getProperty<long>("PLAYERPOSITION_LASTMOVEMENTTICK");
                return (CurrentRegion.Time - lastMovementTick > 3000);
            }
        }

        /// <summary>
        /// This player's bodyguard (ML ability) or null, if there is none.
        /// </summary>
        public GamePlayer Bodyguard
        {
            get
            {
                var bodyguardEffects = EffectList.GetAllOfType<BodyguardEffect>();

                BodyguardEffect bodyguardEffect = bodyguardEffects.FirstOrDefault();
                if (bodyguardEffect == null || bodyguardEffect.GuardTarget != this)
                    return null;

                GamePlayer guard = bodyguardEffect.GuardSource;
                GamePlayer guardee = this;

                return (guard.IsAlive && guard.IsWithinRadius(guardee, BodyguardAbilityHandler.BODYGUARD_DISTANCE) &&
                        !guard.IsCasting && guardee.IsStandingStill)
                    ? guard
                    : null;
            }
        }


        public long GetRemainingIntervalReputationTime()
        {
            if (this.OutlawTimeStamp == 0)
            {
                return reputationDaysDurationInSeconds;
            }

            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            long ellapsed = now - this.OutlawTimeStamp;

            if (ellapsed > 0 && ellapsed >= reputationDaysDurationInSeconds)
            {
                //time over
                return 0;
            }
            else if (ellapsed > 0 && ellapsed < reputationDaysDurationInSeconds)
            {
                return reputationDaysDurationInSeconds - ellapsed;
            }

            return reputationDaysDurationInSeconds;
        }

        private void ConfigureReputationTimer()
        {
            if (this.Reputation < 0 && Properties.IS_REPUTATION_RECOVERY_ACTIVATED && Properties.REPUTATION_DAYS_INTERVAL > 0 && Properties.REPUTATION_POINTS_RECOVERY > 0)
            {
                long remainingTime = this.GetRemainingIntervalReputationTime();

                if (remainingTime <= 0)
                {
                    this.RecoverReputation(Properties.REPUTATION_POINTS_RECOVERY);
                    remainingTime = reputationDaysDurationInSeconds;
                }

                if (this.reputationRecoveryTimer == null)
                {
                    //Number of days expressed in ms	
                    this.reputationRecoveryTimer = new Timer(remainingTime * 1000);
                    this.reputationRecoveryTimer.Elapsed += this.HandleReputationTimerElapsed;
                    this.reputationRecoveryTimer.AutoReset = false;
                    this.reputationRecoveryTimer.Start();
                }
                else
                {
                    this.reputationRecoveryTimer.Stop();
                    this.reputationRecoveryTimer.Interval = remainingTime * 1000;
                    this.reputationRecoveryTimer.Start();
                }
            }
        }

        private void HandleReputationTimerElapsed(object sender, ElapsedEventArgs e)
        {
            this.RecoverReputation(Properties.REPUTATION_POINTS_RECOVERY);
            if (Reputation < 0)
            {
                this.OutlawTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                this.reputationRecoveryTimer.Interval = reputationDaysDurationInSeconds * 1000;
                this.reputationRecoveryTimer.Start();
            }
        }

        public void RecoverReputation(int amount)
        {
            if (Reputation + amount >= 0)
            {
                Reputation = 0;
                Out.SendMessage(LanguageMgr.GetTranslation(Client.Account.Language, "GameObjects.GamePlayer.Reputation.Zero"), eChatType.CT_Staff, eChatLoc.CL_SystemWindow);
            }
            else
            {
                Reputation += amount;
                Out.SendMessage(string.Format("Vous gagnez {0} " + (Math.Abs(amount) > 1 ? "points" : "point") + " de réputation. Vous avez désormais {1} points", amount, Reputation), eChatType.CT_Staff, eChatLoc.CL_SystemWindow);
            }
        }

        /// <summary>
        /// This property is use for the assassinate RA
        /// </summary>
        public bool StayStealth { get => stayStealth; set => stayStealth = value; }
        public SpellHandler PulseSpell { get => m_pulseSpell; set => m_pulseSpell = value; }
        public ShadowNPC ShadowNPC { get => shadowNPC; set => shadowNPC = value; }

        #endregion

    }
}
