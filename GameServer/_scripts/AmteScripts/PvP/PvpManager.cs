﻿using System;
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
using Google.Protobuf.WellKnownTypes;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using static AmteScripts.Managers.PvpManager;
using static System.Formats.Asn1.AsnWriter;
using static DOL.GameEvents.GameEvent;
using Newtonsoft.Json.Linq;

namespace AmteScripts.Managers
{
    public class PvpManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        // Time window, e.g. 14:00..22:00
        private static readonly TimeSpan _startTime = new TimeSpan(14, 0, 0);
        private static readonly TimeSpan _endTime = _startTime.Add(TimeSpan.FromHours(8));
        private const int _checkInterval = 30_000; // 30 seconds
        private const int _saveDelay = 5_000;
        private static RegionTimer _timer;
        private RegionTimer _saveTimer;

        private bool _isOpen;
        private bool _isForcedOpen;
        private ushort _currentRegionID;
        [NotNull] private List<Zone> _zones = new();

        private const int _defaultGuildRank = 9;

        private record Spawn(GameNPC? NPC, Position Position)
        {
            public Spawn(): this(null, Position.Nowhere) { }
            public Spawn(GameNPC npc) : this(npc, Position.Nowhere) { }
            public Spawn(Position pos) : this(null, pos) { }
        }

        private record Team(GamePlayer OwnerPlayer, Guild OwnerGuild, Spawn Spawn, int Emblem);

        /// <summary>The chosen session from DB for the day</summary>
        private PvpSession _activeSession;
        [NotNull] private readonly object _sessionLock = new object();

        // Scoreboard
        [NotNull] private readonly Dictionary<string, PlayerScore> _playerScores = new Dictionary<string, PlayerScore>();
        [NotNull] private readonly Dictionary<Guild, GroupScore> _groupScores = new Dictionary<Guild, GroupScore>();

        // Queues
        [NotNull] private readonly List<GamePlayer> _soloQueue = new List<GamePlayer>();
        [NotNull] private readonly List<GamePlayer> _groupQueue = new List<GamePlayer>();

        // For realm-based spawns
        [NotNull] private readonly Dictionary<eRealm, List<GameNPC>> _spawnNpcsRealm = new Dictionary<eRealm, List<GameNPC>>() { { eRealm.Albion, new List<GameNPC>() }, { eRealm.Midgard, new List<GameNPC>() }, { eRealm.Hibernia, new List<GameNPC>() }, };
        // For random spawns (all spawns in session's zones)
        [NotNull] private readonly List<GameNPC> _spawnNpcsGlobal = new List<GameNPC>();
        // For "RandomLock" so we don't reuse the same spawn
        [NotNull] private readonly object _spawnsLock = new();
        [NotNull] private readonly HashSet<GameNPC> _usedSpawns = new HashSet<GameNPC>();
        // Here we track spawns for players & groups
        [NotNull] private readonly ReaderWriterDictionary<GamePlayer, Spawn> _playerSpawns = new ReaderWriterDictionary<GamePlayer, Spawn>();
        [NotNull] private readonly ReaderWriterDictionary<Guild, Spawn> _groupSpawns = new ReaderWriterDictionary<Guild, Spawn>();
        // Here we track solo-based safe areas (player => area)
        [NotNull] private readonly Dictionary<GamePlayer, AbstractArea> _soloAreas = new Dictionary<GamePlayer, AbstractArea>();
        // And group-based safe areas (group => area)
        [NotNull] private readonly Dictionary<Guild, AbstractArea> _groupAreas = new Dictionary<Guild, AbstractArea>();
        // Key = the group object, Value = the ephemeral guild we created
        [NotNull] private readonly Dictionary<Group, Guild> _groupGuilds = new Dictionary<Group, Guild>();
        [NotNull] private readonly Dictionary<Guild, Group> _guildGroups = new Dictionary<Guild, Group>();
        [NotNull] private readonly object _groupsLock = new object();
        // Guilds players are still associated with after leaving, until they join another
        [NotNull] private readonly Dictionary<string, Guild> _playerLeftGuilds = new Dictionary<string, Guild>();
        // Grace timer for PvP players who get linkdead so they don't lose their progress
        [NotNull] private readonly Dictionary<string, RegionTimer> _graceTimers = new();
        [NotNull] private readonly List<Guild> _allGuilds = new();
        [NotNull] private readonly List<GameFlagBasePad> _allBasePads = new List<GameFlagBasePad>();
        private int _flagCounter = 0;
        private RegionTimer _territoryOwnershipTimer = null;

        #region Singleton
        [NotNull] public static PvpManager Instance { get; } = new PvpManager();

        /// <summary>
        /// Called when a PvP linkdead player’s grace period expires.
        /// The callback removes the player from the PvP session (using the same cleanup as for quitting)
        /// and disconnects the client.
        /// </summary>
        protected int LinkdeathPvPGraceCallback(GamePlayer player, RegionTimer timer)
        {
            if (log.IsInfoEnabled)
                log.InfoFormat("PvP grace period expired for linkdead player {0}({1}). Removing from PvP.", player.Name, player.Client.Account.Name);

            if (player.ObjectState == GameObject.eObjectState.Active)
                PvpManager.Instance.KickPlayer(player);
            else
                PvpManager.Instance.CleanupPlayer(player);
            
            return 0;
        }

        public void PlayerLinkDeath(GamePlayer player)
        {
            if (!IsOpen)
                return;
            
            int gracePeriodMs = 20 * 60 * 1000;
            var timerRegion = WorldMgr.GetRegion(1);
            var timer = new RegionTimer(timerRegion.TimeManager);

            lock (_graceTimers)
            {
                _graceTimers[player.InternalID] = timer;
            }
            
            if (CurrentSession!.SessionType == 2) // CTF
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
                    Instance.SaveScore();
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
                    if (TryRestorePlayer(player))
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
                if (!_saveTimer.IsAlive)
                    _saveTimer.Start(_saveDelay);
            }
        }

        public void OnMemberLeaveGuild(Guild guild, GamePlayer player)
        {
            lock (_sessionLock) // lock this to make sure we don't close the pvp while we're adding the player, this would be bad...
            {
                RemoveFromGuildGroup(guild, player);
                if (!_saveTimer.IsAlive)
                    _saveTimer.Start(_saveDelay);
            }
        }

        public void OnMemberJoinGroup(Group group, GamePlayer player)
        {
            lock (_sessionLock) // lock this to make sure we don't close the pvp while we're adding the player, this would be bad...
            {
                AddToGroupGuild(group, player);
                if (!_saveTimer.IsAlive)
                    _saveTimer.Start(_saveDelay);
            }
        }

        public void OnMemberLeaveGroup(Group group, GamePlayer player)
        {
            lock (_sessionLock) // lock this to make sure we don't close the pvp while we're adding the player, this would be bad...
            {
                RemoveFromGroupGuild(group, player);
                if (!_saveTimer.IsAlive)
                    _saveTimer.Start(_saveDelay);
            }
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
            int[] emblemChoices = new int[] { 5061, 6645, 84471, 6272, 55302, 64792, 111402, 39859, 21509, 123019 };
            pvpGuild.Emblem = emblemChoices[Util.Random(emblemChoices.Length - 1)];
            pvpGuild.SaveIntoDatabase();
            return pvpGuild;
        }

        private Guild CreateGuild(GamePlayer leader)
        {
            return CreateGuild("[PVP] " + leader.Name + "'s guild", leader);
        }

        private Guild CreateGuildForGroup(Group group)
        {
            var groupLeader = group.Leader;
            var pvpGuild = CreateGuild(groupLeader);
            if (pvpGuild == null)
            {
                groupLeader.Out.SendMessage(LanguageMgr.GetTranslation(groupLeader.Client.Account.Language, "PvPManager.CannotCreatePvPGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return null;
            }

            _allGuilds.Add(pvpGuild);
            _groupGuilds[group] = pvpGuild;
            _guildGroups[pvpGuild] = group;
            if (_soloAreas.Remove(groupLeader, out AbstractArea leaderArea))
            {
                _groupAreas.Add(pvpGuild, leaderArea);
            }

            // Add each member to the ephemeral guild
            foreach (var member in group.GetPlayersInTheGroup())
            {
                var rank = pvpGuild.GetRankByID(member == groupLeader ? 0 : _defaultGuildRank);
                pvpGuild.AddPlayer(member, rank);
                
                member.IsInPvP = true;
                member.Bind(true);
                member.SaveIntoDatabase();
                if (_soloAreas.Remove(member, out AbstractArea memberArea))
                {
                    // Remove all solo areas - we've already moved the leader's area into group areas, so it won't be found
                    if (memberArea != null)
                    {
                        _cleanupArea(memberArea);
                    }
                }
            }
            return pvpGuild;
        }

        private void AddToGroupGuild(Group group, GamePlayer player)
        {
            if (!IsOpen)
                return;
            
            bool isNew = true;
            Guild guild = null;
            lock (_groupsLock)
            {
                if (_groupGuilds.TryGetValue(group, out guild))
                {
                    isNew = false;
                    if (guild == player.Guild)
                        return; // Nothing to do
                }
                else
                {
                    isNew = true;
                    guild = CreateGuildForGroup(group);
                    if (guild == null)
                        return;
                }
            }

            if (!isNew)
            {
                guild.AddPlayer(player, guild.GetRankByID(_defaultGuildRank), true);
            }
            else
            {
                group.AddMember(player);
                if (player.Guild != group.Leader.Guild)
                {
                    log.Warn($"Player {player.Name} ({player.InternalID}) was added to group led by {group.Leader} ({group.Leader.InternalID}) but they have different guilds!");
                }
                else if (player.GuildRank.RankLevel < group.Leader.GuildRank.RankLevel)
                {
                    group.MakeLeader(player);
                }
            }
        }

        private void RemoveFromGroupGuild(Group group, GamePlayer player)
        {
            if (!IsOpen)
                return;

            if (_groupGuilds.TryGetValue(group, out var guild))
            {
                if (player.Guild != guild)
                    return;
                
                guild.RemovePlayer(string.Empty, player);
                _playerLeftGuilds[player.InternalID] = guild;
            }
        }

        private void AddToGuildGroup(Guild guild, GamePlayer player)
        {
            if (!IsOpen)
                return;
            
            bool isNew = true;
            Group group = null;
            lock (_groupsLock)
            {
                if (_guildGroups.TryGetValue(guild, out group))
                {
                    isNew = false;
                    if (group.IsInTheGroup(player))
                        return; // Nothing to do
                }
                else
                {
                    isNew = true;
                    if (guild.MemberOnlineCount <= 1)
                        return;
                    
                    group = new Group(player);
                    _groupGuilds[group] = guild;
                    _guildGroups[guild] = group;
                }
            }

            if (isNew)
            {
                GamePlayer leader = player;
                foreach (var member in guild.GetListOfOnlineMembers())
                {
                    group.AddMember(member);
                    if (member.GuildRank.RankLevel < leader.GuildRank.RankLevel)
                        leader = member;
                }
                if (leader != player)
                    group.MakeLeader(leader);
            }
            else
            {
                group.AddMember(player);
                if (player.Guild != group.Leader.Guild)
                {
                    log.Warn($"Player {player.Name} ({player.InternalID}) was added to group led by {group.Leader} ({group.Leader.InternalID}) but they have different guilds!");
                }
                else if (player.GuildRank.RankLevel < group.Leader.GuildRank.RankLevel)
                {
                    group.MakeLeader(player);
                }
            }
        }

        private void RemoveFromGuildGroup(Guild guild, GamePlayer player)
        {
            if (!IsOpen || guild == null)
                return;

            if (_guildGroups.TryGetValue(guild, out var group))
            {
                if (group.IsInTheGroup(player))
                    group.RemoveMember(player);
            }

            _playerLeftGuilds[player.InternalID] = guild;
        }
        
        private bool TryRestorePlayer(GamePlayer player)
        {
            RegionTimer graceTimer;
            lock (_graceTimers)
            {
                if (_graceTimers.Remove(player.InternalID, out graceTimer))
                {
                    graceTimer.Stop();
                }
            }

            if (!_zones.Contains(player.CurrentZone))
            {
                return false;
            }

            Guild myGuild = null;
            if (AllowsGroups && player.Guild != null)
            {
                if (player.Guild.GuildType == Guild.eGuildType.PvPGuild)
                    AddToGuildGroup(player.Guild, player);
                else
                    log.Warn($"Player {player.Name} ({player.InternalID}) logged into PvP with non-PvP guild {player.Guild.Name} ({player.Guild.GuildID})");
            }
            if (!AllowsSolo && myGuild == null)
            {
                player.Out.SendMessage("You have been kicked from the PvP group.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            player.IsInPvP = true;
            player.Out.SendMessage("Welcome back! Your PvP state has been preserved.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
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
        #endregion

        #region Open/Close

        [return: NotNull]
        private static PlayerScore ParsePlayer(IEnumerable<string> parameters)
        {
            PlayerScore score = new PlayerScore();
            foreach (var playerScoreInfo in parameters)
            {
                var playerScore = playerScoreInfo.Split('=');
                var playerScoreKey = playerScore[0];

                PropertyInfo p = typeof(PlayerScore).GetProperty(playerScoreKey);
                if (p == null)
                {
                    log.Warn($"PvP score \"{playerScoreKey}\" with value \"{playerScore[1]}\" of player {score.PlayerID} is unknown");
                    continue;
                }

                try
                {
                    p.SetValue(score, Convert.ChangeType(playerScore[1], p.PropertyType));
                }
                catch (Exception ex)
                {
                    log.Warn($"Cannot set PvP score \"{playerScoreKey}\" to value \"{playerScore[1]}\": {ex}");
                }
            }
            return score; 
        }

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
            while (lines.MoveNext() && !string.IsNullOrEmpty(lines.Current))
            {
                var data = lines.Current.Split(';');
                
                var entry = new GroupScoreEntry(ParsePlayer(data));
                var playerId = entry.PlayerScore.PlayerID;
                var groupScore = GetGroupScore(guild);
                groupScore.Scores![playerId] = entry;
                groupScore.Totals.Add(entry.PlayerScore);
            }
        }

        public bool Open(string sessionID, bool force)
        {
            lock (_sessionLock)
            {
                _isForcedOpen = force;
                if (_isOpen)
                    return true;

                _isOpen = true;
            
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
            
                if (string.IsNullOrEmpty(sessionID))
                {
                    try
                    {
                        sessionID = ParseScores();
                    }
                    catch (FileNotFoundException)
                    {
                        // fine
                    }
                    catch (Exception ex)
                    {
                        log.Warn("Could not open file temp/PvPScore.dat: ", ex);
                        File.Copy("temp/PvPScore.dat", $"temp/PvPScore-error-{DateTime.Now}.dat");
                    }
                }

                if (string.IsNullOrEmpty(sessionID))
                {
                    // pick a random session from DB
                    _activeSession = PvpSessionMgr.PickRandomSession();
                    if (_activeSession == null)
                    {
                        log.Warn("No PvP Sessions in DB, cannot open!");
                        _isOpen = false;
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

                log.Info($"PvpManager: Opened session [{_activeSession.SessionID}] Type={_activeSession.SessionType}, SpawnOpt={_activeSession.SpawnOption}");

                List<GameNPC> padSpawnNpcsGlobal = new List<GameNPC>();

                var zoneStrings = _activeSession.ZoneList.Split(',');
                foreach (var zStr in zoneStrings)
                {
                    if (!ushort.TryParse(zStr.Trim(), out ushort zoneId))
                        continue;

                    Zone zone = WorldMgr.GetZone(zoneId);
                    if (zone == null) continue;

                    _zones.Add(zone);
                    var npcs = WorldMgr.GetNPCsByGuild("PVP", eRealm.None).Where(n => n.CurrentZone == zone && n.Name.StartsWith("SPAWN", StringComparison.OrdinalIgnoreCase) &&
                                                                                     !n.Name.StartsWith("PADSPAWN", StringComparison.OrdinalIgnoreCase)).ToList();

                    _spawnNpcsGlobal.AddRange(npcs);

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
                if (_activeSession.SessionType == 2)
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

                if (_activeSession.SessionType == 4)
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

                if (_activeSession.SessionType == 5)
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
            }
            return true;
        }
        
        private string ParseScores()
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
                            ParseGuildEntry(lines, parameters);
                        }
                        break;
                        
                    case "ps":
                        {
                            var playerScore = ParsePlayer(parameters.Skip(1));
                            if (!string.IsNullOrEmpty(playerScore.PlayerID))
                                _playerScores[playerScore.PlayerID] = playerScore;
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

                if (_activeSession != null && _activeSession.SessionType == 5 && _territoryOwnershipTimer != null)
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
                        var region = kv.Key.CurrentRegion;
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
                        area.ZoneIn?.ZoneRegion?.RemoveArea(area);
                    }
                }
                _groupAreas.Clear();

                if (_activeSession != null && _activeSession.SessionType == 4)
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

                if (_activeSession != null && _activeSession.SessionType == 5)
                {
                    TerritoryManager.Instance.ReleaseSubTerritoriesInZones(CurrentZones);
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
        
        private void SaveScore()
        {
            lock (_sessionLock)
            {
                if (!IsOpen)
                    return;
                
                if (!Directory.Exists("temp"))
                    Directory.CreateDirectory("temp");

                var options = new FileStreamOptions();
                options.Mode = FileMode.Create;
                using StreamWriter file = File.CreateText("temp/PvPScore.dat");
                file.WriteLine($"{CurrentSession.SessionID};{_isForcedOpen}");
                file.WriteLine();
                foreach (var (guild, score) in _groupScores)
                {
                    file.WriteLine($"g;{guild.Name}");
                    foreach (var groupEntry in score.Scores)
                    {
                        file.WriteLine(groupEntry.Value.PlayerScore.Serialize());
                    }
                    file.WriteLine();
                }
                foreach (var (player, score) in _playerScores)
                {
                    file.WriteLine("ps;" + score.Serialize());
                }
            }
        }

        public PvpTempArea FindSafeAreaForTarget(GamePlayer player)
        {
            if (player == null) return null;

            // If solo
            if (IsSolo(player))
            {
                if (_soloAreas.TryGetValue(player, out var soloArea))
                    return soloArea as PvpTempArea;
                return null;
            }
            else
            {
                // Group scenario
                if (player.Guild != null && _groupAreas.TryGetValue(player.Guild, out var groupArea))
                {
                    return groupArea as PvpTempArea;;
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
            _playerScores.Clear();
            _groupScores.Clear();
        }

        [return: NotNullIfNotNull(nameof(guild))]
        public GroupScore GetGroupScore(Guild? guild)
        {
            if (guild == null)
                return null;

            if (!_groupScores.TryGetValue(guild, out GroupScore score))
            {
                score = new GroupScore(
                    guild,
                    new PlayerScore() { PlayerID = guild.GuildID, PlayerName = guild.Name },
                    new()
                );
                _groupScores[guild] = score;
            }
            return score;
        }

        public (GroupScore score, GroupScoreEntry entry) GetGroupScoreEntry(GamePlayer player)
        {
            if (player?.Guild == null)
                return (null, null);

            GroupScoreEntry entry;
            if (!_groupScores.TryGetValue(player.Guild, out GroupScore score))
            {
                entry = new GroupScoreEntry(new PlayerScore
                {
                    PlayerID = player.InternalID,
                    PlayerName = player.Name,
                });
                score = new GroupScore(
                    player.Guild,
                    new PlayerScore() { PlayerID = player.Guild.GuildID, PlayerName = player.Guild.Name},
                    new Dictionary<string, GroupScoreEntry>
                    {
                        { player.InternalID, entry }
                    }
                );
                _groupScores[player.Guild] = score;
            }
            else if (!score.Scores.TryGetValue(player.InternalID, out entry))
            {
                entry = new GroupScoreEntry(new PlayerScore
                {
                    PlayerID = player.InternalID,
                    PlayerName = player.Name
                });
                score.Scores[player.InternalID] = entry;
            }
            return (score, entry);
        }
        
        [return: NotNullIfNotNull(nameof(player))]
        public PlayerScore GetIndividualScore(GamePlayer player)
        {
            if (player == null)
                return null;

            string pid = player.InternalID;
            if (!_playerScores.TryGetValue(pid, out PlayerScore score))
            {
                score = new PlayerScore()
                {
                    PlayerID = pid,
                    PlayerName = player.Name
                };
                _playerScores[pid] = score;
            }
            return score;
        }

        public void HandlePlayerKill(GamePlayer killer, GamePlayer victim)
        {
            if (!_isOpen || _activeSession == null) return;

            if (!killer.IsInPvP || !victim.IsInPvP) return;

            switch (_activeSession.SessionType)
            {
                case 1:
                    UpdateScores_PvPCombat(killer, victim);
                    break;
                case 2:
                    UpdateScores_FlagCapture(killer, victim, wasFlagCarrier: false);
                    break;
                case 3:
                    UpdateScores_TreasureHunt(killer, victim);
                    break;
                case 4:
                    UpdateScores_BringFriends(killer, victim);
                    break;
                case 5:
                    UpdateScores_CaptureTerritories(killer, victim);
                    break;
                case 6:
                    UpdateScores_BossKillCoop(killer, victim);
                    break;
                default:
                    break;
            }

            if (!_saveTimer.IsAlive)
            {
                _saveTimer.Start(_saveDelay);
            }
        }

        private bool IsSolo(GamePlayer killer)
        {
            return killer.Guild == null || killer.Group is not { MemberCount: > 1 };
        }

        /// <summary>
        /// "PvP Combat" (SessionType=1).
        /// Scoring rules:
        /// - Solo kill => 10 pts (+30% if victim is RR5+)
        /// - Group kill => 5 pts (+30% if victim is RR5+)
        /// </summary>
        private void UpdateScores_PvPCombat(GamePlayer killer, GamePlayer victim)
        {
            var killerScore = GetIndividualScore(killer);
            if (killerScore == null) return;

            // check if victim is RR5 or more
            bool rr5bonus = (victim.RealmLevel >= 40);
            bool isSolo = (killer.Group == null || killer.Group.MemberCount <= 1);
            var (groupScore, groupEntry) = GetGroupScoreEntry(killer);
            var fun = (PlayerScore score) =>
            {
                if (isSolo)
                {
                    score.PvP_SoloKills++;
                    int basePts = 10;
                    if (rr5bonus) basePts = (int)(basePts * 1.30);
                    score.PvP_SoloKillsPoints += basePts;
                }
                else
                {
                    score.PvP_GroupKills++;
                    int basePts = 5;
                    if (rr5bonus) basePts = (int)(basePts * 1.30);
                    score.PvP_GroupKillsPoints += basePts;
                }
            };
            fun(killerScore);
            if (groupScore != null)
            {
                fun(groupEntry.PlayerScore);
                fun(groupScore.Totals);
            }
        }

        /// <summary>
        /// "Flag Capture" (SessionType=2).
        /// In reality, you'd also handle separate events for "bring flag to outpost",
        /// "flag ownership tick", etc...
        /// + special points if the victim was carrying the flag.
        /// </summary>
        public void UpdateScores_FlagCapture(GamePlayer killer, GamePlayer victim, bool wasFlagCarrier)
        {
            var killerScore = GetIndividualScore(killer);
            if (killerScore == null) return;

            bool isSolo = IsSolo(killer);
            var (groupScore, groupEntry) = GetGroupScoreEntry(killer);
            var fun = (PlayerScore score) =>
            {
                if (!wasFlagCarrier)
                {
                    if (isSolo)
                    {
                        score.Flag_SoloKills++;
                        score.Flag_SoloKillsPoints += 4;
                    }
                    else
                    {
                        score.Flag_GroupKills++;
                        score.Flag_GroupKillsPoints += 2;
                    }
                }
                else
                {
                    score.Flag_KillFlagCarrierCount++;
                    score.Flag_KillFlagCarrierPoints += 6;
                }
            };
            fun(killerScore);
            if (groupScore != null)
            {
                fun(groupEntry.PlayerScore);
                fun(groupScore.Totals);
            }
        }
        
        /// <summary>
        /// "Treasure Hunt" (SessionType=3).
        /// </summary>
        private void UpdateScores_TreasureHunt(GamePlayer killer, GamePlayer victim)
        {
            var killerScore = GetIndividualScore(killer);
            if (killerScore == null) return;

            bool isSolo = IsSolo(killer);
            var (groupScore, groupEntry) = GetGroupScoreEntry(killer);
            var fun = (PlayerScore score) =>
            {
                if (isSolo)
                {
                    score.Treasure_SoloKills++;
                    score.Treasure_SoloKillsPoints += 4;
                }
                else
                {
                    score.Treasure_GroupKills++;
                    score.Treasure_GroupKillsPoints += 2;
                }
            };
            fun(killerScore);
            if (groupScore != null)
            {
                fun(groupEntry.PlayerScore);
                fun(groupScore.Totals);
            }
        }

        /// <summary>
        /// "Bring Friends" (SessionType=4).
        /// </summary>
        private void UpdateScores_BringFriends(GamePlayer killer, GamePlayer victim)
        {
            var killerScore = GetIndividualScore(killer);
            if (killerScore == null) return;

            bool isSolo = IsSolo(killer);
            var (groupScore, groupEntry) = GetGroupScoreEntry(killer);
            var fun = (PlayerScore score) =>
            {
                if (isSolo)
                {
                    score.Friends_SoloKills++;
                    score.Friends_SoloKillsPoints += 4;
                }
                else
                {
                    score.Friends_GroupKills++;
                    score.Friends_GroupKillsPoints += 2;
                }
            };
            fun(killerScore);
            if (groupScore != null)
            {
                fun(groupEntry.PlayerScore);
                fun(groupScore.Totals);
            }
        }

        /// <summary>
        /// "Capture Territories" (SessionType=5).
        /// </summary>
        private void UpdateScores_CaptureTerritories(GamePlayer killer, GamePlayer victim)
        {
            var killerScore = GetIndividualScore(killer);
            if (killerScore == null) return;

            bool isSolo = IsSolo(killer);
            var (groupScore, groupEntry) = GetGroupScoreEntry(killer);
            var fun = (PlayerScore score) =>
            {
                if (isSolo)
                {
                    score.Terr_SoloKills++;
                    score.Terr_SoloKillsPoints += 4;
                }
                else
                {
                    score.Terr_GroupKills++;
                    score.Terr_GroupKillsPoints += 2;
                }
            };
            fun(killerScore);
            if (groupScore != null)
            {
                fun(groupEntry.PlayerScore);
                fun(groupScore.Totals);
            }
        }

        /// <summary>
        /// "Boss Kill Cooperation" (SessionType=6).
        /// </summary>
        private void UpdateScores_BossKillCoop(GamePlayer killer, GamePlayer victim)
        {
            var killerScore = GetIndividualScore(killer);
            if (killerScore == null) return;

            // check if victim is RR5 or more
            bool rr5bonus = (victim.RealmLevel >= 40);

            bool isSolo = IsSolo(killer);
            var (groupScore, groupEntry) = GetGroupScoreEntry(killer);
            var fun = (PlayerScore score) =>
            {
                if (isSolo)
                {
                    score.Boss_SoloKills++;
                    int basePts = 30;
                    if (rr5bonus) basePts = (int)(basePts * 1.30);
                    score.Boss_SoloKillsPoints += basePts;
                }
                else
                {
                    score.Boss_GroupKills++;
                    int basePts = 15;
                    if (rr5bonus) basePts = (int)(basePts * 1.30);
                    score.Boss_GroupKillsPoints += basePts;
                }
            };
            fun(killerScore);
            if (groupScore != null)
            {
                fun(groupEntry.PlayerScore);
                fun(groupScore.Totals);
            }
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

            // Store old info, remove guild
            StoreOldPlayerInfo(player);

            if (!TeleportSoloPlayer(player))
            {
                _cleanupPlayer(player);
                return false;
            }
            player.Bind(true);
            player.SaveIntoDatabase();

            if (!_saveTimer.IsAlive)
            {
                _saveTimer.Start(_saveDelay);
            }

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

            foreach (var member in group.GetPlayersInTheGroup())
            {
                StoreOldPlayerInfo(member);
            }

            Guild pvpGuild;
            lock (_groupsLock)
            {
                if (!_groupGuilds.TryGetValue(group, out pvpGuild))
                    pvpGuild = CreateGuildForGroup(group);
            }
            if (pvpGuild == null)
            {
                _cleanupGroup(group, "PvPManager.CannotCreatePvPGuild", false);
                return false;
            }

            if (!TeleportEntireGroup(pvpGuild, groupLeader))
            {
                // No message, TeleportEntireGroup will do it
                _cleanupGroup(group, string.Empty, false);
                return false;
            }

            if (!_saveTimer.IsAlive)
            {
                _saveTimer.Start(_saveDelay);
            }
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
        
        private void _cleanupGroup([NotNull] Group group, string messageKey = "", bool disband = true)
        {
            foreach (GamePlayer player in group.GetPlayersInTheGroup())
            {
                if (!string.IsNullOrEmpty(messageKey))
                    player.SendTranslatedMessage(messageKey);
                _cleanupPlayer(player, disband);
            }
        }

        private void _cleanupPlayer(GamePlayer player, bool disband = true)
        {
            lock (_graceTimers)
            {
                _graceTimers.Remove(player.InternalID);
            }

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
                        player.Guild.RemovePlayer("PVP", player);
                        if (disband && _guildGroups.TryGetValue(pvpGuild, out var g))
                        {
                            g.RemoveMember(player);
                        }
                    }
                }
            }

            int totalRemoved = RemoveItemsFromPlayer(player);
            if (totalRemoved > 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.PvPTreasureRemoved", totalRemoved), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            DequeueSolo(player);
            if (_soloAreas.Remove(player, out var area))
            {
                _cleanupArea(area);
            }
            lock (_spawnsLock)
            {
                if (_playerSpawns.Remove(player, out var spawn) && spawn.NPC != null)
                {
                    lock (_usedSpawns)
                    {
                        _usedSpawns.Remove(spawn.NPC);
                    }
                }
            }
            if (pvpGuild != null)
            {
                // Check if *all* members have left. If so, remove area:
                if (!pvpGuild.GetListOfOnlineMembers().Any(m => m.IsInPvP))
                {
                    if (_groupAreas.Remove(pvpGuild, out AbstractArea grpArea))
                    {
                        _cleanupArea(grpArea);
                    }
                    lock (_spawnsLock)
                    {
                        if (_groupSpawns.Remove(pvpGuild, out var spawn) && spawn.NPC != null)
                        {
                            lock (_usedSpawns)
                            {
                                _usedSpawns.Remove(spawn.NPC);
                            }
                        }
                    }
                }
            }

            if (!_saveTimer.IsAlive)
            {
                _saveTimer.Start(_saveDelay);
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.LeftPvP"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
        
        private void _cleanupArea(AbstractArea area)
        {
            if (area is PvpTempArea pvpArea)
                pvpArea.RemoveAllOwnedObjects();

            area.ZoneIn.ZoneRegion.RemoveArea(area);
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
                    if (IsOpen && player.IsInPvP && CurrentSession.SessionType == 2)
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
        private void StoreOldPlayerInfo(GamePlayer player)
        {
            RvrPlayer record = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(player.InternalID));
            bool isNew = false;
            if (record == null)
            {
                record = new RvrPlayer(player);
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
        #endregion

        #region Teleport logic
        private bool TeleportSoloPlayer(GamePlayer player)
        {
            if (!player.IsInPvP)
                player.IsInPvP = true;

            Spawn spawnPos = FindSpawnPosition(player.Realm);
            if (spawnPos == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.CannotFindSpawn"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            lock (_spawnsLock)
            {
                _playerSpawns[player] = spawnPos;
            }
            player.MoveTo(spawnPos.Position);

            // if session says create area + randomlock => do it
            if (_activeSession.CreateCustomArea &&
                _activeSession.SpawnOption.Equals("RandomLock", StringComparison.OrdinalIgnoreCase))
            {
                CreateSafeAreaForSolo(player, spawnPos.Position, _activeSession.TempAreaRadius);
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.JoinedPvP", _activeSession.SessionID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        private bool TeleportEntireGroup(Guild pvpGuild, GamePlayer leader)
        {
            var group = leader.Group;
            if (group == null) return false;

            var spawnPos = FindSpawnPosition(pvpGuild.Realm);
            if (spawnPos == null)
            {
                foreach (GamePlayer player in group.GetPlayersInTheGroup())
                {
                    player.SendTranslatedMessage("PvPManager.NoSpawn");
                }
                return false;
            }
            _groupSpawns[pvpGuild] = spawnPos;
            float distance = 32;
            float increment = (float)(2 * Math.PI / group.MemberCount);
            float baseAngle = (float)(spawnPos.Position.Orientation.InRadians + Math.PI / 2); // 90 degrees from where the spawn npc is facing
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
                ++i;
            }

            // Only create one safe area for the entire group (use leader's position)
            if (_activeSession.CreateCustomArea &&
                _activeSession.SpawnOption.Equals("RandomLock", StringComparison.OrdinalIgnoreCase))
            {
                CreateSafeAreaForGroup(leader, spawnPos.Position, _activeSession.TempAreaRadius);
            }

            leader.Out.SendMessage(LanguageMgr.GetTranslation(leader.Client.Account.Language, "PvPManager.GroupJoinedPvP", _activeSession.SessionID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
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
            if (spawnOpt == "randomlock")
            {
                lock (_usedSpawns)
                {
                    var available = _spawnNpcsGlobal.Where(n => !_usedSpawns.Contains(n)).ToList();
                    if (available.Count > 0)
                    {
                        var chosen = available[Util.Random(available.Count - 1)];
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
                    var chosen = _spawnNpcsGlobal[Util.Random(_spawnNpcsGlobal.Count - 1)];
                    return new Spawn(chosen);
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

                // for each player, store old info, remove guild
                foreach (var p in groupPlayers)
                {
                    StoreOldPlayerInfo(p);
                }

                var pvpGuild = CreateGuildForGroup(newGroup);
                if (pvpGuild == null)
                {
                    _cleanupGroup(newGroup, "PvPManager.CannotCreatePvPGuild", false);
                    return;
                }

                if (!TeleportEntireGroup(pvpGuild, randomLeader))
                {
                    _cleanupGroup(newGroup, string.Empty, false);
                    return;
                }

                if (!_saveTimer.IsAlive)
                {
                    _saveTimer.Start(_saveDelay);
                }
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
            bool isBringFriends = _activeSession.SessionType == 4;
            string areaName = player.Name + "'s Solo Outpost";

            AbstractArea areaObject = new PvpTempArea(player, pos.X, pos.Y, pos.Z, radius, isBringFriends);

            player.CurrentRegion.AddArea(areaObject);
            _soloAreas[player] = areaObject;

            log.Info("PvpManager: Created a solo outpost for " + player.Name);
            _fillOutpostItems(player, pos, areaObject);
        }

        private void CreateSafeAreaForGroup(GamePlayer leader, Position pos, int radius)
        {
            if (leader.Group == null) return;

            bool isBringFriends = _activeSession.SessionType == 4;
            // Retrieve the ephemeral guild you made for the group
            var group = leader.Group;
            _groupGuilds.TryGetValue(group, out Guild pvpGuild);
            AbstractArea areaObject = new PvpTempArea(leader, pos.X, pos.Y, pos.Z, radius, isBringFriends);
            leader.CurrentRegion.AddArea(areaObject);
            _groupAreas[pvpGuild] = areaObject;
            
            log.Info("PvpManager: Created a group outpost for " + pvpGuild.Name);
            _fillOutpostItems(leader, pos, areaObject);
        }

        private void _fillOutpostItems(GamePlayer leader, Position pos, AbstractArea area)
        {
            List<GameStaticItem>? items = _activeSession.SessionType switch
            {
                2 => PvPAreaOutposts.CreateCaptureFlagOutpostPad(pos, leader),
                3 => PvPAreaOutposts.CreateTreasureHuntBase(pos, leader),
                4 or 5 => PvPAreaOutposts.CreateGuildOutpostTemplate01(pos, leader)
            };

            if (items != null)
            {
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
        }
        
        #endregion

        #region BringAFriend Methods & Scores
        private void OnBringAFriend(DOLEvent e, object sender, EventArgs args)
        {
            if (!_isOpen || _activeSession == null || _activeSession.SessionType != 4)
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
            var playerRecord = GetIndividualScore(owner);
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

                if (IsSolo(owner))
                    _soloAreas.TryGetValue(owner, out area);
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

            var doAdd = (PlayerScore score) =>
            {
                score.Friends_BroughtFriendsPoints += basePoints;
                score.Friends_BroughtFamilyBonus += bonus;
            };
            if (owner.Guild is { GuildType: Guild.eGuildType.PvPGuild })
            {
                var groupScore = GetGroupScore(owner.Guild);

                doAdd(groupScore.Totals);
                foreach (var member in owner.Guild.GetListOfOnlineMembers())
                {
                    doAdd(groupScore.GetOrCreateScore(member));
                }
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
            if (!_isOpen || _activeSession == null || _activeSession.SessionType != 4)
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
                var followedScore = GetIndividualScore(followedPlayer);
                var groupScore = GetGroupScore(followedPlayer.Guild);
                var playerGroupScore = groupScore?.GetOrCreateScore(followedPlayer);
                var doAdd = (PlayerScore score) =>
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
            }

            // 2) If the *killer* is a player, and the mob was following someone => killer gets +2
            if (friendMob.PlayerFollow != null && IsInActivePvpZone(killerPlayer))
            {
                if (killerPlayer != friendMob.PlayerFollow)
                {
                    var killerScore = GetIndividualScore(killerPlayer);
                    var groupScore = GetGroupScore(killerPlayer.Guild);
                    var playerGroupScore = groupScore?.GetOrCreateScore(killerPlayer);
                    var doAdd = (PlayerScore score) =>
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
                }
            }
        }
        #endregion

        #region CaptureTerritories Methods
        private int AwardTerritoryOwnershipPoints(RegionTimer timer)
        {
            // 1) Make sure session is open, type=5
            if (!_isOpen || _activeSession == null || _activeSession.SessionType != 5)
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

                var groupScore = GetGroupScore(owningGuild);
                groupScore.Totals.Terr_TerritoriesOwnershipPoints++;
                foreach (var member in owningGuild.GetListOfOnlineMembers())
                {
                    if (member.IsInPvP && member.Client != null && member.CurrentRegion != null && PvpManager.Instance.IsInActivePvpZone(member))
                    {
                        groupScore.GetOrCreateScore(member).Terr_TerritoriesOwnershipPoints++;
                        var score = GetIndividualScore(member);
                        score.Terr_TerritoriesOwnershipPoints += 1;
                    }
                }
            }

            return 30000;
        }
        
        public void AwardTerritoryCapturePoints(Territory territory, GamePlayer player)
        {
            if (CurrentSession is not { SessionType: 5 })
                return;

            if (territory.Type != Territory.eType.Subterritory) // Is this check necessary?
                return;

            if (territory.Zone == null || !CurrentZones.Contains(territory.Zone))
                return;

            var doAdd = (PlayerScore score) =>
            {
                score.Terr_TerritoriesCapturedPoints += 20;
                score.Terr_TerritoriesCapturedCount++;
            };
            if (player.Guild != null)
            {
                var groupScores = GetGroupScore(player.Guild);
                doAdd(groupScores.Totals);
                doAdd(groupScores.GetOrCreateScore(player));
            }
            doAdd(GetIndividualScore(player));
        }

        private bool IsTerritoryInSessionZones(Territory territory, IEnumerable<ushort> zoneIDs)
        {
            if (territory.Zone == null)
                return false;
            return zoneIDs.Contains(territory.Zone.ID);
        }
        #endregion
        
        #region CTF methods

        public void AwardCTFOwnershipPoints(GamePlayer player, int points)
        {
            if (!IsOpen)
                return;
            
            var doAdd = (PlayerScore score) =>
            {
                score.Flag_OwnershipPoints += points;
            };
            
            if (player.Guild != null)
            {
                var groupScores = GetGroupScore(player.Guild); 
                doAdd(groupScores.Totals);
                doAdd(groupScores.GetOrCreateScore(player));
            }
            doAdd(GetIndividualScore(player));
        }

        public void AwardCTFOwnershipPoints(Guild guild, int points)
        {
            if (!IsOpen)
                return;
            
            var doAdd = (PlayerScore score) =>
            {
                score.Flag_OwnershipPoints += points;
            };
            
            if (guild != null)
            {
                var groupScores = GetGroupScore(guild); 
                doAdd(groupScores.Totals);
                foreach (var player in guild.GetListOfOnlineMembers())
                {
                    doAdd(groupScores.GetOrCreateScore(player));
                    doAdd(GetIndividualScore(player));
                }
            }
        }

        public void AwardCTFCapturePoints(GamePlayer player)
        {
            if (!IsOpen)
                return;
            
            var doAdd = (PlayerScore score) =>
            {
                score.Flag_FlagReturnsCount += 1;
                score.Flag_FlagReturnsPoints += 20;
            };
            
            if (player.Guild != null)
            {
                var groupScores = GetGroupScore(player.Guild); 
                doAdd(groupScores.Totals);
                doAdd(groupScores.GetOrCreateScore(player));
            }
            doAdd(GetIndividualScore(player));
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

        #region PlayerScores

        public class Unsaved : System.Attribute
        {
        }
        
        public class PlayerScore
        {
            // --- Universal (for all sessions) ---
            // We'll keep track of the player's name/ID just for clarity
            public string PlayerName { get; set; }
            public string PlayerID { get; set; }

            // --- For session type #1: PvP Combat ---
            [DefaultValue(0)]
            public int PvP_SoloKills { get; set; }
            [DefaultValue(0)]
            public int PvP_SoloKillsPoints { get; set; }
            [DefaultValue(0)]
            public int PvP_GroupKills { get; set; }
            [DefaultValue(0)]
            public int PvP_GroupKillsPoints { get; set; }

            // --- For session type #2: Flag Capture ---
            [DefaultValue(0)]
            public int Flag_SoloKills { get; set; }
            [DefaultValue(0)]
            public int Flag_SoloKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Flag_GroupKills { get; set; }
            [DefaultValue(0)]
            public int Flag_GroupKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Flag_KillFlagCarrierCount { get; set; }
            [DefaultValue(0)]
            public int Flag_KillFlagCarrierPoints { get; set; }
            [DefaultValue(0)]
            public int Flag_FlagReturnsCount { get; set; }
            [DefaultValue(0)]
            public int Flag_FlagReturnsPoints { get; set; }
            [DefaultValue(0)]
            public int Flag_OwnershipPoints { get; set; }

            // --- For session type #3: Treasure Hunt ---
            [DefaultValue(0)]
            public int Treasure_SoloKills { get; set; }
            [DefaultValue(0)]
            public int Treasure_SoloKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Treasure_GroupKills { get; set; }
            [DefaultValue(0)]
            public int Treasure_GroupKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Treasure_BroughtTreasuresPoints { get; set; }
            [DefaultValue(0)]
            public int Treasure_StolenTreasuresPoints { get; set; }

            // --- For session type #4: Bring Friends ---
            [DefaultValue(0)]
            public int Friends_SoloKills { get; set; }
            [DefaultValue(0)]
            public int Friends_SoloKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Friends_GroupKills { get; set; }
            [DefaultValue(0)]
            public int Friends_GroupKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Friends_BroughtFriendsPoints { get; set; }
            [DefaultValue(0)]
            public int Friends_BroughtFamilyBonus { get; set; }
            [DefaultValue(0)]
            public int Friends_FriendKilledCount { get; set; }
            [DefaultValue(0)]
            public int Friends_FriendKilledPoints { get; set; }
            [DefaultValue(0)]
            public int Friends_KillEnemyFriendCount { get; set; }
            [DefaultValue(0)]
            public int Friends_KillEnemyFriendPoints { get; set; }

            // --- For session type #5: Capture Territories ---
            [DefaultValue(0)]
            public int Terr_SoloKills { get; set; }
            [DefaultValue(0)]
            public int Terr_SoloKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Terr_GroupKills { get; set; }
            [DefaultValue(0)]
            public int Terr_GroupKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Terr_TerritoriesCapturedCount { get; set; }
            [DefaultValue(0)]
            public int Terr_TerritoriesCapturedPoints { get; set; }
            [DefaultValue(0)]
            public int Terr_TerritoriesOwnershipPoints { get; set; }

            // --- For session type #6: Boss Kill Cooperation ---
            [DefaultValue(0)]
            public int Boss_SoloKills { get; set; }
            [DefaultValue(0)]
            public int Boss_SoloKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Boss_GroupKills { get; set; }
            [DefaultValue(0)]
            public int Boss_GroupKillsPoints { get; set; }
            [DefaultValue(0)]
            public int Boss_BossHitsCount { get; set; }
            [DefaultValue(0)]
            public int Boss_BossHitsPoints { get; set; }
            [DefaultValue(0)]
            public int Boss_BossKillsCount { get; set; }
            [DefaultValue(0)]
            public int Boss_BossKillsPoints { get; set; }

            public PlayerScore Add(PlayerScore rhs)
            {
                PvP_SoloKills += rhs.PvP_SoloKills;
                PvP_SoloKillsPoints += rhs.PvP_SoloKillsPoints;
                PvP_GroupKills += rhs.PvP_GroupKills;
                PvP_GroupKillsPoints += rhs.PvP_GroupKillsPoints;
                Flag_SoloKills += rhs.Flag_SoloKills;
                Flag_SoloKillsPoints += rhs.Flag_SoloKillsPoints;
                Flag_GroupKills += rhs.Flag_GroupKills;
                Flag_GroupKillsPoints += rhs.Flag_GroupKillsPoints;
                Flag_KillFlagCarrierCount += rhs.Flag_KillFlagCarrierCount;
                Flag_KillFlagCarrierPoints += rhs.Flag_KillFlagCarrierPoints;
                Flag_FlagReturnsCount += rhs.Flag_FlagReturnsCount;
                Flag_FlagReturnsPoints += rhs.Flag_FlagReturnsPoints;
                Flag_OwnershipPoints += rhs.Flag_OwnershipPoints;
                Treasure_SoloKills += rhs.Treasure_SoloKills;
                Treasure_SoloKillsPoints += rhs.Treasure_SoloKillsPoints;
                Treasure_GroupKills += rhs.Treasure_GroupKills;
                Treasure_GroupKillsPoints += rhs.Treasure_GroupKillsPoints;
                Treasure_BroughtTreasuresPoints += rhs.Treasure_BroughtTreasuresPoints;
                Treasure_StolenTreasuresPoints += rhs.Treasure_StolenTreasuresPoints;
                Friends_SoloKills += rhs.Friends_SoloKills;
                Friends_SoloKillsPoints += rhs.Friends_SoloKillsPoints;
                Friends_GroupKills += rhs.Friends_GroupKills;
                Friends_GroupKillsPoints += rhs.Friends_GroupKillsPoints;
                Friends_BroughtFriendsPoints += rhs.Friends_BroughtFriendsPoints;
                Friends_BroughtFamilyBonus += rhs.Friends_BroughtFamilyBonus;
                Friends_FriendKilledCount += rhs.Friends_FriendKilledCount;
                Friends_FriendKilledPoints += rhs.Friends_FriendKilledPoints;
                Friends_KillEnemyFriendCount += rhs.Friends_KillEnemyFriendCount;
                Friends_KillEnemyFriendPoints += rhs.Friends_KillEnemyFriendPoints;
                Terr_SoloKills += rhs.Terr_SoloKills;
                Terr_SoloKillsPoints += rhs.Terr_SoloKillsPoints;
                Terr_GroupKills += rhs.Terr_GroupKills;
                Terr_GroupKillsPoints += rhs.Terr_GroupKillsPoints;
                Terr_TerritoriesCapturedCount += rhs.Terr_TerritoriesCapturedCount;
                Terr_TerritoriesCapturedPoints += rhs.Terr_TerritoriesCapturedPoints;
                Terr_TerritoriesOwnershipPoints += rhs.Terr_TerritoriesOwnershipPoints;
                Boss_SoloKills += rhs.Boss_SoloKills;
                Boss_SoloKillsPoints += rhs.Boss_SoloKillsPoints;
                Boss_GroupKills += rhs.Boss_GroupKills;
                Boss_GroupKillsPoints += rhs.Boss_GroupKillsPoints;
                Boss_BossHitsCount += rhs.Boss_BossHitsCount;
                Boss_BossHitsPoints += rhs.Boss_BossHitsPoints;
                Boss_BossKillsCount += rhs.Boss_BossKillsCount;
                Boss_BossKillsPoints += rhs.Boss_BossKillsPoints;
                return this;
            }

            /// <summary>
            /// Helper: returns total points for the current session type,
            /// summing only the relevant fields.
            /// You can expand this logic to handle any special bonus, etc.
            /// </summary>
            public int GetTotalPoints(int sessionType)
            {
                switch (sessionType)
                {
                    case 1: // PvP Combats
                        return PvP_SoloKillsPoints + PvP_GroupKillsPoints;

                    case 2: // Flag Capture
                        return Flag_SoloKillsPoints +
                               Flag_GroupKillsPoints +
                               Flag_KillFlagCarrierPoints +
                               Flag_FlagReturnsPoints +
                               Flag_OwnershipPoints;

                    case 3: // Treasure Hunt
                        return Treasure_SoloKillsPoints +
                               Treasure_GroupKillsPoints +
                               Treasure_BroughtTreasuresPoints -
                               Treasure_StolenTreasuresPoints;

                    case 4: // Bring Friends
                        return Friends_SoloKillsPoints +
                               Friends_GroupKillsPoints +
                               Friends_BroughtFriendsPoints +
                               Friends_BroughtFamilyBonus -
                               Friends_FriendKilledPoints +
                               Friends_KillEnemyFriendPoints;

                    case 5: // Capture Territories
                        return Terr_SoloKillsPoints +
                               Terr_GroupKillsPoints +
                               Terr_TerritoriesCapturedPoints +
                               Terr_TerritoriesOwnershipPoints;

                    case 6: // Boss Kill Cooperation
                        return Boss_SoloKillsPoints +
                               Boss_GroupKillsPoints +
                               Boss_BossHitsPoints +
                               Boss_BossKillsPoints;

                    default:
                        return 0;
                }
            }

            public string Serialize()
            {
                var playerScoreType = typeof(PlayerScore);
                var properties = playerScoreType.GetProperties();
                return string.Join(
                    ';', properties
                        .Where(p =>
                        {
                            var filter = (DefaultValueAttribute?)p.GetCustomAttribute(typeof(DefaultValueAttribute));
                            var value = p.GetValue(this);
                            return filter == null || (filter.Value == null ? value != null : !filter.Value.Equals(value));
                        })
                        .Select(p => p.Name + "=" + p.GetValue(this))
                );
            }
        }

        public record GroupScoreEntry(PlayerScore PlayerScore) {}

        public record GroupScore(Guild Guild, PlayerScore Totals, Dictionary<string, GroupScoreEntry> Scores)
        {
            public int GetTotalPoints(int sessionType)
            {
                return Scores.Values.Select(e => e.PlayerScore.GetTotalPoints(sessionType)).Aggregate(0, (a, b) => a + b);
            }

            [return: NotNull]
            public PlayerScore GetOrCreateScore([NotNull] GamePlayer player)
            {
                if (!Scores.TryGetValue(player.InternalID, out var score))
                {
                    score = new GroupScoreEntry(new PlayerScore()
                    {
                        PlayerID = player.InternalID,
                        PlayerName = player.Name
                    });
                    Scores[player.InternalID] = score;
                }
                return score!.PlayerScore!;
            }

            public int PlayerCount => Scores.Count;
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

        private ScoreboardEntry MakeScoreboardEntry(PlayerScore ps)
        {
            if (!IsOpen)
                return null;
            
            var sessionType = _activeSession!.SessionType;
            List<ScoreLine> scoreLines = new();
            switch (sessionType)
            {
                case 1: // Pure PvP Combat
                    scoreLines.Add(new ScoreLine("PvP.Score.PvPSoloKills", new Score(ps.PvP_SoloKillsPoints, ps.PvP_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.PvPGrpKills", new Score(ps.PvP_GroupKillsPoints, ps.PvP_GroupKills)));
                    break;

                case 2: // Flag Capture
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPSoloKills", new Score(ps.Flag_SoloKillsPoints, ps.Flag_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPGrpKills", new Score(ps.Flag_GroupKillsPoints, ps.Flag_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPFlagCarrierKillBonus", new Score(ps.Flag_KillFlagCarrierPoints, ps.Flag_KillFlagCarrierCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPFlagsCaptured", new Score(ps.Flag_FlagReturnsPoints, ps.Flag_FlagReturnsCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FlagPvPOwnership", new Score(ps.Flag_OwnershipPoints, 0, ScoreType.BonusPoints)));
                    break;

                case 3: // Treasure Hunt
                    scoreLines.Add(new ScoreLine("PvP.Score.TreasurePvPSoloKills", new Score(ps.Treasure_SoloKillsPoints, ps.Treasure_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.TreasurePvPGrpKills", new Score(ps.Treasure_GroupKillsPoints, ps.Treasure_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.TreasurePvPTreasurePoints", new Score(ps.Treasure_BroughtTreasuresPoints, 0, ScoreType.BonusPoints)));
                    scoreLines.Add(new ScoreLine("PvP.Score.TreasurePvPStolenItemPenalty", new Score(ps.Treasure_StolenTreasuresPoints, 0, ScoreType.MalusPoints)));
                    break;

                case 4: // Bring Friends
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPSoloKills", new Score(ps.Friends_SoloKillsPoints, ps.Friends_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPGrpKills", new Score(ps.Friends_GroupKillsPoints, ps.Friends_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPBroughtFriends", new Score(ps.Friends_BroughtFriendsPoints, 0, ScoreType.BonusPoints)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPFamilyBonus", new Score(ps.Friends_BroughtFamilyBonus, 0, ScoreType.BonusPoints)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPLostFriends", new Score(ps.Friends_FriendKilledPoints, ps.Friends_FriendKilledCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.FriendsPvPKilledOthersFriends", new Score(ps.Friends_KillEnemyFriendPoints, ps.Friends_KillEnemyFriendCount)));
                    break;

                case 5: // Capture Territories
                    scoreLines.Add(new ScoreLine("PvP.Score.CTTPvPSoloKills", new Score(ps.Terr_SoloKillsPoints, ps.Terr_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.CTTPvPGrpKills", new Score(ps.Terr_GroupKillsPoints, ps.Terr_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.CTTPvPTerritoryCaptures", new Score(ps.Terr_TerritoriesCapturedPoints, ps.Terr_TerritoriesCapturedCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.CTTPvPOwnership", new Score(ps.Terr_TerritoriesOwnershipPoints, 0, ScoreType.BonusPoints)));
                    break;

                case 6: // Boss Kill
                    scoreLines.Add(new ScoreLine("PvP.Score.BossPvPSoloBossKills", new Score(ps.Boss_SoloKillsPoints, ps.Boss_SoloKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.BossPvPGroupBossKills", new Score(ps.Boss_GroupKillsPoints, ps.Boss_GroupKills)));
                    scoreLines.Add(new ScoreLine("PvP.Score.BossPvPBossHits", new Score(ps.Boss_BossHitsPoints, ps.Boss_BossHitsCount)));
                    scoreLines.Add(new ScoreLine("PvP.Score.BossPvPBossKills", new Score(ps.Boss_BossKillsPoints, ps.Boss_BossKillsCount)));
                    break;

                default:
                    break;
            }
            scoreLines.Add(new ScoreLine("PvP.Score.Total", new Score(ps.GetTotalPoints(sessionType), 0, ScoreType.BonusPoints)));
            return new ScoreboardEntry(ps.PlayerName, ps.GetTotalPoints(sessionType), scoreLines);
        }

        #region Stats
        
        [return: NotNull]
        private PlayerScore GetPlayerScore(GamePlayer viewer)
        {
            if (_playerScores.TryGetValue(viewer.InternalID, out var myScore))
            {
                return myScore!;
            }
            else
            {
                return new PlayerScore() { PlayerID = viewer.InternalID, PlayerName = viewer.Name };
            }
        }

        [return: NotNull]
        private IEnumerable<PlayerScore> GetGroupScore(GamePlayer viewer)
        {
            IEnumerable<PlayerScore> list = Enumerable.Empty<PlayerScore>();
            if (_activeSession != null && viewer.Guild != null)
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
                if (_groupScores.TryGetValue(viewer.Guild, out GroupScore groupScore))
                {
                    list = new [] { groupScore.Totals };
                }
                else
                {
                    list = new[]
                    {
                        new PlayerScore
                        {
                            PlayerID = viewer.Guild.GuildID,
                            PlayerName = viewer.Guild.Name
                        }
                    };
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
                switch (CurrentSession.SessionType)
                {
                    case 1:
                        sessionTypeString = "PvP Combats";
                        break;
                    case 2:
                        sessionTypeString = "Flag Capture";
                        break;
                    case 3:
                        sessionTypeString = "Treasure Hunt";
                        break;
                    case 4:
                        sessionTypeString = "Bring Friends";
                        break;
                    case 5:
                        sessionTypeString = "Capture Territories";
                        break;
                    case 6:
                        sessionTypeString = "Boss Kill Cooperation";
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
            if (!IsOpen || (all && _playerScores.Count == 0 && _groupScores.Count == 0))
            {
                lines.Add("No scoreboard data yet!");
            }
            else
            {
                IEnumerable<PlayerScore> scores = Enumerable.Empty<PlayerScore>();
                PlayerScore groupTotal = null;
                // We want to sort players by total points descending
                var sessionType = CurrentSession!.SessionType;
                List<ScoreboardEntry> scoreLines;
                var language = viewer.Client.Account.Language;
                bool shortStats = all;
                if (all)
                {
                    if (CurrentSession.GroupCompoOption == 1 || CurrentSession.GroupCompoOption == 3)
                    {
                        // TODO: Don't take solo players if they are part of a group?
                        scores = _playerScores.Values;
                    }
                    if (CurrentSession.GroupCompoOption == 2 || CurrentSession.GroupCompoOption == 3)
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
                    var myScores = GetPlayerScore(viewer);
                    var ourScores = Enumerable.Empty<PlayerScore>();
                    if (CurrentSession.GroupCompoOption == 2 || CurrentSession.GroupCompoOption == 3)
                    {
                        ourScores = GetGroupScore(viewer);
                    }

                    if (ourScores.Any())
                    {
                        lines.Add($"Current Scoreboard for {viewer.Guild?.Name ?? viewer.Name}:");
                        foreach (var ps in ourScores.Select(MakeScoreboardEntry))
                        {
                            AddLines(lines, ps, language, shortStats);
                        }
                    }

                    lines.Add("");
                    lines.Add(viewer.Name + ':');
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