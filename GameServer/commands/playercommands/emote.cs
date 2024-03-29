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

            string EmoteStr = string.Join(" ", args, 1, args.Length - 1);
            string ownRealm = LanguageMgr.GetTranslation(
                client.Account.Language,
                "Commands.Players.Emote.Act",
                client.Player.Name,
                EmoteStr
            );

            string diffRealm = LanguageMgr.GetTranslation(
                client.Account.Language,
                "Commands.Players.Emote.Strange",
                client.Player.Name
            );

            foreach (GamePlayer player in client.Player.GetPlayersInRadius(WorldMgr.SAY_DISTANCE))
            {
                if (GameServer.ServerRules.IsAllowedToUnderstand(client.Player, player))
                {
                    player.Out.SendMessage(ownRealm, eChatType.CT_Emote, eChatLoc.CL_ChatWindow);
                }
                else
                {
                    if (!player.IsIgnoring(client.Player as GameLiving))
                        player.Out.SendMessage(diffRealm, eChatType.CT_Emote, eChatLoc.CL_ChatWindow);
                }
            }
        }
    }
}