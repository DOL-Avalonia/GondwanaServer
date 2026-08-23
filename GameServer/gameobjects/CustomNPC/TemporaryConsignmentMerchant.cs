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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.Database;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.AI.Brain;
using log4net;
using DOL.GS.Finance;
using System.Numerics;

namespace DOL.GS
{
    public class TemporaryConsignmentMerchant : GameConsignmentMerchant
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The Player is buying an Item from the merchant
        /// </summary>
        /// <param name="player"></param>
        /// <param name="playerInventory"></param>
        /// <param name="fromClientSlot"></param>
        /// <param name="toClientSlot"></param>
        public override void OnPlayerBuy(GamePlayer player, eInventorySlot fromClientSlot, eInventorySlot toClientSlot, bool usingMarketExplorer = false)
        {
            IDictionary<int, InventoryItem> clientInventory = GetClientInventory(player);

            InventoryItem fromItem = null;

            if (clientInventory.ContainsKey((int)fromClientSlot))
            {
                fromItem = clientInventory[(int)fromClientSlot];
            }

            if (fromItem == null)
            {
                ChatUtil.SendErrorMessage(player, "I can't find the item you want to purchase!");
                log.ErrorFormat("CM: {0}:{1} can't find item to buy in slot {2} on consignment merchant on lot {3}.", player.Name, player.Client.Account, (int)fromClientSlot, HouseNumber);
                return;
            }

            string buyText = "Do you want to buy this Item?";// (Price after " + ServerProperties.Properties.TRADING_TAX + "% tax: "
                                                             // + Money.GetString((long)(fromItem.Price * (1 + (float)ServerProperties.Properties.TRADING_TAX / 100))) + ")";

            if (player.TargetObject == this)
            {
                player.TempProperties.setProperty(CONSIGNMENT_BUY_ITEM, fromClientSlot);
                player.Out.SendCustomDialog(buyText, new CustomDialogResponse(BuyResponse));
            }
            else
            {
                ChatUtil.SendErrorMessage(player, "I'm sorry, you need to be talking to a market explorer or consignment merchant in order to make a purchase.");
                log.ErrorFormat("CM: {0}:{1} did not have a CM or ME targeted when attempting to purchase {2} on consignment merchant on lot {3}.", player.Name, player.Client.Account, fromItem.Name, HouseNumber);
            }
        }

        protected override void BuyItem(GamePlayer player, bool usingMarketExplorer = false)
        {
            eInventorySlot fromClientSlot = player.TempProperties.getProperty<eInventorySlot>(CONSIGNMENT_BUY_ITEM, eInventorySlot.Invalid);
            player.TempProperties.removeProperty(CONSIGNMENT_BUY_ITEM);

            InventoryItem item = null;

            lock (LockObject())
            {

                if (fromClientSlot != eInventorySlot.Invalid)
                {
                    IDictionary<int, InventoryItem> clientInventory = GetClientInventory(player);

                    if (clientInventory.ContainsKey((int)fromClientSlot))
                    {
                        item = clientInventory[(int)fromClientSlot];
                    }
                }

                if (item == null)
                {
                    ChatUtil.SendErrorMessage(player, "I can't find the item you want to purchase!");
                    log.ErrorFormat("{0}:{1} tried to purchase an item from slot {2} for consignment merchant on lot {3} and the item does not exist.", player.Name, player.Client.Account, (int)fromClientSlot, HouseNumber);

                    return;
                }

                int sellPrice = item.SellPrice;
                int purchasePrice = sellPrice; // (int)(sellPrice * (1 + (float)ServerProperties.Properties.TRADING_TAX / 100));

                lock (player.Inventory)
                {
                    if (purchasePrice <= 0)
                    {
                        ChatUtil.SendErrorMessage(player, "This item can't be purchased!");
                        log.ErrorFormat("{0}:{1} tried to purchase {2} for consignment merchant on lot {3} and purchasePrice was {4}.", player.Name, player.Client.Account, item.Name, HouseNumber, purchasePrice);
                        return;
                    }

                    if (ServerProperties.Properties.CONSIGNMENT_USE_BP)
                    {
                        if (player.GetBalance(Currency.BountyPoints).Amount < purchasePrice)
                        {
                            ChatUtil.SendSystemMessage(player, "GameMerchant.OnPlayerBuy.YouNeedBP", purchasePrice);
                            return;
                        }
                    }
                    else
                    {
                        if (player.CopperBalance < purchasePrice)
                        {
                            ChatUtil.SendSystemMessage(player, "GameMerchant.OnPlayerBuy.YouNeed", Money.GetString(purchasePrice, player.Client.Account.Language));
                            return;
                        }
                    }

                    eInventorySlot toClientSlot = player.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);

                    if (toClientSlot == eInventorySlot.Invalid)
                    {
                        ChatUtil.SendSystemMessage(player, "GameMerchant.OnPlayerBuy.NotInventorySpace", null);
                        return;
                    }

                    if (ServerProperties.Properties.CONSIGNMENT_USE_BP)
                    {
                        ChatUtil.SendMerchantMessage(player, "GameMerchant.OnPlayerBuy.BoughtBP", item.GetName(1, false), purchasePrice);
                        Currency.BountyPoints.Mint(player.GetBalance(Currency.BountyPoints).Amount - purchasePrice);
                        player.Out.SendUpdatePoints();
                    }
                    else
                    {
                        if (player.RemoveMoney(Currency.Copper.Mint(purchasePrice)))
                        {
                            InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Merchant, purchasePrice);
                            ChatUtil.SendMerchantMessage(player, "GameMerchant.OnPlayerBuy.Bought", item.GetName(1, false), Money.GetString(purchasePrice, player.Client.Account.Language));
                        }
                        else
                        {
                            return;
                        }
                    }

                    TotalMoney += sellPrice;

                    if (ServerProperties.Properties.MARKET_ENABLE_LOG)
                    {
                        log.DebugFormat("CM: {0}:{1} purchased '{2}' for {3} from consignment merchant on lot {4}.", player.Name, player.Client.Account.Name, item.Name, purchasePrice, HouseNumber);
                    }

                    this.NotifyPlayers(this, player, _observers, this.MoveItem(player, fromClientSlot, toClientSlot, (ushort)item.Count));
                }
            }
            if (playerOwner != null && playerOwner != player)
            {
                TaskManager.UpdateTaskProgress(playerOwner, "ItemsSoldToPlayers", 1);
            }
        }
        public GamePlayer playerOwner;
        public GameTradingTable tableModel;
        public override bool AddToWorld()
        {
            //set playerOwner before calling

            //set brain for despawning out of range
            var brain = new TempConsignmentBrain(playerOwner);
            brain.Body = this;
            AddBrain(brain);
            OwnerID = playerOwner.ObjectId;
            var CM = DOLDB<HouseConsignmentMerchant>.SelectObject(DB.Column(nameof(HouseConsignmentMerchant.OwnerID)).IsEqualTo(OwnerID));
            if (CM == null)
            {
                // create a new consignment merchant entry, and add it to the DB
                CM = new HouseConsignmentMerchant { OwnerID = playerOwner.ObjectId, HouseNumber = 0, Money = 0 };
                GameServer.Database.AddObject(CM);
            }
            TotalMoney = CM.Money;
            // create table model

            tableModel = new GameTradingTable();
            tableModel.consignmentMerchant = this;
            tableModel.LoadedFromScript = false;
            tableModel.Position = playerOwner.Position;
            tableModel.Name = "Market";
            tableModel.Model = 1494;
            tableModel.Realm = 0;
            tableModel.AddToWorld();

            //create merchant
            this.Position = playerOwner.Position;
            this.Level = 70;
            this.Realm = (eRealm)playerOwner.Realm;
            this.HouseNumber = 0;
            this.Name = "Market";
            this.Model = 667;

            this.Flags |= GameNPC.eFlags.PEACE;
            this.LoadedFromScript = false;
            this.RoamingRange = 0;

            houseRequired = false;
            base.AddToWorld();
            playerOwner.Out.SendSoundEffect(9205, playerOwner.Position, 0);
            return true;
        }

        public override void Delete()
        {
            playerOwner.Out.SendSoundEffect(923, playerOwner.Position, 0);
            tableModel.Delete();
            base.Delete();
        }

    }
}