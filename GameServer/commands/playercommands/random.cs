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
/***[ random.cs ]****
* reqired DOL:	1.5.0
* author|date:	SEpHirOTH |	2004/02/25
* modificatio:  SmallHorse (just a little cleanup)
* modificatio:  noret (made it look like on live servers)
* description: enables the usage of "/random" command
*		"/random <n>" get a random number between 1 and <n>
*		results will be send to all players in emote range
******************/

using System;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&random",
        ePrivLevel.Player,
        "Commands.Players.Random.Description",
        "Commands.Players.Random.Usage")]
    public class RandomCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        // declaring some msg's
        private const int RESULT_RANGE = 512; // emote range
        private const string MESSAGE_HELP = "You must select a maximum number for your random selection!";
        private const string MESSAGE_RESULT_SELF = "You pick a random number between 1 and {0}: {1}"; // thrownMax, thrown
        private const string MESSAGE_RESULT_OTHER = "{0} picks a random number between 1 and {1}: {2}"; // client.Player.Name, thrownMax, thrown
        private const string MESSAGE_LOW_NUMBER = "You must select a maximum number greater than 1!";

        public void OnCommand(GameClient client, string[] args)
        {
            if (IsSpammingCommand(client.Player, "random", 500))
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Random.SlowDown"));
                return;
            }

            // no args - display usage
            if (args.Length < 2)
            {
                SystemMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Random.Help"));
                return;
            }

            int thrownMax;

            // trying to convert number
            try
            {
                thrownMax = System.Convert.ToInt32(args[1]);
            }
            catch (OverflowException)
            {
                thrownMax = int.MaxValue - 1; // max+1 is used in GameObject.Random(int,int)
            }
            catch (Exception)
            {
                SystemMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Random.Help"));
                return;
            }

            if (thrownMax < 2)
            {
                SystemMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Random.LowNumber"));
                return;
            }

            // throw result
            int thrown = Util.Random(1, thrownMax);

            // building result messages
            string selfMessage = LanguageMgr.GetTranslation(
                                    client.Account.Language,
                                    "Commands.Players.Random.Result.Self",
                                    thrownMax, thrown);
            string otherMessage = LanguageMgr.GetTranslation(
                                    client.Account.Language,
                                    "Commands.Players.Random.Result.Other",
                                    client.Player.Name, thrownMax, thrown);

            // sending msg to player
            EmoteMessage(client, selfMessage);

            // sending result & playername to all players in range
            foreach (GamePlayer player in client.Player.GetPlayersInRadius(RESULT_RANGE))
            {
                if (client.Player != player) // client gets unique message
                    EmoteMessage(player, otherMessage); // sending msg to other players
            }
        }

        // these are to make code look better
        private void SystemMessage(GameClient client, string str)
        {
            client.Out.SendMessage(str, eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        private void EmoteMessage(GamePlayer player, string str)
        {
            EmoteMessage(player.Client, str);
        }

        private void EmoteMessage(GameClient client, string str)
        {
            client.Out.SendMessage(str, eChatType.CT_Emote, eChatLoc.CL_SystemWindow);
        }
    }
}