using System.Linq;
using System.Reflection;
using DOL.Language;
using log4net;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.BonusesListRequest, "Handles player bonuses button clicks", eClientStatus.PlayerInGame)]
    public class PlayerBonusesListRequestHandler : PacketHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            GamePlayer player = client.Player;
            if (player == null)
                return;

            int code = packet.ReadByte();
            if (code != 0 && log.IsWarnEnabled)
                log.Warn($"bonuses button: code is other than zero ({code})");

            player.Out.SendCustomTextWindow(LanguageMgr.GetTranslation(client.Account.Language, "PlayerBonusesListRequestHandler.HandlePacket.Bonuses"), player.GetBonuses().ToList());
        }
    }
}