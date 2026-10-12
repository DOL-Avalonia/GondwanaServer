using DOL.GS.Commands;
using DOL.GS.Keeps;
using log4net;
using System;
using System.Reflection;

namespace DOL.GS.PacketHandler
{
    [PacketLib(1127, GameClient.eClientVersion.Version1127)]
    public class PacketLib1127 : PacketLib1126
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Client Version 1.127
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib1127(GameClient client)
            : base(client)
        {
        }

        /// 1127 login granted packet unchanged, work around for server type
        public override void SendLoginGranted(byte color)
        {
            // work around for character screen bugs when server type sent as 00 but player doesnt have a realm
            // 0x07 allows for characters in all realms
            using (GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(GetPacketCode(eServerPackets.LoginGranted)))
            {
                pak.WritePascalString(m_gameClient.Account.Name);
                pak.WritePascalString(GameServer.Instance.Configuration.ServerNameShort); //server name
                pak.WriteByte(0x05); //Server ID, seems irrelevant
                var type = color == 0 ? 7 : color;
                pak.WriteByte((byte)type); // 00 normal type?, 01 mordred type, 03 gaheris type, 07 ywain type
                pak.WriteByte(0x00); // Trial switch 0x00 - subbed, 0x01 - trial acc
                SendTCP(pak);
            }
        }

        public override void SendMessage(string msg, eChatType type, eChatLoc loc)
        {
            SnoopManager.CheckAndBroadcast(m_gameClient.Player, msg, type, loc);
            if (m_gameClient.IsChatTypeDisabled(type)) return;
            SendRawMessage(msg, type, loc);
        }

        public override void SendRawMessage(string msg, eChatType type, eChatLoc loc)
        {
            if (m_gameClient.ClientState is GameClient.eClientState.CharScreen)
                return;

            msg ??= string.Empty;
            using (GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(GetPacketCode(eServerPackets.Message)))
            {
                pak.WriteByte((byte)type);
                if (type is eChatType.CT_Send)
                {
                    pak.WriteNonNullTerminatedString("@@");
                }
                else if (loc is eChatLoc.CL_PopupWindow)
                {
                    pak.WriteNonNullTerminatedString("##");
                }
                pak.WriteString(msg);
                SendTCP(pak);
            }
        }

        public override void SendHookPointStore(GameKeepHookPoint hookPoint)
        {
            using (GSTCPPacketOut pak = PooledObjectFactory.GetForTick<GSTCPPacketOut>().Init(GetPacketCode(eServerPackets.KeepComponentHookpointStore)))
            {
                HookPointInventory inventory;
                if (hookPoint.ID > 0x80) inventory = HookPointInventory.YellowHPInventory;
                else if (hookPoint.ID > 0x60) inventory = HookPointInventory.GreenHPInventory;
                else if (hookPoint.ID > 0x40) inventory = HookPointInventory.LightGreenHPInventory;
                else if (hookPoint.ID > 0x20) inventory = HookPointInventory.BlueHPInventory;
                else inventory = HookPointInventory.RedHPInventory;

                var items = inventory.GetAllItems();
                int count = Math.Min(items.Count, 30);

                pak.WriteByte((byte)count);
                pak.WriteByte(0);
                pak.WriteByte(1);
                pak.WriteByte(1);
                pak.WriteByte(1);
                pak.WriteShortLowEndian((ushort)hookPoint.Component.Keep.KeepID);
                pak.WriteShortLowEndian((ushort)hookPoint.Component.ID);
                pak.WriteShortLowEndian((ushort)hookPoint.ID);

                int i = 0;
                foreach (HookPointItem item in items)
                {
                    if (i >= count) break;
                    pak.WriteByte((byte)i);
                    pak.WriteByte((byte)(item.Flag >> 8));
                    pak.WriteByte((byte)(item.Flag & 0xFF));
                    pak.WriteByte(0); pak.WriteByte(0); pak.WriteByte(0); pak.WriteByte(0);
                    pak.WriteShortLowEndian(0);
                    pak.WriteIntLowEndian((uint)item.Gold);
                    pak.WriteShortLowEndian(item.Icon);
                    pak.WritePascalStringIntLE(item.Name);
                    i++;
                }

                SendTCP(pak);
            }
        }
    }
}