using System;
using log4net;
using DOL.GS.Quests;
using System.Reflection;
using System.Threading.Tasks;

namespace DOL.GS.PacketHandler
{
    [PacketLib(183, GameClient.eClientVersion.Version183)]
    public class PacketLib183 : PacketLib182
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Version 1.83 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib183(GameClient client) : base(client)
        {
        }

        protected override async Task SendQuestPacket(IQuestPlayerData quest, int index)
        {
            await using GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(GetPacketCode(eServerPackets.QuestEntry));
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
                var receiver = m_gameClient.Player;
                name = await AutoTranslateManager.Translate(receiver, name);
                desc = await AutoTranslateManager.Translate(receiver, desc);
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

        protected override async Task SendTaskInfo()
        {
            string name = await BuildTaskString();
            await using GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(GetPacketCode(eServerPackets.QuestEntry));
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