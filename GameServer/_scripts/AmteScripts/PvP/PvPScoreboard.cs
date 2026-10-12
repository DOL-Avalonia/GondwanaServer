using DOL.Database;
using DOL.GS;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static AmteScripts.Managers.PvpManager;

namespace AmteScripts.PvP
{
    /// All score state. Module-thread only; PvpManager wraps off-thread callers with a blocking hop.
    public sealed class PvPScoreboard
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public sealed record HighScore(string OwnerId, bool IsGroup, int TotalPoints, PvPScore Score) { public List<HighScore> Children { get; } = new(); }
        public sealed class Snapshot { public List<(string GuildName, PvPGroupScore Score)> Groups = new(); public List<PvPScore> Solo = new(); public List<PvPScore> Total = new(); }

        private enum ScoreType { Bonus, Malus, BonusPoints, MalusPoints }
        private sealed record Score(int Points, int Count, ScoreType Type = ScoreType.Bonus);
        private sealed record ScoreLine(string Label, Score Points)
        {
            public string ToString(string lang, bool shortDescription)
            {
                if (shortDescription)
                {
                    string t = LanguageMgr.GetTranslation(lang, Label + ".Short");
                    return Points.Type switch
                    {
                        ScoreType.Bonus => $"{t}={Points.Count}({LanguageMgr.GetTranslation(lang, "PvPManager.Score.Pts", Points.Points)})",
                        ScoreType.Malus => $"{t}={Points.Count}(-{LanguageMgr.GetTranslation(lang, "PvPManager.Score.Pts", Points.Points)})",
                        ScoreType.BonusPoints => $"{t}={LanguageMgr.GetTranslation(lang, "PvPManager.Score.Pts", Points.Points)}",
                        ScoreType.MalusPoints => $"{t}=-{LanguageMgr.GetTranslation(lang, "PvPManager.Score.Pts", Points.Points)}",
                        _ => string.Empty
                    };
                }
                string tl = LanguageMgr.GetTranslation(lang, Label);
                return Points.Type switch
                {
                    ScoreType.Bonus => $"  {tl}: {Points.Count} - {LanguageMgr.GetTranslation(lang, "PvPManager.Score.Points", Points.Points)}",
                    ScoreType.Malus => $"  {tl}: {Points.Count} - -{LanguageMgr.GetTranslation(lang, "PvPManager.Score.Points", Points.Points)}",
                    ScoreType.BonusPoints => $"  {tl}: {LanguageMgr.GetTranslation(lang, "PvPManager.Score.Points", Points.Points)}",
                    ScoreType.MalusPoints => $"  {tl}: -{LanguageMgr.GetTranslation(lang, "PvPManager.Score.Points", Points.Points)}",
                    _ => string.Empty
                };
            }
        }
        private sealed record ScoreboardEntry(string Player, int Total, List<ScoreLine> Lines);

        private readonly PvpService _svc;
        private readonly Dictionary<string, PvPScore> _total = new(), _solo = new();
        private readonly Dictionary<Guild, PvPGroupScore> _group = new();
        public volatile bool Dirty;

        public PvPScoreboard(PvpService svc) { _svc = svc; }

        private eSessionTypes SessionType => _svc.CurrentSessionType;
        public int SoloCount => _solo.Count;
        public int GroupCount => _group.Count;
        public IEnumerable<KeyValuePair<Guild, PvPGroupScore>> GroupScores => _group;

        public void Reset() { _total.Clear(); _solo.Clear(); _group.Clear(); }
        public bool TryGetSolo(string playerId, out PvPScore score) => _solo.TryGetValue(playerId, out score);
        public bool TryGetGroup(Guild guild, out PvPGroupScore score) => _group.TryGetValue(guild, out score);
        public void SetSolo(string playerId, PvPScore score) { _solo[playerId] = score; Dirty = true; }
        public PvPGroupScore SetGroupIfMissing(Guild guild, Func<PvPGroupScore> create)
        {
            if (!_group.TryGetValue(guild, out PvPGroupScore s)) { s = create(); _group[guild] = s; Dirty = true; }
            return s;
        }

        #region ensure / award

        public PvPScore EnsureTotal(GamePlayer p)
        {
            if (p == null) return null;
            if (!_total.TryGetValue(p.InternalID, out PvPScore s)) { s = new PvPScore(p, false); _total[p.InternalID] = s; }
            return s;
        }

        public PvPScore EnsureSolo(GamePlayer p)
        {
            if (p == null) return null;
            if (!_solo.TryGetValue(p.InternalID, out PvPScore s)) { s = new PvPScore(p, true); _solo[p.InternalID] = s; }
            return s;
        }

        public PvPGroupScore EnsureGroup(Guild g)
        {
            if (g == null) return null;
            if (!_group.TryGetValue(g, out PvPGroupScore s)) { s = new PvPGroupScore(g); _group[g] = s; }
            return s;
        }

        public (PvPGroupScore score, PvPScore entry) EnsureGroupEntry(GamePlayer p)
        {
            if (p?.Guild == null) return (null, null);
            PvPScore entry;
            if (!_group.TryGetValue(p.Guild, out PvPGroupScore score))
            {
                entry = new PvPScore(p, false);
                score = new PvPGroupScore(p.Guild, new[] { entry });
                _group[p.Guild] = score;
            }
            else if (!score.Scores.TryGetValue(p.InternalID, out entry))
            {
                entry = new PvPScore(p, false);
                score.Scores[p.InternalID] = entry;
            }
            return (score, entry);
        }

        private string GlobalScoreError()
        {
            PvpSession s = _svc.CurrentSession;
            if (s == null) return string.Empty;
            return _svc.Participants.Count < s.MinPlayersForScore ? "PvPManager.NotEnoughPlayers" : null;
        }

        public bool CanScore(GamePlayer p, bool quiet = false)
        {
            string err = GlobalScoreError();
            if (err == null) return true;
            if (!quiet && err.Length > 0) p.SendTranslatedMessage(err);
            return false;
        }

        public bool CanScore(Guild g, bool quiet = false)
        {
            string err = GlobalScoreError();
            if (err == null) return true;
            if (!quiet && err.Length > 0) foreach (GamePlayer p in g.GetListOfOnlineMembers()) if (p.IsInPvP) p.SendTranslatedMessage(err);
            return false;
        }

        public bool Award(GamePlayer p, Action<PvPScore> fun, bool quiet = false, bool forceSolo = false)
        {
            if (p == null || !CanScore(p, quiet)) return false;
            fun(EnsureTotal(p));
            (PvPGroupScore gs, PvPScore entry) = EnsureGroupEntry(p);
            if (gs != null) { fun(gs.Totals); fun(entry); }
            if (gs == null || forceSolo) fun(EnsureSolo(p));
            Dirty = true;
            return true;
        }

        public bool Award(Guild g, Action<PvPScore> fun, bool quiet = false)
        {
            if (g == null || !CanScore(g, quiet)) return false;
            PvPGroupScore gs = EnsureGroup(g);
            fun(gs.Totals);
            foreach (GamePlayer p in g.GetListOfOnlineMembers()) { fun(gs.GetOrCreateScore(p)); fun(EnsureTotal(p)); }
            Dirty = true;
            return true;
        }

        #endregion

        #region high scores

        public int GetPlayerPoints(GamePlayer player, bool checkPreviousGuild = true)
        {
            int soloPoints = _solo.GetValueOrDefault(player.InternalID)?.GetTotalPoints(SessionType) ?? 0;
            Guild guild = player.Guild ?? (checkPreviousGuild ? _svc.GetLastGuild(player.InternalID) : null);
            if (guild != null) return Math.Max(_group.GetValueOrDefault(guild)?.GetTotalPoints(SessionType) ?? 0, soloPoints);
            return soloPoints;
        }

        private HighScore GetHighScore(PvPScore soloScore, Func<Guild, HighScore> guildScoreSupplier)
        {
            string playerId = soloScore.PlayerID;
            int total = soloScore.GetTotalPoints(SessionType);
            HighScore highScore = new(playerId, false, total, soloScore);
            Guild guild = _svc.GetLastGuild(playerId);
            if (guild == null) return highScore;
            HighScore guildScore = guildScoreSupplier(guild);
            if (guildScore == null) return highScore;
            if (guildScore.TotalPoints > total) { guildScore.Children.Add(highScore); return guildScore; }
            string currentGuild;
            GameClient client = WorldMgr.GetClientByPlayerID(playerId, true, false);
            if (client?.Player != null) currentGuild = client.Player.GuildID;
            else currentGuild = GameServer.Database.SelectObject<DOLCharacters>(c => c.ObjectId == playerId)?.GuildID;
            if (currentGuild == guild.GuildID) { guildScore.Children.Add(highScore); return guildScore; }
            return highScore;
        }

        public List<IGrouping<int, HighScore>> GetHighScores()
        {
            Dictionary<string, HighScore> scores = new();
            foreach ((Guild guild, PvPGroupScore score) in _group)
                scores[guild.GuildID] = new HighScore(guild.GuildID, true, score.GetTotalPoints(SessionType), score);
            foreach ((string playerId, PvPScore score) in _solo)
            {
                HighScore hs = GetHighScore(score, g => scores.GetValueOrDefault(g.GuildID));
                if (!hs.IsGroup) scores[playerId] = hs;
            }
            return scores.Values.GroupBy(s => s.TotalPoints).OrderByDescending(g => g.Key).ToList();
        }

        #endregion

        #region statistics (ex GetStatistics / MakeScoreboardEntry)

        private ScoreboardEntry MakeScoreboardEntry(PvPScore ps)
        {
            eSessionTypes t = SessionType;
            List<ScoreLine> l = new();
            void Kills(string solo, string grp) { l.Add(new(solo, new(ps.PvP_SoloKillsPoints, ps.PvP_SoloKills))); l.Add(new(grp, new(ps.PvP_GroupKillsPoints, ps.PvP_GroupKills))); }
            switch (t)
            {
                case eSessionTypes.Deathmatch: Kills("PvP.Score.PvPSoloKills", "PvP.Score.PvPGrpKills"); break;
                case eSessionTypes.CaptureTheFlag:
                    Kills("PvP.Score.FlagPvPSoloKills", "PvP.Score.FlagPvPGrpKills");
                    l.Add(new("PvP.Score.FlagPvPFlagCarrierKillBonus", new(ps.Flag_KillFlagCarrierPoints, ps.Flag_KillFlagCarrierCount)));
                    l.Add(new("PvP.Score.FlagPvPFlagsCaptured", new(ps.Flag_FlagReturnsPoints, ps.Flag_FlagReturnsCount)));
                    l.Add(new("PvP.Score.FlagPvPOwnership", new(ps.Flag_OwnershipPoints, 0, ScoreType.BonusPoints))); break;
                case eSessionTypes.TreasureHunt:
                    Kills("PvP.Score.TreasurePvPSoloKills", "PvP.Score.TreasurePvPGrpKills");
                    l.Add(new("PvP.Score.TreasurePvPTreasurePoints", new(ps.Treasure_BroughtTreasuresPoints, 0, ScoreType.BonusPoints)));
                    l.Add(new("PvP.Score.TreasurePvPStolenItemPenalty", new(ps.Treasure_StolenTreasuresPoints, 0, ScoreType.MalusPoints))); break;
                case eSessionTypes.BringAFriend:
                    Kills("PvP.Score.FriendsPvPSoloKills", "PvP.Score.FriendsPvPGrpKills");
                    l.Add(new("PvP.Score.FriendsPvPBroughtFriends", new(ps.Friends_BroughtFriendsPoints, 0, ScoreType.BonusPoints)));
                    l.Add(new("PvP.Score.FriendsPvPFamilyBonus", new(ps.Friends_BroughtFamilyBonus, 0, ScoreType.BonusPoints)));
                    l.Add(new("PvP.Score.FriendsPvPLostFriends", new(ps.Friends_FriendKilledPoints, ps.Friends_FriendKilledCount)));
                    l.Add(new("PvP.Score.FriendsPvPKilledOthersFriends", new(ps.Friends_KillEnemyFriendPoints, ps.Friends_KillEnemyFriendCount))); break;
                case eSessionTypes.TerritoryCapture:
                    Kills("PvP.Score.CTTPvPSoloKills", "PvP.Score.CTTPvPGrpKills");
                    l.Add(new("PvP.Score.CTTPvPTerritoryCaptures", new(ps.Terr_TerritoriesCapturedPoints, ps.Terr_TerritoriesCapturedCount)));
                    l.Add(new("PvP.Score.CTTPvPOwnership", new(ps.Terr_TerritoriesOwnershipPoints, 0, ScoreType.BonusPoints))); break;
                case eSessionTypes.BossHunt:
                    Kills("PvP.Score.PvPSoloKills", "PvP.Score.PvPGrpKills");
                    l.Add(new("PvP.Score.BossPvPBossHits", new(ps.Boss_BossHitsPoints, ps.Boss_BossHitsCount)));
                    l.Add(new("PvP.Score.BossPvPBossKills", new(ps.Boss_BossKillsPoints, ps.Boss_BossKillsCount))); break;
                case eSessionTypes.KingOfTheHill:
                    Kills("PvP.Score.PvPSoloKills", "PvP.Score.PvPGrpKills");
                    l.Add(new("PvP.Score.KotHTimeHeld", new(ps.KotH_Points, ps.KotH_Ticks, ScoreType.BonusPoints)));
                    l.Add(new("PvP.Score.KotHPressure", new(ps.KotH_PressurePoints, 0, ScoreType.BonusPoints)));
                    l.Add(new("PvP.Score.KotHCaptures", new(ps.KotH_CapturePoints, ps.KotH_Captures))); break;
                case eSessionTypes.CoreRun:
                    Kills("PvP.Score.PvPSoloKills", "PvP.Score.PvPGrpKills");
                    l.Add(new("PvP.Score.CoreRunCores", new(ps.CoreRun_CoresDelivered, ps.CoreRun_CoresDelivered)));
                    l.Add(new("PvP.Score.CoreRunCenter", new(ps.CoreRun_CenterReached * 12, ps.CoreRun_CenterReached)));
                    l.Add(new("PvP.Score.CoreRunEliminations", new(ps.CoreRun_Eliminations * 2, ps.CoreRun_Eliminations, ScoreType.Malus))); break;
                case eSessionTypes.Biohazard:
                    Kills("PvP.Score.PvPSoloKills", "PvP.Score.PvPGrpKills");
                    l.Add(new("PvP.Score.BioSamples", new(ps.Biohazard_SamplesReturned, 0, ScoreType.BonusPoints)));
                    l.Add(new("PvP.Score.BioInfections", new(ps.Biohazard_Infections * 2, ps.Biohazard_Infections)));
                    l.Add(new("PvP.Score.BioZombieKills", new(ps.Biohazard_ZombieKill * 3, ps.Biohazard_ZombieKill)));
                    l.Add(new("PvP.Score.BioZombieDeaths", new(ps.Biohazard_ZombieDeath * 3, ps.Biohazard_ZombieDeath, ScoreType.Malus))); break;
            }
            l.Add(new("PvP.Score.Total", new(ps.GetTotalPoints(t), 0, ScoreType.BonusPoints)));
            string pl = ps.PlayerName + (ps.IsSoloScore ? " (solo)" : string.Empty);
            return new ScoreboardEntry(pl, ps.GetTotalPoints(t), l);
        }

        private static void AddLines(List<string> lines, ScoreboardEntry e, string lang, bool shortStats)
        {
            if (shortStats) lines.Add($"  {e.Player}: " + string.Join(", ", e.Lines.Select(x => x.ToString(lang, true))));
            else lines.AddRange(e.Lines.Select(x => x.ToString(lang, false)));
        }

        public static int GetPvPTaskNextLevelThreshold(string taskName, int level) => taskName switch
        {
            "KillEnemyPlayersAlone" => level switch { 0 => 10, 1 => 50, _ => 100 },
            "KillEnemyPlayersGroup" => level switch { 0 => 15, 1 => 50, _ => 100 },
            _ => int.MaxValue
        };

        public static void GetPvPTaskStatus(string taskName, int kills, out int level, out int progress)
        {
            int a = taskName == "KillEnemyPlayersAlone" ? 10 : taskName == "KillEnemyPlayersGroup" ? 15 : -1;
            if (a < 0) { level = 0; progress = 0; return; }
            if (kills < a) { level = 0; progress = kills; }
            else if (kills < a + 50) { level = 1; progress = kills - a; }
            else if (kills < a + 150) { level = 2; progress = kills - a - 50; }
            else { level = 3; progress = 100; }
        }

        private static string SessionTypeName(eSessionTypes t) => t switch
        {
            eSessionTypes.Deathmatch => "PvP Combats",
            eSessionTypes.CaptureTheFlag => "Flag Capture",
            eSessionTypes.TreasureHunt => "Treasure Hunt",
            eSessionTypes.BringAFriend => "Bring Friends",
            eSessionTypes.TerritoryCapture => "Capture Territories",
            eSessionTypes.BossHunt => "Boss Kill Cooperation",
            eSessionTypes.KingOfTheHill => "King of the Hill",
            eSessionTypes.CoreRun => "Run to the Core",
            eSessionTypes.Biohazard => "Biohazard",
            _ => "Unknown"
        };

        public IList<string> GetStatistics(GamePlayer viewer, bool all)
        {
            List<string> lines = new();
            PvpSession session = _svc.CurrentSession;
            bool open = _svc.IsOpen;
            string typeName = session != null ? SessionTypeName(SessionType) : "Unknown";
            string lang = viewer.Client.Account.Language;
            bool gm = viewer.Client.Account.PrivLevel > 1;

            if (!open) { if (!gm) lines.Add("PvP is CLOSED."); }
            else foreach (Zone z in _svc.CurrentZones) lines.Add(typeName + " in " + (string.IsNullOrEmpty(z.Description) ? $"Zone#{z.ID}" : z.Description));
            lines.Add("");
            if (gm)
            {
                lines.Add(""); lines.Add("---------------------------------------------------------");
                lines.Add("PvP is " + (open ? "OPEN" : "CLOSED") + "."); lines.Add("Session ID: " + (session?.SessionID ?? "(none)"));
                lines.Add("Forced Open: " + _svc.IsForcedOpen); lines.Add("Session Type: " + typeName); lines.Add("");
                if (open)
                {
                    if (_svc.CurrentZones.Count > 0) { lines.Add("Zones in this PvP session:"); foreach (Zone z in _svc.CurrentZones) lines.Add("  > " + (string.IsNullOrEmpty(z.Description) ? $"Zone#{z.ID}" : z.Description)); }
                    else lines.Add("No zones currently configured in the session.");
                }
                lines.Add("---------------------------------------------------------"); lines.Add("");
            }

            if (!open || (all && _solo.Count == 0 && _group.Count == 0)) lines.Add("No scoreboard data yet!");
            else
            {
                if (all)
                {
                    IEnumerable<PvPScore> scores = Enumerable.Empty<PvPScore>();
                    if (session!.GroupCompoOption is 1 or 3) scores = _solo.Values;
                    if (session.GroupCompoOption is 2 or 3) scores = scores.Concat(_group.Values.Select(s => s.Totals));
                    lines.Add("Current Scoreboard:");
                    foreach (ScoreboardEntry e in scores.Select(MakeScoreboardEntry).OrderByDescending(e => e.Total)) AddLines(lines, e, lang, true);
                    lines.Add(""); lines.Add("");
                }
                else if (viewer.IsInPvP)
                {
                    PvPGroupScore ours = session!.GroupCompoOption is 2 or 3 ? GetGroupScore(viewer) : null;
                    if (ours != null) { lines.Add($"Current Scoreboard for {ours.PlayerName}:"); AddLines(lines, MakeScoreboardEntry(ours), lang, false); }
                    PvPScore mine = _solo.GetValueOrDefault(viewer.InternalID) ?? new PvPScore(viewer, true);
                    lines.Add(""); lines.Add(session.AllowGroupDisbandCreate ? viewer.Name + " (solo):" : viewer.Name);
                    AddLines(lines, MakeScoreboardEntry(mine), lang, false);
                    lines.Add(""); lines.Add("");
                }
                if (!Properties.PVP_ALLOW_TASKSPOINTS && !all && viewer.IsInPvP)
                {
                    PvPScore total = _total.GetValueOrDefault(viewer.InternalID) ?? new PvPScore(viewer, false);
                    (PvPGroupScore gs, _) = EnsureGroupEntry(viewer);
                    int groupKills = gs?.Totals.PvP_GroupKills ?? 0, soloKills = total.PvP_SoloKills;
                    GetPvPTaskStatus("KillEnemyPlayersAlone", soloKills, out int sl, out int sp); int sMax = GetPvPTaskNextLevelThreshold("KillEnemyPlayersAlone", sl);
                    GetPvPTaskStatus("KillEnemyPlayersGroup", groupKills, out int gl, out int gp); int gMax = GetPvPTaskNextLevelThreshold("KillEnemyPlayersGroup", gl);
                    lines.Add(LanguageMgr.GetTranslation(lang, "PvPManager.Score.TasksProgression"));
                    lines.Add(sl >= 3 ? $"+ {LanguageMgr.GetTranslation(lang, "Tasks.KillEnemyPlayersAlone")} ({LanguageMgr.GetTranslation(lang, "Tasks.MaxLevelReached")})"
                                      : $"+ {LanguageMgr.GetTranslation(lang, "Tasks.KillEnemyPlayersAlone")} ({LanguageMgr.GetTranslation(lang, "Tasks.Level", sl)}) : {sp}/{sMax}");
                    lines.Add(gl >= 3 ? $"+ {LanguageMgr.GetTranslation(lang, "Tasks.KillEnemyPlayersGroup")} ({LanguageMgr.GetTranslation(lang, "Tasks.MaxLevelReached")})"
                                      : $"+ {LanguageMgr.GetTranslation(lang, "Tasks.KillEnemyPlayersGroup")} ({LanguageMgr.GetTranslation(lang, "Tasks.Level", gl)}) : {gp}/{gMax}");
                    lines.Add("---------------------------------------------------------");
                    lines.Add("");
                    AddPredatorLines(lines, viewer, lang);
                    lines.Add("---------------------------------------------------------");
                }
                else if (!all && viewer.IsInPvP && _svc.IsPredatorEnabled)
                {
                    AddPredatorLines(lines, viewer, lang);
                    lines.Add("---------------------------------------------------------");
                }
            }
            if (open)
            {
                lines.Add(""); lines.Add($"Waiting queue: {_svc.QueueCount} players"); lines.Add($"Session Max Group size: {session!.GroupMaxSize} players"); lines.Add("");
                lines.Add($"Players currently in PvP: {_svc.Participants.Count}");
            }
            return lines;
        }

        private void AddPredatorLines(List<string> lines, GamePlayer viewer, string lang)
        {
            if (!_svc.IsPredatorEnabled)
                return;

            lines.Add(LanguageMgr.GetTranslation(lang, "PvPManager.Score.PredatorMenu"));
            string target = _svc.GetBountyForPredator(viewer)?.Prey?.GetPersonalizedNameFor(viewer);
            if (string.IsNullOrEmpty(target))
                target = LanguageMgr.GetTranslation(lang, _svc.IsPredatorActive ? "PvPManager.Predator.NoTarget" : "PvPManager.Predator.Inactive");
            lines.Add($"+ {LanguageMgr.GetTranslation(lang, "PvPManager.Predator.Target")}: {target}");

            PredatorPair hunted = _svc.GetBountyForPrey(viewer);
            if (hunted != null)
                lines.Add($"+ {LanguageMgr.GetTranslation(lang, "PvPManager.Predator.HuntedBy")}: {hunted.Predator.GetPersonalizedNameFor(viewer)}");
        }

        private PvPGroupScore GetGroupScore(GamePlayer viewer)
        {
            Guild g = viewer.Guild ?? _svc.GetLastGuild(viewer.InternalID);
            if (g == null) return null;
            return _group.TryGetValue(g, out PvPGroupScore s) ? s : new PvPGroupScore(g);
        }

        #endregion

        #region persistence

        public Snapshot CopyForSave()
        {
            Snapshot s = new();
            foreach ((Guild g, PvPGroupScore sc) in _group) s.Groups.Add((g.Name, new PvPGroupScore(sc)));
            foreach (PvPScore sc in _solo.Values) s.Solo.Add(sc.Copy());
            foreach (PvPScore sc in _total.Values) s.Total.Add(sc.Copy());
            return s;
        }

        public static string SerializeSnapshot(string sessionId, bool forced, Snapshot s)
        {
            StringBuilder sb = new(4096);
            sb.Append(sessionId).Append(';').Append(forced).Append('\n').Append('\n');
            foreach ((string name, PvPGroupScore gs) in s.Groups)
            {
                sb.Append("g;").Append(name).Append(';').Append(gs.Serialize()).Append('\n');
                foreach (KeyValuePair<string, PvPScore> e in gs.Scores) sb.Append(e.Value.Serialize()).Append('\n');
                sb.Append('\n');
            }
            foreach (PvPScore sc in s.Solo) sb.Append("s;").Append(sc.Serialize()).Append('\n');
            foreach (PvPScore sc in s.Total) sb.Append("ps;").Append(sc.Serialize()).Append('\n');
            return sb.ToString();
        }

        /// Returns (sessionId, forced) or (empty, false). resolveGuild: name → Guild (PvpService creates/hijacks PvP guilds).
        public (string SessionId, bool Forced) LoadFromFile(string path, Func<string, Guild> resolveGuild)
        {
            if (!File.Exists(path)) return (string.Empty, false);
            try
            {
                using IEnumerator<string> lines = File.ReadLines(path).GetEnumerator();
                if (!lines.MoveNext()) return (string.Empty, false);
                string[] header = lines.Current.Split(';');
                string sessionId = header[0];
                bool.TryParse(header.Length > 1 ? header[1] : "false", out bool forced);
                log.Info($"Restoring PvP session {sessionId}");
                bool finished = false;
                while (!finished)
                {
                    string[] parameters = lines.Current.Split(';');
                    switch (parameters[0])
                    {
                        case "g": ParseGuildEntry(lines, parameters, resolveGuild); break;
                        case "s": { PvPScore s = PvPScore.Parse(parameters.Skip(1), true); if (!string.IsNullOrEmpty(s.PlayerID)) _solo[s.PlayerID] = s; break; }
                        case "ps": { PvPScore s = PvPScore.Parse(parameters.Skip(1), false); if (!string.IsNullOrEmpty(s.PlayerID)) _total[s.PlayerID] = s; break; }
                    }
                    finished = !lines.MoveNext();
                }
                return (sessionId, forced);
            }
            catch (Exception ex)
            {
                log.Error($"[PvP] Could not load {path}, archiving it.", ex);
                try { File.Move(path, Path.Combine(Path.GetDirectoryName(path) ?? ".", $"PvPScore-error-{DateTime.Now:yy-MM-dd.HH-mm-ss}.dat"), true); }
                catch (Exception mv) { log.Warn("[PvP] Could not archive broken score file.", mv); }
                return (string.Empty, false);
            }
        }

        private void ParseGuildEntry(IEnumerator<string> lines, string[] parameters, Func<string, Guild> resolveGuild)
        {
            string guildName = parameters[1];
            Guild guild = string.IsNullOrEmpty(guildName) ? null : resolveGuild(guildName);
            if (guild == null) { log.Warn($"Cannot recover PvP scores for guild \"{guildName}\", guild could not be found or created"); return; }
            PvPGroupScore gs = EnsureGroup(guild);
            gs.Add(PvPScore.Parse(parameters.Skip(2), false));
            while (lines.MoveNext() && !string.IsNullOrEmpty(lines.Current))
            {
                PvPScore entry = PvPScore.Parse(lines.Current.Split(';'), false);
                gs.Scores[entry.PlayerID] = entry;
                gs.Add(entry);
            }
        }

        #endregion
    }
}