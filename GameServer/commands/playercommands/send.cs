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
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&send",
        new string[] { "&tell", "&t" },
        ePrivLevel.Player,
        "Commands.Players.Send.Description",
        "Commands.Players.Send.Usage")]
    public class SendCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length < 3)
            {
                client.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Send.Usage"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (IsSpammingCommand(client.Player, "send", 500))
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Send.SlowDown"));
                return;
            }

            string targetName = args[1];
            // if target name is You (en) or Vous (fr), replace it by the good one
            if (targetName == LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Send.Target.You"))
                client.TemporallyValues.TryGetValue("LastPlayerToRespond", out targetName);
            string message = string.Join(" ", args, 2, args.Length - 2);

            int result = 0;
            GameClient targetClient = WorldMgr.GuessClientByPlayerNameAndRealm(targetName, 0, false, out result);
            if (targetClient != null && !GameServer.ServerRules.IsAllowedToUnderstand(client.Player, targetClient.Player))
            {
                targetClient = null;
            }

            if (targetClient == null)
            {
                // nothing found
                client.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Send.Target.NotFound",
                        targetName),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // prevent to send an anon GM a message to find him - but send the message to the GM - thx to Sumy
            if (targetClient.Player != null && targetClient.Player.IsAnonymous && targetClient.Account.PrivLevel > (uint)ePrivLevel.Player)
            {
                if (client.Account.PrivLevel == (uint)ePrivLevel.Player)
                {
                    client.Out.SendMessage(
                        LanguageMgr.GetTranslation(
                            client.Account.Language,
                            "Commands.Players.Send.Target.NotFound",
                            targetName),
                        eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    targetClient.Player.Out.SendMessage(
                        LanguageMgr.GetTranslation(
                            client.Account.Language,
                            "Commands.Players.Send.Anon.Received",
                            client.Player.Name, message),
                        eChatType.CT_Send, eChatLoc.CL_ChatWindow);
                }
                else
                {
                    // Let GM's communicate with other anon GM's
                    client.Player.SendPrivateMessage(targetClient.Player, "(anon) " + message);
                }
                return;
            }

            switch (result)
            {
                case 2: // name not unique
                    client.Out.SendMessage(
                        LanguageMgr.GetTranslation(
                            client.Account.Language,
                            "Commands.Players.Send.Target.NotUnique"),
                        eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                case 3: // exact match
                case 4: // guessed name
                    if (targetClient == client)
                    {
                        client.Out.SendMessage(
                            LanguageMgr.GetTranslation(
                                client.Account.Language,
                                "Commands.Players.Send.Target.Yourself"),
                            eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        // Stock the player name to the temporally values of the targeted player in case where he use the shorten twice to match the good player name
                        if (!targetClient.TemporallyValues.ContainsKey("LastPlayerToRespond"))
                            targetClient.TemporallyValues.Add("LastPlayerToRespond", client.Player.Name);
                        else
                            targetClient.TemporallyValues["LastPlayerToRespond"] = client.Player.Name;

                        client.Player.SendPrivateMessage(targetClient.Player, message);
                    }
                    return;
            }
        }
    }
}