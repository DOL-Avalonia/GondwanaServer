using System;
using System.Linq;
using DOL.Database;
using DOL.GS;
using DOL.GS.Housing;
using DOL.Language;
using DOL.GS.ServerProperties;
using DOL.GS.PacketHandler;
using DOL.GS.Finance;
using DOL.Events;

namespace DOL.GS.Scripts
{
    public static class BankLoanMgr
    {
        private static System.Threading.Timer m_loanTimer;
        private static int m_currentInterval = 60000; // Track interval state

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            m_loanTimer = new System.Threading.Timer(CheckPeriodicLoans, null, 60000, 60000);
        }

        public static InventoryItem FindCoupon(GamePlayer player, long cost, bool isHouseMerchant)
        {
            lock (player.Inventory)
            {
                foreach (InventoryItem item in player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack).Concat(player.Inventory.GetItemRange(eInventorySlot.FirstVault, eInventorySlot.LastVault)).Concat(player.Inventory.GetItemRange(eInventorySlot.HouseVault_First, eInventorySlot.Consignment_Last)))
                {
                    if (item.Template != null)
                    {
                        if (item.Template.Flags == 46) // Personal 
                        {
                            if (item.MaxCondition == 400 && cost >= 800000) return item;
                            if ((item.MaxCondition == 600 || item.MaxCondition == 800) && cost >= 1200000) return item;
                        }
                        if (item.Template.Flags == 47 && isHouseMerchant) // House
                        {
                            if (item.MaxCondition == 1500 && cost >= 9000000) return item;
                            if ((item.MaxCondition == 3000 || item.MaxCondition == 6000) && cost >= 10000000) return item;
                            if ((item.MaxCondition == 10000 || item.MaxCondition == 25000) && cost >= 40000000) return item;
                        }
                    }
                }
            }
            return null;
        }

        public static bool HasCoupon(GamePlayer player)
        {
            var items = GameServer.Database.SelectObjects<InventoryItem>(DB.Column("OwnerID").IsEqualTo(player.InternalID));
            foreach (var item in items)
            {
                var t = GameServer.Database.FindObjectByKey<ItemTemplate>(item.Id_nb) ?? GameServer.Database.FindObjectByKey<ItemUnique>(item.Id_nb) as ItemTemplate;
                if (t != null && (t.Flags == 46 || t.Flags == 47))
                    return true;
            }
            return false;
        }

        public static void IssueCoupon(GamePlayer player, DBBanque bank, int type, long amount)
        {
            if (bank.LoanAmount > 0) { player.Out.SendMessage("You already have an active loan.", eChatType.CT_System, eChatLoc.CL_SystemWindow); return; }
            if (bank.IsDebtor) { player.Out.SendMessage("You cannot get a loan while you are marked as a debtor.", eChatType.CT_System, eChatLoc.CL_SystemWindow); return; }
            if (DateTime.Now < bank.LoanRestrictionUntil) { player.Out.SendMessage("You are restricted from making loans due to previous debts.", eChatType.CT_System, eChatLoc.CL_SystemWindow); return; }

            long minRequiredFunds = 0;
            if (type == 1)
            {
                if (amount == 400) minRequiredFunds = 500000;
                else if (amount == 600 || amount == 800) minRequiredFunds = 800000;
            }
            else if (type == 2)
            {
                if (amount == 1500) minRequiredFunds = 2500000;
                else if (amount == 3000 || amount == 6000) minRequiredFunds = 5000000;
                else if (amount == 10000 || amount == 25000) minRequiredFunds = 20000000;
            }

            if (bank.Money < minRequiredFunds)
            {
                player.Out.SendMessage($"You do not have the minimum required funds in your bank ({Money.GetString(minRequiredFunds)}).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (HasCoupon(player)) { player.Out.SendMessage("You already have a loan coupon in your inventory or vault.", eChatType.CT_System, eChatLoc.CL_SystemWindow); return; }
            
            ItemUnique item = new ItemUnique
            {
                Model = 499,
                Id_nb = "LOAN_COUPON_" + player.Name + "_" + (DateTime.Now.Ticks / 10000).ToString("X8"),
                Price = 0,
                MaxCondition = (int)amount,
                Weight = 2,
                Name = (type == 1 ? "Personal" : "House") + " Loan Coupon (" + amount + "g)",
                Description = "Give this coupon to a merchant to activate your loan.",
                Flags = (type == 1 ? 46 : 47),
                IsDropable = false,
                IsPickable = false,
                IsTradable = false,
                Object_Type = 0,
                Item_Type = 40,
                ClassType = "DOL.GS.GameInventoryItem"
            };
            GameServer.Database.AddObject(item);
            
            GameInventoryItem invItem = GameInventoryItem.Create(item);
            if (!player.Inventory.AddTemplate(invItem, 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
            {
                GameServer.Database.DeleteObject(item);
                player.Out.SendMessage("Your backpack is full. Please clear some space.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            
            player.Out.SendMessage($"You received a {item.Name}. The loan countdown triggers when used at a merchant.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        public static bool TryUseCoupon(GamePlayer player, long costInCopper, bool isLotMarker)
        {
            InventoryItem coupon = FindCoupon(player, costInCopper, isLotMarker);
            if (coupon == null) 
            {
                if (HasCoupon(player)) player.Out.SendMessage("I noted you have a loan, but it isn't necessary or valid to use it for an item like this.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            long couponValueInCopper = coupon.MaxCondition * 10000L;
            if (player.CopperBalance + couponValueInCopper >= costInCopper)
            {
                ActivateLoan(player, coupon);
                return true;
            }

            return false;
        }

        public static void ActivateLoan(GamePlayer player, InventoryItem coupon)
        {
            long goldAmount = coupon.MaxCondition;
            long copperAmount = goldAmount * 10000L;
            int type = coupon.Template.Flags == 46 ? 1 : 2;
            string templateId = coupon.Id_nb;

            if (coupon.SlotPosition >= (int)eInventorySlot.FirstBackpack && coupon.SlotPosition <= (int)eInventorySlot.LastBackpack)
                player.Inventory.RemoveItem(coupon);
            else
                GameServer.Database.DeleteObject(coupon);

            ItemUnique u = GameServer.Database.FindObjectByKey<ItemUnique>(templateId);
            if (u != null) GameServer.Database.DeleteObject(u);

            player.AddMoney(Currency.Copper.Mint(copperAmount));

            DBBanque bank = GameServer.Database.FindObjectByKey<DBBanque>(player.InternalID) ?? new DBBanque(player.InternalID);
            if (bank.PlayerID == null) GameServer.Database.AddObject(bank);

            bank.LoanType = type;
            bank.LoanOriginalAmount = copperAmount;
            double interest = type == 1 ? 1.15 : 1.10;
            bank.LoanAmount = (long)(copperAmount * interest);
            bank.LoanDaysRemaining = type == 1 ? 7 : 30;
            bank.LastLoanPayment = DateTime.Now;

            GameServer.Database.SaveObject(bank);
            player.Out.SendMessage($"I've cashed your loan coupon of {goldAmount}g. The bank will withdraw daily payments.", eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
        }

        public static void RepayLoanEarly(GamePlayer player, DBBanque bank)
        {
            if (bank.LoanAmount <= 0 || bank.LoanType == 0)
            {
                player.Out.SendMessage("You don't have an active loan.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            double earlyInterest = bank.LoanType == 1 ? 1.10 : 1.06;
            double normalInterest = bank.LoanType == 1 ? 1.15 : 1.10;
            
            long totalPaid = (long)(bank.LoanOriginalAmount * normalInterest) - bank.LoanAmount;
            long newTotalOwed = (long)(bank.LoanOriginalAmount * earlyInterest);
            long earlyRepaymentAmount = newTotalOwed - totalPaid;

            if (earlyRepaymentAmount <= 0) earlyRepaymentAmount = 0;

            long bankBalance = bank.Money > 0 ? bank.Money : 0;
            long pocketMoney = player.CopperBalance;

            // Check if Bank + Pocket can cover the total early repayment cost
            if (bankBalance + pocketMoney >= earlyRepaymentAmount)
            {
                long amountToPay = earlyRepaymentAmount;

                if (bankBalance >= amountToPay)
                {
                    bank.Money -= amountToPay;
                    amountToPay = 0;
                }
                else
                {
                    if (bankBalance > 0)
                    {
                        amountToPay -= bankBalance;
                        bank.Money -= bankBalance;
                    }

                    if (amountToPay > 0)
                    {
                        player.RemoveMoney(Currency.Copper.Mint(amountToPay));
                        amountToPay = 0;
                    }
                }

                bank.LoanAmount = 0;
                bank.LoanDaysRemaining = 0;
                bank.LoanType = 0;
                bank.LoanOriginalAmount = 0;
                GameServer.Database.SaveObject(bank);

                player.Out.SendMessage($"You have successfully repaid your loan early for {Money.GetString(earlyRepaymentAmount)} with a reduced interest rate.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else
            {
                player.Out.SendMessage($"You need {Money.GetString(earlyRepaymentAmount)} in total to be able to repay your loan early.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        public static void CheckPeriodicLoans(object state)
        {
            bool debug = Properties.BANK_LOAN_DEBUG;

            int targetInterval = debug ? 15000 : 60000;
            if (m_currentInterval != targetInterval)
            {
                m_currentInterval = targetInterval;
                m_loanTimer.Change(m_currentInterval, m_currentInterval);
            }

            var banks = GameServer.Database.SelectAllObjects<DBBanque>();
            foreach (var bank in banks)
            {
                bool saved = false;

                if (bank.LoanType > 0 && bank.LoanDaysRemaining > 0 && bank.LastLoanPayment != DateTime.MinValue)
                {
                    TimeSpan passed = DateTime.Now - bank.LastLoanPayment;

                    double requiredIntervalInDays = 1.0;
                    if (debug)
                    {
                        // 30 seconds for personal (Type 1), 15 seconds for house (Type 2)
                        requiredIntervalInDays = bank.LoanType == 1 ? TimeSpan.FromSeconds(30).TotalDays : TimeSpan.FromSeconds(15).TotalDays;
                    }

                    if (passed.TotalDays >= requiredIntervalInDays)
                    {
                        int intervalsToProcess = (int)(passed.TotalDays / requiredIntervalInDays);
                        long totalOwed = bank.LoanType == 1 ? (long)(bank.LoanOriginalAmount * 1.15) : (long)(bank.LoanOriginalAmount * 1.10);
                        int totalDays = bank.LoanType == 1 ? 7 : 30;
                        long dailyPayment = totalOwed / totalDays;
                        long amountToPay = 0;

                        for (int i = 0; i < intervalsToProcess; i++)
                        {
                            if (bank.LoanDaysRemaining > 0)
                            {
                                amountToPay += dailyPayment;
                                bank.LoanAmount -= dailyPayment;
                                bank.LoanDaysRemaining--;
                            }
                        }

                        if (amountToPay > 0)
                        {
                            // 1. Try to take from bank
                            if (bank.Money >= amountToPay)
                            {
                                bank.Money -= amountToPay;
                                amountToPay = 0;
                            }
                            else
                            {
                                if (bank.Money > 0)
                                {
                                    amountToPay -= bank.Money;
                                    bank.Money = 0;
                                }

                                // 2. Try to take from Player's Pocket (Online or Offline)
                                GamePlayer p = WorldMgr.GetClientByPlayerID(bank.PlayerID, false, false)?.Player;
                                if (p != null)
                                {
                                    long pocketMoney = p.CopperBalance;
                                    if (pocketMoney >= amountToPay)
                                    {
                                        p.RemoveMoney(Currency.Copper.Mint(amountToPay));
                                        p.Out.SendMessage($"The bank withdrew {Currency.Copper.Mint(amountToPay).ToText()} from your pocket for your loan.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                                        amountToPay = 0;
                                    }
                                    else if (pocketMoney > 0)
                                    {
                                        p.RemoveMoney(Currency.Copper.Mint(pocketMoney));
                                        p.Out.SendMessage($"The bank withdrew {Currency.Copper.Mint(pocketMoney).ToText()} from your pocket to partially cover your loan.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                                        amountToPay -= pocketMoney;
                                    }
                                }
                                else
                                {
                                    // Player is offline: pull from DBCharacter directly
                                    DOLCharacters character = GameServer.Database.FindObjectByKey<DOLCharacters>(bank.PlayerID);
                                    if (character != null)
                                    {
                                        long offlineMoney = Money.GetMoney(character.Mithril, character.Platinum, character.Gold, character.Silver, character.Copper);
                                        if (offlineMoney >= amountToPay)
                                        {
                                            offlineMoney -= amountToPay;
                                            amountToPay = 0;
                                        }
                                        else if (offlineMoney > 0)
                                        {
                                            amountToPay -= offlineMoney;
                                            offlineMoney = 0;
                                        }

                                        character.Copper = Money.GetCopper(offlineMoney);
                                        character.Silver = Money.GetSilver(offlineMoney);
                                        character.Gold = Money.GetGold(offlineMoney);
                                        character.Platinum = Money.GetPlatinum(offlineMoney);
                                        character.Mithril = Money.GetMithril(offlineMoney);
                                        GameServer.Database.SaveObject(character);
                                    }
                                }

                                // 3. Push any remaining missed payment to Debt
                                if (amountToPay > 0)
                                {
                                    bank.Debt += amountToPay;
                                }
                            }
                        }

                        if (bank.LoanDaysRemaining <= 0)
                        {
                            bank.LoanType = 0;
                            bank.LoanAmount = 0;
                            bank.LoanOriginalAmount = 0;
                        }

                        // Progress the timer correctly by the evaluated fractions of a day
                        bank.LastLoanPayment = bank.LastLoanPayment.AddDays(intervalsToProcess * requiredIntervalInDays);
                        saved = true;
                    }
                }

                // Debtor Grace period check
                double debtorGraceDays = debug
                    ? TimeSpan.FromSeconds(90).TotalDays
                    : TimeSpan.FromHours(Properties.DEBTOR_GRACE_PERIOD_HOURS).TotalDays;

                if (bank.Money < 0 || bank.Debt > 0)
                {
                    if (bank.NegativeMoneySince == DateTime.MinValue)
                    {
                        bank.NegativeMoneySince = DateTime.Now;
                        bank.IsDebtor = true;
                        saved = true;
                        
                        GamePlayer p = WorldMgr.GetClientByPlayerID(bank.PlayerID, false, false)?.Player;
                        if (p != null)
                        {
                            string timeWarning = debug ? "90 seconds" : $"{ServerProperties.Properties.DEBTOR_GRACE_PERIOD_HOURS} hours";
                            p.Out.SendMessage($"WARNING: Your bank account is in debt. You have {timeWarning} to reimburse your debt or your assets will be seized!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        }
                    }
                    else if ((DateTime.Now - bank.NegativeMoneySince).TotalDays >= debtorGraceDays)
                    {
                        SeizeAssets(bank);
                        saved = true;
                    }
                }
                else
                {
                    if (bank.NegativeMoneySince != DateTime.MinValue || bank.IsDebtor)
                    {
                        bank.NegativeMoneySince = DateTime.MinValue;
                        bank.IsDebtor = false;
                        saved = true;
                    }
                }

                if (saved) GameServer.Database.SaveObject(bank);
            }
        }

        private static void SeizeAssets(DBBanque bank)
        {
            if (bank.Money < 0)
            {
                bank.Debt += Math.Abs(bank.Money);
                bank.Money = 0;
            }

            if (bank.LoanAmount > 0)
            {
                bank.Debt += bank.LoanAmount;
                bank.LoanAmount = 0;
                bank.LoanDaysRemaining = 0;
                bank.LoanType = 0;
                bank.LoanOriginalAmount = 0;
            }

            long moneyRecovered = 0;
            
            var houses = GameServer.Database.SelectObjects<DBHouse>(DB.Column("OwnerID").IsEqualTo(bank.PlayerID));
            foreach (var house in houses)
            {
                long price = 0;
                switch (house.Model % 4)
                {
                    case 1: price = 500 * 10000L; break;   // Cottage
                    case 2: price = 2500 * 10000L; break;  // House
                    case 3: price = 5000 * 10000L; break;  // Villa
                    case 0: price = 12500 * 10000L; break; // Mansion
                }

                moneyRecovered += price;
                House h = HouseMgr.GetHouse(house.RegionID, house.HouseNumber);
                if (h != null) HouseMgr.RemoveHouse(h);
                else GameServer.Database.DeleteObject(house);
            }
            
            var invItems = GameServer.Database.SelectObjects<InventoryItem>(DB.Column("OwnerID").IsEqualTo(bank.PlayerID));
            foreach (var item in invItems)
            {
                ItemTemplate tpl = GameServer.Database.FindObjectByKey<ItemTemplate>(item.Id_nb) ?? GameServer.Database.FindObjectByKey<ItemUnique>(item.Id_nb) as ItemTemplate;
                if (item.Item_Type == (int)eInventorySlot.Horse || (tpl != null && tpl.Item_Type == (int)eInventorySlot.Horse)) 
                {
                    moneyRecovered += item.Price / 2;
                    GamePlayer p = WorldMgr.GetClientByPlayerID(bank.PlayerID, false, false)?.Player;
                    if (p != null) p.Inventory.RemoveItem(item);
                    else GameServer.Database.DeleteObject(item);
                }
            }

            if (moneyRecovered >= bank.Debt)
            {
                //bank.Money += (moneyRecovered - bank.Debt); -- Assets forcefully sold ONLY cover the debt. We do not refund positive cash to the player!
                bank.Debt = 0;
            }
            else
            {
                bank.Debt -= moneyRecovered;
            }

            DOLCharacters character = GameServer.Database.FindObjectByKey<DOLCharacters>(bank.PlayerID);
            string charName = character != null ? character.Name : "Unknown";

            if (bank.Debt > 0)
            {
                long debtCopper = bank.Debt;
                long debtGold = debtCopper / 10000;
                int penaltyCost = (int)(debtGold * 1.30); // 30% added
                int jailDays = 7 + (int)(debtGold / 50);

                bank.Debt = 0; // Wipe from bank (shifts over to jail release cost)
                bank.NegativeMoneySince = DateTime.MinValue;
                bank.LoanRestrictionUntil = DateTime.Now.AddDays(jailDays).AddMonths(3);

                if (character != null)
                {
                    GamePlayer p = WorldMgr.GetClientByPlayerID(bank.PlayerID, false, false)?.Player;
                    if (p != null) JailMgr.EmprisonnerRP(p, penaltyCost, DateTime.Now.AddDays(jailDays), "Banker", "Unpaid Debt", false);
                    else JailMgr.EmprisonnerRP(charName, penaltyCost, DateTime.Now.AddDays(jailDays), "Banker", "Unpaid Debt");
                }
            }
            else
            {
                bank.NegativeMoneySince = DateTime.MinValue;
                bank.LoanRestrictionUntil = DateTime.Now.AddDays(7);

                if (character != null)
                {
                    GamePlayer p = WorldMgr.GetClientByPlayerID(bank.PlayerID, false, false)?.Player;
                    if (p != null) JailMgr.EmprisonnerRP(p, 150, DateTime.Now.AddDays(1), "Banker", "Asset Seizure for Debt", false);
                    else JailMgr.EmprisonnerRP(charName, 150, DateTime.Now.AddDays(1), "Banker", "Asset Seizure for Debt");
                }
            }
            bank.IsDebtor = false; 
        }
    }
}