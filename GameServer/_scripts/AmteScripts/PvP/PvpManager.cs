using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using AmteScripts.Areas;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.Territories;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;
using AmteScripts.PvP;
using DOL.GS.Scripts;
using static DOL.GS.Area;
using AmteScripts.PvP.CTF;
using Discord;
using DOL.GS.ServerProperties;
using Google.Protobuf.WellKnownTypes;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using static AmteScripts.Managers.PvpManager;
using static System.Formats.Asn1.AsnWriter;
using static DOL.GameEvents.GameEvent;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Policy;
using AmteScripts.PvP.KotH;
using Zone = DOL.GS.Zone;
using DOL.GS.Spells;
using DOL.GameEvents;

namespace AmteScripts.Managers
{
    public class PvpManager
    {
        [NotNull] private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType!)!;

        public enum eSessionTypes
        {
            None = 0,
            Deathmatch = 1,
            CaptureTheFlag = 2,
            TreasureHunt = 3,
            BringAFriend = 4,
            TerritoryCapture = 5,
            BossHunt = 6,
            KingOfTheHill = 7,
            CoreRun = 8,
        }

        // Time window, e.g. 14:00..22:00
        private static readonly TimeSpan _startTime = new TimeSpan(14, 0, 0);
        private static readonly TimeSpan _endTime = _startTime.Add(TimeSpan.FromHours(8));
        private const int _checkInterval = 30_000; // 30 seconds
        private const int _saveDelay = 5_000;
        private static RegionTimer _timer;
        private RegionTimer _saveTimer;
        private DateTime _startedTime = DateTime.Now;
        
        private static readonly int[] _randomEmblems = { 5061, 6645, 84471, 6272, 55302, 64792, 111402, 39859, 21509, 123019 };

        public ImmutableArray<int> DefaultEmblems => _randomEmblems.ToImmutableArray();

        public int GetEmblemForPlayer(GamePlayer player)
        {
            if (player.Guild != null)
                return player.Guild.Emblem;

            return GetPlayerSoloEmblem(player);
        }

        public int GetPlayerSoloEmblem(GamePlayer player)
        {
            // Quick hash so that a player always gets the same emblem per session
            UInt64 hashedValue = 3074457345618258791ul;
            hashedValue += (ulong)_startedTime.Ticks;
            for(int i = 0; i < player.Name.Length; i++)
            {
                hashedValue += player.Name[i];
                hashedValue *= 3074457345618258799ul;
            }
            var idx = ((uint)hashedValue) % _randomEmblems.Length;
            return _randomEmblems[idx];
        }

        private bool _isOpen;
        private bool _isForcedOpen;
        private ushort _currentRegionID;
        [NotNull] private List<Zone> _zones = new();

        private const int _defaultGuildRank = 9;

        // King of the Hill Variables
        private KotHBanner _activeHill;
        private RegionTimer _kothGameLoop;
        private List<Spawn> _kothPotentialSpawns = new List<Spawn>();
        private Queue<Spawn> _kothRotationQueue = new Queue<Spawn>();
        private const int KOTH_TICK_RATE = 8000;
        private long _kothNextMoveTick;
        private long _kothOwnershipStartTick;
        private const int MAP_MARKER_ID = 9999;

        // --- Core Run Variables ---
        private RegionTimer _coreRunCycleTimer;
        private RegionTimer _coreRunMovementTimer;
        private GameNPC _coreRunAnchorNPC;
        private bool _isCoreRunRedLight = false;
        private Area.Tore _coreRunToreArea;
        private Area.Circle _coreRunCenterSafeZone;
        private Dictionary<string, Coordinate> _coreRunPlayerSnapshots = new Dictionary<string, Coordinate>();
        private const string EVENT_ID_REDLIGHT = "EVT_REDLIGHT";
        private const int CORE_RUN_MOVEMENT_TOLERANCE = 50;
        private const int CORE_RUN_SAFE_RADIUS = 1200;

        /// <summary>
        /// "Group" as it pertains to PvP sessions.
        ///
        /// When a player leaves their group, they:
        /// - Stop gaining score for that group.
        /// - Start gaining solo score.
        /// - Stay associated with this group until they join another one. Score is then best of solo or group.
        ///
        /// When a player CREATES a group, they:
        /// - Copy their current solo score to the group.
        /// - Same as joining the group.
        ///
        /// When a player joins a group, they:
        /// - 
        /// </summary>
        public class PvPGroup
        {
            private GamePlayer? _soloPlayer;
            private Guild? _guild;

            public GamePlayer? SoloPlayer
            {
                get => _soloPlayer;
                set
                {
                    if (value != null)
                        Debug.Assert(_guild == null);
                    _soloPlayer = value;
                }
            }

            public Guild? Guild
            {
                get => _guild;
                set
                {
                    if (value != null)
                        Debug.Assert(_soloPlayer == null);
                    _guild = value;
                }
            }

            public bool IsGuild => Guild != null;
            public bool IsPlayer => SoloPlayer != null;
        }

        public record Spawn(GameNPC? NPC, Position Position)
        {
            public Spawn(): this(null, Position.Nowhere) { }
            public Spawn(GameNPC npc) : this(npc, Position.Nowhere) { }
            public Spawn(Position pos) : this(null, pos) { }
        }

        /// <summary>The chosen session from DB for the day</summary>
        private PvpSession? _activeSession;
        [NotNull] private readonly object _sessionLock = new object();

        // Scoreboard
        // Total score for each player. DO NOT USE THIS FOR ANYTHING, only for stat display.
        [NotNull] private readonly Dictionary<string, PvPScore> _totalScores = new Dictionary<string, PvPScore>();
        // Total score earned WHILE SOLO for each player.
        [NotNull] private readonly Dictionary<string, PvPScore> _soloScores = new Dictionary<string, PvPScore>();
        // Total score earned WHILE GROUPED for each player.
        [NotNull] private readonly Dictionary<Guild, PvPGroupScore> _groupScores = new Dictionary<Guild, PvPGroupScore>();

        // Queues
        [NotNull] private readonly List<GamePlayer> _soloQueue = new List<GamePlayer>();
        [NotNull] private readonly List<GamePlayer> _groupQueue = new List<GamePlayer>();

        // For realm-based spawns
        [NotNull] private readonly Dictionary<eRealm, List<GameNPC>> _spawnNpcsRealm = new Dictionary<eRealm, List<GameNPC>>() { { eRealm.Albion, new List<GameNPC>() }, { eRealm.Midgard, new List<GameNPC>() }, { eRealm.Hibernia, new List<GameNPC>() }, };
        // For random spawns (all spawns in session's zones)
        [NotNull] private Dictionary<string, GameNPC> _spawnNpcsGlobal = new Dictionary<string, GameNPC>();
        // For "RandomLock" so we don't reuse the same spawn
        [NotNull] private readonly HashSet<GameNPC> _usedSpawns = new HashSet<GameNPC>();
        // Here we track spawns for players & groups
        [NotNull] private readonly ReaderWriterDictionary<string, Spawn> _playerSpawns = new ReaderWriterDictionary<string, Spawn>();
        [NotNull] private readonly ReaderWriterDictionary<Guild, Spawn> _groupSpawns = new ReaderWriterDictionary<Guild, Spawn>();
        // Here we track solo-based safe areas (player => area)
        [NotNull] private readonly ReaderWriterDictionary<string, AbstractArea> _soloAreas = new ReaderWriterDictionary<string, AbstractArea>();
        // And group-based safe areas (group => area)
        [NotNull] private readonly ReaderWriterDictionary<Guild, AbstractArea> _groupAreas = new ReaderWriterDictionary<Guild, AbstractArea>();
        // Key = the group object, Value = the ephemeral guild we created
        [NotNull] private readonly ReaderWriterDictionary<Group, Guild> _groupGuilds = new ReaderWriterDictionary<Group, Guild>();
        [NotNull] private readonly Dictionary<Guild, Group> _guildGroups = new Dictionary<Guild, Group>();
        [NotNull] private readonly object _groupsLock = new object();
        // Grace timer for PvP players who get linkdead so they don't lose their progress
        [NotNull] private readonly ReaderWriterDictionary<string, RegionTimer> _graceTimers = new();
        [NotNull] private readonly List<Guild> _allGuilds = new();
        [NotNull] private readonly List<GameFlagBasePad> _allBasePads = new List<GameFlagBasePad>();
        // Keep track of last guild of each player for scores to protect from griefing
        [NotNull] private readonly Dictionary<string, Guild> _playerLastGuilds = new();
        private static readonly Dictionary<string, long> _bossDamageAccumulator = new Dictionary<string, long>();

        private int _flagCounter = 0;
        private RegionTimer _territoryOwnershipTimer = null;

        #region Singleton
        [NotNull] public static PvpManager Instance { get; } = new PvpManager();

        private bool CreateAreas => _activeSession is { CreateCustomArea: true } && _isUniqueSpawns();
        
        public eSessionTypes CurrentSessionType { get => IsOpen ? (eSessionTypes)(_activeSession?.SessionType ?? 0) : eSessionTypes.None; }

        /// <summary>
        /// Called when a PvP linkdead player’s grace period expires.
        /// The callback removes the player from the PvP session (using the same cleanup as for quitting)
        /// and disconnects the client.
        /// </summary>
        protected int LinkdeathPvPGraceCallback(GamePlayer player, RegionTimer timer)
        {
            if (log.IsInfoEnabled)
                log.InfoFormat("PvP grace period expired for linkdead player {0}({1}). Removing from PvP.", player.Name, player.Client.Account.Name);

            _graceTimers.Remove(player.InternalID);

            if (player.ObjectState == GameObject.eObjectState.Active)
                PvpManager.Instance.KickPlayer(player);
            else
                PvpManager.Instance.CleanupPlayer(player);
            
            return 0;
        }

        public static bool CanGroup(GamePlayer source, GamePlayer target, bool quiet = false)
        {
            if (!source.IsInPvP && !target.IsInPvP)
                return true;

            if (Instance.CurrentSession?.AllowGroupDisbandCreate != true)
            {
                if (!quiet)
                {
                    // Commands.Players.Invite.NotAllowed
                    source.SendTranslatedMessage(
                        "Commands.Players.Invite.NotAllowed",
                        eChatType.CT_Important, eChatLoc.CL_SystemWindow
                    );
                }
                return false;
            }

            if (!Instance.AllowsGroups)
            {
                if (!quiet)
                {
                    source.SendTranslatedMessage(
                        "PvPManager.MaxGroup",
                        eChatType.CT_Important, eChatLoc.CL_SystemWindow
                    );
                }
                return false;
            }

            if (Instance.MaxGroupSize > 0 && Instance.MaxGroupSize <= (source.Group?.MemberCount ?? 1))
            {
                if (!quiet)
                {
                    source.SendTranslatedMessage(
                        "PvPManager.MaxGroup",
                        eChatType.CT_Important, eChatLoc.CL_SystemWindow
                    );
                }
                return false;
            }
            return true;
        }

        public void PlayerLinkDeath(GamePlayer player)
        {
            if (!IsOpen)
                return;
            
            int gracePeriodMs = 20 * 60 * 1000;
            var timerRegion = WorldMgr.GetRegion(1);
            var timer = new RegionTimer(timerRegion.TimeManager);

            _graceTimers[player.InternalID] = timer;
            
            if (CurrentSessionType is eSessionTypes.CaptureTheFlag)
            {
                // Remove any FlagInventoryItem items from the player's backpack
                int totalFlagRemoved = 0;
                for (eInventorySlot slot = eInventorySlot.FirstBackpack;
                     slot <= eInventorySlot.LastBackpack;
                     slot++)
                {
                    InventoryItem item = player.Inventory.GetItem(slot);
                    if (item is FlagInventoryItem flag)
                    {
                        int flagCount = flag.Count;
                        if (flag.DropFlagOnGround(player, null))
                        {
                            totalFlagRemoved += flagCount;
                        }
                    }
                }
            }
            
            timer.Callback = (t) => LinkdeathPvPGraceCallback(player, t);
            timer.Start(1 + gracePeriodMs);
            if (log.IsInfoEnabled)
                log.InfoFormat("Linkdead PvP player {0}({1}) will be removed in {2} minutes if not reconnected.", player.Name, player.Client.Account.Name, gracePeriodMs / 60000);
        }

        [ScriptLoadedEvent]
        public static void OnServerStart(DOLEvent e, object sender, EventArgs args)
        {
            log.Info("PvpManager: Loading or Starting...");

            // Create the timer in region 1 for the open/close checks
            var region = WorldMgr.GetRegion(1);
            if (region != null)
            {
                _timer = new RegionTimer(region.TimeManager);
                _timer.Callback = Instance.TickCheck;
                _timer.Start(10_000); // start after 10s
                Instance._saveTimer = new RegionTimer(region.TimeManager);
                Instance._saveTimer.Callback = _ =>
                {
                    Instance._SaveScore();
                    return 0;
                };
            }
            else
            {
                log.Warn("PvpManager: Could not find Region(1) for timer!");
            }

            // Load the DB sessions
            PvpSessionMgr.ReloadSessions();

            if (File.Exists("temp/PvPScore.dat"))
            {
                // Reopen saved session
                Instance.Open(string.Empty, false);
            }
            
            GameEventMgr.AddHandler(GamePlayerEvent.GameEntered, Instance.OnPlayerLogin);
        }

        [ScriptUnloadedEvent]
        public static void OnServerStop(DOLEvent e, object sender, EventArgs args)
        {
            log.Info("PvpManager: Stopping...");
            _timer?.Stop();

            GameEventMgr.RemoveHandler(GamePlayerEvent.GameEntered, Instance.OnPlayerLogin);
        }

        private void OnPlayerLogin(DOLEvent e, object sender, EventArgs args)
        {
            var player = sender as GamePlayer;
            if (player == null) return;

            // check if we have an RvrPlayer row with a pvp session
            RvrPlayer rec = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(player.InternalID));
            if (rec == null || string.IsNullOrEmpty(rec.PvPSession)) return;
            
            // Remove any FlagInventoryItem items from the player's backpack
            int totalFlagRemoved = 0;
            for (eInventorySlot slot = eInventorySlot.FirstBackpack;
                 slot <= eInventorySlot.LastBackpack;
                 slot++)
            {
                InventoryItem item = player.Inventory.GetItem(slot);
                if (item is FlagInventoryItem flag)
                {
                    int flagCount = flag.Count;
                    if (player.Inventory.RemoveItem(item))
                    {
                        totalFlagRemoved += flagCount;
                    }
                }
            }

            lock (_sessionLock)
            {
                if (IsOpen && _activeSession?.SessionID == rec.PvPSession)
                {
                    if (TryRestorePlayer(player, rec))
                        return;
                }
                _cleanupPlayer(player);
                RestorePlayerData(player, rec);
            }
        }

        public void OnPlayerQuit(GamePlayer player)
        {
            if (player?.IsInPvP != true)
                return;

            bool ignore = false;
            _graceTimers.FreezeWhile((d) =>
            {
                if (d.TryGetValue(player.InternalID, out RegionTimer timer))
                {
                    if (timer.IsAlive)
                    {
                        ignore = true;
                    }
                    else
                    {
                        d.Remove(player.InternalID);
                    }
                }
            });
            
            if (ignore)
                return;
            
            CleanupPlayer(player);
            
            RvrPlayer record = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(player.InternalID));
            if (record != null)
            {
                var dbCharacter = player.DBCharacter;
                dbCharacter.Region = record.OldRegion;
                dbCharacter.Xpos = record.OldX;
                dbCharacter.Ypos = record.OldY;
                dbCharacter.Zpos = record.OldZ;
                dbCharacter.BindRegion = record.OldBindRegion;
                dbCharacter.BindXpos = record.OldBindX;
                dbCharacter.BindYpos = record.OldBindY;
                dbCharacter.BindZpos = record.OldBindZ;
                dbCharacter.BindHeading = record.OldBindHeading;
            }
        }

        public void OnMemberJoinGuild(Guild guild, GamePlayer player)
        {
            lock (_sessionLock) // lock this to make sure we don't close the pvp while we're adding the player, this would be bad...
            {
                AddToGuildGroup(guild, player);
                SaveScores();
            }
        }

        public void OnMemberLeaveGuild(Guild guild, GamePlayer player)
        {
            if (player.Group == null) // No group; likely we came here from RemoveFromGroupGuild calling guild.RemoveMember
                return;
            
            lock (_sessionLock) // lock this to make sure we don't close the pvp while we're adding the player, this would be bad...
            {
                RemoveFromGuildGroup(guild, player);
                SaveScores();
            }
        }

        public void OnMemberJoinGroup(Group group, GamePlayer player)
        {
            lock (_sessionLock) // lock this to make sure we don't close the pvp while we're adding the player, this would be bad...
            {
                if (player.ActiveBanner is PvPFlagBanner { Item: FlagInventoryItem flagItem })
                {
                    flagItem.OnJoinGroup(player, group);
                }
                
                AddToGroupGuild(group, player);
                SaveScores();
            }
        }

        public void OnMemberLeaveGroup(Group group, GamePlayer player)
        {
            lock (_sessionLock) // lock this to make sure we don't close the pvp while we're adding the player, this would be bad...
            {
                if (player.ActiveBanner is PvPFlagBanner { Item: FlagInventoryItem flagItem })
                {
                    flagItem.OnLeaveGroup(player, group);
                }
                
                RemoveFromGroupGuild(group, player);
                SaveScores();
            }
        }

        public void SaveScores()
        {
            if (IsOpen && !_saveTimer.IsAlive)
                _saveTimer.Start(_saveDelay);
        }
        
        public void OnGuildRename(Guild guild)
        {
            if (!_groupAreas.TryGetValue(guild, out AbstractArea area))
                return;

            if (area is PvpTempArea pvpArea)
                pvpArea.OwnerGuild = guild; // Refresh names
        }

        private Guild CreateGuild(string guildName, GamePlayer leader = null)
        {
            var pvpGuild = GuildMgr.CreateGuild(eRealm.None, guildName, leader, true, true);
            if (pvpGuild == null)
            {
                pvpGuild = GuildMgr.GetGuildByName(guildName);
                if (pvpGuild == null)
                {
                    log.Error($"Failed to create or find PvP guild \"{guildName}\"");
                    return null;
                }
                if (pvpGuild.GuildType != Guild.eGuildType.PvPGuild)
                {
                    log.Error($"Guild \"{guildName}\" already exists and is not PvP, aborting");
                    return null;
                }
                log.Warn($"PvP Guild {guildName} already exists, hijacking it");
            }
            pvpGuild.GuildType = Guild.eGuildType.PvPGuild;
            pvpGuild.SaveIntoDatabase();
            return pvpGuild;
        }

        private Guild CreateGuild(GamePlayer leader)
        {
            var guild =  CreateGuild("[PVP] " + leader.Name + "'s guild", leader);
            guild.Emblem = GetPlayerSoloEmblem(leader);
            return guild;
        }

        private Guild CreateGuildForGroup(Group group)
        {
            var groupLeader = group.Leader;
            if (groupLeader is null)
            {
                groupLeader.Out.SendMessage(LanguageMgr.GetTranslation(groupLeader.Client.Account.Language, "PvPManager.CannotCreatePvPGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return null;
            }
            
            var pvpGuild = CreateGuild(groupLeader);
            if (pvpGuild == null)
            {
                groupLeader.Out.SendMessage(LanguageMgr.GetTranslation(groupLeader.Client.Account.Language, "PvPManager.CannotCreatePvPGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return null;
            }

            _allGuilds.Add(pvpGuild);
            _groupGuilds[group] = pvpGuild;
            _guildGroups[pvpGuild] = group;
            var groupScore = new PvPGroupScore(pvpGuild);
            _groupScores[pvpGuild] = groupScore;

            if (_soloAreas.Remove(groupLeader.InternalID, out AbstractArea leaderArea))
            {
                _groupAreas.Swap(pvpGuild, leaderArea);
            }
            if (_playerSpawns.Remove(groupLeader.InternalID, out Spawn spawn))
            {
                _groupSpawns.Add(pvpGuild, spawn);
            }

            // Add each member to the ephemeral guild
            foreach (var member in group.GetPlayersInTheGroup())
            {
                var rank = pvpGuild.GetRankByID(member == groupLeader ? 0 : _defaultGuildRank);
                pvpGuild.AddPlayer(member, rank);

                member.IsInPvP = true;
                member.SaveIntoDatabase();
                
                // Merge/transfer scores where it makes sense
                if (Properties.PVPSESSION_TREASURE_TRANSFER_ITEMS && CurrentSessionType is eSessionTypes.TreasureHunt)
                {
                    if (_soloScores.TryGetValue(member.InternalID!, out PvPScore value))
                    {
                        groupScore!.GetOrCreateScore(member).TakeItems(value, true); // COPY here, for contribution
                        groupScore.TakeItems(value); // TAKE here, for the chest
                    }
                }
                
                // Remove all solo areas - we've already moved the leader's area & spawn into group storage, so they won't be found
                _cleanupArea(member);
                _freeSpawn(member);
            }
            
            if (leaderArea is PvpTempArea pvpArea)
                pvpArea.SetOwnership(groupLeader);

            return pvpGuild;
        }

        /// <summary>
        /// Add a player to the guild of an existing group.
        /// Used when using /invite on a player.
        /// </summary>
        /// <param name="group">Group the player is being added to</param>
        /// <param name="player">Player to add</param>
        private void AddToGroupGuild(Group group, GamePlayer player)
        {
            if (!IsOpen)
                return;
            
            bool isNew = true;
            Guild guild = null;
            if (_groupGuilds.TryGetValue(group, out guild))
            {
                // Guild already exists
                if (guild == player.Guild)
                    return; // Nothing to do
                
                guild.AddPlayer(player, guild.GetRankByID(_defaultGuildRank), true);
                if (_groupSpawns.TryGetValue(guild, out Spawn spawn))
                {
                    _freeSpawn(player); // Remove player solo spawn
                    UpdatePvPState(player, spawn); // Update PvP DB record for state recovery
                    player.Bind(spawn.Position);
                }
                if (_groupAreas.ContainsKey(guild))
                {
                    // Remove player solo area
                    _cleanupArea(player);
                }

                if (!_groupScores.TryGetValue(guild, out PvPGroupScore groupScore))
                {
                    groupScore = new PvPGroupScore(guild);
                    _groupScores[guild] = groupScore;
                }
                
                // Merge/transfer scores where it makes sense
                if (Properties.PVPSESSION_TREASURE_TRANSFER_ITEMS && CurrentSessionType is eSessionTypes.TreasureHunt)
                {
                    if (_soloScores.TryGetValue(player.InternalID!, out PvPScore value))
                    {
                        groupScore!.GetOrCreateScore(player).TakeItems(value, true); // COPY here, for contribution
                        groupScore.TakeItems(value); // TAKE here, for the chest
                    }
                }
            }
            else
            {
                // Guild needs to be created
                guild = CreateGuildForGroup(group);
                // Guild needs to be created
                if (guild == null)
                    return;
                
                guild.Emblem = GetPlayerSoloEmblem(player);
                if (_groupSpawns.TryGetValue(guild, out Spawn spawn))
                {
                    foreach (var member in group.GetPlayersInTheGroup())
                    {
                        // Update PvP DB record for state recovery
                        UpdatePvPState(member, spawn);
                        member.Bind(spawn.Position);
                    }
                }
            }
        }

        /// <summary>
        /// Remove a player from a group's guild.
        /// Used when the player disbands or is kicked.
        /// </summary>
        /// <param name="group">Group to remove the player from</param>
        /// <param name="player">Player to remove</param>
        private void RemoveFromGroupGuild(Group group, GamePlayer player)
        {
            if (!IsOpen)
                return;
            
            if (player.Guild == null) // No guild; likely we came here through RemoveFromGuildGroup calling group.RemovePlayer
                return;

            if (!_groupGuilds.TryGetValue(group, out var guild))
                return;
            
            if (!guild.RemovePlayer(string.Empty, player))
                return;
            
            // Check if *all* members have left. If so, remove area:
            if (!guild.GetListOfOnlineMembers().Any(m => m.IsInPvP))
            {
                _cleanupArea(guild);
                _freeSpawn(guild);
            }

            PlayerGroupToSolo(player);
        }

        /// <summary>
        /// Add a player to the group of an existing guild.
        /// Used on relog or when inviting a player to the guild.
        /// </summary>
        /// <param name="guild">Guild to add the player to</param>
        /// <param name="player">Player to add</param>
        private void AddToGuildGroup(Guild guild, GamePlayer player)
        {
            if (!IsOpen)
                return;

            if (player.Guild != guild)
            {
                log.ErrorFormat("{0} called with player.Guild != guild, add the player to the guild first!", MethodBase.GetCurrentMethod());
                return;
            }

            Group group = null;
            if (_guildGroups.TryGetValue(guild, out group))
            {
                // Group already exists
                if (group.IsInTheGroup(player))
                    return; // Nothing to do
                
                group.AddMember(player);
                _freeSpawn(player);
                _cleanupArea(player);
                if (_groupSpawns.TryGetValue(guild, out Spawn guildSpawn))
                {
                    // Update DB record with guild spawn, for state recovery
                    UpdatePvPState(player, guildSpawn);
                    player.Bind(guildSpawn.Position);
                }
                if (player.Guild != group.Leader.Guild)
                {
                    log.Warn($"Player {player.Name} ({player.InternalID}) was added to group led by {group.Leader} ({group.Leader.InternalID}) but they have different guilds!");
                }
                else if (player.GuildRank.RankLevel < group.Leader.GuildRank.RankLevel)
                {
                    group.MakeLeader(player);
                }
            }
            else
            {
                // Guild needs to be created
                
                // There is a race condition here maybe?
                // If two players log in at the same time, TryGetValue up there can maybe return false in both cases, and this can run twice...
                GamePlayer leader = player;
                if (_soloAreas.Remove(leader.InternalID, out AbstractArea leaderArea))
                {
                    _groupAreas.Add(guild, leaderArea);
                    if (leaderArea is PvpTempArea pvpArea)
                        pvpArea.SetOwnership(leader);
                }
                if (_playerSpawns.TryRemove(leader.InternalID, out Spawn spawn))
                {
                    _groupSpawns.TryAdd(guild, spawn);
                }
                
                if (guild.MemberOnlineCount > 1)
                {
                    group = new Group(player);
                    _groupGuilds[group] = guild;
                    _guildGroups[guild] = group;

                    foreach (var member in guild.GetListOfOnlineMembers())
                    {
                        group.AddMember(member);
                        if (member.GuildRank.RankLevel < leader.GuildRank.RankLevel)
                            leader = member;
                        _cleanupArea(member);
                        _freeSpawn(member);
                        if (spawn != null)
                        {
                            // Update DB record with guild spawn, for state recovery
                            UpdatePvPState(player, spawn);
                            player.Bind(spawn.Position);
                        }
                    }

                    if (leader != player)
                        group.MakeLeader(leader);
                }
            }
        }

        /// <summary>
        /// Remove a player from an existing guild's group.
        /// </summary>
        /// <param name="guild">Guild to remove the player from</param>
        /// <param name="player">Player to remove</param>
        private void RemoveFromGuildGroup(Guild guild, GamePlayer player)
        {
            if (!IsOpen || guild == null)
                return;

            if (player.Guild != null)
            {
                log.ErrorFormat("{0} called with player.Guild != null, remove the player from the guild first!", MethodBase.GetCurrentMethod());
                return;
            }

            if (!_guildGroups.TryGetValue(guild, out var group))
                return;

            if (!group.RemoveMember(player))
                return;

            // Check if *all* members have left. If so, remove area:
            if (!guild.GetListOfOnlineMembers().Any(m => m.IsInPvP))
            {
                _cleanupArea(guild);
                _freeSpawn(guild);
            }

            PlayerGroupToSolo(player);

        }

        private void PlayerGroupToSolo(GamePlayer player)
        { 
            if (AllowsSolo)
            {
                Spawn? sp = FindSpawnPosition(player.Realm);
                if (sp == null)
                {
                    player.SendTranslatedMessage("PvPManager.NoSpawn");
                    KickPlayer(player, true);
                    return;
                }
                _playerSpawns[player.InternalID] = sp;
                CreateSafeAreaForSolo(player, sp.Position, _activeSession.TempAreaRadius);
                UpdatePvPState(player, sp); // Update PvP DB record for state recovery
                
                // Remove any owned flags before teleporting to new safe area, or they'll capture the flag instantly
                int totalRemoved = RemoveItemsFromPlayer(player);
                if (totalRemoved > 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.PvPTreasureRemoved", totalRemoved), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }

                player.MoveTo(sp.Position);
                player.Bind(true);
            }
            else
                KickPlayer(player, true);
        }
        
        private bool TryRestorePlayer(GamePlayer player, RvrPlayer rec)
        {
            bool wasLinkDead = false;
            RegionTimer graceTimer;
            if (_graceTimers.Remove(player.InternalID, out graceTimer))
            {
                wasLinkDead = true;
                graceTimer.Stop();
            }

            if (!_zones.Contains(player.CurrentZone))
            {
                return false;
            }

            Spawn? spawn;
            if (player.Guild != null)
            {
                if (player.Guild.GuildType == Guild.eGuildType.PvPGuild)
                {
                    if (AllowsGroups)
                    {
                        AddToGuildGroup(player.Guild, player);
                    }
                    else
                    {
                        // DB changed?
                        player.Guild.RemovePlayer("PVP", player);
                        return false;
                    }
                }
                else
                {
                    log.Warn($"Player {player.Name} ({player.InternalID}) logged into PvP with non-PvP guild {player.Guild.Name} ({player.Guild.GuildID})");
                    // So, we have a RvrRecord in DB, but this player has a non-pvp guild?
                    // Regardless, do nothing, just kick from PvP because something is very wrong

                    if (player.Client.Account.PrivLevel < 2) // Unless this is a GM doing some work?
                    {
                        player.Out.SendMessage("You have been kicked from the PvP group.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }
                }

                spawn = _tryRestoreGroupArea(player, player.Guild, rec);
                if (spawn == null)
                {
                    spawn = FindSpawnPosition(player.Guild.Realm);
                    if (spawn == null)
                    {
                        player.SendTranslatedMessage("PvPManager.NoSpawn");
                        return false;
                    }
                    
                    _groupSpawns[player.Guild] = spawn;
                    if (CreateAreas)
                        CreateSafeAreaForGroup(player, spawn.Position, _activeSession.TempAreaRadius);
                }
            }
            else
            {
                if (!AllowsSolo && player.Guild  == null)
                {
                    player.Out.SendMessage("You have been kicked from the PvP group.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                spawn = _tryRestorePlayerArea(player, rec);
                if (spawn == null)
                {
                    spawn = FindSpawnPosition(player.Realm);
                    if (spawn == null)
                    {
                        player.SendTranslatedMessage("PvPManager.NoSpawn");
                        return false;   
                    }
                    
                    _playerSpawns[player.InternalID] = spawn;
                    if (CreateAreas)
                        CreateSafeAreaForSolo(player, spawn.Position, _activeSession.TempAreaRadius);
                }
            }

            if (!wasLinkDead)
            {
                // Move player to safe place
                RemoveItemsFromPlayer(player); // But remove any special items first
                player.MoveTo(spawn.Position);
            }
            
            player.IsInPvP = true;
            player.Out.SendMessage("Welcome back! Your PvP state has been preserved.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        private Spawn? _tryRestoreGroupArea(GamePlayer player, Guild guild, RvrPlayer rec)
        {
            lock (_groupSpawns)
            {
                Spawn? spawn;
                if (_groupSpawns.TryGetValue(guild, out spawn))
                    return spawn;
            
                GameNPC? spawnNpc = string.IsNullOrEmpty(rec.PvPSpawnNPC) ? null : _spawnNpcsGlobal.GetValueOrDefault(rec.PvPSpawnNPC);
                spawn = new Spawn(spawnNpc, Position.Create(_currentRegionID, rec.PvPSpawnX, rec.PvPSpawnY, rec.PvPSpawnZ));
                if (_isUniqueSpawns())
                {
                    if (spawnNpc == null)
                    {
                        // Not sure how this could happen here - maybe the npc was deleted inbetween
                        return null;
                    }
                    if (!_usedSpawns.Add(spawnNpc))
                    {
                        // Someone else logged in and got this location
                        return null;
                    }
                }
            
                _groupSpawns[guild] = spawn;
                if (CreateAreas)
                {
                    CreateSafeAreaForGroup(player, spawn.Position, _activeSession.TempAreaRadius);
                }
                return spawn;
            }
        }

        private Spawn? _tryRestorePlayerArea(GamePlayer player, RvrPlayer rec)
        {
            lock (_playerSpawns)
            {
                Spawn? spawn;
                if (_playerSpawns.TryGetValue(player.InternalID, out spawn))
                    return spawn;
            
                GameNPC? spawnNpc = string.IsNullOrEmpty(rec.PvPSpawnNPC) ? null : _spawnNpcsGlobal.GetValueOrDefault(rec.PvPSpawnNPC);
                spawn = new Spawn(spawnNpc, Position.Create(player.CurrentZone.ZoneRegion.ID, rec.PvPSpawnX, rec.PvPSpawnY, rec.PvPSpawnZ));
                if (_isUniqueSpawns())
                {
                    if (spawnNpc == null)
                    {
                        // Not sure how this could happen here - maybe the npc was deleted inbetween
                        return null;
                    }
                    if (!_usedSpawns.Add(spawnNpc))
                    {
                        // Someone else logged in and got this location
                        return spawn;
                    }
                }
            
                _playerSpawns[player.InternalID] = spawn;
                if (CreateAreas)
                {
                    CreateSafeAreaForSolo(player, spawn.Position, _activeSession.TempAreaRadius);
                }
                return spawn;
            }
        }

        #endregion

        private PvpManager()
        {
            _isOpen = false;
        }

        #region Timer Check
        private int TickCheck(RegionTimer timer)
        {
            if (!_isOpen)
            {
                if (DateTime.Now.TimeOfDay >= _startTime && DateTime.Now.TimeOfDay < _endTime)
                {
                    Open(string.Empty, false);
                }
            }
            else
            {
                if (!_isForcedOpen && (DateTime.Now.TimeOfDay < _startTime || DateTime.Now.TimeOfDay > _endTime))
                {
                    Close();
                }
            }
            return _checkInterval;
        }
        #endregion

        #region Public Properties
        public bool IsOpen => _isOpen;
        public PvpSession CurrentSession => _activeSession;
        [NotNull] public IReadOnlyList<Zone> CurrentZones => _zones.AsReadOnly();
        public string CurrentSessionId => string.IsNullOrEmpty(CurrentSession?.SessionID) ? "(none)" : CurrentSession.SessionID;
        public bool AllowsGroups => CurrentSession?.GroupCompoOption is 2 or 3;
        public bool AllowsSolo => CurrentSession?.GroupCompoOption is 1 or 3;
        public int MaxGroupSize => !AllowsGroups ? 1 : CurrentSession?.GroupMaxSize ?? 0;
        #endregion

        #region Open/Close

        private void ParseGuildEntry(IEnumerator<string> lines, string[] parameters)
        {
            // g;Miuna's guild
            // f1999a9a-f590-453f-ac72-de09afa0c67a=PvP_SoloKills:1=PvP_SoloKillPoints:2
            // c3ceb30f-3441-4fda-abbe-755dc28d9e08
            //
            // g;Bob's guild
            // 2ef3beb7-11fc-4b2a-b2e7-4515275423e0=PvP_SoloKills:1=PvP_SoloKillPoints:2
            var guildName = parameters[1];
            Guild guild = null;
            if (!string.IsNullOrEmpty(guildName))
            {
                guild = GuildMgr.GetGuildByName(guildName) ?? CreateGuild(guildName);
            }
            if (guild == null)
            {
                log!.Warn($"Cannot recover PvP scores for guild \"{guildName}\", guild could not be found or created");
                return;
            }
            _allGuilds.Add(guild);
            var groupScore = EnsureGroupScore(guild);
            groupScore.Add(PvPScore.Parse(parameters.Skip(2), false));
            while (lines.MoveNext() && !string.IsNullOrEmpty(lines.Current))
            {
                var data = lines.Current.Split(';');
                
                var entry = PvPScore.Parse(data, false);
                var playerId = entry.PlayerID;
                groupScore.Scores![playerId] = entry;
                groupScore.Add(entry);
            }
        }

        public bool Open(string sessionID, bool force)
        {
            lock (_sessionLock)
            {
                _isForcedOpen = force;
                if (_isOpen)
                    return true;
            
                // Reset scoreboard, queues, oldInfos
                ResetScores();
                _soloQueue.Clear();
                _groupQueue.Clear();

                // Now we parse the session's ZoneList => find all "SPAWN" NPCs in those zones
                _zones.Clear();
                _spawnNpcsGlobal.Clear();
                _spawnNpcsRealm[eRealm.Albion].Clear();
                _spawnNpcsRealm[eRealm.Midgard].Clear();
                _spawnNpcsRealm[eRealm.Hibernia].Clear();
                _usedSpawns.Clear();
                _playerSpawns.Clear();
                _groupSpawns.Clear();
                _soloAreas.Clear();
                _groupAreas.Clear();

                try
                {
                    if (string.IsNullOrEmpty(sessionID))
                    {
                        try
                        {
                            sessionID = LoadScores();
                        }
                        catch (FileNotFoundException)
                        {
                            // fine
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                log.Error("Could not open file temp/PvPScore.dat: ", ex);
                                File.Move("temp/PvPScore.dat", $"temp/PvPScore-error-{DateTime.Now:yy-MM-dd.hh-mm-ss}.dat");
                            }
                            catch (Exception ex2)
                            {
                                log.Error("Could not move temp/PvPScore.dat after error: ", ex2);
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(sessionID))
                    {
                        // pick a random session from DB
                        _activeSession = PvpSessionMgr.PickRandomSession();
                        if (_activeSession == null)
                        {
                            log.Warn("No PvP Sessions in DB, cannot open!");
                            return false;
                        }
                    }
                    else
                    {
                        _activeSession = PvpSessionMgr.GetAllSessions().First(s => string.Equals(s.SessionID, sessionID));
                        if (_activeSession == null)
                        {
                            log.Warn($"PvP session {sessionID} could not be found, cannot open!");
                            _isOpen = false;
                            return false;
                        }
                    }

                    _isOpen = true;
                    _startedTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    log.Error($"Cannot open open session \"{sessionID}\": {ex}");
                    _isOpen = false;
                    _activeSession = null;
                    throw;
                }

                log.Info($"PvpManager: Opened session [{_activeSession.SessionID}] Type={CurrentSessionType}, SpawnOpt={_activeSession.SpawnOption}");

                List<GameNPC> padSpawnNpcsGlobal = new List<GameNPC>();

                var zoneStrings = _activeSession.ZoneList.Split(',');
                foreach (var zStr in zoneStrings)
                {
                    if (!ushort.TryParse(zStr.Trim(), out ushort zoneId))
                        continue;

                    Zone zone = WorldMgr.GetZone(zoneId);
                    if (zone == null) continue;

                    _zones.Add(zone);
                    if (_currentRegionID != 0 && zone.ZoneRegion.ID != _currentRegionID)
                    {
                        log.Error($"PvP zone session {_activeSession.SessionID} relies on zones of different regions {_currentRegionID} and {zone.ZoneRegion.ID} -- THINGS WILL BREAK");
                    }
                    _currentRegionID = zone.ZoneRegion.ID;
                    var npcs = WorldMgr.GetNPCsByGuild("PVP", eRealm.None).Where(n => n.CurrentZone == zone && n.Name.StartsWith("SPAWN", StringComparison.OrdinalIgnoreCase) &&
                                                                                     !n.Name.StartsWith("PADSPAWN", StringComparison.OrdinalIgnoreCase)).ToList();

                    _spawnNpcsGlobal = npcs.ToDictionary(n => n.InternalID);

                    // Also see if any are realm-labeled:
                    // e.g. "SPAWN-ALB", "SPAWN-MID", "SPAWN-HIB"
                    foreach (var npc in npcs)
                    {
                        if (npc.Name.IndexOf("SPAWN-ALB", StringComparison.OrdinalIgnoreCase) >= 0)
                            _spawnNpcsRealm[eRealm.Albion].Add(npc);
                        else if (npc.Name.IndexOf("SPAWN-MID", StringComparison.OrdinalIgnoreCase) >= 0)
                            _spawnNpcsRealm[eRealm.Midgard].Add(npc);
                        else if (npc.Name.IndexOf("SPAWN-HIB", StringComparison.OrdinalIgnoreCase) >= 0)
                            _spawnNpcsRealm[eRealm.Hibernia].Add(npc);
                    }

                    // Retrieve PADSPAWN NPCs separately (used solely for flag pads).
                    var padNpcs = WorldMgr.GetNPCsByGuild("PVP", eRealm.None)
                        .Where(n => n.CurrentZone == zone &&
                                   n.Name.StartsWith("PADSPAWN", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    padSpawnNpcsGlobal.AddRange(padNpcs);
                }

                // For Flag Capture sessions (SessionType == 2), create a flag pad at each PADSPAWN npc's position.
                if (CurrentSessionType is eSessionTypes.CaptureTheFlag)
                {
                    foreach (var padSpawnNPC in padSpawnNpcsGlobal)
                    {
                        var basePad = new GameFlagBasePad
                        {
                            Position = padSpawnNPC.Position,
                            Model = 2655,
                            Name = "BaseFlagPad",
                            FlagID = ++_flagCounter
                        };

                        basePad.AddToWorld();
                        _allBasePads.Add(basePad);
                    }
                }

                if (CurrentSessionType is eSessionTypes.BringAFriend)
                {
                    GameEventMgr.AddHandler(GameLivingEvent.Dying, new DOLEventHandler(OnLivingDying_BringAFriend));
                    GameEventMgr.AddHandler(GameLivingEvent.BringAFriend, new DOLEventHandler(OnBringAFriend));
                }
            
                // If we found zero spawns, the fallback logic in FindSpawnPosition might do random coords.
                // Or you can log a warning:
                if (_spawnNpcsGlobal.Count == 0)
                {
                    log.Warn("No 'SPAWN' NPCs found in the session's zones. We'll fallback to random coords.");
                }

                if (CurrentSessionType is eSessionTypes.TerritoryCapture)
                {
                    var reg = WorldMgr.GetRegion(1);
                    if (reg != null)
                    {
                        _territoryOwnershipTimer = new RegionTimer(reg.TimeManager);
                        _territoryOwnershipTimer.Callback = AwardTerritoryOwnershipPoints;
                        _territoryOwnershipTimer.Interval = 20_000;
                        _territoryOwnershipTimer.Start(10_000);
                    }
                }

                if (CurrentSessionType == eSessionTypes.KingOfTheHill)
                {
                    _kothPotentialSpawns.Clear();
                    _kothRotationQueue.Clear();

                    // Creates Hill Area around mobs named "KOTH_HILL".
                    foreach (var zone in _zones)
                    {
                        var hillMobs = WorldMgr.GetNPCsByGuild("PVP", eRealm.None)
                            .Where(n => n.CurrentZone == zone && n.Name.IndexOf("KOTH_HILL", StringComparison.OrdinalIgnoreCase) >= 0)
                            .ToList();

                        foreach (var npc in hillMobs)
                        {
                            _kothPotentialSpawns.Add(new Spawn(npc, npc.Position));
                        }
                    }

                    // Fallback if no KOTH_HILL mobs found: use player spawns
                    if (_kothPotentialSpawns.Count == 0)
                    {
                        log.Warn("No 'KOTH_HILL' mobs found. Falling back to player spawns for Hill locations.");
                        foreach (var npc in _spawnNpcsGlobal.Values)
                            _kothPotentialSpawns.Add(new Spawn(npc, npc.Position));
                    }

                    RefillKotHRotation();
                    SpawnNextHill();

                    // Fix: Create timer bound to the Hill object
                    if (_activeHill != null)
                    {
                        _kothGameLoop = new RegionTimer(_activeHill, KotHLoopCallback);
                        _kothGameLoop.Start(KOTH_TICK_RATE);
                    }
                }
            }
            return true;
        }

        private string LoadScores()
        {
            using var lines = File.ReadLines("temp/PvPScore.dat").GetEnumerator();
                    
            bool finished = !lines.MoveNext();
            if (finished)
                return string.Empty;
            
            var header = lines.Current.Split(';');
            var sessionID = header[0];
            bool.TryParse(header[1], out bool force);
            _isForcedOpen = force;

            log.Info($"Restoring PvP session {sessionID}");
            while (!finished)
            {
                var parameters = lines.Current.Split(';');
                switch (parameters[0])
                {
                    case "g":
                        {
                            // guild scores
                            ParseGuildEntry(lines, parameters);
                        }
                        break;
                        
                    case "s":
                        {
                            // solo scores
                            var playerScore = PvPScore.Parse(parameters.Skip(1), true);
                            if (!string.IsNullOrEmpty(playerScore.PlayerID))
                                _soloScores[playerScore.PlayerID] = playerScore;
                        }
                        break;
                        
                    case "ps":
                        {
                            // total scores
                            var playerScore = PvPScore.Parse(parameters.Skip(1), false);
                            if (!string.IsNullOrEmpty(playerScore.PlayerID))
                                _totalScores[playerScore.PlayerID] = playerScore;
                        }
                        break;
                }
                finished = !lines.MoveNext();
            }
            return sessionID;
        }

        public bool Close()
        {
            lock (_sessionLock)
            {
                if (!_isOpen)
                    return false;

                _isOpen = false;
                _isForcedOpen = false;

                log.InfoFormat("PvpManager: Closing session [{0}].", _activeSession?.SessionID);

                if (_activeSession != null && CurrentSessionType is eSessionTypes.TreasureHunt && _territoryOwnershipTimer != null)
                {
                    _territoryOwnershipTimer.Stop();
                    _territoryOwnershipTimer = null;
                }

                // Force remove all players still flagged IsInPvP
                foreach (var client in WorldMgr.GetAllPlayingClients())
                {
                    var plr = client?.Player;
                    if (plr != null && plr.IsInPvP)
                    {
                        KickPlayer(plr, false);
                    }
                }

                foreach (var pad in _allBasePads)
                {
                    pad.RemoveFlag();
                    pad.RemoveFromWorld();
                }
                _allBasePads.Clear();

                // remove all solo areas
                foreach (var kv in _soloAreas)
                {
                    var area = kv.Value;
                    if (area != null)
                    {
                        var region = kv.Value.Region;
                        region?.RemoveArea(area);
                    }
                }
                _soloAreas.Clear();

                // remove all group areas
                foreach (var kv in _groupAreas)
                {
                    var area = kv.Value;
                    if (area != null)
                    {
                        area.Region?.RemoveArea(area);
                    }
                }
                _groupAreas.Clear();

                if (CurrentSessionType is eSessionTypes.BringAFriend)
                {
                    GameEventMgr.RemoveHandler(GameLivingEvent.Dying, new DOLEventHandler(OnLivingDying_BringAFriend));
                    GameEventMgr.RemoveHandler(GameLivingEvent.BringAFriend, new DOLEventHandler(OnBringAFriend));

                    // For each zone in this session, find all FollowingFriendMob and Reset them
                    var zones = _activeSession.ZoneList.Split(',');
                    foreach (var zStr in zones)
                    {
                        if (!ushort.TryParse(zStr, out ushort zoneId)) continue;
                        var z = WorldMgr.GetZone(zoneId);
                        if (z == null) continue;

                        var allNpcs = WorldMgr.GetNPCsFromRegion(z.ZoneRegion.ID).Where(n => n.CurrentZone == z);
                        foreach (var npc in allNpcs)
                        {
                            if (npc is FollowingFriendMob ff)
                            {
                                ff.ResetFollow();
                            }
                        }
                    }
                }

                if (CurrentSessionType is eSessionTypes.TerritoryCapture)
                {
                    TerritoryManager.Instance.ReleaseSubTerritoriesInZones(CurrentZones);
                }

                if (_kothGameLoop != null)
                {
                    _kothGameLoop.Stop();
                    _kothGameLoop = null;
                }
                if (_activeHill != null)
                {
                    _activeHill.Delete();
                    _activeHill = null;
                }

                if (CurrentSessionType == eSessionTypes.KingOfTheHill)
                {
                    ClearKothMarker();
                }

                ResetScores();
                try
                {
                    File.Delete("temp/PvPScore.dat");
                }
                catch (FileNotFoundException)
                {
                    // fine
                }
                _activeSession = null;
                _soloQueue.Clear();
                _groupQueue.Clear();

                foreach (var value in _allGuilds)
                {
                    GuildMgr.DeleteGuild(value);
                }
                _groupGuilds.Clear();
                _guildGroups.Clear();
                return true;
            }
        }
        
        private void _SaveScore()
        {
            Directory.CreateDirectory("temp");

            Dictionary<Guild, PvPGroupScore> groupScores;
            Dictionary<string, PvPScore> playerScores;
            Dictionary<string, PvPScore> totalScores;
            bool forced;

            lock (_sessionLock)
            {
                if (!IsOpen)
                    return;

                groupScores = new(_groupScores.Select(gs => new KeyValuePair<Guild, PvPGroupScore>(gs.Key, new PvPGroupScore(gs.Value))));
                playerScores = new(_soloScores.Select(kv => new KeyValuePair<string, PvPScore>(kv.Key, kv.Value.Copy())));
                totalScores = new(_totalScores.Select(kv => new KeyValuePair<string, PvPScore>(kv.Key, kv.Value.Copy())));
                forced = _isForcedOpen;
            }

            var options = new FileStreamOptions();
            options.Mode = FileMode.Create;
            using StreamWriter file = File.CreateText("temp/PvPScore.dat");
            file.WriteLine($"{CurrentSession.SessionID};{forced}");
            file.WriteLine();
            foreach (var (guild, score) in groupScores)
            {
                file.Write($"g;{guild.Name};");
                file.Write(score.Serialize());
                file.WriteLine();
                foreach (var groupEntry in score.Scores)
                {
                    file.WriteLine(groupEntry.Value.Serialize());
                }
                file.WriteLine();
            }
            foreach (var (player, score) in playerScores)
            {
                file.WriteLine("s;" + score.Serialize());
            }
            foreach (var (player, score) in totalScores)
            {
                file.WriteLine("ps;" + score.Serialize());
            }
        }

        public PvpTempArea FindSafeAreaForTarget(GamePlayer player)
        {
            if (player == null) return null;

            // If solo
            if (player.Guild == null)
            {
                if (_soloAreas.TryGetValue(player.InternalID, out var soloArea))
                    return soloArea as PvpTempArea;
                return null;
            }
            else
            {
                // Group scenario
                if (_groupAreas.TryGetValue(player.Guild, out var groupArea))
                {
                    return groupArea as PvpTempArea;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns true if this player is in one of the zone(s) configured by the active session.
        /// </summary>
        public bool IsInActivePvpZone(GamePlayer player)
        {
            if (!_isOpen || _activeSession == null) return false;
            if (player == null || player.CurrentZone == null) return false;

            ushort zoneID = player.CurrentZone.ID;
            // parse the zone IDs from _activeSession.ZoneList
            var zoneStrs = _activeSession.ZoneList.Split(',');
            foreach (var zStr in zoneStrs)
            {
                if (ushort.TryParse(zStr, out ushort zId))
                {
                    if (zId == zoneID)
                        return true;
                }
            }
            return false;
        }
        #endregion

        #region Scoreboard
        /// <summary>
        /// Clear the _playerScores dictionary.
        /// </summary>
        private void ResetScores()
        {
            _totalScores.Clear();
            _soloScores.Clear();
            _groupScores.Clear();
        }

        [return: NotNullIfNotNull(nameof(guild))]
        public PvPGroupScore EnsureGroupScore(Guild? guild)
        {
            if (guild == null)
                return null;

            if (!_groupScores.TryGetValue(guild, out PvPGroupScore score))
            {
                score = new PvPGroupScore(guild);
                _groupScores[guild] = score;
            }
            return score;
        }

        public (PvPGroupScore score, PvPScore entry) EnsureGroupScoreEntry(GamePlayer player)
        {
            if (player?.Guild == null)
                return (null, null);

            PvPScore entry;
            if (!_groupScores.TryGetValue(player.Guild, out PvPGroupScore score))
            {
                entry = new PvPScore(player, false);
                score = new PvPGroupScore(player.Guild, [entry]);
                _groupScores[player.Guild] = score;
            }
            else if (!score.Scores.TryGetValue(player.InternalID, out entry))
            {
                entry = new PvPScore(player, false);
                score.Scores[player.InternalID] = entry;
            }
            return (score, entry);
        }
        
        [return: NotNullIfNotNull(nameof(player))]
        public PvPScore EnsureTotalScore(GamePlayer player)
        {
            if (player == null)
                return null;

            string pid = player.InternalID;
            if (!_totalScores.TryGetValue(pid, out PvPScore score))
            {
                score = new PvPScore(player, false);
                _totalScores[pid] = score;
            }
            return score;
        }
        
        [return: NotNullIfNotNull(nameof(player))]
        public PvPScore EnsureSoloScore(GamePlayer player)
        {
            if (player == null)
                return null;

            string pid = player.InternalID;
            if (!_soloScores.TryGetValue(pid, out PvPScore score))
            {
                score = new PvPScore(player, true);
                _soloScores[pid] = score;
            }
            return score;
        }

        public void HandleGroupKill(GamePlayer killer, GamePlayer victim, int points)
        {
            bool doSoloScores = Properties.PVPSESSION_GROUPKILLS_SOLOSCORES;
            PvPScore score = EnsureTotalScore(killer);
            score.PvP_GroupKills += 1;
            score.PvP_GroupKillsPoints += points;
                
            if (doSoloScores)
            {
                score = EnsureSoloScore(killer);
                score.PvP_GroupKills += 1;
                score.PvP_GroupKillsPoints += points;
            }

            var (groupScore, entry) = EnsureGroupScoreEntry(killer);
            if (groupScore != null)
            {
                groupScore.Totals.PvP_GroupKills += 1;
                groupScore.Totals.PvP_GroupKillsPoints += points;
                entry.PvP_GroupKills += 1;
                entry.PvP_GroupKillsPoints += points;
            }
            
            /*
            foreach (var member in killer.Group.GetMembersInTheGroup().OfType<GamePlayer>())
            {
                if (member == killer)
                    continue;
                    
                if (member.IsWithinRadius(victim, WorldMgr.MAX_EXPFORKILL_DISTANCE))
                {
                    score = EnsureTotalScore(member);
                    score.PvP_GroupKills += 1;
                    score.PvP_GroupKillsPoints += points;
                        
                    if (doSoloScores)
                    {
                        score = EnsureSoloScore(member);
                        score.PvP_GroupKills += 1;
                        score.PvP_GroupKillsPoints += points;
                    }

                    if (groupScore != null)
                    {
                        entry = groupScore.GetOrCreateScore(member);
                        entry.PvP_GroupKills += 1;
                        entry.PvP_GroupKillsPoints += points;
                    }
                }
            }
            */
        }

        private void AwardScore(GamePlayer pl, Action<PvPScore> fun, bool forceSolo = false)
        {
            var score = EnsureTotalScore(pl);
            
            fun(score);
            var (groupScore, playerEntry) = EnsureGroupScoreEntry(pl);
            if (groupScore != null)
            {
                fun(groupScore.Totals);
                fun(playerEntry);
            }
            if (groupScore == null || forceSolo)
            {
                score = EnsureSoloScore(pl);
                fun(score);
            }
        }
        
        public void AwardCTFCarrierKill(GamePlayer killerPlayer, GamePlayer carrier)
        {
            AwardScore(killerPlayer, (score =>
            {
                score.Flag_KillFlagCarrierCount++;
                score.Flag_KillFlagCarrierPoints += 6;
            }));
        }

        public void HandlePlayerKill(GamePlayer killer, GamePlayer victim)
        {
            if (!_isOpen || _activeSession == null) return;

            if (!killer.IsInPvP || !victim.IsInPvP) return;
            
            // check if victim is RR5 or more
            bool rr5bonus = (victim.RealmLevel >= 40);
            bool isSolo = killer.Group is not { MemberCount: > 1 };
            int points = 0;
            switch (CurrentSessionType)
            {
                case eSessionTypes.Deathmatch:
                case eSessionTypes.KingOfTheHill:
                case eSessionTypes.CoreRun:
                    points = isSolo ? 10 : 5;
                    if (rr5bonus) points = (int)(points * 1.30);
                    break;
                
                case eSessionTypes.CaptureTheFlag:
                case eSessionTypes.TreasureHunt:
                case eSessionTypes.BringAFriend:
                case eSessionTypes.TerritoryCapture:
                default:
                    points = isSolo ? 4 : 2;
                    break;
                    
                case eSessionTypes.BossHunt:
                    points = isSolo ? 20 : 10;
                    if (rr5bonus) points = (int)(points * 1.30);
                    break;
            }

            PvPScore score;
            if (!isSolo)
            {
                HandleGroupKill(killer, victim, points);
            }
            else
            {
                score = EnsureTotalScore(killer);
                score.PvP_SoloKills += 1;
                score.PvP_SoloKillsPoints += points;
                score = EnsureSoloScore(killer);
                score.PvP_SoloKills += 1;
                score.PvP_SoloKillsPoints += points;
            }
            
            SaveScores();
        }

        private bool IsSolo(GamePlayer killer)
        {
            return killer.Guild == null || killer.Group is not { MemberCount: > 1 };
        }
        #endregion

        #region Add Player/Group (with Guild + Bind Logic)
        /// <summary>
        /// Add a single player (solo) to PvP.
        /// Called by Teleporter or other code.
        /// </summary>
        public bool AddPlayer(GamePlayer player)
        {
            if (!_isOpen || _activeSession == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.PvPNotOpen"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // If group-only session => forbid
            if (_activeSession.GroupCompoOption == 2)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.GroupSessionRequired"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Optionally forbid GMs
            // if (player.Client.Account.PrivLevel == (uint)ePrivLevel.GM)
            // {
            //     player.Out.SendMessage("GM not allowed in PvP!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            //     return false;
            // }

            if (!TeleportSoloPlayer(player))
            {
                _cleanupPlayer(player);
                return false;
            }
            player.Bind(true);
            player.SaveIntoDatabase();
            SaveScores();

            return true;
        }

        /// <summary>
        /// Add an entire group. Called by Teleporter or other code.
        /// </summary>
        public bool AddGroup(GamePlayer groupLeader)
        {
            if (!_isOpen || _activeSession == null) return false;

            var group = groupLeader.Group;
            if (group == null)
            {
                groupLeader.Out.SendMessage(LanguageMgr.GetTranslation(groupLeader.Client.Account.Language, "PvPManager.NoGroup"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (group.MemberCount > _activeSession.GroupMaxSize)
            {
                groupLeader.Out.SendMessage(LanguageMgr.GetTranslation(groupLeader.Client.Account.Language, "PvPManager.GroupTooLarge", _activeSession.GroupMaxSize), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (!TeleportEntireGroup(groupLeader))
            {
                _cleanupGroup(group, false);
                return false;
            }
            SaveScores();
            return true;
        }

        /// <summary>
        /// Remove a single player from PvP. This will NOT restore their location, use this when the player is disconnecting.
        /// </summary>
        public void CleanupPlayer(GamePlayer player, bool disband = true)
        {
            if (!player.IsInPvP)
                return;
            
            _cleanupPlayer(player, disband);
        }

        /// <summary>
        /// Remove a single player from PvP, restoring them to old location + old guild, etc. DO NOT use this when disconnecting, as this will teleport them back, effectively cancelling the logout process.
        /// </summary>
        public void KickPlayer(GamePlayer player, bool disband = true)
        {
            if (!player.IsInPvP)
                return;
            
            _cleanupPlayer(player, disband);
            RestorePlayerData(player);
        }
        
        private void _cleanupGroup([NotNull] Group group, bool disband = true)
        {
            foreach (GamePlayer player in group.GetPlayersInTheGroup())
            {
                _cleanupPlayer(player, disband);
            }
        }

        private void _cleanupPlayer(GamePlayer player, bool disband = true)
        {
            _graceTimers.Remove(player.InternalID);

            Guild pvpGuild = null;
            if (player.Guild != null)
            {
                if (player.Guild.GuildType != Guild.eGuildType.PvPGuild)
                {
                    log.Warn($"Player {player.Name} ({player.InternalID}) being removed from PvP is in non-PvP guild \"{player.Guild.Name}\" ({player.Guild.GuildID})");
                }
                else
                {
                    lock (_groupsLock)
                    {
                        pvpGuild = player.Guild;
                        if (disband && _guildGroups.TryGetValue(pvpGuild, out var g))
                        {
                            g.RemoveMember(player);
                        }
                        pvpGuild.RemovePlayer(string.Empty, player);
                    }
                }
            }

            int totalRemoved = RemoveItemsFromPlayer(player);
            if (totalRemoved > 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.PvPTreasureRemoved", totalRemoved), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            DequeueSolo(player);
            _cleanupArea(player);
            _freeSpawn(player);
            if (pvpGuild != null)
            {
                // Check if *all* members have left. If so, remove area:
                if (!pvpGuild.GetListOfOnlineMembers().Any(m => m.IsInPvP))
                {
                    _cleanupArea(pvpGuild);
                    _freeSpawn(pvpGuild);
                }
            }
            SaveScores();

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.LeftPvP"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
        
        private void _cleanupArea(AbstractArea area)
        {
            if (area is PvpTempArea pvpArea)
                pvpArea.RemoveAllOwnedObjects();

            area.Region?.RemoveArea(area);
        }

        private bool _cleanupArea(GamePlayer player)
        {
            AbstractArea area;
            if (!_soloAreas.Remove(player.InternalID, out area))
                return false;
            
            _cleanupArea(area);
            return true;
        }

        private bool _cleanupArea(Guild guild)
        {
            AbstractArea area;
            if (!_groupAreas.Remove(guild, out area))
                return false;
            
            _cleanupArea(area);
            return true;
        }

        private void _freeSpawn(Spawn spawn)
        {
            if (spawn.NPC != null)
            {
                lock (_usedSpawns)
                {
                    _usedSpawns.Remove(spawn.NPC);
                }
            }
        }

        private void _freeSpawn(Guild group)
        {
            if (_groupSpawns.Remove(group, out Spawn spawn))
            {
                _freeSpawn(spawn);
            }
        }

        private void _freeSpawn(GamePlayer player)
        {
            if (_playerSpawns.Remove(player.InternalID, out Spawn spawn))
            {
                _freeSpawn(spawn);
            }
        }

        private int RemoveItemsFromPlayer(GamePlayer player)
        {
            int totalRemoved = 0;
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                var item = player.Inventory.GetItem(slot);
                if (item is FlagInventoryItem flag)
                {
                    int count = item.Count;
                    if (IsOpen && player.IsInPvP && CurrentSessionType is eSessionTypes.CaptureTheFlag)
                    {
                        if (flag.DropFlagOnGround(player, null))
                            totalRemoved += count;
                    }
                    else
                    {
                        if (player.Inventory.RemoveItem(flag))
                            totalRemoved += count;
                    }
                }
                else if (item is PvPTreasure)
                {
                    int count = item.Count;
                    if (player.Inventory.RemoveItem(item))
                        totalRemoved += count;
                }
            }
            return totalRemoved;
        }

        public void RestorePlayerData(GamePlayer player, RvrPlayer? record = null)
        {
            record ??= GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(player.InternalID));
            if (!string.IsNullOrEmpty(record?.PvPSession))
            {
                record.ResetCharacter(player);

                // If the player was in a guild before PvP, re-add them.
                if (!string.IsNullOrEmpty(record.GuildID))
                {
                    var oldGuild = GuildMgr.GetGuildByGuildID(record.GuildID);
                    if (oldGuild != null)
                    {
                        oldGuild.AddPlayer(player, oldGuild.GetRankByID(record.GuildRank), true);
                    }
                }

                GameServer.Database.DeleteObject(record);
            }
            else
            {
                // Fallback: move the player to a safe location.
                var fallbackPos = Position.Create(51, 434303, 493165, 3088, 1069);
                player.MoveTo(fallbackPos);
            }
            player.IsInPvP = false;
        }

        /// <summary>
        /// Store player's old location, guild, and optional bind.
        /// Remove them from their current guild if any.
        /// </summary>
        private void SetPvPState([NotNull] GamePlayer player, [NotNull] Spawn spawn)
        {
            RvrPlayer record = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(player.InternalID));
            bool isNew = false;
            if (record == null)
            {
                record = new RvrPlayer(player, spawn);
                isNew = true;
            }
            else
            {
                record.PlayerID = player.InternalID;
                record.GuildID = player.GuildID ?? "";
                record.GuildRank = (player.GuildRank != null) ? player.GuildRank.RankLevel : 9;
                record.OldX = (int)player.Position.X;
                record.OldY = (int)player.Position.Y;
                record.OldZ = (int)player.Position.Z;
                record.OldHeading = player.Heading;
                record.OldRegion = player.CurrentRegionID;
                record.OldBindX = player.BindPosition.Coordinate.X;
                record.OldBindY = player.BindPosition.Coordinate.Y;
                record.OldBindZ = player.BindPosition.Coordinate.Z;
                record.OldBindHeading = (int)player.BindPosition.Orientation.InHeading;
                record.OldBindRegion = player.BindPosition.RegionID;

                record.PvPSpawnNPC = spawn.NPC?.InternalID ?? string.Empty;
                record.PvPSpawnX = spawn.Position.X;
                record.PvPSpawnY = spawn.Position.Y;
                record.PvPSpawnZ = spawn.Position.Z;
            }

            record.PvPSession = String.IsNullOrEmpty(_activeSession?.SessionID) ? "PvP" : _activeSession.SessionID;

            record.Dirty = true;

            if (isNew)
                GameServer.Database.AddObject(record);
            else
                GameServer.Database.SaveObject(record);

            if (player.Guild != null)
                player.Guild.RemovePlayer("PVP", player);

            player.IsInPvP = true;
        }

        /// <summary>
        /// Update player's PvP record on group state change for example while in PvP.
        /// </summary>
        private void UpdatePvPState([NotNull] GamePlayer player, [NotNull] Spawn spawn)
        {
            if (!player.IsInPvP)
            {
                log.WarnFormat("Cannot update {0}'s PvP DB record; player not in PvP", player);
                return;
            }
            RvrPlayer record = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(player.InternalID));
            if (record == null)
            {
                log.WarnFormat("Cannot update {0}'s PvP DB record; no record", player);
                return;
            }
            record.PvPSpawnNPC = spawn.NPC?.InternalID ?? string.Empty;
            record.PvPSpawnX = spawn.Position.X;
            record.PvPSpawnY = spawn.Position.Y;
            record.PvPSpawnZ = spawn.Position.Z;
            record.Dirty = true;

            GameServer.Database.SaveObject(record);
        }
        #endregion

        #region Teleport logic
        private bool TeleportSoloPlayer(GamePlayer player)
        {

            Spawn spawnPos = FindSpawnPosition(player.Realm);
            if (spawnPos == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.CannotFindSpawn"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            _playerSpawns[player.InternalID] = spawnPos;

            // Store old info, remove guild
            SetPvPState(player, spawnPos);
            
            player.MoveTo(spawnPos.Position);

            if (!player.IsInPvP)
                player.IsInPvP = true;

            if (CurrentSessionType == eSessionTypes.KingOfTheHill && _activeHill != null)
            {
                player.Out.SendMapObjective(MAP_MARKER_ID, _activeHill.Position);
            }

            // if session says create area + randomlock => do it
            if (CreateAreas)
            {
                CreateSafeAreaForSolo(player, spawnPos.Position, _activeSession.TempAreaRadius);
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.JoinedPvP", _activeSession.SessionID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        private bool TeleportEntireGroup(GamePlayer leader)
        {
            var group = leader.Group;
            if (group == null) return false;

            var spawnPos = FindSpawnPosition(leader.Realm);
            if (spawnPos == null)
            {
                foreach (GamePlayer player in group.GetPlayersInTheGroup())
                {
                    player.SendTranslatedMessage("PvPManager.NoSpawn");
                }
                return false;
            }

            foreach (var member in group.GetPlayersInTheGroup())
            {
                SetPvPState(member, spawnPos);
            }

            if (CurrentSessionType == eSessionTypes.KingOfTheHill && _activeHill != null)
            {
                foreach (var member in group.GetPlayersInTheGroup())
                {
                    member.Out.SendMapObjective(MAP_MARKER_ID, _activeHill.Position);
                }
            }

            Guild pvpGuild;
            lock (_groupsLock)
            {
                if (!_groupGuilds.TryGetValue(group, out pvpGuild))
                    pvpGuild = CreateGuildForGroup(group);
            }

            if (pvpGuild == null)
            {
                foreach (GamePlayer player in group.GetPlayersInTheGroup())
                {
                    player.SendTranslatedMessage("PvPManager.CannotCreatePvPGuild");
                }
                return false;
            }
            
            _groupSpawns[pvpGuild] = spawnPos;
            float distance = 32;
            float increment = (float)(2 * Math.PI / group.MemberCount);
            float baseAngle = (float)(spawnPos.Position.Orientation.InRadians); // 90 degrees from where the spawn npc is facing
            int i = 0;
            if (spawnPos.NPC != null)
                distance /= 2; // Random coordinate is potentially dangerous with clipping, so do this (this will probably solve nothing)
            foreach (var member in group.GetPlayersInTheGroup())
            {
                Position pos = spawnPos.Position;
                if (group.MemberCount > 1)
                {
                    float cos = (float)Math.Cos(baseAngle + i * increment);
                    float sin = (float)Math.Sin(baseAngle + i * increment);
                    pos = spawnPos.Position.With(null, pos.X + (int)(Math.Round(cos * distance)), pos.Y + (int)(Math.Round(sin * distance)));
                }
                
                if (!member.IsInPvP)
                    member.IsInPvP = true;

                member.MoveTo(pos);
                member.Bind(true);
                member.SaveIntoDatabase();
                ++i;
            }

            // Only create one safe area for the entire group (use leader's position)
            if (CreateAreas)
            {
                CreateSafeAreaForGroup(leader, spawnPos.Position, _activeSession.TempAreaRadius);
            }

            leader.Out.SendMessage(LanguageMgr.GetTranslation(leader.Client.Account.Language, "PvPManager.GroupJoinedPvP", _activeSession.SessionID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        private bool _isUniqueSpawns(string spawnOption = null)
        {
            if (string.IsNullOrEmpty(spawnOption))
                spawnOption = _activeSession.SpawnOption.ToLowerInvariant();

            return spawnOption is "randomlock";
        }

        private Spawn? FindSpawnPosition(eRealm realm)
        {
            // 1) If we have no session or no spawns loaded => fallback
            if (_activeSession == null)
                return null;

            // Decide spawn option ( "RealmSpawn", "RandomLock", "RandomUnlock", etc. )
            string spawnOpt = _activeSession.SpawnOption.ToLowerInvariant();

            // 2) If "RealmSpawn" => pick from the realm-labeled spawns
            if (spawnOpt == "realmspawn" && realm != eRealm.None)
            {
                var realmSpawns = _spawnNpcsRealm[realm];
                if (realmSpawns != null && realmSpawns.Count > 0)
                {
                    // pick any spawn from that list at random
                    int idx = Util.Random(realmSpawns.Count - 1);
                    var chosenSpawn = realmSpawns[idx];
                    return new Spawn(chosenSpawn, chosenSpawn.Position);
                }
                else
                {
                    log.Warn($"RealmSpawn: no spawns for realm {realm}, falling back to random coords.");
                }
            }

            // 3) If "RandomLock" => pick from the global spawns, skipping used ones
            if (_isUniqueSpawns(spawnOpt))
            {
                lock (_usedSpawns)
                {
                    var available = _spawnNpcsGlobal.Values.Where(n => !_usedSpawns.Contains(n)).ToList();
                    log.Info($"all: {string.Join(',', _spawnNpcsGlobal.Select(n => n.Value.InternalID))}");
                    log.Info($"used: {string.Join(',', _usedSpawns.Select(n => n.InternalID))}");
                    log.Info($"available: {string.Join(',', available.Select(n => n.InternalID))}");
                    if (available.Count > 0)
                    {
                        var idx = Util.Random(available.Count - 1);
                        var chosen = available[idx];
                        log.Info($"chosen: {chosen.InternalID} ({idx})");
                        _usedSpawns.Add(chosen);
                        return new Spawn(chosen, chosen.Position);
                    }
                }
                // else
                    log.Warn("RandomLock: all spawns used. Fallback to random coords.");
            }

            // 4) If "RandomUnlock" => pick from entire _spawnNpcsGlobal randomly
            // (or if spawnOpt is some other unknown string, we default to random unlock)
            {
                if (_spawnNpcsGlobal.Count > 0)
                {
                    var chosen = _spawnNpcsGlobal.Values.ElementAt(Util.Random(_spawnNpcsGlobal.Count - 1));
                    return new Spawn(chosen, chosen.Position);
                }
                else
                {
                    log.Warn("RandomUnlock: no spawn NPC found, fallback random coords.");
                }
            }

            // 5) Fallback: if we got here, it means no spawn NPC found or spawnOpt is unknown => random
            // parse the session's zone list => pick first zone => random coordinate
            // (this is basically your old approach)

            var zoneStrings = _activeSession.ZoneList.Split(',');
            if (zoneStrings.Length == 0)
                return null;

            if (!ushort.TryParse(zoneStrings[0], out ushort zoneId))
                return null;

            Zone zone = WorldMgr.GetZone(zoneId);
            if (zone == null)
                return null;

            int xcoord = zone.Offset.X + 3000 + Util.Random(1000);
            int ycoord = zone.Offset.Y + 3000 + Util.Random(1000);
            int zcoord = 3000;
            ushort heading = 2048;
            return new Spawn(Position.Create(zone.ZoneRegion.ID, xcoord, ycoord, zcoord, heading));
        }
        #endregion

        #region Queue logic
        public void EnqueueSolo(GamePlayer player)
        {
            if (_soloQueue.Contains(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.AlreadyInSoloQueue"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            _soloQueue.Add(player);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.JoinedSoloQueue"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            TryFormGroupFromSoloQueue();
        }

        private void TryFormGroupFromSoloQueue()
        {
            if (_activeSession == null)
                return;

            int needed = _activeSession.GroupMaxSize;
            if (needed < 2) needed = 2;

            while (_soloQueue.Count >= needed)
            {
                var groupPlayers = _soloQueue.Take(needed).ToList();
                _soloQueue.RemoveRange(0, needed);

                // forcibly create a new DOL Group
                var randomLeader = groupPlayers[Util.Random(groupPlayers.Count - 1)];
                var newGroup = new Group(randomLeader);
                GroupMgr.AddGroup(newGroup);
                newGroup.AddMember(randomLeader);

                foreach (var p in groupPlayers)
                {
                    if (p == randomLeader) continue;
                    newGroup.AddMember(p);
                }

                if (!TeleportEntireGroup(randomLeader))
                {
                    _cleanupGroup(newGroup, false);
                    return;
                }
                SaveScores();
            }
        }

        public void DequeueSolo(GamePlayer player)
        {
            if (_soloQueue.Remove(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.LeftGroupQueue"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        public bool IsPlayerInQueue(GamePlayer player)
        {
            return _soloQueue.Contains(player);
        }
        #endregion

        #region Safe Area Creation
        private void CreateSafeAreaForSolo(GamePlayer player, Position pos, int radius)
        {
            bool isBringFriends = CurrentSessionType is eSessionTypes.BringAFriend;

            AbstractArea areaObject = new PvpTempArea(player, pos.X, pos.Y, pos.Z, radius, isBringFriends);

            player.CurrentRegion.AddArea(areaObject);
            _soloAreas[player.InternalID] = areaObject;

            log.Info("PvpManager: Created a solo outpost for " + player.Name + " at " + pos);
            _fillOutpostItems(player, pos, areaObject);
        }

        private void CreateSafeAreaForGroup(GamePlayer leader, Position pos, int radius)
        {
            var pvpGuild = leader.Guild;
            if (pvpGuild == null) return;
            
            bool isBringFriends = CurrentSessionType is eSessionTypes.BringAFriend;
            AbstractArea areaObject = new PvpTempArea(leader, pos.X, pos.Y, pos.Z, radius, isBringFriends);
            leader.CurrentRegion.AddArea(areaObject);
            _groupAreas[pvpGuild] = areaObject;
            
            log.Info("PvpManager: Created a group outpost for " + pvpGuild.Name + " at " + pos);
            _fillOutpostItems(leader, pos, areaObject);
        }

        private void _fillOutpostItems(GamePlayer leader, Position pos, AbstractArea area)
        {
            List<GameStaticItem>? items = CurrentSessionType switch
            {
                eSessionTypes.CaptureTheFlag => PvPAreaOutposts.CreateCaptureFlagOutpostPad(pos, leader),
                eSessionTypes.TreasureHunt => PvPAreaOutposts.CreateTreasureHuntBase(pos, leader),
                eSessionTypes.BringAFriend or eSessionTypes.TerritoryCapture => PvPAreaOutposts.CreateGuildOutpostTemplate01(pos, leader),
                _ => null
            };

            if (items == null)
                return;

            foreach (var item in items)
            {
                if (area is PvpTempArea pvpArea)
                {
                    pvpArea.AddOwnedObject(item);
                }

                if (item is GamePvPStaticItem pvpItem)
                {
                    pvpItem.SetOwnership(leader);
                }
            }
        }
        
        #endregion

        #region BringAFriend Methods & Scores
        private void OnBringAFriend(DOLEvent e, object sender, EventArgs args)
        {
            if (!_isOpen || CurrentSessionType is not eSessionTypes.BringAFriend)
                return;

            var living = sender as GameLiving;
            if (living == null) return;

            var baArgs = args as BringAFriendArgs;
            if (baArgs == null) return;
            if (!(baArgs.Friend is FollowingFriendMob friendMob)) return;

            if (baArgs.Entered && baArgs.FinalStage)
            {
                var player = living as GamePlayer;
                if (player == null) return;

                AddFollowingFriendToSafeArea(player, friendMob);
            }
            else if (baArgs.Following)
            {
            }
        }

        private void AddFollowingFriendToSafeArea(GamePlayer owner, FollowingFriendMob friendMob)
        {
            var playerRecord = EnsureTotalScore(owner);
            float speed = friendMob.MaxSpeed <= 0 ? 1 : friendMob.MaxSpeed;
            float X = 200f / speed;
            float Y = friendMob.AggroMultiplier;
            double rawPoints = (Y <= 1.0f) ? 10.0 * X : 10.0 * X * Y;
            int basePoints = (int)Math.Round(rawPoints);
            int bonus = 0;

            // Family / guild bonus if friendMob.Guild is set
            string familyGuildName = friendMob.GuildName;
            if (!string.IsNullOrEmpty(familyGuildName))
            {
                AbstractArea area = null;

                if (owner.Guild == null)
                    _soloAreas.TryGetValue(owner.InternalID, out area);
                else if (owner.Guild != null)
                    _groupAreas.TryGetValue(owner.Guild, out area);

                if (area is PvpTempArea safeArea)
                {
                    int oldCount = safeArea.GetFamilyCount(familyGuildName);
                    int newCount = oldCount + 1;
                    safeArea.SetFamilyCount(familyGuildName, newCount);

                    int totalFamInZone = CountFamilyInZone(friendMob);

                    if (newCount >= totalFamInZone)
                    {
                        bonus = GetFamilyBonus(totalFamInZone);
                    }
                }
            }

            var doAdd = (PvPScore score) =>
            {
                score.Friends_BroughtFriendsPoints += basePoints;
                score.Friends_BroughtFamilyBonus += bonus;
            };
            if (owner.Guild is { GuildType: Guild.eGuildType.PvPGuild })
            {
                var groupScore = EnsureGroupScore(owner.Guild);

                doAdd(groupScore.Totals);
                foreach (var member in owner.Guild.GetListOfOnlineMembers())
                {
                    doAdd(groupScore.GetOrCreateScore(member));
                }
            }
            else
            {
                doAdd(EnsureSoloScore(owner));
            }
            doAdd(playerRecord);

            owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "PvPManager.BroughtToSafety", friendMob.Name, basePoints), eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
        }

        /// <summary>
        /// Return the family bonus that applies if 'count' members of the same family are in the area.
        /// </summary>
        private int GetFamilyBonus(int count)
        {
            if (count < 2) return 0;
            if (count == 2) return 2;
            if (count == 3) return 6;
            if (count == 4) return 10;
            if (count == 5 || count == 6) return 15;
            return 20;
        }

        /// <summary>
        /// Count how many FollowingFriendMobs with the same GuildName
        /// are in the *same zone* as 'friendMob'.
        /// </summary>
        private int CountFamilyInZone(FollowingFriendMob friendMob)
        {
            if (friendMob == null || string.IsNullOrEmpty(friendMob.GuildName))
                return 0;

            var zone = friendMob.CurrentZone;
            if (zone == null) return 0;

            // Gather all NPCs in the region, filter by same zone + same guild
            var regionID = zone.ZoneRegion.ID;
            var allNpcs = WorldMgr.GetNPCsFromRegion(regionID).Where(n => n.CurrentZone == zone && n is FollowingFriendMob ff && ff.GuildName == friendMob.GuildName);
            return allNpcs.Count();
        }

        private void OnLivingDying_BringAFriend(DOLEvent e, object sender, EventArgs args)
        {
            if (!_isOpen || CurrentSessionType is not eSessionTypes.BringAFriend)
                return;

            if (!(sender is FollowingFriendMob friendMob)) return;

            GameLiving killer = null;
            if (args is DyingEventArgs dyingArgs && dyingArgs.Killer is GameLiving gl)
            {
                killer = gl;
            }
            

            // 1) If friendMob was actively following a player => that player/team loses 2 points
            var followedPlayer = friendMob.PlayerFollow;
            var killerPlayer = killer as GamePlayer;

            bool isFriendlyKill =
                (followedPlayer == null) || // No owner 
                (killerPlayer == followedPlayer) || // Self kill
                (followedPlayer.Guild != null && killerPlayer != null && followedPlayer.Guild == killerPlayer.Guild); // Same guild

            if (isFriendlyKill)
                return;
            
            if (followedPlayer != null && IsInActivePvpZone(followedPlayer))
            {
                var followedScore = EnsureTotalScore(followedPlayer);
                var groupScore = EnsureGroupScore(followedPlayer.Guild);
                var playerGroupScore = groupScore?.GetOrCreateScore(followedPlayer);
                var doAdd = (PvPScore score) =>
                {
                    score.Friends_FriendKilledCount++;
                    score.Friends_FriendKilledPoints += 2; // penalty
                };
                doAdd(followedScore);
                if (groupScore != null)
                {
                    doAdd(groupScore.Totals);
                    doAdd(playerGroupScore);
                }
                else
                {
                    doAdd(EnsureSoloScore(followedPlayer));
                }
            }

            // 2) If the *killer* is a player, and the mob was following someone => killer gets +2
            if (friendMob.PlayerFollow != null && IsInActivePvpZone(killerPlayer))
            {
                if (killerPlayer != friendMob.PlayerFollow)
                {
                    var killerScore = EnsureTotalScore(killerPlayer);
                    var groupScore = EnsureGroupScore(killerPlayer.Guild);
                    var playerGroupScore = groupScore?.GetOrCreateScore(killerPlayer);
                    var doAdd = (PvPScore score) =>
                    {
                        score.Friends_KillEnemyFriendCount++;
                        score.Friends_KillEnemyFriendPoints += 2;
                    };

                    doAdd(killerScore);
                    if (groupScore != null)
                    {
                        doAdd(groupScore.Totals);
                        doAdd(playerGroupScore);
                    }
                    else
                    {
                        doAdd(EnsureSoloScore(killerPlayer));
                    }
                }
            }
        }
        #endregion

        #region CaptureTerritories Methods
        private int AwardTerritoryOwnershipPoints(RegionTimer timer)
        {
            // 1) Make sure session is open, type=5
            if (!_isOpen || CurrentSessionType is not eSessionTypes.TerritoryCapture)
                return 30000;

            // 2) Figure out which zone IDs are in the session
            var zoneIDs = CurrentZones.Select(z => z.ID);

            var subterritories = TerritoryManager.Instance.Territories
                .Where(t => t.Type == Territory.eType.Subterritory && t.OwnerGuild != null).ToList();

            foreach (var territory in subterritories)
            {
                if (!IsTerritoryInSessionZones(territory, zoneIDs))
                    continue;

                var owningGuild = territory.OwnerGuild;
                if (owningGuild == null)
                    continue;

                var groupScore = EnsureGroupScore(owningGuild);
                groupScore.Totals.Terr_TerritoriesOwnershipPoints++;
                foreach (var member in owningGuild.GetListOfOnlineMembers())
                {
                    if (member.IsInPvP && member.Client != null && member.CurrentRegion != null && PvpManager.Instance.IsInActivePvpZone(member))
                    {
                        groupScore.GetOrCreateScore(member).Terr_TerritoriesOwnershipPoints++;
                        var score = EnsureTotalScore(member);
                        score.Terr_TerritoriesOwnershipPoints += 1;
                    }
                }
            }

            return 30000;
        }
        
        public void AwardTerritoryCapturePoints(Territory territory, GamePlayer player)
        {
            if (CurrentSessionType is not eSessionTypes.TerritoryCapture)
                return;

            if (territory.Type != Territory.eType.Subterritory) // Is this check necessary?
                return;

            if (territory.Zone == null || !CurrentZones.Contains(territory.Zone))
                return;

            var doAdd = (PvPScore score) =>
            {
                score.Terr_TerritoriesCapturedPoints += 20;
                score.Terr_TerritoriesCapturedCount++;
            };
            if (player.Guild != null)
            {
                var groupScores = EnsureGroupScore(player.Guild);
                doAdd(groupScores.Totals);
                doAdd(groupScores.GetOrCreateScore(player));
            }
            doAdd(EnsureTotalScore(player));
        }

        private bool IsTerritoryInSessionZones(Territory territory, IEnumerable<ushort> zoneIDs)
        {
            if (territory.Zone == null)
                return false;
            return zoneIDs.Contains(territory.Zone.ID);
        }
        #endregion

        #region BossHunt Methods
        public void HandleBossHit(GamePlayer player, GameNPC boss, int damageAmount)
        {
            if (!IsOpen || player == null || boss == null) return;

            // Accumulate damage key (PlayerID_BossID) to handle multiple bosses
            string key = $"{player.InternalID}_{boss.InternalID}";

            if (!_bossDamageAccumulator.ContainsKey(key))
                _bossDamageAccumulator[key] = 0;

            _bossDamageAccumulator[key] += damageAmount;

            // Every 100 damage dealt = 1 Point
            int damageThreshold = 100;

            if (_bossDamageAccumulator[key] >= damageThreshold)
            {
                long pointsEarned = _bossDamageAccumulator[key] / damageThreshold;
                _bossDamageAccumulator[key] %= damageThreshold;

                bool isSolo = IsSolo(player);
                double pointValue = pointsEarned;

                // Scale by Boss Level (Higher level boss = points are worth more, or threshold is lower)
                if (boss.Level >= 55) pointValue *= 1.6;
                if (isSolo) pointValue *= 1.3;

                int finalPoints = (int)Math.Max(1, Math.Round(pointValue));

                AwardScore(player, (score) =>
                {
                    score.Boss_BossHitsCount += (int)pointsEarned;
                    score.Boss_BossHitsPoints += finalPoints;
                }, isSolo);

                player.Out.SendMessage($"You scored {finalPoints} points on {boss.Name}!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        public void HandleBossKill(GamePlayer killer, GameNPC boss)
        {
            if (!IsOpen || killer == null || boss == null) return;

            bool isSolo = IsSolo(killer);

            double points = 40.0;

            if (boss.Level > 50)
            {
                points += (boss.Level - 50);
            }

            if (isSolo) points *= 1.3;

            int finalPoints = (int)Math.Round(points);

            string msg = $"[PvP] {killer.Name} {(isSolo ? "(Solo)" : "(Group)")} slew the {boss.Name} for {finalPoints} points!";
            foreach (var client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player != null && client.Player.IsInPvP)
                    client.Player.Out.SendMessage(msg, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            AwardScore(killer, (score) =>
            {
                score.Boss_BossKillsCount++;
                score.Boss_BossKillsPoints += finalPoints;
            }, isSolo);

            SaveScores();
        }
        #endregion

        #region King of the Hill Methods
        private void RefillKotHRotation()
        {
            var rnd = new Random();
            var shuffled = _kothPotentialSpawns.OrderBy(x => rnd.Next()).ToList();
            _kothRotationQueue.Clear();
            foreach (var s in shuffled) _kothRotationQueue.Enqueue(s);
        }

        private void SpawnNextHill()
        {
            if (_activeHill != null)
            {
                _activeHill.Delete();
                _activeHill = null;
            }

            if (_kothGameLoop != null)
            {
                _kothGameLoop.Stop();
                _kothGameLoop = null;
            }

            if (_kothRotationQueue.Count == 0) RefillKotHRotation();
            if (_kothRotationQueue.Count == 0) return;

            var nextLoc = _kothRotationQueue.Dequeue();

            _activeHill = new KotHBanner();
            int radius = _activeSession!.TempAreaRadius > 0 ? _activeSession.TempAreaRadius : 1000;

            _activeHill.Setup(radius, nextLoc.Position.X, nextLoc.Position.Y, nextLoc.Position.Z, nextLoc.Position.RegionID);

            UpdateKothHillMarker();
            int duration = Properties.KOTH_DURATION_SECONDS * 1000;
            if (duration <= 0) duration = 300000;
            _kothNextMoveTick = _activeHill.CurrentRegion.Time + duration;
            _kothOwnershipStartTick = 0;

            string msg = $"[KotH] The Hill has moved to a new location!";
            foreach (var client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player != null && client.Player.IsInPvP)
                    client.Player.Out.SendMessage(msg, eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
            }

            _kothGameLoop = new RegionTimer(_activeHill, KotHLoopCallback);
            _kothGameLoop.Start(KOTH_TICK_RATE);
        }

        private int KotHLoopCallback(RegionTimer timer)
        {
            if (!IsOpen || _activeHill == null) return 0;

            if (_activeHill.CurrentRegion.Time >= _kothNextMoveTick)
            {
                SpawnNextHill();
                return 0; 
            }

            ProcessHillOwnership();
            return KOTH_TICK_RATE;
        }

        private bool IsPlayerEffectivelyAlive(GamePlayer p)
        {
            if (!p.IsInPvP) return false;
            if (p.IsAlive) return true;

            if (SpellHandler.FindEffectOnTarget(p, "SummonMonster") != null) return true;
            if (p.IsDamned) return true;

            return false;
        }

        /// <summary>
        /// Calculates the pressure value of a specific entity based on complex rules.
        /// </summary>
        private double CalculateLivingPressure(GameLiving living)
        {
            if (living is GamePlayer player)
            {
                if (!IsPlayerEffectivelyAlive(player)) return 0.0;

                double points = 1.0;

                // RR5 Check (RealmRank 50 = Rank 5)
                if (player.RealmLevel >= 40) points += 0.4;

                // Zombie/Damned Check
                if (player.IsDamned) points += 0.4;
                if (SpellHandler.FindEffectOnTarget(player, "SummonMonster") != null) points += 0.4;

                return points;
            }
            else if (living is GamePet pet)
            {
                var owner = pet.GetPlayerOwner();
                if (owner == null || !IsPlayerEffectivelyAlive(owner)) return 0.0;

                // Specific Pet Weights
                if (pet is TheurgistPet) return 0.0;
                if (pet is TurretPet) return 0.25;
                if (pet is BDSubPet) return 0.10;
                if (pet is CommanderPet) return 0.30;

                // Standard pets (Cabalist, Enchanter, Spiritmaster, Necro, etc)
                return 0.5;
            }

            return 0.0;
        }

        /// <summary>
        /// Gets the faction object (Guild or Solo Player) for a living
        /// </summary>
        private object GetFaction(GameLiving living)
        {
            var player = living is GamePlayer p ? p : (living as GamePet)?.GetPlayerOwner();
            if (player == null) return null;

            if (AllowsGroups && player.Guild != null)
                return player.Guild;

            return player;
        }

        private void ProcessHillOwnership()
        {
            int radius = _activeSession!.TempAreaRadius > 0 ? _activeSession.TempAreaRadius : 1000;
            var pressureMap = new Dictionary<object, double>();

            // Identify all entities in the hill and calculate pressure
            foreach (GameLiving living in _activeHill.GetPlayersInRadius((ushort)radius))
            {
            }

            // Gather Players
            foreach (GamePlayer p in _activeHill.GetPlayersInRadius((ushort)radius).OfType<GamePlayer>())
            {
                object faction = GetFaction(p);
                if (faction == null) continue;

                double pressure = CalculateLivingPressure(p);
                if (!pressureMap.ContainsKey(faction)) pressureMap[faction] = 0;
                pressureMap[faction] += pressure;
            }

            // Gather Pets (We scan for NPCs in radius, filter for pets owned by PvP players)
            foreach (GameNPC npc in _activeHill.GetNPCsInRadius((ushort)radius))
            {
                if (npc is GamePet pet)
                {
                    object faction = GetFaction(pet);
                    if (faction == null) continue;

                    double pressure = CalculateLivingPressure(pet);
                    if (!pressureMap.ContainsKey(faction)) pressureMap[faction] = 0;
                    pressureMap[faction] += pressure;
                }
            }

            object currentOwnerFaction = null;
            if (_activeHill.OwningGuild != null) currentOwnerFaction = _activeHill.OwningGuild;
            else if (_activeHill.OwningSolo != null) currentOwnerFaction = _activeHill.OwningSolo;

            // Find the faction with the Highest Pressure right now
            object dominantFaction = null;
            double highestPressure = 0.0;

            foreach (var kvp in pressureMap)
            {
                if (kvp.Value > highestPressure)
                {
                    highestPressure = kvp.Value;
                    dominantFaction = kvp.Key;
                }
            }

            double ownerPressure = 0.0;
            if (currentOwnerFaction != null && pressureMap.ContainsKey(currentOwnerFaction))
            {
                ownerPressure = pressureMap[currentOwnerFaction];
            }

            if (currentOwnerFaction == null)
            {
                if (dominantFaction != null && highestPressure > 0)
                {
                    CaptureHill(dominantFaction, true);
                }
                return;
            }

            // Check for Enemy Pressure
            double maxEnemyPressure = 0.0;
            object strongestEnemy = null;

            foreach (var kvp in pressureMap)
            {
                if (kvp.Key != currentOwnerFaction)
                {
                    if (kvp.Value > maxEnemyPressure)
                    {
                        maxEnemyPressure = kvp.Value;
                        strongestEnemy = kvp.Key;
                    }
                }
            }

            // FLIP Logic: If Enemy Pressure > Owner Pressure, ownership changes immediately.
            if (maxEnemyPressure > ownerPressure)
            {
                CaptureHill(strongestEnemy, false);
                return;
            }

            if (ownerPressure <= 0)
            {
                return;
            }

            // Calculate Pressure Ratio/Difference for Scoring
            // "If the owner puts more than 60% of pressure on the Hill than the enemy"
            // Let's interpret "more than 60% of TOTAL pressure" or "Enemy is less than 40% of owner".
            // Implementation: Dominance Ratio = Owner / (Owner + Enemy)

            double totalPressure = ownerPressure + maxEnemyPressure;
            double ownerDominance = ownerPressure / totalPressure;

            // Logic: 
            // 1. Dominating (Ratio > 0.6): 2 pts Owner.
            // 2. Under Pressure (Ratio <= 0.6): 1 pt Owner, 1 pt Enemy.
            if (maxEnemyPressure == 0 || ownerDominance > 0.60)
            {
                AwardHillTick(currentOwnerFaction, 2);
            }
            else
            {
                AwardHillTick(currentOwnerFaction, 1);
                AwardPressurePoints(strongestEnemy, 1);
            }
        }

        private void CaptureHill(object faction, bool isNeutralCapture)
        {
            Guild guild = faction as Guild;
            GamePlayer solo = faction as GamePlayer;

            int pointsToAward = 0;

            if (isNeutralCapture)
            {
                pointsToAward = 50;
            }
            else
            {
                long heldDuration = _activeHill.CurrentRegion.Time - _kothOwnershipStartTick;
                double percentage = heldDuration / 300000.0;

                if (percentage > 1.0) percentage = 1.0;
                pointsToAward = (int)(30 * percentage);

                if (pointsToAward < 1) pointsToAward = 1;
            }

            _activeHill.SetOwner(guild, solo);
            _kothOwnershipStartTick = _activeHill.CurrentRegion.Time;

            AwardCapturePoints(faction, pointsToAward);

            foreach (GamePlayer p in _activeHill.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).OfType<GamePlayer>())
            {
                p.Out.SendSpellEffectAnimation(_activeHill, _activeHill, 5811, 0, false, 1);
                string ownerName = guild != null ? guild.Name : solo!.Name;
                p.Out.SendMessage($"The Hill has been captured by {ownerName}!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
            }
        }

        private void AwardPressurePoints(object faction, int amount)
        {
            if (faction == null) return;

            if (faction is Guild guild)
            {
                var groupScore = EnsureGroupScore(guild);
                groupScore.Totals.KotH_PressurePoints += amount;

                foreach (var member in guild.GetListOfOnlineMembers().Where(m => m.IsInPvP))
                {
                    var pScore = EnsureTotalScore(member);
                    pScore.KotH_PressurePoints += amount;
                    member.Out.SendMessage($"Pressuring the Hill... (+{amount} pressure)", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
            else if (faction is GamePlayer solo)
            {
                var pScore = EnsureTotalScore(solo);
                var sScore = EnsureSoloScore(solo);
                pScore.KotH_PressurePoints += amount;
                sScore.KotH_PressurePoints += amount;
                solo.Out.SendMessage($"Pressuring the Hill... (+{amount} pressure)", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        private void AwardCapturePoints(object faction, int amount)
        {
            if (faction == null) return;

            if (faction is Guild guild)
            {
                var groupScore = EnsureGroupScore(guild);
                groupScore.Totals.KotH_CapturePoints += amount;
                groupScore.Totals.KotH_Captures++;

                foreach (var member in guild.GetListOfOnlineMembers().Where(m => m.IsInPvP))
                {
                    var pScore = EnsureTotalScore(member);
                    pScore.KotH_CapturePoints += amount;
                    pScore.KotH_Captures++;
                    member.Out.SendMessage($"You captured the Hill!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }
            else if (faction is GamePlayer solo)
            {
                var pScore = EnsureTotalScore(solo);
                var sScore = EnsureSoloScore(solo);
                pScore.KotH_CapturePoints += amount; sScore.KotH_CapturePoints += amount;
                pScore.KotH_Captures++; sScore.KotH_Captures++;
                solo.Out.SendMessage($"You captured the Hill!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
            }
        }

        private void AwardHillTick(object faction, int amount)
        {
            if (faction == null) return;

            if (faction is Guild guild)
            {
                var groupScore = EnsureGroupScore(guild);
                groupScore.Totals.KotH_Points += amount;
                groupScore.Totals.KotH_Ticks++;

                foreach (var member in guild.GetListOfOnlineMembers().Where(m => m.IsInPvP))
                {
                    var pScore = EnsureTotalScore(member);
                    pScore.KotH_Points += amount;
                    pScore.KotH_Ticks++;
                    member.Out.SendMessage($"Holding Hill... (+{amount})", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
            else if (faction is GamePlayer solo)
            {
                var pScore = EnsureTotalScore(solo);
                var sScore = EnsureSoloScore(solo);
                pScore.KotH_Points += amount; sScore.KotH_Points += amount;
                pScore.KotH_Ticks++; sScore.KotH_Ticks++;
                solo.Out.SendMessage($"Holding Hill... (+{amount})", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        private void UpdateKothHillMarker()
        {
            if (_activeHill == null) return;

            foreach (var client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player != null && client.Player.IsInPvP)
                {
                    client.Player.Out.SendMapObjective(MAP_MARKER_ID, _activeHill.Position);
                }
            }
        }

        private void ClearKothMarker()
        {
            Position nullPos = Position.Create(0, 0, 0, 0);

            foreach (var client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player != null)
                {
                    client.Player.Out.SendMapObjective(MAP_MARKER_ID, nullPos);
                }
            }
        }
        #endregion

        #region Core Run (Squid Game) Logic

        private void StartCoreRun()
        {
            log.Info("Starting Core Run Logic...");
            _coreRunToreArea = null;
            _coreRunAnchorNPC = null;

            if (_zones.Count == 0)
            {
                log.Error("Core Run: No zones in session!");
                return;
            }

            // 1. Find the Tore Area
            foreach (var zone in _zones)
            {
                var region = WorldMgr.GetRegion(zone.ZoneRegion.ID);
                if (region == null) continue;

                // Look for the Generator NPC to act as our logic anchor (Timer Owner)
                // We search for class type CoreGeneratorNPC or name "Core Generator"
                foreach (var obj in region.Objects)
                {
                    if (obj is CoreGeneratorNPC gen)
                    {
                        _coreRunAnchorNPC = gen;
                        break;
                    }
                }

                // Look for the Tore Area in this region
                foreach (var kvp in region.Areas)
                {
                    if (kvp.Value is Area.Tore tore)
                    {
                        _coreRunToreArea = tore;
                        break;
                    }
                }

                if (_coreRunToreArea != null && _coreRunAnchorNPC != null) break;
            }

            if (_coreRunToreArea == null)
            {
                log.Error("Core Run: Could not find 'Tore' Area in session zones.");
                return;
            }

            if (_coreRunAnchorNPC == null)
            {
                // Fallback: If we can't find the generator, try to find ANY npc in the region to hold the timer
                log.Warn("Core Run: Could not find CoreGeneratorNPC. Finding fallback anchor.");
                var region = WorldMgr.GetRegion(_coreRunToreArea.Region.ID);
                if (region != null && region.Objects.Length > 0)
                {
                    _coreRunAnchorNPC = region.Objects.OfType<GameNPC>().FirstOrDefault();
                }
            }

            if (_coreRunAnchorNPC == null)
            {
                log.Error("Core Run: No NPC found to anchor the timers!");
                return;
            }

            // 2. Create the "Safe Center" Area programmatically
            // This fills the hole of the torus so we get Enter events when players reach the goal.
            _coreRunCenterSafeZone = new Area.Circle("Core Safe Zone", _coreRunToreArea.Coordinate, CORE_RUN_SAFE_RADIUS);
            _coreRunToreArea.Region.AddArea(_coreRunCenterSafeZone);
            log.Info($"Core Run: Created Safe Zone at {_coreRunCenterSafeZone.Coordinate} radius {CORE_RUN_SAFE_RADIUS}");

            // 3. Start the Cycle
            _isCoreRunRedLight = false;
            BroadcastCoreRunMessage("The Core Run begins! GREEN LIGHT - GO!", eChatType.CT_ScreenCenter);

            // Timer runs on the anchor NPC
            _coreRunCycleTimer = new RegionTimer(_coreRunAnchorNPC, CoreRunCycleCallback);
            _coreRunCycleTimer.Start(10000);

            GameEventMgr.AddHandler(AreaEvent.PlayerEnter, new DOLEventHandler(OnCoreRunAreaEnter));
        }

        private void StopCoreRun()
        {
            if (_coreRunCycleTimer != null) { _coreRunCycleTimer.Stop(); _coreRunCycleTimer = null; }
            if (_coreRunMovementTimer != null) { _coreRunMovementTimer.Stop(); _coreRunMovementTimer = null; }

            if (_coreRunCenterSafeZone != null && _coreRunToreArea != null)
            {
                _coreRunToreArea.Region.RemoveArea(_coreRunCenterSafeZone);
                _coreRunCenterSafeZone = null;
            }

            ToggleFakeRedLightEvent(false);

            GameEventMgr.RemoveHandler(AreaEvent.PlayerEnter, new DOLEventHandler(OnCoreRunAreaEnter));
        }

        private int CoreRunCycleCallback(RegionTimer timer)
        {
            if (!IsOpen || CurrentSessionType != eSessionTypes.CoreRun) return 0;

            _isCoreRunRedLight = !_isCoreRunRedLight;

            if (_isCoreRunRedLight)
            {
                // === SWITCH TO RED LIGHT ===
                BroadcastCoreRunMessage("RED LIGHT! FREEZE!", eChatType.CT_ScreenCenter);
                BroadcastCoreRunMessage("PvP Disabled. Movement is fatal.", eChatType.CT_Important);

                ToggleFakeRedLightEvent(true);
                ApplyCoreRunImmunity(true);
                SnapshotTorePlayers();

                _coreRunMovementTimer = new RegionTimer(_coreRunAnchorNPC, CoreRunMovementCheckCallback);
                _coreRunMovementTimer.Start(800);

                return Util.Random(4000, 7000);
            }
            else
            {
                // === SWITCH TO GREEN LIGHT ===
                BroadcastCoreRunMessage("GREEN LIGHT! GO!", eChatType.CT_ScreenCenter);
                BroadcastCoreRunMessage("PvP Enabled.", eChatType.CT_System);

                ToggleFakeRedLightEvent(false);
                ApplyCoreRunImmunity(false);

                if (_coreRunMovementTimer != null) _coreRunMovementTimer.Stop();

                return Util.Random(6000, 12000);
            }
        }

        private void ToggleFakeRedLightEvent(bool active)
        {
            var ev = GameEventManager.Instance.GetEventByID(EVENT_ID_REDLIGHT);

            if (ev == null)
            {
                log.Warn($"Core Run: Event {EVENT_ID_REDLIGHT} not found in EventManager!");
                return;
            }

            if (active)
            {
                if (!ev.IsRunning) GameEventManager.Instance.StartEvent(ev);
            }
            else
            {
                if (ev.IsRunning) GameEventManager.Instance.StopEvent(ev, EndingConditionType.Timer, null);
            }

            if (_coreRunToreArea != null)
            {
                _coreRunToreArea.SpawnBoundary();

                if (_coreRunAnchorNPC != null)
                {
                    new RegionTimer(_coreRunAnchorNPC, (t) => {
                        _coreRunToreArea.SpawnBoundary();
                        return 0;
                    }).Start(500);
                }
            }
        }

        private void SnapshotTorePlayers()
        {
            _coreRunPlayerSnapshots.Clear();
            foreach (var client in WorldMgr.GetAllPlayingClients())
            {
                GamePlayer p = client.Player;
                if (p == null || !p.IsInPvP || !p.IsAlive) continue;

                if (_coreRunToreArea.IsContaining(p.Coordinate))
                {
                    _coreRunPlayerSnapshots[p.InternalID] = p.Coordinate;
                }
            }
        }

        private int CoreRunMovementCheckCallback(RegionTimer timer)
        {
            if (!IsOpen || !_isCoreRunRedLight) return 0;

            List<GamePlayer> toEliminate = new List<GamePlayer>();

            foreach (var kvp in _coreRunPlayerSnapshots)
            {
                GameClient client = WorldMgr.GetClientByPlayerID(kvp.Key, true, false);
                GamePlayer p = client?.Player;

                if (p == null || !p.IsAlive || !p.IsInPvP) continue;

                if (!_coreRunToreArea.IsContaining(p.Coordinate)) continue;

                if (p.Coordinate.DistanceTo(kvp.Value) > CORE_RUN_MOVEMENT_TOLERANCE)
                {
                    toEliminate.Add(p);
                }
            }

            foreach (var p in toEliminate)
            {
                p.Out.SendMessage("You moved during Red Light!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                p.Out.SendSpellEffectAnimation(p, p, 5002, 0, false, 1);

                AwardScore(p, (s) => s.CoreRun_Eliminations++);

                p.Health = 0;
                p.Die(null);
            }

            return 0;
        }

        private void ApplyCoreRunImmunity(bool active)
        {
            foreach (var client in WorldMgr.GetAllPlayingClients())
            {
                GamePlayer p = client.Player;
                if (p == null || !p.IsInPvP) continue;

                if (_coreRunToreArea != null && _coreRunToreArea.IsContaining(p.Coordinate))
                {
                    if (active)
                    {
                        p.TempProperties.setProperty("SQUID_IMMUNE", true);
                        p.Out.SendMessage("Protective Field Active.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        p.TempProperties.removeProperty("SQUID_IMMUNE");
                        p.Out.SendMessage("Protective Field Fading...", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }
            }
        }

        private void BroadcastCoreRunMessage(string msg, eChatType type)
        {
            foreach (var client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player != null && client.Player.IsInPvP)
                    client.Player.Out.SendMessage(msg, type, eChatLoc.CL_SystemWindow);
            }
        }

        private void OnCoreRunAreaEnter(DOLEvent e, object sender, EventArgs args)
        {
            if (!IsOpen || CurrentSessionType != eSessionTypes.CoreRun) return;

            AreaEventArgs areaArgs = args as AreaEventArgs;
            if (areaArgs == null || !(areaArgs.GameObject is GamePlayer player)) return;

            if (areaArgs.Area == _coreRunCenterSafeZone)
            {
                player.Out.SendMessage("You reached the Core Safe Zone! Bind point updated.", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                AwardScore(player, (s) => s.CoreRun_CenterReached++);
                player.Bind(true);

                player.Out.SendSpellEffectAnimation(player, player, 106, 0, false, 1);
                return;
            }

            if (_coreRunToreArea != null)
            {
                double dist = player.Coordinate.DistanceTo(_coreRunToreArea.Coordinate);

                if (dist > _coreRunToreArea.Radius + (_coreRunToreArea.Radius * 2))
                {
                    double bindDist = player.BindPosition.Coordinate.DistanceTo(_coreRunToreArea.Coordinate);
                    if (bindDist < _coreRunToreArea.Radius)
                    {
                        Spawn? spawn = null;
                        if (player.Group != null && _groupSpawns.ContainsKey(player.Guild))
                            spawn = _groupSpawns[player.Guild];
                        else if (_playerSpawns.ContainsKey(player.InternalID))
                            spawn = _playerSpawns[player.InternalID];

                        if (spawn != null)
                        {
                            player.Bind(spawn.Position);
                            player.Out.SendMessage("You reached the Outer Rim! Bind point reset to Spawn.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                }
            }
        }

        public void HandleCoreDelivery(GamePlayer player, int points)
        {
            AwardScore(player, (s) =>
            {
                s.CoreRun_CoresDelivered++;
                s.PvP_SoloKillsPoints += points;
            });
            SaveScores();
        }

        #endregion

        #region CTF methods

        public void AwardCTFOwnershipPoints(GamePlayer player, int points)
        {
            if (!IsOpen)
                return;
            
            var doAdd = (PvPScore score) =>
            {
                score.Flag_OwnershipPoints += points;
            };
            
            if (player.Guild != null)
            {
                var groupScores = EnsureGroupScore(player.Guild); 
                doAdd(groupScores.Totals);
                doAdd(groupScores.GetOrCreateScore(player));
            }
            else
            {
                doAdd(EnsureSoloScore(player));
            }
            doAdd(EnsureTotalScore(player));
        }

        public void AwardCTFOwnershipPoints(Guild guild, int points)
        {
            if (!IsOpen)
                return;
            
            var doAdd = (PvPScore score) =>
            {
                score.Flag_OwnershipPoints += points;
            };
            
            if (guild != null)
            {
                var groupScores = EnsureGroupScore(guild); 
                doAdd(groupScores.Totals);
                foreach (var player in guild.GetListOfOnlineMembers())
                {
                    doAdd(groupScores.GetOrCreateScore(player));
                    doAdd(EnsureTotalScore(player));
                }
            }
        }

        public void AwardCTFCapturePoints(GamePlayer player)
        {
            if (!IsOpen)
                return;
            
            var doAdd = (PvPScore score) =>
            {
                score.Flag_FlagReturnsCount += 1;
                score.Flag_FlagReturnsPoints += 20;
            };
            
            if (player.Guild != null)
            {
                var groupScores = EnsureGroupScore(player.Guild); 
                doAdd(groupScores.Totals);
                doAdd(groupScores.GetOrCreateScore(player));
            }
            else
            {
                doAdd(EnsureSoloScore(player));
            }
            doAdd(EnsureTotalScore(player));
        }
        
        #endregion

        #region Old Compatibility Methods
        /// <summary>
        /// For old code referencing IsPvPRegion(ushort),
        /// we define "PvP region" as the zones in the current session's ZoneList.
        /// </summary>
        public bool IsPvPRegion(ushort regionID)
        {
            if (_activeSession == null)
                return false;

            return CurrentZones.Select(z => z.ID).Contains(regionID);
        }

        /// <summary>
        /// If the living is a GamePlayer, returns IsInPvP flag
        /// </summary>
        public bool IsIn(GameLiving living)
        {
            if (living is GamePlayer plr)
                return plr.IsInPvP;
            return false;
        }
        #endregion

        enum ScoreType
        {
            Bonus,
            /// <summary>
            /// Don't display count, display negative points
            /// </summary>
            Malus,
            /// <summary>
            /// Don't display count
            /// </summary>
            BonusPoints,
            /// <summary>
            /// Don't display count, display negative points
            /// </summary>
            MalusPoints
        }

        private record Score(int Points, int Count, ScoreType Type = ScoreType.Bonus);

        private record ScoreLine(string Label, Score Points)
        {
            
            /// <inheritdoc />
            public override string ToString()
            {
                return base.ToString();
            }

            public string ToString(string language, bool shortDescription)
            {
                if (shortDescription)
                {
                    var translated = LanguageMgr.GetTranslation(language, Label + ".Short");
                    return Points.Type switch
                    {
                        ScoreType.Bonus => $"{translated}={Points.Count}({LanguageMgr.GetTranslation(language, "PvPManager.Score.Pts", Points.Points)})",
                        ScoreType.Malus => $"{translated}={Points.Count}(-{LanguageMgr.GetTranslation(language, "PvPManager.Score.Pts", Points.Points)})",
                        ScoreType.BonusPoints => $"{translated}={LanguageMgr.GetTranslation(language, "PvPManager.Score.Pts", Points.Points)}",
                        ScoreType.MalusPoints => $"{translated}=-{LanguageMgr.GetTranslation(language, "PvPManager.Score.Pts", Points.Points)}",
                    };
                }
                else
                {
                    var translated = LanguageMgr.GetTranslation(language, Label);
                    return Points.Type switch
                    {
                        ScoreType.Bonus => $"  {translated}: {Points.Count} - {LanguageMgr.GetTranslation(language, "PvPManager.Score.Points", Points.Points)}",
                        ScoreType.Malus => $"  {translated}: {Points.Count} - -{LanguageMgr.GetTranslation(language, "PvPManager.Score.Points", Points.Points)}",
                        ScoreType.BonusPoints => $"  {translated}: {LanguageMgr.GetTranslation(language, "PvPManager.Score.Points", Points.Points)}",
                        ScoreType.MalusPoints => $"  {translated}: -{LanguageMgr.GetTranslation(language, "PvPManager.Score.Points", Points.Points)}",
                    };
                }
            }
        }

        private record ScoreboardEntry(string Player, int Total, List<ScoreLine> Lines);

        private ScoreboardEntry MakeScoreboardEntry(PvPScore ps)
        {
            if (!IsOpen)
                return null;

            var sessionType = CurrentSessionType;
            List<ScoreLine> scoreLines = new();
            switch (sessionType)
            {
                case eSessionTypes.Deathmatch:
                    scoreLines.Add(new ScoreLine("PvP.Score.PvPSoloKills", new Score(ps.PvP_SoloKillsPoints, ps.PvP_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.PvPGrpKills", new Score(ps.PvP_GroupKillsPoints, ps.PvP_GroupKills)));
                    break;

                case eSessionTypes.CaptureTheFlag:
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPSoloKills", new Score(ps.PvP_SoloKillsPoints, ps.PvP_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPGrpKills", new Score(ps.PvP_GroupKillsPoints, ps.PvP_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPFlagCarrierKillBonus", new Score(ps.Flag_KillFlagCarrierPoints, ps.Flag_KillFlagCarrierCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPFlagsCaptured", new Score(ps.Flag_FlagReturnsPoints, ps.Flag_FlagReturnsCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPOwnership", new Score(ps.Flag_OwnershipPoints, 0, ScoreType.BonusPoints)));
                    break;

                case eSessionTypes.TreasureHunt:
                    scoreLines.Add(new ScoreLine("PvP.Score.TreasurePvPSoloKills", new Score(ps.PvP_SoloKillsPoints, ps.PvP_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.TreasurePvPGrpKills", new Score(ps.PvP_GroupKillsPoints, ps.PvP_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.TreasurePvPTreasurePoints", new Score(ps.Treasure_BroughtTreasuresPoints, 0, ScoreType.BonusPoints)));
                    scoreLines.Add(new ScoreLine("PvP.Score.TreasurePvPStolenItemPenalty", new Score(ps.Treasure_StolenTreasuresPoints, 0, ScoreType.MalusPoints)));
                    break;

                case eSessionTypes.BringAFriend:
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPSoloKills", new Score(ps.PvP_SoloKillsPoints, ps.PvP_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPGrpKills", new Score(ps.PvP_GroupKillsPoints, ps.PvP_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPBroughtFriends", new Score(ps.Friends_BroughtFriendsPoints, 0, ScoreType.BonusPoints)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPFamilyBonus", new Score(ps.Friends_BroughtFamilyBonus, 0, ScoreType.BonusPoints)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPLostFriends", new Score(ps.Friends_FriendKilledPoints, ps.Friends_FriendKilledCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPKilledOthersFriends", new Score(ps.Friends_KillEnemyFriendPoints, ps.Friends_KillEnemyFriendCount)));
                    break;

                case eSessionTypes.TerritoryCapture:
                    scoreLines.Add(new ScoreLine("PvP.Score.CTTPvPSoloKills", new Score(ps.PvP_SoloKillsPoints, ps.PvP_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.CTTPvPGrpKills", new Score(ps.PvP_GroupKillsPoints, ps.PvP_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.CTTPvPTerritoryCaptures", new Score(ps.Terr_TerritoriesCapturedPoints, ps.Terr_TerritoriesCapturedCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.CTTPvPOwnership", new Score(ps.Terr_TerritoriesOwnershipPoints, 0, ScoreType.BonusPoints)));
                    break;

                case eSessionTypes.BossHunt:
                    scoreLines.Add(new ScoreLine("PvP.Score.BossPvPSoloBossKills", new Score(ps.PvP_SoloKillsPoints, ps.PvP_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.BossPvPGroupBossKills", new Score(ps.PvP_GroupKillsPoints, ps.PvP_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.BossPvPBossHits", new Score(ps.Boss_BossHitsPoints, ps.Boss_BossHitsCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.BossPvPBossKills", new Score(ps.Boss_BossKillsPoints, ps.Boss_BossKillsCount)));
                    break;

                case eSessionTypes.KingOfTheHill:
                    scoreLines.Add(new ScoreLine("PvP.Score.KotHKills", new Score(ps.PvP_SoloKillsPoints + ps.PvP_GroupKillsPoints, ps.PvP_SoloKills + ps.PvP_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.KotHTimeHeld", new Score(ps.KotH_Points, ps.KotH_Ticks, ScoreType.BonusPoints)));
                    scoreLines.Add(new ScoreLine("PvP.Score.KotHPressure", new Score(ps.KotH_PressurePoints, 0, ScoreType.BonusPoints)));
                    scoreLines.Add(new ScoreLine("PvP.Score.KotHCaptures", new Score(ps.KotH_CapturePoints, ps.KotH_Captures)));
                    break;

                case eSessionTypes.CoreRun:
                    scoreLines.Add(new ScoreLine("PvP.Score.CoreRunKills", new Score(ps.PvP_SoloKillsPoints + ps.PvP_GroupKillsPoints, ps.PvP_SoloKills + ps.PvP_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.CoreRunCores", new Score(ps.CoreRun_CoresDelivered * 20, ps.CoreRun_CoresDelivered, ScoreType.Bonus)));
                    scoreLines.Add(new ScoreLine("PvP.Score.CoreRunCenter", new Score(ps.CoreRun_CenterReached * 10, ps.CoreRun_CenterReached, ScoreType.Bonus)));
                    scoreLines.Add(new ScoreLine("PvP.Score.CoreRunEliminations", new Score(ps.CoreRun_Eliminations * 3, ps.CoreRun_Eliminations, ScoreType.Malus)));
                    break;
                default:
                    break;
            }
            scoreLines.Add(new ScoreLine("PvP.Score.Total", new Score(ps.GetTotalPoints(sessionType), 0, ScoreType.BonusPoints)));
            string pl = ps.PlayerName!;
            if (ps.IsSoloScore)
                pl += " (solo)";
            return new ScoreboardEntry(pl, ps.GetTotalPoints(sessionType), scoreLines);
        }

        #region Stats
        
        [return: NotNull]
        private PvPScore GetTotalScore(GamePlayer viewer)
        {
            if (_totalScores.TryGetValue(viewer.InternalID, out var myScore))
            {
                return myScore!;
            }
            else
            {
                return new PvPScore(viewer, false);
            }
        }

        [return: NotNull]
        private IEnumerable<PvPScore> GetGroupScore(GamePlayer viewer)
        {
            IEnumerable<PvPScore> list = Enumerable.Empty<PvPScore>();
            Guild g = viewer.Guild;
            if (g == null)
                g = _playerLastGuilds.GetValueOrDefault(viewer.InternalID);
            
            if (_activeSession != null && g != null)
            {
                /*if (_groupScores.TryGetValue(viewer.Guild, out GroupScore groupScore))
                {
                    list = viewer.Guild.GetListOfOnlineMembers()
                        .Select(p => groupScore.Scores.GetValueOrDefault(p.InternalID)?.PlayerScore ?? new PlayerScore()
                        {
                            PlayerID = p.InternalID,
                            PlayerName = p.Name
                        })
                        .OrderBy(s => s.GetTotalPoints(_activeSession.SessionType))
                        .Prepend(groupScore.Totals);
                }
                else
                {
                    list = viewer.Guild.GetListOfOnlineMembers()
                        .Select(p => new PlayerScore()
                        {
                            PlayerID = p.InternalID,
                            PlayerName = p.Name
                        });
                }*/
                if (_groupScores.TryGetValue(viewer.Guild, out PvPGroupScore groupScore))
                {
                    list = [groupScore];
                }
                else
                {
                    list = [new PvPScore(viewer.Guild)];
                }
            }
            return list;
        }

        private void AddLines(List<string> lines, ScoreboardEntry entry, string language, bool shortStats)
        {
            if (shortStats)
            {
                lines.Add($"  {entry.Player}: " + string.Join(", ", entry.Lines.Select(l => l.ToString(language, shortStats))));
            }
            else
            {
                lines.AddRange(
                    entry.Lines.Select(
                        l => l.ToString(language, shortStats)
                    )
                );
            }
        }

        public IList<string> GetStatistics(GamePlayer viewer, bool all = false)
        {
            var lines = new List<string>();
            string sessionTypeString = "Unknown";
            if (CurrentSession != null)
            {
                switch (CurrentSessionType)
                {
                    case eSessionTypes.Deathmatch:
                        sessionTypeString = "PvP Combats";
                        break;
                    case eSessionTypes.CaptureTheFlag:
                        sessionTypeString = "Flag Capture";
                        break;
                    case eSessionTypes.TreasureHunt:
                        sessionTypeString = "Treasure Hunt";
                        break;
                    case eSessionTypes.BringAFriend:
                        sessionTypeString = "Bring Friends";
                        break;
                    case eSessionTypes.TerritoryCapture:
                        sessionTypeString = "Capture Territories";
                        break;
                    case eSessionTypes.BossHunt:
                        sessionTypeString = "Boss Kill Cooperation";
                        break;
                    case eSessionTypes.KingOfTheHill:
                        sessionTypeString = "King of the Hill";
                        break;
                    case eSessionTypes.CoreRun:
                        sessionTypeString = "Run to the Core";
                        break;
                    default:
                        sessionTypeString = "Unknown";
                        break;
                }
            }

            if (!IsOpen)
            {
                if (viewer.Client.Account.PrivLevel == 1)
                {
                    lines.Add("PvP is CLOSED.");
                }
            }
            else
            {
                if (CurrentZones.Any())
                {
                    foreach (Zone z in CurrentZones)
                    {
                        if (z != null)
                        {
                            string zoneName = !string.IsNullOrEmpty(z.Description) ? z.Description : $"Zone#{z.ID}";
                            lines.Add(sessionTypeString + " in " + zoneName);
                        }
                        else
                        {
                            lines.Add(sessionTypeString + " in Unknown Zone");
                        }
                    }
                }
            }
            lines.Add("");

            if (viewer.Client.Account.PrivLevel > 1)
            {
                lines.Add("");
                lines.Add("-------------------------------------------------------------");
                lines.Add("PvP is " + (IsOpen ? "OPEN" : "CLOSED") + ".");
                lines.Add("Session ID: " + (CurrentSession?.SessionID ?? "(none)"));
                lines.Add("Forced Open: " + _isForcedOpen);
                lines.Add("Session Type: " + sessionTypeString);
                lines.Add("");

                if (_isOpen)
                {
                    if (CurrentZones.Any())
                    {
                        lines.Add("Zones in this PvP session:");
                        foreach (Zone z in CurrentZones)
                        {
                            if (z != null)
                            {
                                string zoneName = !string.IsNullOrEmpty(z.Description)
                                    ? z.Description
                                    : $"Zone#{z.ID}";
                                lines.Add("  > " + zoneName);
                            }
                            else
                            {
                                lines.Add($"  (Unknown Zone)");
                            }
                        }
                    }
                    else
                    {
                        lines.Add("No zones currently configured in the session.");
                    }
                }
                lines.Add("-------------------------------------------------------------");
                lines.Add("");
                lines.Add("");
            }

            // Show scoreboard
            if (!IsOpen || (all && _soloScores.Count == 0 && _groupScores.Count == 0))
            {
                lines.Add("No scoreboard data yet!");
            }
            else
            {
                IEnumerable<PvPScore> scores = Enumerable.Empty<PvPScore>();
                PvPScore groupTotal = null;
                // We want to sort players by total points descending
                var sessionType = CurrentSessionType;
                List<ScoreboardEntry> scoreLines;
                var language = viewer.Client.Account.Language;
                bool shortStats = all;
                if (all)
                {
                    if (CurrentSession.GroupCompoOption is 1 or 3)
                    {
                        // TODO: Don't take solo players if they are part of a group?
                        scores = _soloScores.Values;
                    }
                    if (CurrentSession.GroupCompoOption is 2 or 3)
                    {
                        scores = scores.Concat(_groupScores.Values.Select(s => s.Totals));
                    }

                    lines.Add("Current Scoreboard:");
                    scoreLines = scores
                        .OrderByDescending(s => s.GetTotalPoints(sessionType))
                        .Select(MakeScoreboardEntry)
                        .ToList();

                    foreach (var ps in scoreLines)
                    {
                        AddLines(lines, ps, language, shortStats);
                    }

                    lines.Add("");
                    lines.Add("");
                }
                else if (viewer.IsInPvP)
                {
                    var ourScores = Enumerable.Empty<PvPScore>();
                    bool hasGroup = false;
                    if (CurrentSession.GroupCompoOption == 2 || CurrentSession.GroupCompoOption == 3)
                    {
                        ourScores = GetGroupScore(viewer);
                    }

                    if (ourScores.Any())
                    {
                        hasGroup = true;
                        lines.Add($"Current Scoreboard for {viewer.Guild?.Name ?? viewer.Name}:");
                        foreach (var ps in ourScores.Select(MakeScoreboardEntry))
                        {
                            AddLines(lines, ps, language, shortStats);
                        }
                    }
                    
                    var myScores = _soloScores.GetValueOrDefault(viewer.InternalID) ?? new PvPScore(viewer, true);
                    lines.Add("");
                    if (CurrentSession.AllowGroupDisbandCreate)
                    {
                        lines.Add(viewer.Name + " (solo):");
                    }
                    else
                    {
                        lines.Add(viewer.Name);
                    }
                    AddLines(lines, MakeScoreboardEntry(myScores), language, shortStats);

                    lines.Add("");
                    lines.Add("");
                }
            }

            if (IsOpen)
            {
                lines.Add("");
                lines.Add($"Waiting queue: {_soloQueue.Count} players");
                lines.Add($"Session Max Group size: {CurrentSession?.GroupMaxSize} players");
                lines.Add("");

                int inPvP = 0;
                foreach (var c in WorldMgr.GetAllPlayingClients())
                {
                    if (c?.Player != null && c.Player.IsInPvP)
                        inPvP++;
                }
                lines.Add($"Players currently in PvP: {inPvP}");
            }

            return lines;
        }
        #endregion
    }
}