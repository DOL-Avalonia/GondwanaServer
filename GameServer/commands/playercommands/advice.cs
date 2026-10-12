using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&advice",
        ePrivLevel.Player,
        "Commands.Players.Advice.Description",
        "Commands.Players.Advice.Description1",
        "Commands.Players.Advice.Usage",
        "Commands.Players.Advice.All",
        "Commands.Players.Advice.Name")]
    public class AdviceCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.IsMuted)
            {
                client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Advice.Muted"), eChatType.CT_Staff, eChatLoc.CL_SystemWindow);
                return;
            }

            if (IsSpammingCommand(client.Player, "advice"))
                return;

            string msg = "";
            if (args.Length >= 2)
            {
                for (int i = 1; i < args.Length; ++i)
                {
                    msg += args[i] + " ";
                }
            }
            else
            {
                int total = 0;
                foreach (GameClient playerClient in WorldMgr.GetAllClients())
                {
                    if (playerClient.Player == null) continue;
                    if (playerClient.Player.Advisor &&
                       ((playerClient.Player.Realm == client.Player.Realm && playerClient.Player.IsAnonymous == false) ||
                       client.Account.PrivLevel > 1))
                    {
                        total++;
                        client.Out.SendMessage(total + ")" + playerClient.Player.Name + (playerClient.Player.IsAnonymous ? " [ANON]" : ""), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }

                }
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Advice.AdvicersOn", total), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            var advisors = WorldMgr.GetAllClients()
                .Where(c => (c is { Account.PrivLevel: > 1 }) || (c is { Player.Advisor: true } && c.Player.Realm == client.Player.Realm))
                .Where(c => c is { Player.ObjectState: GameObject.eObjectState.Active }).Select(c => c.Player)
                .ToList();
            string realm = getRealmString(client.Player.Realm);
            var keyTranslator = new KeyTranslator("Commands.Players.Advice.Advice");
            var msgTranslator = new AutoTranslator(client.Player, msg);
            foreach (var player in advisors)
            {
                GameLoopAsync.Then(msgTranslator.Translate(player),
                    (player, keyTranslator, realm, name: client.Player.Name),
                    static (msg, s) => GameLoopAsync.Message(s.player, s.keyTranslator.Translate(s.player, s.realm, s.name, msg), eChatType.CT_Staff, eChatLoc.CL_ChatWindow));
            }
        }

        public string getRealmString(eRealm Realm)
        {
            switch (Realm)
            {
                case eRealm.Albion: return " ALB";
                case eRealm.Midgard: return " MID";
                case eRealm.Hibernia: return " HIB";
                default: return " NONE";
            }
        }
    }
}