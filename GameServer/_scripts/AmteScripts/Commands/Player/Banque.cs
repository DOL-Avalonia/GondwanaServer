using DOL.Database;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using System.Collections.Generic;
using System.Linq;


namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&banque",
        ePrivLevel.Player,
        "Commands.Players.Banque.Description",
        "Commands.Players.Banque.Usage",
        "Commands.Players.Banque.Usage.Cheque",
        "Commands.Players.Banque.Usage.Transfer",
        "Commands.Players.Banque.Usage.TermDeposit")]
    public class BanqueCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length == 1)
            {
                DisplaySyntax(client);
                return;
            }

            try
            {
                Banquier target = client.Player.TargetObject as Banquier;
                if (target != null)
                {
                    DBBanque bank = GameServer.Database.FindObjectByKey<DBBanque>(client.Player.InternalID);
                    string action = args[1].ToLower();

                    if (bank == null)
                    {
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Cheque.NoAccount"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    if (action == "transfer")
                    {
                        if (args.Length < 4)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Transfer.Usage"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        string targetPlayerName = args[2];
                        long transferAmount = GetMoney(args, 3);

                        if (transferAmount <= 0) return;

                        if (bank.Money < transferAmount)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Transfer.NotEnoughFunds"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        DOLCharacters targetChar = GameServer.Database.SelectObject<DOLCharacters>(c => c.Name == targetPlayerName);
                        if (targetChar == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Transfer.PlayerNotFound", targetPlayerName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (targetChar.ObjectId == client.Player.InternalID)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Transfer.CannotTransferSelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        DBBanque targetBank = GameServer.Database.FindObjectByKey<DBBanque>(targetChar.ObjectId);
                        if (targetBank == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Transfer.TargetNoAccount", targetPlayerName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        // Execute Transfer
                        bank.Money -= transferAmount;
                        targetBank.Money += transferAmount;
                        GameServer.Database.SaveObject(bank);
                        GameServer.Database.SaveObject(targetBank);

                        string formattedTransferAmount = Currency.Copper.Mint(transferAmount).ToText();
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Transfer.Success", formattedTransferAmount, targetPlayerName), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        // Notify target if online
                        GameClient targetClient = WorldMgr.GetClientByPlayerName(targetPlayerName, true, false);
                        if (targetClient != null && targetClient.Player != null)
                        {
                            targetClient.Player.Out.SendMessage(LanguageMgr.GetTranslation(targetClient.Account.Language, "Banque.Transfer.Received", formattedTransferAmount, client.Player.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        }
                        return;
                    }

                    else if (action == "termdeposit")
                    {
                        if (args.Length < 4)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.TermDeposit.Usage"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        string termStr = args[2].ToLower();
                        int days = 0;
                        int interestRate = 0;

                        if (termStr == "biweekly" || termStr == "bimensuel") { days = 14; interestRate = 3; }
                        else if (termStr == "monthly" || termStr == "mensuel") { days = 30; interestRate = 5; }
                        else if (termStr == "quarterly" || termStr == "trimestriel") { days = 90; interestRate = 7; }
                        else
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.TermDeposit.InvalidTerm"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        long depositAmount = GetMoney(args, 3);
                        if (depositAmount <= 0) return;

                        if (bank.Money < depositAmount)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.TermDeposit.NotEnoughFunds"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var currentDeposits = GameServer.Database.SelectObjects<DBTermDeposit>(d => d.PlayerID == client.Player.InternalID);
                        if (currentDeposits.Count >= Properties.MAX_TERM_DEPOSITS)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.TermDeposit.MaxReached", Properties.MAX_TERM_DEPOSITS), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        // Execute Deposit
                        bank.Money -= depositAmount;
                        GameServer.Database.SaveObject(bank);

                        DBTermDeposit term = new DBTermDeposit
                        {
                            PlayerID = client.Player.InternalID,
                            Amount = depositAmount,
                            InterestRate = interestRate,
                            MaturityDate = DateTime.Now.AddDays(days)
                        };
                        GameServer.Database.AddObject(term);

                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.TermDeposit.Success", Currency.Copper.Mint(depositAmount).ToText(), days, interestRate), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    else if (action == "chèque" || action == "cheque")
                    {
                        long newMoney = GetMoney(args, 2);
                        if (newMoney > 1000000000)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Cheque.MaxValue"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (!Banquier.TakeMoney(bank, client.Player, newMoney))
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Cheque.NoMoney"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }


                        ItemUnique item = new ItemUnique
                        {
                            Model = 499,
                            Id_nb = "BANQUE_CHEQUE_" + client.Player.Name + "_" + (DateTime.Now.Ticks / 10000).ToString("X8"),
                            Price = newMoney,
                            Weight = 2,
                            Name = LanguageMgr.GetTranslation(client.Account.Language, "Banque.Cheque.Name", client.Player.Name),
                            Description = LanguageMgr.GetTranslation(client.Account.Language, "Banque.Cheque.Description1", Money.GetString(newMoney)) + "\n\n" + LanguageMgr.GetTranslation(client.Account.Language, "Banque.Cheque.Description2")
                        };
                        GameServer.Database.AddObject(item);

                        string message = "";
                        GameInventoryItem inventoryItem = GameInventoryItem.Create(item);
                        if (!client.Player.Inventory.AddTemplate(inventoryItem, 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                        {
                            ItemTemplate dummyVault = CharacterVaultKeeper.GetDummyVaultItem(client.Player);
                            CharacterVault vault = new CharacterVault(client.Player, 0, dummyVault);
                            if (!vault.AddItem(client.Player, inventoryItem, true))
                            {
                                vault = new CharacterVault(client.Player, 1, dummyVault);
                                if (!vault.AddItem(client.Player, inventoryItem, true))
                                {
                                    bank.Money += newMoney;
                                    GameServer.Database.SaveObject(bank);
                                    GameServer.Database.DeleteObject(item);
                                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Cheque.InventoryFull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }
                            }
                            message += LanguageMgr.GetTranslation(client.Account.Language, "Banque.Cheque.MovedToVault") + "\n";
                        }
                        message += LanguageMgr.GetTranslation(client.Account.Language, "Banque.AccountOverview", Money.GetString(bank.Money));
                        client.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        InventoryLogging.LogInventoryAction(client.Player, target, eInventoryActionType.Other, newMoney);
                        InventoryLogging.LogInventoryAction(target, client.Player, eInventoryActionType.Other, item);
                    }
                    else
                    {
                        if (bank == null)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Withdraw.NoAccount"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        long newMoney = GetMoney(args, 1);
                        if (!Banquier.WithdrawMoney(bank, client.Player, newMoney))
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Withdraw.NoMoney"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        string message = LanguageMgr.GetTranslation(client.Account.Language, "Banque.AccountOverview", Money.GetString(bank.Money));
                        client.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        InventoryLogging.LogInventoryAction(target, client.Player, eInventoryActionType.Other, newMoney);
                    }
                }
                else
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Banque.Withdraw.SelectNPC"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                client.Out.SendUpdateMoney();
            }
            catch (Exception)
            {
                DisplaySyntax(client);
            }
        }

        private static long GetMoney(string[] args, int offset)
        {
            int C;
            int S = 0;
            int G = 0;
            int P = 0;
            if (int.TryParse(args[offset], out C))
            {
                if (args.Length > offset + 1 && int.TryParse(args[offset + 1], out S))
                {
                    if (args.Length > offset + 2 && int.TryParse(args[offset + 2], out G))
                    {
                        if (args.Length > offset + 3 && int.TryParse(args[offset + 3], out P))
                            P = Math.Max(0, Math.Min(P, 999));
                        G = Math.Max(0, Math.Min(G, 999));
                    }
                    S = Math.Max(0, Math.Min(S, 99));
                }
                C = Math.Max(0, Math.Min(C, 99));
            }

            return Money.GetMoney(0, P, G, S, C);
        }
    }
}