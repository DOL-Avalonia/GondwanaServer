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
//12/13/2004
//Written by Gavinius
//based on Nardin and Zjovaz previous script


using System;
using System.Collections;
using DOL.Database;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS
{
    [NPCGuildScript("Enchanter")]
    public class Enchanter : GameNPC
    {
        private const string ENCHANT_ITEM_WEAK = "enchanting item";
        private int[] BONUS_TABLE = new int[] { 5, 5, 10, 15, 20, 25, 30, 30 };

        private static readonly double[] MIN_BASELINE = new double[]
        {
            0, // Level 0
            200, 500, 1000, 2000, 3500, 5500, 8000, 10500, 13500, 17000, // Levels 1-10
            22000, 30000, 40000, 52000, 66000, 82000, 100000, 120000, 144000, 170000, // Levels 11-20
            210000, 260000, 310000, 370000, 430000, 490000, 560000, 630000, 710000, 800000, // Levels 21-30
            900000, 1000000, 1100000, 1210000, 1330000, 1450000, 1580000, 1710000, 1850000, 2000000, // Levels 31-40
            2090000, 2180000, 2270000, 2360000, 2450000, 2540000, 2630000, 2720000, 2810000, 2900000, 3000000 // Levels 41-51
        };

        private static readonly double[] MAX_BASELINE = new double[]
        {
            0, // Level 0
            300, 700, 1500, 2800, 4500, 7000, 10000, 14000, 18500, 24000, // Levels 1-10
            32000, 43000, 56000, 72000, 90000, 110000, 135000, 160000, 185000, 210000, // Levels 11-20
            280000, 370000, 480000, 610000, 760000, 930000, 1110000, 1300000, 1500000, 1700000, // Levels 21-30
            1810000, 1930000, 2060000, 2200000, 2350000, 2510000, 2680000, 2860000, 3050000, 3000000, // Levels 31-40
            3300000, 3600000, 3900000, 4200000, 4500000, 4800000, 5100000, 5400000, 5700000, 6000000, 6000000 // Levels 41-51
        };

        /// <summary>
        /// Adds messages to ArrayList which are sent when object is targeted
        /// </summary>
        /// <param name="player">GamePlayer that is examining this object</param>
        /// <returns>list with string messages</returns>
		public override IList GetExamineMessages(GamePlayer player)
        {
            IList list = new ArrayList();
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Enchanter.GetExamineMessages.Text1",
                                                GetName(0, false, player.Client.Account.Language, this)));
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Enchanter.GetExamineMessages.Text2",
                                                GetName(0, false, player.Client.Account.Language, this), GetPronoun(0, true, player.Client.Account.Language),
                                                GetAggroLevelString(player, false)));
            return list;
        }

        public override bool Interact(GamePlayer player)
        {
            if (base.Interact(player))
            {
                TurnTo(player, 25000);
                string Material;
                if (player.Realm == eRealm.Hibernia)
                    Material = LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "Enchanter.Interact.Text1");
                else
                    Material = LanguageMgr.GetTranslation(ServerProperties.Properties.DB_LANGUAGE, "Enchanter.Interact.Text2");

                SayTo(player, eChatLoc.CL_ChatWindow, LanguageMgr.GetTranslation(player.Client.Account.Language, "Enchanter.Interact.Text3", Material));
                return true;
            }
            return false;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            GamePlayer t = source as GamePlayer;
            if (t == null || item == null)
                return false;

            if (item.Level >= 10 && item.IsCrafted)
            {
                if (item.Object_Type != (int)eObjectType.Magical && item.Object_Type != (int)eObjectType.Bolt && item.Object_Type != (int)eObjectType.Poison)
                {
                    if (item.Bonus == 0)
                    {
                        t.TempProperties.setProperty(ENCHANT_ITEM_WEAK, new WeakRef(item));
                        t.Client.Out.SendCustomDialog(LanguageMgr.GetTranslation(t.Client, "Enchanter.ReceiveItem.Text1", Money.GetString(CalculEnchantPrice(item))), new CustomDialogResponse(EnchanterDialogResponse));
                    }
                    else
                        SayTo(t, eChatLoc.CL_SystemWindow, LanguageMgr.GetTranslation(t.Client, "Enchanter.ReceiveItem.Text2"));
                }
                else
                    SayTo(t, eChatLoc.CL_SystemWindow, LanguageMgr.GetTranslation(t.Client, "Enchanter.ReceiveItem.Text3"));
            }
            else
                SayTo(t, eChatLoc.CL_SystemWindow, LanguageMgr.GetTranslation(t.Client, "Enchanter.ReceiveItem.Text4"));

            return false;
        }

        protected void EnchanterDialogResponse(GamePlayer player, byte response)
        {
            WeakReference itemWeak =
                (WeakReference)player.TempProperties.getProperty<object>(
                    ENCHANT_ITEM_WEAK,
                    new WeakRef(null)
                    );
            player.TempProperties.removeProperty(ENCHANT_ITEM_WEAK);


            if (response != 0x01 || !this.IsWithinRadius(player, WorldMgr.INTERACT_DISTANCE))
                return;

            InventoryItem item = (InventoryItem)itemWeak.Target;
            if (item == null || item.SlotPosition == (int)eInventorySlot.Ground
                || item.OwnerID == null || item.OwnerID != player.InternalID)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Enchanter.EnchanterDialogResponse.Text1"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            long Fee = CalculEnchantPrice(item);

            if (player.CopperBalance < Fee)
            {
                SayTo(player, eChatLoc.CL_SystemWindow, LanguageMgr.GetTranslation(player.Client.Account.Language, "Enchanter.EnchanterDialogResponse.Text2", Money.GetString(Fee)));
                return;
            }
            if (item.Level < 50)
                item.Bonus = BONUS_TABLE[(item.Level / 5) - 2];
            else
                item.Bonus = 35;

            item.Name = LanguageMgr.GetTranslation(player.Client.Account.Language, "Enchanter.EnchanterDialogResponse.Text3") + " " + item.Name;
            player.Out.SendInventoryItemsUpdate(new InventoryItem[] { item });
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Enchanter.EnchanterDialogResponse.Text4",
                                    GetName(0, false, player.Client.Account.Language, this), Money.GetString(Fee)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.RemoveMoney(Currency.Copper.Mint(Fee));
            InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Merchant, Fee);
            SayTo(player, eChatLoc.CL_SystemWindow, LanguageMgr.GetTranslation(player.Client.Account.Language, "Enchanter.EnchanterDialogResponse.Text5", item.GetName(1, false)));
            return;
        }

        public long CalculEnchantPrice(InventoryItem item)
        {
            GetBaselinePrice(item.Level, out double minBase, out double maxBase);

            double currentPrice = item.Price;
            double minAllowedPrice = minBase * 0.40;
            double maxAllowedPrice = maxBase * 1.60;

            double effectivePrice = currentPrice;

            if (currentPrice < minAllowedPrice)
            {
                effectivePrice = minAllowedPrice;
            }
            else if (currentPrice > maxAllowedPrice)
            {
                effectivePrice = maxAllowedPrice;
            }

            double basePrice = effectivePrice / 5.0;
            double levelDiscountPercent;

            if (item.Level >= 51)
            {
                levelDiscountPercent = 20.0;
            }
            else if (item.Level <= 20)
            {
                levelDiscountPercent = 35.0;
            }
            else
            {
                levelDiscountPercent = 35.0 - ((item.Level - 20.0) * (15.0 / 31.0));
            }

            double priceAfterLevel = basePrice * (1.0 - (levelDiscountPercent / 100.0));
            double baselineModifier = 1.0;

            if (effectivePrice < minBase)
            {
                double percentBelow = (minBase - effectivePrice) / minBase;
                double penalty = percentBelow * 0.50;
                baselineModifier = 1.0 + penalty;
            }
            else if (effectivePrice > maxBase)
            {
                double percentAbove = (effectivePrice - maxBase) / maxBase;
                double extraDiscount = percentAbove * 0.20;

                extraDiscount = Math.Min(0.15, extraDiscount);
                baselineModifier = 1.0 - extraDiscount;
            }

            double priceAfterBaseline = priceAfterLevel * baselineModifier;

            double qualityFactor = Math.Min(100.0, Math.Max(1.0, item.Quality)) / 100.0;
            double finalPrice = priceAfterBaseline * qualityFactor;

            return (long)finalPrice;
        }

        /// <summary>
        /// Fetches the expected copper price ranges based on the pre-calculated item level array.
        /// </summary>
        private void GetBaselinePrice(int level, out double minPrice, out double maxPrice)
        {
            int safeLevel = Math.Max(1, Math.Min(51, level));

            minPrice = MIN_BASELINE[safeLevel];
            maxPrice = MAX_BASELINE[safeLevel];
        }
    }
}