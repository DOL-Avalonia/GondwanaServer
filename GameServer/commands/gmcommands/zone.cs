/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.Linq;
using System.Collections.Generic;

using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Database;
using DOL.GS.Geometry;
using Google.Protobuf.WellKnownTypes;
using System.Security.Policy;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&zone",
        ePrivLevel.GM,
        "/zone info",
        "/zone divingflag <0 = use region, 1 = on, 2 = off>",
        "/zone waterlevel <#>",
        "/zone maxflyaltitude <#> - Set max fly altitude for epic mounts in this zone",
        "/zone bonus <zoneID|current> <xpBonus> <rpBonus> <bpBonus> <coinBonus> <Save? (true/false)>",
        "/zone allowMagicalItem <true|false> Should Players use Magical items in this zone",
        "/zone allowReputation <true|false> - Allow or disallow the reputation system in this zone",
        "/zone tensionrate <float> - Set the tension rate for this zone",
        "/zone isdungeon <true|false> - Flag the zone as Dungeon or Overworld")]
    public class ZoneCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            try
            {
                Zone zone;

                if (args[1].ToLower() == "info")
                {
                    var info = new List<string>();
                    info.Add(" ");
                    info.Add(" NPCs in zone:");
                    info.Add(" Alb: " + client.Player.CurrentZone.GetNPCsOfZone(eRealm.Albion).Count);
                    info.Add(" Hib: " + client.Player.CurrentZone.GetNPCsOfZone(eRealm.Hibernia).Count);
                    info.Add(" Mid: " + client.Player.CurrentZone.GetNPCsOfZone(eRealm.Midgard).Count);
                    info.Add(" None: " + client.Player.CurrentZone.GetNPCsOfZone(eRealm.None).Count);
                    info.Add(" ");
                    info.Add(string.Format(" Objects in zone: {0}, Total allowed for region: {1}", client.Player.CurrentZone.TotalNumberOfObjects, ServerProperties.Properties.REGION_MAX_OBJECTS));
                    info.Add(" ");
                    info.Add(" Zone Description: " + client.Player.CurrentZone.Description);
                    info.Add(" Zone Realm: " + GlobalConstants.RealmToName(client.Player.CurrentZone.Realm));
                    info.Add(" Zone ID: " + client.Player.CurrentZone.ID);
                    info.Add(" Zone IsDungeon: " + client.Player.CurrentZone.IsDungeon);
                    info.Add(" Zone SkinID: " + client.Player.CurrentZone.ZoneSkinID);
                    info.Add(" Zone X: " + client.Player.CurrentZone.Offset.X);
                    info.Add(" Zone Y: " + client.Player.CurrentZone.Offset.Y);
                    info.Add(" Zone Width: " + client.Player.CurrentZone.Width);
                    info.Add(" Zone Height: " + client.Player.CurrentZone.Height);
                    info.Add(" Zone DivingEnabled: " + client.Player.CurrentZone.IsDivingEnabled);
                    info.Add(" Zone Waterlevel: " + client.Player.CurrentZone.Waterlevel);
                    info.Add(" Zone MaxFlyAltitude: " + client.Player.CurrentZone.MaxFlyAltitude);
                    info.Add(" Zone AllowMagical Items: " + client.Player.CurrentZone.AllowMagicalItem);

                    bool internalFlag = client.Player.CurrentZone.AllowReputation;
                    bool displayedFlag = !internalFlag;
                    info.Add(" Zone AllowReputation: " + displayedFlag + " (" + (internalFlag ? "Reputation System Not Active" : "Reputation Decreases Allowed") + ")");
                    info.Add(" Zone TensionRate: " + client.Player.CurrentZone.TensionRate);

                    zone = WorldMgr.GetZone(client.Player.CurrentZone.ID);
                    var dbZone = DOLDB<Zones>.SelectObject(DB.Column(nameof(Zones.ZoneID)).IsEqualTo(zone.ID).And(DB.Column(nameof(Zones.RegionID)).IsEqualTo(zone.ZoneRegion.ID)));

                    if (dbZone != null)
                    {
                        string dflag = "Use Region";
                        if (dbZone.DivingFlag == 1)
                            dflag = "Always Yes";
                        else if (dbZone.DivingFlag == 2)
                            dflag = "Always No";

                        info.Add(" Zone DivingFlag: " + dbZone.DivingFlag + " (" + dflag + ")");
                    }

                    client.Out.SendCustomTextWindow("[ " + client.Player.CurrentZone.Description + " ]", info);
                    return;
                }

                if (args[1].ToLower() == "divingflag")
                {
                    zone = WorldMgr.GetZone(client.Player.CurrentZone.ID);
                    byte divingFlag = Convert.ToByte(args[2]);
                    if (divingFlag > 2)
                    {
                        DisplaySyntax(client);
                        return;
                    }

                    if (divingFlag == 0)
                        zone.IsDivingEnabled = client.Player.CurrentRegion.IsRegionDivingEnabled;
                    else if (divingFlag == 1)
                        zone.IsDivingEnabled = true;
                    else
                        zone.IsDivingEnabled = false;

                    var dbZone = DOLDB<Zones>.SelectObject(DB.Column(nameof(Zones.ZoneID)).IsEqualTo(zone.ID).And(DB.Column(nameof(Zones.RegionID)).IsEqualTo(zone.ZoneRegion.ID)));
                    dbZone.DivingFlag = divingFlag;
                    GameServer.Database.SaveObject(dbZone);

                    // Update water level and diving flag for the new zone
                    client.Out.SendPlayerPositionAndObjectID();

                    string dflag = "Use Region";
                    if (dbZone.DivingFlag == 1)
                        dflag = "Always Yes";
                    else if (dbZone.DivingFlag == 2)
                        dflag = "Always No";

                    DisplayMessage(client, string.Format("Diving Flag for {0}:{1} changed to {2} ({3}).", zone.ID, zone.Description, divingFlag, dflag));
                    return;
                }

                if (args[1].ToLower() == "maxflyaltitude" && args.Length == 3)
                {
                    zone = WorldMgr.GetZone(client.Player.CurrentZone.ID);
                    if (!int.TryParse(args[2], out int maxFlyAlt)) return;
                    zone.MaxFlyAltitude = maxFlyAlt;

                    var dbZone = DOLDB<Zones>.SelectObject(DB.Column(nameof(Zones.ZoneID)).IsEqualTo(zone.ID).And(DB.Column(nameof(Zones.RegionID)).IsEqualTo(zone.ZoneRegion.ID)));
                    if (dbZone != null)
                    {
                        dbZone.MaxFlyAltitude = maxFlyAlt;
                        GameServer.Database.SaveObject(dbZone);
                    }
                    DisplayMessage(client, string.Format("MaxFlyAltitude for {0}:{1} changed to {2}.", zone.ID, zone.Description, maxFlyAlt));
                    return;
                }

                if (args[1].ToLower() == "waterlevel")
                {
                    zone = WorldMgr.GetZone(client.Player.CurrentZone.ID);
                    int waterlevel = Convert.ToInt32(args[2]);
                    zone.Waterlevel = waterlevel;

                    var dbZone = DOLDB<Zones>.SelectObject(DB.Column(nameof(Zones.ZoneID)).IsEqualTo(zone.ID).And(DB.Column(nameof(Zones.RegionID)).IsEqualTo(zone.ZoneRegion.ID)));
                    dbZone.WaterLevel = waterlevel;
                    GameServer.Database.SaveObject(dbZone);

                    // Update water level and diving flag for the new zone
                    client.Out.SendPlayerPositionAndObjectID();
                    
                    client.Player.MoveTo(client.Player.Position + Vector.Create(z: 1));

                    DisplayMessage(client, string.Format("Waterlevel for {0}:{1} changed to {2}.", zone.ID, zone.Description, waterlevel));
                    return;
                }

                if (args.Length == 3 && args[1].ToLowerInvariant() == "allowmagicalitem" && bool.TryParse(args[2], out bool allowMagicalItem))
                {
                    client.Player.CurrentZone.AllowMagicalItem = allowMagicalItem;

                    //update current players
                    foreach (var cl in WorldMgr.GetAllPlayingClients().Where(c => c.Player.CurrentZone.ID == client.Player.CurrentZone.ID))
                    {
                        cl.Player.CurrentZone.AllowMagicalItem = allowMagicalItem;
                    }

                    //update db
                    if (WorldMgr.Zones.ContainsKey(client.Player.CurrentZone.ID))
                    {
                        WorldMgr.Zones[client.Player.CurrentZone.ID].AllowMagicalItem = allowMagicalItem;
                        var zoneDb = GameServer.Database.FindObjectByKey<Zones>(client.Player.CurrentZone.ID);

                        if (zoneDb != null)
                        {
                            zoneDb.AllowMagicalItem = allowMagicalItem;
                            GameServer.Database.SaveObject(zoneDb);
                            client.Out.SendMessage("Zone saved with AllowMagicalItem : " + allowMagicalItem, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }

                    return;
                }

                if (args.Length == 3 && args[1].ToLowerInvariant() == "allowreputation"
    && bool.TryParse(args[2], out bool userTypedValue))
                {
                    bool actualInternalValue = !userTypedValue;

                    zone = WorldMgr.GetZone(client.Player.CurrentZone.ID);
                    zone.AllowReputation = actualInternalValue;

                    // Update existing players in the zone
                    foreach (var plr in WorldMgr.GetAllPlayingClients().Where(c => c.Player.CurrentZone.ID == zone.ID))
                    {
                        plr.Player.CurrentZone.AllowReputation = actualInternalValue;
                    }

                    // Update DB
                    var dbZone = DOLDB<Zones>.SelectObject(DB.Column(nameof(Zones.ZoneID)).IsEqualTo(zone.ID).And(DB.Column(nameof(Zones.RegionID)).IsEqualTo(zone.ZoneRegion.ID)));
                    if (dbZone != null)
                    {
                        dbZone.AllowReputation = actualInternalValue;
                        GameServer.Database.SaveObject(dbZone);
                    }

                    DisplayMessage(client,$"AllowReputation for Zone {zone.ID}:{zone.Description} set to {userTypedValue}.");
                    return;
                }

                if (args.Length == 3 && args[1].ToLowerInvariant() == "tensionrate")
                {
                    zone = WorldMgr.GetZone(client.Player.CurrentZone.ID);
                    if (!float.TryParse(args[2], out float tensionRate))
                    {
                        DisplayMessage(client, "Invalid tension rate (must be a float).");
                        return;
                    }

                    // Set the tension rate in memory
                    zone.TensionRate = tensionRate;

                    // Update DB
                    var dbZone = DOLDB<Zones>.SelectObject(DB.Column(nameof(Zones.ZoneID)).IsEqualTo(zone.ID).And(DB.Column(nameof(Zones.RegionID)).IsEqualTo(zone.ZoneRegion.ID)));
                    if (dbZone != null)
                    {
                        dbZone.TensionRate = tensionRate;
                        GameServer.Database.SaveObject(dbZone);
                    }

                    DisplayMessage(client,$"TensionRate for {zone.ID}:{zone.Description} changed to {tensionRate}.");
                    return;
                }

                if (args.Length == 3 && args[1].ToLowerInvariant() == "isdungeon"
                    && bool.TryParse(args[2], out bool isDungeon))
                {
                    zone = WorldMgr.GetZone(client.Player.CurrentZone.ID);
                    zone.IsDungeon = isDungeon;

                    // Update DB
                    var dbZone = DOLDB<Zones>.SelectObject(DB.Column(nameof(Zones.ZoneID)).IsEqualTo(zone.ID).And(DB.Column(nameof(Zones.RegionID)).IsEqualTo(zone.ZoneRegion.ID)));
                    if (dbZone != null)
                    {
                        dbZone.IsDungeon = isDungeon;
                        GameServer.Database.SaveObject(dbZone);
                    }

                    DisplayMessage(client, $"Zone {zone.ID}:{zone.Description} IsDungeon set to {isDungeon}.");
                    return;
                }

                //make sure that only numbers are used to avoid errors.
                foreach (char c in string.Join(" ", args, 2, 4))
                {
                    if (char.IsLetter(c))
                    {
                        DisplaySyntax(client);
                        return;
                    }
                }

                switch (args[1].ToString().ToLower())
                {
                    case "c":
                    case "cu":
                    case "cur":
                    case "curr":
                    case "curre":
                    case "current":
                        {
                            zone = WorldMgr.GetZone(client.Player.CurrentZone.ID);
                        }
                        break;
                    default:
                        {
                            //make sure that its a number again.
                            foreach (char c in args[1])
                            {
                                if (!(char.IsNumber(c)))
                                {
                                    DisplaySyntax(client);
                                    return;
                                }
                            }

                            if (WorldMgr.GetZone(ushort.Parse(args[1])) == null)
                            {
                                DisplayMessage(client, "No Zone with that ID was found!");
                                return;
                            }
                            zone = WorldMgr.GetZone(ushort.Parse(args[1]));
                        }
                        break;
                }

                zone.BonusExperience = int.Parse(args[2]);
                zone.BonusRealmpoints = int.Parse(args[3]);
                zone.BonusBountypoints = int.Parse(args[4]);
                zone.BonusCoin = int.Parse(args[5]);

                if (args[6].ToLower().StartsWith("t"))
                {
                    client.Player.TempProperties.setProperty("ZONE_BONUS_SAVE", zone);
                    client.Player.Out.SendCustomDialog(string.Format("Are you sure you want to overwrite {0} in the database?", zone.Description), new CustomDialogResponse(AreYouSure));
                }
                else
                {
                    client.Player.Out.SendCustomDialog(string.Format("The zone settings for {0} will be reverted back to database settings on server restart.", zone.Description), null);
                }
            }
            catch
            {
                DisplaySyntax(client);
            }
        }

        public static void AreYouSure(GamePlayer player, byte response)
        {
            //here we get the zones new info.
            Zone zone = player.TempProperties.getProperty<Zone>("ZONE_BONUS_SAVE");

            if (response != 0x01)
            {
                player.Out.SendCustomDialog(string.Format("{0}'s bonuses will not be saved to the database!", zone.Description), null);
                player.TempProperties.removeProperty("ZONE_BONUS_SAVE");
                return;
            }

            //find the zone.
            var dbZone = DOLDB<Zones>.SelectObject(DB.Column(nameof(Zones.ZoneID)).IsEqualTo(zone.ID).And(DB.Column(nameof(Zones.RegionID)).IsEqualTo(zone.ZoneRegion.ID)));
            //update the zone bonuses.
            dbZone.Bountypoints = zone.BonusBountypoints;
            dbZone.Realmpoints = zone.BonusRealmpoints;
            dbZone.Coin = zone.BonusCoin;
            dbZone.Experience = zone.BonusExperience;
            GameServer.Database.SaveObject(dbZone);

            player.Out.SendCustomDialog(string.Format("{0}'s new zone bonuses have been updated to the database and changes have already taken effect!", zone.Description), null);

            //remove the property.
            player.TempProperties.removeProperty("ZONE_BONUS_SAVE");
        }
    }
}
