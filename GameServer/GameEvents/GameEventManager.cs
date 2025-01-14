﻿using Discord;
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

        public static GameEventManager Instance => instance ?? (instance = new GameEventManager());

        public List<GameNPC> PreloadedMobs { get; }
        public List<GameStaticItem> PreloadedCoffres { get; }

        private GameEventManager()
        {
            Events = new List<GameEvent>();
            Areas = new List<AreaGameEvent>();
            PreloadedCoffres = new List<GameStaticItem>();
            PreloadedMobs = new List<GameNPC>();
        }

        public List<GameEvent> Events { get; set; }
        public List<AreaGameEvent> Areas { get; set; }

        public bool Init()
        {
            return true;
        }

        private async void TimeCheck(object o)
        {
            Instance.timer.Change(Timeout.Infinite, Timeout.Infinite);
            int counter = 0;
            while (counter < this.Events.Where(ev => ev.Status == EventStatus.NotOver).Count())
            {
                GameEvent ev = Events.Where(ev => ev.Status == EventStatus.NotOver).ToArray()[counter];
                //End events with timer over
                if (ev.EndTime.HasValue && ev.EndingConditionTypes.Contains(EndingConditionType.Timer) && DateTime.UtcNow >= ev.EndTime.Value.DateTime)
                {
                    await this.StopEvent(ev, EndingConditionType.Timer);
                }
                else if (!ev.StartedTime.HasValue && ev.StartTriggerTime.HasValue && ev.StartConditionType == StartingConditionType.Timer)
                {
                    //Start Events Timer
                    if (DateTime.UtcNow >= ev.StartTriggerTime.Value.DateTime)
                    {
                        await this.StartEvent(ev);
                    }
                    else counter++;
                }
                else counter++;
            }

            var chanceEvents = this.Events.Where(e => e.Status == EventStatus.NotOver && e.EventChanceInterval.HasValue && e.EventChance > 0 && !e.StartedTime.HasValue).ToList();
            var rand = new Random((int)(DateTimeOffset.Now.ToUnixTimeSeconds() / 10000));


            //Start Event if chance proc
            foreach (var ev in chanceEvents)
            {
                if (!ev.ChanceLastTimeChecked.HasValue)
                {
                    ev.ChanceLastTimeChecked = DateTimeOffset.UtcNow;
                    ev.SaveToDatabase();
                }
                else
                {
                    if (DateTimeOffset.UtcNow - ev.ChanceLastTimeChecked.Value >= ev.EventChanceInterval!.Value)
                    {
                        ev.ChanceLastTimeChecked = DateTimeOffset.UtcNow;
                        if (rand.Next(0, 101) <= ev.EventChance)
                        {
                            await this.StartEvent(ev);
                        }
                    }
                }
            }

            Instance.timer.Change(Instance.period, Instance.period);
        }

        /// <summary>
        /// Init Events Areas after Area loaded event
        /// </summary>
        /// <returns></returns>
        [GameServerStartedEvent]
        public static void LoadAreas(DOLEvent e, object sender, EventArgs arguments)
        {
            var areasFromDb = GameServer.Database.SelectAllObjects<AreaXEvent>();

            if (areasFromDb == null)
            {
                return;
            }

            foreach (var dbEvent in areasFromDb)
            {
                var area = WorldMgr.GetAllRegions().Select(w => w.GetArea(dbEvent.AreaID)).OfType<AbstractArea>().FirstOrDefault();
                if (area == null)
                {
                    log.Warn($"DB Area event {dbEvent.EventID} has invalid area ${dbEvent.AreaID}");
                    continue;
                }
                var areaEvent = new AreaGameEvent(dbEvent, area);
                Instance.Areas.Add(areaEvent);
            }
        }

        /// <summary>
        /// Reset area event on leave
        /// </summary>
        /// <returns></returns>
        public async Task ResetAreaEvent(AbstractArea area)
        {
            var areaXEvents = Instance.Areas.Where(a => a.AreaID.Equals(area.DbArea.ObjectId));

            if (areaXEvents == null)
            {
                return;
            }
            foreach (var areaEvent in areaXEvents)
            {
                await areaEvent.ResetAreaEvent();
            }
        }
        /// <summary>
        /// Update area event on enter
        /// </summary>
        /// <returns></returns>
        public static void UpdateAreaEvent(AbstractArea area)
        {
            var areaXEvent = Instance.Areas.FirstOrDefault(a => a.AreaID.Equals(area.DbArea.ObjectId));

            if (areaXEvent == null)
            {
                return;
            }
            areaXEvent.CheckConditions(area);
        }

        /// <summary>
        /// Update area event on use item
        /// </summary>
        /// <returns></returns>
        public static void AreaUseItemEvent(GamePlayer player, string itemId)
        {
            foreach (AbstractArea area in player.CurrentAreas)
            {
                if (area.DbArea == null)
                    continue;
                var areaXEvent = Instance.Areas.FirstOrDefault(a => a.AreaID.Equals(area.DbArea.ObjectId));

                if (areaXEvent == null)
                {
                    continue;
                }
                if (areaXEvent.UseItem == itemId)
                    areaXEvent.UseItemCounter++;
                areaXEvent.CheckConditions();
            }
        }

        /// <summary>
        /// Update area event on whisper
        /// </summary>
        /// <returns></returns>
        public static void AreaWhisperEvent(GamePlayer player, string whisper)
        {
            foreach (AbstractArea area in player.CurrentAreas)
            {
                if (area.DbArea == null)
                    continue;
                var areaXEvent = Instance.Areas.FirstOrDefault(a => a.AreaID.Equals(area.DbArea.ObjectId));

                if (areaXEvent == null)
                {
                    continue;
                }
                if (areaXEvent.Whisper == whisper)
                    areaXEvent.WhisperCounter++;
                areaXEvent.CheckConditions();
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

                if (objects != null)
                {
                    foreach (var coffreInfo in objects.Where(o => o.IsCoffre))
                    {
                        if (coffreInfo.ItemID != null)
                        {
                            var coffre = Instance.PreloadedCoffres.FirstOrDefault(c => c.InternalID.Equals(coffreInfo.ItemID));

                            if (coffre != null)
                            {
                                coffre.CanRespawnWithinEvent = coffreInfo.CanRespawn;
                                newEvent.Coffres.Add(coffre);
                                Instance.PreloadedCoffres.Remove(coffre);
                                coffreCount++;

                                if (coffreInfo.StartEffect > 0)
                                {
                                    if (newEvent.StartEffects.ContainsKey(coffreInfo.ItemID))
                                    {
                                        newEvent.StartEffects[coffreInfo.ItemID] = (ushort)coffreInfo.StartEffect;
                                    }
                                    else
                                    {
                                        newEvent.StartEffects.Add(coffreInfo.ItemID, (ushort)coffreInfo.StartEffect);
                                    }
                                }

                                if (coffreInfo.EndEffect > 0)
                                {
                                    if (newEvent.EndEffects.ContainsKey(coffreInfo.ItemID))
                                    {
                                        newEvent.EndEffects[coffreInfo.ItemID] = (ushort)coffreInfo.EndEffect;
                                    }
                                    else
                                    {
                                        newEvent.EndEffects.Add(coffreInfo.ItemID, (ushort)coffreInfo.EndEffect);
                                    }
                                }
                            }
                        }
                    }

                    foreach (var mobInfo in objects.Where(o => o.IsMob))
                    {
                        if (mobInfo.ItemID != null)
                        {
                            var mob = Instance.PreloadedMobs.FirstOrDefault(c => c.InternalID.Equals(mobInfo.ItemID));

                            if (mob != null)
                            {
                                mob.CanRespawnWithinEvent = mobInfo.CanRespawn;
                                mob.ExperienceEventFactor = mobInfo.ExperienceFactor;
                                newEvent.Mobs.Add(mob);
                                Instance.PreloadedMobs.Remove(mob);
                                mobCount++;

                                if (mobInfo.StartEffect > 0)
                                {
                                    if (newEvent.StartEffects.ContainsKey(mobInfo.ItemID))
                                    {
                                        newEvent.StartEffects[mobInfo.ItemID] = (ushort)mobInfo.StartEffect;
                                    }
                                    else
                                    {
                                        newEvent.StartEffects.Add(mobInfo.ItemID, (ushort)mobInfo.StartEffect);
                                    }
                                }

                                if (mobInfo.EndEffect > 0)
                                {
                                    if (newEvent.EndEffects.ContainsKey(mobInfo.ItemID))
                                    {
                                        newEvent.EndEffects[mobInfo.ItemID] = (ushort)mobInfo.EndEffect;
                                    }
                                    else
                                    {
                                        newEvent.EndEffects.Add(mobInfo.ItemID, (ushort)mobInfo.EndEffect);
                                    }
                                }
                            }
                        }
                    }
                }

                Instance.Events.Add(newEvent);
            }
            log.Info(string.Format("{0} Mobs Loaded Into Events", mobCount));
            log.Info(string.Format("{0} Coffre Loaded Into Events", coffreCount));
            log.Info(string.Format("{0} Events Loaded", Instance.Events.Count()));

            CreateMissingRelationObjects(Instance.Events.Select(ev => ev.ID));
            Instance.timer = new System.Threading.Timer(Instance.TimeCheck, Instance, Instance.dueTime, Instance.period);
            GameEventMgr.Notify(GameServerEvent.GameEventLoaded);
        }

        internal IList<string> GetEventInfo(string id)
        {
            List<string> infos = new List<string>();
            var ev = Instance.Events.FirstOrDefault(e => e.ID.Equals(id));

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
                var ev = Instance.Events.FirstOrDefault(e => e.ID.Equals(obj.EventID));

                if (ev != null)
                {
                    var region = WorldMgr.GetRegion(obj.CurrentRegionID);

                    if (region != null)
                    {
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
                    }
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

                var ev = Instance.Events.FirstOrDefault(e => e.ID.Equals(obj.EventID));

                //Try to Add Mob from Region in case of update and remove it from world
                if (ev != null)
                {
                    var region = WorldMgr.GetRegion(obj.CurrentRegionID);

                    if (region != null)
                    {
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
                    }
                }

                GameServer.Database.AddObject(newMob);
            }

            Instance.PreloadedMobs.Clear();
        }

        internal IList<string> GetEventsLightInfos()
        {
            List<string> infos = new List<string>();

            foreach (var e in this.Events.Where(e => e.Status == EventStatus.NotOver))
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

            IEnumerable<GameEvent> events = Instance.Events.Where(e => e.Status == EventStatus.NotOver);

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
            return Events.FirstOrDefault(e => e.ID == eventID);
        }

        private void GetMainInformations(GameEvent e, List<string> infos)
        {
            infos.Add(" -- Event Name: " + e.EventName);
            infos.Add(" -- Event Zone Name: " + (e.EventAreas != null ? string.Join(",", e.EventAreas) : string.Empty));
            infos.Add(" -- Event Zone ID: " + (e.EventZones != null ? string.Join(",", e.EventZones) : string.Empty));
            infos.Add(" -- Started Time: " + (e.StartedTime.HasValue ? e.StartedTime.Value.ToLocalTime().ToString() : string.Empty));

            if (e.EndingConditionTypes.Contains(EndingConditionType.Timer) && e.EndTime.HasValue)
            {
                infos.Add(" -- Remaining Time: " + (e.Status == EventStatus.NotOver ? string.Format(@"{0:dd\:hh\:mm\:ss}", e.EndTime.Value.Subtract(DateTimeOffset.UtcNow)) : "-"));
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
            infos.Add(" -- EndText: " + e.EndText ?? string.Empty);
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
                var ev = this.Events.FirstOrDefault(e => e.ID.Equals(eventId));

                if (ev == null)
                {
                    break;
                }

                if (ev.TimerType == TimerType.DateType && ev.EndingConditionTypes.Contains(EndingConditionType.Timer) && ev.EndingConditionTypes.Count() == 1)
                {
                    log.Error(string.Format("Cannot Reset Event {0}, Name: {1} with DateType with only Timer as Ending condition", ev.ID, ev.EventName));
                }
                else
                {
                    this.ResetEvent(ev);
                    resetIds.Add(ev.ID);
                }
            }

            if (resetIds.Any())
            {
                log.Info(string.Format("Event Reset called by Event {0}, Reset events are : {1}", id, string.Join(",", resetIds)));
            }
            else
            {
                log.Error(string.Format("Reset called by Event: {0} but not Event Resets", id));
            }

            Instance.timer.Change(Instance.period, Instance.period);
        }

        public void ResetEvent(GameEvent ev)
        {
            if (!ev.ParallelLaunch)
            {
                if (!ev.StartedTime.HasValue)
                {
                    GameEvent startedEvent = Instance.Events.FirstOrDefault(e => e.ID.Equals(ev.ID) && e.StartedTime.HasValue);
                    if (startedEvent != null)
                        ev = startedEvent;
                }
                while (Instance.Events.Where(e => e.ID.Equals(ev!.ID) && e.StartedTime.HasValue).Count() > 1)
                {
                    CleanEvent(ev);
                    Instance.Events.Remove(ev);
                    ev = Instance.Events.FirstOrDefault(e => e.ID.Equals(ev!.ID) && e.StartedTime.HasValue);
                }
                if (ev != null && Instance.Events.Where(e => e.ID.Equals(ev.ID)).Count() > 1)
                {
                    CleanEvent(ev);
                    Instance.Events.Remove(ev);
                }
                ev = Instance.Events.FirstOrDefault(e => e.ID.Equals(ev!.ID));
            }
            ev!.StartedTime = (DateTimeOffset?)null;
            ev.EndTime = (DateTimeOffset?)null;
            ev.Status = EventStatus.NotOver;
            ev.WantedMobsCount = 0;
            CleanEvent(ev);
            if (ev != null && Instance.Events.Where(e => e.ID.Equals(ev.ID)).Count() > 1)
            {
                Instance.Events.Remove(ev);
            }

            if (ev!.StartConditionType == StartingConditionType.Money)
            {
                //Reset related NPC Money
                var moneyNpcDb = GameServer.Database.SelectObjects<MoneyNpcDb>(DB.Column("EventID").IsEqualTo(ev.ID))?.FirstOrDefault();

                if (moneyNpcDb != null)
                {
                    var mob = GameServer.Database.FindObjectByKey<Mob>(moneyNpcDb.MobID);

                    if (mob != null)
                    {
                        MoneyEventNPC mobIngame = WorldMgr.Regions[mob.Region].Objects?.FirstOrDefault(o => o?.InternalID?.Equals(mob.ObjectId) == true && o is MoneyEventNPC) as MoneyEventNPC;

                        if (mobIngame != null)
                        {
                            mobIngame.CurrentSilver = 0;
                            mobIngame.CurrentCopper = 0;
                            mobIngame.CurrentGold = 0;
                            mobIngame.CurrentMithril = 0;
                            mobIngame.CurrentPlatinum = 0;
                            mobIngame.SaveIntoDatabase();
                        }
                    }


                    moneyNpcDb.CurrentAmount = 0;
                    GameServer.Database.SaveObject(moneyNpcDb);
                }

            }

            //restore temporarly disabled RemovedMobs
            foreach (var mob in ev.RemovedMobs)
            {
                mob.Value.InternalID = mob.Key;
                mob.Value.AddToWorld();
            }
            ev.RemovedMobs.Clear();

            foreach (var item in ev.RemovedCoffres)
            {
                item.Value.InternalID = item.Key;
                item.Value.AddToWorld();
            }
            ev.RemovedCoffres.Clear();

            ev.SaveToDatabase();
        }

        private IEnumerable<GamePlayer> GetPlayersInEventZones(IEnumerable<string> eventZones)
        {
            return WorldMgr.GetAllPlayingClients()
                .Where(c => eventZones.Contains(c.Player.CurrentZone.ID.ToString()))
                .Select(c => c.Player);
        }

        public async Task<bool> StartEvent(GameEvent ev, AreaGameEvent areaEvent = null, GamePlayer startingPlayer = null)
        {
            //temporarly disable
            var disabledMobs = GameServer.Database.SelectObjects<Mob>(DB.Column("RemovedByEventID").IsNotNull());
            foreach (var mob in disabledMobs)
            {
                if (mob.RemovedByEventID.Split("|").Contains(ev.ID.ToString()))
                {
                    var mobInRegion = WorldMgr.Regions[mob.Region].Objects.FirstOrDefault(o => o != null && o is GameNPC npc && npc.InternalID != null && npc.InternalID.Equals(mob.ObjectId));
                    if (mobInRegion != null)
                    {
                        var npcInRegion = mobInRegion as GameNPC;
                        //copy npc
                        ev.RemovedMobs[npcInRegion!.InternalID] = npcInRegion;
                        npcInRegion.RemoveFromWorld();
                        npcInRegion.Delete();
                    }
                }
            }
            var disabledCoffres = GameServer.Database.SelectObjects<DBCoffre>(DB.Column("RemovedByEventID").IsNotNull());
            foreach (var coffre in disabledCoffres)
            {
                if (coffre.RemovedByEventID.Split("|").Contains(ev.ID.ToString()))
                {
                    var coffreInRegion = WorldMgr.Regions[coffre.Region].Objects.FirstOrDefault(o => o != null && o is GameStaticItem item && item.InternalID.Equals(coffre.ObjectId)) as GameStaticItem;
                    if (coffreInRegion != null)
                    {
                        var itemInRegion = coffreInRegion as GameStaticItem;
                        ev.RemovedCoffres[itemInRegion.InternalID] = itemInRegion;
                        itemInRegion.RemoveFromWorld();
                        itemInRegion.Delete();
                    }
                }
            }

            List<GameEvent> events = new List<GameEvent>();
            if (areaEvent != null && ev.InstancedConditionType != InstancedConditionTypes.All)
            {
                GameEvent newEvent = null;
                if (ev.ParallelLaunch)
                    newEvent = new GameEvent(ev);
                else
                    newEvent = ev;
                AreaGameEvent newAreaEvent = new AreaGameEvent(areaEvent);
                newAreaEvent.LaunchedEvent = newEvent;
                newAreaEvent.LaunchedInstancedConditionType = ev.InstancedConditionType;
                Instance.Areas.Add(newAreaEvent);

                List<Group> addedGroups = new List<Group>();
                List<Guild> addedGuilds = new List<Guild>();
                List<object> addedBattlegroups = new List<object>();
                bool isFirst = true;
                bool CreateInstance(GamePlayer playerFor)
                {
                    // If player already has this event continue
                    if (Instance.Events.Any(e => e.Owner == playerFor && e.ID == ev.ID))
                        return false;
                    
                    switch (ev.InstancedConditionType)
                    {
                        case InstancedConditionTypes.Player:
                            if (!isFirst)
                                newEvent = new GameEvent(ev);
                            isFirst = false;
                            newEvent.Owner = playerFor;
                            return true;
                        case InstancedConditionTypes.Group:
                            if (playerFor.Group != null)
                            {
                                if (addedGroups.Contains(playerFor.Group))
                                    return false;
                                addedGroups.Add(playerFor.Group);
                            }
                            if (!isFirst)
                                newEvent = new GameEvent(ev);
                            isFirst = false;
                            newEvent.Owner = playerFor;
                            return true;
                        case InstancedConditionTypes.Guild:
                            if (playerFor.Guild != null)
                            {
                                if (addedGuilds.Contains(playerFor.Guild))
                                {
                                    return false;
                                }
                                addedGuilds.Add(playerFor.Guild);
                            }
                            if (!isFirst)
                                newEvent = new GameEvent(ev);
                            isFirst = false;
                            newEvent.Owner = playerFor;
                            return true;
                        case InstancedConditionTypes.Battlegroup:
                            var battleGroup = (playerFor.TempProperties.getProperty<object>(BattleGroup.BATTLEGROUP_PROPERTY, null) as BattleGroup) ?? playerFor.BattleGroup;
                            if (battleGroup != null)
                            {
                                if (addedBattlegroups.Contains(battleGroup))
                                {
                                    return false;
                                }
                                addedBattlegroups.Add(battleGroup);
                            }
                            if (!isFirst)
                                newEvent = new GameEvent(ev);
                            isFirst = false;
                            newEvent.Owner = playerFor;
                            return true;
                        default:
                            break;
                    }
                    return false;
                }

                var players = areaEvent.GetPlayersInArea();
                if (startingPlayer != null)
                    players.Add(startingPlayer.Client);
                foreach (var cl in players)
                {
                    if (CreateInstance(cl.Player))
                    {
                        events.Add(newEvent);

                        if (!Instance.Events.Contains(newEvent))
                            Instance.Events.Add(newEvent);
                    }
                }

            }
            else
            {
                // If event is parallel launch, create new instance of event for each player
                if (ev.ParallelLaunch && ev.InstancedConditionType == InstancedConditionTypes.All)
                    ev.InstancedConditionType = InstancedConditionTypes.Player;

                // Set owner of event
                if (startingPlayer != null)
                {
                    if (ev.InstancedConditionType == InstancedConditionTypes.Group)
                        ev.Owner = startingPlayer.Group.Leader;
                    else
                        ev.Owner = startingPlayer;
                }

                if (ev.ParallelLaunch)
                    events.Add(new GameEvent(ev));
                else
                    events.Add(ev);
            }

            foreach (var e in events)
            {
                StartEventSetup(e);

                if (e.StartEventSound > 0)
                {
                    foreach (var player in GetPlayersInEventZones(e.EventZones))
                    {
                        player.Out.SendSoundEffect((ushort)e.StartEventSound, player.Position, 0);
                    }
                }
            }

            //need give more time to client after addtoworld to perform animation
            await Task.Delay(500);

            foreach (var e in events)
            {
                await Task.Run(() => GameEventManager.Instance.StartEventEffects(e));
            }

            ev.SaveToDatabase();

            return true;
        }
        public bool StartEventSetup(GameEvent e)
        {
            e.WantedMobsCount = 0;

            if (e.EndingConditionTypes.Contains(EndingConditionType.Timer))
            {
                if (e.TimerType == TimerType.ChronoType)
                {
                    e.EndTime = DateTimeOffset.UtcNow.AddMinutes(e.ChronoTime);
                }
                else
                {
                    //Cannot launch event if Endate is not set in DateType and no other ending exists
                    if (!e.EndTime.HasValue)
                    {
                        if (e.EndingConditionTypes.Count() == 1)
                        {
                            log.Error(string.Format("Cannot Launch Event {0}, Name: {1} with DateType because EndDate is Null", e.ID, e.EventName));
                            return false;
                        }
                        else
                        {
                            log.Warn(string.Format("Event Id: {0}, Name: {1}, started with ending type Timer DateType but Endate is Null, Event Started with other endings", e.ID, e.EventName));
                        }
                    }
                }
            }

            foreach (var mob in e.Mobs)
            {
                mob.Health = mob.MaxHealth;
                mob.Event = e;
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
                            mobGroup.NPCs.Add(mob);
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
                            mobGroup.NPCs.Add(mob);
                            mob.AddToMobGroup(mobGroup);
                            mobGroup.ApplyGroupInfos();
                        }
                    }
                }

                if (e.IsKillingEvent && e.MobNamesToKill.Contains(mob.Name))
                {
                    e.WantedMobsCount++;
                }
            }

            if (e.IsKillingEvent)
            {
                int delta = e.MobNamesToKill.Count() - e.WantedMobsCount;

                if (e.WantedMobsCount == 0 && e.EndingConditionTypes.Where(ed => ed != EndingConditionType.Kill).Count() == 0)
                {
                    log.Error(string.Format("Event ID: {0}, Name: {1}, cannot be start because No Mobs found for Killing Type ending and no other ending type set", e.ID, e.EventName));
                    return false;
                }
                else if (delta > 0)
                {
                    log.Error(string.Format("Event ID: {0}, Name {1}: with Kill type has {2} mobs missings, MobNamesToKill column in datatabase and tagged mobs Name should match.", e.ID, e.EventName, delta));
                }
            }

            e.StartedTime = DateTimeOffset.UtcNow;
            e.Status = EventStatus.NotOver;


            if (e.DebutText != null && e.EventZones?.Any() == true)
            {
                SendEventNotification(e, (lang) => e.FormatEventMessage(e.GetFormattedDebutText(lang, e.Owner)), (e.Discord == 1 || e.Discord == 3), true);
            }

            if (e.HasHandomText)
            {
                e.RandomTextTimer.Start();
            }

            if (e.HasRemainingTimeText)
            {
                e.RemainingTimeTimer.Start();
            }

            foreach (var mob in e.Mobs)
            {
                mob.AddToWorld();
            }

            e.Coffres.ForEach(c => c.AddToWorld());
            return true;
        }

        public async Task<bool> StartEventEffects(GameEvent e)
        {
            foreach (var mob in e.Mobs)
            {
                if (e.StartEffects.ContainsKey(mob.InternalID))
                {
                    this.ApplyEffect(mob, e.StartEffects);
                }
            }

            foreach (var coffre in e.Coffres)
            {
                if (e.StartEffects.ContainsKey(coffre.InternalID))
                {
                    ApplyEffect(coffre, e.StartEffects);
                }
            }

            List<GameNPC> npc;
            lock (e.RelatedNPCs)
            {
                npc = new List<GameNPC>(e.RelatedNPCs);
            }
            foreach (var mob in npc)
            {
                mob.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).Cast<GamePlayer>().ForEach(mob.RefreshEffects);
            }

            if (!string.IsNullOrEmpty(e.StartActionStopEventID))
            {
                await FinishEventByEventById(e.ID, e.StartActionStopEventID);
            }
            return true;
        }

        private async Task FinishEventByEventById(string originEventId, string startActionStopEventID)
        {
            var ev = this.Events.FirstOrDefault(e => e.ID.Equals(startActionStopEventID));

            if (ev == null)
            {
                log.Error(string.Format("Impossible To Stop Event Id {0} from StartActionStopEventID (Event {1}). Event not found", startActionStopEventID, originEventId));
                return;
            }

            log.Info(string.Format("Stop Event Id {0} from StartActionStopEventID (Event {1})", startActionStopEventID, originEventId));
            await this.StopEvent(ev, EndingConditionType.StartingEvent);
        }

        private void ApplyEffect(GameObject item, Dictionary<string, ushort> dic)
        {
            foreach (GamePlayer pl in item.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                pl.Out.SendSpellEffectAnimation(item, item, dic[item.InternalID], 0, false, 5);


            }
        }

        public void SendEventNotification(GameEvent e, Func<string, string> message, bool sendDiscord, bool createNews = false)
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
            foreach (var player in GetPlayersInEventZones(e.EventZones))
            {
                lang = player?.Client?.Account?.Language ?? Properties.SERV_LANGUAGE!;
                if (!cachedMessages.TryGetValue(lang, out msg))
                {
                    msg = message(lang);
                    if (string.IsNullOrEmpty(msg))
                        msg = message(Properties.SERV_LANGUAGE);
                    cachedMessages[lang] = msg;
                }
                NotifyPlayer(player, e.AnnonceType, msg);
            }
        }

        public async Task StopEvent(GameEvent e, EndingConditionType end)
        {
            e.EndTime = DateTimeOffset.UtcNow;

            if (e.EndText != null && e.EventZones?.Any() == true)
            {
                SendEventNotification(e, (string lang) => e.FormatEventMessage(e.GetFormattedEndText(lang, e.Owner)), (e.Discord == 2 || e.Discord == 3), true);

                foreach (var player in GetPlayersInEventZones(e.EventZones))
                {
                    if (e.EndEventSound > 0)
                    {
                        player.Out.SendSoundEffect((ushort)e.EndEventSound, player.Position, 0);
                    }
                }

                //Enjoy the message
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            if (end == EndingConditionType.Kill && e.IsKillingEvent)
            {
                e.Status = EventStatus.EndedByKill;
                //Allow time to loot
                await Task.Delay(TimeSpan.FromSeconds(15));
                await ShowEndEffects(e);
                CleanEvent(e);
            }
            else if (end == EndingConditionType.StartingEvent)
            {
                e.Status = EventStatus.EndedByEventStarting;
                await ShowEndEffects(e);
                CleanEvent(e);
            }
            else if (end == EndingConditionType.Timer)
            {
                e.Status = EventStatus.EndedByTimer;
                await ShowEndEffects(e);
                CleanEvent(e);
            }
            else if (end == EndingConditionType.AreaEvent)
            {
                e.Status = EventStatus.EndedByAreaEvent;
                await ShowEndEffects(e);
                CleanEvent(e);
            }
            else if (end == EndingConditionType.TextNPC)
            {
                e.Status = EventStatus.EndedByTextNPC;
                await ShowEndEffects(e);
                CleanEvent(e);
            }
            else if (end == EndingConditionType.Switch)
            {
                e.Status = EventStatus.EndedBySwitch;
                await ShowEndEffects(e);
                CleanEvent(e);
            }

            var eventsCount = GameEventManager.Instance.Events.Where(ev => ev.ID.Equals(e.ID)).Count();
            if (eventsCount == 1)
            {
                //restore temporarly disabled
                foreach (var mob in e.RemovedMobs)
                {
                    mob.Value.AddToWorld();
                    mob.Value.InternalID = mob.Key;
                }
                e.RemovedMobs.Clear();

                foreach (var item in e.RemovedCoffres)
                {
                    item.Value.AddToWorld();
                    item.Value.InternalID = item.Key;
                }
                e.RemovedCoffres.Clear();
            }
            else
            {
                GameEventManager.Instance.Events.Remove(e);
            }

            //Handle Consequences
            //Consequence A
            if (e.EndingConditionTypes.Count() == 1 || (e.EndingConditionTypes.Count() > 1 && e.EndingConditionTypes.First() == end))
            {
                await this.HandleConsequence(e.EndingActionA, e.EventZones, e.EndActionStartEventID, e.ResetEventId, e);
            }
            else
            {
                //Consequence B
                await this.HandleConsequence(e.EndingActionB, e.EventZones, e.EndActionStartEventID, e.ResetEventId, e);
            }

            log.Info(string.Format("Event Id: {0}, Name: {1} was stopped At: {2}", e.ID, e.EventName, DateTime.Now.ToString()));

            //Handle Interval Starting Event
            //let a chance to this event to trigger at next interval
            if (e.StartConditionType == StartingConditionType.Interval)
            {
                e.Status = EventStatus.NotOver;
                e.StartedTime = null;
                e.EndTime = null;
                e.ChanceLastTimeChecked = (DateTimeOffset?)null;
            }

            e.SaveToDatabase();
        }

        private async Task ShowEndEffects(GameEvent e)
        {
            foreach (var mob in e.Mobs)
            {
                if (e.EndEffects.ContainsKey(mob.InternalID))
                {
                    this.ApplyEffect(mob, e.EndEffects);
                }
            }

            foreach (var coffre in e.Coffres)
            {
                if (e.EndEffects.ContainsKey(coffre.InternalID))
                {
                    this.ApplyEffect(coffre, e.EndEffects);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        private async Task HandleConsequence(EndingAction action, IEnumerable<string> zones, string startEventId, string resetEventId, GameEvent startingEvent)
        {
            string eventId = startingEvent.ID;

            if (action == EndingAction.BindStone)
            {
                foreach (var cl in WorldMgr.GetAllPlayingClients().Where(c => zones.Contains(c.Player.CurrentZone.ID.ToString())))
                {
                    cl.Player.MoveToBind();
                }

                return;
            }

            if (action == EndingAction.JumpToTPPoint)
            {
                if (!startingEvent.TPPointID.HasValue)
                {
                    log.Error($"Event {startingEvent.ID} has JumpToTPPoint action but no TPPointID is set.");
                    return;
                }

                IList<DBTPPoint> tpPoints = GameServer.Database.SelectObjects<DBTPPoint>(DB.Column("TPID").IsEqualTo(startingEvent.TPPointID.Value));
                DBTP dbtp = GameServer.Database.SelectObjects<DBTP>(DB.Column("TPID").IsEqualTo(startingEvent.TPPointID.Value)).FirstOrDefault();

                if (tpPoints != null && tpPoints.Count > 0 && dbtp != null)
                {
                    TPPoint tpPoint = null;
                    switch ((eTPPointType)dbtp.TPType)
                    {
                        case eTPPointType.Loop:
                            tpPoint = GetLoopNextTPPoint(dbtp.TPID, tpPoints);
                            break;

                        case eTPPointType.Random:
                            tpPoint = GetRandomTPPoint(tpPoints);
                            break;

                        case eTPPointType.Smart:
                            tpPoint = GetSmartNextTPPoint(tpPoints);
                            break;
                    }

                    if (tpPoint != null)
                    {
                        foreach (var cl in WorldMgr.GetAllPlayingClients().Where(c => zones.Contains(c.Player.CurrentZone.ID.ToString())))
                        {
                            cl.Player.MoveTo(Position.Create(tpPoint.Region, tpPoint.Position.X, tpPoint.Position.Y, tpPoint.Position.Z));
                        }
                    }
                }
                return;
            }

            if (startEventId != null && startingEvent.Status != EventStatus.EndedByTimer)
            {
                var ev = Instance.Events.FirstOrDefault(e => e.ID.Equals(startEventId));
                if (ev != null && ev.TimeBeforeReset != 0)
                {
                    bool startEvent = true;
                    bool startTimer = false;
                    ev.EventFamily[eventId] = true;
                    foreach (var family in ev.EventFamily)
                    {
                        if (family.Value == false)
                        {
                            startTimer = true;
                            startEvent = false;
                        }
                    }

                    if (startTimer == true)
                    {
                        ev.ResetFamilyTimer.Start();
                    }

                    if (startEvent == false)
                    {
                        return;
                    }
                    else
                    {
                        ev.ResetFamilyTimer.Stop();
                        foreach (var family in ev.EventFamily)
                        {
                            ev.EventFamily[family.Key] = false;
                        }
                    }
                }

                if (ev == null)
                {
                    log.Error(string.Format("Ending Consequence Event: Impossible to start Event ID: {0}. Event not found.", startEventId));
                    return;
                }

                if (ev.StartConditionType != StartingConditionType.Event)
                {
                    log.Error(string.Format("Ending Consequence Event: Impossible to start Event ID: {0}. Event is not Event Start type", startEventId));
                }
                else
                {
                    ev.StartedTime = null;
                    await Instance.StartEvent(ev);
                }
            }

            if (resetEventId != null)
            {
                var resetEvent = this.Events.FirstOrDefault(e => e.ID.Equals(resetEventId));

                if (resetEvent == null)
                {
                    log.Error("Impossible to reset Event from resetEventId : " + resetEventId);
                    return;
                }

                if (resetEvent.TimerType == TimerType.DateType && resetEvent.EndingConditionTypes.Contains(EndingConditionType.Timer) && resetEvent.EndingConditionTypes.Count() == 1)
                {
                    log.Error(string.Format("Cannot Reset Event {0}, Name: {1} with DateType with only Timer as Ending condition", resetEvent.ID, resetEvent.EventName));
                }
                else
                {
                    this.ResetEventsFromId(resetEventId);
                }
            }
        }

        private Dictionary<int, int> tpPointSteps = new Dictionary<int, int>();

        private TPPoint GetSmartNextTPPoint(IList<DBTPPoint> tpPoints)
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

        private TPPoint GetLoopNextTPPoint(int tpid, IList<DBTPPoint> tpPoints)
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

        private TPPoint GetRandomTPPoint(IList<DBTPPoint> tpPoints)
        {
            DBTPPoint randomDBTPPoint = tpPoints[Util.Random(tpPoints.Count - 1)];
            return new TPPoint(randomDBTPPoint.Region, randomDBTPPoint.X, randomDBTPPoint.Y, randomDBTPPoint.Z, eTPPointType.Random, randomDBTPPoint);
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

        private static void CleanEvent(GameEvent e)
        {
            try
            {
                foreach (var mob in e.Mobs)
                {
                    if (mob.ObjectState == GameObject.eObjectState.Active)
                    {
                        if (!mob.IsPeaceful)
                            mob.Health = 0;
                        mob.RemoveFromWorld();
                        mob.Delete();
                    }
                }

                foreach (var coffre in e.Coffres)
                {
                    if (coffre.ObjectState == GameObject.eObjectState.Active)
                        coffre.RemoveFromWorld();
                }
                e.Clean();
            }
            catch (Exception ex)
            {
                log.Error($"Exception while cleaning up event {e.ID}: {ex}");
            }
            
            List<GameNPC> npc;
            lock (e.RelatedNPCs)
            {
                npc = new List<GameNPC>(e.RelatedNPCs);
            }
            foreach (var mob in npc)
            {
                mob.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).Cast<GamePlayer>().ForEach(mob.RefreshEffects);
            }
        }

        public IEnumerable<string> GetDependentEventsFromRootEvent(string id)
        {
            List<string> ids = new List<string>();
            IEnumerable<string> newIds = Enumerable.Empty<string>();

            var ev = Instance.Events.FirstOrDefault(e => e.ID.Equals(id));

            if (ev != null)
            {
                var deps = this.SearchDependencies(ev.ID);

                if (deps != null)
                {
                    ids = deps.ToList();
                    List<string> loopMem = new List<string>();
                    while (newIds != null)
                    {
                        loopMem.Clear();
                        foreach (var i in ids)
                        {
                            deps = this.SearchDependencies(i);

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
                }

                return ids.Any() ? ids : null;
            }

            return null;
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

        private IEnumerable<string> SearchDependencies(string id)
        {
            var ev = Instance.Events.Where(e => e.EndActionStartEventID?.Equals(id) == true);

            if (!ev.Any())
            {
                return null;
            }

            return ev.Select(e => e.ID);
        }
    }
}