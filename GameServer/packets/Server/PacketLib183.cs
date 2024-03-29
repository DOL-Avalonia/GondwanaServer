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
using log4net;
using DOL.GS.Quests;
using System.Reflection;

namespace DOL.GS.PacketHandler
{
    [PacketLib(183, GameClient.eClientVersion.Version183)]
    public class PacketLib183 : PacketLib182
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Version 1.83 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib183(GameClient client) : base(client)
        {
        }
        protected override void SendQuestPacket(IQuestPlayerData quest, int index)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.QuestEntry)))
            {
                pak.WriteByte((byte)index);
                if (quest.Status != eQuestStatus.InProgress)
                {
                    pak.WriteByte(0);
                    pak.WriteByte(0);
                    pak.WriteByte(0);
                    pak.WriteByte(0);
                }
                else
                {
                    string name = quest.Quest.Name;
                    string desc = quest.Quest.Description;
                    if (name.Length > byte.MaxValue)
                    {
                        if (log.IsWarnEnabled) log.Warn("quest name is too long for 1.71 clients (" + name.Length + ") '" + name + "'");
                        name = name.Substring(0, byte.MaxValue);
                    }
                    if (desc.Length > ushort.MaxValue)
                    {
                        if (log.IsWarnEnabled) log.Warn("quest description is too long for 1.71 clients (" + desc.Length + ") '" + desc + "'");
                        desc = desc.Substring(0, ushort.MaxValue);
                    }
                    if (name.Length + desc.Length > 2048 - 10)
                    {
                        if (log.IsWarnEnabled) log.Warn("quest name + description length is too long and would have crashed the client.\nName (" + name.Length + "): '" + name + "'\nDesc (" + desc.Length + "): '" + desc + "'");
                        name = name.Substring(0, 32);
                        desc = desc.Substring(0, 2048 - 10 - name.Length); // all that's left
                    }
                    pak.WriteByte((byte)name.Length);
                    pak.WriteShortLowEndian((ushort)desc.Length);
                    pak.WriteByte(0);
                    pak.WriteStringBytes(name); //Write Quest Name without trailing 0
                    pak.WriteStringBytes(desc); //Write Quest Description without trailing 0
                }
                SendTCP(pak);
            }
        }

        protected override void SendTaskInfo()
        {
            string name = BuildTaskString();

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.QuestEntry)))
            {
                pak.WriteByte(0); //index
                pak.WriteShortLowEndian((ushort)name.Length);
                pak.WriteByte((byte)0);
                pak.WriteByte((byte)0);
                pak.WriteStringBytes(name); //Write Quest Name without trailing 0
                pak.WriteStringBytes(""); //Write Quest Description without trailing 0
                SendTCP(pak);
            }
        }
    }
}