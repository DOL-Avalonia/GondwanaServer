using DOL.Database;
using DOL.Events;
using DOL.GameEvents;
using DOL.GS;
using DOL.Territories;
using DOL.GS.GameEvents;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOL.MobGroups;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmteScripts.Managers
{
    public static class TerritoryRelicManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TerritoryRelicManager));
        public static Dictionary<int, TerritoryRelicStatic> ActiveRelics = new Dictionary<int, TerritoryRelicStatic>();
        private static RegionTimer _mapUpdateTimer;

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            SyncRelicCoordinatesWithPads();
            GameEventMgr.AddHandler(GroupMobEvent.MobGroupDead, OnMobGroupDefeated);
            GameEventMgr.AddHandler(GameServerEvent.Started, OnServerStarted);

            if (_mapUpdateTimer == null)
            {
                var reg = WorldMgr.GetRegion(1);
                if (reg != null)
                {
                    _mapUpdateTimer = new RegionTimer(reg.TimeManager);
                    _mapUpdateTimer.Callback = new RegionTimerCallback(t => { UpdateMapPins(); return 2000; });
                    _mapUpdateTimer.Start(2000);
                }
            }
        }

        private static bool IsDungeonLocation(DBMinotaurRelic dbRelic)
        {
            var region = WorldMgr.GetRegion((ushort)dbRelic.SpawnRegion);
            if (region == null) return false;
            var zone = region.GetZone(dbRelic.SpawnX, dbRelic.SpawnY);
            return zone != null && zone.IsDungeon;
        }

        public static void OnServerStarted(DOLEvent e, object sender, EventArgs args)
        {
            HideAllProtectors();

            var reg = WorldMgr.GetRegion(1) ?? WorldMgr.GetAllRegions().FirstOrDefault();
            if (reg != null)
            {
                RegionTimer startupTimer = new RegionTimer(reg.TimeManager);
                startupTimer.Callback = new RegionTimerCallback(timer =>
                {
                    RestoreRelics();
                    return 0;
                });
                startupTimer.Start(2500);
            }
            else
            {
                RestoreRelics();
            }
        }

        public static void HideAllProtectors()
        {
            var allDbRelics = GameServer.Database.SelectAllObjects<DBMinotaurRelic>().Where(r => r.IsTerritoryRelic).ToList();
            foreach (var dbRelic in allDbRelics)
            {
                if (!string.IsNullOrEmpty(dbRelic.ProtectorClassType))
                {
                    if (MobGroupManager.Instance.Groups.TryGetValue(dbRelic.ProtectorClassType, out MobGroup group))
                    {
                        foreach (var npc in group.NPCs.ToList())
                        {
                            if (npc.ObjectState == GameObject.eObjectState.Active)
                                npc.RemoveFromWorld();

                            if (npc.IsRespawning)
                                npc.StopRespawn();
                        }
                    }
                }
            }
        }

        private static void RestoreRelics()
        {
            HideAllProtectors();

            /*if (TerritoryManager.Instance.Territories.Count == 0)
            {
                TerritoryManager.LoadTerritories(null, null, null);
            }*/

            var allDbRelics = GameServer.Database.SelectAllObjects<DBMinotaurRelic>()
                        .Where(r => r.IsTerritoryRelic).ToList();

            var activeDbRelics = allDbRelics.Where(r => !string.IsNullOrEmpty(r.relicTarget) && r.relicTarget != "inactive").ToList();

            if (DOL.GS.Scripts.GvGManager.IsOpen && activeDbRelics.Count == 0)
            {
                log.Info("[TerritoryRelicManager] GvG is active but 0 relics are deployed. Forcing a new randomizer cycle.");
                OnGvGOpened();
                return;
            }

            foreach (var dbRelic in activeDbRelics)
            {
                var relic = new TerritoryRelicStatic(dbRelic);
                ActiveRelics.Add(dbRelic.RelicID, relic);

                if (dbRelic.relicTarget == "carrier" || dbRelic.relicTarget == "-")
                {
                    dbRelic.relicTarget = "outpost";
                    GameServer.Database.SaveObject(dbRelic);
                }

                bool successfullyAttached = false;

                if (dbRelic.relicTarget != "outpost")
                {
                    var pad = WorldMgr.GetAllRegions()
                                      .SelectMany(r => r.Objects)
                                      .OfType<TerRelicPadNPC>()
                                      .FirstOrDefault(p => p.InternalID == dbRelic.relicTarget);

                    if (pad != null)
                    {
                        var territory = TerritoryManager.GetCurrentTerritory(pad);
                        if (territory != null && !territory.IsNeutral())
                        {
                            relic.AttachToTerritory(territory, pad);
                            pad.CurrentRelic = relic;
                            successfullyAttached = true;
                            log.Info($"[TerritoryRelicManager] Relic '{relic.Name}' restored successfully to pad in territory '{territory.Name}'.");
                        }
                        else if (territory == null)
                        {
                            log.Warn($"[TerritoryRelicManager] Territory for pad {pad.Name} was neutral or null! Returning relic '{relic.Name}' to outpost.");
                        }
                        else if (territory.IsNeutral())
                        {
                            log.Info($"[TerritoryRelicManager] Territory '{territory.Name}' has become neutral while offline. Relic returning to outpost.");
                        }
                    }
                    else
                    {
                        log.Warn($"[TerritoryRelicManager] Relic '{relic.Name}' target pad '{dbRelic.relicTarget}' could not be found! Returning to outpost.");
                    }
                }

                if (!successfullyAttached)
                {
                    if (dbRelic.relicTarget != "outpost")
                    {
                        dbRelic.relicTarget = "outpost";
                        GameServer.Database.SaveObject(dbRelic);
                    }
                    relic.Position = Position.Create((ushort)dbRelic.SpawnRegion, dbRelic.SpawnX, dbRelic.SpawnY, dbRelic.SpawnZ + 100, (ushort)dbRelic.SpawnHeading);
                    relic.AddToWorld();
                }
            }

            log.Info($"[TerritoryRelicManager] Resumed {ActiveRelics.Count} active territory relics from previous session.");
        }

        public static void SyncRelicCoordinatesWithPads()
        {
            var allDbRelics = GameServer.Database.SelectAllObjects<DBMinotaurRelic>()
                                .Where(r => r.IsTerritoryRelic)
                                .ToList();

            int updatedCount = 0;

            foreach (var dbRelic in allDbRelics)
            {
                if (dbRelic.SpawnX == 0 && dbRelic.SpawnY == 0 && !string.IsNullOrEmpty(dbRelic.RelicPadSpawn))
                {
                    var padMob = GameServer.Database.SelectObject<Mob>(DB.Column("Mob_ID").IsEqualTo(dbRelic.RelicPadSpawn));

                    if (padMob != null)
                    {
                        dbRelic.SpawnRegion = padMob.Region;
                        dbRelic.SpawnX = padMob.X;
                        dbRelic.SpawnY = padMob.Y;
                        dbRelic.SpawnZ = padMob.Z;
                        dbRelic.SpawnHeading = padMob.Heading;

                        GameServer.Database.SaveObject(dbRelic);
                        updatedCount++;
                    }
                    else
                    {
                        log.Warn($"[TerritoryRelicManager] OUTRELICPAD Mob_ID '{dbRelic.RelicPadSpawn}' not found for Relic '{dbRelic.Name}'. Cannot sync coordinates.");
                    }
                }
            }

            if (updatedCount > 0)
            {
                log.Info($"[TerritoryRelicManager] Successfully synced {updatedCount} territory relic(s) with their pad coordinates in the database.");
            }
        }

        public static void OnGvGOpened()
        {
            log.Info("GvG Opened! Resetting Territory Relics.");

            foreach (var client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player == null) continue;
                foreach (var relic in ActiveRelics.Values)
                {
                    client.Player.Out.SendMinotaurRelicMapRemove((byte)relic.DbRecord.RelicID);
                }
            }

            var previousActiveIds = ActiveRelics.Keys.ToList();

            foreach (var relic in ActiveRelics.Values.ToList())
            {
                relic.Destroy();
            }
            ActiveRelics.Clear();

            HideAllProtectors();

            var allDbRelics = GameServer.Database.SelectAllObjects<DBMinotaurRelic>()
                                .Where(r => r.IsTerritoryRelic).ToList();

            foreach (var dbRelic in allDbRelics)
            {
                dbRelic.relicTarget = "inactive";
                GameServer.Database.SaveObject(dbRelic);
            }

            if (allDbRelics.Count == 0) return;

            int relicsToSpawn = Properties.GVG_RELICS_PER_CYCLE > 0 ? Properties.GVG_RELICS_PER_CYCLE : 2;

            var dungeonRelics = allDbRelics.Where(r => IsDungeonLocation(r)).ToList();
            var overworldRelics = allDbRelics.Where(r => !IsDungeonLocation(r)).ToList();

            int targetDungeonSpawns = relicsToSpawn / 2;
            int targetOverworldSpawns = relicsToSpawn - targetDungeonSpawns;

            if (targetDungeonSpawns > dungeonRelics.Count)
            {
                targetDungeonSpawns = dungeonRelics.Count;
                targetOverworldSpawns = relicsToSpawn - targetDungeonSpawns;
            }
            else if (targetOverworldSpawns > overworldRelics.Count)
            {
                targetOverworldSpawns = overworldRelics.Count;
                targetDungeonSpawns = relicsToSpawn - targetOverworldSpawns;
            }

            var shuffledDungeon = dungeonRelics.OrderBy(x => Util.Random(1000)).Take(targetDungeonSpawns).ToList();
            var shuffledOverworld = overworldRelics.OrderBy(x => Util.Random(1000)).Take(targetOverworldSpawns).ToList();

            var shuffled = new List<DBMinotaurRelic>();
            shuffled.AddRange(shuffledDungeon);
            shuffled.AddRange(shuffledOverworld);
            shuffled = shuffled.OrderBy(x => Util.Random(1000)).ToList();

            var newActiveIds = shuffled.Select(r => r.RelicID).ToList();
            var eventsToBroadcast = new List<KeyValuePair<string, string>>();

            foreach (var dbRelic in shuffled)
            {
                dbRelic.relicTarget = "outpost";
                GameServer.Database.SaveObject(dbRelic);

                var relic = new TerritoryRelicStatic(dbRelic);
                relic.AddToWorld();
                ActiveRelics.Add(dbRelic.RelicID, relic);

                if (previousActiveIds.Contains(dbRelic.RelicID))
                {
                    eventsToBroadcast.Add(new KeyValuePair<string, string>("TerritoryRelics.Manager.Recalled", dbRelic.Name));
                }
                else
                {
                    eventsToBroadcast.Add(new KeyValuePair<string, string>("TerritoryRelics.Manager.Manifested", dbRelic.Name));
                }
            }

            foreach (var oldId in previousActiveIds)
            {
                if (!newActiveIds.Contains(oldId))
                {
                    var oldRelic = allDbRelics.FirstOrDefault(r => r.RelicID == oldId);
                    if (oldRelic != null)
                    {
                        eventsToBroadcast.Add(new KeyValuePair<string, string>("TerritoryRelics.Manager.Dissipated", oldRelic.Name));
                    }
                }
            }

            foreach (var ev in eventsToBroadcast)
            {
                // In-Game Broadcast
                foreach (var client in WorldMgr.GetAllPlayingClients())
                {
                    if (client.Player == null) continue;
                    client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, ev.Key, ev.Value), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
                }

                // Discord Webhook Broadcast
                if (Properties.DISCORD_ACTIVE && !string.IsNullOrEmpty(Properties.DISCORD_WEBHOOK_ID))
                {
                    var hook = new DolWebHook(Properties.DISCORD_WEBHOOK_ID);
                    hook.SendMessage(LanguageMgr.GetTranslation("EN", ev.Key, ev.Value));
                }
            }
        }

        private static void OnMobGroupDefeated(DOLEvent e, object sender, EventArgs args)
        {
            var group = sender as MobGroup;
            if (group == null) return;

            var protectedRelic = ActiveRelics.Values.FirstOrDefault(r => r.DbRecord.ProtectorClassType == group.GroupId);
            if (protectedRelic != null && protectedRelic.IsLocked)
            {
                protectedRelic.Unlock();
            }
        }

        public static void UpdateMapPins()
        {
            foreach (var client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player == null) continue;

                foreach (var relic in ActiveRelics.Values)
                {
                    byte markerId = (byte)relic.DbRecord.RelicID;
                    Position posToDisplay = relic.Position;
                    if (relic.CurrentCarrier != null)
                    {
                        posToDisplay = relic.CurrentCarrier.Position;
                    }

                    bool isAtSpawn = (relic.CurrentTerritory == null && relic.CurrentCarrier == null && !relic.IsDroppedOnGround);
                    bool showMarker = true;

                    if (isAtSpawn)
                    {
                        if (client.Player.CurrentRegionID != posToDisplay.RegionID ||
                            client.Player.Coordinate.DistanceTo(posToDisplay.Coordinate) > 3000)
                        {
                            showMarker = false;
                        }
                    }
                    else
                    {
                        if (client.Player.CurrentRegionID != posToDisplay.RegionID)
                        {
                            showMarker = false;
                        }
                    }

                    if (showMarker)
                    {
                        client.Player.Out.SendMinotaurRelicMapUpdate(markerId, posToDisplay);
                    }
                    else
                    {
                        client.Player.Out.SendMinotaurRelicMapRemove(markerId);
                    }
                }
            }
        }
    }
}