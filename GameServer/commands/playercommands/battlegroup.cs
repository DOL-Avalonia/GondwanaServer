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
using System.Collections;
using System.Text;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&battlechat",
        new string[] { "&bc" },
        ePrivLevel.Player,
        "Commands.Players.Battlechat.Description",
        "Commands.Players.Battlechat.Usage")]
    public class BattleGroupCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (IsSpammingCommand(client.Player, "battlechat"))
                return;

            BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
            if (mybattlegroup == null)
            {
                client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                return;
            }
            if (mybattlegroup.Listen == true && (((bool)mybattlegroup.Members[client.Player]) == false))
            {
                client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.OnlyModerator"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                return;
            }
            if (args.Length < 2)
            {
                client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Usage"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                return;
            }

            StringBuilder text = new StringBuilder(7 + 3 + client.Player.Name.Length + (args.Length - 1) * 8);
            if ((bool)mybattlegroup.Members[client.Player] == true)
            {
                text.Append(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlechat.ChatName"));
                text.Append(": \"");
                text.Append(args[1]);
                for (int i = 2; i < args.Length; i++)
                {
                    text.Append(" ");
                    text.Append(args[i]);
                }
                text.Append("\"");
                string message = text.ToString();
                foreach (GamePlayer ply in mybattlegroup.Members.Keys)
                {
                    ply.Out.SendMessage(" " + ply.GetPersonalizedName(client.Player) + message, eChatType.CT_BattleGroupLeader, eChatLoc.CL_ChatWindow);
                }
            }
            else
            {
                text.Append(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlechat.ChatName"));
                text.Append(": \"");
                text.Append(args[1]);
                for (int i = 2; i < args.Length; i++)
                {
                    text.Append(" ");
                    text.Append(args[i]);
                }
                text.Append("\"");
                string message = text.ToString();
                foreach (GamePlayer ply in mybattlegroup.Members.Keys)
                {
                    ply.Out.SendMessage(" " + ply.GetPersonalizedName(client.Player) + message, eChatType.CT_BattleGroup, eChatLoc.CL_ChatWindow);
                }
            }
        }
    }

    [CmdAttribute(
        "&battlegroup",
        new string[] { "&bg" },
        ePrivLevel.Player,
        "Commands.Players.Battlegroup.BG.Description",
        "Commands.Players.Battlegroup.BG.Usage")]
    public class BattleGroupSetupCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (IsSpammingCommand(client.Player, "battlegroup"))
                return;

            if (args.Length < 2)
            {
                PrintHelp(client);
                return;
            }
            switch (args[1].ToLower())
            {
                case "help":
                    {
                        PrintHelp(client);
                    }
                    break;
                case "invite":
                    {
                        if (args.Length < 3)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.UsageInvite"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        GameClient inviteeclient = WorldMgr.GetClientByPlayerName(args[2], false, true);
                        if (inviteeclient == null || !GameServer.ServerRules.IsSameRealm(inviteeclient.Player, client.Player, true)) // allow priv level>1 to invite anyone
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.NoPlayer"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (client == inviteeclient)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InviteYourself"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        BattleGroup oldbattlegroup = inviteeclient.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (oldbattlegroup != null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.PlayerInBattlegroup", client.Player.GetPersonalizedName(inviteeclient.Player)), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            mybattlegroup = new BattleGroup();
                            mybattlegroup.AddBattlePlayer(client.Player, true);
                        }
                        else if (((bool)mybattlegroup.Members[client.Player]) == false)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.LeaderInvite"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        inviteeclient.Player.TempProperties.setProperty(JOIN_BATTLEGROUP_PROPERTY, mybattlegroup);
                        inviteeclient.Player.Out.SendCustomDialog(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.JoinBattleGroup", inviteeclient.Player.GetPersonalizedName(client.Player)), new CustomDialogResponse(JoinBattleGroup));
                    }
                    break;
                case "groups":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);

                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        StringBuilder text = new StringBuilder(ServerProperties.Properties.BATTLEGROUP_MAX_MEMBER); //create the string builder
                        ArrayList curBattleGroupGrouped = new ArrayList(); //create the arraylist
                        ArrayList curBattleGroupNotGrouped = new ArrayList();
                        int i = 1; //This will list each group in the battle group.
                        text.Length = 0;
                        text.Append("The group structure of your Battle Group:");
                        client.Out.SendMessage(text.ToString(), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                        text.Length = 0;

                        foreach (GamePlayer player in mybattlegroup.Members.Keys)
                        {
                            if (player.Group != null && player.Group.MemberCount > 1)
                            {
                                curBattleGroupGrouped.Add(player);
                            }
                            else
                            {
                                curBattleGroupNotGrouped.Add(player);
                            }
                        }

                        ArrayList ListedPeople = new ArrayList();
                        int firstrun = 0;
                        foreach (GamePlayer grouped in curBattleGroupGrouped)
                        {
                            if (firstrun == 0)
                            {
                                text.Length = 0;
                                text.Append(i);
                                text.Append(") ");
                                i++; //Eg. 1)Batlas Ichijin etc.
                                text.Append(grouped.Group.GroupMemberString(grouped));
                                client.Out.SendMessage(text.ToString(), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                                firstrun = 1;
                            }
                            else if (!ListedPeople.Contains(grouped))
                            {
                                text.Length = 0;
                                text.Append(i);
                                text.Append(") ");
                                i++; //Eg. 1)Batlas Ichijin etc.
                                text.Append(grouped.Group.GroupMemberString(grouped));
                                client.Out.SendMessage(text.ToString(), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            }

                            foreach (GamePlayer gpl in grouped.Group.GetPlayersInTheGroup())
                            {
                                if (mybattlegroup.IsInTheBattleGroup(gpl))
                                    ListedPeople.Add(gpl);
                            }
                        }

                        foreach (GamePlayer nongrouped in curBattleGroupNotGrouped)
                        {
                            text.Length = 0;
                            text.Append(i);
                            text.Append(") ");
                            i++;

                            if ((bool)mybattlegroup.Members[nongrouped] == true)
                            {
                                text.Append(" ");
                                text.Append(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Leader"));
                            }
                            client.Out.SendMessage(text.ToString() + client.Player.GetPersonalizedName(nongrouped) + "\n", eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                        }
                    }
                    break;
                case "groupclass":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);

                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        StringBuilder text = new StringBuilder(ServerProperties.Properties.BATTLEGROUP_MAX_MEMBER); //create the string builder
                        ArrayList curBattleGroupGrouped = new ArrayList(); //create the arraylist
                        ArrayList curBattleGroupNotGrouped = new ArrayList();
                        int i = 1; //This will list each group in the battle group.
                        text.Length = 0;
                        text.Append(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.CurrentlyInBattleGroup"));
                        client.Out.SendMessage(text.ToString(), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                        text.Length = 0;

                        foreach (GamePlayer player in mybattlegroup.Members.Keys)
                        {
                            if (player.Group != null && player.Group.MemberCount > 1)
                            {
                                curBattleGroupGrouped.Add(player);
                            }
                            else
                            {
                                curBattleGroupNotGrouped.Add(player);
                            }
                        }

                        ArrayList ListedPeople = new ArrayList();
                        int firstrun = 0;
                        foreach (GamePlayer grouped in curBattleGroupGrouped)
                        {
                            if (firstrun == 0)
                            {
                                text.Length = 0;
                                text.Append(i);
                                text.Append(") ");
                                i++; //Eg. 1)Batlas Ichijin etc.
                                text.Append(grouped.Group.GroupMemberClassString(grouped));
                                client.Out.SendMessage(text.ToString(), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                                firstrun = 1;
                            }
                            else if (!ListedPeople.Contains(grouped))
                            {
                                text.Length = 0;
                                text.Append(i);
                                text.Append(") ");
                                i++; //Eg. 1)Batlas Ichijin etc.
                                text.Append(grouped.Group.GroupMemberClassString(grouped));
                                client.Out.SendMessage(text.ToString(), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            }

                            foreach (GamePlayer gpl in grouped.Group.GetPlayersInTheGroup())
                            {
                                if (mybattlegroup.IsInTheBattleGroup(gpl))
                                    ListedPeople.Add(gpl);
                            }
                        }

                        foreach (GamePlayer nongrouped in curBattleGroupNotGrouped)
                        {
                            text.Length = 0;
                            text.Append(i);
                            text.Append(") ");
                            i++;

                            if ((bool)mybattlegroup.Members[nongrouped] == true)
                            {
                                text.Append(" ");
                                text.Append(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Leader"));
                            }
                            text.Append("(" + nongrouped.CharacterClass.Name + ")");
                            client.Out.SendMessage(text.ToString() + client.Player.GetPersonalizedName(nongrouped) + "\n", eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                        }
                    }
                    break;

                case "who":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        int i = 0;
                        StringBuilder text = new StringBuilder(ServerProperties.Properties.BATTLEGROUP_MAX_MEMBER);
                        text.Length = 0;
                        text.Append(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.CurrentlyInBattleGroup"));
                        client.Out.SendMessage(text.ToString(), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);

                        foreach (GamePlayer player in mybattlegroup.Members.Keys)
                        {
                            i++;
                            text.Length = 0;
                            text.Append(i);
                            text.Append(") ");

                            if ((bool)mybattlegroup.Members[player] == true)
                            {
                                text.Append(" ");
                                text.Append(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Leader"));
                            }
                            text.Append(player.Name);

                            client.Out.SendMessage(text.ToString() + (client.Player == player ? " " : client.Player.GetPersonalizedName(player)),
                                eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            //TODO: make function formatstring                        
                        }
                    }
                    break;
                case "remove":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (args.Length < 3)
                        {
                            PrintHelp(client);
                        }
                        GameClient inviteeclient = WorldMgr.GetClientByPlayerName(args[2], false, false);
                        if (inviteeclient == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.NoPlayer"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        mybattlegroup.RemoveBattlePlayer(inviteeclient.Player);
                    }
                    break;
                case "leave":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        mybattlegroup.RemoveBattlePlayer(client.Player);
                    }
                    break;
                case "listen":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if ((bool)mybattlegroup.Members[client.Player] == false)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.LeaderCommand"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        mybattlegroup.Listen = !mybattlegroup.Listen;
                        string message = LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.ListenMode") + (mybattlegroup.Listen ? "on." : "off.");
                        foreach (GamePlayer ply in mybattlegroup.Members.Keys)
                        {
                            ply.Out.SendMessage(message, eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                        }
                    }
                    break;
                case "promote":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if ((bool)mybattlegroup.Members[client.Player] == false)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.LeaderCommand"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (args.Length < 3)
                        {
                            PrintHelp(client);
                        }
                        string invitename = String.Join(" ", args, 2, args.Length - 2);
                        GameClient inviteeclient = WorldMgr.GetClientByPlayerName(invitename, false, false);
                        if (inviteeclient == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.NoPlayer"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        mybattlegroup.Members[inviteeclient.Player] = true;
                        foreach (GamePlayer ply in mybattlegroup.Members.Keys)
                        {
                            string message = LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Moderator", ply.GetPersonalizedName(inviteeclient.Player));
                            ply.Out.SendMessage(message, eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                        }
                    }
                    break;
                case "public":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if ((bool)mybattlegroup.Members[client.Player] == false)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.LeaderCommand"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        mybattlegroup.IsPublic = true;
                        string message = LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Public");
                        foreach (GamePlayer ply in mybattlegroup.Members.Keys)
                        {
                            ply.Out.SendMessage(message, eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                        }
                    }
                    break;
                case "credit":
                    {
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.CommandNotImplemented"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                    }
                    break;
                case "grantcredit":
                    {
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.CommandNotImplemented"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                    }
                    break;
                case "private":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if ((bool)mybattlegroup.Members[client.Player] == false)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.LeaderCommand"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        mybattlegroup.IsPublic = false;
                        string message = LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Private");
                        foreach (GamePlayer ply in mybattlegroup.Members.Keys)
                        {
                            ply.Out.SendMessage(message, eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                        }
                    }
                    break;
                case "join":
                    {
                        if (args.Length < 3)
                        {
                            PrintHelp(client);
                            return;
                        }
                        BattleGroup oldbattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (oldbattlegroup != null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.AlreadyInBattlegroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        GameClient inviteeclient = WorldMgr.GetClientByPlayerName(args[2], false, false);
                        if (inviteeclient == null || !GameServer.ServerRules.IsSameRealm(client.Player, inviteeclient.Player, true)) // allow priv level>1 to join anywhere
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.NoPlayer"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (client == inviteeclient)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.OwnBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        BattleGroup mybattlegroup = inviteeclient.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.NotBattleGroupMember"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if ((bool)mybattlegroup.Members[inviteeclient.Player] == false)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.NotBattleGroupLeader"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (!mybattlegroup.IsPublic)
                        {
                            if (args.Length == 4 && args[3] == mybattlegroup.Password)
                            {
                                mybattlegroup.AddBattlePlayer(client.Player, false);
                            }
                            else
                            {
                                client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.NotPublic"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            }
                        }
                        else
                            mybattlegroup.AddBattlePlayer(client.Player, false);
                    }
                    break;
                case "password":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if ((bool)mybattlegroup.Members[client.Player] == false)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.LeaderCommand"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (args.Length < 3)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Password", mybattlegroup.Password) + mybattlegroup.Password, eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (args[2] == "clear")
                        {
                            mybattlegroup.Password = "";
                            return;
                        }
                        mybattlegroup.Password = args[2];
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.PasswordChanged", mybattlegroup.Password) + mybattlegroup.Password, eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                    }
                    break;
                case "count":
                    {
                        BattleGroup curbattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattlegroupCount", curbattlegroup.Members.Count), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                    }
                    break;
                case "status":
                    {
                        BattleGroup curbattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattlegroupStatus", curbattlegroup.Members.Count), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                    }
                    break;
                case "loot":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (mybattlegroup.Listen == true && (((bool)mybattlegroup.Members[client.Player]) == false))
                        {
                            client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.LeaderCommand"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (args[2] == "normal" || args[2] == "norm" || args[2] == "n" || args[2] == "N" || args[2] == "Norm" || args[2] == "Normal")
                        {
                            mybattlegroup.SetBGLootType(false);
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattleGroupLootNormal"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                        }
                        else if (args[2] == "treasurer" || args[2] == "treasure" || args[2] == "t" || args[2] == "T" || args[2] == "Treasurer" || args[2] == "Treasure")
                        {
                            mybattlegroup.SetBGLootType(true);
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattleGroupLootTreasurer"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                        }
                    }
                    break;
                case "lootlevel":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (mybattlegroup.Listen == true && (((bool)mybattlegroup.Members[client.Player]) == false))
                        {
                            client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.LeaderCommand"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (Convert.ToInt32(args[2]) == 0)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattlegroupLootThresholdOff"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattlegroupLootThresholdOn", mybattlegroup.GetBGLootTypeThreshold()), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                        }
                    }
                    break;
                case "treasurer":
                    {
                        BattleGroup mybattlegroup = client.Player.TempProperties.getProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                        if (mybattlegroup == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.InBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if ((bool)mybattlegroup.Members[client.Player] == false)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.LeaderCommand"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (args.Length < 3)
                        {
                            PrintHelp(client);
                        }
                        string treasname = String.Join(" ", args, 2, args.Length - 2);
                        GameClient treasclient = WorldMgr.GetClientByPlayerName(treasname, false, false);
                        if (treasclient == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.NoPlayer", client.Player.GetPersonalizedName(treasclient.Player)), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        mybattlegroup.SetBGTreasurer(treasclient.Player);
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattlegroupTreasurerOn", client.Player.GetPersonalizedName(treasclient.Player)), eChatType.CT_BattleGroupLeader, eChatLoc.CL_SystemWindow);
                        treasclient.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattlegroupTreasurerIsYou"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                        foreach (GamePlayer ply in mybattlegroup.Members.Keys)
                        {
                            ply.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattlegroupTreasurerIs", client.Player.GetPersonalizedName(treasclient.Player)), eChatType.CT_Chat, eChatLoc.CL_SystemWindow);
                        }
                        if (mybattlegroup.GetBGTreasurer() == null)
                        {
                            foreach (GamePlayer ply in mybattlegroup.Members.Keys)
                            {
                                ply.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.BattlegroupTreasurerOff"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                            }
                        }
                    }
                    break;

                default:
                    {
                        PrintHelp(client);
                    }
                    break;
            }
        }

        public void PrintHelp(GameClient client)
        {
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Usage"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Help"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Invite"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Who"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Remove"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Leave"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Listen"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Leader"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Public"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Private"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.JoinPublic"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.JoinPrivate"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.PasswordDisplay"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.PasswordClear"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.PasswordNew"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Count"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Groups"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.GroupClass"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Loot"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Treasurer"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Status"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.LootLevel"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.Credit"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Battlegroup.Help.GrantCredit"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
        }

        protected const string JOIN_BATTLEGROUP_PROPERTY = "JOIN_BATTLEGROUP_PROPERTY";

        public static void JoinBattleGroup(GamePlayer player, byte response)
        {
            BattleGroup mybattlegroup = player.TempProperties.getProperty<BattleGroup>(JOIN_BATTLEGROUP_PROPERTY, null);
            if (mybattlegroup == null) return;
            lock (mybattlegroup)
            {
                if (mybattlegroup.Members.Count < 1)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Commands.Players.Battlegroup.NoBattleGroup"), eChatType.CT_BattleGroup, eChatLoc.CL_SystemWindow);
                    return;
                }
                if (response == 0x01)
                {
                    mybattlegroup.AddBattlePlayer(player, false);
                }
                player.TempProperties.removeProperty(JOIN_BATTLEGROUP_PROPERTY);
            }
        }
    }
}