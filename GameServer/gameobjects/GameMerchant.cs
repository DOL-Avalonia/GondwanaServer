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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Language;
using DOL.GS.PacketHandler;
using DOL.Events;
using DOL.GS.Quests;
using DOL.Territories;
using DOL.GS.Finance;
using DOL.GS.Profession;
using DOL.GS.Scripts;

namespace DOL.GS
{
    /// <summary>
    /// Represents an in-game merchant
    /// </summary>
    public class GameMerchant : GameNPC
    {
        #region GetExamineMessages / Interact

        /// <summary>
		/// Adds messages to ArrayList which are sent when object is targeted
		/// </summary>
		/// <param name="player">GamePlayer that is examining this object</param>
		/// <returns>list with string messages</returns>
		public override IList GetExamineMessages(GamePlayer player)
        {
            IList list = base.GetExamineMessages(player);
            list.RemoveAt(list.Count - 1);
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.GetExamineMessages.YouExamine",
                                                GetName(0, false, player.Client.Account.Language, this), GetPronoun(0, true, player.Client.Account.Language),
                                                GetAggroLevelString(player, false)));
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.GetExamineMessages.RightClick"));
            return list;
        }

        /// <summary>
        /// Called when a player right clicks on the merchant
        /// </summary>
        /// <param name="player">Player that interacted with the merchant</param>
        /// <returns>True if succeeded</returns>
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            if (player.Reputation < 0)
            {
                TurnTo(player, 5000);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.Interact.Territory.Outlaws"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (this is ITextNPC textNPC)
            {
                var policy = textNPC.GetTextNPCPolicy(player);

                if (policy.IsOutlawFriendly.HasValue)
                {
                    if (policy.IsOutlawFriendly.Value && player.Reputation >= 0 && player.Client.Account.PrivLevel == 1)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.Interact.Territory.AgainstOutlaws"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return false;
                    }
                    else if (!policy.IsOutlawFriendly.Value && player.Reputation < 0 && player.Client.Account.PrivLevel == 1)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.Interact.Territory.AgainstNonOutlaws"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return false;
                    }
                }

                if (policy.IsTerritoryLinked.HasValue && policy.IsTerritoryLinked.Value && !TerritoryManager.IsPlayerInOwnedTerritory(player, this))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TextNPC.NotInOwnedTerritory"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
            }

            TurnTo(player, 10000);
            SendMerchantWindow(player);
            return true;
        }

        /// <summary>
        /// send the merchants item offer window to a player
        /// </summary>
        /// <param name="player"></param>
        public virtual void SendMerchantWindow(GamePlayer player)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(SendMerchantWindowCallback), player);
        }

        /// <summary>
        /// Sends merchant window from threadpool thread
        /// </summary>
        /// <param name="state">The game player to send to</param>
        protected virtual void SendMerchantWindowCallback(object state)
        {
            ((GamePlayer)state).Out.SendMerchantWindow(Catalog, eMerchantWindowType.Normal);
        }
        #endregion

        #region Items List

        /// <summary>
        /// Items available for sale
        /// </summary>
        protected MerchantTradeItems m_tradeItems;

        /// <summary>
        /// Gets the items available from this merchant
        /// </summary>
		[Obsolete("Use .Catalog instead.")]
        public MerchantTradeItems TradeItems
        {
            get => Catalog.IsEmpty ? null : Catalog.ConvertToMerchantTradeItems();
            set
            {
                if (value != null) Catalog = value.Catalog;
            }
        }

        public MerchantCatalog Catalog { get; set; } = MerchantCatalog.Create();

        #endregion

        #region Buy / Sell / Apparaise

        public virtual void OnPlayerBuy(GamePlayer player, int globalSlotPosition, int amountToBuy)
        {
            int pageNumber = globalSlotPosition / MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS;
            int slotPosition = globalSlotPosition % MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS;

            OnPlayerBuy(player, slotPosition, pageNumber, amountToBuy);
        }

        public virtual void OnPlayerBuy(GamePlayer player, int slotPosition, int pageNumber, int amountToBuy)
        {
            var page = Catalog.GetPage(pageNumber);
            var articleToBuy = page.GetEntry((byte)slotPosition);
            var itemToBuy = articleToBuy.Item;
            if (itemToBuy == null) return;
            var currency = page.Currency;

            if (itemToBuy.PackSize > 0)
                amountToBuy *= itemToBuy.PackSize;

            if (amountToBuy <= 0) return;

            long cost = amountToBuy * articleToBuy.GetCurrencyFor(player);
            GameInventoryItem item;

            lock (player.Inventory)
            {
                var price = currency.Mint(cost);
                var costToText = price.ToText();
                var playerHasNotEnoughBalance = player.GetBalance(price.Currency).Amount < price.Amount;
                if (playerHasNotEnoughBalance)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerBuy.YouNeedGeneric", costToText), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
                
                item = GameInventoryItem.Create(itemToBuy);
                if (!player.Inventory.AddTemplate(item, amountToBuy, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerBuy.NotInventorySpace"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
                InventoryLogging.LogInventoryAction(this, player, eInventoryActionType.Merchant, itemToBuy, amountToBuy);

                string message;
                if (amountToBuy == 1)
                    message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerBuy.BoughtGeneric", itemToBuy.Name, costToText);
                else
                    message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerBuy.BoughtPiecesGeneric", amountToBuy, itemToBuy.Name, costToText);
                if (!player.RemoveMoney(price)) throw new Exception("Money amount changed while adding items.");
                player.Out.SendMessage(message, eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
            }
                
            player.Notify(GamePlayerEvent.ReceiveItem, player, new ReceiveItemEventArgs(this, player, item));
        }

        public static void OnPlayerBuy(GamePlayer player, int item_slot, int page, int number, MerchantTradeItems TradeItems)
        {
            ItemTemplate template = TradeItems.GetItem(page, (eMerchantWindowSlot)item_slot);
            if (template == null) return;

            int amountToBuy = number;
            if (template.PackSize > 0)
                amountToBuy *= template.PackSize;

            if (amountToBuy <= 0) return;

            var totalCost = Currency.Copper.Mint(number * MerchantCatalogEntry.GetCurrencyFor(player, template.Price));

            GameInventoryItem item;
            lock (player.Inventory)
            {

                if (player.CopperBalance < totalCost.Amount)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerBuy.YouNeed", totalCost.ToText()), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
                
                item = GameInventoryItem.Create(template);
                if (!player.Inventory.AddTemplate(item, amountToBuy, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerBuy.NotInventorySpace"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
                InventoryLogging.LogInventoryAction(TradeItems.ItemsListID, $"(TRADEITEMS;{TradeItems.ItemsListID})", player, eInventoryActionType.Merchant, template, amountToBuy);
                //Generate the buy message
                string message;
                if (amountToBuy > 1)

                    message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerBuy.BoughtPieces", amountToBuy, template.GetName(1, false), totalCost.ToText());
                else
                    message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerBuy.Bought", template.GetName(1, false), totalCost.ToText());

                // Check if player has enough money and subtract the money
                if (!player.RemoveMoney(totalCost))
                {
                    throw new Exception("Money amount changed while adding items.");
                }
                player.SendMessage(message, eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
                InventoryLogging.LogInventoryAction(player, TradeItems.ItemsListID, $"(TRADEITEMS;{TradeItems.ItemsListID})", eInventoryActionType.Merchant, totalCost.Amount);
            }
            
            player.Notify(GamePlayerEvent.ReceiveItem, player, new ReceiveItemEventArgs(null, player, item));
        }

        public static void OnPlayerBuy(GamePlayer player, int item_slot, int number, MerchantTradeItems TradeItems)
        {
            int pagenumber = item_slot / MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS;
            int slotnumber = item_slot % MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS;

            OnPlayerBuy(player, slotnumber, pagenumber, number, TradeItems);
        }

        /// <summary>
        /// Called when a player sells something
        /// </summary>
        /// <param name="player">Player making the sale</param>
        /// <param name="item">The InventoryItem to be sold</param>
        /// <returns>true if selling is allowed, false if it should be prevented</returns>
        public virtual void OnPlayerSell(GamePlayer player, InventoryItem item)
        {
            if (item == null || player == null) return;
            if (!item.IsDropable || item.Flags == 1)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerSell.CantBeSold"), eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!this.IsWithinRadius(player, GS.ServerProperties.Properties.WORLD_PICKUP_DISTANCE)) // tested
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerSell.TooFarAway", GetName(0, true)), eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
                return;
            }

            long itemValue = OnPlayerAppraise(player, item, true);

            if (itemValue == 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerSell.IsntInterested", GetName(0, true), item.GetName(0, false)), eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
                return;
            }

            if (player.Inventory.RemoveItem(item))
            {
                string message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerSell.GivesYou", GetName(0, true), Money.GetString(itemValue), item.GetName(0, false));
                player.AddMoney(Currency.Copper.Mint(itemValue));
                player.SendMessage(message, eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
                InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Merchant, item, item.Count);
                InventoryLogging.LogInventoryAction(this, player, eInventoryActionType.Merchant, itemValue);
                return;
            }
            else
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerSell.CantBeSold"), eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
        }

        /// <summary>
        /// Called to appraise the value of an item
        /// </summary>
        /// <param name="player">The player whose item needs appraising</param>
        /// <param name="item">The item to be appraised</param>
        /// <param name="silent"></param>
        /// <returns>The price this merchant will pay for the offered items</returns>
        public virtual long OnPlayerAppraise(GamePlayer player, InventoryItem item, bool silent)
        {
            if (item == null)
                return 0;

            int itemCount = Math.Max(1, item.Count);
            int packSize = Math.Max(1, item.PackSize);

            // Default sell ratio from server properties (usually 50)
            int sellRatio = ServerProperties.Properties.ITEM_SELL_RATIO;

            // Custom Potion/Parchment Logic: Adjust ratio based on charges
            if (IsChargedMagicalItem(item))
            {
                sellRatio = GetMagicalItemSellRatio(item);
            }

            long val = item.Price * itemCount / packSize * sellRatio / 100;

            if (!item.IsDropable || item.Flags == 1)
            {
                val = 0;
            }

            if (!silent)
            {
                string message;
                if (val == 0)
                {
                    message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerSell.IsntInterested", GetName(0, true), item.GetName(0, false));
                }
                else
                {
                    message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.OnPlayerAppraise.Offers", GetName(0, true), Money.GetString(val), item.GetName(0, false));
                }
                player.Out.SendMessage(message, eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
            }
            return val;
        }

        /// <summary>
        /// Logic to identify the specific stackable potions/perchments defined by the server rules.
        /// Use Flag 3 on all Potions items and Flag 4 for all Perchments.
        /// </summary>
        private bool IsChargedMagicalItem(InventoryItem item)
        {
            if (item == null) return false;
            if (item.Object_Type != 41) return false;
            if (item.Item_Type != 40) return false;
            if (item.MaxCount != 1) return false;
            if (item.MaxCharges <= 1) return false;
            if (item.Template.Flags != 3 && item.Template.Flags != 4) return false;
            if (item.SpellID <= 0) return false;

            if (item.SpellID1 != 0 || item.Charges1 != 0 || item.MaxCharges1 != 0 ||
                item.ProcChance != 0 || item.ProcSpellID != 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates the sell percentage based on remaining charges.
        /// </summary>
        private int GetMagicalItemSellRatio(InventoryItem item)
        {
            if (item.MaxCharges <= 0) return 10;

            double percent = (double)item.Charges / item.MaxCharges;

            if (percent > 0.80) return 50;

            if (percent > 0.60) return 40;

            if (percent > 0.40) return 30;

            if (percent > 0.20) return 20;

            return 10;
        }

        #endregion

        #region NPCTemplate
        public override void LoadTemplate(INpcTemplate template)
        {
            base.LoadTemplate(template);

            if (template != null && string.IsNullOrEmpty(template.ItemsListTemplateID) == false)
            {
                Catalog = MerchantCatalog.LoadFromDatabase(template.ItemsListTemplateID);
            }
        }
        #endregion NPCTemplate

        #region Database

        /// <summary>
        /// Loads a merchant from the DB
        /// </summary>
        /// <param name="merchantobject">The merchant DB object</param>
        public override void LoadFromDatabase(DataObject merchantobject)
        {
            base.LoadFromDatabase(merchantobject);
            if (!(merchantobject is Mob)) return;
            Mob merchant = (Mob)merchantobject;
            if (string.IsNullOrEmpty(merchant.ItemsListTemplateID) == false)
                Catalog = MerchantCatalog.LoadFromDatabase(merchant.ItemsListTemplateID);
        }

        /// <summary>
        /// Saves a merchant into the DB
        /// </summary>
        public override void SaveIntoDatabase()
        {
            Mob merchant = null;
            if (InternalID != null)
                merchant = GameServer.Database.FindObjectByKey<Mob>(InternalID);
            if (merchant == null)
                merchant = new Mob();

            merchant.Name = Name;
            merchant.Guild = GuildName;;
            merchant.X = Position.X;
            merchant.Y = Position.Y;
            merchant.Z = Position.Z;
            merchant.Heading = Orientation.InHeading;
            merchant.Region = Position.RegionID;
            merchant.Speed = MaxSpeedBase;
            merchant.Realm = (byte)Realm;
            merchant.RoamingRange = RoamingRange;
            merchant.Model = Model;
            merchant.Size = Size;
            merchant.Level = Level;
            merchant.Gender = (byte)Gender;
            merchant.Flags = (uint)Flags;
            merchant.PathID = PathID;
            merchant.PackageID = PackageID;
            merchant.OwnerID = OwnerID;
            merchant.IsRenaissance = IsRenaissance;

            IOldAggressiveBrain aggroBrain = Brain as IOldAggressiveBrain;
            if (aggroBrain != null)
            {
                merchant.AggroLevel = aggroBrain.AggroLevel;
                merchant.AggroRange = aggroBrain.AggroRange;
            }
            merchant.ClassType = this.GetType().ToString();
            merchant.EquipmentTemplateID = EquipmentTemplateID;
            merchant.ItemsListTemplateID = Catalog.ItemListId;

            if (InternalID == null)
            {
                GameServer.Database.AddObject(merchant);
                InternalID = merchant.ObjectId;
            }
            else
            {
                GameServer.Database.SaveObject(merchant);
            }
        }

        /// <summary>
        /// Deletes a merchant from the DB
        /// </summary>
        public override void DeleteFromDatabase()
        {
            if (InternalID != null)
            {
                Mob merchant = GameServer.Database.FindObjectByKey<Mob>(InternalID);
                if (merchant != null)
                    GameServer.Database.DeleteObject(merchant);
            }
            InternalID = null;
        }

        #endregion
    }


    /// <summary>
    /// Game Territory Merchant
    /// If territory has a guild, can interract only this guild players
    /// </summary>
    public class GameTerritoryMerchant : GameMerchant
    {
        public override bool Interact(GamePlayer player)
        {
            if (player.Client.Account.PrivLevel == 1 && !IsWithinRadius(player, InteractDistance))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObject.Interact.TooFarAway", GetName(0, true)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Notify(GameObjectEvent.InteractFailed, this, new InteractEventArgs(player));
                return false;
            }

            Notify(GameObjectEvent.Interact, this, new InteractEventArgs(player));
            player.Notify(GameObjectEvent.InteractWith, player, new InteractWithEventArgs(this));

            bool accessGranted = false;

            if (CurrentTerritory != null && !CurrentTerritory.IsNeutral())
            {
                if (player.Guild != null && CurrentTerritory.OwnerGuild == player.Guild)
                {
                    accessGranted = true;
                }
            }

            if (!accessGranted)
            {
                /*
                if (player.Reputation < 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.Interact.Territory.Outlaws"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                    Notify(GameObjectEvent.InteractFailed, this, new InteractEventArgs(player));
                    return false;
                }
                */
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.Interact.Territory.NotSameGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Notify(GameObjectEvent.InteractFailed, this, new InteractEventArgs(player));
                return false;
            }

            FireAmbientSentence(eAmbientTrigger.interact, player);

            TurnTo(player, 10000);
            SendMerchantWindow(player);

            return true;
        }
    }

    [Obsolete("Use GameMerchant instead. "
        + "Set currency in database. "
        + "Adjust exchange rates by setting the amount/price for another currency item.")]
    public abstract class GameItemCurrencyMerchant : GameMerchant
    {
        protected virtual Currency Currency { get; }
        private static Dictionary<Currency, int> currencyExchangeRates = new Dictionary<Currency, int>();
        private static Dictionary<Currency, ItemTemplate> defaultCurrencyItems = new Dictionary<Currency, ItemTemplate>();

        public override void LoadTemplate(INpcTemplate template)
        {
            base.LoadTemplate(template);
            SetCurrencyForAllPages();
        }

        public override void LoadFromDatabase(DataObject merchantobject)
        {
            base.LoadFromDatabase(merchantobject);
            SetCurrencyForAllPages();
        }
        private void SetCurrencyForAllPages()
        {
            foreach (var page in Catalog.GetAllPages()) page.SetCurrency(Currency);
        }

        static GameItemCurrencyMerchant()
        {
            LoadExchangeRates();
        }

        private static void LoadExchangeRates()
        {
            if (ServerProperties.Properties.CURRENCY_EXCHANGE_ALLOW == false) return;

            foreach (string currencyExchangePair in ServerProperties.Properties.CURRENCY_EXCHANGE_VALUES.Split(';'))
            {
                string[] asVal = currencyExchangePair.Split('|');

                if (asVal.Length > 1 && int.TryParse(asVal[1], out int currencyValue) && currencyValue > 0)
                {
                    var currencyId = asVal[0];
                    var currency = Currency.Item(currencyId);

                    var defaultCurrencyItem = DOLDB<ItemTemplate>.SelectObject(DB.Column("id_nb").IsEqualTo(currencyId));
                    if (defaultCurrencyItem == null) continue;
                    if (string.IsNullOrEmpty(defaultCurrencyItem.ClassType))
                    {
                        defaultCurrencyItem.AllowUpdate = true;
                        defaultCurrencyItem.ClassType = $"Currency.{currencyId}";
                        GameServer.Database.SaveObject(defaultCurrencyItem);
                    }
                    defaultCurrencyItems[currency] = defaultCurrencyItem;
                    currencyExchangeRates[Currency.Item(currencyId)] = currencyValue;
                }
            }
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            SendInteractMessage(player);
            return true;
        }

        protected virtual void SendInteractMessage(GamePlayer player)
        {
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameMerchant.GetExamineMessages.BuyItemsFor", this.Name, Currency.ToText()), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (item == null) return false;
            var receivedCurrency = Currency.Item(item.Template.Id_nb);
            currencyExchangeRates.TryGetValue(receivedCurrency, out var fromCurrencyRate);
            currencyExchangeRates.TryGetValue(Currency, out var toCurrencyRate);

            if (source is GamePlayer player && receivedCurrency.Equals(Currency) == false
                && fromCurrencyRate > 0 && toCurrencyRate > 0)
            {
                var fromCurrencyItemCount = item.Count;
                var toCurrencyItemCount = fromCurrencyItemCount * fromCurrencyRate / toCurrencyRate;

                if (toCurrencyItemCount > 0)
                {
                    defaultCurrencyItems.TryGetValue(Currency, out var defaultCurrencyItem);
                    InventoryItem newItem = GameInventoryItem.Create(defaultCurrencyItem);
                    newItem.OwnerID = player.InternalID;
                    newItem.Count = toCurrencyItemCount;

                    if (!player.Inventory.AddTemplate(newItem, newItem.Count, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                        player.CreateItemOnTheGround(newItem);

                    var playerItem = player.Inventory.GetItem((eInventorySlot)item.SlotPosition);
                    playerItem.Count -= toCurrencyItemCount * toCurrencyRate / fromCurrencyRate;

                    if (playerItem.Count < 1)
                        player.Inventory.RemoveItem(item);

                    return true;
                }
            }
            return base.ReceiveItem(source, item);
        }
    }
    [Obsolete("This is going to be removed. See GameItemCurrencyMerchant's obsolete message for more details.")]
    public class GameBountyMerchant : GameItemCurrencyMerchant
    {
        protected static Dictionary<string, int> exchangeRatesToBountyPoints { get; private set; } = null;
        protected override Currency Currency => Currency.BountyPoints;

        static GameBountyMerchant()
        {
            LoadExchangeRates();
        }


        private static void LoadExchangeRates()
        {
            if (ServerProperties.Properties.BP_EXCHANGE_ALLOW)
            {
                foreach (string sCurrencyValue in ServerProperties.Properties.BP_EXCHANGE_VALUES.Split(';'))
                {
                    string[] asVal = sCurrencyValue.Split('|');

                    if (asVal.Length > 1 && int.TryParse(asVal[1], out int currencyValue) && currencyValue > 0)
                    {
                        if (exchangeRatesToBountyPoints == null)
                            exchangeRatesToBountyPoints = new Dictionary<string, int>(1);

                        exchangeRatesToBountyPoints[asVal[0]] = currencyValue;
                    }
                }
            }
        }

        /// <summary>
        /// Exchange special currency for merchant currency type
        /// </summary>
        /// <param name="source"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (source is GamePlayer player && item != null && exchangeRatesToBountyPoints != null
                && exchangeRatesToBountyPoints.TryGetValue(item.Id_nb, out int value) && value > 0)
            {
                player.GainBountyPoints(item.Count * value);
                player.Inventory.RemoveItem(item);
                return true;
            }
            return base.ReceiveItem(source, item);
        }
    }

    [Obsolete("This is going to be removed. See GameItemCurrencyMerchant's obsolete message for more details.")]
    public class GameBloodSealsMerchant : GameItemCurrencyMerchant
    {
        protected override Currency Currency
            => Currency.Item("BloodSeal", "Blood Seals");
    }

    [Obsolete("This is going to be removed. See GameItemCurrencyMerchant's obsolete message for more details.")]
    public class GameDiamondSealsMerchant : GameItemCurrencyMerchant
    {
        protected override Currency Currency
            => Currency.Item("DiamondSeal", "Diamond Seals");
    }

    [Obsolete("This is going to be removed. See GameItemCurrencyMerchant's obsolete message for more details.")]
    public class GameSapphireSealsMerchant : GameItemCurrencyMerchant
    {
        protected override Currency Currency
            => Currency.Item("SapphireSeal", "Sapphire Seals");
    }

    [Obsolete("This is going to be removed. See GameItemCurrencyMerchant's obsolete message for more details.")]
    public class GameEmeraldSealsMerchant : GameItemCurrencyMerchant
    {
        protected override Currency Currency
            => Currency.Item("EmeraldSeal", "Emerald Seals");
    }

    [Obsolete("This is going to be removed. See GameItemCurrencyMerchant's obsolete message for more details.")]
    public class GameAuruliteMerchant : GameItemCurrencyMerchant
    {
        protected override Currency Currency
            => Currency.Item("aurulite", "units of aurulite");
    }

    [Obsolete("This is going to be removed. See GameItemCurrencyMerchant's obsolete message for more details.")]
    public class GameAtlanteanGlassMerchant : GameItemCurrencyMerchant
    {
        protected override Currency Currency
            => Currency.Item("atlanteanglass", "Atlantean Glass");
    }


    [Obsolete("This is going to be removed. See GameItemCurrencyMerchant's obsolete message for more details.")]
    public class GameDragonMerchant : GameItemCurrencyMerchant
    {
        protected override Currency Currency
            => Currency.Item("dragonscales", "Dragon Scales");
    }
}