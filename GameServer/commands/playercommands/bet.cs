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
        "Place a bet on an active Arena match during the betting phase.",
        "/bet list - List active teams and the pool",
        "/bet <Team1 or Team2> <gold amount> (Example: /bet 1 500 to bet 500 gold on Team 1)")]
    public class BetCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            var session = ArenaManager.Instance.GetSession(player.CurrentRegionID);

            if (session == null || (session.State != ArenaManager.eArenaState.Running && session.State != ArenaManager.eArenaState.Queuing))
            {
                player.Out.SendMessage("There is no active Arena match in this region.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (args.Length >= 2 && args[1].ToLower() == "list")
            {
                player.Out.SendMessage("\n--- Arena Contest Teams ---", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                if (session.CurrentTeamA != null && session.CurrentTeamB != null)
                {
                    player.Out.SendMessage($"[1] {session.CurrentTeamA.TeamName}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    player.Out.SendMessage($"[2] {session.CurrentTeamB.TeamName}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    long totalPool = session.ActiveBets.Values.Sum(b => b.AmountInCopper) + 1000000L; // Includes 100g house seed
                    player.Out.SendMessage($"Total Betting Pool: {Money.GetString(totalPool)}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    player.Out.SendMessage("Teams are currently preparing...", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                player.Out.SendMessage("---------------------------\n", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (args.Length < 3)
            {
                DisplaySyntax(client);
                return;
            }

            if (!session.IsBettingOpen || session.CurrentTeamA == null || session.CurrentTeamB == null)
            {
                player.Out.SendMessage("Betting is currently closed. You can only place bets during the 30-second countdown before a match starts.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // Prevent participants from betting on their own matches (Match-fixing prevention)
            if (session.CurrentTeamA.Members.Contains(player) || session.CurrentTeamB.Members.Contains(player))
            {
                player.Out.SendMessage("You cannot place bets on a match you are participating in!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (session.ActiveBets.ContainsKey(player.InternalID))
            {
                player.Out.SendMessage("You have already placed a bet for this match.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!int.TryParse(args[1], out int teamChoice) || (teamChoice != 1 && teamChoice != 2))
            {
                player.Out.SendMessage("Invalid team choice. Use 1 for Team 1 or 2 for Team 2.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            
            if (!long.TryParse(args[2], out long goldAmount) || goldAmount <= 0)
            {
                player.Out.SendMessage("Invalid amount. Please specify a positive number of gold.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            long maxBet = Properties.ARENA_MAX_BET_GOLD;
            if (goldAmount > maxBet)
            {
                player.Out.SendMessage($"The maximum allowed bet is {maxBet} gold.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
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
                player.Out.SendMessage("You are currently marked as a Debtor! The bookie refuses your bets until you pay off your bank loans.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (betInCopper > totalWalletAndBank + maxCredit)
            {
                player.Out.SendMessage($"You can only bet up to {Money.GetString(totalWalletAndBank + maxCredit)} (including a {Money.GetString(maxCredit)} credit line).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            lock (session)
            {
                if (!session.IsBettingOpen)
                {
                    player.Out.SendMessage("You were too late! Betting has just closed.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
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
                            player.Out.SendMessage($"WARNING: Your bank account is now empty and you are in debt ({Money.GetString(bank.Debt)}) due to betting on credit! You are now a debtor and have 3 days to reimburse the bank or your assets will be seized and you will be sent to jail!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
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

                player.Out.SendMessage($"You successfully placed a bet of {goldAmount} gold on {selectedTeam.TeamName}!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        public void DisplaySyntax(GameClient client, string[] args = null)
        {
            client.Out.SendMessage("Usage: /bet list OR /bet <1 or 2> <gold amount>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}