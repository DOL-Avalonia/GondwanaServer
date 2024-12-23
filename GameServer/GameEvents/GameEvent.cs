﻿using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOLDatabase.Tables;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Timers;
using DOL.Language;
using Grpc.Core;
using System.Net;

namespace DOL.GameEvents
{
    public class GameEvent
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private object _db;
        private GamePlayer owner;

        public Timer RandomTextTimer { get; }
        public Timer RemainingTimeTimer { get; }

        public Timer ResetFamilyTimer { get; }

        public Dictionary<string, ushort> StartEffects;
        public Dictionary<string, ushort> EndEffects;
        public Dictionary<string, GameNPC> RemovedMobs { get; }
        public Dictionary<string, GameStaticItem> RemovedCoffres { get; }
        public List<GameNPC> RelatedNPCs { get; } = new();

        public GameEvent(GameEvent ev)
        {
            _db = ev._db;
            ID = ev.ID;
            RandomTextTimer = new Timer();
            RemainingTimeTimer = new Timer();
            ResetFamilyTimer = new Timer();
            Owner = ev.Owner;
            InstancedConditionType = ev.InstancedConditionType;

            ParseValuesFromDb(ev._db as EventDB);

            Coffres = new List<GameStaticItem>();
            foreach (var coffre in ev.Coffres)
            {
                Coffres.Add(coffre.Copy());
            }
            Mobs = new List<GameNPC>();
            foreach (var mob in ev.Mobs)
            {
                GameNPC newMob = null;
                var mobDef = GameServer.Database.FindObjectByKey<Mob>(mob.InternalID);
                Console.WriteLine("MobDef: " + mobDef.ClassType);
                Type type = ScriptMgr.FindNPCGuildScriptClass(mobDef.Guild, (eRealm)mob.Realm);
                Assembly gasm = Assembly.GetAssembly(typeof(GameServer));
                if (type != null)
                {
                    try
                    {
                        newMob = (GameNPC)type.Assembly.CreateInstance(type.FullName);
                    }
                    catch (Exception e)
                    {
                        if (log.IsErrorEnabled)
                            log.Error("LoadFromDatabase", e);
                    }
                }
                if (newMob == null)
                {
                    try
                    {
                        newMob = (GameNPC)gasm.CreateInstance(mobDef.ClassType, false);
                    }
                    catch
                    {
                    }
                    if (newMob == null)
                    {
                        foreach (Assembly asm in ScriptMgr.Scripts)
                        {
                            try
                            {
                                newMob = (GameNPC)asm.CreateInstance(mobDef.ClassType, false);
                            }
                            catch
                            {
                            }

                            if (newMob != null)
                                break;
                        }
                    }
                }
                if (newMob == null)
                {
                    newMob = new GameNPC();
                }

                newMob.Name = mob.Name;
                newMob.InternalID = mob.InternalID;
                newMob.GuildName = mobDef.Guild;
                newMob.LoadFromDatabase(mobDef);
                newMob.BuildAmbientTexts();
                newMob.Event = this;
                Mobs.Add(newMob);
            }

            StartEffects = new Dictionary<string, ushort>(ev.StartEffects);
            EndEffects = new Dictionary<string, ushort>(ev.EndEffects);
            RemovedMobs = new Dictionary<string, GameNPC>(ev.RemovedMobs);
            RemovedCoffres = new Dictionary<string, GameStaticItem>(ev.RemovedCoffres);
        }

        public GameEvent(EventDB db)
        {
            _db = db.Clone();
            ID = db.ObjectId;
            this.RandomTextTimer = new Timer();
            this.RemainingTimeTimer = new Timer();
            this.ResetFamilyTimer = new Timer();
            this.EventFamily = new Dictionary<string, bool>();

            ParseValuesFromDb(db);

            this.Coffres = new List<GameStaticItem>();
            this.Mobs = new List<GameNPC>();
            this.StartEffects = new Dictionary<string, ushort>();
            this.EndEffects = new Dictionary<string, ushort>();
            RemovedMobs = new Dictionary<string, GameNPC>();
            RemovedCoffres = new Dictionary<string, GameStaticItem>();
        }

        public void ParseValuesFromDb(EventDB db)
        {
            EventAreas = !string.IsNullOrEmpty(db.EventAreas) ? db.EventAreas.Split(new char[] { '|' }) : null;
            EventChance = db.EventChance;
            EventName = db.EventName;
            EventZones = !string.IsNullOrEmpty(db.EventZones) ? db.EventZones.Split(new char[] { '|' }) : null;
            ShowEvent = db.ShowEvent;
            StartConditionType = Enum.TryParse(db.StartConditionType.ToString(), out StartingConditionType st) ? st : StartingConditionType.Timer;
            EventChanceInterval = db.EventChanceInterval > 0 && db.EventChanceInterval < long.MaxValue ? TimeSpan.FromMinutes(db.EventChanceInterval) : (TimeSpan?)null;
            DebutText = !string.IsNullOrEmpty(db.DebutText) ? db.DebutText : null;
            EndText = !string.IsNullOrEmpty(db.EndText) ? db.EndText : null;
            StartedTime = (DateTimeOffset?)null;
            EndingConditionTypes = db.EndingConditionTypes.Split(new char[] { '|' }).Select(c => Enum.TryParse(c, out EndingConditionType end) ? end : GameEvents.EndingConditionType.Timer);
            RandomText = !string.IsNullOrEmpty(db.RandomText) ? db.RandomText.Split(new char[] { '|' }) : null;
            RandTextInterval = db.RandTextInterval > 0 && db.RandTextInterval < long.MaxValue ? TimeSpan.FromMinutes(db.RandTextInterval) : (TimeSpan?)null;
            RemainingTimeInterval = db.RemainingTimeInterval > 0 && db.RemainingTimeInterval < long.MaxValue ? TimeSpan.FromMinutes(db.RemainingTimeInterval) : (TimeSpan?)null;
            RemainingTimeText = !string.IsNullOrEmpty(db.RemainingTimeText) ? db.RemainingTimeText : null;
            EndingActionA = Enum.TryParse(db.EndingActionA.ToString(), out EndingAction endActionA) ? endActionA : EndingAction.None;
            EndingActionB = Enum.TryParse(db.EndingActionB.ToString(), out EndingAction endActionB) ? endActionB : EndingAction.None;
            MobNamesToKill = !string.IsNullOrEmpty(db.MobNamesToKill) ? db.MobNamesToKill.Split(new char[] { '|' }) : null;
            EndActionStartEventID = !string.IsNullOrEmpty(db.EndActionStartEventID) ? db.EndActionStartEventID : null;
            StartActionStopEventID = !string.IsNullOrEmpty(db.StartActionStopEventID) ? db.StartActionStopEventID : null;
            StartTriggerTime = db.StartTriggerTime > 0 && db.StartTriggerTime < long.MaxValue ? DateTimeOffset.FromUnixTimeSeconds(db.StartTriggerTime) : (DateTimeOffset?)null;
            TimerType = Enum.TryParse(db.TimerType.ToString(), out TimerType timer) ? timer : TimerType.DateType;
            EndTime = (DateTimeOffset?)null;
            ChronoTime = db.ChronoTime;
            KillStartingGroupMobId = !string.IsNullOrEmpty(db.KillStartingGroupMobId) ? db.KillStartingGroupMobId : null;
            ResetEventId = !string.IsNullOrEmpty(db.ResetEventId) ? db.ResetEventId : null;
            Status = EventStatus.NotOver;
            ChanceLastTimeChecked = DateTimeOffset.FromUnixTimeSeconds(0);
            AnnonceType = Enum.TryParse(db.AnnonceType.ToString(), out AnnonceType a) ? a : AnnonceType.Center;
            Discord = db.Discord;
            InstancedConditionType = Enum.TryParse(db.InstancedConditionType.ToString(), out InstancedConditionTypes inst) ? inst : InstancedConditionTypes.All;
            AreaStartingId = !string.IsNullOrEmpty(db.AreaStartingId) ? db.AreaStartingId : null;
            QuestStartingId = !string.IsNullOrEmpty(db.QuestStartingId) ? db.QuestStartingId : null;
            ParallelLaunch = db.ParallelLaunch;
            StartEventSound = db.StartEventSound;
            RandomEventSound = db.RandomEventSound;
            RemainingTimeEvSound = db.RemainingTimeEvSound;
            EndEventSound = db.EndEventSound;
            TPPointID = db.TPPointID;

            // get kes from string[] db.EventFamily, and set values to false 
            if (db.EventFamily != null)
                foreach (string family in db.EventFamily.Split('|'))
                    EventFamily.Add(family, false);
            if (db.TimerBeforeReset != 0)
            {
                TimeBeforeReset = db.TimerBeforeReset;
                ResetFamilyTimer.Interval = ((long)TimeBeforeReset) * 1000;
                ResetFamilyTimer.Elapsed += ResetFamilyTimer_Elapsed;
            }

            //Handle invalid ChronoType
            if (TimerType == TimerType.ChronoType && ChronoTime <= 0)
            {
                //Define 5 minutes by default
                log.Error(string.Format("Event with Chrono Timer tpye has wrong value: {0}, value set to 5 minutes instead", ChronoTime));
                ChronoTime = 5;
            }

            if (StartConditionType == StartingConditionType.Kill && KillStartingGroupMobId == null)
            {
                log.Error(string.Format("Event Id: {0}, Name: {1}, with kill Starting Type will not start because KillStartingMob is Null", ID, EventName));
            }

            if (RandTextInterval.HasValue && RandomText != null && this.EventZones?.Any() == true)
            {
                this.RandomTextTimer.Interval = ((long)RandTextInterval.Value.TotalMinutes).ToTimerMilliseconds();
                this.RandomTextTimer.Elapsed += RandomTextTimer_Elapsed;
                this.RandomTextTimer.AutoReset = true;
                this.HasHandomText = true;
            }

            if (RemainingTimeText != null && RemainingTimeInterval.HasValue && this.EventZones?.Any() == true)
            {
                this.HasRemainingTimeText = true;
                this.RemainingTimeTimer.Interval = ((long)RemainingTimeInterval.Value.TotalMinutes).ToTimerMilliseconds();
                this.RemainingTimeTimer.AutoReset = true;
                this.RemainingTimeTimer.Elapsed += RemainingTimeTimer_Elapsed;
            }

            if (MobNamesToKill?.Any() == true && EndingConditionTypes.Contains(EndingConditionType.Kill))
            {
                IsKillingEvent = true;
            }

            if (EndTime.HasValue && EndingConditionTypes.Contains(EndingConditionType.Timer))
            {
                IsTimingEvent = true;
            }
        }
        
        public string FormatEventMessage(string message)
        {
            if (Owner == null)
                return message;
            
            if (Owner.Guild is not { GuildType: Guild.eGuildType.ServerGuild })
            {
                message = message.Replace("<guilde>", Owner.GuildName);
            }
            if (message.Contains("<player>"))
            {
                message = message.Replace("<player>", Owner.Name);
            }
            if (Owner.Group != null)
            {
                message = message.Replace("<group>", Owner.Group.Leader.Name);
            }
            if (Owner != null)
            {
                message = message.Replace("<race>", Owner.RaceName);
            }
            if (Owner != null)
            {
                message = message.Replace("<class>", Owner.CharacterClass.Name);
            }
            return message;
        }

        public bool IsVisibleTo(GameObject obj)
        {
            if (Owner == obj)
                return true;
            
            switch (InstancedConditionType)
            {
                case InstancedConditionTypes.All:
                    return true;
                case InstancedConditionTypes.Player:
                    return Owner == obj;
                case InstancedConditionTypes.Group:
                    return obj is GameLiving living && Owner?.Group?.IsInTheGroup(living) == true;
                case InstancedConditionTypes.Guild:
                    return Owner?.Guild is { IsSystemGuild: false } && (obj as GamePlayer)?.Guild == Owner.Guild;
                case InstancedConditionTypes.Battlegroup:
                    return obj is GamePlayer player && Owner?.BattleGroup?.IsInTheBattleGroup(player) == true;
                default:
                    return false; // Unknown instance type
            }
        }

        private IEnumerable<GamePlayer> GetPlayersInEventZones(IEnumerable<string> eventZones)
        {
            return WorldMgr.GetAllPlayingClients()
                .Where(c => eventZones.Contains(c.Player.CurrentZone.ID.ToString()))
                .Select(c => c.Player);
        }

        private void RemainingTimeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var player in GetPlayersInEventZones(this.EventZones))
            {
                string message = this.GetFormattedRemainingTimeText(player.Client.Account.Language, player);
                GameEventManager.NotifyPlayer(player, AnnonceType, message);
            }

            if (this.RemainingTimeEvSound > 0)
            {
                foreach (var player in GetPlayersInEventZones(this.EventZones))
                {
                    player.Out.SendSoundEffect((ushort)this.RemainingTimeEvSound, player.Position, 0);
                }
            }
        }

        private void RandomTextTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var rand = new Random(DateTime.Now.Millisecond);
            int index = rand.Next(0, this.RandomText.Count());

            foreach (var player in GetPlayersInEventZones(this.EventZones))
            {
                string message = this.GetFormattedRandomText(player.Client.Account.Language, player);
                GameEventManager.NotifyPlayer(player, this.AnnonceType, message);
            }

            if (!string.IsNullOrEmpty(this.RandomEventSound))
            {
                var sounds = this.RandomEventSound.Split('|').Select(int.Parse).ToArray();
                int soundIndex = rand.Next(0, sounds.Length);
                foreach (var player in GetPlayersInEventZones(this.EventZones))
                {
                    player.Out.SendSoundEffect((ushort)sounds[soundIndex], player.Position, 0);
                }
            }
        }

        private void ResetFamilyTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ResetFamilyTimer.Stop();
            foreach (var family in EventFamily)
            {
                if (EventFamily[family.Key])
                {
                    GameEventManager.Instance.ResetEventsFromId(family.Key);
                    EventFamily[family.Key] = false;
                }
            }
        }

        public string ID
        {
            get;
            set;
        }

        public string EventName
        {
            get;
            set;
        }

        public IEnumerable<string> EventAreas
        {
            get;
            set;
        }

        public IEnumerable<string> EventZones
        {
            get;
            set;
        }

        public IEnumerable<string> MobNamesToKill
        {
            get;
            set;
        }

        public string ResetEventId
        {
            get;
            set;
        }

        public bool HasHandomText
        {
            get;
            set;
        }

        public TimerType TimerType
        {
            get;
            set;
        }

        public long ChronoTime
        {
            get;
            set;
        }

        public string KillStartingGroupMobId
        {
            get;
            set;
        }

        public DateTimeOffset? ChanceLastTimeChecked
        {
            get;
            set;
        }

        public bool HasRemainingTimeText
        {
            get;
            set;
        }

        public bool IsKillingEvent
        {
            get;
            set;
        }

        public bool IsTimingEvent
        {
            get;
            set;
        }

        public bool ShowEvent
        {
            get;
            set;
        }

        public StartingConditionType StartConditionType
        {
            get;
            set;
        }


        public IEnumerable<EndingConditionType> EndingConditionTypes
        {
            get;
            set;
        }

        public int EventChance
        {
            get;
            set;
        }

        public TimeSpan? EventChanceInterval
        {
            get;
            set;
        }

        public AnnonceType AnnonceType
        {
            get;
            set;
        }

        public int Discord
        {
            get;
            set;
        }

        public InstancedConditionTypes InstancedConditionType
        {
            get;
            set;
        }

        public string AreaStartingId
        {
            get;
            set;
        }

        public string QuestStartingId
        {
            get;
            set;
        }

        public int WantedMobsCount
        {
            get;
            set;
        }

        public string DebutText
        {
            get;
            set;
        }

        public int StartEventSound { get; set; }

        public string EndActionStartEventID
        {
            get;
            set;
        }

        public string StartActionStopEventID
        {
            get;
            set;
        }

        public IEnumerable<string> RandomText
        {
            get;
            set;
        }

        public string RandomEventSound { get; set; }

        public TimeSpan? RandTextInterval
        {
            get;
            set;
        }

        public string RemainingTimeText
        {
            get;
            set;
        }

        public int RemainingTimeEvSound { get; set; }

        public TimeSpan? RemainingTimeInterval
        {
            get;
            set;
        }

        public string EndText
        {
            get;
            set;
        }

        public int EndEventSound { get; set; }

        public EventStatus Status
        {
            get;
            set;
        }

        public EndingAction EndingActionA
        {
            get;
            set;
        }


        public EndingAction EndingActionB
        {
            get;
            set;
        }

        public DateTimeOffset? EndTime
        {
            get;
            set;
        }

        public DateTimeOffset? StartedTime
        {
            get;
            set;
        }


        public DateTimeOffset? StartTriggerTime
        {
            get;
            set;
        }

        public List<GameNPC> Mobs
        {
            get;
        }

        public List<GameStaticItem> Coffres
        {
            get;
        }

        public bool ParallelLaunch
        {
            get;
            set;
        }

        public Dictionary<string, bool> EventFamily
        {
            get;
            set;
        }

        public int TimeBeforeReset
        {
            get;
            set;
        }

        public int? TPPointID { get; set; }

        public string GetFormattedDebutText(string language, GamePlayer player)
        {
            if (string.IsNullOrEmpty(DebutText))
                return string.Empty;

            return LanguageMgr.GetEventMessage(language, DebutText, player?.Name ?? string.Empty);
        }

        public string GetFormattedEndText(string language, GamePlayer player)
        {
            if (string.IsNullOrEmpty(EndText))
                return string.Empty;

            if (player == null)
                return LanguageMgr.GetEventMessage(language, EndText, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            
            string playerName = player.Name;
            string groupName = player.Group?.Leader?.Name ?? "???";
            string guildName = player.GuildName ?? "???";
            string className = player.CharacterClass?.Name ?? "???";
            string raceName = player.RaceName ?? "???";

            return LanguageMgr.GetEventMessage(language, EndText ?? string.Empty, playerName, groupName, guildName, className, raceName);
        }

        public string GetFormattedRemainingTimeText(string language, GamePlayer player)
        {
            if (string.IsNullOrEmpty(RemainingTimeText))
                return string.Empty;

            if (player == null)
                return LanguageMgr.GetEventMessage(language, RemainingTimeText);

            return LanguageMgr.GetEventMessage(language, RemainingTimeText, player.Name);
        }

        public string GetFormattedRandomText(string language, GamePlayer player)
        {
            if (RandomText?.Any() != true)
                return string.Empty;
            
            var rand = new Random(DateTime.Now.Millisecond);
            string message = RandomText.ElementAt(rand.Next(0, RandomText.Count()));
            return LanguageMgr.GetEventMessage(language, message, player?.Name);
        }

        public GamePlayer Owner
        {
            get => owner;
            set => owner = value;
        }

        public void Clean()
        {
            if (this.RandomTextTimer != null)
            {
                this.RandomTextTimer.Stop();
            }

            if (this.RemainingTimeTimer != null)
            {
                this.RemainingTimeTimer.Stop();
            }
        }

        public void SaveToDatabase()
        {
            var db = _db as EventDB;
            bool needClone = false;

            if (db == null)
            {
                db = new EventDB();
                needClone = true;
            }

            db.EventAreas = EventAreas != null ? string.Join("|", EventAreas) : null;
            db.EventChance = EventChance;
            db.EventName = EventName;
            db.EventZones = EventZones != null ? string.Join("|", EventZones) : null;
            db.ShowEvent = ShowEvent;
            db.StartConditionType = (int)StartConditionType;
            db.EndingConditionTypes = string.Join("|", EndingConditionTypes.Select(t => ((int)t).ToString()));
            db.EventChanceInterval = EventChanceInterval.HasValue ? (long)EventChanceInterval.Value.TotalMinutes : 0;
            db.DebutText = !string.IsNullOrEmpty(DebutText) ? DebutText : null;
            db.EndText = EndText;
            db.StartedTime = StartedTime?.ToUnixTimeSeconds() ?? 0;
            db.EndTime = EndTime.HasValue ? EndTime.Value.ToUnixTimeSeconds() : 0;
            db.RandomText = RandomText != null ? string.Join("|", RandomText) : null;
            db.RandTextInterval = RandTextInterval.HasValue ? (long)RandTextInterval.Value.TotalMinutes : 0;
            db.RemainingTimeInterval = RemainingTimeInterval.HasValue ? (long)RemainingTimeInterval.Value.TotalMinutes : 0;
            db.RemainingTimeText = !string.IsNullOrEmpty(RemainingTimeText) ? RemainingTimeText : null;
            db.EndingActionA = (int)EndingActionA;
            db.EndingActionB = (int)EndingActionB;
            db.StartActionStopEventID = !string.IsNullOrEmpty(StartActionStopEventID) ? StartActionStopEventID : null;
            db.EndActionStartEventID = EndActionStartEventID;
            db.MobNamesToKill = MobNamesToKill != null ? string.Join("|", MobNamesToKill) : null;
            db.Status = (int)Status;
            db.StartTriggerTime = StartTriggerTime.HasValue ? StartTriggerTime.Value.ToUnixTimeSeconds() : 0;
            db.ChronoTime = ChronoTime;
            db.TimerType = (int)this.TimerType;
            db.KillStartingGroupMobId = KillStartingGroupMobId;
            db.ResetEventId = ResetEventId;
            db.ChanceLastTimeChecked = ChanceLastTimeChecked.HasValue ? ChanceLastTimeChecked.Value.ToUnixTimeSeconds() : 0;
            db.AnnonceType = (byte)AnnonceType;
            db.Discord = Discord;
            db.InstancedConditionType = (int)InstancedConditionType;
            db.AreaStartingId = AreaStartingId;
            db.QuestStartingId = QuestStartingId;
            db.StartEventSound = StartEventSound;
            db.RandomEventSound = RandomEventSound;
            db.RemainingTimeEvSound = RemainingTimeEvSound;
            db.EndEventSound = EndEventSound;
            db.TPPointID = TPPointID;

            if (ID == null)
            {
                GameServer.Database.AddObject(db);
                ID = db.ObjectId;
            }
            else
            {
                db.ObjectId = ID;
                GameServer.Database.SaveObject(db);
            }

            if (needClone)
                _db = db.Clone();
        }
    }
}