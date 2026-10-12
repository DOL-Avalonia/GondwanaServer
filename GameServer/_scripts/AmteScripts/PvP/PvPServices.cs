using AmteScripts.Areas;
using AmteScripts.Managers;
using AmteScripts.PvP.CoreRun;
using AmteScripts.PvP.CTF;
using AmteScripts.PvP.Modes;
using AmteScripts.PvP.Rewards;
using Discord;
using DOL.Database;
using DOL.Events;
using DOL.GameEvents;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static AmteScripts.Managers.PvpManager;

namespace AmteScripts.PvP
{
    /// Owns every piece of mutable PvP state. Runs on the ModuleService context; PvpManager marshals other threads.
    public sealed class PvpService : IGameModule
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        public static PvpService Instance { get; } = new();
        public string Name => "PvP";
        public long NextTick { get; set; }

        private static readonly TimeSpan StartTime = new(14, 0, 0), EndTime = new(22, 0, 0);
        private const int SCHEDULE_INTERVAL = 30_000, SAVE_DELAY = 5_000, LINKDEATH_GRACE = 20 * 60 * 1000;
        private const int PREDATOR_CHECK_INTERVAL = 60_000, PREDATOR_MINIMUM_SESSION_SECONDS_LEFT = 300, DEFAULT_GUILD_RANK = 9;
        private static readonly int[] RandomEmblems = { 5061, 6645, 84471, 6272, 55302, 64792, 111402, 39859, 21509, 123019 };

        public readonly PvPParticipants Participants = new();
        public readonly PvPScoreboard Scoreboard;
        private volatile PvPSessionContext _ctx;
        internal readonly Lock Gate = new();
        private PvPGameModeBase _mode;
        private bool _isForcedOpen, _restorePending;
        private DateTime _startedTime = DateTime.Now;
        private long _nextScheduleAt, _nextSaveAt;
        private int _fileGen;

        private readonly Dictionary<eRealm, List<GameNPC>> _spawnNpcsRealm = new() { { eRealm.Albion, new() }, { eRealm.Midgard, new() }, { eRealm.Hibernia, new() } };
        private readonly Dictionary<string, GameNPC> _spawnNpcsGlobal = new();
        private readonly HashSet<GameNPC> _usedSpawns = new();
        private readonly Dictionary<string, Spawn> _playerSpawns = new();
        private readonly Dictionary<Guild, Spawn> _groupSpawns = new();
        private readonly Dictionary<string, AbstractArea> _soloAreas = new();
        private readonly Dictionary<Guild, AbstractArea> _groupAreas = new();
        private readonly Dictionary<Group, Guild> _groupGuilds = new();
        private readonly Dictionary<Guild, Group> _guildGroups = new();
        private readonly List<Guild> _allGuilds = new();
        private readonly Dictionary<string, Guild> _playerLastGuilds = new();
        private readonly Dictionary<string, PvPEntity> _allParticipants = new();
        private readonly List<GamePlayer> _soloQueue = new();
        private readonly Dictionary<string, (GamePlayer Player, long Until)> _linkdead = new();
        private readonly PvPPredatorManager _predator = new();
        private bool _predatorArmed;
        private long _nextPredatorAt, _predatorTimeLeft;
        internal PredatorPair GetBountyForPredator(GamePlayer p) => _predator.GetBountyForPredator(p);
        internal PredatorPair GetBountyForPrey(GamePlayer p) => _predator.GetBountyForPrey(p);
        internal bool IsPredatorActive => _predator.Active;
        private readonly List<GamePlayer> _scratch = new(64);
        public int PeakPlayerCount { get; private set; }

        private PvpService()
        {
            Scoreboard = new PvPScoreboard(this);
            _predator.OnPreyKilled = OnPreyKilled;
            _predator.OnBountyAssigned = OnBountyAssigned;
        }

        #region state

        public bool IsOpen => _ctx != null;
        public PvPSessionContext Context => _ctx;
        public PvpSession CurrentSession => _ctx?.Session;
        public eSessionTypes CurrentSessionType => _ctx?.Type ?? eSessionTypes.None;
        public PvPGameModeBase Mode => _mode;
        public IReadOnlyList<Zone> CurrentZones => _ctx?.Zones ?? Array.Empty<Zone>();
        public bool AllowsGroups => CurrentSession?.GroupCompoOption is 2 or 3;
        public bool AllowsSolo => CurrentSession?.GroupCompoOption is 1 or 3;
        public int MaxGroupSize => !AllowsGroups ? 1 : CurrentSession?.GroupMaxSize ?? 0;
        public bool IsPredatorEnabled => CurrentSession?.MinTeamsForPredator >= 0;
        internal bool IsForcedOpen => _isForcedOpen;
        internal int QueueCount => _soloQueue.Count;
        internal Guild GetLastGuild(string playerId) => _playerLastGuilds.GetValueOrDefault(playerId);
        private bool CreateAreas => CurrentSession is { CreateCustomArea: true } && IsUniqueSpawns();

        public int GetEmblemForPlayer(GamePlayer p) => p.Guild != null ? p.Guild.Emblem : GetPlayerSoloEmblem(p);

        public int GetPlayerSoloEmblem(GamePlayer p)
        {
            ulong h = 3074457345618258791ul + (ulong)_startedTime.Ticks;
            for (int i = 0; i < p.Name.Length; i++) { h += p.Name[i]; h *= 3074457345618258799ul; }
            return RandomEmblems[(uint)h % RandomEmblems.Length];
        }

        #endregion

        #region module tick

        [ScriptLoadedEvent]
        public static void OnScriptsLoaded(DOLEvent e, object sender, EventArgs args)
        {
            PvpSessionMgr.ReloadSessions();
            Instance._restorePending = File.Exists(ScoreFilePath);      // NOT Open() here: SubZone index is empty until the loop ticks
            GameEventMgr.AddHandler(GamePlayerEvent.GameEntered, Instance.OnPlayerLogin);
            GameEventMgr.AddHandler(GamePlayerEvent.RegionChanged, Instance.OnPlayerRegionChanged);
            ModuleService.Instance.Register(Instance);
        }

        [ScriptUnloadedEvent]
        public static void OnScriptsUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            ModuleService.Instance.Unregister(Instance);
            GameEventMgr.RemoveHandler(GamePlayerEvent.GameEntered, Instance.OnPlayerLogin);
            GameEventMgr.RemoveHandler(GamePlayerEvent.RegionChanged, Instance.OnPlayerRegionChanged);
        }

        public int Tick()
        {
            lock (Gate)
            {
                long now = GameLoop.GameLoopTime;
                if (_restorePending) { _restorePending = false; OpenInternal(string.Empty, false); }
                if (now >= _nextScheduleAt) { CheckSchedule(); _nextScheduleAt = now + SCHEDULE_INTERVAL; }
                int next = SCHEDULE_INTERVAL;
                if (_ctx != null)
                {
                    if (_mode != null) next = Math.Min(next, _mode.Tick(now));
                    next = Math.Min(next, _ctx.MapPins.Tick(now));
                    next = Math.Min(next, TickPredator(now));
                    next = Math.Min(next, TickLinkdeath(now));
                    if (Scoreboard.Dirty)
                    {
                        if (now >= _nextSaveAt) { Scoreboard.Dirty = false; _nextSaveAt = now + SAVE_DELAY; SaveScoresAsync(); }
                        else next = Math.Min(next, (int)(_nextSaveAt - now));
                    }
                }
                return next;
            }
        }

        private void CheckSchedule()
        {
            TimeSpan t = DateTime.Now.TimeOfDay;
            bool inWindow = t >= StartTime && t < EndTime;
            if (_ctx == null) { if (inWindow) OpenInternal(string.Empty, false); return; }
            PeakPlayerCount = Math.Max(PeakPlayerCount, Participants.Count);
            if (!_isForcedOpen && !inWindow) CloseInternal();
        }

        #endregion

        #region open / close

        private void ClearSessionState()
        {
            _spawnNpcsGlobal.Clear(); foreach (List<GameNPC> l in _spawnNpcsRealm.Values) l.Clear();
            _usedSpawns.Clear(); _playerSpawns.Clear(); _groupSpawns.Clear(); _soloAreas.Clear(); _groupAreas.Clear();
            _groupGuilds.Clear(); _guildGroups.Clear(); _allGuilds.Clear(); _playerLastGuilds.Clear(); _allParticipants.Clear();
            foreach (GamePlayer p in _soloQueue) p.TempProperties.removeProperty("PvPQueued");
            _soloQueue.Clear(); _linkdead.Clear();
        }

        private Guild GetOrCreatePvPGuild(string name)
        {
            Guild g = GuildMgr.GetGuildByName(name);
            if (g != null && g.GuildType != Guild.eGuildType.PvPGuild)
            {
                log.Warn($"Score file references guild \"{name}\" which is not a PvP guild, skipping");
                return null;
            }
            g ??= CreateGuild(name);
            if (g != null && !_allGuilds.Contains(g)) _allGuilds.Add(g);
            return g;
        }

        internal bool OpenInternal(string sessionId, bool force)
        {
            if (_ctx != null) { _isForcedOpen = force; return true; }
            PeakPlayerCount = 0;
            try
            {
                foreach (Region region in WorldMgr.GetAllRegions())
                    foreach (RewardChest chest in region.Objects.OfType<RewardChest>().Where(c => c.Tier is eRewardTier.PvPTier1 or eRewardTier.PvPTier2 or eRewardTier.PvPTier3).ToList())
                    { chest.RemoveFromWorld(); chest.Delete(); }
            }
            catch (Exception ex) { log.Error("Error clearing old PvP reward chests: ", ex); }

            Scoreboard.Reset();
            ClearSessionState();
            bool forced = force;
            if (string.IsNullOrEmpty(sessionId))
            {
                (string restored, bool restoredForced) = Scoreboard.LoadFromFile(ScoreFilePath, GetOrCreatePvPGuild);
                if (!string.IsNullOrEmpty(restored)) { sessionId = restored; forced = restoredForced; }
            }
            PvpSession session = string.IsNullOrEmpty(sessionId) ? PvpSessionMgr.PickRandomSession()
                                                                : PvpSessionMgr.GetAllSessions().FirstOrDefault(s => string.Equals(s.SessionID, sessionId));
            if (session == null) { log.Warn($"PvP session '{sessionId}' not found / no sessions in DB, cannot open!"); Scoreboard.Reset(); return false; }

            List<Zone> zones = new();
            ushort regionId = 0;
            foreach (string zStr in session.ZoneList.Split(','))
            {
                if (!ushort.TryParse(zStr.Trim(), out ushort zoneId) || WorldMgr.GetZone(zoneId) is not Zone zone) continue;
                if (regionId != 0 && zone.ZoneRegion.ID != regionId) log.Error($"PvP session {session.SessionID} relies on zones of different regions {regionId} and {zone.ZoneRegion.ID} -- THINGS WILL BREAK");
                regionId = zone.ZoneRegion.ID;
                zones.Add(zone);
            }
            List<GameNPC> spawnNpcs = new();
            foreach (GameNPC npc in WorldMgr.GetNPCsByGuild("PVP", eRealm.None))           // one scan (was one per zone, and only the last zone's spawns were kept)
            {
                if (!zones.Contains(npc.CurrentZone)) continue;
                if (!npc.Name.StartsWith("SPAWN", StringComparison.OrdinalIgnoreCase) || npc.Name.StartsWith("PADSPAWN", StringComparison.OrdinalIgnoreCase)) continue;
                spawnNpcs.Add(npc);
                _spawnNpcsGlobal[npc.InternalID] = npc;
                if (npc.Name.Contains("SPAWN-ALB", StringComparison.OrdinalIgnoreCase)) _spawnNpcsRealm[eRealm.Albion].Add(npc);
                else if (npc.Name.Contains("SPAWN-MID", StringComparison.OrdinalIgnoreCase)) _spawnNpcsRealm[eRealm.Midgard].Add(npc);
                else if (npc.Name.Contains("SPAWN-HIB", StringComparison.OrdinalIgnoreCase)) _spawnNpcsRealm[eRealm.Hibernia].Add(npc);
            }
            if (spawnNpcs.Count == 0) log.Warn("No 'SPAWN' NPCs found in the session's zones. We'll fallback to random coords.");

            _startedTime = DateTime.Now;
            _isForcedOpen = forced;
            PvPSessionContext ctx = new(session, zones, spawnNpcs, Participants, Scoreboard);
            _mode = CreateMode(ctx.Type);
            _ctx = ctx;
            _mode.Open(ctx);
            log.Info($"PvpManager: Opened session [{session.SessionID}] Type={ctx.Type}, SpawnOpt={session.SpawnOption}");
            return true;
        }

        internal bool CloseInternal()
        {
            PvPSessionContext ctx = _ctx;
            if (ctx == null) return false;
            log.Info($"PvpManager: Closing session [{ctx.Session.SessionID}].");
            _predator.Stop(); _predatorArmed = false;
            List<IGrouping<int, PvPScoreboard.HighScore>> scores = Scoreboard.GetHighScores();
            List<GamePlayer> pvpPlayers = new(Participants.Players);
            _mode?.Close();
            ctx.MapPins.Clear();
            _mode = null;
            _ctx = null;
            _isForcedOpen = false;

            foreach (GamePlayer p in pvpPlayers) KickPlayerInternal(p, false);
            if (scores.Count > 0)
            {
                _ = DoAnnouncements(scores, pvpPlayers, ctx.Type);
                SpawnRewardChests(scores, pvpPlayers, PeakPlayerCount, ctx.Session.MinPlayersForScore);
            }

            foreach (GamePlayer p in new List<GamePlayer>(Participants.Players))
                Participants.Remove(p);

            foreach (AbstractArea a in _soloAreas.Values)
                CleanupArea(a);

            foreach (AbstractArea a in _groupAreas.Values)
                CleanupArea(a);

            lock (ScoreFileLock)
            {
                Interlocked.Increment(ref _fileGen);
                try { File.Delete(ScoreFilePath); } catch (Exception ex) { log.Warn($"[PvP] Could not delete {ScoreFilePath}.", ex); }
            }

            foreach (Guild g in _allGuilds)
                GuildMgr.DeleteGuild(g);

            ClearSessionState();
            Scoreboard.Reset();
            Scoreboard.Dirty = false;
            return true;
        }

        private static PvPGameModeBase CreateMode(eSessionTypes type) => type switch
        {
            eSessionTypes.CaptureTheFlag => new CaptureTheFlagMode(),
            eSessionTypes.TreasureHunt => new TreasureHuntMode(),
            eSessionTypes.BringAFriend => new BringAFriendMode(),
            eSessionTypes.TerritoryCapture => new TerritoryCaptureMode(),
            eSessionTypes.BossHunt => new BossHuntMode(),
            eSessionTypes.KingOfTheHill => new KingOfTheHillMode(),
            eSessionTypes.CoreRun => new CoreRunMode(),
            eSessionTypes.Biohazard => new BiohazardMode(),
            _ => new DeathmatchMode(),
        };

        private void SaveScoresAsync()
        {
            PvPSessionContext ctx = _ctx; if (ctx == null) return;
            var snapshot = Scoreboard.CopyForSave();
            string sessionId = ctx.Session.SessionID; bool forced = _isForcedOpen;
            int gen = Volatile.Read(ref _fileGen);
            Task.Run(() =>
            {
                string text = PvPScoreboard.SerializeSnapshot(sessionId, forced, snapshot);
                lock (ScoreFileLock)
                {
                    if (gen != Volatile.Read(ref _fileGen)) return;
                    try { Directory.CreateDirectory(ScoreDirectory); File.WriteAllText(ScoreFilePath + ".tmp", text); File.Move(ScoreFilePath + ".tmp", ScoreFilePath, true); }
                    catch (Exception ex) { log.Error($"[PvP] Failed to save {ScoreFilePath}.", ex); }
                }
            });
        }

        private async Task DoAnnouncements(IList<IGrouping<int, PvPScoreboard.HighScore>> scores, List<GamePlayer> pvpPlayers, eSessionTypes sessionType)
        {
            const int BROADCAST_MAX_RANK = 3;
            var sessionTranslator = new KeyTranslator("PvPManager.Session." + sessionType);
            var toBroadcast = scores.Where(g => g.Key > 0).Select((g, i) => (g, i)).Take(BROADCAST_MAX_RANK).ToList();
            string dateStr = DateTime.Now.Date.ToString("MM/dd/yyyy");
            foreach (GamePlayer p in pvpPlayers)
            {
                // SPECIAL DISCORD Announcements
                /*
                var embed = new DiscordEmbed { Title = $"🏆 {sessionName} – results", Color = DiscordColors.Gold }.WithTimestamp();
                int rank = 1;
                foreach (var grp in toBroadcast) embed.AddField($"#{rank++}", string.Join("\n", grp.g.Select(hs => $"**{hs.Score.PlayerName}** – {grp.g.Key} pts")));
                hook.SendEmbed(embed);*/

                if (p?.Client == null) continue;
                string lang = p.Client.Account.Language;
                string sessionName = LanguageMgr.GetTranslation(lang, "PvPManager.Session." + sessionType);
                string msg = string.Format(LanguageMgr.GetTranslation(lang, "PvPManager.Score.Title"), sessionName, dateStr) + "\n\n";
                if (toBroadcast.Count == 0) msg += LanguageMgr.GetTranslation(lang, "PvPManager.Score.NoWinner") + "\n";
                else foreach ((IGrouping<int, PvPScoreboard.HighScore> grp, int i) in toBroadcast)
                {
                    string rank = LanguageMgr.GetTranslation(lang, i switch { 0 => "PvPManager.Score.Rank.1", 1 => "PvPManager.Score.Rank.2", 2 => "PvPManager.Score.Rank.3", _ => "PvPManager.Score.Rank.N" });
                    if (string.IsNullOrEmpty(rank)) rank = i switch { 0 => "First", 1 => "Second", 2 => "Third", _ => $"{i + 1}th" }; else if (i > 2) rank = string.Format(rank, i + 1);
                    foreach (PvPScoreboard.HighScore hs in grp)
                    {
                        string tpl = LanguageMgr.GetTranslation(lang, (hs.IsGroup ? "PvPManager.Score.Dialog.Guild." : "PvPManager.Score.Dialog.Player.") + (i + 1));
                        if (string.IsNullOrEmpty(tpl)) tpl = LanguageMgr.GetTranslation(lang, "PvPManager.Score.Dialog.Fallback") ?? "- {3} winner {0}: {2} points.";
                        msg += string.Format(tpl, hs.Score.PlayerName, sessionName, grp.Key, rank) + "\n";
                    }
                }
                p.Out.SendMessage(msg, eChatType.CT_Help, eChatLoc.CL_SystemWindow);
            }
            foreach ((IGrouping<int, PvPScoreboard.HighScore> grp, int i) in toBroadcast)
            {
                var playerKey = new KeyTranslator("PvPManager.Score.Announce.Player." + (i + 1));
                var guildKey = new KeyTranslator("PvPManager.Score.Announce.Guild." + (i + 1));
                await Task.WhenAll(grp.Select(hs =>
                {
                    KeyTranslator key = hs.IsGroup ? guildKey : playerKey;
                    object[] args = { hs.Score.PlayerName, sessionTranslator, grp.Key };
                    return Task.WhenAll(
                        NewsMgr.CreateNews(key, eRealm.None, eNewsType.RvRGlobal, false, args),
                        Task.Run(async () => { if (Properties.DISCORD_ACTIVE) new DolWebHook(Properties.DISCORD_WEBHOOK_ID).SendMessage(await key.Translate(Properties.SERV_LANGUAGE, Properties.AUTOTRANSLATE_ENABLE, args)); }),
                        Task.WhenAll(pvpPlayers.Select(async p => p.Out.SendMessage(await key.Translate(p, args), eChatType.CT_System, eChatLoc.CL_SystemWindow))));
                }));
            }
        }

        private void SpawnRewardChests(List<IGrouping<int, PvPScoreboard.HighScore>> scores, List<GamePlayer> pvpPlayers, int peak, int neededPlayers)
        {
            int rank = 1;
            foreach (IGrouping<int, PvPScoreboard.HighScore> grp in scores.Where(g => g.Key > 0).Take(3))
            {
                eRewardTier tier = rank == 1 ? eRewardTier.PvPTier1 : rank == 2 ? eRewardTier.PvPTier2 : eRewardTier.PvPTier3;
                foreach (PvPScoreboard.HighScore hs in grp)
                {
                    int kills = hs.Score is PvPGroupScore g ? g.Totals.PvP_GroupKills + g.Totals.PvP_SoloKills : hs.Score.PvP_SoloKills + hs.Score.PvP_GroupKills;
                    IEnumerable<string> ids = hs.Score is PvPGroupScore gs ? gs.Scores.Keys : new[] { hs.OwnerId };
                    foreach (string id in ids)
                    {
                        GamePlayer p = pvpPlayers.FirstOrDefault(x => x.InternalID == id);
                        if (p != null) SpawnChestsForPvPPlayer(p, tier, peak, kills, neededPlayers);
                    }
                }
                rank++;
            }
        }

        private static void SpawnChestsForPvPPlayer(GamePlayer p, eRewardTier tier, int peak, int score, int needed)
        {
            try
            {
                List<GameNPC> tps = WorldMgr.GetNPCsByType(typeof(TeleporterPvP), p.Realm);
                if (tps.Count == 0) tps = WorldMgr.GetNPCsByType(typeof(TeleporterPvP), eRealm.None);
                GameNPC tp = tps.Where(t => t.CurrentRegionID == p.CurrentRegionID).OrderBy(t => t.Coordinate.DistanceTo(p.Coordinate)).FirstOrDefault();
                if (tp == null) { log.Warn($"Could not find TeleporterPvP to spawn reward chests for PvP player {p.Name}"); return; }
                if (RewardChestSpawner.SpawnChestsForPlayer(p, tier, tp, peak, score, needed))
                    p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client.Account.Language, "PvPManager.ChestsHaveSpawned"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
            catch (Exception ex) { log.Error("Error spawning PvP reward chests: ", ex); }
        }

        #endregion

        #region participants: join / kick / restore

        private void RegisterParticipant(GamePlayer p)
        {
            if (!Participants.Add(p)) return;
            _mode?.OnPlayerJoined(p);
            _ctx?.MapPins.SendAll(p);
            PeakPlayerCount = Math.Max(PeakPlayerCount, Participants.Count);
            log.Info("Players in PvP: " + Participants.Count);
        }

        private void UnregisterParticipant(GamePlayer p)
        {
            if (!Participants.Remove(p)) return;
            _ctx?.MapPins.RemoveAll(p);
            _mode?.OnPlayerLeft(p);
            log.Info("Players in PvP: " + Participants.Count);
        }

        internal bool AddPlayerInternal(GamePlayer player)
        {
            PvpSession s = CurrentSession;
            string lang = player.Client.Account.Language;
            if (s == null) { player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "PvPManager.PvPNotOpen"), eChatType.CT_System, eChatLoc.CL_SystemWindow); return false; }
            if (player.Level < Properties.PVP_MIN_LEVEL) { player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "PvPManager.LevelTooLow", Properties.PVP_MIN_LEVEL), eChatType.CT_System, eChatLoc.CL_SystemWindow); return false; }
            if (s.GroupCompoOption == 2) { player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "PvPManager.GroupSessionRequired"), eChatType.CT_System, eChatLoc.CL_SystemWindow); return false; }
            if (!TeleportSoloPlayer(player)) { CleanupPlayerInternal(player, true); return false; }
            player.Bind(true);
            player.SaveIntoDatabase();
            Scoreboard.Dirty = true;
            return true;
        }

        internal bool AddGroupInternal(GamePlayer leader)
        {
            PvpSession s = CurrentSession;
            if (s == null) return false;
            Group group = leader.Group;
            string lang = leader.Client.Account.Language;
            if (group == null) { leader.Out.SendMessage(LanguageMgr.GetTranslation(lang, "PvPManager.NoGroup"), eChatType.CT_System, eChatLoc.CL_SystemWindow); return false; }
            foreach (GamePlayer m in group.GetPlayersInTheGroup())
                if (m.Level < Properties.PVP_MIN_LEVEL) { leader.Out.SendMessage(LanguageMgr.GetTranslation(lang, "PvPManager.GroupMemberLevelTooLow", m.Name, Properties.PVP_MIN_LEVEL), eChatType.CT_System, eChatLoc.CL_SystemWindow); return false; }
            if (group.MemberCount > s.GroupMaxSize) { leader.Out.SendMessage(LanguageMgr.GetTranslation(lang, "PvPManager.GroupTooLarge", s.GroupMaxSize), eChatType.CT_System, eChatLoc.CL_SystemWindow); return false; }
            if (!TeleportEntireGroup(leader)) { foreach (GamePlayer m in group.GetPlayersInTheGroup()) CleanupPlayerInternal(m, false); return false; }
            Scoreboard.Dirty = true;
            return true;
        }

        private bool TeleportSoloPlayer(GamePlayer player)
        {
            Spawn spawn = FindSpawnPosition(player.Realm);
            if (spawn == null) { player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.CannotFindSpawn"), eChatType.CT_System, eChatLoc.CL_SystemWindow); return false; }
            bool changingRegion = player.CurrentRegionID != spawn.Position.RegionID;
            _playerSpawns[player.InternalID] = spawn;
            SetPvPState(player, spawn);
            player.MoveTo(spawn.Position);
            if (CreateAreas) CreateSafeAreaForSolo(player, spawn.Position, CurrentSession.TempAreaRadius);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.JoinedPvP", CurrentSession.SessionID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            if (!changingRegion) ScheduleRulesPopup(player);
            _allParticipants[player.InternalID] = new PvPPlayerEntity(player);
            Scoreboard.SetSolo(player.InternalID, new PvPScore(player, true));
            RegisterParticipant(player);
            UpdatePredator();
            return true;
        }

        private bool TeleportEntireGroup(GamePlayer leader)
        {
            Group group = leader.Group;
            if (group == null) return false;
            Spawn spawn = FindSpawnPosition(leader.Realm);
            if (spawn == null) { foreach (GamePlayer p in group.GetPlayersInTheGroup()) p.SendTranslatedMessage("PvPManager.NoSpawn"); return false; }
            foreach (GamePlayer m in group.GetPlayersInTheGroup()) SetPvPState(m, spawn);
            if (!_groupGuilds.TryGetValue(group, out Guild pvpGuild)) pvpGuild = CreateGuildForGroup(group);
            if (pvpGuild == null) { foreach (GamePlayer p in group.GetPlayersInTheGroup()) p.SendTranslatedMessage("PvPManager.CannotCreatePvPGuild"); return false; }
            _groupSpawns[pvpGuild] = spawn;
            float distance = spawn.NPC != null ? 16 : 32, increment = (float)(2 * Math.PI / group.MemberCount), baseAngle = (float)spawn.Position.Orientation.InRadians;
            int i = 0;
            foreach (GamePlayer m in group.GetPlayersInTheGroup())
            {
                Position pos = spawn.Position;
                if (group.MemberCount > 1)
                    pos = pos.With(null, pos.X + (int)Math.Round(Math.Cos(baseAngle + i * increment) * distance), pos.Y + (int)Math.Round(Math.Sin(baseAngle + i * increment) * distance));
                bool changingRegion = m.CurrentRegionID != pos.RegionID;
                m.MoveTo(pos); m.Bind(true); m.SaveIntoDatabase();
                if (!changingRegion) ScheduleRulesPopup(m);
                RegisterParticipant(m);
                ++i;
            }
            if (CreateAreas) CreateSafeAreaForGroup(leader, spawn.Position, CurrentSession.TempAreaRadius);
            leader.Out.SendMessage(LanguageMgr.GetTranslation(leader.Client.Account.Language, "PvPManager.GroupJoinedPvP", CurrentSession.SessionID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            UpdatePredator();
            return true;
        }

        internal void KickPlayerInternal(GamePlayer player, bool disband = true)
        {
            if (!player.IsInPvP && !Participants.Contains(player)) return;
            CleanupPlayerInternal(player, disband);
            RestorePlayerData(player);
        }

        internal void CleanupPlayerInternal(GamePlayer player, bool disband = true)
        {
            _linkdead.Remove(player.InternalID);
            Guild pvpGuild = null;
            if (player.Guild != null)
            {
                if (player.Guild.GuildType != Guild.eGuildType.PvPGuild) log.Warn($"Player {player.Name} being removed from PvP is in non-PvP guild \"{player.Guild.Name}\"");
                else
                {
                    pvpGuild = player.Guild;
                    if (disband && _guildGroups.TryGetValue(pvpGuild, out Group g)) g.RemoveMember(player);
                    pvpGuild.RemovePlayer(string.Empty, player);
                }
            }
            int removed = RemoveItemsFromPlayer(player);
            if (removed > 0 && player.ObjectState == GameObject.eObjectState.Active)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.PvPTreasureRemoved", removed), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            DequeueSoloInternal(player);
            _allParticipants.Remove(player.InternalID);
            CleanupArea(player); FreeSpawn(player);
            UnregisterParticipant(player);
            if (pvpGuild != null) CleanupGuild(pvpGuild);
            Scoreboard.Dirty = true;
            if (player.ObjectState == GameObject.eObjectState.Active)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.LeftPvP"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        private int RemoveItemsFromPlayer(GamePlayer player)
        {
            int total = 0;
            bool dropFlags = IsOpen && player.IsInPvP && CurrentSessionType is eSessionTypes.CaptureTheFlag;
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                InventoryItem item = player.Inventory.GetItem(slot);
                if (item is FlagInventoryItem flag) { int c = item.Count; if (dropFlags ? flag.DropFlagOnGround(player, null) : player.Inventory.RemoveItem(flag)) total += c; }
                else if (item is PvPTreasure) { int c = item.Count; if (player.Inventory.RemoveItem(item)) total += c; }
            }
            return total;
        }

        internal void RestorePlayerData(GamePlayer player, RvrPlayer record = null)
        {
            record ??= PvPRecordStore.Get(player.InternalID);
            if (!string.IsNullOrEmpty(record?.PvPSession))
            {
                record.ResetCharacter(player);
                if (!string.IsNullOrEmpty(record.GuildID) && GuildMgr.GetGuildByGuildID(record.GuildID) is Guild old) old.AddPlayer(player, old.GetRankByID(record.GuildRank), true);
                PvPRecordStore.Delete(player.InternalID);
            }
            else player.MoveTo(Position.Create(51, 434303, 493165, 3088, 1069));
            player.IsInPvP = false;
        }

        private void SetPvPState(GamePlayer player, Spawn spawn)
        {
            RvrPlayer rec = PvPRecordStore.Get(player.InternalID) ?? new RvrPlayer(player, spawn);
            rec.PlayerID = player.InternalID; rec.GuildID = player.GuildID ?? ""; rec.GuildRank = player.GuildRank?.RankLevel ?? 9;
            rec.OldX = player.Position.X; rec.OldY = player.Position.Y; rec.OldZ = player.Position.Z; rec.OldHeading = player.Heading; rec.OldRegion = player.CurrentRegionID;
            rec.OldBindX = player.BindPosition.X; rec.OldBindY = player.BindPosition.Y; rec.OldBindZ = player.BindPosition.Z; rec.OldBindHeading = player.BindPosition.Orientation.InHeading; rec.OldBindRegion = player.BindPosition.RegionID;
            rec.PvPSpawnNPC = spawn.NPC?.InternalID ?? ""; rec.PvPSpawnX = spawn.Position.X; rec.PvPSpawnY = spawn.Position.Y; rec.PvPSpawnZ = spawn.Position.Z;
            rec.PvPSession = string.IsNullOrEmpty(CurrentSession?.SessionID) ? "PvP" : CurrentSession.SessionID;
            PvPRecordStore.Save(rec);
            player.Guild?.RemovePlayer("PVP", player);
            player.IsInPvP = true;
        }

        private void UpdatePvPState(GamePlayer player, Spawn spawn)
        {
            RvrPlayer rec = PvPRecordStore.Get(player.InternalID);
            if (!player.IsInPvP || rec == null) { log.Warn($"Cannot update {player}'s PvP DB record"); return; }
            rec.PvPSpawnNPC = spawn.NPC?.InternalID ?? ""; rec.PvPSpawnX = spawn.Position.X; rec.PvPSpawnY = spawn.Position.Y; rec.PvPSpawnZ = spawn.Position.Z;
            PvPRecordStore.Save(rec);
        }

        internal void OnPvPRecordLoaded(GamePlayer player, RvrPlayer rec)   // ex PvpManager.OnPlayerLogin
        {
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
                if (player.Inventory.GetItem(slot) is FlagInventoryItem flag) player.Inventory.RemoveItem(flag);
            if (IsOpen && CurrentSession.SessionID == rec.PvPSession && TryRestorePlayer(player, rec)) return;
            CleanupPlayerInternal(player, true);
            RestorePlayerData(player, rec);
        }

        private bool TryRestorePlayer(GamePlayer player, RvrPlayer rec)
        {
            bool wasLinkDead = _linkdead.Remove(player.InternalID);
            if (!_ctx.IsSessionZone(player.CurrentZone)) return false;
            Spawn spawn;
            if (player.Guild != null)
            {
                if (player.Guild.GuildType == Guild.eGuildType.PvPGuild)
                {
                    if (AllowsGroups) AddToGuildGroup(player.Guild, player);
                    else { player.Guild.RemovePlayer("PVP", player); return false; }
                }
                else
                {
                    log.Error($"Player {player.Name} logged into PvP with non-PvP guild {player.Guild.Name}");
                    if (player.Client.Account.PrivLevel < 2) { player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.KickedFromPvP"), eChatType.CT_System, eChatLoc.CL_SystemWindow); return false; }
                }
                spawn = TryRestoreGroupArea(player, player.Guild, rec);
                if (spawn == null)
                {
                    spawn = FindSpawnPosition(player.Guild.Realm);
                    if (spawn == null) { player.SendTranslatedMessage("PvPManager.NoSpawn"); return false; }
                    _groupSpawns[player.Guild] = spawn;
                    if (CreateAreas) CreateSafeAreaForGroup(player, spawn.Position, CurrentSession.TempAreaRadius);
                }
                Guild g = player.Guild;
                Scoreboard.SetGroupIfMissing(g, () => new PvPGroupScore(g, new[] { player }));
            }
            else
            {
                if (!AllowsSolo) { player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.KickedFromPvP"), eChatType.CT_System, eChatLoc.CL_SystemWindow); return false; }
                spawn = TryRestorePlayerArea(player, rec);
                if (spawn == null)
                {
                    spawn = FindSpawnPosition(player.Realm);
                    if (spawn == null) { player.SendTranslatedMessage("PvPManager.NoSpawn"); return false; }
                    _playerSpawns[player.InternalID] = spawn;
                    if (CreateAreas) CreateSafeAreaForSolo(player, spawn.Position, CurrentSession.TempAreaRadius);
                }
                if (!Scoreboard.TryGetSolo(player.InternalID, out _)) Scoreboard.SetSolo(player.InternalID, new PvPScore(player, true));
                _allParticipants[player.InternalID] = new PvPPlayerEntity(player);
            }
            if (!wasLinkDead)
            {
                RemoveItemsFromPlayer(player);
                bool changingRegion = player.CurrentRegionID != spawn.Position.RegionID;
                player.MoveTo(spawn.Position);
                if (!changingRegion) ScheduleRulesPopup(player);
            }
            else ScheduleRulesPopup(player);
            player.IsInPvP = true;
            RegisterParticipant(player);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.WelcomeBack"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            UpdatePredator();
            return true;
        }

        private Spawn TryRestoreGroupArea(GamePlayer player, Guild guild, RvrPlayer rec)
        {
            if (_groupSpawns.TryGetValue(guild, out Spawn spawn)) return spawn;
            GameNPC npc = string.IsNullOrEmpty(rec.PvPSpawnNPC) ? null : _spawnNpcsGlobal.GetValueOrDefault(rec.PvPSpawnNPC);
            spawn = new Spawn(npc, Position.Create(_ctx.RegionID, rec.PvPSpawnX, rec.PvPSpawnY, rec.PvPSpawnZ));
            if (IsUniqueSpawns() && (npc == null || !_usedSpawns.Add(npc))) return null;
            _groupSpawns[guild] = spawn;
            if (CreateAreas) CreateSafeAreaForGroup(player, spawn.Position, CurrentSession.TempAreaRadius);
            return spawn;
        }

        private Spawn TryRestorePlayerArea(GamePlayer player, RvrPlayer rec)
        {
            if (_playerSpawns.TryGetValue(player.InternalID, out Spawn spawn)) return spawn;
            GameNPC npc = string.IsNullOrEmpty(rec.PvPSpawnNPC) ? null : _spawnNpcsGlobal.GetValueOrDefault(rec.PvPSpawnNPC);
            spawn = new Spawn(npc, Position.Create(_ctx.RegionID, rec.PvPSpawnX, rec.PvPSpawnY, rec.PvPSpawnZ));
            if (IsUniqueSpawns())
            {
                if (npc == null) return null;
                if (!_usedSpawns.Add(npc)) return spawn;
            }
            _playerSpawns[player.InternalID] = spawn;
            if (CreateAreas) CreateSafeAreaForSolo(player, spawn.Position, CurrentSession.TempAreaRadius);
            return spawn;
        }

        internal void PlayerLinkDeathInternal(GamePlayer player)
        {
            if (_ctx == null || !Participants.Contains(player)) return;
            _linkdead[player.InternalID] = (player, GameLoop.GameLoopTime + LINKDEATH_GRACE);
            _allParticipants.Remove(player.InternalID);
            _mode?.OnPlayerLinkdead(player);
            log.Info($"Linkdead PvP player {player.Name} will be removed in {LINKDEATH_GRACE / 60000} minutes if not reconnected.");
        }

        private int TickLinkdeath(long now)
        {
            if (_linkdead.Count == 0) return int.MaxValue;
            _scratch.Clear();
            foreach (KeyValuePair<string, (GamePlayer Player, long Until)> kv in _linkdead) if (now >= kv.Value.Until) _scratch.Add(kv.Value.Player);
            foreach (GamePlayer p in _scratch)
            {
                log.Info($"PvP grace period expired for linkdead player {p.Name}. Removing from PvP.");
                if (p.ObjectState == GameObject.eObjectState.Active) KickPlayerInternal(p, true); else CleanupPlayerInternal(p, true);
            }
            return SCHEDULE_INTERVAL;
        }

        /// Synchronous (PvpManager hops with Send): must finish before GamePlayer.Quit() saves the character.
        internal void OnPlayerQuitInternal(GamePlayer player)
        {
            if (!player.IsInPvP && !Participants.Contains(player)) return;
            if (IsPredatorEnabled) _predator.Abandon(player, Properties.PVPSESSION_PREDATOR_DESERTER_SECONDS);
            if (_linkdead.TryGetValue(player.InternalID, out (GamePlayer Player, long Until) ld) && ld.Player == player)
            {
                if (Participants.Remove(player)) _mode?.OnPlayerLeft(player);   // old instance leaves the loops, grace + spawn/area stay for the reconnect
                return;
            }
            CleanupPlayerInternal(player, true);
            RvrPlayer rec = PvPRecordStore.Get(player.InternalID);
            if (rec == null) return;
            DOLCharacters c = player.DBCharacter;
            c.Region = rec.OldRegion; c.Xpos = rec.OldX; c.Ypos = rec.OldY; c.Zpos = rec.OldZ;
            c.BindRegion = rec.OldBindRegion; c.BindXpos = rec.OldBindX; c.BindYpos = rec.OldBindY; c.BindZpos = rec.OldBindZ; c.BindHeading = rec.OldBindHeading;
        }

        private void OnPlayerLogin(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GamePlayer p) PvPRecordStore.Load(p, static (pl, rec) => Instance.OnRecordLoaded(pl, rec));
        }

        /// Module thread. One RvrPlayer read per login for PvP + RvR + Arena; each system only sees its own records.
        private void OnRecordLoaded(GamePlayer player, RvrPlayer rec)
        {
            if (player.ObjectState != GameObject.eObjectState.Active) return;
            switch (rec?.PvPSession)
            {
                case null: case "": case "None": RvrManager.Instance.OnPlayerLogIn(player, null); break;
                case "RvR": RvrManager.Instance.OnPlayerLogIn(player, rec); break;
                case "Arena": ArenaManager.Instance.OnPlayerLogin(player, rec); break;
                default: OnPvPRecordLoaded(player, rec); break;
            }
        }

        private void OnPlayerRegionChanged(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GamePlayer { IsInPvP: true } p) return;
            ModuleService.Run(static s => { s.svc._mode?.OnPlayerRegionEntered(s.p); s.svc._ctx?.MapPins.SendAll(s.p); s.svc.ScheduleRulesPopup(s.p); }, (svc: this, p));
        }

        internal void ScheduleRulesPopup(GamePlayer player, int delay = 2000)
        {
            if (player != null) new ECSGameTimer(player, _ => { SendPvPRules(player); return 0; }, delay);
        }

        public void SendPvPRules(GamePlayer player)      // read-only on the immutable context: safe from any thread
        {
            PvpSession s = CurrentSession;
            if (s == null) return;
            string lang = player.Client.Account.Language;
            string ruleKey = CurrentSessionType switch
            {
                eSessionTypes.Deathmatch => "Deathmatch",
                eSessionTypes.CaptureTheFlag => "CaptureTheFlag",
                eSessionTypes.TreasureHunt => "TreasureHunt",
                eSessionTypes.BringAFriend => "BringAFriend",
                eSessionTypes.TerritoryCapture => "TerritoryCapture",
                eSessionTypes.BossHunt => "BossHunt",
                eSessionTypes.KingOfTheHill => "KingOfTheHill",
                eSessionTypes.CoreRun => "CoreRun",
                eSessionTypes.Biohazard => "Biohazard",
                _ => null
            };
            if (ruleKey == null) return;
            string msg = LanguageMgr.GetTranslation(lang, "PvPManager.Rules.Title") + "\n\n" + LanguageMgr.GetTranslation(lang, "PvPManager.Rules." + ruleKey);
            msg += "\n\n" + LanguageMgr.GetTranslation(lang, "PvPManager.Rules.GroupRules") + "\n\n";
            msg += s.GroupCompoOption switch { 1 => LanguageMgr.GetTranslation(lang, "PvPManager.Rules.SoloOnly"), 2 => LanguageMgr.GetTranslation(lang, "PvPManager.Rules.GroupRequired", s.GroupMaxSize), _ => LanguageMgr.GetTranslation(lang, "PvPManager.Rules.SoloOrGroup", s.GroupMaxSize) };
            bool groupAllowed = s.GroupCompoOption != 1 && s.GroupMaxSize > 1;
            if (groupAllowed)
            {
                msg += "\n- " + LanguageMgr.GetTranslation(lang, s.AllowGroupDisbandCreate ? "PvPManager.Rules.GroupAllowed" : "PvPManager.Rules.GroupNotAllowed");
                msg += "\n- " + LanguageMgr.GetTranslation(lang, s.AllowSummonBanner ? "PvPManager.Rules.BannerAllowed" : "PvPManager.Rules.BannerNotAllowed");
            }
            bool terr = CurrentSessionType == eSessionTypes.TerritoryCapture;
            if (groupAllowed || terr)
            {
                msg += "\n\n" + LanguageMgr.GetTranslation(lang, "PvPManager.Rules.ExtraCommands") + "\n\n";
                if (groupAllowed) msg += "- " + LanguageMgr.GetTranslation(lang, "PvPManager.Rules.RenameCommand") + "\n";
                if (terr) msg += "- " + LanguageMgr.GetTranslation(lang, "PvPManager.Rules.TerritoriesCommand") + "\n";
            }
            player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        #endregion

        #region spawns / areas

        private bool IsUniqueSpawns(string opt = null) => string.Equals(opt ?? CurrentSession?.SpawnOption, "randomlock", StringComparison.OrdinalIgnoreCase);

        private Spawn FindSpawnPosition(eRealm realm)
        {
            PvpSession s = CurrentSession;
            if (s == null) return null;
            string opt = s.SpawnOption.ToLowerInvariant();
            if (opt == "realmspawn" && realm != eRealm.None)
            {
                if (_spawnNpcsRealm.TryGetValue(realm, out List<GameNPC> rs) && rs.Count > 0) { GameNPC n = rs[Util.Random(rs.Count - 1)]; return new Spawn(n, n.Position); }
                log.Warn($"RealmSpawn: no spawns for realm {realm}, falling back to random coords.");
            }
            if (IsUniqueSpawns(opt))
            {
                List<GameNPC> available = _spawnNpcsGlobal.Values.Where(n => !_usedSpawns.Contains(n)).ToList();
                if (available.Count > 0) { GameNPC n = available[Util.Random(available.Count - 1)]; _usedSpawns.Add(n); return new Spawn(n, n.Position); }
                log.Warn("RandomLock: all spawns used. Fallback to random coords.");
            }
            if (_spawnNpcsGlobal.Count > 0) { GameNPC n = _spawnNpcsGlobal.Values.ElementAt(Util.Random(_spawnNpcsGlobal.Count - 1)); return new Spawn(n, n.Position); }
            log.Warn("RandomUnlock: no spawn NPC found, fallback random coords.");
            Zone zone = _ctx?.Zones.FirstOrDefault();
            if (zone == null) return null;
            return new Spawn(Position.Create(zone.ZoneRegion.ID, zone.Offset.X + 3000 + Util.Random(1000), zone.Offset.Y + 3000 + Util.Random(1000), 3000, 2048));
        }

        private void FreeSpawn(Spawn spawn) { if (spawn?.NPC != null) _usedSpawns.Remove(spawn.NPC); }
        private void FreeSpawn(GamePlayer p) { if (_playerSpawns.Remove(p.InternalID, out Spawn s)) FreeSpawn(s); }
        private void FreeSpawn(Guild g) { if (_groupSpawns.Remove(g, out Spawn s)) FreeSpawn(s); }

        private void CreateSafeAreaForSolo(GamePlayer player, Position pos, int radius)
        {
            PvpTempArea area = new(player, pos.X, pos.Y, pos.Z, radius, _mode?.OutpostIsSafeArea ?? false);
            player.CurrentRegion.AddArea(area);
            _soloAreas[player.InternalID] = area;
            log.Info($"PvpManager: Created a solo outpost for {player.Name} at {pos}");
            FillOutpostItems(player, pos, area);
        }

        private void CreateSafeAreaForGroup(GamePlayer leader, Position pos, int radius)
        {
            if (leader.Guild == null) return;
            PvpTempArea area = new(leader, pos.X, pos.Y, pos.Z, radius, _mode?.OutpostIsSafeArea ?? false);
            leader.CurrentRegion.AddArea(area);
            _groupAreas[leader.Guild] = area;
            log.Info($"PvpManager: Created a group outpost for {leader.Guild.Name} at {pos}");
            FillOutpostItems(leader, pos, area);
        }

        private void FillOutpostItems(GamePlayer leader, Position pos, PvpTempArea area)
        {
            List<GameStaticItem> items = _mode?.CreateOutpostItems(pos, leader);
            if (items == null) return;
            foreach (GameStaticItem item in items) { area.AddOwnedObject(item); if (item is GamePvPStaticItem pvp) pvp.SetOwnership(leader); }
        }

        private static void CleanupArea(AbstractArea area) { if (area is PvpTempArea t) t.RemoveAllOwnedObjects(); area.Region?.RemoveArea(area); }
        private bool CleanupArea(GamePlayer p) { if (!_soloAreas.Remove(p.InternalID, out AbstractArea a)) return false; CleanupArea(a); return true; }
        private bool CleanupArea(Guild g) { if (!_groupAreas.Remove(g, out AbstractArea a)) return false; CleanupArea(a); return true; }

        public PvpTempArea FindSafeAreaForTarget(GamePlayer p)
        {
            if (p == null) return null;
            return (p.Guild == null ? _soloAreas.GetValueOrDefault(p.InternalID) : _groupAreas.GetValueOrDefault(p.Guild)) as PvpTempArea;
        }

        internal void OnGuildRenameInternal(Guild guild) { if (_groupAreas.TryGetValue(guild, out AbstractArea a) && a is PvpTempArea t) t.OwnerGuild = guild; }

        #endregion

        #region guilds <-> groups

        private Guild CreateGuild(string name, GamePlayer leader = null)
        {
            Guild g = GuildMgr.CreateGuild(eRealm.None, name, leader, true, true);
            if (g == null)
            {
                g = GuildMgr.GetGuildByName(name);
                if (g == null) { log.Error($"Failed to create or find PvP guild \"{name}\""); return null; }
                if (g.GuildType != Guild.eGuildType.PvPGuild) { log.Error($"Guild \"{name}\" exists and is not a PvP guild, aborting"); return null; }
                log.Warn($"PvP guild \"{name}\" already exists, reusing it");
            }
            g.GuildType = Guild.eGuildType.PvPGuild;
            g.SaveIntoDatabase();
            return g;
        }

        private Guild CreateGuild(GamePlayer leader)
        {
            Guild g = CreateGuild("[PVP] " + leader.Name + "'s guild", leader);
            if (g != null) g.Emblem = GetPlayerSoloEmblem(leader);
            return g;
        }

        private Guild CreateGuildForGroup(Group group)
        {
            GamePlayer leader = group.Leader;
            if (leader == null) return null;
            Guild g = CreateGuild(leader);
            if (g == null) { leader.Out.SendMessage(LanguageMgr.GetTranslation(leader.Client.Account.Language, "PvPManager.CannotCreatePvPGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow); return null; }
            _allGuilds.Add(g);
            _allParticipants[g.GuildID] = new PvPGuildGroupEntity(g, group);
            _groupGuilds[group] = g; _guildGroups[g] = group;
            PvPGroupScore gs = Scoreboard.SetGroupIfMissing(g, () => new PvPGroupScore(g));
            if (_soloAreas.Remove(leader.InternalID, out AbstractArea leaderArea)) _groupAreas[g] = leaderArea;
            if (_playerSpawns.Remove(leader.InternalID, out Spawn spawn)) _groupSpawns[g] = spawn;
            foreach (GamePlayer m in group.GetPlayersInTheGroup())
            {
                g.AddPlayer(m, g.GetRankByID(m == leader ? 0 : DEFAULT_GUILD_RANK));
                m.IsInPvP = true; m.SaveIntoDatabase();
                _playerLastGuilds[m.InternalID] = g;
                TransferTreasure(m, gs);
                CleanupArea(m); FreeSpawn(m);
            }
            if (leaderArea is PvpTempArea t) t.SetOwnership(leader);
            return g;
        }

        private void TransferTreasure(GamePlayer m, PvPGroupScore gs)
        {
            if (!Properties.PVPSESSION_TREASURE_TRANSFER_ITEMS || CurrentSessionType is not eSessionTypes.TreasureHunt) return;
            if (Scoreboard.TryGetSolo(m.InternalID, out PvPScore solo)) { gs.GetOrCreateScore(m).TakeItems(solo, true); gs.TakeItems(solo); }
        }

        private void AddToGroupGuild(Group group, GamePlayer player)
        {
            if (!IsOpen) return;
            if (_groupGuilds.TryGetValue(group, out Guild guild))
            {
                if (guild == player.Guild) return;
                guild.AddPlayer(player, guild.GetRankByID(DEFAULT_GUILD_RANK), true);
                _playerLastGuilds[player.InternalID] = guild;
                if (_groupSpawns.TryGetValue(guild, out Spawn spawn)) { FreeSpawn(player); UpdatePvPState(player, spawn); player.Bind(spawn.Position); }
                if (_groupAreas.ContainsKey(guild)) CleanupArea(player);
                _allParticipants.Remove(player.InternalID);
                TransferTreasure(player, Scoreboard.EnsureGroup(guild));
            }
            else
            {
                guild = CreateGuildForGroup(group);
                if (guild == null) return;
                guild.Emblem = GetPlayerSoloEmblem(player);
                if (_groupSpawns.TryGetValue(guild, out Spawn spawn))
                    foreach (GamePlayer m in group.GetPlayersInTheGroup()) { UpdatePvPState(m, spawn); m.Bind(spawn.Position); }
            }
        }

        private void RemoveFromGroupGuild(Group group, GamePlayer player)
        {
            if (!IsOpen || player.Guild == null || !_groupGuilds.TryGetValue(group, out Guild guild) || !guild.RemovePlayer(string.Empty, player)) return;
            CleanupGuild(guild);
            PlayerGroupToSolo(player);
        }

        private void AddToGuildGroup(Guild guild, GamePlayer player)
        {
            if (!IsOpen) return;
            if (player.Guild != guild) { log.Error("AddToGuildGroup called with player.Guild != guild"); return; }
            if (_guildGroups.TryGetValue(guild, out Group group))
            {
                if (group.IsInTheGroup(player)) return;
                group.AddMember(player);
                _allParticipants.Remove(player.InternalID);
                FreeSpawn(player); CleanupArea(player);
                if (_groupSpawns.TryGetValue(guild, out Spawn gs)) { UpdatePvPState(player, gs); player.Bind(gs.Position); }
                if (player.Guild != group.Leader.Guild) log.Warn($"Player {player.Name} added to group led by {group.Leader} but they have different guilds!");
                else if (player.GuildRank.RankLevel < group.Leader.GuildRank.RankLevel) group.MakeLeader(player);
            }
            else
            {
                GamePlayer leader = player;
                if (_soloAreas.Remove(leader.InternalID, out AbstractArea la)) { _groupAreas[guild] = la; if (la is PvpTempArea t) t.SetOwnership(leader); }
                Spawn spawn = null;
                if (_playerSpawns.Remove(leader.InternalID, out spawn)) _groupSpawns.TryAdd(guild, spawn);
                _allParticipants.TryAdd(guild.GuildID, new PvPGuildGroupEntity(guild));
                if (guild.MemberOnlineCount > 1)
                {
                    group = new Group(player);
                    _groupGuilds[group] = guild; _guildGroups[guild] = group;
                    foreach (GamePlayer m in guild.GetListOfOnlineMembers())
                    {
                        group.AddMember(m);
                        if (m.GuildRank.RankLevel < leader.GuildRank.RankLevel) leader = m;
                        CleanupArea(m); FreeSpawn(m);
                        if (spawn != null) { UpdatePvPState(m, spawn); m.Bind(spawn.Position); }
                    }
                    if (leader != player) group.MakeLeader(leader);
                }
            }
            _playerLastGuilds[player.InternalID] = guild;
        }

        private void RemoveFromGuildGroup(Guild guild, GamePlayer player)
        {
            if (!IsOpen || guild == null) return;
            if (player.Guild != null) { log.Error("RemoveFromGuildGroup called with player.Guild != null"); return; }
            if (!_guildGroups.TryGetValue(guild, out Group group) || !group.RemoveMember(player)) return;
            CleanupGuild(guild);
            PlayerGroupToSolo(player);
        }

        private void PlayerGroupToSolo(GamePlayer player)
        {
            if (!AllowsSolo) { KickPlayerInternal(player, true); return; }
            Spawn sp = FindSpawnPosition(player.Realm);
            if (sp == null) { player.SendTranslatedMessage("PvPManager.NoSpawn"); KickPlayerInternal(player, true); return; }
            _playerSpawns[player.InternalID] = sp;
            CreateSafeAreaForSolo(player, sp.Position, CurrentSession.TempAreaRadius);
            UpdatePvPState(player, sp);
            int removed = RemoveItemsFromPlayer(player);
            if (removed > 0) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.PvPTreasureRemoved", removed), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player.MoveTo(sp.Position); player.Bind(true);
            _allParticipants[player.InternalID] = new PvPPlayerEntity(player);
        }

        private void CleanupGuild(Guild guild)
        {
            if (guild.GetListOfOnlineMembers().Any(m => m.IsInPvP)) return;
            CleanupArea(guild); FreeSpawn(guild); _allParticipants.Remove(guild.GuildID);
        }

        internal void OnMemberJoinGuildInternal(Guild guild, GamePlayer player)
        {
            if (IsPredatorEnabled) _predator.Abandon(player, Properties.PVPSESSION_PREDATOR_DESERTER_SECONDS);
            AddToGuildGroup(guild, player); Scoreboard.Dirty = true; UpdatePredator();
        }

        internal void OnMemberLeaveGuildInternal(Guild guild, GamePlayer player)
        {
            if (player.Group == null) return;
            if (IsPredatorEnabled) _predator.Abandon(player, Properties.PVPSESSION_PREDATOR_DESERTER_SECONDS);
            RemoveFromGuildGroup(guild, player); Scoreboard.Dirty = true; UpdatePredator();
        }

        internal void OnMemberJoinGroupInternal(Group group, GamePlayer player)
        {
            if (player.ActiveBanner is PvPFlagBanner { Item: FlagInventoryItem f }) f.OnJoinGroup(player, group);
            AddToGroupGuild(group, player); Scoreboard.Dirty = true; UpdatePredator();
        }

        internal void OnMemberLeaveGroupInternal(Group group, GamePlayer player)
        {
            if (player.ActiveBanner is PvPFlagBanner { Item: FlagInventoryItem f }) f.OnLeaveGroup(player, group);
            if (IsPredatorEnabled) _predator.Abandon(player, Properties.PVPSESSION_PREDATOR_DESERTER_SECONDS);
            RemoveFromGroupGuild(group, player); Scoreboard.Dirty = true; UpdatePredator();
        }

        public bool CanGroup(GamePlayer source, GamePlayer target, bool quiet = false)
        {
            if (!source.IsInPvP && !target.IsInPvP) return true;
            if (CurrentSession?.AllowGroupDisbandCreate != true) { if (!quiet) source.SendTranslatedMessage("Commands.Players.Invite.NotAllowed", eChatType.CT_Important, eChatLoc.CL_SystemWindow); return false; }
            if (!AllowsGroups || (MaxGroupSize > 0 && MaxGroupSize <= (source.Group?.MemberCount ?? 1))) { if (!quiet) source.SendTranslatedMessage("PvPManager.MaxGroup", eChatType.CT_Important, eChatLoc.CL_SystemWindow); return false; }
            return true;
        }

        #endregion

        #region solo queue

        internal void EnqueueSoloInternal(GamePlayer player)
        {
            string lang = player.Client.Account.Language;
            if (player.Level < Properties.PVP_MIN_LEVEL) { player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "PvPManager.LevelTooLow", Properties.PVP_MIN_LEVEL), eChatType.CT_System, eChatLoc.CL_SystemWindow); return; }
            if (_soloQueue.Contains(player)) { player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "PvPManager.AlreadyInSoloQueue"), eChatType.CT_System, eChatLoc.CL_SystemWindow); return; }
            _soloQueue.Add(player);
            player.TempProperties.setProperty("PvPQueued", true);
            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "PvPManager.JoinedSoloQueue"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            TryFormGroupFromSoloQueue();
        }

        internal void DequeueSoloInternal(GamePlayer player)
        {
            player.TempProperties.removeProperty("PvPQueued");
            if (_soloQueue.Remove(player) && player.ObjectState == GameObject.eObjectState.Active)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.LeftGroupQueue"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        private void TryFormGroupFromSoloQueue()
        {
            int needed = Math.Max(2, CurrentSession?.GroupMaxSize ?? 0);
            while (CurrentSession != null && _soloQueue.Count >= needed)
            {
                List<GamePlayer> players = _soloQueue.Take(needed).ToList();
                _soloQueue.RemoveRange(0, needed);
                foreach (GamePlayer p in players) p.TempProperties.removeProperty("PvPQueued");
                GamePlayer leader = players[Util.Random(players.Count - 1)];
                Group group = new(leader);
                GroupMgr.AddGroup(group);
                group.AddMember(leader);
                foreach (GamePlayer p in players) if (p != leader) group.AddMember(p);
                if (!TeleportEntireGroup(leader)) { foreach (GamePlayer p in players) CleanupPlayerInternal(p, false); return; }
                Scoreboard.Dirty = true;
            }
        }

        #endregion

        #region kills

        internal void HandlePlayerKillInternal(GamePlayer killer, GamePlayer victim)
        {
            if (_ctx == null || !killer.IsInPvP || !victim.IsInPvP) return;
            if (!GameLiving.TaskSafeguardCheck(killer, victim, "Standard", false)) return;
            bool rr5 = victim.RealmLevel >= 40 && (_mode?.KillHasRr5Bonus ?? false);
            bool isSolo = killer.Group is not { MemberCount: > 1 };
            int basePoints = _mode?.KillBasePoints ?? 2;
            int points = isSolo ? 2 * basePoints : basePoints;
            if (rr5) points = (int)(points * 1.30);
            int killCount = killer.Client.Account.PrivLevel > 1 ? 10 : 1;
            if (_predator.CompleteBounty(killer, victim) == true)
            {
                double ks = Math.Max(basePoints * 2, Scoreboard.GetPlayerPoints(killer)), vs = Math.Max(basePoints * 4, Scoreboard.GetPlayerPoints(victim));
                points = (int)Math.Round(points * Math.Clamp(vs / ks, 1.5, 4.0));
                _predator.SetPlayerTimeout(killer, Properties.PVPSESSION_PREDATOR_KILL_COOLDOWN_SECONDS);
            }
            points *= killCount;
            if (!isSolo)
            {
                (PvPGroupScore gs, _) = Scoreboard.EnsureGroupEntry(killer);
                int prev = gs?.Totals.PvP_GroupKills ?? 0;
                Scoreboard.Award(killer, s => { s.PvP_GroupKills += killCount; s.PvP_GroupKillsPoints += points; }, false, Properties.PVPSESSION_GROUPKILLS_SOLOSCORES);
                if (!Properties.PVP_ALLOW_TASKSPOINTS)
                {
                    TaskXPlayer td = killer.TaskXPlayer ?? TaskManager.EnsureTaskData(killer);
                    if (td != null) { td.KillEnemyPlayersPvPGroupStats += killCount; PvPRecordStore.Persist(td); }
                    if (gs != null)
                    {
                        int cur = gs.Totals.PvP_GroupKills;
                        PvPScoreboard.GetPvPTaskStatus("KillEnemyPlayersGroup", prev, out int pl, out _);
                        PvPScoreboard.GetPvPTaskStatus("KillEnemyPlayersGroup", cur, out int cl, out int cp);
                        bool up = cl > pl; int dl = up ? cl - 1 : cl; int max = PvPScoreboard.GetPvPTaskNextLevelThreshold("KillEnemyPlayersGroup", dl);
                        if (dl < 3) foreach (GamePlayer m in killer.Group.GetPlayersInTheGroup())
                        {
                            string msg = LanguageMgr.GetTranslation(m.Client.Account.Language, "Tasks.KillEnemyPlayersGroupLog", up ? max : cp, max);
                            if (!string.IsNullOrEmpty(msg)) m.Out.SendMessage(msg, eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
                        }
                        CheckGroupMilestones(killer, prev, cur);
                    }
                }
            }
            else
            {
                PvPScore total = Scoreboard.EnsureTotal(killer);
                int prev = total.PvP_SoloKills;
                Scoreboard.Award(killer, s => { s.PvP_SoloKills += killCount; s.PvP_SoloKillsPoints += points; });
                int cur = total.PvP_SoloKills;
                if (!Properties.PVP_ALLOW_TASKSPOINTS)
                {
                    TaskXPlayer td = killer.TaskXPlayer ?? TaskManager.EnsureTaskData(killer);
                    if (td != null) { td.KillEnemyPlayersPvPAloneStats += killCount; PvPRecordStore.Persist(td); }
                    PvPScoreboard.GetPvPTaskStatus("KillEnemyPlayersAlone", prev, out int pl, out _);
                    PvPScoreboard.GetPvPTaskStatus("KillEnemyPlayersAlone", cur, out int cl, out int cp);
                    bool up = cl > pl; int dl = up ? cl - 1 : cl; int max = PvPScoreboard.GetPvPTaskNextLevelThreshold("KillEnemyPlayersAlone", dl);
                    if (dl < 3)
                    {
                        string msg = LanguageMgr.GetTranslation(killer.Client.Account.Language, "Tasks.KillEnemyPlayersAloneLog", up ? max : cp, max);
                        if (!string.IsNullOrEmpty(msg)) killer.Out.SendMessage(msg, eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
                    }
                    CheckSoloMilestones(killer, prev, cur);
                }
            }
        }

        private void CheckSoloMilestones(GamePlayer p, int prev, int cur)
        {
            if (prev < 10 && cur >= 10) GrantSoloReward(p, 10, 0.04, 80, 8000);
            if (prev < 60 && cur >= 60) GrantSoloReward(p, 50, 0.16, 800, 20000);
            if (prev < 160 && cur >= 160) GrantSoloReward(p, 100, 0.40, 3600, 50000);
        }

        private static void GrantSoloReward(GamePlayer p, int displayKills, double rpRatio, int bp, int baseGold)
        {
            long rp = (long)(p.CalculateRPsToGainRealmRank() * rpRatio);
            long gold = p.Level * (long)baseGold;
            long finalBp = bp + bp * p.GetModified(eProperty.BountyPoints) / 100;
            long finalGold = gold + gold * p.GetModified(eProperty.MythicalCoin) / 100;
            p.GainRealmPoints(rp); p.GainBountyPoints(finalBp); p.AddMoney(DOL.GS.Finance.Currency.Copper.Mint(finalGold));
            p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client.Account.Language, "PvPManager.RewardSolo", displayKills), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
        }

        private void CheckGroupMilestones(GamePlayer k, int prev, int cur)
        {
            if (prev < 15 && cur >= 15) GrantGroupReward(k, 15, 0.04, 80, 8000);
            if (prev < 65 && cur >= 65) GrantGroupReward(k, 50, 0.16, 800, 20000);
            if (prev < 165 && cur >= 165) GrantGroupReward(k, 100, 0.40, 3600, 50000);
        }

        private static void GrantGroupReward(GamePlayer k, int displayKills, double xpRatio, int bp, int baseGold)
        {
            Group g = k.Group;
            if (g == null) return;
            double ratio = 1.0 / g.MemberCount * 2.0;
            foreach (GamePlayer m in g.GetPlayersInTheGroup())
            {
                long xp = (long)((m.ExperienceForNextLevel - m.ExperienceForCurrentLevel) * xpRatio * ratio);
                long finalBp = (long)(bp * ratio); finalBp += finalBp * m.GetModified(eProperty.BountyPoints) / 100;
                long gold = (long)(m.Level * baseGold * ratio); gold += gold * m.GetModified(eProperty.MythicalCoin) / 100;
                m.GainExperience(GameLiving.eXPSource.Other, xp); m.GainBountyPoints(finalBp); m.AddMoney(DOL.GS.Finance.Currency.Copper.Mint(gold));
                m.Out.SendMessage(LanguageMgr.GetTranslation(m.Client.Account.Language, "PvPManager.RewardGroup", displayKills), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
            }
        }

        internal static bool IsSolo(GamePlayer p) => p.Guild == null || p.Group is not { MemberCount: > 1 };

        internal void HandleBossKillInternal(GamePlayer killer, GameNPC boss)
        {
            if (!IsOpen || killer == null || boss == null)
                return;

            bool solo = IsSolo(killer);
            double pts = 40.0 + Math.Max(0, boss.Level - 50);
            if (solo) pts *= 1.3;
            int final = (int)Math.Round(pts);
            foreach (GamePlayer p in Participants.Players)
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Language, "PvPManager.BossHunt.SlewBoss", killer.Name, LanguageMgr.GetTranslation(p.Language, solo ? "PvPManager.Solo" : "PvPManager.Group"), boss.Name, final), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            Scoreboard.Award(killer, s => { s.Boss_BossKillsCount++; s.Boss_BossKillsPoints += final; }, solo);
        }

        internal void HandleCoreDeliveryInternal(GamePlayer player, int points)
        {
            Scoreboard.Award(player, s => s.CoreRun_CoresDelivered += points);
            player.TempProperties.removeProperty("CoreRunState");
            Spawn spawn = player.Guild != null ? _groupSpawns.GetValueOrDefault(player.Guild) : _playerSpawns.GetValueOrDefault(player.InternalID);
            if (spawn != null) player.Bind(spawn.Position);
        }

        #endregion

        #region predator

        internal void UpdatePredator()
        {
            if (!IsPredatorEnabled || _predatorArmed || _predator.Active) return;
            if (Participants.Count < CurrentSession.MinTeamsForPredator) return;
            _predatorArmed = true;
            _nextPredatorAt = GameLoop.GameLoopTime + PREDATOR_CHECK_INTERVAL;
            log.Debug($"[PVP] Predator check armed ({PREDATOR_CHECK_INTERVAL / 1000} s).");
        }

        private int TickPredator(long now)
        {
            if (!_predatorArmed || !IsPredatorEnabled) return int.MaxValue;
            if (now < _nextPredatorAt) return (int)(_nextPredatorAt - now);
            int delay = _predator.Active ? PredatorHeartbeat() : TryStartPredator();
            if (delay <= 0) { _predatorArmed = false; return int.MaxValue; }
            _nextPredatorAt = now + delay;
            return delay;
        }

        private List<PvPEntity> GetPredatorEligiblePlayers(out int numTeams)
        {
            Dictionary<string, GamePlayer> byId = new(Participants.Count);
            foreach (GamePlayer p in Participants.Players) byId[p.InternalID] = p;
            List<PvPScoreboard.HighScore> scores = Scoreboard.GetHighScores().SelectMany(g => g).Reverse().ToList();
            List<PvPEntity> entities = new(scores.Count * 2);
            numTeams = 0;
            foreach (PvPScoreboard.HighScore hs in scores)
            {
                if (hs.IsGroup)
                {
                    int before = entities.Count;
                    foreach (PvPScoreboard.HighScore child in hs.Children)
                        if (byId.TryGetValue(child.OwnerId, out GamePlayer p) && p.IsInPvP && _predator.CanQueue(p, true)) entities.Add(new PvPPlayerEntity(p));
                    if (entities.Count > before) ++numTeams;
                }
                else if (_allParticipants.TryGetValue(hs.OwnerId, out PvPEntity e) && _predator.CanQueue(e, true)) { ++numTeams; entities.Add(e); }
            }
            return entities;
        }

        private int TryStartPredator()
        {
            int roundSeconds = Properties.PVPSESSION_PREDATOR_ROUND_SECONDS;
            if (!IsOpen || !IsPredatorEnabled) return 0;
            TimeSpan left = EndTime - DateTime.Now.TimeOfDay;
            TimeSpan required = TimeSpan.FromSeconds(Math.Min(PREDATOR_MINIMUM_SESSION_SECONDS_LEFT, roundSeconds));
            if (left < required) { log.Debug($"[PVP] Not starting predator: not enough session time left ({left} < {required})"); return 0; }
            List<PvPEntity> entities = GetPredatorEligiblePlayers(out int numTeams);
            if (numTeams < CurrentSession.MinTeamsForPredator) { log.Debug($"[PVP] Not starting predator: {numTeams} < {CurrentSession.MinTeamsForPredator} teams"); return PREDATOR_CHECK_INTERVAL; }
            log.Debug($"[PVP] Starting predator for {roundSeconds} s: {string.Join(", ", entities.Select(e => e.Name))}");
            _predatorTimeLeft = roundSeconds * 1000L;
            _predator.Start(entities);
            return (int)Math.Min(PREDATOR_CHECK_INTERVAL, _predatorTimeLeft);
        }

        private int PredatorHeartbeat()
        {
            if (!_predator.Active) return 0;
            _predatorTimeLeft -= PREDATOR_CHECK_INTERVAL;
            if (_predatorTimeLeft <= 0) return EndPredator();
            _predator.FillInPlayers(GetPredatorEligiblePlayers(out _));
            return (int)Math.Min(PREDATOR_CHECK_INTERVAL, _predatorTimeLeft);
        }

        private int EndPredator()
        {
            log.Debug("[PvP] Stopping predator.");
            foreach (PredatorPair b in _predator.GetBounties()) if (b.Prey != null) b.Predator.SendTranslation("PvP.Predator.Timeout");
            _predator.Stop();
            return Properties.PVPSESSION_PREDATOR_COOLDOWN_SECONDS * 1000;   // TryStartPredator runs again after the cooldown
        }

        private bool OnPreyKilled(GamePlayer prey, PredatorPair bounty, IList<GamePlayer> killers)
        {
            int bp = prey.BountyPointsValue;
            foreach (GamePlayer pr in bounty.Predator.GetPlayers())
            {
                pr.GainBountyPoints(bp, false);
                pr.SendTranslatedMessage("PvP.Predator.KilledPrey", eChatType.CT_PlayerDied, eChatLoc.CL_SystemWindow, bounty.Prey!.GetPersonalizedNameFor(pr));
            }
            foreach (GamePlayer p in prey.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                if (!bounty.IsPredator(p)) p.SendTranslatedMessage("PvP.Predator.KillBroadcast", eChatType.CT_PlayerDied, eChatLoc.CL_SystemWindow, bounty.Predator.GetPersonalizedNameFor(p), bounty.Prey!.GetPersonalizedNameFor(p));
            return true;
        }

        private static bool OnBountyAssigned(GamePlayer predator, PredatorPair bounty)
        {
            string name = bounty.Prey?.GetPersonalizedNameFor(predator);
            GameLoopAsync.Fire(async () =>
            {
                if (name is null)
                {
                    await predator.SendTranslatedMessage("PvP.Predator.PreyRemoved", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow, name);
                    await predator.SendTranslatedMessage("PvP.Predator.LostPrey", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    await predator.SendTranslatedMessage("PvP.Predator.PreyAssigned", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow, name);
                    await predator.SendTranslatedMessage("PvP.Predator.NewPrey", eChatType.CT_System, eChatLoc.CL_SystemWindow, name);
                }
            }, "Predator.Bounty");

            return true;
        }

        #endregion
    }
}
