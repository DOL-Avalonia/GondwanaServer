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
using DOL.Language;
using System.Linq;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&group",
        new string[] { "&g" },
        ePrivLevel.Player,
        "Commands.Players.Group.Description",
        "Commands.Players.Group.Usage")]
    public class GCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.Group == null)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Group.NotInGroup"
                    )
                );
                return;
            }

            if (IsSpammingCommand(client.Player, "group", 500))
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Group.SlowDown"
                    )
                );
                return;
            }

            if (args.Length >= 2)
            {
                client.Player.Group.SendMessageToGroupMembers(client.Player, string.Join(' ', args.Skip(1)), eChatType.CT_Group, eChatLoc.CL_ChatWindow);
            }
            else
            {
                DisplaySyntax(client);
            }
        }
    }
}