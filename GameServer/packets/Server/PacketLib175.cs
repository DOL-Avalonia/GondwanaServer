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
using System.Collections.Generic;
using System.Reflection;
using DOL.GS.PlayerTitles;
using log4net;
using DOL.GS.Housing;
using DOL.GS.PropertyCalc;
using System.Linq;

namespace DOL.GS.PacketHandler
{
    [PacketLib(175, GameClient.eClientVersion.Version175)]
    public class PacketLib175 : PacketLib174
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Version 1.75 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib175(GameClient client) : base(client)
        {
        }

        public override void SendCustomTextWindow(string caption, IList<string> text)
        {
            if (text == null)
                return;

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DetailWindow)))
            {
                pak.WriteByte(0); // new in 1.75
                if (caption == null)
                    caption = "";
                if (caption.Length > byte.MaxValue)
                    caption = caption.Substring(0, byte.MaxValue);
                pak.WritePascalString(caption); //window caption

                WriteCustomTextWindowData(pak, text);

                //Trailing Zero!
                pak.WriteByte(0);
                SendTCP(pak);
            }
        }

        public override void SendPlayerTitles()
        {
            var titles = m_gameClient.Player.Titles;
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DetailWindow)))
            {
                pak.WriteByte(1); // new in 1.75
                pak.WritePascalString("Player Statistics"); //window caption

                byte line = 1;
                foreach (string str in m_gameClient.Player.FormatStatistics())
                {
                    pak.WriteByte(line++);
                    pak.WritePascalString(str);
                }

                pak.WriteByte(200);
                long titlesCountPos = pak.Position;
                pak.WriteByte(0); // length of all titles part
                pak.WriteByte((byte)titles.Count);
                line = 0;
                foreach (IPlayerTitle title in titles)
                {
                    pak.WriteByte(line++);
                    pak.WritePascalString(title.GetDescription(m_gameClient.Player));
                }
                long titlesLen = (pak.Position - titlesCountPos - 1); // include titles count
                if (titlesLen > byte.MaxValue)
                    log.WarnFormat("Titles block is too long! {0} (player: {1})", titlesLen, m_gameClient.Player);
                //Trailing Zero!
                pak.WriteByte(0);
                //Set titles length
                pak.Position = titlesCountPos;
                pak.WriteByte((byte)titlesLen); // length of all titles part
                SendTCP(pak);
            }
        }

        public override void SendPlayerTitleUpdate(GamePlayer player)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {
                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte(0x0B); // subcode
                IPlayerTitle title = player.CurrentTitle;
                if (title == PlayerTitleMgr.ClearTitle)
                {
                    pak.WriteByte(0); // flag
                    pak.WriteInt(0); // unk1 + str len
                }
                else
                {
                    pak.WriteByte(1); // flag
                    string val = GameServer.ServerRules.GetPlayerTitle(m_gameClient.Player, player);
                    pak.WriteShort((ushort)val.Length);
                    pak.WriteShort(0); // unk1
                    pak.WriteStringBytes(val);
                }
                SendTCP(pak);
            }
        }

        public override void SendUpdatePlayer()
        {
            GamePlayer player = m_gameClient.Player;
            if (player == null)
                return;

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
            {
                pak.WriteByte(0x03); //subcode
                pak.WriteByte(0x0e); //number of entry
                pak.WriteByte(0x00); //subtype
                pak.WriteByte(0x00); //unk
                                     //entry :

                pak.WriteByte(player.GetDisplayLevel(m_gameClient.Player)); //level
                pak.WritePascalString(m_gameClient.Player.GetPersonalizedName(player));

                pak.WriteByte((byte)(player.MaxHealth >> 8)); // maxhealth high byte ?
                pak.WritePascalString(player.CharacterClass.Name); // class name
                pak.WriteByte((byte)(player.MaxHealth & 0xFF)); // maxhealth low byte ?

                pak.WritePascalString( /*"The "+*/player.CharacterClass.Profession); // Profession

                pak.WriteByte(0x00); //unk

                pak.WritePascalString(player.CharacterClass.GetTitle(player, player.Level));

                //todo make function to calcule realm rank
                //client.Player.RealmPoints
                //todo i think it s realmpoint percent not realrank
                pak.WriteByte((byte)player.RealmLevel); //urealm rank
                pak.WritePascalString(player.RealmRankTitle(player.Client.Account.Language));
                pak.WriteByte((byte)player.RealmSpecialtyPoints); // realm skill points

                pak.WritePascalString(player.CharacterClass.BaseName); // base class

                pak.WriteByte((byte)(HouseMgr.GetHouseNumberByPlayer(player) >> 8)); // personal house high byte
                pak.WritePascalString(player.GuildName);
                pak.WriteByte((byte)(HouseMgr.GetHouseNumberByPlayer(player) & 0xFF)); // personal house low byte

                pak.WritePascalString(player.LastName);

                pak.WriteByte(0x0); // ML Level
                pak.WritePascalString(player.RaceName);

                pak.WriteByte(0x0);
                if (player.GuildRank != null)
                    pak.WritePascalString(player.GuildRank.Title);
                else
                    pak.WritePascalString("");
                pak.WriteByte(0x0);

                AbstractCraftingSkill skill = CraftingMgr.getSkillbyEnum(player.CraftingPrimarySkill);
                if (skill != null)
                    pak.WritePascalString(skill.Name); //crafter guilde: alchemist
                else
                    pak.WritePascalString("None"); //no craft skill at start

                pak.WriteByte(0x0);
                pak.WritePascalString(player.CraftTitle.GetValue(player, player)); //crafter title: legendary alchemist

                pak.WriteByte(0x0);
                pak.WritePascalString("None"); //ML title

                // new in 1.75
                pak.WriteByte(0x0);
                string title = "None";
                if (player.CurrentTitle != PlayerTitleMgr.ClearTitle)
                    title = GameServer.ServerRules.GetPlayerTitle(player, player);
                pak.WritePascalString(title); // new in 1.74
                SendTCP(pak);
            }
        }

        public override void SendCharStatsUpdate()
        {
            var player = m_gameClient.Player;
            if (player == null)
                return;

            eStat[] updateStats =
            [
                eStat.STR,
                eStat.DEX,
                eStat.CON,
                eStat.QUI,
                eStat.INT,
                eStat.PIE,
                eStat.EMP,
                eStat.CHR,
            ];

            var baseStats = new int[updateStats.Length];
            var itemStats = new int[updateStats.Length];
            var itemCaps = new int[updateStats.Length];
            var buffStats = new int[updateStats.Length];
            var abilityStats = new int[updateStats.Length];

            foreach (var (stat, i) in updateStats.Select((stat, i) => (stat, i)))
            {
                var prop = (eProperty)stat;
                baseStats[i] = player.GetBaseStat(stat);
                itemStats[i] = player.GetModifiedFromItems(prop);
                itemCaps[i] = (byte)StatCalculator.GetItemBonusCap(player, (eProperty)updateStats[i]);
                
                int acuityItemBonus = 0;
                if (player.CharacterClass.ClassType != eClassType.PureTank && (int)updateStats[i] == (int)player.CharacterClass.ManaStat)
                {
                    if (player.CharacterClass.ID != (int)eCharacterClass.Scout && player.CharacterClass.ID != (int)eCharacterClass.Hunter && player.CharacterClass.ID != (int)eCharacterClass.Ranger)
                    {
                        acuityItemBonus = player.AbilityBonus[(int)eProperty.Acuity];
                    }
                }
                abilityStats[i] = player.AbilityBonus[(int)updateStats[i]] + acuityItemBonus;
                buffStats[i] = player.GetModified(prop) - (baseStats[i] + itemStats[i] + abilityStats[i]);

                if (stat is eStat.CON)
                    baseStats[i] -= player.TotalConstitutionLostAtDeath;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.StatsUpdate)))
            {

                // base
                baseStats.Foreach(s => pak.WriteShort((ushort)s));
                pak.WriteShort(0);
                
                buffStats.Foreach(s => pak.WriteShort((ushort)s));
                pak.WriteShort(0);
                
                itemStats.Foreach(s => pak.WriteShort((ushort)s));
                pak.WriteShort(0);

                itemCaps.Foreach(s => pak.WriteByte((byte)s));
                pak.WriteByte(0);
                
                abilityStats.Foreach(s => pak.WriteByte((byte)s));
                pak.WriteByte(0);

                //Why don't we and mythic use this class bonus byte?
                //pak.Fill(0, 9);
                //if (player.CharacterClass.ID == (int)eCharacterClass.Vampiir)
                //	pak.WriteByte((byte)(player.Level - 5)); // Vampire bonuses
                //else
                pak.WriteByte(0x00); // FF if resists packet
                pak.WriteByte((byte)player.TotalConstitutionLostAtDeath);
                pak.WriteShort((ushort)player.MaxHealth);
                pak.WriteShort((ushort)0);

                SendTCP(pak);
            }
        }

        public override void SendCharResistsUpdate()
        {
            if (m_gameClient.Player == null)
                return;

            eResist[] updateResists =
            {
                eResist.Crush,
                eResist.Slash,
                eResist.Thrust,
                eResist.Heat,
                eResist.Cold,
                eResist.Matter,
                eResist.Body,
                eResist.Spirit,
                eResist.Energy,
            };

            var racial = new int[updateResists.Length];
            var items = new int[updateResists.Length];
            var caps = new int[updateResists.Length];
            var buffs = new int[updateResists.Length];
            var secondary = new int[updateResists.Length];
            
            foreach (var (stat, i) in updateResists.Select((stat, i) => (stat, i)))
            {
                var prop = (eProperty)stat;
                racial[i] = SkillBase.GetRaceResist(m_gameClient.Player.Race, stat, m_gameClient.Player);
                caps[i] = ResistCalculator.GetItemBonusCap(m_gameClient.Player, prop);
                items[i] = m_gameClient.Player.ItemBonus[(int)prop];
                secondary[i] = m_gameClient.Player.SpecBuffBonusCategory[(int)prop] + m_gameClient.Player.AbilityBonus[(int)prop];
                buffs[i] = m_gameClient.Player.GetModified(prop) - (racial[i] - items[i] - secondary[i]);
            }


            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.StatsUpdate)))
            {
                racial.Foreach(s => pak.WriteShort((ushort)s));
                buffs.Foreach(s => pak.WriteShort((ushort)s));
                items.Foreach(s => pak.WriteShort((ushort)s));
                caps.Foreach(s => pak.WriteByte((byte)s));
                secondary.Foreach(s => pak.WriteByte((byte)s));

                pak.WriteByte(0xFF); // FF if resists packet
                pak.WriteByte(0);
                pak.WriteShort(0);
                pak.WriteShort(0);

                SendTCP(pak);
            }
        }

        public override void SendPlayerCreate(GamePlayer playerToCreate)
        {
            Region playerRegion = playerToCreate.CurrentRegion;
            if (playerRegion == null)
            {
                if (log.IsWarnEnabled)
                    log.Warn("SendPlayerCreate: playerRegion == null");
                return;
            }

            Zone playerZone = playerToCreate.CurrentZone;
            if (playerZone == null)
            {
                if (log.IsWarnEnabled)
                    log.Warn("SendPlayerCreate: playerZone == null");
                return;
            }

            if (m_gameClient.Player == null || playerToCreate.IsVisibleTo(m_gameClient.Player) == false)
                return;

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PlayerCreate172)))
            {
                pak.WriteShort((ushort)playerToCreate.Client.SessionID);
                pak.WriteShort((ushort)playerToCreate.ObjectID);
                pak.WriteShort(playerToCreate.Model);
                pak.WriteShort((ushort)playerToCreate.Position.Z);
                //Dinberg:Instances - as with all objects, we need to use a zoneSkinID for clientside positioning.
                pak.WriteShort(playerZone.ZoneSkinID);
                pak.WriteShort(GetXOffsetInZone(playerToCreate));
                pak.WriteShort(GetYOffsetInZone(playerToCreate));
                pak.WriteShort(playerToCreate.Orientation.InHeading);

                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.EyeSize)); //1-4 = Eye Size / 5-8 = Nose Size
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.LipSize)); //1-4 = Ear size / 5-8 = Kin size
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.MoodType)); //1-4 = Ear size / 5-8 = Kin size
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.EyeColor)); //1-4 = Skin Color / 5-8 = Eye Color
                pak.WriteByte(playerToCreate.GetDisplayLevel(m_gameClient.Player));
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.HairColor)); //Hair: 1-4 = Color / 5-8 = unknown
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.FaceType)); //1-4 = Unknown / 5-8 = Face type
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.HairStyle)); //1-4 = Unknown / 5-8 = Hair Style

                int flags = (GameServer.ServerRules.GetLivingRealm(m_gameClient.Player, playerToCreate) & 0x03) << 2;
                if (playerToCreate.IsAlive == false) flags |= 0x01;
                if (playerToCreate.IsUnderwater) flags |= 0x02; //swimming
                if (playerToCreate.IsStealthed) flags |= 0x10;
                if (playerToCreate.IsWireframe) flags |= 0x20;
                if (playerToCreate.CharacterClass.ID == (int)eCharacterClass.Vampiir) flags |= 0x40; //Vamp fly
                pak.WriteByte((byte)flags);
                pak.WriteByte(0x00); // new in 1.74

                pak.WritePascalString(GameServer.ServerRules.GetPlayerName(m_gameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerGuildName(m_gameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerLastName(m_gameClient.Player, playerToCreate));
                //RR 12 / 13
                pak.WritePascalString(GameServer.ServerRules.GetPlayerPrefixName(m_gameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerTitle(m_gameClient.Player, playerToCreate)); // new in 1.74, NewTitle
                pak.WriteByte(0x00); // new in 1.75
                SendTCP(pak);
            }

            // Update Cache
            m_gameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(playerToCreate.CurrentRegionID, (ushort)playerToCreate.ObjectID)] = GameTimer.GetTickCount();

            SendObjectGuildID(playerToCreate, playerToCreate.Guild); //used for nearest friendly/enemy object buttons and name colors on PvP server
        }

        public override void SendLoginGranted(byte color)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.LoginGranted)))
            {
                pak.WriteByte(0x01); //isSI
                pak.WriteByte(ParseVersion((int)m_gameClient.Version, true));
                pak.WriteByte(ParseVersion((int)m_gameClient.Version, false));
                //pak.WriteByte(build);
                pak.WriteByte(0x00);
                pak.WritePascalString(m_gameClient.Account.Name);
                pak.WritePascalString(GameServer.Instance.Configuration.ServerNameShort); //server name
                pak.WriteByte(0x0C); //Server ID
                pak.WriteByte(color);
                pak.WriteByte(0x00);
                pak.WriteByte(0x00); // new in 1.75
                SendTCP(pak);
            }
        }

        public override void SendLoginGranted()
        {
            if (GameServer.ServerRules.IsAllowedCharsInAllRealms(m_gameClient))
            {
                SendLoginGranted(1);
            }
            else
            {
                SendLoginGranted(ServerTypeID);
            }
        }
    }
}
