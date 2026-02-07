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
using System.Reflection;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&area",
        ePrivLevel.GM,
        "Commands.GM.Area.Description",
        "Commands.GM.Area.Usage.Create",
        "Commands.GM.Area.Usage.AllowVol",
        "Commands.GM.Area.Usage.Sound",
        "Commands.GM.Area.Usage.RealmPoints",
        "Commands.GM.Area.Usage.IsPvP",
        "Commands.GM.Area.Usage.IsSafe",
        "Commands.GM.Area.Usage.Radius",
        "Commands.GM.Area.Usage.Maxradius",
        "Commands.GM.Area.Usage.Boundary",
        "Commands.GM.Area.Usage.BoundaryEvent",
        "Commands.GM.Area.Usage.BoundarySpacing",
        "Commands.GM.Area.Usage.Spell",
        "Commands.GM.Area.Usage.SpellEvent",
        "Commands.GM.Area.Usage.StormLevel",
        "Commands.GM.Area.Usage.StormSize",
        "Commands.GM.Area.Usage.EventList",
        "Commands.GM.Area.Usage.IsRadioactive",
        "Commands.GM.Area.Usage.Remove")]
    public class AreaCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length == 1)
            {
                DisplaySyntax(client);
                return;
            }

            switch (args[1].ToLower())
            {
                #region Create
                case "create":
                    {
                        if (!(args.Length != 7 || args.Length != 8))
                        {
                            DisplaySyntax(client);
                            return;
                        }

                        DBArea area = new DBArea();
                        area.Description = args[2];

                        switch (args[3].ToLower())
                        {
                            case "circle": area.ClassType = "DOL.GS.Area+Circle"; break;
                            case "square": area.ClassType = "DOL.GS.Area+Square"; break;
                            case "rectangle": area.ClassType = "DOL.GS.Area+Rectangle"; break;
                            case "ellipse": area.ClassType = "DOL.GS.Area+Ellipse"; break;
                            case "tore": area.ClassType = "DOL.GS.Area+Tore"; break;
                            case "safe":
                            case "safearea": area.ClassType = "DOL.GS.Area+SafeArea"; break;
                            case "bind":
                            case "bindarea": area.ClassType = "DOL.GS.Area+BindArea"; break;
                            default:
                                {
                                    DisplaySyntax(client);
                                    return;
                                }
                        }

                        area.Radius = Convert.ToInt16(args[4]);
                        switch (args[5].ToLower())
                        {
                            case "y": { area.CanBroadcast = true; break; }
                            case "n": { area.CanBroadcast = false; break; }
                            default: { DisplaySyntax(client); return; }
                        }
                        area.Sound = byte.Parse(args[6]);
                        area.Region = client.Player.CurrentRegionID;
                        area.X = client.Player.Position.X;
                        area.Y = client.Player.Position.Y;
                        area.Z = client.Player.Position.Z;
                        area.ObjectId = area.Description;

                        if (args.Length >= 8 && bool.TryParse(args[7], out bool canVol))
                        {
                            area.AllowVol = canVol;
                        }

                        Assembly gasm = Assembly.GetAssembly(typeof(GameServer));
                        AbstractArea newArea = (AbstractArea)gasm!.CreateInstance(area.ClassType, false);
                        newArea!.LoadFromDatabase(area);

                        newArea.Sound = area.Sound;
                        newArea.CanBroadcast = area.CanBroadcast;
                        WorldMgr.GetRegion(client.Player.CurrentRegionID).AddArea(newArea);
                        try
                        {
                            GameServer.Database.AddObject(area);
                        }
                        catch
                        {
                            client.Out.SendMessage("Le nom de cet Area doit etre unique", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;
                        }

                        DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Area.AreaCreated", area.Description, area.X, area.Y, area.Z, area.Radius, area.CanBroadcast.ToString(), area.Sound));
                        break;
                    }
                #endregion Create

                #region Settings (AllowVol, Sound, RP, PvP, Safe, Radius)

                case "allowvol":
                    {
                        if (args.Length < 3)
                        {
                            client.Out.SendMessage("Usage: /area allowvol <true/false>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);

                        if (currentArea == null)
                        {
                            client.Out.SendMessage("You are not standing in a valid database area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        string valStr = args[2].ToLower();
                        bool enable = (valStr == "true" || valStr == "on" || valStr == "yes" || valStr == "1");

                        currentArea.DbArea.AllowVol = enable;

                        // Try to update live property if it exists on your AbstractArea implementation
                        // currentArea.AllowVol = enable; 

                        GameServer.Database.SaveObject(currentArea.DbArea);
                        client.Out.SendMessage($"Area '{currentArea.Description}' AllowVol set to: {enable}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }

                case "sound":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area sound set <ID>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);

                        if (currentArea == null)
                        {
                            client.Out.SendMessage("You are not standing in a valid database area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (byte.TryParse(args[3], out byte soundId))
                        {
                            currentArea.DbArea.Sound = soundId;
                            currentArea.Sound = soundId; // Update live object immediately
                            GameServer.Database.SaveObject(currentArea.DbArea);
                            client.Out.SendMessage($"Area '{currentArea.Description}' Sound ID set to: {soundId}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            client.Out.SendMessage("Invalid Sound ID. Must be a number (0-255).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }

                case "realmpoints":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area realmpoints set <value>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);

                        if (currentArea == null)
                        {
                            client.Out.SendMessage("You are not standing in a valid database area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (int.TryParse(args[3], out int rpValue))
                        {
                            currentArea.DbArea.RealmPoints = rpValue;
                            GameServer.Database.SaveObject(currentArea.DbArea);
                            client.Out.SendMessage($"Area '{currentArea.Description}' RealmPoints bonus set to: {rpValue}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            client.Out.SendMessage("Invalid RealmPoints value.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }

                case "ispvp":
                    {
                        if (args.Length < 3)
                        {
                            client.Out.SendMessage("Usage: /area ispvp <true/false>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);

                        if (currentArea == null)
                        {
                            client.Out.SendMessage("You are not standing in a valid database area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        string valStr = args[2].ToLower();
                        bool enable = (valStr == "true" || valStr == "on" || valStr == "yes" || valStr == "1");

                        currentArea.DbArea.IsPvP = enable;
                        GameServer.Database.SaveObject(currentArea.DbArea);
                        client.Out.SendMessage($"Area '{currentArea.Description}' IsPvP set to: {enable}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }

                case "issafe":
                    {
                        if (args.Length < 3)
                        {
                            client.Out.SendMessage("Usage: /area issafe <true/false>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);

                        if (currentArea == null)
                        {
                            client.Out.SendMessage("You are not standing in a valid database area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        string valStr = args[2].ToLower();
                        bool enable = (valStr == "true" || valStr == "on" || valStr == "yes" || valStr == "1");

                        currentArea.DbArea.SafeArea = enable;
                        GameServer.Database.SaveObject(currentArea.DbArea);
                        client.Out.SendMessage($"Area '{currentArea.Description}' IsSafe set to: {enable}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }

                case "radius":
                case "maxradius":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area <radius|maxradius> set <value>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var areas = client.Player.CurrentRegion.GetAreasOfSpot(client.Player.Coordinate);
                        AbstractArea? currentArea = areas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);

                        if (currentArea == null)
                        {
                            client.Out.SendMessage("You are not standing in a valid database area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (!int.TryParse(args[3], out int newValue)) return;

                        bool isMax = args[1].ToLower() == "maxradius";

                        if (isMax)
                        {
                            if (newValue < currentArea.DbArea.Radius)
                            {
                                client.Out.SendMessage("MaxRadius cannot be smaller than Radius!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }
                            currentArea.DbArea.MaxRadius = newValue;
                        }
                        else
                        {
                            currentArea.DbArea.Radius = newValue;
                        }

                        GameServer.Database.SaveObject(currentArea.DbArea);
                        currentArea.LoadFromDatabase(currentArea.DbArea);

                        client.Out.SendMessage($"{args[1]} updated to {newValue} for area: {currentArea.Description}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }
                #endregion Settings

                #region Boundaries / Effects
                case "boundary":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area boundary set <modelID>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var areas = client.Player.CurrentRegion.GetAreasOfSpot(client.Player.Coordinate);
                        AbstractArea? currentArea = areas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);

                        if (currentArea == null)
                        {
                            client.Out.SendMessage("You are not standing in a valid area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (!int.TryParse(args[3], out int modelID)) return;

                        currentArea.DbArea.Boundary = modelID;
                        GameServer.Database.SaveObject(currentArea.DbArea);

                        currentArea.SpawnBoundary(); // Live Refresh

                        client.Out.SendMessage($"Boundary model set to {modelID} for: {currentArea.Description}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }

                case "boundaryspacing":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area boundaryspacing set <value>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var areas = client.Player.CurrentRegion.GetAreasOfSpot(client.Player.Coordinate);
                        var currentArea = areas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);

                        if (currentArea == null)
                        {
                            client.Out.SendMessage("You are not standing in a valid database area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (!int.TryParse(args[3], out int spacing))
                        {
                            client.Out.SendMessage("Invalid value.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (spacing < 50) spacing = 50;

                        currentArea.DbArea.BoundarySpacing = spacing;
                        GameServer.Database.SaveObject(currentArea.DbArea);

                        currentArea.SpawnBoundary();

                        client.Out.SendMessage($"Boundary Spacing set to {spacing} for area: {currentArea.Description}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }

                case "spell":
                case "effectfreq":
                case "effectamount":
                case "effectvariance":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area <spell|effectfreq|effectamount|effectvariance> set <value>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);
                        if (currentArea == null) return;

                        string subcmd = args[1].ToLower();
                        double val = double.Parse(args[3]);

                        if (subcmd == "spell") currentArea.DbArea.SpellID = (int)val;
                        if (subcmd == "effectfreq") currentArea.DbArea.EffectFrequency = (int)val;
                        if (subcmd == "effectamount") currentArea.DbArea.EffectAmount = (int)val;
                        if (subcmd == "effectvariance") currentArea.DbArea.EffectVariance = val;

                        GameServer.Database.SaveObject(currentArea.DbArea);
                        currentArea.StartEffectLoop();

                        client.Out.SendMessage($"Effect parameter {subcmd} updated.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }

                case "stormlevel":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area stormlevel set <value>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);
                        if (currentArea == null) return;

                        if (byte.TryParse(args[3], out byte level))
                        {
                            currentArea.DbArea.StormLevel = level;
                            GameServer.Database.SaveObject(currentArea.DbArea);
                            client.Out.SendMessage($"Storm Level set to {level}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }

                case "stormsize":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area stormsize set <value>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);
                        if (currentArea == null) return;

                        if (byte.TryParse(args[3], out byte size))
                        {
                            currentArea.DbArea.StormSize = size;
                            GameServer.Database.SaveObject(currentArea.DbArea);
                            client.Out.SendMessage($"Storm Size set to {size}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }

                case "boundaryevent":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area boundaryevent set <modelID>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);
                        if (currentArea == null) return;

                        if (int.TryParse(args[3], out int model))
                        {
                            currentArea.DbArea.BoundaryEvent = model;
                            GameServer.Database.SaveObject(currentArea.DbArea);
                            client.Out.SendMessage($"Boundary Event Model set to {model}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }

                case "spellevent":
                    {
                        if (args.Length < 4 || args[2].ToLower() != "set")
                        {
                            client.Out.SendMessage("Usage: /area spellevent set <spellID>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);
                        if (currentArea == null) return;

                        if (int.TryParse(args[3], out int spellID))
                        {
                            currentArea.DbArea.SpellIDEvent = spellID;
                            GameServer.Database.SaveObject(currentArea.DbArea);
                            client.Out.SendMessage($"Event Spell ID set to {spellID}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }

                case "eventlist":
                    {
                        if (args.Length < 4)
                        {
                            client.Out.SendMessage("Usage: /area eventlist <add|remove|set> <value>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);
                        if (currentArea == null) return;

                        string sub = args[2].ToLower();
                        string val = args[3];
                        string current = currentArea.DbArea.EventList ?? "";
                        var list = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                        if (sub == "set")
                        {
                            currentArea.DbArea.EventList = val;
                        }
                        else if (sub == "add")
                        {
                            if (!list.Contains(val)) list.Add(val);
                            currentArea.DbArea.EventList = string.Join(";", list);
                        }
                        else if (sub == "remove")
                        {
                            list.Remove(val);
                            currentArea.DbArea.EventList = string.Join(";", list);
                        }

                        GameServer.Database.SaveObject(currentArea.DbArea);
                        client.Out.SendMessage($"EventList updated: {currentArea.DbArea.EventList}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }

                case "radioactive":
                    {
                        if (args.Length < 3)
                        {
                            client.Out.SendMessage("Usage: /area radioactive <on/off>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var currentArea = client.Player.CurrentAreas.OfType<AbstractArea>().FirstOrDefault(a => a.DbArea != null);
                        if (currentArea == null)
                        {
                            client.Out.SendMessage("You are not standing in a valid database area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        bool enable = (args[2].ToLower() == "on" || args[2].ToLower() == "true");
                        currentArea.DbArea.IsRadioactive = enable;
                        currentArea.IsRadioactive = enable;
                        GameServer.Database.SaveObject(currentArea.DbArea);

                        client.Out.SendMessage($"Area '{currentArea.Description}' Radioactive state set to: {enable}", eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        if (enable) currentArea.OnPlayerEnter(client.Player);
                        else currentArea.OnPlayerLeave(client.Player);

                        break;
                    }
                #endregion

                #region Remove
                case "remove":
                    {
                        var currentSpot = client.Player.Coordinate;
                        var region = client.Player.CurrentRegion;

                        var areaToRemove = region.GetAreasOfSpot(currentSpot)
                            .OfType<AbstractArea>()
                            .FirstOrDefault(a => a.DbArea != null);

                        if (areaToRemove == null)
                        {
                            client.Out.SendMessage("You are not standing inside a valid database area.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        string areaDesc = areaToRemove.Description;

                        try
                        {
                            areaToRemove.StopEffectLoop();
                            areaToRemove.ClearBoundary();
                            region.RemoveArea(areaToRemove);
                            GameServer.Database.DeleteObject(areaToRemove.DbArea);

                            client.Out.SendMessage($"Area '{areaDesc}' has been successfully removed from the world and database.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        catch (Exception ex)
                        {
                            client.Out.SendMessage($"Error removing area: {ex.Message}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }
                #endregion

                #region Default
                default:
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    #endregion Default
            }
        }
    }
}