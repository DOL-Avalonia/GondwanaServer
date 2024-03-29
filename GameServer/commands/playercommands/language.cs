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

using System;
using System.Linq;

using DOL.Database;
using DOL.Language;

namespace DOL.GS.Commands
{
    [Cmd(
        "&language",
        ePrivLevel.Player,
        "Commands.Players.Language.Description",
        "Commands.Players.Language.Usage.Current",
        "Commands.Players.Language.Usage.Set",
        "Commands.Players.Language.Usage.Show"
    )]
    public class LanguageCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (IsSpammingCommand(client.Player, "language"))
                return;

            if (client.Account.PrivLevel == (uint)ePrivLevel.Player && !DOL.GS.ServerProperties.Properties.ALLOW_CHANGE_LANGUAGE)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Language.NoChange"));
                return;
            }

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }

            switch (args[1].ToLower())
            {
                #region current
                case "current":
                    {
                        DisplayMessage(
                            client,
                            LanguageMgr.GetTranslation(
                                client.Account.Language,
                                "Commands.Players.Language.Current"),
                            client.Account.Language);
                        return;
                    }
                #endregion current

                #region set
                case "set":
                    {
                        if (args.Length < 3)
                        {
                            DisplaySyntax(client, "set");
                            return;
                        }

                        if (!LanguageMgr.Languages.Contains(args[2].ToUpper()))
                        {
                            DisplayMessage(
                                client,
                                LanguageMgr.GetTranslation(
                                    client.Account.Language,
                                    "Commands.Players.Language.LanguageNotSupported",
                                    args[2].ToUpper()));
                            return;
                        }

                        client.Account.Language = args[2];
                        GameServer.Database.SaveObject(client.Account);
                        DisplayMessage(
                            client,
                            LanguageMgr.GetTranslation(
                                client.Account.Language,
                                "Commands.Players.Language.Set",
                                args[2].ToUpper()));
                        client.Out.SendUpdatePlayer();
                        client.Out.SendUpdateCraftingSkills();
                        client.Out.SendUpdateWeaponAndArmorStats();
                        client.Out.SendUpdatePlayerSkills();
                        client.Out.SendCharResistsUpdate();
                        client.Out.SendCharStatsUpdate();
                        client.Out.SendPlayerTitles();
                        client.Out.SendQuestListUpdate();
                        return;
                    }
                #endregion set

                #region show
                case "show":
                    {
                        string languages = "";
                        foreach (string language in LanguageMgr.Languages)
                        {
                            if (client.Account.Language == language)
                                languages += ("*" + language + ","); // The * marks a language as the players current used language
                            else
                                languages += (language + ",");
                        }

                        if (languages.EndsWith(","))
                            languages = languages.Substring(0, languages.Length - 1);

                        DisplayMessage(
                            client,
                            LanguageMgr.GetTranslation(
                                client.Account.Language,
                                "Commands.Players.Language.AvailableLanguages",
                                languages));
                        return;
                    }
                #endregion show

                default:
                    {
                        DisplaySyntax(client);
                        return;
                    }
            }
        }
    }
}