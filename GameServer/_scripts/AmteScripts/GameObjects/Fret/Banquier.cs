using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;

namespace DOL.GS.Scripts
{
    /// <summary>
    /// Summary description for Banquier.
    /// </summary>
    public class Banquier : GameNPC
    {
        public Banquier()
        {
            SetOwnBrain(new BlankBrain());
            GuildName = "Banquier";
        }

        public override bool ReceiveMoney(GameLiving source, long money)
        {
            return ReceiveMoney(source, money, true);
        }

        public bool ReceiveMoney(GameLiving source, long money, bool removeMoney)
        {
            if (source is not GamePlayer player)
                return false;
            if (money <= 0) return false;
            DBBanque bank = GameServer.Database.FindObjectByKey<DBBanque>(player.InternalID);
            if (bank == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.AccountCreate"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                bank = new DBBanque(player.InternalID);
                GameServer.Database.AddObject(bank);
            }

            if (removeMoney)
            {
                if (!player.RemoveMoney(Currency.Copper.Mint(money)))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.DontHaveAmount"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                InventoryLogging.LogInventoryAction(source, this, eInventoryActionType.Other, money);
            }
            bank.Money = money + bank.Money;

            GameServer.Database.SaveObject(bank);

            string message = LanguageMgr.TranslateMoneyLong(player, bank.Money);

            if (!string.IsNullOrEmpty(message))
                message += LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.Moneyamount", message);
            else
                message = LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.Nomoney");
            player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (!(source is GamePlayer)) return false;
            GamePlayer player = source as GamePlayer;

            if (player != null && player.Reputation < 0)
            {
                TurnTo(player, 5000);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.InteractOutlaw"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (!item.Id_nb.StartsWith("BANQUE_CHEQUE")) return false;

            if (player!.Inventory.RemoveCountFromStack(item, item.Count))
            {
                ReceiveMoney(player, item.Price, false);
                InventoryLogging.LogInventoryAction(source, this, eInventoryActionType.Other, item, item.Count);
                InventoryLogging.LogInventoryAction(this, source, eInventoryActionType.Other, item.Price);
            }
            return true;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            if (player.Reputation < 0)
            {
                TurnTo(player, 5000);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.InteractOutlaw"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            DBBanque bank = GameServer.Database.FindObjectByKey<DBBanque>(player.InternalID);
            if (bank == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.InteractText01") + "\r\n" + LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.InteractText02"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            ProcessMaturedDeposits(player, bank);

            string autoRentToggle = bank.AutoPayRent
                ? LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.AutoRent.On")
                : LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.AutoRent.Off");

            string formattedMoney = Currency.Copper.Mint(bank.Money).ToText();
            string message = LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.Greetings1", player.Name, Money.GetString(bank.Money)) + " " + "\r\n";
            message += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Greetings2") + "\n\n";
            message += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Greetings3") + "\n";
            message += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Greetings4") + "\n";
            message += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Greetings5") + "\n";
            message += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Greetings6") + "\n";
            message += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Greetings7") + "\n\n";
            message += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Greetings8", autoRentToggle);
            player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_PopupWindow);
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str)) return false;
            GamePlayer player = source as GamePlayer;
            if (player == null)
                return true;

            if (player.Reputation < 0)
            {
                TurnTo(player, 5000);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.InteractOutlaw"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }

            DBBanque bank = GameServer.Database.FindObjectByKey<DBBanque>(player.InternalID);
            if (bank == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.NoAccountYet1") + "\r\n" + LanguageMgr.GetTranslation(player.Client.Account.Language,"Banker.NoAccountYet2"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            string lowerStr = str.ToLower();

            // Toggle Auto Rent
            if (lowerStr == "automatic house rental payment (on)" || lowerStr == "automatic house rental payment (off)" ||
                lowerStr == "paiement automatique du loyer (activé)" || lowerStr == "paiement automatique du loyer (désactivé)")
            {
                player.TempProperties.setProperty("Banker_PromptingAutoRent", true);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.AutoRent.Prompt"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            // Capture YES or OUI
            if ((lowerStr == "yes" || lowerStr == "oui") && player.TempProperties.getProperty<bool>("Banker_PromptingAutoRent", false))
            {
                player.TempProperties.removeProperty("Banker_PromptingAutoRent");
                bank.AutoPayRent = true;
                GameServer.Database.SaveObject(bank);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.AutoRent.SetOn"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Interact(player);
                return true;
            }

            // Capture NO or NON
            if ((lowerStr == "no" || lowerStr == "non") && player.TempProperties.getProperty<bool>("Banker_PromptingAutoRent", false))
            {
                player.TempProperties.removeProperty("Banker_PromptingAutoRent");
                bank.AutoPayRent = false;
                GameServer.Database.SaveObject(bank);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.AutoRent.SetOff"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Interact(player);
                return true;
            }

            player.TempProperties.removeProperty("Banker_PromptingAutoRent");

            switch (lowerStr)
            {
                case "retirer de l'argent":
                case "withdraw money":
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.HowMuchWithdraw") + "\r\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.WithdrawAmount"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
                case "la totalité":
                case "everything":
                    WithdrawMoney(bank, player, bank.Money);
                    break;
                case "quelques pièces":
                case "some coins":
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.WithdrawSomeCoins"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
                case "faire un chèque":
                case "write a check":
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.WriteCheck"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
                case "encaisser un chèque":
                case "cash a check":
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.CashCheck"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
                case "transfer money":
                case "transférer de l'argent":
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Transfer.Help"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
                case "term deposits":
                case "dépôts à terme":
                    ShowTermDepositMenu(player, bank);
                    break;
                case "open biweekly deposit":
                case "ouvrir un dépôt bimensuel":
                    PromptTermDeposit(player, "Banker.Term.Biweekly", "biweekly", 3);
                    break;
                case "open monthly deposit":
                case "ouvrir un dépôt mensuel":
                    PromptTermDeposit(player, "Banker.Term.Monthly", "monthly", 5);
                    break;
                case "open quarterly deposit":
                case "ouvrir un dépôt trimestriel":
                    PromptTermDeposit(player, "Banker.Term.Quarterly", "quarterly", 7);
                    break;
            }
            return true;
        }

        private void ShowTermDepositMenu(GamePlayer player, DBBanque bank)
        {
            var deposits = GameServer.Database.SelectObjects<DBTermDeposit>(d => d.PlayerID == player.InternalID);

            string msg = LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Header", deposits.Count, Properties.MAX_TERM_DEPOSITS) + "\n\n";

            if (deposits.Count > 0)
            {
                msg += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Active") + "\n";
                foreach (var d in deposits)
                {
                    TimeSpan remaining = d.MaturityDate - DateTime.Now;
                    string timeStr = remaining.Days > 0
                        ? LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Days", remaining.Days)
                        : LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Hours", remaining.Hours);

                    msg += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Contract", Currency.Copper.Mint(d.Amount).ToText(), d.InterestRate, timeStr) + "\n";
                }
                msg += "\n";
            }

            if (deposits.Count < Properties.MAX_TERM_DEPOSITS)
            {
                msg += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.NewContract") + "\n";
                msg += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Option1") + "\n";
                msg += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Option2") + "\n";
                msg += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Option3") + "\n";
            }
            else
            {
                msg += LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.MaxReached") + "\n";
            }

            player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        private void PromptTermDeposit(GamePlayer player, string termDisplayKey, string termCmd, int interest)
        {
            string displayLabel = LanguageMgr.GetTranslation(player.Client.Account.Language, termDisplayKey);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Prompt", displayLabel, interest, termCmd), eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        private void ProcessMaturedDeposits(GamePlayer player, DBBanque bank)
        {
            var deposits = GameServer.Database.SelectObjects<DBTermDeposit>(d => d.PlayerID == player.InternalID);
            foreach (var d in deposits)
            {
                if (DateTime.Now >= d.MaturityDate)
                {
                    long interestEarned = (long)(d.Amount * (d.InterestRate / 100.0));
                    long totalReturn = d.Amount + interestEarned;

                    bank.Money += totalReturn;
                    GameServer.Database.SaveObject(bank);
                    GameServer.Database.DeleteObject(d);

                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Banker.Term.Matured", Currency.Copper.Mint(totalReturn).ToText(), d.InterestRate), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
        }

        /// <summary>
        /// Retire de l'argent dans la banque et le donne au joueur
        /// </summary>
        public static bool WithdrawMoney(DBBanque bank, GamePlayer player, long money)
        {
            if (bank.Money < money)
                return false;

            bank.Money -= money;
            GameServer.Database.SaveObject(bank);
            player.AddMoney(Currency.Copper.Mint(money));
            player.SaveIntoDatabase();
            return true;
        }

        /// <summary>
        /// Retire de l'argent dans la banque sans le donner au joueur
        /// </summary>
        public static bool TakeMoney(DBBanque bank, GamePlayer player, long money)
        {
            if (bank.Money < money)
                return false;

            bank.Money -= money;
            GameServer.Database.SaveObject(bank);
            return true;
        }
    }
}
