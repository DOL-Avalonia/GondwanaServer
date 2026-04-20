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
using DOL.GS.Geometry;
using System;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.Language;
using System.Collections.Generic;

namespace DOL.GS.Commands
{
    [CmdAttribute("&jump",
        ePrivLevel.GM,
        "Commands.GM.Jump.Description",
        "Commands.GM.Jump.Information",
        "Commands.GM.Jump.Usage.ToPlayerName",
        "/jump to <#ClientID> ex. /jump to #10",
        "Commands.GM.Jump.Usage.ToNameRealmID",
        "Commands.GM.Jump.Usage.ToXYZRegionID",
        "/jump to <myhouse | house [#]>",
        "/jump to region [#] - jump to same location in new region",
        "Commands.GM.Jump.Usage.PlayerNameToXYZ",
        "Commands.GM.Jump.Usage.PlayerNameToXYZRegID",
        "Commands.GM.Jump.Usage.PlayerNToPlayerN",
        "Commands.GM.Jump.Usage.ToGT",
        "Commands.GM.Jump.Usage.RelXYZ",
        "Commands.GM.Jump.Usage.Push",
        "Commands.GM.Jump.Usage.Pop",
        "/jump refresh - force a world refresh around you"
        )]
    public class JumpCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private const string TEMP_KEY_JUMP = "JumpLocStack";

        public void OnCommand(GameClient client, string[] args)
        {
            try
            {
                #region Refresh
                if (args.Length == 2 && args[1].ToLower() == "refresh")
                {
                    client.Player.LeaveHouse();
                    client.Player.RefreshWorld();
                    return;
                }
                #endregion Refresh
                #region Jump to Target
                if (args.Length == 3 && args[1].ToLower() == "to" && args[2].ToLower() == "target")
                {
                    var target = client.Player.TargetObject;
                    var player = client.Player;

                    if (client.Player.CurrentRegionID != target.CurrentRegionID && client.Player.HasTerritoryRelic())
                        client.Player.DropTerritoryRelicsOnDeath(null);

                    player.MoveTo(target.Position);
                    return;
                }
                #endregion
                #region Jump to GT
                if (args.Length == 3 && args[1].ToLower() == "to" && args[2].ToLower() == "gt")
                {
                    client.Player.MoveTo(client.Player.GroundTargetPosition.With(orientation: client.Player.Orientation));
                    return;
                }
                #endregion Jump to GT
                #region Jump to house
                else if (args.Length >= 3 && args[1].ToLower() == "to" && (args[2].ToLower() == "house" || args[2].ToLower() == "myhouse"))
                {
                    House house = null;
                    if (args[2] == "myhouse")
                    {
                        house = HouseMgr.GetHouseByPlayer(client.Player);
                    }
                    else
                    {
                        house = HouseMgr.GetHouse(Convert.ToInt32(args[3]));
                    }
                    if (house != null)
                    {
                        if (client.Player.HasTerritoryRelic())
                            client.Player.DropTerritoryRelicsOnDeath(null);

                        client.Player.MoveTo(house.OutdoorJumpPosition);
                    }
                    else
                    {
                        client.Out.SendMessage("This house number is not owned by anyone!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    return;
                }
                #endregion Jump to house
                #region Jump t region #
                if (args.Length == 4 && args[1] == "to" && args[2] == "region")
                {
                    ushort targetRegion = Convert.ToUInt16(args[3]);
                    if (client.Player.CurrentRegionID != targetRegion && client.Player.HasTerritoryRelic())
                        client.Player.DropTerritoryRelicsOnDeath(null);

                    client.Player.MoveTo(client.Player.Position.With(regionID: targetRegion));
                    return;
                }
                #endregion Jump t region #
                #region Jump to PlayerName or ClientID
                if (args.Length == 3 && args[1] == "to")
                {
                    GameClient clientc = null;
                    if (args[2].StartsWith("#"))
                    {
                        try
                        {
                            var sessionID = Convert.ToUInt32(args[2].Substring(1));
                            clientc = WorldMgr.GetClientFromID(sessionID);
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        clientc = WorldMgr.GetClientByPlayerName(args[2], false, true);
                    }

                    if (clientc == null)
                    {
                        GameNPC[] npcs = WorldMgr.GetNPCsByName(args[2], eRealm.None);

                        if (npcs.Length > 0)
                        {
                            // for multiple npc's first try to jump to the npc in the players current region
                            GameNPC jumpTarget = npcs[0];

                            foreach (GameNPC npc in npcs)
                            {
                                if (npc.CurrentRegionID == client.Player.CurrentRegionID)
                                {
                                    jumpTarget = npc;
                                    break;
                                }
                            }

                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.JumpToX", npcs[0].CurrentRegion.Description), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            if (jumpTarget.InHouse && jumpTarget.CurrentHouse != null)
                            {
                                jumpTarget.CurrentHouse.Enter(client.Player);
                            }
                            else
                            {
                                client.Player.MoveTo(jumpTarget.Position);
                            }
                            return;
                        }

                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.CannotBeFound", args[2]), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    if (CheckExpansion(client, clientc, clientc.Player.CurrentRegionID))
                    {
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.JumpToX", clientc.Player.CurrentRegion.Description), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        if (client.Player.CurrentRegionID != clientc.Player.CurrentRegionID && client.Player.HasTerritoryRelic())
                            client.Player.DropTerritoryRelicsOnDeath(null);
                        if (clientc.Player.CurrentHouse != null && clientc.Player.InHouse)
                            clientc.Player.CurrentHouse.Enter(client.Player);
                        else
                            client.Player.MoveTo(clientc.Player.Position.With(client.Player.Orientation));
                        return;
                    }

                    client.Out.SendMessage("You don't have an expansion needed to jump to this location.", eChatType.CT_System, eChatLoc.CL_SystemWindow);

                    return;
                }
                #endregion Jump to PlayerName
                #region Jump to Name Realm
                else if (args.Length == 4 && args[1] == "to")
                {
                    GameClient clientc;
                    clientc = WorldMgr.GetClientByPlayerName(args[2], false, true);
                    if (clientc == null)
                    {
                        int realm = 0;
                        int.TryParse(args[3], out realm);

                        GameNPC[] npcs = WorldMgr.GetNPCsByName(args[2], (eRealm)realm);

                        if (npcs.Length > 0)
                        {
                            // for multiple npc's first try to jump to the npc in the players current region
                            GameNPC jumpTarget = npcs[0];

                            foreach (GameNPC npc in npcs)
                            {
                                if (npc.CurrentRegionID == client.Player.CurrentRegionID)
                                {
                                    jumpTarget = npc;
                                    break;
                                }
                            }

                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.JumpToX", npcs[0].CurrentRegion.Description), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            client.Player.MoveTo(jumpTarget.Position);
                            return;
                        }

                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.CannotBeFoundInRealm", args[2], realm), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }
                    if (CheckExpansion(client, clientc, clientc.Player.CurrentRegionID))
                    {
                        if (clientc.Player.InHouse)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.CannotJumpToInsideHouse"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.JumpToX", clientc.Player.CurrentRegion.Description), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        if (clientc.Player.CurrentHouse != null && clientc.Player.InHouse)
                            clientc.Player.CurrentHouse.Enter(client.Player);
                        else
                        {
                            var jumpPosition = clientc.Player.Position.With(client.Player.Orientation);
                            client.Player.MoveTo(jumpPosition);
                        }
                        return;
                    }
                    return;
                }
                #endregion Jump to Name Realm
                #region Jump to X Y Z
                else if (args.Length == 5 && args[1] == "to")
                {
                    var jumpPosition = Position.Create(
                        client.Player.CurrentRegionID,
                        x: Convert.ToInt32(args[2]),
                        y: Convert.ToInt32(args[3]),
                        z: Convert.ToInt32(args[4]),
                        client.Player.Orientation);
                    client.Player.MoveTo(jumpPosition);
                    return;
                }
                #endregion Jump to X Y Z
                #region Jump rel +/-X +/-Y +/-Z
                else if (args.Length == 5 && args[1] == "rel")
                {
                    var offset = Vector.Create(x:Convert.ToInt32(args[2]), y: Convert.ToInt32(args[3]), z: Convert.ToInt32(args[4]));
                    client.Player.MoveTo(client.Player.Position + offset);
                    return;
                }
                #endregion Jump rel +/-X +/-Y +/-Z
                #region Jump to X Y Z RegionID
                else if (args.Length == 6 && args[1] == "to")
                {
                    ushort targetRegion = Convert.ToUInt16(args[5]);
                    if (CheckExpansion(client, client, targetRegion))
                    {
                        var jumpPosition = Position.Create(
                            regionID: targetRegion,
                            x: Convert.ToInt32(args[2]),
                            y: Convert.ToInt32(args[3]),
                            z: Convert.ToInt32(args[4]),
                            client.Player.Orientation);

                        if (client.Player.CurrentRegionID != targetRegion && client.Player.HasTerritoryRelic())
                            client.Player.DropTerritoryRelicsOnDeath(null);

                        client.Player.MoveTo(jumpPosition);
                        return;
                    }
                    return;
                }
                #endregion Jump to X Y Z RegionID
                #region Jump PlayerName to X Y Z
                else if (args.Length == 6 && args[2] == "to")
                {
                    GameClient clientc;
                    clientc = WorldMgr.GetClientByPlayerName(args[1], false, true);
                    if (clientc == null)
                    {
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.PlayerIsNotInGame", args[1]), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }
                    clientc.Player.MoveTo(clientc.Player.CurrentRegionID, Convert.ToInt32(args[3]), Convert.ToInt32(args[4]), Convert.ToInt32(args[5]), clientc.Player.Heading);
                    return;
                }
                #endregion Jump PlayerName to X Y Z
                #region Jump PlayerName to X Y Z RegionID
                else if (args.Length == 7 && args[2] == "to")
                {
                    GameClient clientc;
                    clientc = WorldMgr.GetClientByPlayerName(args[1], false, true);
                    if (clientc == null)
                    {
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.PlayerIsNotInGame", args[1]), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    ushort targetRegion = Convert.ToUInt16(args[6]);
                    if (CheckExpansion(clientc, clientc, targetRegion))
                    {
                        var jumpPosition = Position.Create(targetRegion, x: Convert.ToInt32(args[3]), y: Convert.ToInt32(args[4]), z: Convert.ToInt32(args[5]), clientc.Player.Orientation);

                        if (clientc.Player.CurrentRegionID != targetRegion && clientc.Player.HasTerritoryRelic())
                            clientc.Player.DropTerritoryRelicsOnDeath(null);

                        clientc.Player.MoveTo(jumpPosition);
                        return;
                    }
                    return;
                }
                #endregion Jump PlayerName to X Y Z RegionID
                #region Jump PlayerName to TargetCible (Player or NPC)
                else if (args.Length == 4 && args[2] == "to")
                {
                    GameClient clientc;
                    GameClient clientto;
                    clientc = WorldMgr.GetClientByPlayerName(args[1], false, true);
                    if (clientc == null)
                    {
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.PlayerIsNotInGame", args[1]), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    if (args[3] == "me")
                    {
                        clientto = client;
                    }
                    else
                    {
                        clientto = WorldMgr.GetClientByPlayerName(args[3], false, false);
                    }

                    if (clientto != null)
                    {
                        if (CheckExpansion(clientto, clientc, clientto.Player.CurrentRegionID))
                        {
                            if (clientc.Player.CurrentRegionID != clientto.Player.CurrentRegionID && clientc.Player.HasTerritoryRelic())
                                clientc.Player.DropTerritoryRelicsOnDeath(null);

                            if (clientto.Player.CurrentHouse != null && clientto.Player.InHouse)
                                clientto.Player.CurrentHouse.Enter(clientc.Player);
                            else
                                clientc.Player.MoveTo(clientto.Player.Position.With(clientc.Player.Orientation));
                            return;
                        }
                        return;
                    }
                    else
                    {
                        GameNPC[] npcs = WorldMgr.GetNPCsByName(args[3], eRealm.None);

                        if (npcs.Length > 0)
                        {
                            GameNPC jumpTarget = npcs[0];

                            foreach (GameNPC npc in npcs)
                            {
                                if (npc.CurrentRegionID == clientc.Player.CurrentRegionID)
                                {
                                    jumpTarget = npc;
                                    break;
                                }
                            }

                            if (CheckExpansion(client, clientc, jumpTarget.CurrentRegionID))
                            {
                                if (clientc.Player.CurrentRegionID != jumpTarget.CurrentRegionID && clientc.Player.HasTerritoryRelic())
                                    clientc.Player.DropTerritoryRelicsOnDeath(null);

                                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.JumpToX", jumpTarget.CurrentRegion.Description), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                                if (jumpTarget.InHouse && jumpTarget.CurrentHouse != null)
                                {
                                    jumpTarget.CurrentHouse.Enter(clientc.Player);
                                }
                                else
                                {
                                    clientc.Player.MoveTo(jumpTarget.Position.With(clientc.Player.Orientation));
                                }
                            }
                            return;
                        }

                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.CannotBeFound", args[3]), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }
                }
                #endregion Jump PlayerName to PlayerCible
                #region push/pop
                else if (args.Length > 1 && args[1] == "push")
                {
                    Stack<Position> positions;

                    positions = client.Player.TempProperties.getProperty<object>(TEMP_KEY_JUMP, null) as Stack<Position>;

                    if (positions == null)
                    {
                        positions = new Stack<Position>(3);
                        client.Player.TempProperties.setProperty(TEMP_KEY_JUMP, positions);
                    }

                    positions.Push(client.Player.Position);

                    string message = LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.Pushed");

                    if (positions.Count > 1)
                        message += " " + LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.PushedTotal", positions.Count);

                    message += " - " + LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.PopInstructions");

                    client.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else if (args.Length > 1 && args[1] == "pop")
                {
                    Stack<Position> positions;

                    positions = client.Player.TempProperties.getProperty<object>(TEMP_KEY_JUMP, null) as Stack<Position>;

                    if (positions == null || positions.Count < 1)
                    {
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.Jump.NothingPushed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    var jumploc = positions.Pop();

                    // slight abuse of the stack principle, but convenient to always have your last popped loc jumpable
                    if (positions.Count < 1)
                        positions.Push(jumploc);

                    client.Player.MoveTo(jumploc);
                }
                #endregion push/pop
                #region DisplaySyntax
                else
                {
                    DisplaySyntax(client);
                    return;
                }
                #endregion DisplaySyntax
            }

            catch (Exception ex)
            {
                DisplayMessage(client, ex.Message);
            }
        }

        public bool CheckExpansion(GameClient clientJumper, GameClient clientJumpee, ushort RegionID)
        {
            Region reg = WorldMgr.GetRegion(RegionID);
            if (reg != null && reg.Expansion > (int)clientJumpee.ClientType)
            {
                clientJumper.Out.SendMessage(LanguageMgr.GetTranslation(clientJumper, "Commands.GM.Jump.CheckExpansion.CannotJump", clientJumpee.Player.Name, reg.Description), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                if (clientJumper != clientJumpee)
                    clientJumpee.Out.SendMessage(LanguageMgr.GetTranslation(clientJumpee, "Commands.GM.Jump.CheckExpansion.ClientNoSup", clientJumper.Player.Name, reg.Description), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            return true;
        }
    }
}