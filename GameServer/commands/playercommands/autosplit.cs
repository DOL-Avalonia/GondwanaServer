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
using DOL.Language;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [CmdAttribute("&autosplit",
         ePrivLevel.Player,
         "Commands.Players.Autosplit.Description",
         "Commands.Players.Autosplit.Usage",
         "Commands.Players.Autosplit.Usage.Coin",
         "Commands.Players.Autosplit.Usage.Loot",
         "Commands.Players.Autosplit.Usage.Self")]
    public class AutosplitCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            // If they are not in a group, then this command should not work at all
            if (client.Player.Group == null)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Autosplit.InGroup"
                    )
                );
                return;
            }

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }

            string command = args[1].ToLower();

            // /autosplit for leaders -- Make sue it is the group leader using this command, if it is, execute it.
            if (command == "on" || command == "off" || command == "coins" || command == "loot")
            {
                if (client.Player != client.Player.Group.Leader)
                {
                    DisplayMessage(
                        client,
                        LanguageMgr.GetTranslation(
                            client.Account.Language,
                            "Commands.Players.Autosplit.Leader"
                        )
                    );
                    return;
                }

                switch (command)
                {
                    case "on":
                        {
                            client.Player.Group.AutosplitLoot = true;
                            client.Player.Group.AutosplitCoins = true;
                            client.Player.Group.SendMessageToGroupMembers(
                                LanguageMgr.GetTranslation(
                                    client.Account.Language,
                                    "Commands.Players.Autosplit.On"
                                ),
                                eChatType.CT_System,
                                eChatLoc.CL_SystemWindow
                            );
                            break;
                        }

                    case "off":
                        {
                            client.Player.Group.AutosplitLoot = false;
                            client.Player.Group.AutosplitCoins = false;
                            client.Player.Group.SendMessageToGroupMembers(
                                LanguageMgr.GetTranslation(
                                    client.Account.Language,
                                    "Commands.Players.Autosplit.Off"
                                ),
                                eChatType.CT_System,
                                eChatLoc.CL_SystemWindow
                            );
                            break;
                        }
                    case "coins":
                        {
                            client.Player.Group.AutosplitCoins = !client.Player.Group.AutosplitCoins;
                            client.Player.Group.SendMessageToGroupMembers(
                                (client.Player.Group.AutosplitCoins ?
                                    LanguageMgr.GetTranslation(
                                        client.Account.Language,
                                        "Commands.Players.Autosplit.Coins.On"
                                    )
                                    :
                                    LanguageMgr.GetTranslation(
                                        client.Account.Language,
                                        "Commands.Players.Autosplit.Coins.Off"
                                    )
                                ),
                                eChatType.CT_System,
                                eChatLoc.CL_SystemWindow
                            );
                            break;
                        }
                    case "loot":
                        {
                            client.Player.Group.AutosplitLoot = !client.Player.Group.AutosplitLoot;
                            client.Player.Group.SendMessageToGroupMembers(
                                (client.Player.Group.AutosplitCoins ?
                                    LanguageMgr.GetTranslation(
                                        client.Account.Language,
                                        "Commands.Players.Autosplit.Loot.On"
                                    ) :
                                    LanguageMgr.GetTranslation(
                                        client.Account.Language,
                                        "Commands.Players.Autosplit.Loot.Off"
                                    )
                                ),
                                eChatType.CT_System,
                                eChatLoc.CL_SystemWindow
                            );
                            break;
                        }
                }
                return;
            }

            // /autosplit for Members including leader -- 
            if (command == "self")
            {
                client.Player.AutoSplitLoot = !client.Player.AutoSplitLoot;
                client.Player.Group.SendMessageToGroupMembers(
                    (client.Player.AutoSplitLoot ?
                        LanguageMgr.GetTranslation(
                            client.Account.Language,
                            "Commands.Players.Autosplit.Self.On",
                            client.Player.Name
                        ) :
                        LanguageMgr.GetTranslation(
                            client.Account.Language,
                            "Commands.Players.Autosplit.Self.Off",
                            client.Player.Name
                        )
                    ),
                    eChatType.CT_System,
                    eChatLoc.CL_SystemWindow
                );
                return;
            }

            //if nothing matched, then they tried to invent thier own commands -- show syntax
            DisplaySyntax(client);
        }
    }
}