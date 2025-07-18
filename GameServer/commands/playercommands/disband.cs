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
using AmteScripts.Managers;
using DOL.GS.PacketHandler;
using DOL.Language;
using System.Linq;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&disband",
        ePrivLevel.Player,
        "Commands.Players.Disband.Description",
        "Commands.Players.Disband.Usage",
        "Commands.Players.Disband.Usage.Name")]
    public class DisbandCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.Group == null)
            {
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Disband.NotInGroup"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (args.Length < 2)//disband myslef
            {
                client.Player.Group.MemberDisband(client.Player);
                return;
            }
            else//disband by name
            {
                if (client.Player.Group.Leader != client.Player)
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Disband.NotLeader"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                string name = args[1];

                if (name.Equals(client.Player.Name, System.StringComparison.OrdinalIgnoreCase))
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Disband.NoYourself"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                int startCount = client.Player.Group.MemberCount;

                foreach (GameLiving living in client.Player.Group.GetMembersInTheGroup().Where(gl => gl.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
                {
                    client.Player.Group.RemoveMember(living);
                }

                //no target found to remove
                if (client.Player.Group != null && client.Player.Group.MemberCount == startCount)
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Disband.NoPlayer"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }
        }

        private bool CheckDisbandAllowed(GamePlayer player)
        {
            if (!player.IsInPvP || !PvpManager.Instance.IsOpen)
                return true;

            var session = PvpManager.Instance.CurrentSession;
            if (session == null)
                return true;

            if (!session.AllowGroupDisbandCreate)
            {
                player.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        player.Client.Account.Language,
                        "Commands.Players.Disband.NotAllowed"),
                    eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return false;
            }

            return true;
        }
    }
}
