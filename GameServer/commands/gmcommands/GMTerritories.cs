using DOL.Database;
using DOL.GameEvents;
using DOL.Geometry;
using DOL.GS;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using DOL.MobGroups;
using DOL.Territory;
using DOLDatabase.Tables;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.commands.gmcommands
{
    [CmdAttribute(
        "&GMTerritories",
        ePrivLevel.GM,
        "Commands.GM.GMTerritories.Description",
        "Commands.GM.GMTerritories.Usage.GroupMob",
        "Commands.GM.GMTerritories.Usage.Key",
        "Commands.GM.GMTerritories.Usage.KeyChance",
        "Commands.GM.GMTerritories.Usage.BonusAdd",
        "Commands.GM.GMTerritories.Usage.BonusRemove",
         "Commands.GM.GMTerritories.Resist")]
    public class GMTerritoires
        : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            Territory.Territory territory = null;

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }
            string areaId = null;


            switch (args[1].ToLowerInvariant())
            {

                case "add":

                    if (args.Length < 6)
                    {
                        DisplaySyntax(client);
                        return;
                    }

                    areaId = args[2];
                    ushort zoneId = 0;
                    string name = args[4];
                    string groupId = args[5];

                    if (string.IsNullOrEmpty(areaId) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(groupId))
                    {
                        DisplaySyntax(client);
                        break;
                    }

                    if (ushort.TryParse(args[3], out zoneId))
                    {
                        var areaDb = GameServer.Database.SelectObjects<DBArea>(DB.Column("id").IsEqualTo(areaId))?.FirstOrDefault();

                        if (areaDb == null)
                        {
                            client.Out.SendMessage("Impossible de trouver l'area avec l'id : " + areaId, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        if (!WorldMgr.Regions.ContainsKey(areaDb.Region))
                        {
                            client.Out.SendMessage("Impossible de trouver la region : " + areaDb.Region, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        var zone = WorldMgr.Regions[areaDb.Region].Zones.FirstOrDefault(z => z.ID.Equals(zoneId));

                        if (zone == null)
                        {
                            client.Out.SendMessage("Impossible de trouver la zone : " + zoneId + " dans la region " + areaDb.Region, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        if (!MobGroupManager.Instance.Groups.ContainsKey(groupId))
                        {
                            client.Out.SendMessage("Impossible de trouver le groupeId : " + groupId, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        var areas = WorldMgr.Regions[areaDb.Region].GetAreasOfZone(zone, new System.Numerics.Vector3(areaDb.X, areaDb.Y, 0), false);

                        if (areas == null)
                        {
                            client.Out.SendMessage("Impossible de trouver des areas dans la zone : " + zone.ID, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        var area = areas.FirstOrDefault(a => ((AbstractArea)a).Description.Equals(areaDb.Description));

                        if (area == null)
                        {
                            client.Out.SendMessage($"Impossible de trouver des l'area {areaDb.Description} dans la zone {zone.ID}", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        var mobinfo = TerritoryManager.Instance.FindBossFromGroupId(groupId);

                        if (mobinfo.Error != null)
                        {
                            client.Out.SendMessage(mobinfo.Error, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        bool saved = TerritoryManager.Instance.AddTerritory(area, areaId, areaDb.Region, groupId, mobinfo.Mob);

                        if (!saved)
                        {
                            client.Out.SendMessage("Le Territoire " + name + " n'a pas pu etre sauvegardé dans la base de données.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        client.Out.SendMessage("Le Territoire " + name + " a été créé correctement.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                    }

                    break;

                case "info":
                    if (args.Length == 3)
                    {
                        areaId = args[2];

                        if (string.IsNullOrEmpty(areaId))
                        {
                            DisplaySyntax(client);
                            break;
                        }

                        territory = TerritoryManager.Instance.Territories.FirstOrDefault(t => t.AreaId.Equals(areaId));

                        if (territory == null)
                        {
                            client.Out.SendMessage("Le Territoire avec AreaId " + areaId + " n'a pas été trouvé.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        IList<string> infos = territory.GetInformations();

                        client.Out.SendCustomTextWindow($"[ Territoire {territory.Name} ]", infos);
                        break;
                    }

                    DisplaySyntax(client);
                    break;

                case "clear":
                    if (args.Length == 3)
                    {
                        areaId = args[2];

                        if (string.IsNullOrEmpty(areaId))
                        {
                            DisplaySyntax(client);
                            break;
                        }

                        territory = TerritoryManager.Instance.Territories.FirstOrDefault(t => t.AreaId.Equals(areaId));

                        if (territory == null)
                        {
                            client.Out.SendMessage("Le Territoire avec AreaId " + areaId + " n'a pas été trouvé.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                            break;
                        }

                        TerritoryManager.Instance.RestoreTerritoryGuildNames(territory);
                        TerritoryManager.ClearEmblem(territory);
                        territory.ClearPortal();
                        client.Out.SendMessage("Le Territoire avec AreaId " + areaId + " est de nouveau neutre.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                    }

                    break;

                case "bonus":

                    if (args.Length != 5)
                    {
                        DisplaySyntax(client);
                        break;
                    }

                    string action = args[2].ToLowerInvariant();
                    var bonus = this.GetResist(args[3].ToLowerInvariant());
                    areaId = args[4];

                    if (string.IsNullOrEmpty(areaId) || !bonus.HasValue)
                    {
                        DisplaySyntax(client);
                        break;
                    }

                    territory = TerritoryManager.Instance.Territories.FirstOrDefault(t => t.AreaId.Equals(areaId));

                    if (territory == null)
                    {
                        client.Out.SendMessage("Le Territoire avec AreaId " + areaId + " n'a pas été trouvé.", eChatType.CT_System, eChatLoc.CL_ChatWindow);
                        break;
                    }

                    if (action == "add")
                    {
                        territory.Bonus.Add(bonus.Value);
                        if (territory.GuildOwner != null)
                        {
                            var guild = GuildMgr.GetGuildByName(territory.GuildOwner);

                            if (guild != null)
                            {
                                guild.RemoveTerritory(territory.AreaId);
                                guild.AddTerritory(territory.AreaId, true);
                            }
                        }

                        territory.SaveIntoDatabase();

                        client.Out.SendMessage("Le Territoire avec AreaId " + areaId + " a désormais un bonus suppmémentaire de " + bonus.Value.ToString(), eChatType.CT_System, eChatLoc.CL_ChatWindow);
                        break;
                    }
                    else if (action == "remove")
                    {
                        int index;
                        index = territory.Bonus.FindIndex(b => b == bonus.Value);

                        if (index != -1)
                        {
                            territory.Bonus.RemoveAt(index);

                            if (territory.GuildOwner != null)
                            {
                                var guild = GuildMgr.GetGuildByName(territory.GuildOwner);

                                if (guild != null)
                                {
                                    guild.RemoveTerritory(territory.AreaId);
                                    guild.AddTerritory(territory.AreaId, true);
                                }
                            }

                            territory.SaveIntoDatabase();

                            client.Out.SendMessage("Le bonus de " + bonus.Value.ToString() + " a été retiré de Territoire avec AreaId " + areaId, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                        }
                    }

                    break;

                /*        <nature|crush|slash|thrust|body|cold|energy|heat|matter|spirit>      
                 * 	Natural = eProperty.Resist_Natural,
                    Crush = eProperty.Resist_Crush,
                    Slash = eProperty.Resist_Slash,
                    Thrust = eProperty.Resist_Thrust,
                    Body = eProperty.Resist_Body,
                    Cold = eProperty.Resist_Cold,
                    Energy = eProperty.Resist_Energy,
                    Heat = eProperty.Resist_Heat,
                    Matter = eProperty.Resist_Matter,
                    Spirit = eProperty.Resist_Spirit               
                 * 
                 * */


                default:
                    DisplaySyntax(client);
                    break;
            }
        }


        private eResist? GetResist(string resist)
        {
            switch (resist)
            {

                case "nature": return eResist.Natural;
                case "crush": return eResist.Crush;
                case "slash": return eResist.Slash;
                case "thrust": return eResist.Thrust;
                case "body": return eResist.Body;
                case "cold": return eResist.Cold;
                case "energy": return eResist.Energy;
                case "heat": return eResist.Heat;
                case "matter": return eResist.Matter;
                case "spirit": return eResist.Spirit;

                default:
                    return null;
            }
        }
    }
}