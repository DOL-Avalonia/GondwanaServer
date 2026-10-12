using System;
using System.Collections.Concurrent;
using System.Reflection;
using DOL.Database;
using DOL.GS.Geometry;
using DOL.GS.Quests;
using DOL.GS.ServerRules;
using log4net;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.PlayerRegionChangeRequest, "Player Region Change Request handler.", eClientStatus.PlayerInGame)]
    public class PlayerRegionChangeRequestHandler : PacketHandler
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        // One handler instance serves every client: thread-safe cache of jump point handlers.
        private static readonly ConcurrentDictionary<string, IJumpPointHandler> m_customJumpPointHandlers = new();

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            GamePlayer player = client.Player;
            if (player?.CurrentRegion == null)
                return;

            ushort jumpSpotId = client.Version < GameClient.eClientVersion.Version1126 ? packet.ReadShort() : packet.ReadShortLowEndian();

            eRealm targetRealm = player.Realm;
            if (player.CurrentRegion.Expansion == (int)eClientExpansion.TrialsOfAtlantis && player.CurrentZone.Realm != eRealm.None)
                // if we are in TrialsOfAtlantis then base the target jump on the current region realm instead of the players realm
                // this is only used if zone table has the proper realms defined, otherwise it reverts to old behavior - Tolakram
                targetRealm = player.CurrentZone.Realm;

            // One small DB read per zone jump (rare event). Zone points are static data; cache them later if it ever shows up in the long-tick log.
            var filterRealm = DB.Column(nameof(ZonePoint.Realm)).IsEqualTo((byte)targetRealm)
                .Or(DB.Column(nameof(ZonePoint.Realm)).IsEqualTo(0))
                .Or(DB.Column(nameof(ZonePoint.Realm)).IsNull());
            ZonePoint zonePoint = DOLDB<ZonePoint>.SelectObject(DB.Column(nameof(ZonePoint.Id)).IsEqualTo(jumpSpotId).And(filterRealm));

            if (zonePoint == null || zonePoint.TargetRegion == 0)
            {
                ChatUtil.SendDebugMessage(client, $"Invalid Jump (ZonePoint table): [{jumpSpotId}]{(zonePoint == null ? ". Entry missing!" : ". TargetRegion is 0!")}");
                zonePoint = new ZonePoint { Id = jumpSpotId };
                Log.Error($"ZonePoint {jumpSpotId} at Region {player.CurrentRegionID} and coordinates ({player.Coordinate}) on client {client.Version} missing. Either ZonePoint missing or RegionChangeRequestHandler needs to be updated.");
            }

            if (client.Account.PrivLevel > 1)
            {
                client.Out.SendMessage($"JumpSpotID = {jumpSpotId}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage($"ZonePoint Target: Region = {zonePoint.TargetRegion}, ClassType = '{zonePoint.ClassType}'", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            //Dinberg: Fix - some jump points are handled code side, such as instances.
            //As such, region MAY be zero in the database, so this causes an issue.
            Region targetRegion = zonePoint.TargetRegion != 0 ? WorldMgr.GetRegion(zonePoint.TargetRegion) : null;

            if (targetRegion != null && !player.CurrentRegion.IsCustom && targetRegion.IsDisabled)
            {
                // check for target region disabled if player is in a standard region
                // otherwise the custom region should handle OnZonePoint for this check
                if (player.Mission is not TaskDungeonMission mission || mission.TaskRegion.Skin != targetRegion.Skin)
                {
                    client.Out.SendMessage("This region has been disabled!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    if (client.Account.PrivLevel == 1)
                        return;
                }
            }

            if (!player.CurrentRegion.OnZonePoint(player, zonePoint))
                return;

            Battleground bg = GameServer.KeepManager.GetBattleground(zonePoint.TargetRegion);
            if (bg != null && player.Level < bg.MinLevel && player.Level > bg.MaxLevel && player.RealmLevel >= bg.MaxRealmLevel)
                return;

            IJumpPointHandler customHandler = null;

            if (!string.IsNullOrEmpty(zonePoint.ClassType))
            {
                if (m_customJumpPointHandlers.TryGetValue(zonePoint.ClassType, out customHandler) && customHandler.GetType().FullName != zonePoint.ClassType)
                    customHandler = null;

                if (customHandler == null)
                {
                    //Dinberg - Instances need to use a special handler. This is because some instances will result
                    //in duplicated zonepoints, such as if Tir Na Nog were to be instanced for a quest.
                    string typeName = player.CurrentRegion.IsInstance ? "DOL.GS.ServerRules.InstanceDoorJumpPoint" : zonePoint.ClassType;
                    Type type = ScriptMgr.GetType(typeName);

                    if (type == null)
                        Log.Error($"jump point {zonePoint.Id}: class {typeName} not found!");
                    else if (!typeof(IJumpPointHandler).IsAssignableFrom(type))
                        Log.Error($"jump point {zonePoint.Id}: class {typeName} must implement IJumpPointHandler interface!");
                    else
                    {
                        try
                        {
                            customHandler = (IJumpPointHandler)Activator.CreateInstance(type);
                        }
                        catch (Exception e)
                        {
                            customHandler = null;
                            Log.Error($"jump point {zonePoint.Id}: error creating a new instance of jump point handler {typeName}", e);
                        }
                    }
                }

                if (customHandler != null)
                    m_customJumpPointHandlers[zonePoint.ClassType] = customHandler;
            }

            if (targetRegion != null && targetRegion.Expansion > (int)client.ClientType)
            {
                player.Out.SendMessage($"Destination region ({targetRegion.Description}) is not supported by your client type.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (customHandler != null)
            {
                try
                {
                    if (!customHandler.IsAllowedToJump(zonePoint, player))
                        return;
                }
                catch (Exception e)
                {
                    if (Log.IsErrorEnabled)
                        Log.Error($"Jump point handler ({zonePoint.ClassType})", e);
                    player.Out.SendMessage($"exception in jump point ({zonePoint.Id}) handler...", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }

            player.MoveTo(zonePoint.GetTargetPosition());
        }
    }
}