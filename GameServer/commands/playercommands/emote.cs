using DOL.GS.PacketHandler;
using DOL.Language;
using System.Linq;
using System.Threading.Tasks;

namespace DOL.GS.Commands
{
    /// <summary>
    /// Command handler to handle emotes
    /// </summary>
    [CmdAttribute(
        "&emote", new string[] { "&em", "&e" },
        ePrivLevel.Player,
        "Commands.Players.Emote.Description",
        "Commands.Players.Emote.Usage")]
    public class CustomEmoteCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        /// <summary>
        /// Method to handle the command from the client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public void OnCommand(GameClient client, string[] args)
        {
            if (IsSpammingCommand(client.Player, "emote"))
                return;

            // no emotes if dead
            if (!client.Player.IsAlive)
            {
                client.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        client.Account.Language, "Commands.Players.Emote.Dead"),
                    eChatType.CT_Emote,
                    eChatLoc.CL_SystemWindow);
                return;
            }

            if (args.Length < 2)
            {
                client.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Emote.Empty"
                    ),
                    eChatType.CT_System,
                    eChatLoc.CL_SystemWindow
                );
                return;
            }

            if (client.Player.IsMuted)
            {
                client.Player.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Emote.Empty"
                    ),
                    eChatType.CT_Staff,
                    eChatLoc.CL_SystemWindow
                );
                return;
            }

            string emoteStrRaw = string.Join(" ", args, 1, args.Length - 1);
            var players = client.Player.GetPlayersInRadius(WorldMgr.SAY_DISTANCE).Cast<GamePlayer>().Where(p => !p.IsIgnoring(client.Player)).ToList();
            var keyTranslator = new KeyTranslator("Commands.Players.Emote.Act");
            var strangeTranslator = new KeyTranslator("Commands.Players.Emote.Strange");
            var msgTranslator = new AutoTranslator(client.Player, emoteStrRaw);
            foreach (var player in players)
            {
                if (GameServer.ServerRules.IsAllowedToUnderstand(client.Player, player))
                    GameLoopAsync.Then(msgTranslator.Translate(player), (player, keyTranslator, name: client.Player.Name), static (msg, s) =>
                        GameLoopAsync.Message(s.player, s.keyTranslator.Translate(s.player, s.name, msg), eChatType.CT_Emote, eChatLoc.CL_ChatWindow));
                else
                    GameLoopAsync.Message(player, strangeTranslator.Translate(player, client.Player.Name), eChatType.CT_Emote, eChatLoc.CL_ChatWindow);
            }
        }
    }
}