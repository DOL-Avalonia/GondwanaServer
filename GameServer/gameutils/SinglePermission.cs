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
using DOL.Database;

namespace DOL.GS
{
    /// <summary>
    /// SinglePermission is special permission of command for player
    /// </summary>
    public class SinglePermission
    {
        protected SinglePermission()
        {

        }

        public static bool HasPermission(GamePlayer player, string command)
        {
            var obj = DOLDB<DBSinglePermission>.SelectObject(DB.Column(nameof(DBSinglePermission.Command)).IsEqualTo(command).And(DB.Column(nameof(DBSinglePermission.PlayerID)).IsEqualTo(player.ObjectId).Or(DB.Column(nameof(DBSinglePermission.PlayerID)).IsEqualTo(player.AccountName))));
            if (obj == null)
                return false;
            return true;
        }

        public static void setPermission(GamePlayer player, string command)
        {
            DBSinglePermission perm = new DBSinglePermission();
            perm.Command = command;
            perm.PlayerID = player.ObjectId;
            GameServer.Database.AddObject(perm);
        }

        public static void setPermissionAccount(GamePlayer player, string command)
        {
            DBSinglePermission perm = new DBSinglePermission();
            perm.Command = command;
            perm.PlayerID = player.AccountName;
            GameServer.Database.AddObject(perm);
        }

        public static bool removePermission(GamePlayer player, string command)
        {
            var obj = DOLDB<DBSinglePermission>.SelectObject(DB.Column(nameof(DBSinglePermission.Command)).IsEqualTo(command).And(DB.Column(nameof(DBSinglePermission.PlayerID)).IsEqualTo(player.ObjectId)));
            if (obj == null)
            {
                return false;
            }
            GameServer.Database.DeleteObject(obj);
            return true;
        }

        public static bool removePermissionAccount(GamePlayer player, string command)
        {
            var obj = DOLDB<DBSinglePermission>.SelectObject(DB.Column(nameof(DBSinglePermission.Command)).IsEqualTo(command).And(DB.Column(nameof(DBSinglePermission.PlayerID)).IsEqualTo(player.AccountName)));
            if (obj == null)
            {
                return false;
            }
            GameServer.Database.DeleteObject(obj);
            return true;
        }
    }
}