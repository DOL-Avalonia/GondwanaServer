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

namespace DOL.GS.ServerRules
{
    /// <summary>
    /// Handles DF entrance jump point allowing only one realm to enter on Normal server type.
    /// </summary>
    public class DFEnterJumpPoint : IJumpPointHandler
    {
        /// <summary>
        /// Decides whether player can jump to the target point.
        /// All messages with reasons must be sent here.
        /// Can change destination too.
        /// </summary>
        /// <param name="targetPoint">The jump destination</param>
        /// <param name="player">The jumping player</param>
        /// <returns>True if allowed</returns>
        public bool IsAllowedToJump(ZonePoint targetPoint, GamePlayer player)
        {
            if (player.Client.Account.PrivLevel > 1)
            {
                return true;
            }
            if (GameServer.Instance.Configuration.ServerType != eGameServerType.GST_Normal)
                return true;
            if (ServerProperties.Properties.ALLOW_ALL_REALMS_DF)
                return true;
            return (player.Realm == DarknessFallOwner);
        }

        public static eRealm DarknessFallOwner = eRealm.None;

        /// <summary>
        /// initialize the darkness fall entrance system
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        [ScriptLoadedEvent]
        public static void OnScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            CheckDFOwner();
            GameEventMgr.AddHandler(KeepEvent.KeepTaken, new DOLEventHandler(OnKeepTaken));
        }

        /// <summary>
        /// check if a realm have more keep at start
        /// to know the DF owner
        /// </summary>

        private static void CheckDFOwner()
        {
            int albcount = GameServer.KeepManager.GetTowerCountByRealm(eRealm.Albion);
            int midcount = GameServer.KeepManager.GetTowerCountByRealm(eRealm.Midgard);
            int hibcount = GameServer.KeepManager.GetTowerCountByRealm(eRealm.Hibernia);

            if (albcount > midcount && albcount > hibcount)
            {
                DarknessFallOwner = eRealm.Albion;
            }
            if (midcount > albcount && midcount > hibcount)
            {
                DarknessFallOwner = eRealm.Midgard;
            }
            if (hibcount > midcount && hibcount > albcount)
            {
                DarknessFallOwner = eRealm.Hibernia;
            }
        }

        /// <summary>
        /// when  keep is taken it check if the realm which take gain the control of DF
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="arguments"></param>
        public static void OnKeepTaken(DOLEvent e, object sender, EventArgs arguments)
        {
            KeepEventArgs args = arguments as KeepEventArgs;
            eRealm realm = (eRealm)args.Keep.Realm;
            if (realm != DarknessFallOwner)
            {
                int currentDFOwnerTowerCount = GameServer.KeepManager.GetTowerCountByRealm(DarknessFallOwner);
                int challengerOwnerTowerCount = GameServer.KeepManager.GetTowerCountByRealm(realm);
                if (currentDFOwnerTowerCount < challengerOwnerTowerCount)
                    DarknessFallOwner = realm;
            }
        }

    }
}
