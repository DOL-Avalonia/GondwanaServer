using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;


namespace DOL.GS.Commands
{
    [CmdAttribute(
         "&trade",
         ePrivLevel.Player,
         "Commands.Players.Trade.Description",
         "Commands.Players.Trade.Usage")]
    public class TradeChannelCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            const string BROAD_TICK = "Broad_Tick";

            if (args.Length < 2)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Broadcast.NoText"));
                return;
            }
            if (client.Player.IsMuted)
            {
                client.Player.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Broadcast.Muted"),
                    eChatType.CT_Staff, eChatLoc.CL_SystemWindow);
                return;
            }
            string message = string.Join(" ", args, 1, args.Length - 1);

            long BroadTick = client.Player.TempProperties.getProperty<long>(BROAD_TICK);
            if (BroadTick > 0 && BroadTick - client.Player.CurrentRegion.Time <= 0)
            {
                client.Player.TempProperties.removeProperty(BROAD_TICK);
            }
            long changeTime = client.Player.CurrentRegion.Time - BroadTick;
            if (changeTime < 800 && BroadTick > 0)
            {
                client.Player.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Broadcast.SlowDown"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Player.TempProperties.setProperty(BROAD_TICK, client.Player.CurrentRegion.Time);
                return;
            }
            Broadcast(client.Player, message);

            client.Player.TempProperties.setProperty(BROAD_TICK, client.Player.CurrentRegion.Time);
        }

        private void Broadcast(GamePlayer player, string message)
        {
            KeyTranslator keyTranslator = new("Commands.Players.Trade.Message");
            AutoTranslator messageTranslator = new(player, message);
            foreach (GameClient c in WorldMgr.GetClientsOfRegion(player.CurrentRegionID))
            {
                if (GameServer.ServerRules.IsAllowedToUnderstand(c.Player, player))
                {
                    GamePlayer p = c.Player;
                    if (p == null || !GameServer.ServerRules.IsAllowedToUnderstand(p, player)) continue;
                    GameLoopAsync.Then(Task.WhenAll(keyTranslator.Translate(p), messageTranslator.Translate(p)), (p, sender: player), static (r, s) =>
                    {
                        if (s.p.ObjectState == GameObject.eObjectState.Active)
                            s.p.Out.SendMessage(string.Format(r[0], s.p.GetPersonalizedName(s.sender), r[1]), eChatType.CT_Trade, eChatLoc.CL_ChatWindow);
                    });
                }
            }
        }
    }
}


