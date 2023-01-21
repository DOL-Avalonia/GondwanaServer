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
using System.Reflection;
using log4net;

namespace DOL.GS.PacketHandler
{
    [PacketLib(193, GameClient.eClientVersion.Version193)]
    public class PacketLib193 : PacketLib192
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Version 1.93 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib193(GameClient client)
            : base(client)
        {
        }

        public override void SendBlinkPanel(byte flag)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {
                GamePlayer player = base.m_gameClient.Player;

                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte((byte)8);
                pak.WriteByte((byte)flag);
                pak.WriteByte((byte)0);

                SendTCP(pak);
            }
        }
    }
}
