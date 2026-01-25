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
using DOL.Database;
using System.Collections.Generic;
using DOL.GS.Behaviour;
using System.Linq;
using System.Threading.Tasks;

namespace DOL.GS.PacketHandler
{
    [PacketLib(187, GameClient.eClientVersion.Version187)]
    public class PacketLib187 : PacketLib186
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Version 1.87 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib187(GameClient client)
            : base(client)
        {
        }

        public override Task SendQuestOfferWindow(GameNPC questNPC, GamePlayer player, IQuestPlayerData quest)
        {
            return SendQuestWindow(questNPC, player, quest, true);
        }

        public override Task SendQuestRewardWindow(GameNPC questNPC, GamePlayer player, IQuestPlayerData quest)
        {
            return SendQuestWindow(questNPC, player, quest, false);
        }

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

            string personalizedSummary = await quest.Quest.GetSummaryForPlayer(player);
            if (personalizedSummary.Length > 255)
                pak.WritePascalString(personalizedSummary.Substring(0, 255)); // Summary is max 255 bytes !
            else
                pak.WritePascalString(personalizedSummary);

            if (offer)
            {
                string personalizedStory = await quest.Quest.GetStoryForPlayer(player);

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
            foreach (var desc in await Task.WhenAll(quest.Goals.Select(g => AutoTranslateManager.Translate(m_gameClient.Player, g.Description))))
            {
                pak.WritePascalString($"{desc}\r");
            }
            pak.WriteByte(quest.Quest.MinLevel);
            pak.WriteByte(0); // Money percent level
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
                WriteTemplateData(pak, reward, 1);
            pak.WriteByte((byte)quest.FinalRewards.ChoiceOf);
            pak.WriteByte((byte)quest.FinalRewards.OptionalItems.Count);
            foreach (ItemTemplate reward in quest.FinalRewards.OptionalItems)
                WriteTemplateData(pak, reward, 1);
            SendTCP(pak);
        }

        protected virtual void WriteTemplateData(GSTCPPacketOut pak, ItemTemplate template, int count)
        {
            if (template == null)
            {
                pak.Fill(0x00, 19);
                return;
            }

            pak.WriteByte((byte)template.Level);

            int value1;
            int value2;

            switch (template.Object_Type)
            {
                case (int)eObjectType.Arrow:
                case (int)eObjectType.Bolt:
                case (int)eObjectType.Poison:
                case (int)eObjectType.GenericItem:
                    value1 = count; // Count
                    value2 = template.SPD_ABS;
                    break;
                case (int)eObjectType.Thrown:
                    value1 = template.DPS_AF;
                    value2 = count; // Count
                    break;
                case (int)eObjectType.Instrument:
                    value1 = (template.DPS_AF == 2 ? 0 : template.DPS_AF);
                    value2 = 0;
                    break;
                case (int)eObjectType.Shield:
                    value1 = template.Type_Damage;
                    value2 = template.DPS_AF;
                    break;
                case (int)eObjectType.AlchemyTincture:
                case (int)eObjectType.SpellcraftGem:
                    value1 = 0;
                    value2 = 0;
                    /*
					must contain the quality of gem for spell craft and think same for tincture
					*/
                    break;
                case (int)eObjectType.GardenObject:
                    value1 = 0;
                    value2 = template.SPD_ABS;
                    /*
					Value2 byte sets the width, only lower 4 bits 'seem' to be used (so 1-15 only)

					The byte used for "Hand" (IE: Mini-delve showing a weapon as Left-Hand
					usabe/TwoHanded), the lower 4 bits store the height (1-15 only)
					*/
                    break;

                default:
                    value1 = template.DPS_AF;
                    value2 = template.SPD_ABS;
                    break;
            }
            pak.WriteByte((byte)value1);
            pak.WriteByte((byte)value2);

            if (template.Object_Type == (int)eObjectType.GardenObject)
                pak.WriteByte((byte)(template.DPS_AF));
            else
                pak.WriteByte((byte)(template.Hand << 6));
            pak.WriteByte((byte)((template.Type_Damage > 3
                ? 0
                : template.Type_Damage << 6) | template.Object_Type));
            pak.WriteShort((ushort)template.Weight);
            pak.WriteByte(template.BaseConditionPercent);
            pak.WriteByte(template.BaseDurabilityPercent);
            pak.WriteByte((byte)template.Quality);
            pak.WriteByte((byte)template.Bonus);
            pak.WriteShort((ushort)template.Model);
            pak.WriteByte((byte)template.Extension);
            if (template.Emblem != 0)
                pak.WriteShort((ushort)template.Emblem);
            else
                pak.WriteShort((ushort)template.Color);
            pak.WriteByte((byte)0); // Flag
            pak.WriteByte((byte)template.Effect);
            if (count > 1)
                pak.WritePascalString(String.Format("{0} {1}", count, template.Name));
            else
                pak.WritePascalString(template.Name);
        }

        public override async Task SendQuestListUpdate()
        {
            if (m_gameClient == null || m_gameClient.Player == null)
                return;

            await SendTaskInfo();
            int questIndex = 1;
            foreach (var quest in m_gameClient.Player.QuestList)
                await SendQuestPacket(quest, questIndex++);
            
            while (questIndex <= 25)
                await SendQuestPacket(null, questIndex++);
        }

        protected override async Task SendQuestPacket(IQuestPlayerData data, int index)
        {
            await using GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.QuestEntry));
            string name = $"{data.Quest.Name} (Level {data.Quest.MinLevel})";
                
            GamePlayer receiver = m_gameClient.Player;
            string desc = data.Quest.Description ?? string.Empty;

            // --- AUTOTRANSLATE HOOK FOR QUEST TEXTS ---
            // We treat quest texts as "server texts", so sender = null
            name = await AutoTranslateManager.Translate(null, receiver, name);
            desc = await AutoTranslateManager.Translate(null, receiver, desc);

            if (name.Length > byte.MaxValue)
                name = name.Substring(0, 256);
            pak.WriteByte((byte)index);
            pak.WriteByte((byte)name.Length);
            pak.WriteShort((ushort)data.Status);
            pak.WriteByte((byte)data.VisibleGoals.Count);
            pak.WriteByte(data.Quest.MinLevel);
            pak.WriteStringBytes(name);
            pak.WritePascalString(desc);
            for (var idx = 0; idx < data.VisibleGoals.Count; ++idx)
            {
                var goal = data.VisibleGoals[idx];
                desc = await AutoTranslateManager.Translate(null, receiver, goal.Description);
                if (goal.ProgressTotal == 1)
                    desc = $"{desc}\r";
                else
                    desc = $"{desc} ({goal.Progress} / {goal.ProgressTotal})\r";
                pak.WriteShortLowEndian((ushort)desc.Length);
                pak.WriteStringBytes(desc);
                pak.WriteShortLowEndian(goal.PointA.ZoneId);
                pak.WriteShortLowEndian(goal.PointA.X);
                pak.WriteShortLowEndian(goal.PointA.Y);
                pak.WriteShortLowEndian(0x00); // unknown
                pak.WriteShortLowEndian((ushort)goal.Type);
                pak.WriteShortLowEndian(0x00); // unknown
                pak.WriteShortLowEndian(goal.PointB.ZoneId);
                pak.WriteShortLowEndian(goal.PointB.X);
                pak.WriteShortLowEndian(goal.PointB.Y);
                pak.WriteByte((byte)goal.Status);
                if (goal.QuestItem == null)
                {
                    pak.WriteByte(0x00);
                }
                else
                {
                    pak.WriteByte((byte)idx);
                    WriteTemplateData(pak, goal.QuestItem, 1);
                }
            }

            SendTCP(pak);
        }
    }
}
