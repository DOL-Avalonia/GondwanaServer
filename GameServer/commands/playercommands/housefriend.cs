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
using DOL.GS.PacketHandler;
using DOL.GS.Housing;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&housefriend",
        ePrivLevel.Player,
        "Commands.Players.Housefriend.Description",
        "Commands.Players.Housefriend.All",
        "Commands.Players.Housefriend.Player",
        "Commands.Players.Housefriend.Account",
        "Commands.Players.Housepoint.Guild")]
    public class HousefriendCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length == 1)
            {
                DisplaySyntax(client);
                return;
            }

            if (!client.Player.InHouse)
            {
                client.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Housefriend.NotInHouse"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            switch (args[1])
            {
                case "player":
                    {
                        if (args.Length == 2)
                            return;

                        if (client.Player.Name == args[2])
                            return;

                        GameClient targetClient = WorldMgr.GetClientByPlayerNameAndRealm(args[2], 0, true);
                        if (targetClient == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Housefriend.NotOnline"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (client.Player.CurrentHouse.AddPermission(targetClient.Player, PermissionType.Player, HousingConstants.MinPermissionLevel))
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Housefriend.Added", targetClient.Player.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }

                case "account":
                    {
                        if (args.Length == 2)
                            return;

                        if (client.Player.Name == args[2])
                            return;

                        GameClient targetClient = WorldMgr.GetClientByPlayerNameAndRealm(args[2], 0, true);
                        if (targetClient == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Housefriend.NotOnline"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (client.Player.CurrentHouse.AddPermission(targetClient.Player, PermissionType.Account, HousingConstants.MinPermissionLevel))
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Housefriend.AddedAccount", targetClient.Player.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }

                case "guild":
                    {
                        if (args.Length == 2)
                            return;

                        Guild targetGuild = GuildMgr.GetGuildByName(args[2]);
                        if (targetGuild == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Housefriend.NoGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (client.Player.CurrentHouse.AddPermission(targetGuild.Name, PermissionType.Guild, HousingConstants.MinPermissionLevel))
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Housefriend.Added", targetGuild.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;

                    }
                case "all":
                    {
                        if (client.Player.CurrentHouse.AddPermission("All", PermissionType.All, HousingConstants.MinPermissionLevel))
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Housefriend.AddedAll"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        break;
                    }
                default:
                    DisplaySyntax(client);
                    break;
            }
        }
    }
}