using Discord;
using DOL.Database;
using DOL.events.server;
using DOL.Events;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.GS.Spells;
using DOL.Language;
using DOL.MobGroups;
using DOLDatabase.Tables;
using log4net;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DOL.GameEvents
{
    public class GameEventManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private readonly int dueTime = 5000;
        private readonly int period = 10000;
        private static GameEventManager instance;
        private System.Threading.Timer timer;
        private Random _RNG;

        public static GameEventManager Instance => instance ?? (instance = new GameEventManager());

        public List<GameNPC> PreloadedMobs { get; }
        public List<GameStaticItem> PreloadedCoffres { get; }

        private GameEventManager()
        {
            Events = new Dictionary<string, GameEvent>();
            PreloadedCoffres = new List<GameStaticItem>();
            PreloadedMobs = new List<GameNPC>();
            _RNG = new Random((int)(DateTimeOffset.Now.ToUnixTimeSeconds()));
        }

        private object _eventStorageLock = new();

        public Dictionary<string, GameEvent> Events { get; set; }
        
        public IEnumerable<GameEvent> RunningEvents { get => Events.Values.Where(ev => ev.IsRunning);  }

        private Dictionary<IArea, List<GameEvent>> _areaEvents = new();
        private List<GameEvent> _timeStartEvents = new();
        private List<GameEvent> _timeEndEvents = new();
        private List<GameEvent> _timeChanceEvents = new();
        private List<GameEvent> _onetimeEvents = new();

        private Dictionary<string, List<GameEvent>> _groupKillEvents = new();
        private Dictionary<string, List<GameEvent>> _questEvents = new();
        private Dictionary<string, List<GameEvent>> _eventRelations = new();
        private Dictionary<InstancedConditionTypes, List<GameEvent>> _instancedEvents = new();

        public IDictionary<IArea, List<GameEvent>> AreaEvents => _areaEvents;
        public IEnumerable<GameEvent> TimeStartEvents => _timeStartEvents;
        public IEnumerable<GameEvent> TimeEndEvents => _timeEndEvents;
        public IEnumerable<GameEvent> TimeChanceEvents => _timeChanceEvents;
        public IEnumerable<GameEvent> GroupKillEvents => _groupKillEvents!.Values.SelectMany(l => l);
        public IEnumerable<GameEvent> QuestEvents => _questEvents!.Values.SelectMany(l => l);
        public IEnumerable<GameEvent> OnetimeEvents => _onetimeEvents;

        public IDictionary<string, List<GameEvent>> EventRelations => _eventRelations;

        public IEnumerable<GameEvent> GetEventsStartedByKillingGroup(MobGroup group)
        {
            return _groupKillEvents.GetValueOrDefault(group.GroupId) ?? Enumerable.Empty<GameEvent>();
        }

        public IEnumerable<GameEvent> GetEventsStartedByQuest(string questID)
        {
            return _questEvents.GetValueOrDefault(questID) ?? Enumerable.Empty<GameEvent>();
        }

        public bool Init()
        {
            return true;
        }

        private async void TimeCheck(object o)
        {
            var now = DateTimeOffset.UtcNow;
            List<GameEvent> events;

            lock (_eventStorageLock)
            {
                events = TimeStartEvents.Where(ev => ev.IsReady).ToList();
            }
            foreach (GameEvent ev in events)
            {
                if (ev.StartTriggerTime.HasValue && now >= ev.StartTriggerTime.Value)
                {
                    if (ev.IsGlobalYearlyEvent && ev.EndTime.HasValue && now >= ev.EndTime.Value)
                    {
                        while (now >= ev.EndTime.Value)
                        {
                            ev.StartTriggerTime = ev.StartTriggerTime.Value.AddYears(1);
                            ev.EndTime = ev.EndTime.Value.AddYears(1);
                        }
                        ev.SaveToDatabase();
                        log.Info($"Yearly event '{ev.EventName}' auto-advanced schedule to year {ev.StartTriggerTime.Value.Year}.");
                        continue;
                    }

                    await ev.Start();
                }
            }
            
            lock (_eventStorageLock)
            {
                events = TimeEndEvents.SelectMany(ev => ev.GetInstances()).Where(ev => ev.IsRunning).ToList();
            }
            foreach (GameEvent ev in events)
            {
                // Do we really want to do this here?
                if (ev.EndTime.HasValue && now >= ev.EndTime.Value)
                {
                    await ev.Stop(EndingConditionType.Timer);

                    if (ev.IsGlobalYearlyEvent && ev.StartTriggerTime.HasValue)
                    {
                        while (now >= ev.EndTime.Value)
                        {
                            ev.StartTriggerTime = ev.StartTriggerTime.Value.AddYears(1);
                            ev.EndTime = ev.EndTime.Value.AddYears(1);
                        }
                        ev.SaveToDatabase();
                        log.Info($"Yearly event '{ev.EventName}' finished its cycle and auto-advanced to year {ev.StartTriggerTime.Value.Year}.");
                    }
                }
            }
            
            lock (_eventStorageLock)
            {
                events = TimeChanceEvents.Where(ev => ev.IsReady).ToList();
            }
            foreach (GameEvent ev in events)
            {
                var interval = ev.EventChanceInterval;
                var last = ev.ChanceLastTimeChecked;
                if (!interval.HasValue)
                    continue;

                var nextCheck = ev.ChanceLastTimeChecked.HasValue ? last.Value + interval.Value : DateTimeOffset.MinValue;
                if (now < nextCheck)
                    continue;

                if (_RNG.Next(0, 101) < ev.EventChance)
                    await ev.Start();

                ev.ChanceLastTimeChecked = now;
            }

            Instance.timer.Change(Instance.period, Instance.period);
        }

        public void PlayerEntersArea(GamePlayer player, IArea area)
        {
            if (!AreaEvents.TryGetValue(area, out List<GameEvent> areaEvents) || areaEvents.Count == 0)
                return;

            foreach (var areaEv in areaEvents.Where(ev => ev.AreaConditions != null))
            {
                areaEv.GetOrCreateInstance(player)?.AreaConditions!.PlayerEntersArea(player, area);
            }
        }

        public void PlayerLeavesArea(GamePlayer player, IArea area)
        {
            if (!AreaEvents.TryGetValue(area, out List<GameEvent> areaEvents) || areaEvents.Count == 0)
                return;

            foreach (var areaEv in areaEvents.Where(ev => ev.AreaConditions != null))
            {
                areaEv.GetInstance(player)?.AreaConditions!.PlayerLeavesArea(player, area);
            }
        }

        /// <summary>
        /// Init Events Objects after Coffre loaded event
        /// </summary>
        /// <returns></returns>
        [GameServerCoffreLoaded]
        public static void LoadObjects(DOLEvent e, object sender, EventArgs arguments)
        {
            int mobCount = 0;
            int coffreCount = 0;
            var eventsFromDb = GameServer.Database.SelectAllObjects<EventDB>();

            if (eventsFromDb == null)
            {
                return;
            }

            //Load Only Not Over Events
            foreach (var eventdb in eventsFromDb)
            {
                GameEvent newEvent = new GameEvent(eventdb);

                var objects = GameServer.Database.SelectObjects<EventsXObjects>(DB.Column("EventID").IsEqualTo(eventdb.ObjectId));
                if (objects == null)
                    continue;

                foreach (var coffreInfo in objects.Where(o => o.IsCoffre && !string.IsNullOrEmpty(o.ItemID)))
                {
                    var coffre = Instance.PreloadedCoffres.FirstOrDefault(c => c.InternalID.Equals(coffreInfo.ItemID));
                    if (coffre == null)
                        continue;

                    coffre.CanRespawnWithinEvent = coffreInfo.CanRespawn;
                    newEvent.Coffres.Add(coffre);
                    Instance.PreloadedCoffres.Remove(coffre);
                    coffreCount++;

                    if (coffreInfo.StartEffect > 0)
                    {
                        newEvent.StartEffects[coffreInfo.ItemID] = (ushort)coffreInfo.StartEffect;
                    }

                    if (coffreInfo.EndEffect > 0)
                    {
                        newEvent.EndEffects[coffreInfo.ItemID] = (ushort)coffreInfo.EndEffect;
                    }
                }
                foreach (var mobInfo in objects.Where(o => o.IsMob && !string.IsNullOrEmpty(o.ItemID)))
                {
                    var mob = Instance.PreloadedMobs.FirstOrDefault(c => c.InternalID.Equals(mobInfo.ItemID));
                    if (mob == null)
                        continue;
                    
                    mob.CanRespawnWithinEvent = mobInfo.CanRespawn;
                    mob.ExperienceEventFactor = mobInfo.ExperienceFactor;
                    newEvent.Mobs.Add(mob);
                    Instance.PreloadedMobs.Remove(mob);
                    mobCount++;
                    if (mobInfo.StartEffect > 0)
                    {
                        newEvent.StartEffects[mobInfo.ItemID] = (ushort)mobInfo.StartEffect;
                    }

                    if (mobInfo.EndEffect > 0)
                    {
                        newEvent.EndEffects[mobInfo.ItemID] = (ushort)mobInfo.EndEffect;
                    }
                }

                Instance.Events.Add(eventdb.ObjectId, newEvent);
                
                if (newEvent.AreaConditions?.Area != null)
                {
                    List<GameEvent> events;
                    if (!Instance.AreaEvents.TryGetValue(newEvent.AreaConditions.Area, out events))
                    {
                        events = new List<GameEvent>();
                        Instance.AreaEvents[newEvent.AreaConditions.Area] = events;
                    }
                    events.Add(newEvent);
                }
                
                switch (newEvent.StartConditionType)
                {
                    case StartingConditionType.Timer:
                        Instance._timeStartEvents!.Add(newEvent);
                        break;
                    
                    case StartingConditionType.Kill:
                        if (!string.IsNullOrEmpty(newEvent.KillStartingGroupMobId))
                        {
                            string key = newEvent.KillStartingGroupMobId;
                            if (Instance._groupKillEvents.TryGetValue(key, out List<GameEvent> list))
                            {
                                list.Add(newEvent);
                            }
                            else
                            {
                                Instance._groupKillEvents.Add(key, new List<GameEvent>{ newEvent });
                            }
                        }
                        break;
                    
                    case StartingConditionType.Interval:
                        if (newEvent.EventChanceInterval.HasValue)
                        {
                            Instance._timeChanceEvents.Add(newEvent);
                        }
                        break;
                    
                    case StartingConditionType.Event:
                    case StartingConditionType.Money:
                    case StartingConditionType.Areaxevent:
                        // Handled separately
                        break;
                    case StartingConditionType.Quest:
                        if (!string.IsNullOrEmpty(newEvent.QuestStartingId))
                        {
                            string key = newEvent.QuestStartingId;
                            if (Instance._questEvents.TryGetValue(key, out List<GameEvent> list))
                            {
                                list.Add(newEvent);
                            }
                            else
                            {
                                Instance._questEvents.Add(key, new List<GameEvent>{ newEvent });
                            }
                        }
                        break;
                    case StartingConditionType.Switch:
                        break;
                    case StartingConditionType.Onetime:
                        Instance._onetimeEvents.Add(newEvent);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                foreach (KeyValuePair<int, EndingConditionType> entry in newEvent.EndingConditionTypes.Select((x, i) => new KeyValuePair<int, EndingConditionType>(i, x)))
                {
                    switch (entry.Value)
                    {
                        case EndingConditionType.Timer:
                            Instance._timeEndEvents.Add(newEvent);
                            break;
                        case EndingConditionType.Kill:
                        case EndingConditionType.StartingEvent:
                        case EndingConditionType.AreaEvent:
                        case EndingConditionType.TextNPC:
                        case EndingConditionType.Switch:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (newEvent.IsInstancedEvent)
                {
                    if (Instance._instancedEvents.TryGetValue(newEvent.InstancedConditionType, out List<GameEvent> list))
                    {
                        list.Add(newEvent);
                    }
                    else
                    {
                        Instance._instancedEvents.Add(newEvent.InstancedConditionType, new List<GameEvent>{ newEvent });
                    }
                }

                foreach (var child in newEvent.EventFamily.Select(child => child.EventID))
                {
                    if (Instance._eventRelations.TryGetValue(child, out List<GameEvent> list))
                    {
                        list.Add(newEvent);
                    }
                    else
                    {
                        Instance._eventRelations.Add(child, new List<GameEvent>{ newEvent });
                    }
                }
                
                if (newEvent.Status == EventStatus.Starting || newEvent.Status == EventStatus.Ending)
                    newEvent.Reset();
            }
            log.Info(string.Format("{0} Mobs Loaded Into Events", mobCount));
            log.Info(string.Format("{0} Coffre Loaded Into Events", coffreCount));
            log.Info(string.Format("{0} Events Loaded", Instance.Events.Count()));

            CreateMissingRelationObjects(Instance.Events.Values.Select(ev => ev.ID));
            Instance.timer = new System.Threading.Timer(Instance.TimeCheck, Instance, Instance.dueTime, Instance.period);

            // Evaluate and start uncompleted Onetime events
            foreach (var ev in Instance._onetimeEvents.Where(e => !e.IsCompleted))
            {
                bool isChained = Instance.Events.Values.Any(e => e.EndActionStartEventID == ev.ID);

                if (!isChained || ev.StartedTime.HasValue)
                {
                    Task.Run(() => ev.Start());
                }
            }
            GameEventMgr.Notify(GameServerEvent.GameEventLoaded);
        }

        internal IList<string> GetEventInfo(string id)
        {
            List<string> infos = new List<string>();
            var ev = Instance.Events.Values.FirstOrDefault(e => e.ID.Equals(id));

            if (ev == null)
            {
                return null;
            }

            this.GetMainInformations(ev, infos);
            this.GetGMInformations(ev, infos, false);

            return infos;
        }

        /// <summary>
        /// Add Tagged Mob or Coffre with EventID in Database
        /// </summary>
        public static void CreateMissingRelationObjects(IEnumerable<string> eventIds)
        {
            foreach (var obj in Instance.PreloadedCoffres.Where(pc => eventIds.Contains(pc.EventID)))
            {
                var newCoffre = new EventsXObjects()
                {
                    EventID = obj.EventID,
                    ItemID = obj.InternalID,
                    IsCoffre = true,
                    Name = obj.Name,
                    Region = obj.CurrentRegionID,
                    CanRespawn = true
                };

                //Try to Add Coffre from Region in case of update and remove it from world
                var ev = Instance.GetEventByID(obj.EventID);

                if (ev == null)
                    continue;
                
                var region = WorldMgr.GetRegion(obj.CurrentRegionID);
                if (region == null)
                    continue;

                var coffreObj = region.Objects.FirstOrDefault(o => o?.InternalID?.Equals(obj.InternalID) == true);
                if (coffreObj != null && coffreObj is GameStaticItem coffre)
                {
                    ev.Coffres.Add(coffre);
                    coffre.RemoveFromWorld();
                }
                else
                {
                    ev.Coffres.Add(obj);
                }

                GameServer.Database.AddObject(newCoffre);
            }

            Instance.PreloadedCoffres.Clear();

            foreach (var obj in Instance.PreloadedMobs.Where(pb => eventIds.Contains(pb.EventID)))
            {
                var newMob = new EventsXObjects()
                {
                    EventID = obj.EventID,
                    ItemID = obj.InternalID,
                    Name = obj.Name,
                    IsMob = true,
                    ExperienceFactor = obj.ExperienceEventFactor,
                    Region = obj.CurrentRegionID,
                    CanRespawn = true
                };
                
                var ev = Instance.GetEventByID(obj.EventID);

                //Try to Add Mob from Region in case of update and remove it from world
                if (ev == null)
                    continue;
                
                var region = WorldMgr.GetRegion(obj.CurrentRegionID);
                if (region == null)
                    continue;
            
                var mobObj = region.Objects.FirstOrDefault(o => o?.InternalID?.Equals(obj.InternalID) == true);
                if (mobObj != null && mobObj is GameNPC npc)
                {
                    ev.Mobs.Add(npc);
                    npc.RemoveFromWorld();
                    npc.Delete();
                }
                else
                {
                    ev.Mobs.Add(obj);
                }

                GameServer.Database.AddObject(newMob);
            }

            Instance.PreloadedMobs.Clear();
        }

        /// <summary>
        /// Returns the first currently running event that is marked as Global/Everywhere.
        /// </summary>
        public GameEvent GetActiveYearlyEvent()
        {
            return RunningEvents.FirstOrDefault(ev =>
                (ev.EventAreas != null && ev.EventAreas.Any(a => a.Equals("Everywhere", StringComparison.OrdinalIgnoreCase) || a.Equals("All", StringComparison.OrdinalIgnoreCase))) ||
                (ev.EventZones != null && ev.EventZones.Any(z => z.Equals("Everywhere", StringComparison.OrdinalIgnoreCase) || z.Equals("All", StringComparison.OrdinalIgnoreCase)))
            );
        }

        internal IList<string> GetEventsLightInfos()
        {
            List<string> infos = new List<string>();

            foreach (var e in RunningEvents)
            {
                GetMainInformations(e, infos);

                GetGMInformations(e, infos, true);

                infos.Add("");
                infos.Add("--------------------");
            }


            return infos;
        }

        /// <summary>
        /// Show Events Infos depending IsPlayer or not, GM see everything. ShowAllEvents for showing even finished events
        /// </summary>
        /// <param name="isPlayer"></param>
        /// <param name="showAllEvents"></param>
        /// <returns></returns>
        public List<string> GetEventsInfos(bool isPlayer, bool showAllEvents)
        {
            List<string> infos = new List<string>();

            IEnumerable<GameEvent> events = Instance.Events.Values.SelectMany(e => e.GetInstances());

            if (showAllEvents)
            {
                events = events.OrderBy(e => (int)e.Status);
            }
            else
            {
                events = events.Where(e => e.StartedTime.HasValue);
            }


            if (isPlayer)
            {
                events = events.Where(e => e.ShowEvent);
            }

            foreach (var e in events)
            {
                infos.Add("");
                if (!isPlayer)
                    infos.Add(" -- ID: " + e.ID);

                this.GetMainInformations(e, infos);

                if (!isPlayer)
                {
                    this.GetGMInformations(e, infos, false);
                }

                infos.Add("");
                infos.Add("--------------------");
            }

            return infos;
        }

        public GameEvent GetEventByID(string eventID)
        {
            return string.IsNullOrEmpty(eventID) ? null : Events.GetValueOrDefault(eventID, null);
        }

        private void GetMainInformations(GameEvent e, List<string> infos)
        {
            infos.Add(" -- Event Name: " + e.EventName);
            infos.Add(" -- Event Zone Name: " + (e.EventAreas != null ? string.Join(",", e.EventAreas) : string.Empty));
            infos.Add(" -- Event Zone ID: " + (e.EventZones != null ? string.Join(",", e.EventZones) : string.Empty));
            infos.Add(" -- Started Time: " + (e.StartedTime.HasValue ? e.StartedTime.Value.ToLocalTime().ToString() : string.Empty));

            if (e.EndingConditionTypes.Contains(EndingConditionType.Timer) && e.EndTime.HasValue)
            {
                infos.Add(" -- Remaining Time: " + (e.IsRunning ? string.Format(@"{0:dd\:hh\:mm\:ss}", e.EndTime.Value.Subtract(DateTimeOffset.UtcNow)) : "-"));
            }
        }

        private void GetGMInformations(GameEvent e, List<string> infos, bool isLight)
        {
            infos.Add(" -- Status: " + e.Status.ToString());
            infos.Add(" -- StartConditionType: " + e.StartConditionType.ToString());
            infos.Add(" -- StartActionStopEventID: " + e.StartActionStopEventID ?? string.Empty);
            infos.Add(" -- DebutText: " + e.DebutText ?? string.Empty);
            infos.Add(" -- TimerType: " + e.TimerType.ToString());
            infos.Add(" -- ChronoTime: " + e.ChronoTime + " mins");
            infos.Add(" -- EndTime: " + (e.EndTime.HasValue ? e.EndTime.Value.ToLocalTime().ToString() : string.Empty));
            infos.Add(" -- EndingActionA: " + e.EndingActionA.ToString());
            infos.Add(" -- EndingActionB: " + e.EndingActionB.ToString());
            infos.Add(" -- StartTriggerTime: " + (e.StartTriggerTime.HasValue ? e.StartTriggerTime.Value.ToLocalTime().ToString() : string.Empty));
            infos.Add(" -- MobNamesToKill: " + (e.MobNamesToKill != null ? string.Join(",", e.MobNamesToKill) : "-"));
            infos.Add(" -- KillStartingGroupMobId: " + (e.KillStartingGroupMobId ?? "-"));
            infos.Add(" -- ResetEventId: " + (e.ResetEventId ?? "-"));
            infos.Add(" -- ChanceLastTimeChecked: " + (e.ChanceLastTimeChecked.HasValue ? e.ChanceLastTimeChecked.Value.ToLocalTime().ToString() : "-"));
            infos.Add(" -- EndingConditionTypes: ");
            foreach (var t in e.EndingConditionTypes)
            {
                infos.Add("    * " + t.ToString());
            }

            infos.Add(" -- EndActionStartEventID: " + (e.EndActionStartEventID ?? string.Empty));
            infos.Add(" -- EndTextA: " + e.EndTextA ?? string.Empty);
            infos.Add(" -- EndTextB: " + e.EndTextB ?? string.Empty);
            infos.Add(" -- EventChance: " + e.EventChance);
            infos.Add(" -- EventChanceInterval: " + (e.EventChanceInterval.HasValue ? (e.EventChanceInterval.Value.TotalMinutes + " mins") : string.Empty));
            infos.Add(" -- RemainingTimeText: " + e.RemainingTimeText ?? string.Empty);
            infos.Add(" -- RemainingTimeInterval: " + (e.RemainingTimeInterval.HasValue ? (e.RemainingTimeInterval.Value.TotalMinutes.ToString() + " mins") : string.Empty));
            infos.Add(" -- ShowEvent: " + e.ShowEvent);
            infos.Add("");

            if (!isLight)
            {
                infos.Add(" ------- MOBS ---------- Total ( " + e.Mobs.Count() + " )");
                infos.Add("");
                foreach (var mob in e.Mobs)
                {
                    infos.Add(" * id: " + mob.InternalID);
                    infos.Add(" * Name: " + mob.Name);
                    infos.Add(" * Brain: " + mob.Brain?.GetType()?.FullName ?? string.Empty);
                    infos.Add(string.Format(" * X: {0}, Y: {1}, Z: {2}", mob.Position.X, mob.Position.Y, mob.Position.Z));
                    infos.Add(" * Region: " + mob.CurrentRegionID);
                    infos.Add(" * Zone: " + (mob.CurrentZone != null ? mob.CurrentZone.ID.ToString() : "-"));
                    infos.Add(" * Area: " + (mob.CurrentAreas != null ? string.Join(",", mob.CurrentAreas) : string.Empty));
                    infos.Add("");
                }
                infos.Add(" ------- COFFRES ---------- Total ( " + e.Coffres.Count() + " )");
                infos.Add("");
                foreach (var coffre in e.Coffres)
                {
                    infos.Add(" * id: " + coffre.InternalID);
                    infos.Add(" * Name: " + coffre.Name);
                    infos.Add(string.Format(" * X: {0}, Y: {1}, Z: {2}", coffre.Position.X, coffre.Position.Y, coffre.Position.Z));
                    infos.Add(" * Region: " + coffre.CurrentRegionID);
                    infos.Add(" * Zone: " + (coffre.CurrentZone != null ? coffre.CurrentZone.ID.ToString() : "-"));
                    infos.Add(" * Area: " + (coffre.CurrentAreas != null ? string.Join(",", coffre.CurrentAreas) : string.Empty));
                    infos.Add("");
                }
            }
            else
            {
                infos.Add(" ------- MOBS ---------- Total ( " + e.Mobs.Count() + " )");
                infos.Add(" ------- COFFRES ---------- Total ( " + e.Coffres.Count() + " )");
                infos.Add("");
            }
        }

        public void ResetEventsFromId(string id)
        {
            // TODO: review this, this is awful
            Instance.timer.Change(Timeout.Infinite, 0);
            List<string> resetIds = new List<string>();
            var ids = this.GetDependentEventsFromRootEvent(id);

            if (ids == null)
            {
                ids = new string[] { id };
            }
            else
            {
                if (!ids.Contains(id))
                {
                    ids = Enumerable.Concat(ids, new string[] { id });
                }
            }

            foreach (var eventId in ids.OrderBy(i => i))
            {
                var ev = GetEventByID(eventId);
                if (ev == null)
                {
                    continue;
                }

                if (!ev.IsGlobalYearlyEvent && ev.TimerType == TimerType.DateType && ev.EndingConditionTypes.Contains(EndingConditionType.Timer) && ev.EndingConditionTypes.Count() == 1)
                {
                    log.Error(string.Format("Cannot Reset Event {0}, Name: {1} with DateType with only Timer as Ending condition", ev.ID, ev.EventName));
                }
                else
                {
                    ev.Reset();
                    resetIds.Add(ev.ID);
                }
            }

            if (resetIds.Any())
            {
                log.Info(string.Format("Event Reset called by Event {0}, Reset events are : {1}", id, string.Join(",", resetIds)));

                foreach (var resetId in resetIds)
                {
                    var ev = GetEventByID(resetId);
                    if (ev != null && ev.StartConditionType == StartingConditionType.Onetime)
                    {
                        bool isChained = Instance.Events.Values.Any(e => e.EndActionStartEventID == ev.ID);
                        if (!isChained)
                        {
                            Task.Run(() => ev.Start());
                        }
                    }
                }
            }
            else
            {
                log.Error(string.Format("Reset called by Event: {0} but not Event Resets", id));
            }

            Instance.timer.Change(Instance.period, Instance.period);
        }

        private IEnumerable<GamePlayer> GetPlayersInEventZones(IEnumerable<string> eventZones)
        {
            return WorldMgr.GetAllPlayingClients()
                .Where(c => eventZones.Contains(c.Player.CurrentZone.ID.ToString()))
                .Select(c => c.Player);
        }

        private Dictionary<int, int> tpPointSteps = new Dictionary<int, int>();

        public TPPoint GetSmartNextTPPoint(IList<DBTPPoint> tpPoints)
        {
            TPPoint smartNextPoint = null;
            int maxPlayerCount = 0;

            foreach (var tpPoint in tpPoints)
            {
                int playerCount = WorldMgr.GetPlayersCloseToSpot(Position.Create(tpPoint.Region, tpPoint.X, tpPoint.Y, tpPoint.Z), 1500).OfType<GamePlayer>().Count(); // Using 1500 directly
                if (playerCount > maxPlayerCount)
                {
                    maxPlayerCount = playerCount;
                    smartNextPoint = new TPPoint(tpPoint.Region, tpPoint.X, tpPoint.Y, tpPoint.Z, eTPPointType.Smart, tpPoint);
                }
            }

            return smartNextPoint ?? new TPPoint(tpPoints.First().Region, tpPoints.First().X, tpPoints.First().Y, tpPoints.First().Z, eTPPointType.Smart, tpPoints.First());
        }

        public TPPoint GetLoopNextTPPoint(int tpid, IList<DBTPPoint> tpPoints)
        {
            if (!tpPointSteps.ContainsKey(tpid))
            {
                tpPointSteps[tpid] = 1;
            }

            int currentStep = tpPointSteps[tpid];
            DBTPPoint currentDBTPPoint = tpPoints.FirstOrDefault(p => p.Step == currentStep) ?? tpPoints.First();
            TPPoint tpPoint = new TPPoint(currentDBTPPoint.Region, currentDBTPPoint.X, currentDBTPPoint.Y, currentDBTPPoint.Z, eTPPointType.Loop, currentDBTPPoint);
            tpPointSteps[tpid] = (currentStep % tpPoints.Count) + 1;
            return tpPoint;
        }

        public TPPoint GetRandomTPPoint(IList<DBTPPoint> tpPoints)
        {
            DBTPPoint randomDBTPPoint = tpPoints[Util.Random(tpPoints.Count - 1)];
            return new TPPoint(randomDBTPPoint.Region, randomDBTPPoint.X, randomDBTPPoint.Y, randomDBTPPoint.Z, eTPPointType.Random, randomDBTPPoint);
        }

        public IEnumerable<string> GetDependentEventsFromRootEvent(string id)
        {
            List<string> ids = new List<string>();
            IEnumerable<string> newIds = Enumerable.Empty<string>();

            var ev = Instance.GetEventByID(id);
            if (ev == null)
                return null;
            
            var deps = this.SearchDependencies(ev);
            if (!deps.Any())
                return null;
            
            ids = deps.ToList();
            List<string> loopMem = new List<string>();
            while (newIds != null)
            {
                loopMem.Clear();
                foreach (var i in ids)
                {
                    ev = Instance.GetEventByID(i);
                    if (ev == null)
                        continue;
                    
                    deps = this.SearchDependencies(ev);
                    if (deps != null)
                    {
                        newIds = this.Add(deps, ids);
                        if (newIds != null)
                            loopMem.AddRange(newIds);
                    }
                }

                if (loopMem.Any())
                {
                    loopMem.ForEach(i => ids.Add(i));
                }

                if (deps == null)
                {
                    newIds = null;
                }
            }

            return ids.Any() ? ids.Distinct() : null;
        }


        private IEnumerable<string> Add(IEnumerable<string> deps, List<string> ids)
        {
            List<string> newIds = new List<string>();

            foreach (var id in deps)
            {
                if (!ids.Contains(id))
                {
                    newIds.Add(id);
                }
            }

            return (newIds == null || !newIds.Any()) ? null : newIds;
        }

        private IEnumerable<string> SearchDependencies(GameEvent ev)
        {
            if (ev.StartConditionType == StartingConditionType.Onetime)
            {
                List<string> deps = new List<string>();

                if (!string.IsNullOrEmpty(ev.EndActionStartEventID))
                    deps.Add(ev.EndActionStartEventID);

                if (!string.IsNullOrEmpty(ev.ResetEventId))
                    deps.Add(ev.ResetEventId);

                deps.AddRange(ev.EventFamily.Select(c => c.EventID));

                return deps;
            }
            else
            {
                var events = Instance.Events.Values.Where(e => e.EndActionStartEventID?.Equals(ev.ID) == true).Select(e => e.ID).Concat(ev.EventFamily.Select(c => c.EventID));
                return events;
            }
        }
        
        public void StartEvent(GameEvent gameEvent, GamePlayer? triggerPlayer = null)
        {
            Task.Run(() => gameEvent.Start(triggerPlayer));
        }
        
        public bool StopEvent(GameEvent gameEvent, EndingConditionType endType, GamePlayer? triggerPlayer = null)
        {
            gameEvent = gameEvent.GetInstances().FirstOrDefault(i => i.IsOwnedBy(triggerPlayer));
            if (gameEvent == null)
                return false;
            Task.Run(() => gameEvent.Stop(endType));
            return false;
        }
    }
}