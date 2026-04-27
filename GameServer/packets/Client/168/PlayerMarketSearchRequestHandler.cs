using System;
using System.Reflection;
using DOL.Database;
using DOL.GS.Housing;
using log4net;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.MarketSearchRequest, "Handles player market search", eClientStatus.PlayerInGame)]
    public class PlayerMarketSearchRequestHandler : IPacketHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            if (client == null || client.Player == null)
                return;

            if ((client.Player.TargetObject is IGameInventoryObject) == false)
                return;

            var searchOffset = packet.ReadByte();
            packet.Skip(3); // 3 bytes unused

            MarketSearch.SearchData search = new MarketSearch.SearchData();

            search.name = packet.ReadString(searchOffset);

            if (search.name.Equals("|"))
            {
                search.name = string.Empty;
            }

            search.realm = (eRealm)client.Player.Realm;
            search.slot = (int)packet.ReadByte();

            // Bonus 1
            var bonus1 = packet.ReadByte();
            var bonus1b = packet.ReadByte();
            search.bonus1 = bonus1b * 256 + bonus1;

            var bonus1Value = (int)packet.ReadByte();
            var bonus1bValue = (int)packet.ReadByte();
            search.bonus1Value = bonus1bValue * 256 + bonus1Value;

            // Bonus 2
            var bonus2 = packet.ReadByte();
            var bonus2b = packet.ReadByte();
            search.bonus2 = bonus2b * 256 + bonus2;

            var bonus2Value = (int)packet.ReadByte();
            var bonus2bValue = (int)packet.ReadByte();
            search.bonus2Value = bonus2bValue * 256 + bonus2Value;

            // Bonus 3
            var bonus3 = packet.ReadByte();
            var bonus3b = packet.ReadByte();
            search.bonus3 = bonus3b * 256 + bonus3;

            var bonus3Value = (int)packet.ReadByte();
            var bonus3bValue = (int)packet.ReadByte();
            search.bonus3Value = bonus3bValue * 256 + bonus3Value;

            search.proc = (int)packet.ReadByte();
            packet.Skip(1);

            search.armorType = (byte)packet.ReadByte();
            search.damageType = (byte)packet.ReadByte(); // 1=crush, 2=slash, 3=thrust
            search.levelMin = (byte)packet.ReadByte();
            search.levelMax = (byte)packet.ReadByte();
            search.minQual = (byte)packet.ReadByte();

            search.priceMin = packet.ReadIntLowEndian();
            search.priceMax = packet.ReadIntLowEndian();

            search.playerCrafted = (byte)packet.ReadByte(); // 1 = show only Player crafted, 0 = all
            search.visual = (int)packet.ReadByte();
            search.page = (byte)packet.ReadByte();

            search.clientVersion = client.Version.ToString();

            if (ServerProperties.Properties.MARKET_ENABLE_LOG && log.IsDebugEnabled)
            {
                log.Debug("----- MARKET EXPLORER SEARCH PACKET ANALYSIS ---------------------");
                log.DebugFormat("name          : {0}", search.name);
                log.DebugFormat("slot          : {0}", search.slot);
                log.DebugFormat("armorType     : {0}", search.armorType);
                log.DebugFormat("damageType    : {0}", search.damageType);
                log.DebugFormat("levelMin      : {0}", search.levelMin);
                log.DebugFormat("levelMax      : {0}", search.levelMax);
                log.DebugFormat("minQual       : {0}", search.minQual);
                log.DebugFormat("priceMin      : {0}", search.priceMin);
                log.DebugFormat("priceMax      : {0}", search.priceMax);
                log.DebugFormat("playerCrafted : {0}", search.playerCrafted);
                log.DebugFormat("visual        : {0}", search.visual);
                log.DebugFormat("page          : {0}", search.page);
                log.Debug("------------------------------------------------------------------");
            }

            (client.Player.TargetObject as IGameInventoryObject)!.SearchInventory(client.Player, search);
        }
    }
}