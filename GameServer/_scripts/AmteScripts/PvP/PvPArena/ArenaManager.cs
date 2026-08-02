using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.GS.Spells;
using AmteScripts.PvP.Rewards;
using DOL.GS.ServerProperties;
using DOL.GameEvents;
using DOL.GS.Finance;
using DOL.GS.Geometry;
using DOL.GS.Scripts;
using System.Collections.Immutable;

namespace AmteScripts.Managers
{
    public class ArenaManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        public static ArenaManager Instance { get; } = new ArenaManager();

        public enum eArenaMode { None, Solo, TwoVsTwo, ThreeVsThree, FourVsFour }
        public enum eArenaState { Idle, Queuing, Running, Cooldown }
        private enum eArchetype { Tank, Healer, Caster, Stealth }

        public const string ARENA_PARTICIPANT_PROP = "ArenaParticipant";
        public const string ARENA_WAITING_PROP = "ArenaWaiting";

        public class ArenaBet
        {
            public string PlayerID;
            public int TeamChoice;
            public long AmountInCopper;
        }

        public class ArenaTeam
        {
            public string TeamName;
            public List<GamePlayer> Members = new List<GamePlayer>();
            public bool IsDisqualified = false;
            public bool SubscribedAsGroup = false;

            public bool IsEliminated(ushort regionId)
            {
                if (IsDisqualified) return true;
                return Members.All(m => m == null || !m.IsAlive || m.ObjectState != GameObject.eObjectState.Active || m.CurrentRegionID != regionId || !m.TempProperties.getProperty(ARENA_PARTICIPANT_PROP, false) || m.TempProperties.getProperty("ArenaMatchDead", false));
            }
        }

        public class ArenaSession
        {
            public ushort RegionID;
            public GameNPC ArenaMaster;
            public eArenaState State = eArenaState.Idle;
            public eArenaMode Mode = eArenaMode.None;
            public int TeamSize => Mode == eArenaMode.Solo ? 1 : Mode == eArenaMode.TwoVsTwo ? 2 : Mode == eArenaMode.ThreeVsThree ? 3 : 4;

            public long NextStateTime;

            public List<GamePlayer> SoloQueue = new List<GamePlayer>(); 
            public List<Group> GroupQueue = new List<Group>(); 

            public List<ArenaTeam> ActiveTeams = new List<ArenaTeam>();
            public List<ArenaTeam> NextRoundTeams = new List<ArenaTeam>();
            public Queue<ArenaTeam> Bracket = new Queue<ArenaTeam>();

            public ArenaTeam CurrentTeamA;
            public ArenaTeam CurrentTeamB;

            public bool IsBettingOpen = false;
            public long BettingEndTime;
            public Dictionary<string, ArenaBet> ActiveBets = new Dictionary<string, ArenaBet>();

            public int RoundNumber = 1;
            public int PeakParticipantsCount = 0;
            public int PeakTeamsCount = 0;

            public List<GameNPC> LeftSpawns = new List<GameNPC>();
            public List<GameNPC> RightSpawns = new List<GameNPC>();
            public Dictionary<string, long> OobWarnings = new Dictionary<string, long>();
            public List<RewardChest> SpawnedChests = new List<RewardChest>();
            public bool IsDebugMode = false;
        }

        private Dictionary<ushort, ArenaSession> _sessions = new Dictionary<ushort, ArenaSession>();
        private object _lock = new object();

        private ArenaManager()
        {
            GameEventMgr.AddHandler(GamePlayerEvent.GameEntered, OnPlayerLogin);
            GameEventMgr.AddHandler(GamePlayerEvent.Quit, OnPlayerQuitOrRegionChange);
            GameEventMgr.AddHandler(GamePlayerEvent.Linkdeath, OnPlayerQuitOrRegionChange);
            GameEventMgr.AddHandler(GamePlayerEvent.RegionChanged, OnPlayerQuitOrRegionChange);
            GameEventMgr.AddHandler(GameLivingEvent.Dying, OnPlayerDying);
        }

        public void Start()
        {
            var region = WorldMgr.GetRegion(1);
            if (region != null)
            {
                var loopTimer = new RegionTimer(region.TimeManager);
                loopTimer.Callback = TickCheck;
                loopTimer.Start(2000);
            }
        }

        public ArenaSession GetOrCreateSession(ushort regionId, GameNPC master = null)
        {
            lock (_lock)
            {
                if (!_sessions.ContainsKey(regionId)) _sessions[regionId] = new ArenaSession { RegionID = regionId, ArenaMaster = master };
                if (master != null) _sessions[regionId].ArenaMaster = master;
                return _sessions[regionId];
            }
        }

        public ArenaSession GetSession(ushort regionId) { lock (_lock) return _sessions.GetValueOrDefault(regionId); }

        public bool HasSpawns(ushort regionId)
        {
            return WorldMgr.GetNPCsFromRegion(regionId).Count(n => n.Name.StartsWith("ARENA_SPAWN_LEFT_")) >= 4 &&
                   WorldMgr.GetNPCsFromRegion(regionId).Count(n => n.Name.StartsWith("ARENA_SPAWN_RIGHT_")) >= 4;
        }

        private void ResolveBets(ArenaSession session, ArenaTeam winner)
        {
            if (session.ActiveBets.Count == 0) return;

            int winningChoice = (winner == session.CurrentTeamA) ? 1 : 2;
            long winningPool = 0;
            long losingPool = 0;

            foreach (var bet in session.ActiveBets.Values)
            {
                if (bet.TeamChoice == winningChoice) winningPool += bet.AmountInCopper;
                else losingPool += bet.AmountInCopper;
            }

            long houseSeed = 100 * 10000L; // 100 gold house seed to guarantee a minimum payout multiplier
            long totalPool = winningPool + losingPool + houseSeed;

            if (winningPool == 0)
            {
                if (losingPool > 0)
                    BroadcastRegion(session.RegionID, $"No one bet on the winning team! The house claims the {DOL.GS.Finance.Money.Mint(losingPool, Currency.Copper).ToText()} pool!", eChatType.CT_Important);
            }
            else
            {
                double houseCut = 0.05; // 5% house edge
                long distributablePool = (long)(totalPool * (1.0 - houseCut));

                foreach (var bet in session.ActiveBets.Values)
                {
                    if (bet.TeamChoice == winningChoice)
                    {
                        double share = (double)bet.AmountInCopper / winningPool;
                        long payout = (long)(distributablePool * share);

                        GiveMoney(bet.PlayerID, payout, $"You WON your bet! Payout: {DOL.GS.Finance.Money.Mint(payout, Currency.Copper).ToText()}");
                    }
                    else
                    {
                        GamePlayer bettor = WorldMgr.GetClientByPlayerID(bet.PlayerID, false, false)?.Player;
                        if (bettor != null)
                            bettor.Out.SendMessage($"You LOST your bet of {DOL.GS.Finance.Money.Mint(bet.AmountInCopper, Currency.Copper).ToText()}.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                }
                BroadcastRegion(session.RegionID, $"The betting pool of {DOL.GS.Finance.Money.Mint(distributablePool, Currency.Copper).ToText()} has been distributed to the winners!", eChatType.CT_System);
            }

            session.ActiveBets.Clear();
        }

        private void GiveMoney(string playerId, long amount, string message)
        {
            DBBanque bank = GameServer.Database.FindObjectByKey<DBBanque>(playerId) ?? new DBBanque(playerId);
            if (bank.PlayerID == null) GameServer.Database.AddObject(bank);

            long remainingToGive = amount;

            // Pay off bank debt automatically first!
            if (bank.Debt > 0)
            {
                long debt = bank.Debt;
                if (remainingToGive >= debt)
                {
                    bank.Debt = 0;
                    remainingToGive -= debt;

                    bank.IsDebtor = false;
                    bank.NegativeMoneySince = DateTime.MinValue;
                }
                else
                {
                    bank.Debt -= remainingToGive;
                    remainingToGive = 0;
                }
                GameServer.Database.SaveObject(bank);
            }

            GamePlayer p = WorldMgr.GetClientByPlayerID(playerId, false, false)?.Player;
            if (p != null) 
            {
                if (remainingToGive > 0)
                    p.AddMoney(Currency.Copper.Mint(remainingToGive));
                
                p.Out.SendMessage(message, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                if (bank.Money >= 0 && amount > remainingToGive)
                    p.Out.SendMessage("Your winnings have automatically cleared your bank debt!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            } 
            else 
            {
                if (remainingToGive > 0)
                {
                    bank.Money += remainingToGive;
                    GameServer.Database.SaveObject(bank);
                }
            }
        }

        private void RefundBets(ArenaSession session)
        {
            foreach (var bet in session.ActiveBets.Values)
            {
                GiveMoney(bet.PlayerID, bet.AmountInCopper, $"The match was cancelled. Your bet of {DOL.GS.Finance.Money.Mint(bet.AmountInCopper, Currency.Copper).ToText()} has been refunded.");
            }
            session.ActiveBets.Clear();
        }

        public void StartQueue(ArenaSession session, eArenaMode mode)
        {
            session.LeftSpawns = WorldMgr.GetNPCsFromRegion(session.RegionID).Where(n => n.Name.StartsWith("ARENA_SPAWN_LEFT_")).OrderBy(n => n.Name).ToList();
            session.RightSpawns = WorldMgr.GetNPCsFromRegion(session.RegionID).Where(n => n.Name.StartsWith("ARENA_SPAWN_RIGHT_")).OrderBy(n => n.Name).ToList();

            session.Mode = mode;
            session.State = eArenaState.Queuing;
            int mins = Properties.ARENA_QUEUE_MINUTES > 0 ? Properties.ARENA_QUEUE_MINUTES : 45;
            session.NextStateTime = WorldMgr.GetRegion(session.RegionID).Time + (mins * 60 * 1000);
            
            string modeStr = mode == eArenaMode.Solo ? "Solo vs Solo" : $"{session.TeamSize} vs {session.TeamSize}";
            BroadcastRegion(session.RegionID, $"A new [{modeStr}] Arena contest is open for subscription! Talk to {session.ArenaMaster.Name} to join. Queue closes in {mins} minutes.", eChatType.CT_Important);
        }

        public void EnqueueSolo(ArenaSession session, GamePlayer player)
        {
            lock (_lock)
            {
                if (!session.SoloQueue.Contains(player))
                {
                    session.SoloQueue.Add(player);
                    player.TempProperties.setProperty("ArenaQueued", true);
                    player.TempProperties.setProperty("ArenaRegion", session.RegionID);
                    player.TempProperties.setProperty("ArenaQueueTime", DateTime.Now.Ticks);
                    player.Out.SendMessage($"You joined the Arena {session.TeamSize}v{session.TeamSize} queue.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
        }

        public void EnqueueGroup(ArenaSession session, Group group)
        {
            lock (_lock)
            {
                if (!session.GroupQueue.Contains(group))
                {
                    session.GroupQueue.Add(group);
                    foreach (var member in group.GetPlayersInTheGroup())
                    {
                        member.TempProperties.setProperty("ArenaQueued", true);
                        member.TempProperties.setProperty("ArenaRegion", session.RegionID);
                    }
                    group.SendPlayerActionTranslationToGroupMembers(group.Leader, "Your group has joined the Arena queue.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
        }

        public void DequeuePlayer(ArenaSession session, GamePlayer player, bool intentional)
        {
            lock (_lock)
            {
                player.TempProperties.removeProperty("ArenaQueued");
                session.SoloQueue.Remove(player);

                var g = session.GroupQueue.FirstOrDefault(gr => gr.GetPlayersInTheGroup().Contains(player));
                if (g != null)
                {
                    session.GroupQueue.Remove(g);
                    foreach (var m in g.GetPlayersInTheGroup())
                    {
                        m.TempProperties.removeProperty("ArenaQueued");
                        if (m != player) m.Out.SendMessage($"Your group was removed from the Arena queue because {(intentional ? "a member left" : "a member disconnected or changed regions")}.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                }
            }
        }

        private eArchetype GetArchetype(GamePlayer p)
        {
            int id = p.CharacterClass.ID;
            int[] stealthers = { (int)eCharacterClass.Infiltrator, (int)eCharacterClass.Scout, (int)eCharacterClass.Minstrel, (int)eCharacterClass.Nightshade, (int)eCharacterClass.Ranger, (int)eCharacterClass.Shadowblade, (int)eCharacterClass.Hunter, (int)eCharacterClass.AlbionRogue, (int)eCharacterClass.MidgardRogue, (int)eCharacterClass.Stalker };
            int[] healers = { (int)eCharacterClass.Cleric, (int)eCharacterClass.Friar, (int)eCharacterClass.Heretic, (int)eCharacterClass.Healer, (int)eCharacterClass.Shaman, (int)eCharacterClass.Druid, (int)eCharacterClass.Warden, (int)eCharacterClass.Bard, (int)eCharacterClass.Acolyte, (int)eCharacterClass.Seer, (int)eCharacterClass.Naturalist };
            
            if (stealthers.Contains(id)) return eArchetype.Stealth;
            if (healers.Contains(id)) return eArchetype.Healer;
            if (p.CharacterClass.ClassType == eClassType.ListCaster) return eArchetype.Caster;
            
            return eArchetype.Tank; // Everything else (PureTank, Hybrids like Paladin/Thane/Champion/Mauler)
        }

        private int TickCheck(RegionTimer timer)
        {
            foreach (var session in _sessions.Values.ToList())
            {
                var region = WorldMgr.GetRegion(session.RegionID);
                if (region == null) continue;
                long now = region.Time;

                if (session.State == eArenaState.Cooldown && now >= session.NextStateTime)
                {
                    session.State = eArenaState.Idle;

                    foreach (var chest in session.SpawnedChests)
                    {
                        chest.RemoveFromWorld();
                        chest.Delete();
                    }
                    session.SpawnedChests.Clear();
                    BroadcastRegion(session.RegionID, "The Arena has been cleaned and is now open for new challenges!", eChatType.CT_System);
                }
                else if (session.State == eArenaState.Queuing && now >= session.NextStateTime)
                {
                    TryStartTournament(session);
                }
                else if (session.State == eArenaState.Running)
                {
                    CheckDistanceViolations(session, now);

                    if (session.CurrentTeamA != null && session.CurrentTeamB != null)
                    {
                        if (session.IsBettingOpen && now >= session.BettingEndTime)
                        {
                            session.IsBettingOpen = false;
                            BroadcastRegion(session.RegionID, "Bets are CLOSED! Let the battle begin!", eChatType.CT_Important);
                            UnlockTeam(session.CurrentTeamA);
                            UnlockTeam(session.CurrentTeamB);
                        }
                        else if (!session.IsBettingOpen)
                        {
                            if (!session.IsDebugMode)
                                CheckCombatState(session, now);
                        }
                    }
                    else if (now >= session.NextStateTime)
                    {
                        PrepareNextRound(session, now);
                    }
                }
            }
            return 2000;
        }

        private void TryStartTournament(ArenaSession session)
        {
            lock (_lock)
            {
                // Kick excess solo players who registered last
                if (session.Mode != eArenaMode.Solo && session.SoloQueue.Count > 0)
                {
                    int remainder = session.SoloQueue.Count % session.TeamSize;
                    if (remainder > 0)
                    {
                        // Order by newest (largest JoinTime) and drop them
                        var droppedPlayers = session.SoloQueue.OrderByDescending(p => p.TempProperties.getProperty<long>("ArenaQueueTime", 0)).Take(remainder).ToList();
                        foreach (var drop in droppedPlayers)
                        {
                            session.SoloQueue.Remove(drop);
                            drop.Out.SendMessage("You were removed from the Arena queue because there were not enough players to form a full team. Better luck next time!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            ClearParticipant(drop, null);
                        }
                    }
                }

                int totalPlayers = session.SoloQueue.Count + session.GroupQueue.Sum(g => g.MemberCount);
                int potentialTeams = session.GroupQueue.Count + (session.SoloQueue.Count / session.TeamSize);
                
                int minSolo = Properties.ARENA_MIN_SOLO > 0 ? Properties.ARENA_MIN_SOLO : 8;
                int minTeams = Properties.ARENA_MIN_TEAMS > 0 ? Properties.ARENA_MIN_TEAMS : 6;

                bool canStart = (session.Mode == eArenaMode.Solo && totalPlayers >= minSolo) || 
                                (session.Mode != eArenaMode.Solo && potentialTeams >= minTeams);

                if (!canStart)
                {
                    CancelSession(session, "Not enough participants to start the Arena. You must re-subscribe.");
                    return;
                }

                session.State = eArenaState.Running;
                session.RoundNumber = 1;
                session.ActiveTeams.Clear();

                if (session.Mode == eArenaMode.Solo)
                {
                    foreach (var p in session.SoloQueue)
                        session.ActiveTeams.Add(new ArenaTeam { TeamName = p.Name, Members = new List<GamePlayer> { p } });
                }
                else
                {
                    // Add Pre-made Groups
                    foreach (var g in session.GroupQueue)
                        session.ActiveTeams.Add(new ArenaTeam { TeamName = g.Leader.Name + "'s Team", Members = g.GetPlayersInTheGroup().ToList(), SubscribedAsGroup = true });

                    // AUTO-BALANCING: Group Solo Players by Archetype
                    var healers = new Queue<GamePlayer>(session.SoloQueue.Where(p => GetArchetype(p) == eArchetype.Healer).OrderBy(x => Guid.NewGuid()));
                    var tanks = new Queue<GamePlayer>(session.SoloQueue.Where(p => GetArchetype(p) == eArchetype.Tank).OrderBy(x => Guid.NewGuid()));
                    var casters = new Queue<GamePlayer>(session.SoloQueue.Where(p => GetArchetype(p) == eArchetype.Caster).OrderBy(x => Guid.NewGuid()));
                    var stealthers = new Queue<GamePlayer>(session.SoloQueue.Where(p => GetArchetype(p) == eArchetype.Stealth).OrderBy(x => Guid.NewGuid()));

                    int teamsToForm = session.SoloQueue.Count / session.TeamSize;
                    var newTeams = new List<ArenaTeam>();
                    
                    for (int i = 0; i < teamsToForm; i++)
                        newTeams.Add(new ArenaTeam { SubscribedAsGroup = false });

                    int currentTeamIdx = 0;
                    var allQueues = new[] { healers, tanks, casters, stealthers };

                    foreach (var q in allQueues)
                    {
                        while (q.Count > 0)
                        {
                            newTeams[currentTeamIdx].Members.Add(q.Dequeue());
                            currentTeamIdx = (currentTeamIdx + 1) % teamsToForm;
                        }
                    }

                    foreach (var team in newTeams)
                    {
                        team.TeamName = team.Members[0].Name + "'s Team";
                        if (team.Members.Count > 1)
                        {
                            Group newGrp = new Group(team.Members[0]);
                            GroupMgr.AddGroup(newGrp);
                            foreach (var member in team.Members)
                                if (member != team.Members[0]) newGrp.AddMember(member);
                        }
                        session.ActiveTeams.Add(team);
                    }
                }

                session.SoloQueue.Clear();
                session.GroupQueue.Clear();

                session.PeakParticipantsCount = session.ActiveTeams.Sum(t => t.Members.Count);
                session.PeakTeamsCount = session.ActiveTeams.Count;

                // Setup Participants Bind, Database Persistence, and Teleport
                foreach (var team in session.ActiveTeams)
                {
                    foreach (var p in team.Members)
                    {
                        p.TempProperties.removeProperty("ArenaQueued");
                        p.TempProperties.setProperty(ARENA_PARTICIPANT_PROP, true);
                        RoleplayReward.ResetRPChain(p);

                        // Database persistence for Crash-Safety Using RvrPlayer Record
                        RvrPlayer dbPlayer = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(p.InternalID));
                        bool isNew = false;
                        if (dbPlayer == null)
                        {
                            dbPlayer = new RvrPlayer();
                            dbPlayer.PlayerID = p.InternalID;
                            isNew = true;
                        }

                        dbPlayer.GuildID = p.GuildID ?? "";
                        dbPlayer.GuildRank = p.GuildRank != null ? p.GuildRank.RankLevel : 9;

                        dbPlayer.OldX = p.Coordinate.X;
                        dbPlayer.OldY = p.Coordinate.Y;
                        dbPlayer.OldZ = p.Coordinate.Z;
                        dbPlayer.OldHeading = p.Heading;
                        dbPlayer.OldRegion = p.CurrentRegionID;

                        dbPlayer.OldBindX = p.BindPosition.Coordinate.X;
                        dbPlayer.OldBindY = p.BindPosition.Coordinate.Y;
                        dbPlayer.OldBindZ = p.BindPosition.Coordinate.Z;
                        dbPlayer.OldBindHeading = (int)p.BindPosition.Orientation.InHeading;
                        dbPlayer.OldBindRegion = p.BindPosition.RegionID;

                        dbPlayer.PvPSession = "Arena";

                        if (isNew) GameServer.Database.AddObject(dbPlayer);
                        else GameServer.Database.SaveObject(dbPlayer);

                        if (p.Guild != null)
                        {
                            p.Guild.RemovePlayer("Arena", p);
                        }

                        p.BindPosition = session.ArenaMaster.Position;
                        p.SaveIntoDatabase();
                        
                        p.MoveTo(session.ArenaMaster.Position);
                        SetWaitState(p, true, false);
                    }
                }

                foreach (var t in session.ActiveTeams.OrderBy(x => Guid.NewGuid())) 
                    session.Bracket.Enqueue(t);
                
                session.NextStateTime = WorldMgr.GetRegion(session.RegionID).Time + 2000;
            }
        }

        private void PrepareNextRound(ArenaSession session, long now)
        {
            if (session.Bracket.Count == 0)
            {
                if (session.NextRoundTeams.Count > 1)
                {
                    foreach (var t in session.NextRoundTeams) session.Bracket.Enqueue(t);
                    session.NextRoundTeams.Clear();
                    session.RoundNumber++;
                }
                else if (session.NextRoundTeams.Count == 1)
                {
                    DeclareFinalWinner(session, session.NextRoundTeams[0]);
                    return;
                }
                else
                {
                    CancelSession(session, "The tournament ended prematurely (No Teams Left).");
                    return;
                }
            }

            if (session.Bracket.Count == 1)
            {
                var byeTeam = session.Bracket.Dequeue();
                session.NextRoundTeams.Add(byeTeam);
                BroadcastToParticipants(session, $"{byeTeam.TeamName} gets a bye this round and advances!", false);
                session.NextStateTime = now + 1000;
                return;
            }

            session.CurrentTeamA = session.Bracket.Dequeue();
            session.CurrentTeamB = session.Bracket.Dequeue();

            // Set up betting phase
            session.IsBettingOpen = true;
            session.BettingEndTime = now + 45000; // 45 seconds for bets
            session.NextStateTime = session.BettingEndTime; // Pause loop
            
            string roundMsg = (session.Bracket.Count == 0 && session.NextRoundTeams.Count == 0) ? "FINAL ROUND" : $"ROUND {session.RoundNumber:D2}";
            BroadcastToParticipants(session, $"{roundMsg} : {session.CurrentTeamA.TeamName} vs {session.CurrentTeamB.TeamName}", true);
            
            BroadcastRegion(session.RegionID, $"=== ARENA MATCH: [1] {session.CurrentTeamA.TeamName} vs [2] {session.CurrentTeamB.TeamName} ===", eChatType.CT_Important);
            BroadcastRegion(session.RegionID, $"Betting is open for 45 seconds! Use /bet <1 or 2> <gold>!", eChatType.CT_System);
            BroadcastSoundToParticipants(session, 9205);

            // Teleport and root (visible)
            TeleportTeamToSpawns(session.CurrentTeamA, session.LeftSpawns, true, true);
            TeleportTeamToSpawns(session.CurrentTeamB, session.RightSpawns, true, true);
        }

        private void CheckCombatState(ArenaSession session, long now)
        {
            bool aElim = session.CurrentTeamA.IsEliminated(session.RegionID);
            bool bElim = session.CurrentTeamB.IsEliminated(session.RegionID);

            if (aElim || bElim)
            {
                ArenaTeam winner = (aElim && bElim) ? (Util.Chance(50) ? session.CurrentTeamA : session.CurrentTeamB) : (bElim ? session.CurrentTeamA : session.CurrentTeamB);
                ArenaTeam loser = winner == session.CurrentTeamA ? session.CurrentTeamB : session.CurrentTeamA;

                string roundName = (session.Bracket.Count == 0 && session.NextRoundTeams.Count == 0) ? "FINAL ROUND" : $"ROUND {session.RoundNumber:D2}";
                
                BroadcastToParticipants(session, $"{roundName} Winner : {winner.TeamName}", true);
                BroadcastRegionLog(session.RegionID, $"{roundName} Winner : {winner.TeamName}");
                ResolveBets(session, winner);

                foreach (var p in loser.Members) ClearParticipant(p, loser);
                foreach (var p in winner.Members)
                {
                    if (p != null)
                    {
                        p.TempProperties.removeProperty("ArenaMatchDead");
                        if (!p.IsAlive) { p.Health = p.MaxHealth; p.Out.SendPlayerRevive(p); p.Out.SendUpdatePoints(); }
                        p.Health = p.MaxHealth; p.Mana = p.MaxMana; p.Endurance = p.MaxEndurance;
                        p.MoveTo(session.ArenaMaster.Position);
                        SetWaitState(p, true, false);
                    }
                }

                session.NextRoundTeams.Add(winner);
                session.CurrentTeamA = null;
                session.CurrentTeamB = null;
                session.NextStateTime = now + 5000;
            }
        }

        private void DeclareFinalWinner(ArenaSession session, ArenaTeam winner)
        {
            BroadcastToParticipants(session, $"FINAL ROUND Winner : {winner.TeamName}!", true);
            BroadcastRegion(session.RegionID, $"FINAL ROUND Winner : {winner.TeamName}!", eChatType.CT_Important);
            BroadcastSoundToParticipants(session, 9213);

            GiveRewards(session, winner);

            foreach (var p in winner.Members) ClearParticipant(p, winner);
            
            session.State = eArenaState.Cooldown;
            int cdMins = Properties.ARENA_COOLDOWN_MINUTES > 0 ? Properties.ARENA_COOLDOWN_MINUTES : 60;
            session.NextStateTime = WorldMgr.GetRegion(session.RegionID).Time + (cdMins * 60 * 1000);
            
            session.ActiveTeams.Clear();
            session.NextRoundTeams.Clear();
            session.Bracket.Clear();
        }

        private void BroadcastSoundToParticipants(ArenaSession session, ushort soundId)
        {
            var allTeams = new List<ArenaTeam>(session.Bracket);
            allTeams.AddRange(session.NextRoundTeams);
            if (session.CurrentTeamA != null) allTeams.Add(session.CurrentTeamA);
            if (session.CurrentTeamB != null) allTeams.Add(session.CurrentTeamB);

            foreach (var t in allTeams)
            {
                foreach (var p in t.Members)
                {
                    if (p != null && p.Client != null)
                    {
                        p.Out.SendSoundEffect(soundId, p.Position, 0);
                    }
                }
            }
        }

        private void GiveRewards(ArenaSession session, ArenaTeam winner)
        {
            int playersOrTeams = session.Mode == eArenaMode.Solo ? session.PeakParticipantsCount : session.PeakTeamsCount;
            int minNeeded = session.Mode == eArenaMode.Solo ? Properties.ARENA_MIN_SOLO : Properties.ARENA_MIN_TEAMS;
            if (minNeeded <= 0) minNeeded = 1;
            
            double ratio = (double)playersOrTeams / minNeeded;
            eRewardTier tier = eRewardTier.PvPTier3;
            string rarityPrefix = "Uncommon";
            int color = 18;
            int targetSlots = 7;
            double targetMaxUti = 72;
            int regionID = 30;

            if (ratio >= 4.5) { tier = eRewardTier.PvPTier1; rarityPrefix = "Eminent"; color = 22; targetSlots = 9; targetMaxUti = 92; regionID = 230; }
            else if (ratio >= 4.0) { tier = eRewardTier.PvPTier1; rarityPrefix = "Illustrious"; color = 22; targetSlots = 9; targetMaxUti = 85; regionID = 230; }
            else if (ratio >= 3.5) { tier = eRewardTier.PvPTier2; rarityPrefix = "Epic"; color = 19; targetSlots = 8; targetMaxUti = 95; regionID = 233; }
            else if (ratio >= 3.0) { tier = eRewardTier.PvPTier2; rarityPrefix = "Superior"; color = 19; targetSlots = 8; targetMaxUti = 86; regionID = 233; }
            else if (ratio >= 2.5) { tier = eRewardTier.PvPTier2; rarityPrefix = "Flawless"; color = 19; targetSlots = 8; targetMaxUti = 82; regionID = 58; }
            else if (ratio >= 2.0) { tier = eRewardTier.PvPTier3; rarityPrefix = "Rare"; color = 18; targetSlots = 8; targetMaxUti = 75; regionID = 35; }
            else if (ratio >= 1.5) { tier = eRewardTier.PvPTier3; rarityPrefix = "Fine"; color = 18; targetSlots = 7; targetMaxUti = 80; regionID = 35; }
            
            // Dynamic Scaler based on total rounds fought
            int roundsFought = Math.Max(1, session.RoundNumber - 1);
            double roundScale = roundsFought / 10.0;
            if (roundScale <= 0) roundScale = 0.1;

            double rpRatio = 0.04 * roundScale; 
            int bpReward = (int)(80 * roundScale);
            int baseGold = (int)(8000 * roundScale);

            foreach (var p in winner.Members)
            {
                if (p != null && p.IsAlive && p.CurrentRegionID == session.RegionID)
                {
                    // Calculate and Distribute the Base Rewards
                    long rpReward = (long)(p.CalculateRPsToGainRealmRank() * rpRatio);
                    long goldReward = (long)(p.Level * baseGold);
                    
                    int bpBonus = p.GetModified(eProperty.BountyPoints);
                    long finalBpReward = bpReward + (bpReward * bpBonus) / 100;
                    
                    int coinBonus = p.GetModified(eProperty.MythicalCoin);
                    long finalGoldReward = goldReward + (goldReward * coinBonus) / 100;
                    
                    if (rpReward > 0) p.GainRealmPoints(rpReward);
                    if (finalBpReward > 0) p.GainBountyPoints(finalBpReward);
                    if (finalGoldReward > 0) p.AddMoney(DOL.GS.Finance.Currency.Copper.Mint(finalGoldReward));
                    
                    p.Out.SendMessage($"Congratulations! You survived {roundsFought} rounds and won the Arena! You received {rpReward} RP, {finalBpReward} BP, and {DOL.GS.Finance.Money.Mint(finalGoldReward, Currency.Copper).ToText()}!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                    // Spawn the Physical Chests
                    var c1 = CreateChest(p, session.ArenaMaster, tier, eRewardChestType.ScrollsAndBuffs, new LootGeneratorRewardScrolls(), "Chest of Magic Scrolls", targetSlots, targetMaxUti, regionID, rarityPrefix, color);
                    var c2 = CreateChest(p, session.ArenaMaster, tier, eRewardChestType.ArmorsAndWeapons, new LootGeneratorRewardArmors(), "Chest of Armaments", targetSlots, targetMaxUti, regionID, rarityPrefix, color);
                    var c3 = CreateChest(p, session.ArenaMaster, tier, eRewardChestType.Jewellery, new LootGeneratorRewardJewels(), "Chest of Magical Jewellery", targetSlots, targetMaxUti, regionID, rarityPrefix, color);

                    c1.Siblings.Add(c2); c1.Siblings.Add(c3);
                    c2.Siblings.Add(c1); c2.Siblings.Add(c3);
                    c3.Siblings.Add(c1); c3.Siblings.Add(c2);

                    c1.AddToWorld(); c2.AddToWorld(); c3.AddToWorld();
                    p.Out.SendMessage($"Your {rarityPrefix} reward chests have spawned nearby!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                    lock (_lock)
                    {
                        session.SpawnedChests.Add(c1);
                        session.SpawnedChests.Add(c2);
                        session.SpawnedChests.Add(c3);
                    }
                }
            }
        }

        private RewardChest CreateChest(GamePlayer player, GameNPC refObj, eRewardTier tier, eRewardChestType type, ILootGenerator generator, string name, int targetSlots, double targetMaxUti, int regionID, string rarityPrefix, int color)
        {
            int distance = 160;
            ushort baseHeading = (ushort)refObj.SpawnPosition.Orientation.InHeading;
            ushort angle45 = 512;
            ushort angle25 = 284;
            ushort baseFacing = (ushort)((baseHeading + angle25) % 4096);
            ushort placementHeading = baseHeading;
            if (type == eRewardChestType.ArmorsAndWeapons) placementHeading = (ushort)((baseHeading + angle45) % 4096);
            if (type == eRewardChestType.Jewellery) placementHeading = (ushort)((baseHeading - angle45 + 4096) % 4096);

            double angleRadians = Angle.Heading(placementHeading).InRadians;
            int cx = refObj.Position.X - (int)Math.Round(Math.Sin(angleRadians) * distance);
            int cy = refObj.Position.Y + (int)Math.Round(Math.Cos(angleRadians) * distance);

            var chest = new RewardChest
            {
                Name = name,
                Model = 1596,
                CurrentRegionID = refObj.CurrentRegionID,
                Heading = baseFacing,
                Position = Position.Create(refObj.CurrentRegionID, cx, cy, refObj.Position.Z, baseFacing),
                Realm = eRealm.None,
                Tier = tier,
                ChestType = type,
                OwnerID = player.InternalID,
                LootGenerators = new List<ILootGenerator> { generator },
                ItemChance = 100,
                ConfigTargetSlots = targetSlots,
                ConfigTargetMaxUti = targetMaxUti,
                ConfigRegionID = regionID,
                ConfigRarityPrefix = rarityPrefix,
                ConfigItemColor = color
            };

            int duration = Properties.ARENA_QUEUE_MINUTES + 60;
            return chest;
        }

        private void CheckDistanceViolations(ArenaSession session, long now)
        {
            var allTeams = new List<ArenaTeam>(session.Bracket);
            allTeams.AddRange(session.NextRoundTeams);
            if (session.CurrentTeamA != null) allTeams.Add(session.CurrentTeamA);
            if (session.CurrentTeamB != null) allTeams.Add(session.CurrentTeamB);

            foreach (var team in allTeams)
            {
                if (team.IsDisqualified) continue;

                foreach (var p in team.Members)
                {
                    if (p == null || !p.IsAlive || p.CurrentRegionID != session.RegionID) continue;

                    if (p.Coordinate.DistanceTo(session.ArenaMaster.Coordinate) > 5000)
                    {
                        if (!session.OobWarnings.ContainsKey(p.InternalID))
                        {
                            session.OobWarnings[p.InternalID] = now + 15000;
                            p.Out.SendMessage("You left the Arena bounds! Return within 15 seconds or be disqualified!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            long timeLeft = session.OobWarnings[p.InternalID] - now;
                            if (timeLeft <= 0) DisqualifyTeam(session, team, $"{p.Name} left the arena bounds.");
                            else if (timeLeft <= 5000 && timeLeft > 3000) p.Out.SendMessage("Return to the arena! 5 seconds left!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                            else if (timeLeft <= 10000 && timeLeft > 8000) p.Out.SendMessage("Return to the arena! 10 seconds left!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        }
                    }
                    else if (session.OobWarnings.ContainsKey(p.InternalID))
                    {
                        session.OobWarnings.Remove(p.InternalID);
                        p.Out.SendMessage("You returned to the arena boundaries.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }
            }
        }

        public void DisqualifyTeam(ArenaSession session, ArenaTeam team, string reason)
        {
            if (team.IsDisqualified) return;
            team.IsDisqualified = true;

            foreach (var p in team.Members)
            {
                if (p != null)
                {
                    if (p.Client != null && p.Client.IsPlaying && p.CurrentRegionID == session.RegionID)
                        p.Out.SendMessage($"Your team has been disqualified: {reason}", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    ClearParticipant(p, team);
                    p.TempProperties.setProperty("ArenaMatchDead", true);
                    p.MoveTo(session.ArenaMaster.Position);
                }
            }

            int validTeams = session.ActiveTeams.Count(t => !t.IsDisqualified);
            int minThreshold = session.Mode == eArenaMode.Solo ? Properties.ARENA_MIN_SOLO : Properties.ARENA_MIN_TEAMS;

            if (validTeams < minThreshold)
                CancelSession(session, "Too many participants left the arena. Minimum threshold breached.");
        }

        public void CancelSession(ArenaSession session, string reason)
        {
            if (session.State != eArenaState.Idle)
            {
                BroadcastToParticipants(session, $"Arena Contest Cancelled: {reason}");

                foreach (var chest in session.SpawnedChests)
                {
                    chest.RemoveFromWorld();
                    chest.Delete();
                }
                session.SpawnedChests.Clear();
                RefundBets(session);
            }

            foreach (var p in session.SoloQueue) ClearParticipant(p, null);
            foreach (var g in session.GroupQueue) foreach (var p in g.GetPlayersInTheGroup()) ClearParticipant(p, null);
            foreach (var t in session.ActiveTeams) foreach (var p in t.Members) ClearParticipant(p, t);
            foreach (var t in session.NextRoundTeams) foreach (var p in t.Members) ClearParticipant(p, t);
            if (session.CurrentTeamA != null) foreach (var p in session.CurrentTeamA.Members) ClearParticipant(p, session.CurrentTeamA);
            if (session.CurrentTeamB != null) foreach (var p in session.CurrentTeamB.Members) ClearParticipant(p, session.CurrentTeamB);

            session.SoloQueue.Clear(); session.GroupQueue.Clear(); session.ActiveTeams.Clear();
            session.NextRoundTeams.Clear(); session.Bracket.Clear(); session.CurrentTeamA = null; session.CurrentTeamB = null;
            session.State = eArenaState.Idle;
            session.IsBettingOpen = false;
            session.IsDebugMode = false;
        }

        private void TeleportTeamToSpawns(ArenaTeam team, List<GameNPC> spawns, bool wait, bool visible = false)
        {
            for (int i = 0; i < team.Members.Count; i++)
            {
                var p = team.Members[i];
                if (p == null || !p.IsAlive) continue;
                var sp = spawns[i % spawns.Count];
                p.MoveTo(sp.Position);
                SetWaitState(p, wait, visible); 
                p.Health = p.MaxHealth; p.Mana = p.MaxMana; p.Endurance = p.MaxEndurance;
            }
        }

        private void UnlockTeam(ArenaTeam team)
        {
            if (team == null) return;
            foreach (var p in team.Members)
            {
                if (p != null && p.IsAlive)
                {
                    SetWaitState(p, false, true);
                }
            }
        }

        private void SetWaitState(GamePlayer p, bool wait, bool visible = false)
        {
            if (p == null) return;
            if (wait)
            {
                p.TempProperties.setProperty(ARENA_WAITING_PROP, true);
                p.StopCurrentSpellcast();
                p.StopAttack();
                if (!visible) p.Stealth(true);
            }
            else
            {
                p.TempProperties.removeProperty(ARENA_WAITING_PROP);
                p.Stealth(false);
            }
        }

        private void ClearParticipant(GamePlayer p, ArenaTeam team)
        {
            if (p == null) return;
            p.TempProperties.removeProperty(ARENA_PARTICIPANT_PROP);
            p.TempProperties.removeProperty("ArenaQueued");
            p.TempProperties.removeProperty("ArenaRegion");
            p.TempProperties.removeProperty("ArenaMatchDead");
            p.TempProperties.removeProperty("ArenaQueueTime");
            RoleplayReward.ResetRPChain(p);
            SetWaitState(p, false, true);

            // Recover Original Guild and DB Bind location using RvrPlayer Record
            RvrPlayer dbPlayer = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(p.InternalID));
            if (dbPlayer != null && dbPlayer.PvPSession == "Arena")
            {
                string oldGuildId = dbPlayer.GuildID;
                int oldGuildRank = dbPlayer.GuildRank;
                dbPlayer.ResetCharacter(p);
                p.SaveIntoDatabase();
                GameServer.Database.DeleteObject(dbPlayer);

                if (!string.IsNullOrEmpty(oldGuildId))
                {
                    Guild restoredGuild = GuildMgr.GetGuildByGuildID(oldGuildId);
                    if (restoredGuild != null)
                    {
                        DBRank restoredRank = restoredGuild.GetRankByID(oldGuildRank) ?? restoredGuild.GetRankByID(9);
                        restoredGuild.AddPlayer(p, restoredRank, true);
                    }
                }
            }

            // Disband the auto-generated group if they weren't grouped before
            if (team != null && !team.SubscribedAsGroup && p.Group != null)
            {
                p.Group.RemoveMember(p);
            }
        }

        private void BroadcastToParticipants(ArenaSession session, string message, bool centerScreen = false)
        {
            foreach (var p in session.SoloQueue) p.Out.SendMessage(message, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            foreach (var g in session.GroupQueue) foreach (var p in g.GetPlayersInTheGroup()) p.Out.SendMessage(message, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            
            var allTeams = new List<ArenaTeam>(session.Bracket);
            allTeams.AddRange(session.NextRoundTeams);
            if (session.CurrentTeamA != null) allTeams.Add(session.CurrentTeamA);
            if (session.CurrentTeamB != null) allTeams.Add(session.CurrentTeamB);

            foreach (var t in allTeams) foreach (var p in t.Members) if (p != null) { p.Out.SendMessage(message, eChatType.CT_Important, eChatLoc.CL_SystemWindow); if (centerScreen) p.Out.SendMessage(message, eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow); }
        }

        private void BroadcastRegion(ushort regionId, string message, eChatType type = eChatType.CT_System)
        {
            foreach (var c in WorldMgr.GetClientsOfRegion(regionId)) c.Player?.Out.SendMessage(message, type, eChatLoc.CL_SystemWindow);
        }

        private void BroadcastRegionLog(ushort regionId, string message)
        {
            foreach (var c in WorldMgr.GetClientsOfRegion(regionId))
                if (c.Player != null && !c.Player.TempProperties.getProperty(ARENA_PARTICIPANT_PROP, false)) c.Player.Out.SendMessage(message, eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
        }

        private static void OnPlayerLogin(DOLEvent e, object sender, EventArgs args)
        {
            // If the server crashed while players were in the arena, restore them so they aren't orphaned
            if (sender is GamePlayer p)
            {
                RvrPlayer dbPlayer = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(p.InternalID));
                if (dbPlayer != null && dbPlayer.PvPSession == "Arena")
                {
                    string oldGuildId = dbPlayer.GuildID;
                    int oldGuildRank = dbPlayer.GuildRank;
                    dbPlayer.ResetCharacter(p);
                    p.MoveTo(p.BindPosition);
                    p.SaveIntoDatabase();

                    GameServer.Database.DeleteObject(dbPlayer);

                    if (!string.IsNullOrEmpty(oldGuildId))
                    {
                        Guild restoredGuild = GuildMgr.GetGuildByGuildID(oldGuildId);
                        if (restoredGuild != null)
                        {
                            DBRank restoredRank = restoredGuild.GetRankByID(oldGuildRank) ?? restoredGuild.GetRankByID(9);
                            restoredGuild.AddPlayer(p, restoredRank, true);
                        }
                    }

                    p.TempProperties.removeProperty(ARENA_PARTICIPANT_PROP);
                    p.TempProperties.removeProperty("ArenaQueued");
                }
            }
        }

        private static void OnPlayerQuitOrRegionChange(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GamePlayer p)
            {
                ushort regId = p.TempProperties.getProperty<ushort>("ArenaRegion", 0);
                if (e == GamePlayerEvent.RegionChanged && p.CurrentRegionID == regId) return;

                if (p.TempProperties.getProperty<bool>("ArenaQueued", false))
                {
                    var session = Instance.GetSession(regId);
                    if (session != null) Instance.DequeuePlayer(session, p, false);
                }

                if (p.TempProperties.getProperty<bool>(ARENA_PARTICIPANT_PROP, false))
                {
                    var session = Instance.GetSession(regId);
                    if (session != null)
                    {
                        var allTeams = new List<ArenaTeam>(session.Bracket);
                        allTeams.AddRange(session.NextRoundTeams);
                        if (session.CurrentTeamA != null) allTeams.Add(session.CurrentTeamA);
                        if (session.CurrentTeamB != null) allTeams.Add(session.CurrentTeamB);

                        var team = allTeams.FirstOrDefault(t => t.Members.Contains(p));
                        if (team != null && !team.IsDisqualified)
                        {
                            Instance.DisqualifyTeam(session, team, $"{p.Name} left the region or disconnected.");
                        }
                    }
                    Instance.ClearParticipant(p, null);
                }
            }
        }

        private static void OnPlayerDying(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GamePlayer p && p.TempProperties.getProperty<bool>(ARENA_PARTICIPANT_PROP, false))
            {
                p.TempProperties.setProperty("ArenaMatchDead", true);
            }
        }

        public void HandleDebugCommand(ArenaSession session, GamePlayer player, string command)
        {
            lock (_lock)
            {
                if (command == "force shutdown")
                {
                    CancelSession(session, "Forced shutdown by GM.");
                    player.Out.SendMessage("Arena session force shut down.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (command == "debug: start solo match" || command == "debug: start as spectator")
                {
                    CancelSession(session, "Debug override.");
                    session.IsDebugMode = true;
                    session.State = eArenaState.Running;
                    session.Mode = eArenaMode.Solo;
                    session.RoundNumber = 1;

                    session.LeftSpawns = WorldMgr.GetNPCsFromRegion(session.RegionID).Where(n => n.Name.StartsWith("ARENA_SPAWN_LEFT_")).OrderBy(n => n.Name).ToList();
                    session.RightSpawns = WorldMgr.GetNPCsFromRegion(session.RegionID).Where(n => n.Name.StartsWith("ARENA_SPAWN_RIGHT_")).OrderBy(n => n.Name).ToList();

                    var teamA = new ArenaTeam { TeamName = "Fighters (Debug)", Members = new List<GamePlayer>() };
                    var teamB = new ArenaTeam { TeamName = "Dummy Target", Members = new List<GamePlayer>() };

                    session.ActiveTeams.Add(teamA);
                    session.CurrentTeamA = teamA;
                    session.CurrentTeamB = teamB;
                    session.NextStateTime = WorldMgr.GetRegion(session.RegionID).Time + 3600000;

                    if (command == "debug: start solo match")
                    {
                        teamA.Members.Add(player);
                        SetupDebugParticipant(player, session);
                        TeleportTeamToSpawns(teamA, session.LeftSpawns, false, true);
                        player.Out.SendMessage("You forced a solo debug match. You are currently the active fighter.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else if (command == "debug: start as spectator")
                    {
                        var teamWait = new ArenaTeam { TeamName = player.Name + " (Spectator)", Members = new List<GamePlayer> { player } };
                        session.Bracket.Enqueue(teamWait);
                        session.ActiveTeams.Add(teamWait);

                        SetupDebugParticipant(player, session);
                        player.MoveTo(session.ArenaMaster.Position);
                        SetWaitState(player, true, false);
                        player.Out.SendMessage("You forced a debug match and joined as a spectator.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }

                else if (command == "debug: switch as spectator" || command == "debug: join as a spectator")
                {
                    if (session.State != eArenaState.Running || !session.IsDebugMode) return;

                    // Remove from fighter teams if they are in them
                    if (session.CurrentTeamA != null && session.CurrentTeamA.Members.Contains(player))
                        session.CurrentTeamA.Members.Remove(player);
                    if (session.CurrentTeamB != null && session.CurrentTeamB.Members.Contains(player))
                        session.CurrentTeamB.Members.Remove(player);

                    // If not already in the waiting bracket, put them there
                    if (!session.Bracket.Any(t => t.Members.Contains(player)))
                    {
                        var teamWait = new ArenaTeam { TeamName = player.Name + " (Spectator)", Members = new List<GamePlayer> { player } };
                        session.Bracket.Enqueue(teamWait);
                        session.ActiveTeams.Add(teamWait);
                    }

                    SetupDebugParticipant(player, session);
                    player.MoveTo(session.ArenaMaster.Position);
                    SetWaitState(player, true, false);
                    player.Out.SendMessage("You joined/switched to spectator mode.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else if (command == "debug: switch as fighter" || command == "debug: join as a fighter")
                {
                    if (session.State != eArenaState.Running || !session.IsDebugMode) return;

                    // Rebuild the bracket queue to cleanly remove the spectator team if they are in it
                    var tempBracket = new List<ArenaTeam>(session.Bracket);
                    var specTeam = tempBracket.FirstOrDefault(t => t.Members.Contains(player));

                    if (specTeam != null)
                    {
                        specTeam.Members.Remove(player);
                        if (specTeam.Members.Count == 0)
                        {
                            tempBracket.Remove(specTeam);
                            session.ActiveTeams.Remove(specTeam);
                        }
                        session.Bracket = new Queue<ArenaTeam>(tempBracket);
                    }

                    // Put into the active fighting team
                    if (session.CurrentTeamA == null)
                        session.CurrentTeamA = new ArenaTeam { TeamName = "Fighters (Debug)" };

                    if (!session.CurrentTeamA.Members.Contains(player))
                    {
                        session.CurrentTeamA.Members.Add(player);
                        if (!session.ActiveTeams.Contains(session.CurrentTeamA))
                            session.ActiveTeams.Add(session.CurrentTeamA);
                    }

                    SetupDebugParticipant(player, session);

                    if (session.LeftSpawns.Count > 0)
                    {
                        player.MoveTo(session.LeftSpawns[0].Position);
                    }

                    SetWaitState(player, false, true);
                    player.Health = player.MaxHealth;
                    player.Mana = player.MaxMana;
                    player.Endurance = player.MaxEndurance;

                    player.Out.SendMessage("You joined/switched to fighter mode.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
        }

        private void SetupDebugParticipant(GamePlayer p, ArenaSession session)
        {
            p.TempProperties.removeProperty("ArenaQueued");
            p.TempProperties.setProperty(ARENA_PARTICIPANT_PROP, true);
            RoleplayReward.ResetRPChain(p);

            RvrPlayer dbPlayer = GameServer.Database.SelectObject<RvrPlayer>(DB.Column("PlayerID").IsEqualTo(p.InternalID));
            bool isNew = false;
            if (dbPlayer == null)
            {
                dbPlayer = new RvrPlayer();
                dbPlayer.PlayerID = p.InternalID;
                isNew = true;
            }

            dbPlayer.GuildID = p.GuildID ?? "";
            dbPlayer.GuildRank = p.GuildRank != null ? p.GuildRank.RankLevel : 9;

            dbPlayer.OldX = p.Coordinate.X;
            dbPlayer.OldY = p.Coordinate.Y;
            dbPlayer.OldZ = p.Coordinate.Z;
            dbPlayer.OldHeading = p.Heading;
            dbPlayer.OldRegion = p.CurrentRegionID;

            dbPlayer.OldBindX = p.BindPosition.Coordinate.X;
            dbPlayer.OldBindY = p.BindPosition.Coordinate.Y;
            dbPlayer.OldBindZ = p.BindPosition.Coordinate.Z;
            dbPlayer.OldBindHeading = (int)p.BindPosition.Orientation.InHeading;
            dbPlayer.OldBindRegion = p.BindPosition.RegionID;

            dbPlayer.PvPSession = "Arena";

            if (isNew) GameServer.Database.AddObject(dbPlayer);
            else GameServer.Database.SaveObject(dbPlayer);

            if (p.Guild != null) p.Guild.RemovePlayer("Arena", p);

            p.BindPosition = session.ArenaMaster.Position;
            p.SaveIntoDatabase();
        }
    }

    public class ArenaManagerStarter
    {
        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            ArenaManager.Instance.Start();
        }
    }
}