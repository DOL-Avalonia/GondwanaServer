using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&announce",
        ePrivLevel.GM,
        "Commands.GM.Announce.Description",
        "Commands.GM.Announce.Usage")]
    public class AnnounceCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length < 3)
            {
                DisplaySyntax(client);
                return;
            }

            string message = string.Join(" ", args, 2, args.Length - 2);
            if (message == "")
                return;
            
            var senderName = client.Player.Name;
            switch (args.GetValue(1)!.ToString()!.ToLower())
            {
                #region Log
                case "log":
                    {
                        Announce(client.Player, "Commands.GM.Announce.LogAnnounce", message, (p, key, msg) =>
                        {
                            var str = string.Format(key, msg);
                            p.Out.SendMessage(str, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        });
                        break;
                    }
                #endregion Log
                #region Window
                case "window":
                    {
                        Announce(client.Player, "Commands.GM.Announce.WindowAnnounce", message, (p, key, msg) =>
                        {
                            var str = string.Format(key, senderName);
                            p.Out.SendCustomTextWindow(str, [msg]);
                        });
                        break;
                    }
                #endregion Window
                #region Send
                case "send":
                    {
                        Announce(client.Player, "Commands.GM.Announce.SendAnnounce", message, (p, key, msg) =>
                        {
                            var str = string.Format(key, msg);
                            p.Out.SendMessage(str, eChatType.CT_Send, eChatLoc.CL_ChatWindow);
                        });
                        break;
                    }
                #endregion Send
                #region Center
                case "center":
                    {
                        Announce(client.Player, "Commands.GM.Announce.SendAnnounce", message, (p, key, msg) =>
                        {
                            var str = string.Format(key, msg);
                            p.Out.SendMessage(str, eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                        });
                        break;
                    }
                #endregion Center
                #region Confirm
                case "confirm":
                    {
                        Announce(client.Player, "Commands.GM.Announce.ConfirmAnnounce", message, (p, key, msg) =>
                        {
                            var str = string.Format(key, senderName, msg);
                            p.Out.SendDialogBox(eDialogCode.SimpleWarning, 0, 0, 0, 0, eDialogType.Ok, true, str);
                        });
                        break;
                    }
                #endregion Confirm
                #region Default
                default:
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    #endregion Default
            }
        }

        private void Announce(GamePlayer sender, string translationId, string message, Action<GamePlayer, string, string> handler)
        {
            var translator = new KeyTranslator(translationId);
            var msgTranslator = new AutoTranslator(sender, message);
            foreach (var c in WorldMgr.GetAllPlayingClients())
            {
                var player = c.Player;
                if (player == null)
                    continue;

                GameLoopAsync.Then(Task.WhenAll(translator.Translate(player), msgTranslator.Translate(player)), (player, handler), static (r, s) =>
                {
                    if (s.player.ObjectState == GameObject.eObjectState.Active) s.handler(s.player, r[0], r[1]);
                });
            }
        }
    }
}
