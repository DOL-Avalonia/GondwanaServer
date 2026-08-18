using AmteScripts.Managers;
using DOL.Database;
using DOL.GS;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using System.Linq;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&bet",
        ePrivLevel.Player,
        "BetCommand.Description",
        "BetCommand.Usage.List",
        "BetCommand.Usage.Bet")]
    public class BetCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            string lang = client.Account.Language;
            var session = ArenaManager.Instance.GetSession(player.CurrentRegionID);

            if (session == null || (session.State != ArenaManager.eArenaState.Running && session.State != ArenaManager.eArenaState.Queuing))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.NoActiveMatch"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (args.Length >= 2 && args[1].ToLower() == "list")
            {
                player.Out.SendMessage("\n" + LanguageMgr.GetTranslation(lang, "BetCommand.List.Header") + "\n", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                if (session.CurrentTeamA != null && session.CurrentTeamB != null)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.List.Team1", session.CurrentTeamA.TeamName) + "\n", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.List.Team2", session.CurrentTeamB.TeamName) + "\n", eChatType.CT_System, eChatLoc.CL_SystemWindow);

                    long totalPool = session.ActiveBets.Values.Sum(b => b.AmountInCopper) + 1000000L;
                    string poolText = Currency.Copper.Mint(totalPool).ToText(lang);

                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.List.TotalPool", poolText) + "\n", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.List.Preparing") + "\n", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.List.Footer") + "\n", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (args.Length < 3)
            {
                DisplaySyntax(client);
                return;
            }

            if (!session.IsBettingOpen || session.CurrentTeamA == null || session.CurrentTeamB == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.Error.Closed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // Prevent participants from betting on their own matches (Match-fixing prevention)
            if (session.CurrentTeamA.Members.Contains(player) || session.CurrentTeamB.Members.Contains(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.Error.Participant"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (session.ActiveBets.ContainsKey(player.InternalID))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.Error.AlreadyBet"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!int.TryParse(args[1], out int teamChoice) || (teamChoice != 1 && teamChoice != 2))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.Error.InvalidTeam"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!long.TryParse(args[2], out long goldAmount) || goldAmount <= 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.Error.InvalidAmount"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            long maxBet = Properties.ARENA_MAX_BET_GOLD;
            if (goldAmount > maxBet)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.Error.MaxBet", maxBet), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return;
            }

            long betInCopper = goldAmount * 10000L;

            DBBanque bank = GameServer.Database.FindObjectByKey<DBBanque>(player.InternalID);
            bool isNewBank = false;
            if (bank == null)
            {
                bank = new DBBanque(player.InternalID);
                GameServer.Database.AddObject(bank); // Create immediately
                isNewBank = true; 
            }

            long totalWalletAndBank = player.CopperBalance + bank.Money;
            long maxCredit = 5000 * 10000L; // 5000 gold maximum allowed negative balance (credit line)

            if (bank.IsDebtor && bank.Debt > 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.Debtor"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (betInCopper > totalWalletAndBank + maxCredit)
            {
                string limitText = Currency.Copper.Mint(totalWalletAndBank + maxCredit).ToText(lang);
                string creditText = Currency.Copper.Mint(maxCredit).ToText(lang);
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.NotEnoughMoney", limitText, creditText), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            lock (session)
            {
                if (!session.IsBettingOpen)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.TooLate"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                // Deduct money - empty inventory first, then hit bank
                long remaining = betInCopper;
                long invMoney = player.CopperBalance;

                if (invMoney >= remaining)
                {
                    player.RemoveMoney(Currency.Copper.Mint(remaining));
                }
                else
                {
                    if (invMoney > 0)
                    {
                        player.RemoveMoney(Currency.Copper.Mint(invMoney));
                        remaining -= invMoney;
                    }

                    if (bank.Money >= remaining)
                    {
                        bank.Money -= remaining;
                    }
                    else
                    {
                        if (bank.Money > 0)
                        {
                            remaining -= bank.Money;
                            bank.Money = 0;
                        }

                        bank.Debt += remaining;

                        if (bank.Debt > 0 && !bank.IsDebtor)
                        {
                            bank.IsDebtor = true;
                            bank.NegativeMoneySince = DateTime.Now;
                            string debtText = Currency.Copper.Mint(bank.Debt).ToText(lang);
                            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.BankEmptyDebt", debtText), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        }
                    }
                }

                if (!isNewBank) GameServer.Database.SaveObject(bank);

                ArenaManager.ArenaTeam selectedTeam = teamChoice == 1 ? session.CurrentTeamA : session.CurrentTeamB;

                session.ActiveBets[player.InternalID] = new ArenaManager.ArenaBet
                {
                    PlayerID = player.InternalID,
                    AmountInCopper = betInCopper,
                    TeamChoice = teamChoice
                };

                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "BetCommand.Success", goldAmount, selectedTeam.TeamName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        public void DisplaySyntax(GameClient client, string[] args = null)
        {
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "BetCommand.Usage"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}