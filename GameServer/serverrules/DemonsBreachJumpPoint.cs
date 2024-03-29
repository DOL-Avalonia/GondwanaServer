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

using DOL.Database;
using DOL.GS.Keeps;
using DOL.GS.PacketHandler;
using DOL.Events;
using DOL.Language;

namespace DOL.GS.ServerRules
{
    public class NergalsBreachJumpPoint : IJumpPointHandler
    {
        public bool IsAllowedToJump(ZonePoint targetPoint, GamePlayer player)
        {
            if (player.Client.Account.PrivLevel > 1)
            {
                return true;
            }
            if (player.Level < 5)
            {
                return true;
            }
            player.Client.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DemonsBreachJumpPoint.Requirements"), eChatType.CT_System, eChatLoc.CL_ChatWindow);
            return false;
        }

    }

    public class BalbansBreachJumpPoint : IJumpPointHandler
    {
        public bool IsAllowedToJump(ZonePoint targetPoint, GamePlayer player)
        {
            if (player.Client.Account.PrivLevel > 1)
            {
                return true;
            }
            if (player.Level < 10 && player.Level > 4)
            {
                return true;
            }
            player.Client.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DemonsBreachJumpPoint.Requirements"), eChatType.CT_System, eChatLoc.CL_ChatWindow);
            return false;
        }
    }
}
