using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using DOL.GS.ServerProperties;

namespace DOL.GS.Scripts
{
    /// <summary>
    /// StorageBagItem : base class for a portable bag
    ///
    /// Able to be extended into a bag that only holds certain items, simply override CanHoldItem
    /// </summary>
    public class StorageBagItem : GameInventoryItem
    {
        private int _cachedFilledSlots = -1;
        public StorageBagItem()
            : base()
        {
        }

        public StorageBagItem(ItemTemplate template)
            : base(template)
        {
        }

        public StorageBagItem(InventoryItem item)
            : base(item)
        {
            OwnerID = item.OwnerID;
            ObjectId = item.ObjectId;
        }

        public StorageBagVault BagVault { get; protected set; }

        /// <summary>
        /// Whether a bag has the ability to hold the item at any point (not counting e.g. current bag space)
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public virtual bool CanHoldItem(InventoryItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item is StorageBagItem) // Prevent storing bags in bags
            {
                return false;
            }

            if (this.IsTradable && !item.IsTradable) // Prevent workaround untradeable items
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Use(GamePlayer player)
        {
            StorageBagVault vault = new StorageBagVault(player, this);
            player.ActiveInventoryObject = vault;
            player.Out.SendInventoryItemsUpdate(vault.GetClientInventory(player), eInventoryWindowType.PlayerVault);
            return (true);
        }

        /// <inheritdoc />
        public override void OnReceive(GamePlayer player)
        {
            BagVault = new StorageBagVault(player, this);

            base.OnReceive(player);
        }

        /// <inheritdoc />
        public override void OnLose(GamePlayer player)
        {
            BagVault = null;

            base.OnLose(player);
        }

        /// <summary>
        /// Gets the amount of slots actively filled in the bag's vault.
        /// </summary>
        public int GetFilledSlotsCount()
        {
            if (_cachedFilledSlots == -1)
            {
                if (string.IsNullOrEmpty(ObjectId))
                    return 0;

                _cachedFilledSlots = GameServer.Database.SelectObjects<InventoryItem>(DB.Column("OwnerID").IsEqualTo(ObjectId)
                    .And(DB.Column("SlotPosition").IsGreaterOrEqualTo((int)eInventorySlot.HouseVault_First))
                    .And(DB.Column("SlotPosition").IsLessOrEqualTo((int)eInventorySlot.HouseVault_Last))).Count;
            }

            return _cachedFilledSlots;
        }

        /// <summary>
        /// Retrieves the extra weight dynamically based on slots filled in the database.
        /// </summary>
        public int GetAdditionalWeight()
        {
            return GetFilledSlotsCount() * Properties.STORAGE_BAG_WEIGHT_PER_SLOT;
        }

        /// <summary>
        /// Invalidates the cache so the DB is re-queried next time encumbrance is checked.
        /// </summary>
        public void InvalidateWeightCache()
        {
            _cachedFilledSlots = -1;
        }
    }

    public class IngredientsBag : StorageBagItem
    {
        public IngredientsBag()
            : base()
        {
        }

        public IngredientsBag(ItemTemplate template)
            : base(template)
        {
        }

        public IngredientsBag(InventoryItem item)
            : base(item)
        {
            OwnerID = item.OwnerID;
            ObjectId = item.ObjectId;
        }

        /// <inheritdoc />
        /*public override void OnReceive(GamePlayer player)
        {
            base.OnReceive(player);

            GameEventMgr.AddHandler(player, GamePlayerEvent.ReceiveItem, PlayerReceivesItem);
        }

        /// <inheritdoc />
        public override void OnLose(GamePlayer player)
        {
            base.OnLose(player);

            GameEventMgr.RemoveHandler(player, GamePlayerEvent.ReceiveItem, PlayerReceivesItem);
        }

        protected void PlayerReceivesItem(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GamePlayer player || args is not ReceiveItemEventArgs eventArgs)
                return;

            if (CanHoldItem(eventArgs.Item))
            {
                var allItems = BagVault.GetClientInventory(player);
                if (allItems.Count >= BagVault.VaultSize)
                {
                    return;
                }
                for (int slot = BagVault.FirstClientSlot; slot < BagVault.LastClientSlot; ++slot)
                {
                    if (!allItems.ContainsKey(slot))
                    {
                        BagVault.DoMoveItem(player, eventArgs.Item.SlotPosition, );
                    }
                }
            }
        }*/

        /// <inheritdoc />
        public override bool CanHoldItem(InventoryItem item)
        {
            if (!base.CanHoldItem(item))
                return false;

            bool isStandardIngredientPackage = item.PackageID is "craft_ingredient" or "Bountyrecipe" or "combine_ingredient" or "loot_ingredient";
            bool isCraftRealmUpdateAllowed = item.PackageID == "craft_realm_update" && (item.Item_Type == 24 || item.Item_Type == 40);

            return isStandardIngredientPackage || isCraftRealmUpdateAllowed;
        }
    }

    public class StorageBagVault : GameVault
    {
        private StorageBagItem BagItem { get; init; }

        public StorageBagVault(GamePlayer player, StorageBagItem item)
        {
            Name = item.Name;
            BagItem = item;
        }

        public override bool Interact(GamePlayer player)
        {
            if (player.ActiveInventoryObject != null)
            {
                player.ActiveInventoryObject.RemoveObserver(player);
            }

            AddObserver(player);

            player.ActiveInventoryObject = this;
            player.Out.SendInventoryItemsUpdate(GetClientInventory(player), eInventoryWindowType.HouseVault);
            return true;
        }

        /// <inheritdoc />
        public override bool CanAddItem(GamePlayer player, InventoryItem item)
        {
            return BagItem.CanHoldItem(item);
        }

        public override bool CanHandleMove(GamePlayer player, ushort fromSlot, ushort toSlot)
        {
            if (player == null || player.ActiveInventoryObject != this)
                return false;

            if (fromSlot >= (ushort)eInventorySlot.FirstVault && fromSlot <= (ushort)eInventorySlot.LastVault)
                return true;

            if (toSlot >= (ushort)eInventorySlot.FirstVault && toSlot <= (ushort)eInventorySlot.LastVault)
                return true;

            return false;
        }

        public override bool MoveItem(GamePlayer player, ushort fromSlot, ushort toSlot, ushort count)
        {
            if (fromSlot == toSlot)
            {
                return false;
            }

            bool fromVault = IsVaultInventorySlot(fromSlot);
            bool toVault = IsVaultInventorySlot(toSlot);

            if (fromVault == false && toVault == false)
            {
                return false;
            }

            StorageBagVault gameVault = player.ActiveInventoryObject as StorageBagVault;
            if (gameVault == null)
            {
                player.SendTranslatedMessage("Items.Specialitems.StorageBag.NoBag", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                player.Out.SendInventoryItemsUpdate(null);
                return false;
            }

            if (toVault)
            {
                if (!gameVault.CanAddItem(player, player.Inventory.GetItem((eInventorySlot)fromSlot)))
                {
                    player.SendTranslatedMessage("Items.Specialitems.StorageBag.BadItem", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            DoMoveItem(player, fromSlot, toSlot, count);

            return true;
        }

        public override bool OnAddItem(GamePlayer player, InventoryItem item)
        {
            BagItem.InvalidateWeightCache();
            player.UpdateEncumberance();
            return base.OnAddItem(player, item);
        }

        public override bool OnRemoveItem(GamePlayer player, InventoryItem item)
        {
            BagItem.InvalidateWeightCache();
            player.UpdateEncumberance();
            return base.OnRemoveItem(player, item);
        }

        public void DoMoveItem(GamePlayer player, ushort fromSlot, ushort toSlot, ushort count)
        {
            lock (m_vaultSync)
            {
                this.NotifyPlayers(this, player, _observers, this.MoveItem(player, (eInventorySlot)fromSlot, (eInventorySlot)toSlot, count));
            }

            BagItem.InvalidateWeightCache();
            player.UpdateEncumberance();
        }

        /// <inheritdoc />
        public override string GetOwner(GamePlayer player = null)
        {
            return BagItem.ObjectId;
        }

        /// <summary>
        /// List of items in the vault.
        /// </summary>
        public override IList<InventoryItem> DBItems(GamePlayer player = null)
        {
            return GameServer.Database.SelectObjects<InventoryItem>(DB.Column("OwnerID").IsEqualTo(BagItem.ObjectId).And(DB.Column("SlotPosition").IsGreaterOrEqualTo(FirstDBSlot).And(DB.Column("SlotPosition").IsLessOrEqualTo(LastDBSlot))));
        }
    }
}
