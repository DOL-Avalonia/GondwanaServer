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

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&salvage",
        ePrivLevel.Player,
        "Commands.Players.Salvage.Description",
        "Commands.Players.Salvage.Usage")]
    public class SalvageCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (IsSpammingCommand(client.Player, "salvage"))
                return;

            WorldInventoryItem item = client.Player.TargetObject as WorldInventoryItem;
            if (item == null)
                return;
            client.Player.SalvageItem(item.Item);
        }
    }
}