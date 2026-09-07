using System;
using log4net;
using DOL.GS.Quests;
using System.Reflection;
using DOL.GS.ServerProperties;
using System.Threading.Tasks;

namespace DOL.GS.PacketHandler
{
    [PacketLib(184, GameClient.eClientVersion.Version184)]
    public class PacketLib184 : PacketLib183
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Version 1.83 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib184(GameClient client) : base(client)
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
                pak.WriteByte(0);
            }
            else
            {
                string name = string.Format("{0} (Level {1})", quest.Quest.Name, quest.Quest.MinLevel);
                string desc = quest.Quest.Description;
                GamePlayer receiver = m_gameClient.Player;
                name = await AutoTranslateManager.Translate(receiver, name);
                desc = await AutoTranslateManager.Translate(receiver, desc);
                if (name.Length > byte.MaxValue)
                {
                    if (log.IsWarnEnabled) log.Warn("quest name is too long for 1.68+ clients (" + name.Length + ") '" + name + "'");
                    name = name.Substring(0, byte.MaxValue);
                }
                if (desc.Length > byte.MaxValue)
                {
                    if (log.IsWarnEnabled) log.Warn("quest description is too long for 1.68+ clients (" + desc.Length + ") '" + desc + "'");
                    desc = desc.Substring(0, byte.MaxValue);
                }
                pak.WriteByte((byte)name.Length);
                pak.WriteShortLowEndian((ushort)desc.Length);
                pak.WriteByte(0); // Quest Zone ID ?
                pak.WriteByte(0);
                pak.WriteStringBytes(name); //Write Quest Name without trailing 0
                pak.WriteStringBytes(desc); //Write Quest Description without trailing 0
            }

            SendTCP(pak);
        }

        protected override async Task SendTaskInfo()
        {
            string name = await BuildTaskString();
            if (Properties.ENABLE_TASK_SYSTEM)
            {
                // TODO: What?
                name = "use /task to see a list of your active tasks";
            }

            await using GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(GetPacketCode(eServerPackets.QuestEntry));
            pak.WriteByte(0); //index
            pak.WriteShortLowEndian((ushort)name.Length);
            pak.WriteByte((byte)0);
            pak.WriteByte((byte)0);
            pak.WriteByte((byte)0);
            pak.WriteStringBytes(name); //Write Quest Name without trailing 0
            pak.WriteStringBytes(""); //Write Quest Description without trailing 0
            SendTCP(pak);
        }
    }
}