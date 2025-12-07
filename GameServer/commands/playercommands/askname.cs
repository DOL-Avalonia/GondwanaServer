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
using System.Linq;

using DOL.GS.Friends;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&askname",
        ePrivLevel.Player,
        "Commands.Players.Askname.Description",
        "Commands.Players.Askname.Usage")]
    public class AskNameCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length > 2 || client.Player.TargetObject == null || client.Player.TargetObject is not GamePlayer targetPlayer)
            {
                return;
            }

            if (IsSpammingCommand(client.Player, "askname"))
                return;

            string name = targetPlayer.Name;

            if (args.Length == 1)
            {
                if (!client.Player.SerializedAskNameList.Contains(name))
                {
                    var requestText = LanguageMgr.GetTranslation(targetPlayer.Client, "Commands.Players.Askname.Request", targetPlayer.GetPersonalizedName(client.Player));
                    targetPlayer.Out.SendDialogBox(eDialogCode.AskName, (ushort)(client.SessionID), 0, 0, 0, eDialogType.YesNo, false, requestText);
                }
            }
            else if (args.Length == 2 && args[1] == "remove")
            {
                // Remove player from SerializedAskNameList if there
                client.Player.SerializedAskNameList = client.Player.SerializedAskNameList.Where(x => x != name).ToArray();
                // Save to database
                client.Player.SaveIntoDatabase();
                // Update player in world SendLivingDataUpdate
                targetPlayer.Out.SendObjectRemove(client.Player);
                targetPlayer.Out.SendPlayerCreate(client.Player);
                targetPlayer.Out.SendLivingEquipmentUpdate(client.Player);
                client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Askname.Removed", name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
    }
}