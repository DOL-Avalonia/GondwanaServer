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
using DOL.Database;
using DOL.GS.Behaviour;
using DOL.GS.Quests;
using log4net;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DOL.GS.PacketHandler
{
    [PacketLib(194, GameClient.eClientVersion.Version194)]
    public class PacketLib194 : PacketLib193
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        const ushort MAX_STORY_LENGTH = 1000;   // Via trial and error, 1.108 client. 
                                                // Often will cut off text around 990 but longer strings do not result in any errors. -Tolakram

        protected override async Task SendQuestWindow(GameNPC questNPC, GamePlayer player, IQuestPlayerData quest, bool offer)
        {
            await using GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog));
            pak.WriteShort((offer) ? (byte)0x22 : (byte)0x21); // Dialog
            pak.WriteShort(quest.Quest.Id);
            pak.WriteShort((ushort)questNPC.ObjectID);
            pak.WriteByte(0x00); // unknown
            pak.WriteByte(0x00); // unknown
            pak.WriteByte(0x00); // unknown
            pak.WriteByte(0x00); // unknown
            pak.WriteByte((offer) ? (byte)0x02 : (byte)0x01); // Accept/Decline or Finish/Not Yet
            pak.WriteByte(0x01); // Wrap
            pak.WritePascalString(quest.Quest.Name);

            String personalizedSummary = await quest.Quest.GetSummaryForPlayer(player);
            if (personalizedSummary.Length > 255)
                pak.WritePascalString(personalizedSummary.Substring(0, 255)); // Summary is max 255 bytes !
            else
                pak.WritePascalString(personalizedSummary);

            if (offer)
            {
                String personalizedStory = await quest.Quest.GetStoryForPlayer(player);
                if (personalizedStory.Length > ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH)
                {
                    pak.WriteShort((ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH);
                    pak.WriteStringBytes(personalizedStory.Substring(0, ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH));
                }
                else
                {
                    pak.WriteShort((ushort)personalizedStory.Length);
                    pak.WriteStringBytes(personalizedStory);
                }
            }
            else
            {
                string personalizedConclusion = await quest.Quest.GetConclusionForPlayer(player);
                if (personalizedConclusion.Length > (ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH)
                {
                    pak.WriteShort((ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH);
                    pak.WriteStringBytes(personalizedConclusion.Substring(0, (ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH));
                }
                else
                {
                    pak.WriteShort((ushort)personalizedConclusion.Length);
                    pak.WriteStringBytes(personalizedConclusion);
                }
            }

            pak.WriteShort(quest.Quest.Id);
            pak.WriteByte((byte)quest.Goals.Count); // #goals count
            foreach (var desc in await Task.WhenAll(quest.Goals.Select(g => AutoTranslateManager.MaybeTranslate(m_gameClient.Player, g.Description))))
            {
                pak.WritePascalString($"{desc}\r");
            }
            pak.WriteInt((uint)(quest.FinalRewards.Money)); // unknown, new in 1.94
            if (quest.FinalRewards.Experience > 0)
            {
                pak.WriteByte((byte)GamePlayerUtils.GetExperiencePercentForCurrentLevel(player, quest.FinalRewards.Experience));
            }
            else
            {
                pak.WriteByte((byte)(-1 * long.Clamp(quest.FinalRewards.Experience, -100, 0)));
            }
            pak.WriteByte((byte)quest.FinalRewards.BasicItems.Count);
            foreach (ItemTemplate reward in quest.FinalRewards.BasicItems)
            {
                WriteItemData(pak, GameInventoryItem.Create(reward));
            }
            pak.WriteByte((byte)quest.FinalRewards.ChoiceOf);
            pak.WriteByte((byte)quest.FinalRewards.OptionalItems.Count);
            foreach (ItemTemplate reward in quest.FinalRewards.OptionalItems)
            {
                WriteItemData(pak, GameInventoryItem.Create(reward));
            }
            SendTCP(pak);
        }

        /// <summary>
        /// Constructs a new PacketLib for Version 1.94 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib194(GameClient client)
            : base(client)
        {
        }
    }
}
