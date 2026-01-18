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

                Task.Run(async () =>
                {
                    string toSend = await translator.Translate(client.Player);
                    string message = "[Guild] " + client.Player.Name + ": \"" + toSend + "\"";

                    ply.Out.SendMessage(message, eChatType.CT_Guild, eChatLoc.CL_ChatWindow);
                });
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

                Task.Run(async () =>
                {
                    string toSend = await translator.Translate(client.Player);
                    string message = "[Officers] " + client.Player.Name + ": \"" + toSend + "\"";
                    
                    ply.Out.SendMessage(message, eChatType.CT_Officer, eChatLoc.CL_ChatWindow);
                });
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

                    Task.Run(async () =>
                    {
                        string toSend = await translator.Translate(client.Player);
                        string message = "[Alliance] " + client.Player.Name + ": \"" + toSend + "\"";

                        ply.Out.SendMessage(message, eChatType.CT_Alliance, eChatLoc.CL_ChatWindow);
                    });
                }
            }
        }
    }
}