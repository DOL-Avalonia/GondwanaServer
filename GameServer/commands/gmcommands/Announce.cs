/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
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
            var targets = WorldMgr.GetAllPlayingClients();

            Task.Run(async () =>
            {
                var players = targets.Select(c => c.Player);
                var messages = new Dictionary<GamePlayer, string>(await AutoTranslateManager.Translate(sender, players, message));
                var keys = new Dictionary<GamePlayer, string>(await LanguageMgr.Translate(players, translationId));
                foreach (var p in players)
                {
                    var msg = messages.GetValueOrDefault(p, message);
                    keys.TryGetValue(p, out var key);
                    handler(p, key, msg);
                }
            });
        }
    }
}
