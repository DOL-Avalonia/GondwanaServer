using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Commands;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOL.MobGroups;
using DOLDatabase.Tables;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

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
        public GameEventAreaTrigger? AreaConditions { get; init; }
        
        public GameEvent? ChainPreviousEvent { get; set; }
        public GameEvent? ChainNextEvent { get; set; }

        /**
         * @brief Event instance constructor
         */
        public GameEvent(GameEvent ev)
        {
            ParallelLaunch = ev.ParallelLaunch;
            IsInstanceMaster = false;
            _db = ev._db;
            ID = ev.ID;
            RandomTextTimer = new Timer();
            RemainingTimeTimer = new Timer();
            ResetFamilyTimer = new Timer();
            Owner = ev.Owner;

            EventAreas = ev.EventAreas;
            EventChance = ev.EventChance;
            EventName = ev.EventName;
            EventZones = ev.EventZones;
            ShowEvent = ev.ShowEvent;
            StartConditionType = ev.StartConditionType;
            EventChanceInterval = ev.EventChanceInterval;
            DebutText = ev.DebutText;
            EndTextA = ev.EndTextA;
            EndTextB = ev.EndTextB;
            EndEventSoundA = ev.EndEventSoundA;
            EndEventSoundB = ev.EndEventSoundB;
            StartedTime = (DateTimeOffset?)null;
            EndingConditionTypes = ev.EndingConditionTypes;
            RandomText = ev.RandomText;
            RandTextInterval = ev.RandTextInterval;
            RemainingTimeInterval = ev.RemainingTimeInterval;
            RemainingTimeText = ev.RemainingTimeText;
            EndingActionA = ev.EndingActionA;
            EndingActionB = ev.EndingActionB;
            MobNamesToKill = ev.MobNamesToKill;
            EndActionStartEventID = ev.EndActionStartEventID;
            StartActionStopEventID = ev.StartActionStopEventID;
            StartTriggerTime = ev.StartTriggerTime;
            TimerType = ev.TimerType;
            EndTime = (DateTimeOffset?)null;
            ChronoTime = ev.ChronoTime;
            KillStartingGroupMobId = ev.KillStartingGroupMobId;
            ResetEventId = ev.ResetEventId;
            ChanceLastTimeChecked = DateTimeOffset.FromUnixTimeSeconds(0);
            AnnonceType = ev.AnnonceType;
            SecondaryAnnonceType = ev.SecondaryAnnonceType;
            Discord = ev.Discord;
            InstancedConditionType = ev.InstancedConditionType;
            AreaStartingId = ev.AreaStartingId;
            QuestStartingId = ev.QuestStartingId;
            ParallelLaunch = ev.ParallelLaunch;
            StartEventSound = ev.StartEventSound;
            RandomEventSound = ev.RandomEventSound;
            RemainingTimeEvSound = ev.RemainingTimeEvSound;
            TPPointID = ev.TPPointID;
            _eventFamily = new(ev.EventFamily);
            TimeBeforeReset = ev.TimeBeforeReset;
            EventFamilyType = ev.EventFamilyType;
            EventFamilyOrdering = ev.EventFamilyOrdering;
            ActionCancelQuestId = ev.ActionCancelQuestId;
            if (TimeBeforeReset > 0)
            {
                ResetFamilyTimer.Interval = ((long)TimeBeforeReset) * 1000;
                ResetFamilyTimer.Elapsed += ResetFamilyTimer_Elapsed;
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
            FamilyFailText = ev.FamilyFailText;

            IsKillingEvent = ev.IsKillingEvent;
            IsTimingEvent = ev.IsTimingEvent;

            Coffres = new List<GameStaticItem>();
            foreach (var coffre in ev.Coffres)
            {
                var copy = coffre.Copy();
                copy.LoadedFromScript = true;
                Coffres.Add(copy);
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
                        newMob = (GameNPC)gasm!.CreateInstance(mobDef.ClassType, false);
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
            InstancedConditionType = ev.InstancedConditionType;
            EndTime = ev.EndTime;
            ChronoTime = ev.ChronoTime;
            if (ev.AreaConditions != null)
            {
                AreaConditions = new GameEventAreaTrigger(ev.AreaConditions);
                AreaConditions.Event = this;
            }
        }

        public GameEvent(EventDB db)
        {
            _db = db.Clone();
            ID = db.ObjectId;
            this.RandomTextTimer = new Timer();
            this.RemainingTimeTimer = new Timer();
            this.ResetFamilyTimer = new Timer();
            _eventFamily = new List<Child>();
            IsInstanceMaster = true;

            AreaXEvent? dbAreaConditions = GameServer.Database.SelectObject<AreaXEvent>(e => e.EventID == this.ID);
            if (dbAreaConditions != null)
            {
                var area = WorldMgr.GetAllRegions().Select(r => r.GetArea(dbAreaConditions.AreaID)).OfType<AbstractArea>().FirstOrDefault();
                if (area == null)
                {
                    log.Warn($"AreaXEvent for {EventName} ({ID}) has invalid area ${dbAreaConditions.AreaID}");
                }
                else
                {
                    AreaConditions = new GameEventAreaTrigger(this, dbAreaConditions, area);
                }
            }

            ParseValuesFromDb(db);

            if (IsInstancedEvent)
                Instances = new List<GameEvent>();

            this.Coffres = new List<GameStaticItem>();
            this.Mobs = new List<GameNPC>();
            this.StartEffects = new Dictionary<string, ushort>();
            this.EndEffects = new Dictionary<string, ushort>();
            RemovedMobs = new Dictionary<string, GameNPC>();
            RemovedCoffres = new Dictionary<string, GameStaticItem>();
        }

        private void _Cleanup()
        {
            try
            {
                foreach (var mob in Mobs)
                {
                    if (mob.ObjectState == GameObject.eObjectState.Active)
                    {
                        if (!mob.IsPeaceful)
                            mob.Health = 0;
                        mob.RemoveFromWorld();
                        mob.Delete();
                    }
                }

                foreach (var coffre in Coffres)
                {
                    if (coffre.ObjectState == GameObject.eObjectState.Active)
                        coffre.RemoveFromWorld();
                }
                
                if (this.RandomTextTimer != null)
                {
                    this.RandomTextTimer.Stop();
                }

                if (this.RemainingTimeTimer != null)
                {
                    this.RemainingTimeTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                log.Error($"Exception while cleaning up event {this}: {ex}");
            }

            List<GameNPC> npc;
            lock (RelatedNPCs)
            {
                npc = new List<GameNPC>(RelatedNPCs);
            }
            foreach (var mob in npc)
            {
                mob.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).Cast<GamePlayer>().ForEach(mob.RefreshEffects);
            }
            AreaConditions?.Reset();

            //Handle Interval Starting Event
            //let a chance to this event to trigger at next interval
            if (StartConditionType == StartingConditionType.Interval)
            {
                Status = EventStatus.Idle;
                StartedTime = null;
                EndTime = null;
                ChanceLastTimeChecked = (DateTimeOffset?)null;
            }
            
            if (!IsRunning)
            {
                RestoreMobs();
            }
        }
        
        private void _Reset()
        {
            var prev = ExchangeStatus(EventStatus.Ending);
            log.DebugFormat("Starting reset of event {0} (status is {1})", this, prev);
            try
            {
                StartedTime = (DateTimeOffset?)null;
                EndTime = (DateTimeOffset?)null;
                WantedMobsCount = 0;
                EventFamily.ForEach(c => c.Active = false);
                _Cleanup();
                
                if (GameEventManager.Instance.EventRelations.TryGetValue(ID, out var list))
                {
                    foreach (var ev in list)
                    {
                        if (ev.EventFamilyType == FamilyConditionType.EventRunning)
                        {
                            var instance = ev.GetInstance(Owner);
                            if (instance != null)
                                instance.OnChildReset(this);
                        }
                    }
                }
                
                if (IsInstancedEvent)
                {
                    if (IsInstanceMaster)
                    {
                        List<GameEvent> instances;
                        lock (Instances)
                        {
                            // Calling reset on instances will modify the list, so we need to copy first
                            instances = Instances.ToList();
                            Instances.Clear();
                        }
                        instances.ForEach(ev => ev.Reset());
                    }
                    else
                    {
                        bool deleteInstance = true;
                        if (AreaConditions != null)
                        {
                            AreaConditions.Reset();
                            if (AreaConditions.PlayersInArea.Count != 0)
                            {
                                deleteInstance = false;
                            }
                        }
                        if (deleteInstance)
                        {
                            var master = GameEventManager.Instance.GetEventByID(ID);
                            if (master != null)
                                master.RemoveInstance(this);
                        }
                    }
                }

                if (IsInstanceMaster && StartConditionType == StartingConditionType.Money)
                {
                    // TODO: review this, this won't work for instanced events...
                    //Reset related NPC Money
                    var moneyNpcDb = GameServer.Database.SelectObjects<MoneyNpcDb>(DB.Column("EventID").IsEqualTo(ID))?.FirstOrDefault();

                    if (moneyNpcDb != null)
                    {
                        var mob = GameServer.Database.FindObjectByKey<Mob>(moneyNpcDb.MobID);

                        if (mob != null)
                        {
                            MoneyEventNPC mobIngame = WorldMgr.Regions[mob.Region].Objects?.FirstOrDefault(o => o?.InternalID?.Equals(mob.ObjectId) == true && o is MoneyEventNPC) as MoneyEventNPC;

                            if (mobIngame != null)
                            {
                                // Reset money to 0
                                mobIngame.CurrentSilver = 0;
                                mobIngame.CurrentCopper = 0;
                                mobIngame.CurrentGold = 0;
                                mobIngame.CurrentMithril = 0;
                                mobIngame.CurrentPlatinum = 0;

                                // ALSO reset the resources to 0
                                mobIngame.CurrentResource1 = 0;
                                mobIngame.CurrentResource2 = 0;
                                mobIngame.CurrentResource3 = 0;
                                mobIngame.CurrentResource4 = 0;

                                mobIngame.SaveIntoDatabase();
                            }
                        }

                        // Reset money in the DB
                        moneyNpcDb.CurrentAmount = 0;

                        // Reset resources in the DB
                        moneyNpcDb.CurrentResource1 = 0;
                        moneyNpcDb.CurrentResource2 = 0;
                        moneyNpcDb.CurrentResource3 = 0;
                        moneyNpcDb.CurrentResource4 = 0;

                        GameServer.Database.SaveObject(moneyNpcDb);
                    }
                }

                if (!IsInstancedEvent || IsInstanceMaster)
                {
                    if (!string.IsNullOrEmpty(KillStartingGroupMobId) && MobGroupManager.Instance.Groups.TryGetValue(KillStartingGroupMobId, out MobGroup group))
                    {
                        group.NPCs.Where(npc => npc.Event is null or { IsRunning: true }).ForEach(npc => npc.Spawn());
                    }
                }

                SaveToDatabase();
            }
            catch (Exception ex)
            {
                log.Error($"Exception while resetting event {EventName} ({ID}): {ex}");
            }
            Status = EventStatus.Idle;
            log.DebugFormat("Finished reset of event {0}", this);

            if (IsInstanceMaster && AreaConditions != null)
            {
                foreach (var player in AreaConditions.Area.Players)
                {
                    GetOrCreateInstance(player)?.AreaConditions!.PlayerEntersArea(player, AreaConditions.Area);
                }
            }
        }
        
        public void RemoveInstance(GameEvent instance)
        {
            if (Instances == null)
                return;

            lock (Instances)
            {
                Instances.Remove(instance);
            }
        }

        public void Reset()
        {
            GameEvent ev = this;
            while (ev.ChainNextEvent != null)
            {
                ev = ev.ChainNextEvent;
            }
            while (ev != null)
            {
                ev._Reset();
                ev = ev.ChainPreviousEvent;
                if (ev == this)
                    return; // We have cyclic dependencies...
            }
        }

        private void ApplyEffect(GameObject item, Dictionary<string, ushort> dic)
        {
            foreach (GamePlayer pl in item.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                pl.Out.SendSpellEffectAnimation(item, item, dic[item.InternalID], 0, false, 5);
            }
        }
        
        public async Task<bool> StartEventEffects()
        {
            foreach (var mob in Mobs)
            {
                if (StartEffects.ContainsKey(mob.InternalID))
                {
                    ApplyEffect(mob, StartEffects);
                }
            }

            foreach (var coffre in Coffres)
            {
                if (StartEffects.ContainsKey(coffre.InternalID))
                {
                    ApplyEffect(coffre, StartEffects);
                }
            }

            List<GameNPC> npc;
            lock (RelatedNPCs)
            {
                npc = new List<GameNPC>(RelatedNPCs);
            }
            foreach (var mob in npc)
            {
                mob.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).Cast<GamePlayer>().ForEach(mob.RefreshEffects);
            }

            if (!string.IsNullOrEmpty(StartActionStopEventID))
            {
                var ev = GameEventManager.Instance.GetEventByID(StartActionStopEventID);
                if (ev == null)
                {
                    log.Error(string.Format("Impossible To Stop Event Id {0} from StartActionStopEventID (Event {1}). Event not found", StartActionStopEventID, this.ID));
                }
                else
                {
                    log.Info(string.Format("Stop Event Id {0} from StartActionStopEventID (Event {1})", StartActionStopEventID, this.ID));
                }
                await ev!.Stop(EndingConditionType.StartingEvent);
            }
            return true;
        }

        private async Task<bool> _Start(GamePlayer? triggerPlayer, GameEvent? starterEvent)
        {
            log.DebugFormat("Attempting to start event {0} ({1}) for player {2} (IsInstance = {3})", EventName, ID, triggerPlayer, IsInstancedEvent && !IsInstanceMaster);
            var prev = CompareExchangeStatus(EventStatus.Starting, EventStatus.Idle);
            if (prev is EventStatus.Starting or EventStatus.Started or EventStatus.Ending)
            {
                log.DebugFormat("Cannot start event {0} ({1}) for player {2} (IsInstance = {3}), status is {4}", EventName, ID, triggerPlayer, IsInstancedEvent && !IsInstanceMaster, prev);
                return false;
            }
            Owner ??= triggerPlayer;
            ChainPreviousEvent = starterEvent?.GetInstance(triggerPlayer);
            if (ChainPreviousEvent != null)
            {
                ChainPreviousEvent.ChainNextEvent = this;
            }
            try
            {
                if (GameEventManager.Instance.EventRelations.TryGetValue(ID, out var list) && list.Count > 0)
                {
                    bool success = true;
                    foreach (var ev in list)
                    {
                        if (ev.EventFamilyType is FamilyConditionType.EventStarted or FamilyConditionType.EventRunning)
                        {
                            if (IsInstancedEvent && !ev.IsInstancedEvent && ev.OrderedFamily)
                            {
                                log.Warn($"Event {ev} parent of an ordered family event is not instanced, but its child {this} is instanced, this will behave in very unintuitive ways!");
                            }
                            var instance = ev.GetOrCreateInstance(triggerPlayer);
                            success = success && (instance?.OnChildStart(this) == true);
                        }
                    }
                    if (!success)
                    {
                        _Reset();
                        return false;
                    }
                }
                
                //temporarly disable
                var eventMaster = GameEventManager.Instance.GetEventByID(ID) ?? this;
                eventMaster.DisableMobs();

                if (StartEventSetup())
                {
                    if (StartEventSound > 0)
                    {
                        foreach (var player in GetPlayersInEventZones(EventZones))
                        {
                            player.Out.SendSoundEffect((ushort)StartEventSound, player.Position, 0);
                        }
                    }

                    //need give more time to client after addtoworld to perform animation
                    await Task.Delay(500);

                    if (TryExchangeStatus(EventStatus.Started, EventStatus.Starting))
                    {
                        await Task.Run(() => StartEventEffects());
                        
                        log.DebugFormat("Event {0} started by {1}", this, triggerPlayer == null ? "server" : triggerPlayer);
                        SaveToDatabase();
                        return true;
                    }
                }
                else
                {
                    log.Warn($"Event startup of {EventName} ({ID}) was aborted: status changed");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Exception while trying to start event {EventName} ({ID}): {ex}");
            }
            Reset();
            return false;
        }
        
        private void DisableMobs()
        {
            var disabledMobs = GameServer.Database.SelectObjects<Mob>(DB.Column("RemovedByEventID").IsNotNull());
            foreach (var mob in disabledMobs)
            {
                if (mob.RemovedByEventID.Split("|").Contains(ID.ToString()))
                {
                    var mobInRegion = WorldMgr.Regions[mob.Region].Objects.FirstOrDefault(o => o != null && o is GameNPC npc && npc.InternalID != null && npc.InternalID.Equals(mob.ObjectId));
                    if (mobInRegion != null)
                    {
                        var npcInRegion = mobInRegion as GameNPC;
                        //copy npc
                        RemovedMobs[npcInRegion!.InternalID] = npcInRegion;
                        npcInRegion.RemoveFromWorld();
                        npcInRegion.Delete();
                    }
                }
            }
            var disabledCoffres = GameServer.Database.SelectObjects<DBCoffre>(DB.Column("RemovedByEventID").IsNotNull());
            foreach (var coffre in disabledCoffres)
            {
                if (coffre.RemovedByEventID.Split("|").Contains(ID.ToString()))
                {
                    var coffreInRegion = WorldMgr.Regions[coffre.Region].Objects.FirstOrDefault(o => o != null && o is GameStaticItem item && item.InternalID.Equals(coffre.ObjectId)) as GameStaticItem;
                    if (coffreInRegion != null)
                    {
                        var itemInRegion = coffreInRegion as GameStaticItem;
                        RemovedCoffres[itemInRegion.InternalID] = itemInRegion;
                        itemInRegion.RemoveFromWorld();
                        itemInRegion.Delete();
                    }
                }
            }
        }

        private void RestoreMobs()
        {
            //restore temporarly disabled RemovedMobs
            foreach (var mob in RemovedMobs)
            {
                mob.Value.InternalID = mob.Key;
                mob.Value.AddToWorld();
            }
            RemovedMobs.Clear();

            foreach (var item in RemovedCoffres)
            {
                item.Value.InternalID = item.Key;
                item.Value.AddToWorld();
            }
            RemovedCoffres.Clear();
        }

        public async Task<bool> StartInstances(GamePlayer? triggerPlayer = null, GameEvent? previousEvent = null)
        {
            if (!TryExchangeStatus(EventStatus.Starting, EventStatus.Idle))
            {
                return false;
            }
            try
            {
                var condition = InstancedConditionType;
                if (condition == InstancedConditionTypes.All)
                    condition = InstancedConditionTypes.Player; // This doesn't seem right to me, but this is what was happening before

                // Keep track of instances we spawn
                Dictionary<object, GamePlayer> playersRegistered = new();
                var key = GetOwnerKey(triggerPlayer);
                if (key != null) 
                    playersRegistered.TryAdd(key, triggerPlayer);
                // Register all players in the area, TODO: There has to be a better way to do this?
                foreach (var player in WorldMgr.GetAllPlayingClients().Select(c => c.Player).Where(p => p.CurrentAreas.OfType<AbstractArea>().Any(a => a.DbArea.ObjectId == AreaStartingId))) // 
                {
                    key = GetOwnerKey(player);
                    if (key != null)
                        playersRegistered.TryAdd(key, player);
                }

                // Unregister players with running instances, prepare to start existing ready instances
                List<GameEvent> startingExistingInstances = new();
                lock (Instances)
                {
                    foreach (var i in Instances)
                    {
                        key = GetOwnerKey(i.Owner);
                        if (key == null)
                            continue;
                        
                        playersRegistered.Remove(key);
                        if (i.IsReady)
                            startingExistingInstances.Add(i);
                    }
                }

                if (playersRegistered.Count > 0 || startingExistingInstances.Count > 0)
                {
                    List<GameEvent> spawnedInstances = new();
                    if (ParallelLaunch == EventLaunchType.InstancedStartAll)
                    {
                        // Spawn an instance for every registered player
                        foreach (GamePlayer pl in playersRegistered.Values)
                        {
                            try
                            {
                                var instance = Instantiate(pl);
                                if (instance != null)
                                    spawnedInstances.Add(instance);
                            }
                            catch (Exception ex)
                            {
                                log.Error($"Could not instantiate event {this.EventName} ({ID}) for player {pl}: {ex}");
                            }
                        }
                    }

                    List<(GameEvent ev, Task<bool> task)> allTasks = new();
                    foreach (GameEvent e in startingExistingInstances.Concat(spawnedInstances))
                    {
                        // Start all events...
                        allTasks.Add((e, e._Start(triggerPlayer, previousEvent)));
                    }
                    foreach (var entry in allTasks)
                    {
                        try
                        {
                            // Await all startups
                            await entry.task;
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Could not start event {this}: {ex}");
                            entry.ev.Reset();
                        }
                    }
                    if (ParallelLaunch == EventLaunchType.InstancedStartAll)
                    {
                        lock (Instances)
                        {
                            Instances.AddRange(spawnedInstances.Where(i => i.IsRunning));
                        }
                    }
                }
                Status = EventStatus.Idle;
            }
            catch (Exception ex)
            {
                log.Error($"Exception while starting event {this.EventName} ({ID}): {ex}");
                Reset();
                return false;
            }
            return true;
        }

        public async Task<bool> Start(GamePlayer? triggerPlayer = null, GameEvent? previousEvent = null)
        {
            if (IsInstancedEvent)
            {
                if (IsInstanceMultiStart)
                {
                    GameEvent master = IsInstanceMaster ? this : GameEventManager.Instance.GetEventByID(this.ID);
                    if (master != null)
                        return await StartInstances(triggerPlayer, previousEvent);
                }
                if (IsInstanceMaster)
                {
                    GameEvent instance = GetOrCreateInstance(triggerPlayer);
                    if (instance == null)
                    {
                        return false;
                    }
                    return await instance._Start(triggerPlayer, previousEvent);
                }
            }
            return await _Start(triggerPlayer, previousEvent);
        }

        private async Task ShowEndEffects()
        {
            foreach (var mob in Mobs)
            {
                if (EndEffects.ContainsKey(mob.InternalID))
                {
                    this.ApplyEffect(mob, EndEffects);
                }
            }

            foreach (var coffre in Coffres)
            {
                if (EndEffects.ContainsKey(coffre.InternalID))
                {
                    this.ApplyEffect(coffre, EndEffects);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        private async Task StartChainedEvent(string evID)
        {
            var ev = evID == this.ID ? this : GameEventManager.Instance.GetEventByID(evID)?.GetOrCreateInstance(Owner);
            if (ev == null)
            {
                log.Warn($"Cannot perform ending action {EndingAction.Event}: no event with ID {evID} was found");
                return;
            }

            ev.OnRequestStart(this);
        }

        private bool OnRequestStart(GameEvent gameEvent)
        {
            if (EventFamily.Count == 0)
            {
                Task.Run(() => Start(Owner));
                return true;
            }

            bool success;
            if (OrderedFamily)
            {
                success = ActivateChildOrdered(gameEvent);
                if (!success)
                {
                    OnBadFamilyOrder();
                }
            }
            else
                success = ActivateChildUnordered(gameEvent);

            if (success)
                return CheckFamily();
            return false;
        }

        private bool OnChildStart(GameEvent gameEvent)
        {
            if (EventFamily.Count == 0)
                return true;
            
            bool success;
            if (EventFamilyType is FamilyConditionType.EventStarted or FamilyConditionType.EventRunning)
            {
                if (OrderedFamily)
                {
                    success = ActivateChildOrdered(gameEvent);
                    if (!success)
                    {
                        OnBadFamilyOrder();
                        if (IsFamilyOrderEnforced)
                            return false;  // prevent starting
                    }
                }
                else
                    success = ActivateChildUnordered(gameEvent);

                if (success)
                    CheckFamily();
            }
            return true;
        }

        private void OnChildReset(GameEvent gameEvent)
        {
            if (EventFamily.Count == 0)
                return;

            if (EventFamilyType is FamilyConditionType.EventRunning)
            {
                var child = EventFamily.FirstOrDefault(c => c.EventID == gameEvent.ID);
                if (child != null)
                {
                    if (child.Active && OrderedFamily)
                    {
                        EventFamily.ForEach(c => c.Active = false);
                    }
                    child.Active = false;
                }
            }
        }

        private void OnChildEnd(GameEvent gameEvent)
        {
            if (IsRunning || EventFamily.Count == 0)
                return;
            
            if (EventFamilyType is FamilyConditionType.EventEnded)
            {
                bool success;
                if (OrderedFamily)
                {
                    success = ActivateChildOrdered(gameEvent);
                    if (!success)
                    {
                        OnBadFamilyOrder();
                        return;
                    }
                }
                else
                    success = ActivateChildUnordered(gameEvent);
                if (success)
                    CheckFamily();
            }
            else if (EventFamilyType is FamilyConditionType.EventRunning)
            {
                var child = EventFamily.FirstOrDefault(c => c.EventID == gameEvent.ID);
                if (child != null)
                {
                    if (child.Active && IsFamilyOrderEnforced)
                    {
                        ResetChildren();
                    }
                }
            }
        }

        private bool ActivateChildOrdered(GameEvent childEvent)
        {
            var child = EventFamily.FirstOrDefault(c => c.Active == false);
            if (child == null)
                return false;

            if (child.EventID == childEvent.ID)
            {
                child.Active = true;
                return true;
            }
            return false;
        }

        private void OnBadFamilyOrder()
        {
            if (!string.IsNullOrEmpty(FamilyFailText) && EventFamily.Any(c => c.Active))
            {
                foreach (var cl in GetPlayersInEventZones(this.EventZones))
                {
                    ChatUtil.SendImportant(cl, FamilyFailText);
                }
            }
            switch (EventFamilyOrdering)
            {
                case FamilyOrdering.Unordered:
                case FamilyOrdering.Soft:
                case FamilyOrdering.Strict:
                    break;
                
                case FamilyOrdering.Hidden:
                    EventFamily.ForEach(c => c.Active = false);
                    break;

                case FamilyOrdering.Reset:
                    EventFamily.ForEach(c => c.Active = false);
                    ResetChildren();
                    break;

                case FamilyOrdering.Stop:
                    EventFamily.ForEach(c => c.Active = false);
                    StopChildren();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        

        private bool ActivateChildUnordered(GameEvent childEvent)
        {
            var child = GetFamilyChild(childEvent.ID);
            if (child != null)
            {
                child.Active = true;
                return true;
            }
            return false;
        }

        private bool CheckFamily()
        {
            bool ok = EventFamily.All(c => c.Active == true);
            if (ok)
            {
                if (TimeBeforeReset > 0)
                {
                    ResetFamilyTimer.Stop();
                }
                Task.Run(() => Start(Owner));
                return true;
            }
            else
            {
                if (TimeBeforeReset > 0 && !ResetFamilyTimer.Enabled)
                {
                    ResetFamilyTimer.Start();
                }
                return false;
            }
        }
        
        private Child? GetFamilyChild(string id)
        {
            int idx = _eventFamily.FindIndex((child) => child.EventID == id);
            return idx < 0 ? null : EventFamily[idx];
        }

        private async Task ResetOtherEvent(string evID)
        {
            var resetEvent = GameEventManager.Instance.GetEventByID(evID);
            if (resetEvent == null)
            {
                log.Error("Impossible to reset Event from resetEventId : cannot find event " + evID);
                return;
            }

            if (resetEvent.TimerType == TimerType.DateType && resetEvent.EndingConditionTypes.Contains(EndingConditionType.Timer) && resetEvent.EndingConditionTypes.Count() == 1)
            {
                // Why is this an error?
                log.Error(string.Format("Cannot Reset Event {0}, Name: {1} with DateType with only Timer as Ending condition", resetEvent.ID, resetEvent.EventName));
            }
            else
            {
                GameEventManager.Instance.ResetEventsFromId(ResetEventId);
            }
        }

        private async Task TeleportPlayers(int tpPointId)
        {
            IList<DBTPPoint> tpPoints = GameServer.Database.SelectObjects<DBTPPoint>(DB.Column("TPID").IsEqualTo(tpPointId));
            DBTP dbtp = GameServer.Database.SelectObjects<DBTP>(DB.Column("TPID").IsEqualTo(tpPointId)).FirstOrDefault();

            if (tpPoints != null && tpPoints.Count > 0 && dbtp != null)
            {
                TPPoint tpPoint = null;
                switch ((eTPPointType)dbtp.TPType)
                {
                    case eTPPointType.Loop:
                        tpPoint = GameEventManager.Instance.GetLoopNextTPPoint(dbtp.TPID, tpPoints);
                        break;

                    case eTPPointType.Random:
                        tpPoint = GameEventManager.Instance.GetRandomTPPoint(tpPoints);
                        break;

                    case eTPPointType.Smart:
                        tpPoint = GameEventManager.Instance.GetSmartNextTPPoint(tpPoints);
                        break;
                }

                if (tpPoint != null)
                {
                    foreach (var cl in GetPlayersInEventZones(this.EventZones))
                    {
                        cl.MoveTo(Position.Create(tpPoint.Region, tpPoint.Position.X, tpPoint.Position.Y, tpPoint.Position.Z));
                    }
                }
            }
        }

        private async Task PerformEndAction(EndingAction action)
        {
            switch (action)
            {
                case EndingAction.None:
                    return;
                
                case EndingAction.BindStone:
                    foreach (var pl in GetPlayersInEventZones(EventZones))
                        pl.MoveToBind();
                    return;
                
                case EndingAction.Event:
                    await StartChainedEvent(EndActionStartEventID);
                    return;
                
                case EndingAction.Reset:
                    if (string.IsNullOrEmpty(ResetEventId))
                        log.WarnFormat("Event {0} has ending action Reset but no ResetEventId", this);
                    else
                        await ResetOtherEvent(ResetEventId);
                    return;

                case EndingAction.JumpToTPPoint:
                    if (!TPPointID.HasValue)
                    {
                        log.Error($"Event {this} has JumpToTPPoint action but no TPPointID is set.");
                    }
                    else
                    {
                        await TeleportPlayers(TPPointID.Value);
                    }
                    return;
                
                case EndingAction.CancelQuest:
                    if (ActionCancelQuestId == 0)
                    {
                        log.Error($"Event {this} has CancelQuest action but no ActionCancelQuestId is set.");
                    }
                    else
                    {
                        CancelQuest(ActionCancelQuestId);
                    }
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private IEnumerable<GamePlayer> GetAllPlayersInvolved()
        {
            switch (InstancedConditionType)
            {
                case InstancedConditionTypes.All:
                    return GetPlayersInEventZones(this.EventZones);

                case InstancedConditionTypes.Player:
                    return Owner != null ? new GamePlayer[] { Owner } : Enumerable.Empty<GamePlayer>();

                case InstancedConditionTypes.Group:
                    return Owner?.Group.GetPlayersInTheGroup() ?? Enumerable.Empty<GamePlayer>();

                case InstancedConditionTypes.Guild:
                    // TODO: how to handle offline players, for example with CancelQuest?
                    return Owner?.Guild?.IsSystemGuild == false ? Owner.Guild.GetListOfOnlineMembers() : Enumerable.Empty<GamePlayer>();

                case InstancedConditionTypes.Battlegroup:
                    return Owner?.BattleGroup?.GetPlayersInTheBattleGroup() ?? Enumerable.Empty<GamePlayer>();

                case InstancedConditionTypes.GroupOrSolo:
                    return Owner != null ? (Owner.Group?.GetPlayersInTheGroup() ?? new GamePlayer[] { Owner }) : Enumerable.Empty<GamePlayer>();

                case InstancedConditionTypes.GuildOrSolo:
                    return Owner != null ? (Owner?.Guild?.IsSystemGuild == false ? Owner.Guild.GetListOfOnlineMembers() : new GamePlayer[] { Owner }) : Enumerable.Empty<GamePlayer>();

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void CancelQuest(int actionCancelQuestId)
        {
            GetAllPlayersInvolved().ForEach(p => p.QuestList.FirstOrDefault(q => q.QuestId == actionCancelQuestId)?.AbortQuest());
        }

        public async Task Stop(EndingConditionType end, bool silent = false)
        {
            var prev = CompareExchangeStatus(EventStatus.Ending, EventStatus.Started);
            if (prev != EventStatus.Started)
            {
                prev = CompareExchangeStatus(EventStatus.Ending, EventStatus.Starting);
                if (prev != EventStatus.Starting)
                    return;
            }
            log.DebugFormat("Attempting to end event {0}, was {1}", this, prev);
            try
            {
                // Disable mob respawns
                this.Mobs.ForEach(npc => npc.StopRespawn());
                EndTime = DateTimeOffset.UtcNow;
                var (endText, endSound) = GetEndingTextAndSound(end);

                if (!silent)
                {
                    if (!string.IsNullOrEmpty(endText) && EventZones?.Any() == true)
                    {
                        SendEventNotification((string lang) => FormatEventMessage(GetFormattedEndText(lang, Owner, endText)), (Discord == 2 || Discord == 3), true);
                    }

                    if (endSound > 0 && EventZones?.Any() == true)
                    {
                        foreach (var player in GetPlayersInEventZones(EventZones))
                        {
                            player.Out.SendSoundEffect((ushort)endSound, player.Position, 0);
                        }
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }

                if (end == EndingConditionType.Kill && IsKillingEvent)
                {
                    Status = EventStatus.EndedByKill;
                    //Allow time to loot
                    await Task.Delay(TimeSpan.FromSeconds(15));
                    await ShowEndEffects();
                    _Cleanup();
                }
                else if (end == EndingConditionType.StartingEvent)
                {
                    Status = EventStatus.EndedByEventStarting;
                    await ShowEndEffects();
                    _Cleanup();
                }
                else if (end == EndingConditionType.Timer)
                {
                    Status = EventStatus.EndedByTimer;
                    await ShowEndEffects();
                    _Cleanup();
                }
                else if (end == EndingConditionType.AreaEvent)
                {
                    Status = EventStatus.EndedByAreaEvent;
                    await ShowEndEffects();
                    _Cleanup();
                }
                else if (end == EndingConditionType.TextNPC)
                {
                    Status = EventStatus.EndedByTextNPC;
                    await ShowEndEffects();
                    _Cleanup();
                }
                else if (end == EndingConditionType.Switch)
                {
                    Status = EventStatus.EndedBySwitch;
                    await ShowEndEffects();
                    _Cleanup();
                }
                
                if (GameEventManager.Instance.EventRelations.TryGetValue(ID, out var list))
                {
                    foreach (var ev in list)
                    {
                        if (ev.EventFamilyType is  FamilyConditionType.EventRunning)
                        {
                            var instance = ev.GetInstance(Owner);
                            instance.OnChildEnd(this);
                        }
                        else if (ev.EventFamilyType is FamilyConditionType.EventEnded)
                        {
                            var instance = ev.GetOrCreateInstance(Owner);
                            instance.OnChildEnd(this);
                        }
                    }
                }

                //Handle Consequences
                //Consequence A
                if (EndingConditionTypes.Count() == 1 || (EndingConditionTypes.Count() > 1 && EndingConditionTypes.First() == end))
                {
                    await PerformEndAction(EndingActionA);
                }
                else
                {
                    //Consequence B
                    await PerformEndAction(EndingActionB);
                }

                log.Info(string.Format("Event Id: {0}, Name: {1} was stopped At: {2}", ID, EventName, DateTime.Now.ToString()));

                SaveToDatabase();
                
                if (IsInstancedEvent && ChainNextEvent != null)
                {
                    var master = GameEventManager.Instance.GetEventByID(ID);
                    if (master != null)
                        master.RemoveInstance(this);
                }
            }
            finally
            {
                Status = EventStatus.Idle;

                //Handle Interval Starting Event
                //let a chance to this event to trigger at next interval
                if (StartConditionType == StartingConditionType.Interval)
                {
                    Status = EventStatus.Idle;
                    StartedTime = null;
                    EndTime = null;
                    ChanceLastTimeChecked = (DateTimeOffset?)null;
                }
            }
            log.DebugFormat("Finished event {0}", this);
        }

        private (string text, int sound) GetEndingTextAndSound(EndingConditionType triggeredEnd)
        {
            var conds = EndingConditionTypes.ToList();
            bool single = (conds.Count == 1);

            if (single)
            {
                return (EndTextA, EndEventSoundA);
            }

            if (triggeredEnd == conds.First())
            {
                return (EndTextA, EndEventSoundA);
            }
            else
            {
                return (EndTextB, EndEventSoundB);
            }
        }

        public void SendEventNotification(Func<string, string> message, bool sendDiscord, bool createNews = false)
        {
            string lang = Properties.SERV_LANGUAGE!;
            string msg = message(lang) ?? string.Empty;
            Dictionary<string, string> cachedMessages = new Dictionary<string, string>(8)
            {
                { lang, msg }
            };
            
            if (!String.IsNullOrEmpty(msg))
            {
                if (Properties.DISCORD_ACTIVE && sendDiscord)
                {
                    var hook = new DolWebHook(Properties.DISCORD_WEBHOOK_ID);
                    hook.SendMessage(msg);
                }
                if (createNews)
                {
                    foreach (string l in LanguageMgr.GetAllSupportedLanguages())
                    {
                        if (!cachedMessages.ContainsKey(l))
                            cachedMessages[l] = message(l);
                    }
                    NewsMgr.CreateNews(cachedMessages, 0, eNewsType.RvRLocal, false);
                }
            }
            foreach (var player in GetPlayersInEventZones(EventZones))
            {
                lang = player?.Client?.Account?.Language ?? Properties.SERV_LANGUAGE!;
                if (!cachedMessages.TryGetValue(lang, out msg))
                {
                    msg = message(lang);
                    if (string.IsNullOrEmpty(msg))
                        msg = message(Properties.SERV_LANGUAGE);
                    cachedMessages[lang] = msg;
                }
                NotifyPlayer(player, AnnonceType, msg);
            }
        }
        
        public static void NotifyPlayer(GamePlayer player, AnnonceType annonceType, string message)
        {
            eChatType type;
            eChatLoc loc;

            switch (annonceType)
            {
                case AnnonceType.Log:
                    type = eChatType.CT_Merchant;
                    loc = eChatLoc.CL_SystemWindow;
                    break;

                case AnnonceType.Send:
                    type = eChatType.CT_Send;
                    loc = eChatLoc.CL_SystemWindow;
                    break;

                case AnnonceType.Windowed:
                    type = eChatType.CT_System;
                    loc = eChatLoc.CL_PopupWindow;
                    break;

                default:
                    type = eChatType.CT_ScreenCenter;
                    loc = eChatLoc.CL_SystemWindow;
                    break;
            }
            
            if (annonceType == AnnonceType.Confirm)
            {
                player.Out.SendDialogBox(eDialogCode.CustomDialog, 0, 0, 0, 0, eDialogType.Ok, true, message);
            }
            else
            {
                player.Out.SendMessage(message, type, loc);
            }
        }

        public static void NotifyPlayerSecondary(GamePlayer player, SecondaryAnnonceType secondType, string message)
        {
            eChatType type;
            eChatLoc loc;

            switch (secondType)
            {
                case SecondaryAnnonceType.Log:
                    type = eChatType.CT_Merchant;
                    loc = eChatLoc.CL_SystemWindow;
                    break;

                case SecondaryAnnonceType.Send:
                    type = eChatType.CT_Send;
                    loc = eChatLoc.CL_SystemWindow;
                    break;

                default:
                    type = eChatType.CT_ScreenCenter;
                    loc = eChatLoc.CL_SystemWindow;
                    break;
            }
            player.Out.SendMessage(message, type, loc);
        }

        public GameEvent Instantiate(GamePlayer owner)
        {
            log.DebugFormat("Instantiating event {0} {1} for {2}", EventName, ID, owner?.Name ?? "(no owner)");

            GamePlayer? trueOwner = InstancedConditionType switch
            {
                InstancedConditionTypes.All => owner,
                InstancedConditionTypes.Player => owner,
                InstancedConditionTypes.Group => owner!.Group?.Leader,
                InstancedConditionTypes.Guild => owner!.Guild == null ? null : owner,
                InstancedConditionTypes.Battlegroup => owner!.BattleGroup == null ? null : owner,
                InstancedConditionTypes.GroupOrSolo => owner!.Group?.Leader ?? owner,
                InstancedConditionTypes.GuildOrSolo => owner,
                _ => throw new ArgumentOutOfRangeException()
            };
            if (trueOwner == null)
                return null;
            GameEvent ret = new GameEvent(this);
            ret.Owner = trueOwner;
            return ret;
        }
        
        private bool StartEventSetup()
        {
            WantedMobsCount = 0;
            if (EndingConditionTypes?.Contains(EndingConditionType.Timer) == true)
            {
                if (TimerType == TimerType.ChronoType)
                {
                    EndTime = DateTimeOffset.UtcNow.AddMinutes(ChronoTime);
                }
                else
                {
                    //Cannot launch event if Endate is not set in DateType and no other ending exists
                    if (!EndTime.HasValue)
                    {
                        if (EndingConditionTypes.Count() == 1)
                        {
                            log.Error(string.Format("Cannot Launch Event {0}, Name: {1} with DateType because EndDate is Null", ID, EventName));
                            return false;
                        }
                        else
                        {
                            log.Warn(string.Format("Event Id: {0}, Name: {1}, started with ending type Timer DateType but Endate is Null, Event Started with other endings", ID, EventName));
                        }
                    }
                }
            }

            foreach (var mob in Mobs)
            {
                mob.Health = mob.MaxHealth;
                mob.Event = this;
                var db = GameServer.Database.FindObjectByKey<Mob>(mob.InternalID);
                mob.LoadFromDatabase(db);

                var dbGroupMob = GameServer.Database.SelectObjects<GroupMobXMobs>(DB.Column("MobID").IsEqualTo(mob.InternalID))?.FirstOrDefault();

                if (dbGroupMob != null)
                {
                    MobGroup mobGroup;
                    if (MobGroupManager.Instance.Groups.TryGetValue(dbGroupMob.GroupId, out mobGroup))
                    {
                        mob.AddToMobGroup(mobGroup);
                        if (!mobGroup.NPCs.Contains(mob))
                        {
                            mobGroup.AddMob(mob, true);
                            mobGroup.ApplyGroupInfos();
                        }
                    }
                    else
                    {
                        var mobgroupDb = GameServer.Database.FindObjectByKey<GroupMobDb>(dbGroupMob.GroupId);
                        if (mobgroupDb != null)
                        {
                            var groupInteraction = mobgroupDb.GroupMobInteract_FK_Id != null ?
                            GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(mobgroupDb.GroupMobInteract_FK_Id))?.FirstOrDefault() : null;

                            var groupOriginStatus = mobgroupDb.GroupMobOrigin_FK_Id != null ?
                            GameServer.Database.SelectObjects<GroupMobStatusDb>(DB.Column("GroupStatusId").IsEqualTo(mobgroupDb.GroupMobOrigin_FK_Id))?.FirstOrDefault() : null;
                            mobGroup = new MobGroup(mobgroupDb, groupInteraction, groupOriginStatus);
                            MobGroupManager.Instance.Groups.Add(dbGroupMob.MobID, mobGroup);
                            mobGroup.AddMob(mob, true);
                            mob.AddToMobGroup(mobGroup);
                            mobGroup.ApplyGroupInfos();
                        }
                    }
                }

                if (IsKillingEvent && MobNamesToKill.Contains(mob.Name))
                {
                    WantedMobsCount++;
                }
            }

            if (IsKillingEvent)
            {
                int delta = MobNamesToKill.Count() - WantedMobsCount;

                if (WantedMobsCount == 0 && EndingConditionTypes.Where(ed => ed != EndingConditionType.Kill).Count() == 0)
                {
                    log.Error(string.Format("Event ID: {0}, Name: {1}, cannot be start because No Mobs found for Killing Type ending and no other ending type set", ID, EventName));
                    return false;
                }
                else if (delta > 0)
                {
                    log.Error(string.Format("Event ID: {0}, Name {1}: with Kill type has {2} mobs missings, MobNamesToKill column in datatabase and tagged mobs Name should match.", ID, EventName, delta));
                }
            }


            if (DebutText != null && EventZones?.Any() == true)
            {
                SendEventNotification((lang) => FormatEventMessage(GetFormattedDebutText(lang, Owner)), (Discord == 1 || Discord == 3), true);
            }

            if (HasHandomText)
            {
                RandomTextTimer.Start();
            }

            if (HasRemainingTimeText)
            {
                RemainingTimeTimer.Start();
            }

            foreach (var mob in Mobs)
            {
                mob.AddToWorld();
            }

            Coffres.ForEach(c => c.AddToWorld());
            StartedTime = DateTimeOffset.UtcNow;
            return true;
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
            EndTextA = !string.IsNullOrEmpty(db.EndTextA) ? db.EndTextA : null;
            EndTextB = !string.IsNullOrEmpty(db.EndTextB) ? db.EndTextB : null;
            EndEventSoundA = db.EndEventSoundA;
            EndEventSoundB = db.EndEventSoundB;
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
            ChanceLastTimeChecked = DateTimeOffset.FromUnixTimeSeconds(0);
            AnnonceType = Enum.TryParse(db.AnnonceType.ToString(), out AnnonceType a) ? a : AnnonceType.Center;
            SecondaryAnnonceType = (SecondaryAnnonceType)db.SecondaryAnnonceType;
            Discord = db.Discord;
            InstancedConditionType = Enum.TryParse(db.InstancedConditionType.ToString(), out InstancedConditionTypes inst) ? inst : InstancedConditionTypes.All;
            AreaStartingId = !string.IsNullOrEmpty(db.AreaStartingId) ? db.AreaStartingId : null;
            QuestStartingId = !string.IsNullOrEmpty(db.QuestStartingId) ? db.QuestStartingId : null;
            ParallelLaunch = (EventLaunchType)db.ParallelLaunch;
            StartEventSound = db.StartEventSound;
            RandomEventSound = db.RandomEventSound;
            RemainingTimeEvSound = db.RemainingTimeEvSound;
            TPPointID = db.TPPointID;
            ActionCancelQuestId = db.ActionCancelQuestId;
            EventFamilyType = (FamilyConditionType)db.EventFamilyType;
            EventFamilyOrdering = (FamilyOrdering)db.EventFamilyOrdering;
            FamilyFailText = db.FamilyFailText;

            // get kes from string[] db.EventFamily, and set values to false 
            if (db.EventFamily != null)
                foreach (string family in db.EventFamily.Split('|'))
                    _eventFamily.Add(new Child { Active = false, EventID = family });
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
        
        public string FamilyFailText
        {
            get;
            set;
        }

        public int ActionCancelQuestId
        {
            get;
            set;
        }

        public bool IsOwnedBy(GamePlayer player)
        {
            if (IsInstancedEvent && IsInstanceMaster)
                return false; // Noone owns the root event, players own specific instances
            
            switch (InstancedConditionType)
            {
                case InstancedConditionTypes.All:
                    return true;

                case InstancedConditionTypes.Player:
                    return Owner == player;

                case InstancedConditionTypes.Group:
                    return player.Group != null && player.Group.Leader == Owner;

                case InstancedConditionTypes.GroupOrSolo:
                    return player.Group == null ? player == Owner : player.Group.Leader == Owner;

                case InstancedConditionTypes.Guild:
                    return player.Guild?.IsSystemGuild == false && player.Guild == Owner.Guild;

                case InstancedConditionTypes.GuildOrSolo:
                    return player.Guild?.IsSystemGuild != false ? player == Owner : player.Guild == Owner.Guild;

                case InstancedConditionTypes.Battlegroup:
                    return Owner?.BattleGroup != null ? player.BattleGroup == Owner.BattleGroup : player == Owner;

                default:
                    throw new ArgumentOutOfRangeException();
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

            if (obj is GamePlayer { Client.Account.PrivLevel: > 1 })
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

        public IEnumerable<GamePlayer> GetPlayersInEventZones(IEnumerable<string> eventZones)
        {
            IEnumerable<GamePlayer> enumerable;

            var condition = InstancedConditionType;
            if (!IsInstancedEvent)
                condition = InstancedConditionTypes.All;
            
            switch (condition)
            {
                case InstancedConditionTypes.All:
                    enumerable = WorldMgr.GetAllPlayingClients().Select(c => c.Player);
                    break;

                case InstancedConditionTypes.Player:
                    enumerable = Owner == null ? Enumerable.Empty<GamePlayer>() : new GamePlayer[] { Owner };
                    break;

                case InstancedConditionTypes.Group:
                    enumerable = Owner == null ? Enumerable.Empty<GamePlayer>() : Owner?.Group?.GetPlayersInTheGroup() ?? Enumerable.Empty<GamePlayer>();
                    break;
                
                case InstancedConditionTypes.Guild:
                    enumerable = Owner == null ? Enumerable.Empty<GamePlayer>() : Owner?.Guild?.GetListOfOnlineMembers() ?? Enumerable.Empty<GamePlayer>();
                    break;

                case InstancedConditionTypes.Battlegroup:
                    enumerable = Owner == null ? Enumerable.Empty<GamePlayer>() : Owner?.BattleGroup?.Members.Values.OfType<GamePlayer>() ?? Enumerable.Empty<GamePlayer>();
                    break;
                case InstancedConditionTypes.GroupOrSolo:
                    enumerable = Owner == null ? Enumerable.Empty<GamePlayer>() : Owner?.Group?.GetPlayersInTheGroup() ?? new GamePlayer[] { Owner };
                    break;
                
                case InstancedConditionTypes.GuildOrSolo:
                    enumerable = Owner == null ? Enumerable.Empty<GamePlayer>() : Owner?.Guild?.GetListOfOnlineMembers() ?? new GamePlayer[] { Owner };
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return enumerable
                .Where(p => eventZones.Contains(p.CurrentZone.ID.ToString()));
        }

        private void RemainingTimeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var player in GetPlayersInEventZones(this.EventZones))
            {
                string message = this.GetFormattedRemainingTimeText(player.Client.Account.Language, player);
                NotifyPlayerSecondary(player, this.SecondaryAnnonceType, message);
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
                NotifyPlayerSecondary(player, this.SecondaryAnnonceType, message);
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
            ResetChildren();
        }

        public void StopChildren()
        {
            ResetFamilyTimer.Stop();
            foreach (var child in EventFamily)
            {
                GameEvent ev = GameEventManager.Instance.GetEventByID(child.EventID).GetInstance(Owner);
                if (ev != null)
                    Task.Run(() => ev.Stop(EndingConditionType.Family));
                child.Active = false;
            }
        }

        public void ResetChildren()
        {
            ResetFamilyTimer.Stop();
            foreach (var child in EventFamily)
            {
                GameEvent ev = GameEventManager.Instance.GetEventByID(child.EventID).GetInstance(Owner);
                if (ev != null)
                    ev.Reset();
                child.Active = false;
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
        public SecondaryAnnonceType SecondaryAnnonceType
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

        public string EndTextA
        {
            get;
            set;
        }

        public string EndTextB
        {
            get;
            set;
        }

        public int EndEventSoundA { get; set; }

        public int EndEventSoundB { get; set; }

        private int _status = (int)EventStatus.Idle;

        private EventStatus CompareExchangeStatus(EventStatus desired, EventStatus expected)
        {
            return (EventStatus)Interlocked.CompareExchange(ref _status, (int)desired, (int)expected);
        }

        private EventStatus ExchangeStatus(EventStatus desired)
        {
            return (EventStatus)Interlocked.Exchange(ref _status, (int)desired);
        }
        
        private bool TryExchangeStatus(EventStatus desired, EventStatus expected)
        {
            return ((EventStatus)Interlocked.CompareExchange(ref _status, (int)desired, (int)expected)) == expected;
        }

        public EventStatus Status
        {
            get => (EventStatus)_status;
            private set => _status = (int)value;
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

        public enum EventLaunchType
        {
            Normal, // Not instanced
            Instanced, // Each instance has their own separate starting conditions
            InstancedStartAll, // Start all existing instances together
            InstancedEveryone, // Start all existing instances together, and start new instances for everyone in event areas
        }
        
        public EventLaunchType ParallelLaunch
        {
            get;
            set;
        }

        /// <summary>
        /// If true, this event spawns an instance when starting, separately from the master event.
        /// This means the event can be started again by other players while it is running.
        /// Players not part of an instance cannot see the mobs belonging to that instance.
        /// </summary>
        public bool IsInstancedEvent => ParallelLaunch != EventLaunchType.Normal;
        
        public bool IsInstanceMultiStart => (int) ParallelLaunch >= (int) EventLaunchType.InstancedStartAll;
        
        public bool IsInstanceMaster { get; set; }
        
        public List<GameEvent> Instances { get; init; }

        public class Child
        {
            public String EventID { get; init; }
            
            public bool Active { get; set; }
        }

        private List<Child> _eventFamily = new();

        public IReadOnlyList<Child> EventFamily
        {
            get => _eventFamily.AsReadOnly();
        }

        public FamilyConditionType EventFamilyType
        {
            get;
            set;
        }

        public FamilyOrdering EventFamilyOrdering
        {
            get;
            set;
        }

        public bool OrderedFamily => EventFamilyOrdering is not FamilyOrdering.Unordered;

        public bool IsFamilyOrderEnforced => EventFamilyOrdering is not (FamilyOrdering.Unordered or FamilyOrdering.Soft or FamilyOrdering.Hidden);

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

        public string GetFormattedEndText(string language, GamePlayer player, string endText)
        {
            if (string.IsNullOrEmpty(endText))
                return string.Empty;

            if (player == null)
                return LanguageMgr.GetEventMessage(language, endText, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            
            string playerName = player.Name;
            string groupName = player.Group?.Leader?.Name ?? "???";
            string guildName = player.GuildName ?? "???";
            string className = player.CharacterClass?.Name ?? "???";
            string raceName = player.RaceName ?? "???";

            return LanguageMgr.GetEventMessage(language, endText, playerName, groupName, guildName, className, raceName);
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

        public object GetOwnerKey(GamePlayer player)
        {
            switch (InstancedConditionType)
            {
                case InstancedConditionTypes.All:
                    return this;

                case InstancedConditionTypes.Player:
                    return player;

                case InstancedConditionTypes.Group:
                    return player.Group;

                case InstancedConditionTypes.Guild:
                    return player.Guild?.IsSystemGuild == false ? player.Guild : null;

                case InstancedConditionTypes.GroupOrSolo:
                    return player.Group ?? (object)player;

                case InstancedConditionTypes.GuildOrSolo:
                    return player.Guild?.IsSystemGuild == false ? player.Guild : (object)player;

                case InstancedConditionTypes.Battlegroup:
                    return player.BattleGroup;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public GamePlayer Owner
        {
            get => owner;
            set => owner = value;
        }

        public string OwnerDebugName
        {
            get => Owner != null ? $"player ${Owner}" : "<server>";
        }

        public void SaveToDatabase()
        {
            if (!IsInstanceMaster)
                return;
            
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
            db.EndTextA = EndTextA;
            db.EndTextB = EndTextB;
            db.EndEventSoundA = EndEventSoundA;
            db.EndEventSoundB = EndEventSoundB;
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
            db.SecondaryAnnonceType = (byte)SecondaryAnnonceType;
            db.Discord = Discord;
            db.InstancedConditionType = (int)InstancedConditionType;
            db.AreaStartingId = AreaStartingId;
            db.QuestStartingId = QuestStartingId;
            db.StartEventSound = StartEventSound;
            db.RandomEventSound = RandomEventSound;
            db.RemainingTimeEvSound = RemainingTimeEvSound;
            db.TPPointID = TPPointID;
            db.EventFamilyType = (int)EventFamilyType;
            db.EventFamilyOrdering = (int)EventFamilyOrdering;
            db.FamilyFailText = FamilyFailText ?? string.Empty;

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

        public IEnumerable<GameEvent> GetInstances()
        {
            if (IsInstancedEvent && IsInstanceMaster)
            {
                lock (Instances)
                {
                    return Instances.ToList();
                }
            }
            else
            {
                return new[] { this };
            }
        }

        public GameEvent? GetInstance(GameObject objectFor)
        {
            if (!IsInstancedEvent)
            {
                return this;
            }
            
            if (objectFor is GameLiving living)
            {
                var controller = living.GetController();
                if (controller is GameNPC eventNPC)
                    return eventNPC.Event;

                if (controller is GamePlayer player)
                    return GetInstances().FirstOrDefault(i => i.IsOwnedBy(player));
            }
            return null;
        }
        
        public GameEvent? GetOrCreateInstance(GamePlayer? triggerPlayer)
        {
            if (IsInstancedEvent)
            {
                if (!IsInstanceMaster)
                {
                    return IsOwnedBy(triggerPlayer) ? this : GameEventManager.Instance.GetEventByID(ID)?.GetOrCreateInstance(triggerPlayer);
                }
                lock (Instances)
                {
                    var key = GetOwnerKey(triggerPlayer);
                    if (key == null)
                        return null;
                    GameEvent instance = Instances.FirstOrDefault(i => i.IsOwnedBy(triggerPlayer));
                    if (instance == null)
                    {
                        instance = Instantiate(triggerPlayer);
                        if (instance == null)
                            return null;
                        Instances.Add(instance);
                    }
                    return instance;
                }
            }
            else
            {
                return this;
            }
        }

        public bool IsReady => (IsInstancedEvent && IsInstanceMaster) || (Status == EventStatus.Idle && (ChainNextEvent == null || ChainNextEvent.IsReady));

        public bool IsRunning
        {
            get
            {
                if (IsInstancedEvent && IsInstanceMaster)
                {
                    lock (Instances)
                    {
                        return Instances.Any(e => e.IsRunning);
                    }
                }
                else
                {
                    return Status is EventStatus.Started or EventStatus.Starting;
                }
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string ret = this.ID + "(\"" + this.EventName + "\")";
            bool isInstance = IsInstancedEvent && !IsInstanceMaster;
            if (Owner != null || isInstance)
            {
                ret += '[' + (isInstance ? "owner:" : "instance:") + (Owner == null ? "server" : Owner.Name) + ']';
            }

            return ret;
        }
    }
}