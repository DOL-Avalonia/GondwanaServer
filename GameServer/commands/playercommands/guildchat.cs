using DOL.GS.PacketHandler;
using DOL.Language;
using System.Linq;
using System.Threading.Tasks;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&gu",
        new string[] { "&guild" },
        ePrivLevel.Player,
        "Commands.Players.Guildchat.Description",
        "Commands.Players.Guildchat.Usage")]
    public class GuildChatCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.Guild == null)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Guildchat.NotGuilded"));
                return;
            }

            if (!client.Player.Guild.HasRank(client.Player, Guild.eRank.GcSpeak))
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Guildchat.NoPermission"));
                return;
            }

            if (IsSpammingCommand(client.Player, "guildchat", 500))
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Guildchat.SlowDown"));
                return;
            }

            if (args.Length < 2)
                return;

            string rawText = string.Join(" ", args, 1, args.Length - 1);
            var translator = new AutoTranslator(client.Player, rawText);
            foreach (GamePlayer ply in client.Player.Guild.GetListOfOnlineMembers())
            {
                if (ply == null)
                    continue;

                GameLoopAsync.Then(translator.Translate(ply), (ply, name: client.Player.Name), static (text, s) =>
                { if (s.ply.ObjectState == GameObject.eObjectState.Active) s.ply.Out.SendMessage($"[Guild] {s.name}: \"{text}\"", eChatType.CT_Guild, eChatLoc.CL_ChatWindow); });
            }
        }
    }

    [CmdAttribute(
        "&o",
        new string[] { "&osend" },
        ePrivLevel.Player,
        "Commands.Players.Osend.Description",
        "Commands.Players.Osend.Usage")]
    public class OfficerGuildChatCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.Guild == null)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Osend.NotGuilded"));
                return;
            }

            if (!client.Player.Guild.HasRank(client.Player, Guild.eRank.OcSpeak))
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Osend.NoPermission"
                    )
                );
                return;
            }

            if (IsSpammingCommand(client.Player, "osend", 500))
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Osend.SlowDown"));
                return;
            }

            if (args.Length < 2)
                return;

            string rawText = string.Join(" ", args, 1, args.Length - 1);
            var translator = new AutoTranslator(client.Player, rawText);
            foreach (GamePlayer ply in client.Player.Guild.GetListOfOnlineMembers())
            {
                if (!client.Player.Guild.HasRank(ply, Guild.eRank.OcHear))
                    continue;

                GameLoopAsync.Then(translator.Translate(ply), (ply, name: client.Player.Name), static (text, s) =>
                { if (s.ply.ObjectState == GameObject.eObjectState.Active) s.ply.Out.SendMessage($"[Officers] {s.name}: \"{text}\"", eChatType.CT_Guild, eChatLoc.CL_ChatWindow); });
            }
        }
    }

    [CmdAttribute(
        "&as",
        new string[] { "&asend" },
        ePrivLevel.Player,
        "Commands.Players.Asend.Description",
        "Commands.Players.Asend.Usage")]
    public class AllianceGuildChatCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.Guild == null)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Asend.NotGuilded"));
                return;
            }

            if (client.Player.Guild.alliance == null)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Asend.NoAlliance"));
                return;
            }

            if (!client.Player.Guild.HasRank(client.Player, Guild.eRank.AcSpeak))
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Asend.NoPermission"));
                return;
            }

            if (client.Player.IsMuted)
            {
                client.Player.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Asend.Muted"),
                    eChatType.CT_Staff, eChatLoc.CL_SystemWindow);
                return;
            }

            if (args.Length < 2)
                return;

            string rawText = string.Join(" ", args, 1, args.Length - 1);
            var translator = new AutoTranslator(client.Player, rawText);
            foreach (Guild gui in client.Player.Guild.alliance.Guilds)
            {
                foreach (GamePlayer ply in gui.GetListOfOnlineMembers())
                {
                    if (!gui.HasRank(ply, Guild.eRank.AcHear))
                        continue;

                    GameLoopAsync.Then(translator.Translate(ply), (ply, name: client.Player.Name), static (text, s) =>
                    { if (s.ply.ObjectState == GameObject.eObjectState.Active) s.ply.Out.SendMessage($"[Alliance] {s.name}: \"{text}\"", eChatType.CT_Guild, eChatLoc.CL_ChatWindow); });
                }
            }
        }
    }
}