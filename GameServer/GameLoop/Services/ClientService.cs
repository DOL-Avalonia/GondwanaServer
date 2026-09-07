using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using log4net;

namespace DOL.GS
{
    public sealed class ClientService : GameServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private static readonly uint MIN_PLAYER_WORLD_UPDATE_RATE = 50;
        private static readonly uint MIN_NPC_UPDATE_RATE = 1000;
        private static readonly uint MIN_ITEM_UPDATE_RATE = 10000;
        private static readonly uint MIN_HOUSING_UPDATE_RATE = 10000;
        private static readonly uint MIN_PLAYER_UPDATE_RATE = 1000;

        private ServiceObjectView<GameClient> _view;

        public static ClientService Instance { get; } = new();

        private static uint GetPlayerWorldUpdateInterval
            => Math.Max(ServerProperties.Properties.WORLD_PLAYER_UPDATE_INTERVAL, MIN_PLAYER_WORLD_UPDATE_RATE);
        private static uint GetPlayerNPCUpdateInterval
            => Math.Max(ServerProperties.Properties.WORLD_NPC_UPDATE_INTERVAL, MIN_NPC_UPDATE_RATE);
        private static uint GetPlayerItemUpdateInterval
            => Math.Max(ServerProperties.Properties.WORLD_OBJECT_UPDATE_INTERVAL, MIN_ITEM_UPDATE_RATE);
        private static uint GetPlayerHousingUpdateInterval
            => Math.Max(ServerProperties.Properties.WORLD_OBJECT_UPDATE_INTERVAL, MIN_HOUSING_UPDATE_RATE);
        private static uint GetPlayertoPlayerUpdateInterval
            => Math.Max(ServerProperties.Properties.WORLD_PLAYERTOPLAYER_UPDATE_INTERVAL, MIN_PLAYER_UPDATE_RATE);

        public override void Tick()
        {
            ProcessPostedActionsParallel();

            try
            {
                _view = ServiceObjectStore.UpdateAndGetView<GameClient>(ServiceObjectType.Client);
            }
            catch (Exception e)
            {
                log.Error("UpdateAndGetView failed. Skipping this tick.", e);
                return;
            }

            _view.ExecuteForEach(TickInternal);
        }

        private static void TickInternal(GameClient client)
        {
            try
            {
                if (client == null)
                    return;

                try
                {
                    GamePlayer player = client.Player;

                    if (client.ClientState == GameClient.eClientState.Playing && player == null)
                    {
                        GameServer.Instance.Disconnect(client);
                        return;
                    }

                    if (client.ClientState != GameClient.eClientState.Playing || player == null || player.ObjectState != GameObject.eObjectState.Active)
                    {
                        if (client.LastWorldUpdateRegion != null)
                        {
                            client.GameObjectUpdateArray.Clear();
                            client.HouseUpdateArray.Clear();
                            client.LastWorldUpdateRegion = null;
                        }

                        return;
                    }

                    if (!GameServiceUtils.ShouldTick(client.NextWorldUpdateTick))
                        return;

                    if (player.CurrentRegion != client.LastWorldUpdateRegion)
                    {
                        client.GameObjectUpdateArray.Clear();
                        client.HouseUpdateArray.Clear();
                        client.LastWorldUpdateRegion = player.CurrentRegion;
                    }

                    TickMonitor monitor = new();
                    UpdatePlayerWorld(player, (uint)GameLoop.GameLoopTime);
                    client.NextWorldUpdateTick = GameLoop.GameLoopTime + GetPlayerWorldUpdateInterval;

                    if (monitor.IsLongTick(out long elapsedMs) && log.IsWarnEnabled)
                        log.Warn($"Long tick");
                }
                finally
                {
                    try { client.PacketProcessor?.SendPendingPackets(); }
                    catch (Exception e) { if (log.IsErrorEnabled) log.Error("SendPendingPackets", e); }
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error($"Error", e);
            }
        }

        private static void UpdatePlayerWorld(GamePlayer player, uint nowTicks)
        {
            player.RecordPositionHistory();   // ← RewindTime source of truth, unchanged.

            if (ServerProperties.Properties.WORLD_PLAYERTOPLAYER_UPDATE_INTERVAL > 0)
                UpdatePlayerOtherPlayers(player, nowTicks);
            if (ServerProperties.Properties.WORLD_NPC_UPDATE_INTERVAL > 0)
                UpdatePlayerNPCs(player, nowTicks);
            if (ServerProperties.Properties.WORLD_OBJECT_UPDATE_INTERVAL > 0)
            {
                UpdatePlayerItems(player, nowTicks);
                UpdatePlayerDoors(player, nowTicks);
                UpdatePlayerHousing(player, nowTicks);
            }
        }

        private static void UpdatePlayerOtherPlayers(GamePlayer player, uint nowTicks)
        {
            var players = new HashSet<GamePlayer>(
                player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE).Cast<GamePlayer>());
            try
            {
                foreach (var objEntry in player.Client.GameObjectUpdateArray.ToArray())
                {
                    var objKey = objEntry.Key;
                    if (objKey is null) continue;
                    GameObject obj = WorldMgr.GetRegion(objKey.Item1)?.GetObject(objKey.Item2);
                    if (obj is GamePlayer lostPlayer
                        && (!players.Contains(lostPlayer)
                            || !(lostPlayer.IsVisibleTo(player) && (!lostPlayer.IsStealthed || player.CanDetect(lostPlayer))))
                        && (nowTicks - objEntry.Value) >= GetPlayertoPlayerUpdateInterval)
                    {
                        if (lostPlayer.ActiveBanner != null)
                            player.Client.Out.SendRvRGuildBanner(lostPlayer, null);
                        if (lostPlayer.IsVisibleTo(player) && (!lostPlayer.IsStealthed || player.CanDetect(lostPlayer)))
                            player.Client.Out.SendPlayerForgedPosition(lostPlayer);
                        player.Client.GameObjectUpdateArray.TryRemove(objKey, out _);
                    }
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error cleaning OtherPlayers cache Player: {0}, Ex: {1}", player.Name, e);
            }
            try
            {
                foreach (GamePlayer lplayer in players)
                {
                    if (lplayer == null) continue;
                    if (player.Client.GameObjectUpdateArray.TryGetValue(
                        new Tuple<ushort, ushort>(lplayer.CurrentRegionID, (ushort)lplayer.ObjectID), out long lastUpdate))
                    {
                        if ((nowTicks - lastUpdate) >= GetPlayertoPlayerUpdateInterval)
                            player.Client.Out.SendPlayerForgedPosition(lplayer);
                    }
                    else
                        player.Client.Out.SendPlayerForgedPosition(lplayer);
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error updating OtherPlayers Player: {0}, Ex: {1}", player.Name, e);
            }
        }

        private static void UpdatePlayerNPCs(GamePlayer player, uint nowTicks)
        {
            if (player.LastNPCUpdateTick + 100 > nowTicks)
                return;
            bool all = player.LastNPCUpdateAllTick + 1000 < nowTicks;
            player.LastNPCUpdateTick = nowTicks;
            if (all)
                player.LastNPCUpdateAllTick = nowTicks;

            var query = player.GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE)
                .Cast<GameNPC>().Where(n => n.IsVisibleTo(player));
            if (!all)
                query = query.Where(n => n.IsMoving);

            foreach (GameNPC npc in query)
            {
                player.Client.Out.SendObjectUpdate(npc);
                npc.RecordPositionHistory();   // ← RewindTime for NPCs, unchanged.
            }
        }

        private static void UpdatePlayerItems(GamePlayer player, uint nowTicks)
        {
            var objs = player.GetItemsInRadius(WorldMgr.OBJ_UPDATE_DISTANCE).Cast<GameStaticItem>().ToArray();
            try
            {
                foreach (var objEntry in player.Client.GameObjectUpdateArray.ToArray())
                {
                    var objKey = objEntry.Key;
                    if (objKey is null) continue;
                    GameObject obj = WorldMgr.GetRegion(objKey.Item1)?.GetObject(objKey.Item2);
                    if (obj is GameStaticItem item
                        && (!objs.Contains(item) || !item.IsVisibleTo(player))
                        && (nowTicks - objEntry.Value) >= GetPlayerItemUpdateInterval)
                        player.Client.GameObjectUpdateArray.TryRemove(objKey, out long _);
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error cleaning Item cache Player: {0}, Ex: {1}", player.Name, e);
            }
            try
            {
                foreach (GameStaticItem lobj in objs)
                {
                    if (player.Client.GameObjectUpdateArray.TryGetValue(
                        new Tuple<ushort, ushort>(lobj.CurrentRegionID, (ushort)lobj.ObjectID), out long lastUpdate))
                    {
                        if ((nowTicks - lastUpdate) >= GetPlayerItemUpdateInterval)
                            player.Client.Out.SendObjectCreate(lobj);
                    }
                    else
                        player.Client.Out.SendObjectCreate(lobj);
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error updating Items Player: {0}, Ex: {1}", player.Name, e);
            }
        }

        private static void UpdatePlayerDoors(GamePlayer player, uint nowTicks)
        {
            var doors = player.GetDoorsInRadius(WorldMgr.OBJ_UPDATE_DISTANCE)
                .Cast<IDoor>().OfType<GameObject>().Where(o => o.IsVisibleTo(player)).ToArray();
            try
            {
                foreach (var objEntry in player.Client.GameObjectUpdateArray.ToArray())
                {
                    var objKey = objEntry.Key;
                    if (objKey is null) continue;
                    GameObject obj = WorldMgr.GetRegion(objKey.Item1)?.GetObject(objKey.Item2);
                    if (obj is IDoor && !doors.Contains(obj)
                        && (nowTicks - objEntry.Value) >= GetPlayerItemUpdateInterval)
                        player.Client.GameObjectUpdateArray.TryRemove(objKey, out _);
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error cleaning Doors cache Player: {0}, Ex: {1}", player.Name, e);
            }
            try
            {
                foreach (IDoor ldoor in doors)
                {
                    if (player.Client.GameObjectUpdateArray.TryGetValue(
                        new Tuple<ushort, ushort>(((GameObject)ldoor).CurrentRegionID, (ushort)ldoor.ObjectID),
                        out long lastUpdate))
                    {
                        if ((nowTicks - lastUpdate) >= GetPlayerItemUpdateInterval)
                            player.SendDoorUpdate(ldoor);
                    }
                    else
                        player.SendDoorUpdate(ldoor);
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error updating Doors Player: {0}, Ex: {1}", player.Name, e);
            }
        }

        // Kept public static: external callers previously used WorldUpdateThread.UpdatePlayerHousing.
        public static void UpdatePlayerHousing(GamePlayer player, uint nowTicks)
        {
            if (player.CurrentRegion == null || !player.CurrentRegion.HousingEnabled)
                return;

            IDictionary<int, House> housesDict = HouseMgr.GetHouses(player.CurrentRegionID);
            if (housesDict == null)
                return;

            var houses = housesDict.Values.Where(h => h != null
                && player.Coordinate.DistanceTo(h.Position) <= HousingConstants.HouseViewingDistance).ToArray();
            try
            {
                foreach (var houseEntry in player.Client.HouseUpdateArray.ToArray())
                {
                    var houseKey = houseEntry.Key;
                    House house = HouseMgr.GetHouse(houseKey.Item1, houseKey.Item2);
                    if (!houses.Contains(house)
                        && (nowTicks - houseEntry.Value) >= (GetPlayerHousingUpdateInterval >> 2))
                        player.Client.HouseUpdateArray.TryRemove(houseKey, out _);
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error cleaning House cache Player: {0}, Ex: {1}", player.Name, e);
            }
            try
            {
                foreach (House lhouse in houses)
                {
                    if (player.Client.HouseUpdateArray.TryGetValue(
                        new Tuple<ushort, ushort>(lhouse.RegionID, (ushort)lhouse.HouseNumber), out long lastUpdate))
                    {
                        if ((nowTicks - lastUpdate) >= GetPlayerHousingUpdateInterval)
                            player.Client.Out.SendHouseOccupied(lhouse, lhouse.IsOccupied);
                    }
                    else
                    {
                        player.Client.Out.SendHouse(lhouse);
                        player.Client.Out.SendGarden(lhouse);
                        player.Client.Out.SendHouseOccupied(lhouse, lhouse.IsOccupied);
                    }
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error updating Houses Player: {0}, Ex: {1}", player.Name, e);
            }
        }
    }
}