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
 */

using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    /// <summary>
    /// Interface for a GameInventoryObject
    /// </summary>
    public interface IGameInventoryObject
    {
        object LockObject();
        int FirstClientSlot { get; }
        int LastClientSlot { get; }
        int FirstDBSlot { get; }
        int LastDBSlot { get; }
        string GetOwner(GamePlayer player);
        IEnumerable<InventoryItem> DBItems(GamePlayer player);
        Dictionary<int, InventoryItem> GetClientInventory(GamePlayer player);

        /// <summary>
        /// Whether a move request is to be handled by this inventory
        ///
        /// If this returns false, the move is handled by the default handler in PlayerMoveItemRequestHandler
        /// </summary>
        /// <param name="player"></param>
        /// <param name="fromClientSlot"></param>
        /// <param name="toClientSlot"></param>
        /// <returns></returns>
        bool CanHandleMove(GamePlayer player, ushort fromClientSlot, ushort toClientSlot);

        /// <summary>
        /// Do the move between inventories
        /// </summary>
        /// <param name="player"></param>
        /// <param name="fromClientSlot"></param>
        /// <param name="toClientSlot"></param>
        /// <param name="itemCount"></param>
        /// <returns></returns>
        bool MoveItem(GamePlayer player, ushort fromClientSlot, ushort toClientSlot, ushort itemCount);

        /// <summary>
        /// Add an item to an empty slot
        /// </summary>
        /// <param name="player"></param>
        /// <param name="item"></param>
        /// <param name="quiet"></param>
        /// <returns></returns>
        bool AddItem(GamePlayer player, InventoryItem item, bool quiet = false);

        bool IsVaultInventorySlot(ushort clientSlot);

        bool OnAddItem(GamePlayer player, InventoryItem item);
        bool OnRemoveItem(GamePlayer player, InventoryItem item);
        bool SetSellPrice(GamePlayer player, ushort clientSlot, uint sellPrice);
        bool SearchInventory(GamePlayer player, MarketSearch.SearchData searchData);
        void AddObserver(GamePlayer player);
        void RemoveObserver(GamePlayer player);
    }

    /// <summary>
    /// Extension class for GameInventoryObject.
    /// </summary>
    public static class GameInventoryObjectExtensions
    {
        public static bool CanHandleRequest(this IGameInventoryObject thisObject, ushort fromClientSlot, ushort toClientSlot)
        {
            return (fromClientSlot >= thisObject.FirstClientSlot && fromClientSlot <= thisObject.LastClientSlot) || (toClientSlot >= thisObject.FirstClientSlot && toClientSlot <= thisObject.LastClientSlot);
        }

        public static Dictionary<int, InventoryItem> GetClientItems(this IGameInventoryObject thisObject, GamePlayer player)
        {
            Dictionary<int, InventoryItem> inventory = new Dictionary<int, InventoryItem>();
            int slotOffset = thisObject.FirstClientSlot - thisObject.FirstDBSlot;

            foreach (InventoryItem item in thisObject.DBItems(player))
            {
                if (item != null && !inventory.ContainsKey(item.SlotPosition + slotOffset))
                    inventory.Add(item.SlotPosition + slotOffset, item);
            }

            return inventory;
        }

        public static IDictionary<int, InventoryItem> MoveItem(this IGameInventoryObject thisObject, GamePlayer player, eInventorySlot fromClientSlot, eInventorySlot toClientSlot, ushort count)
        {
            lock (thisObject.LockObject())
            {
                if (!GetItemInSlot(fromClientSlot, out InventoryItem fromItem))
                {
                    SendUnsupportedActionMessage(player);
                    return null;
                }

                GetItemInSlot(toClientSlot, out InventoryItem toItem);
                IDictionary<int, InventoryItem> updatedItems = MoveItemInner(fromItem, toItem);
                return updatedItems;
            }

            bool GetItemInSlot(eInventorySlot slot, out InventoryItem item)
            {
                item = null;

                if (thisObject.IsVaultInventorySlot((ushort)slot))
                    thisObject.GetClientInventory(player).TryGetValue((int)slot, out item);
                else
                    item = player.Inventory.GetItem(slot);

                return item != null;
            }

            IDictionary<int, InventoryItem> MoveItemInner(InventoryItem fromItem, InventoryItem toItem)
            {
                Dictionary<int, InventoryItem> updatedItems = new Dictionary<int, InventoryItem>(2);

                // Blood Vials Combination across all Inventory Objects (bags, vaults, etc.)
                if (fromItem != null && toItem != null &&
                    fromItem.Id_nb != null && fromItem.Id_nb.StartsWith("vt_") &&
                    toItem.Id_nb != null && toItem.Id_nb.StartsWith("vt_"))
                {
                    if (LootGeneratorBloodVials.CombineVials(player, fromItem, toItem))
                    {
                        if (thisObject.IsVaultInventorySlot((ushort)fromClientSlot))
                        {
                            thisObject.GetClientInventory(player).TryGetValue((int)fromClientSlot, out InventoryItem refreshedFromItem);
                            updatedItems[(int)fromClientSlot] = refreshedFromItem;
                        }

                        if (thisObject.IsVaultInventorySlot((ushort)toClientSlot))
                        {
                            thisObject.GetClientInventory(player).TryGetValue((int)toClientSlot, out InventoryItem refreshedToItem);
                            updatedItems[(int)toClientSlot] = refreshedToItem ?? toItem;
                        }

                        return updatedItems;
                    }
                }

                if (toItem == null)
                    MoveItemToEmptySlot(thisObject, player, fromClientSlot, toClientSlot, fromItem, count, updatedItems);
                else if (toItem.IsStackable && fromItem.Count <= toItem.MaxCount && toItem.Count < toItem.MaxCount && toItem.Name.Equals(fromItem.Name))
                {
                    // `count` is inconsistent here.
                    // With account vaults, it seems to always be 0, so we can treat it as an error if it isn't.
                    // With consignment merchants, it takes the stack's size, but stacking / splitting is disallowed anyway.
                    // Others... ?
                    if (count != 0)
                    {
                        SendUnsupportedActionMessage(player);
                        return updatedItems;
                    }

                    StackItems(thisObject, player, fromClientSlot, toClientSlot, fromItem, toItem, count, updatedItems);
                }
                else
                    SwitchItems(thisObject, player, fromClientSlot, toClientSlot, fromItem, toItem, updatedItems);

                return updatedItems;
            }
        }

        public static void NotifyPlayers(this IGameInventoryObject thisObject, GameObject thisOwner, GamePlayer player, Dictionary<string, GamePlayer> observers, IDictionary<int, InventoryItem> updatedItems)
        {
            List<string> inactiveList = new();
            Dictionary<int, InventoryItem> updatedItemsForObservers = null;
            bool playerNotified = false;

            // Prepare a new list for observers so that we don't update their inventories.
            if (updatedItems != null)
            {
                updatedItemsForObservers = new(2);

                foreach (var updateItem in updatedItems)
                {
                    if (updateItem.Key >= thisObject.FirstClientSlot && updateItem.Key <= thisObject.LastClientSlot)
                        updatedItemsForObservers[updateItem.Key] = updateItem.Value;
                }
            }

            // Send updates to observers.
            foreach (GamePlayer observer in observers.Values)
            {
                if (observer.ActiveInventoryObject != thisObject)
                {
                    inactiveList.Add(observer.Name);
                    continue;
                }

                if (!thisOwner.IsWithinRadius(observer, WorldMgr.INFO_DISTANCE))
                {
                    observer.ActiveInventoryObject = null;
                    inactiveList.Add(observer.Name);

                    continue;
                }

                if (player == observer)
                {
                    if (updatedItems != null)
                    {
                        player.Client.Out.SendInventoryItemsUpdate(updatedItems, eInventoryWindowType.Update);
                        playerNotified = true;
                    }
                }
                else if (updatedItemsForObservers != null)
                    observer.Client.Out.SendInventoryItemsUpdate(updatedItemsForObservers, eInventoryWindowType.Update);
            }

            // Happens if the player wasn't added to the observers.
            if (!playerNotified)
                player.Client.Out.SendInventoryItemsUpdate(updatedItems, eInventoryWindowType.Update);

            // Remove inactive observers.
            foreach (string observerName in inactiveList)
                observers.Remove(observerName);
        }

        public static IDictionary<int, InventoryItem> AddItem(this IGameInventoryObject thisObject, GamePlayer player, InventoryItem item)
        {
            int FindEmptySlot()
            {
                var vaultItems = thisObject.DBItems(player);
                bool[] hasItem = new bool[thisObject.LastDBSlot - thisObject.FirstDBSlot + 1];
                foreach (InventoryItem itemInVault in vaultItems)
                {
                    if (itemInVault.SlotPosition >= thisObject.FirstDBSlot && itemInVault.SlotPosition <= thisObject.LastDBSlot)
                    {
                        hasItem[itemInVault.SlotPosition - thisObject.FirstDBSlot] = true;
                    }
                }
                for (int i = 0; i < hasItem.Length; ++i)
                {
                    if (!hasItem[i])
                    {
                        return thisObject.FirstDBSlot + i;
                    }
                }
                return 0;
            }

            int emptySlot = FindEmptySlot();
            if (emptySlot == 0)
            {
                return new Dictionary<int, InventoryItem>();
            }
            item.SlotPosition = emptySlot;
            item.OwnerID = thisObject.GetOwner(player);

            if (!GameServer.Database.AddObject(item))
            {
                SendErrorMessage(player, nameof(AddItem), 0, (eInventorySlot)emptySlot, item, null, (ushort)item.Count);
                return new Dictionary<int, InventoryItem>();
            }

            thisObject.OnAddItem(player, item);
            return new Dictionary<int, InventoryItem>{{ emptySlot, item }};
        }

        private static void MoveItemToEmptySlot(this IGameInventoryObject thisObject, GamePlayer player, eInventorySlot fromClientSlot, eInventorySlot toClientSlot, InventoryItem fromItem, ushort count, Dictionary<int, InventoryItem> updatedItems)
        {
            if (count == 0)
            {
                MoveWholeStack();
                return;
            }

            int fromItemCount = Math.Max(0, fromItem.Count - count);

            if (fromItemCount == 0)
            {
                MoveWholeStack();
                return;
            }

            SplitStack();

            void MoveWholeStack()
            {
                if (IsBackpackSlot(fromClientSlot))
                {
                    if (!player.Inventory.RemoveTradeItem(fromItem))
                    {
                        SendErrorMessage(player, nameof(MoveWholeStack), fromClientSlot, toClientSlot, fromItem, null, count);
                        return;
                    }

                    if (thisObject.IsVaultInventorySlot((ushort)toClientSlot))
                    {
                        fromItem.SlotPosition = (int)toClientSlot - thisObject.FirstClientSlot + thisObject.FirstDBSlot;
                        fromItem.OwnerID = thisObject.GetOwner(player);

                        if (!thisObject.OnAddItem(player, fromItem))
                        {
                            fromItem.SlotPosition = (int)fromClientSlot;
                            fromItem.OwnerID = player.InternalID;
                            player.Inventory.AddTradeItem(fromClientSlot, fromItem);
                            SendErrorMessage(player, nameof(MoveWholeStack), fromClientSlot, toClientSlot, fromItem, null, count);
                            return;
                        }
                    }
                    else
                    {
                        player.Inventory.AddTradeItem(fromClientSlot, fromItem);
                        SendUnsupportedActionMessage(player);
                        return;
                    }
                }
                else if (thisObject.IsVaultInventorySlot((ushort)fromClientSlot))
                {
                    if (thisObject.IsVaultInventorySlot((ushort)toClientSlot))
                    {
                        thisObject.OnRemoveItem(player, fromItem);
                        fromItem.SlotPosition = (int)toClientSlot - thisObject.FirstClientSlot + thisObject.FirstDBSlot;
                        fromItem.OwnerID = thisObject.GetOwner(player);
                        thisObject.OnAddItem(player, fromItem);
                    }
                    else if (IsBackpackSlot(toClientSlot))
                    {
                        if (!thisObject.OnRemoveItem(player, fromItem))
                        {
                            SendErrorMessage(player, nameof(MoveWholeStack), fromClientSlot, toClientSlot, fromItem, null, count);
                            return;
                        }

                        fromItem.SlotPosition = (int)toClientSlot;
                        fromItem.OwnerID = player.InternalID;

                        if (!player.Inventory.AddTradeItem(toClientSlot, fromItem))
                        {
                            fromItem.SlotPosition = (int)fromClientSlot - thisObject.FirstClientSlot + thisObject.FirstDBSlot;
                            fromItem.OwnerID = thisObject.GetOwner(player);
                            thisObject.OnAddItem(player, fromItem);
                            SendErrorMessage(player, nameof(MoveWholeStack), fromClientSlot, toClientSlot, fromItem, null, count);
                            return;
                        }
                    }
                    else
                    {
                        SendUnsupportedActionMessage(player);
                        return;
                    }
                }
                else
                {
                    SendUnsupportedActionMessage(player);
                    return;
                }

                if (!GameServer.Database.SaveObject(fromItem))
                {
                    if (IsBackpackSlot(fromClientSlot))
                    {
                        thisObject.OnRemoveItem(player, fromItem);
                        fromItem.SlotPosition = (int)fromClientSlot;
                        fromItem.OwnerID = player.InternalID;
                        player.Inventory.AddTradeItem(fromClientSlot, fromItem);
                    }
                    else if (thisObject.IsVaultInventorySlot((ushort)fromClientSlot))
                    {
                        if (IsBackpackSlot(toClientSlot)) player.Inventory.RemoveTradeItem(fromItem);
                        else thisObject.OnRemoveItem(player, fromItem);

                        fromItem.SlotPosition = (int)fromClientSlot - thisObject.FirstClientSlot + thisObject.FirstDBSlot;
                        fromItem.OwnerID = thisObject.GetOwner(player);
                        thisObject.OnAddItem(player, fromItem);
                    }

                    SendErrorMessage(player, nameof(MoveWholeStack), fromClientSlot, toClientSlot, fromItem, null, count);
                    return;
                }

                updatedItems.Add((int)fromClientSlot, null);
                updatedItems.Add((int)toClientSlot, fromItem);
            }

            void SplitStack()
            {
                if (thisObject.IsVaultInventorySlot((ushort)fromClientSlot))
                {
                    fromItem.Count -= count;

                    if (!GameServer.Database.SaveObject(fromItem))
                    {
                        fromItem.Count += count;
                        SendErrorMessage(player, nameof(SplitStack), fromClientSlot, toClientSlot, fromItem, null, count);
                        return;
                    }
                }
                else if (IsBackpackSlot(fromClientSlot))
                {
                    if (!player.Inventory.RemoveCountFromStack(fromItem, count))
                    {
                        SendErrorMessage(player, nameof(SplitStack), fromClientSlot, toClientSlot, fromItem, null, count);
                        return;
                    }
                }
                else
                {
                    SendUnsupportedActionMessage(player);
                    return;
                }

                InventoryItem toItem = (InventoryItem)fromItem.Clone();
                toItem.Count = count;
                toItem.AllowAdd = fromItem.Template.AllowAdd;

                if (thisObject.IsVaultInventorySlot((ushort)toClientSlot))
                {
                    toItem.SlotPosition = (int)toClientSlot - thisObject.FirstClientSlot + thisObject.FirstDBSlot;
                    toItem.OwnerID = thisObject.GetOwner(player);

                    if (!thisObject.OnAddItem(player, toItem))
                    {
                        if (thisObject.IsVaultInventorySlot((ushort)fromClientSlot))
                        {
                            fromItem.Count += count;
                            GameServer.Database.SaveObject(fromItem);
                        }
                        else player.Inventory.AddCountToStack(fromItem, count);

                        SendErrorMessage(player, nameof(SplitStack), fromClientSlot, toClientSlot, fromItem, toItem, count);
                        return;
                    }

                    if (!GameServer.Database.AddObject(toItem))
                    {
                        thisObject.OnRemoveItem(player, toItem);
                        if (thisObject.IsVaultInventorySlot((ushort)fromClientSlot))
                        {
                            fromItem.Count += count;
                            GameServer.Database.SaveObject(fromItem);
                        }
                        else player.Inventory.AddCountToStack(fromItem, count);

                        SendErrorMessage(player, nameof(SplitStack), fromClientSlot, toClientSlot, fromItem, toItem, count);
                        return;
                    }
                }
                else if (IsBackpackSlot(toClientSlot))
                {
                    toItem.SlotPosition = (int)toClientSlot;
                    toItem.OwnerID = player.InternalID;

                    if (!player.Inventory.AddItem(toClientSlot, toItem))
                    {
                        if (thisObject.IsVaultInventorySlot((ushort)fromClientSlot))
                        {
                            fromItem.Count += count;
                            GameServer.Database.SaveObject(fromItem);
                        }
                        else player.Inventory.AddCountToStack(fromItem, count);

                        SendErrorMessage(player, nameof(SplitStack), fromClientSlot, toClientSlot, fromItem, toItem, count);
                        return;
                    }
                }
                else
                {
                    SendUnsupportedActionMessage(player);
                    return;
                }

                updatedItems.Add((int)fromClientSlot, fromItem);
                updatedItems.Add((int)toClientSlot, toItem);
            }
        }

        private static void StackItems(this IGameInventoryObject thisObject, GamePlayer player, eInventorySlot fromClientSlot, eInventorySlot toClientSlot, InventoryItem fromItem, InventoryItem toItem, ushort requestedCount, Dictionary<int, InventoryItem> updatedItems)
        {
            int maxCanMove = Math.Min(fromItem.Count, toItem.MaxCount - toItem.Count);
            int countToMove = (requestedCount == 0 || requestedCount > maxCanMove) ? maxCanMove : requestedCount;

            if (countToMove <= 0) return;

            bool addedToVault = false;
            bool addedToBackpack = false;

            if (thisObject.IsVaultInventorySlot((ushort)toClientSlot))
            {
                toItem.Count += countToMove;
                try
                {
                    if (!GameServer.Database.SaveObject(toItem))
                    {
                        toItem.Count -= countToMove;
                        SendErrorMessage(player, nameof(StackItems), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                        return;
                    }
                    addedToVault = true;
                }
                catch (Exception)
                {
                    toItem.Count -= countToMove;
                    SendErrorMessage(player, nameof(StackItems), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                    return;
                }
            }
            else if (IsBackpackSlot(toClientSlot))
            {
                try
                {
                    if (!player.Inventory.AddCountToStack(toItem, countToMove))
                    {
                        SendErrorMessage(player, nameof(StackItems), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                        return;
                    }
                    addedToBackpack = true;
                }
                catch (Exception)
                {
                    SendErrorMessage(player, nameof(StackItems), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                    return;
                }
            }
            else
            {
                SendUnsupportedActionMessage(player);
                return;
            }

            bool removeSuccess = false;
            if (thisObject.IsVaultInventorySlot((ushort)fromClientSlot))
            {
                if (fromItem.Count - countToMove <= 0)
                {
                    try
                    {
                        if (GameServer.Database.DeleteObject(fromItem))
                        {
                            if (fromItem.Template is ItemUnique u && !fromItem.IsStackable) { try { if (u.IsPersisted) GameServer.Database.DeleteObject(u); } catch { } }
                            thisObject.OnRemoveItem(player, fromItem);
                            fromItem = null;
                            removeSuccess = true;
                        }
                    }
                    catch (Exception) { removeSuccess = false; }
                }
                else
                {
                    fromItem.Count -= countToMove;
                    try
                    {
                        if (GameServer.Database.SaveObject(fromItem)) removeSuccess = true;
                        else fromItem.Count += countToMove;
                    }
                    catch (Exception)
                    {
                        fromItem.Count += countToMove;
                        removeSuccess = false;
                    }
                }
            }
            else if (IsBackpackSlot(fromClientSlot))
            {
                if (fromItem.Count - countToMove <= 0)
                {
                    try
                    {
                        if (player.Inventory.RemoveItem(fromItem))
                        {
                            if (fromItem.Template is ItemUnique u && !fromItem.IsStackable) { try { if (u.IsPersisted) GameServer.Database.DeleteObject(u); } catch { } }
                            fromItem = null;
                            removeSuccess = true;
                        }
                    }
                    catch (Exception) { removeSuccess = false; }
                }
                else
                {
                    try { if (player.Inventory.RemoveCountFromStack(fromItem, countToMove)) removeSuccess = true; }
                    catch (Exception) { removeSuccess = false; }
                }
            }

            if (!removeSuccess)
            {
                if (addedToVault)
                {
                    toItem.Count -= countToMove;
                    try { GameServer.Database.SaveObject(toItem); } catch { }
                }
                else if (addedToBackpack)
                {
                    try { player.Inventory.RemoveCountFromStack(toItem, countToMove); } catch { }
                }

                SendErrorMessage(player, nameof(StackItems), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                return;
            }

            if (fromItem != null) updatedItems.Add((int)fromClientSlot, fromItem);
            else updatedItems.Add((int)fromClientSlot, null);

            updatedItems.Add((int)toClientSlot, toItem);
        }

        private static void SwitchItems(this IGameInventoryObject thisObject, GamePlayer player, eInventorySlot fromClientSlot, eInventorySlot toClientSlot, InventoryItem fromItem, InventoryItem toItem, Dictionary<int, InventoryItem> updatedItems)
        {
            if (thisObject.IsVaultInventorySlot((ushort)fromClientSlot))
            {
                if (thisObject.IsVaultInventorySlot((ushort)toClientSlot))
                {
                    int fromDBSlot = fromItem.SlotPosition;
                    int toDBSlot = toItem.SlotPosition;

                    fromItem.SlotPosition = toDBSlot;
                    toItem.SlotPosition = fromDBSlot;

                    if (!GameServer.Database.SaveObject(fromItem))
                    {
                        SendErrorMessage(player, nameof(SwitchItems), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                        return;
                    }

                    if (!GameServer.Database.SaveObject(toItem))
                    {
                        SendErrorMessage(player, nameof(SwitchItems), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                        return;
                    }

                    updatedItems.Add((int)toClientSlot, fromItem);
                    updatedItems.Add((int)fromClientSlot, toItem);
                    return;
                }

                if (IsBackpackSlot(toClientSlot))
                {
                    SwitchItemsFromOrToBackpack(fromClientSlot, toClientSlot, fromItem, toItem);
                    return;
                }

                SendUnsupportedActionMessage(player);
                return;
            }

            if (IsBackpackSlot(fromClientSlot))
            {
                if (thisObject.IsVaultInventorySlot((ushort)toClientSlot))
                {
                    SwitchItemsFromOrToBackpack(toClientSlot, fromClientSlot, toItem, fromItem);
                    return;
                }

                SendUnsupportedActionMessage(player);
                return;
            }

            SendUnsupportedActionMessage(player);

            void SwitchItemsFromOrToBackpack(eInventorySlot vaultSlot, eInventorySlot backpackSlot, InventoryItem vaultItem, InventoryItem backpackItem)
            {
                if (!thisObject.OnRemoveItem(player, vaultItem))
                {
                    SendErrorMessage(player, nameof(SwitchItemsFromOrToBackpack), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                    return;
                }

                if (!player.Inventory.RemoveTradeItem(backpackItem))
                {
                    thisObject.OnAddItem(player, vaultItem);
                    SendErrorMessage(player, nameof(SwitchItemsFromOrToBackpack), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                    return;
                }

                backpackItem.SlotPosition = vaultItem.SlotPosition;
                backpackItem.OwnerID = thisObject.GetOwner(player);
                vaultItem.SlotPosition = (int)backpackSlot;
                vaultItem.OwnerID = player.InternalID;

                if (!thisObject.OnAddItem(player, backpackItem))
                {
                    backpackItem.SlotPosition = (int)backpackSlot;
                    backpackItem.OwnerID = player.InternalID;
                    player.Inventory.AddTradeItem(backpackSlot, backpackItem);

                    vaultItem.SlotPosition = (int)vaultSlot;
                    vaultItem.OwnerID = thisObject.GetOwner(player);
                    thisObject.OnAddItem(player, vaultItem);
                    SendErrorMessage(player, nameof(SwitchItemsFromOrToBackpack), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                    return;
                }

                if (!player.Inventory.AddTradeItem(backpackSlot, vaultItem))
                {
                    thisObject.OnRemoveItem(player, backpackItem);
                    backpackItem.SlotPosition = (int)backpackSlot;
                    backpackItem.OwnerID = player.InternalID;
                    player.Inventory.AddTradeItem(backpackSlot, backpackItem);

                    vaultItem.SlotPosition = (int)vaultSlot;
                    vaultItem.OwnerID = thisObject.GetOwner(player);
                    thisObject.OnAddItem(player, vaultItem);
                    SendErrorMessage(player, nameof(SwitchItemsFromOrToBackpack), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                    return;
                }

                if (!GameServer.Database.SaveObject(backpackItem) || !GameServer.Database.SaveObject(vaultItem))
                {
                    SendErrorMessage(player, nameof(SwitchItemsFromOrToBackpack), fromClientSlot, toClientSlot, fromItem, toItem, 0);
                    return;
                }

                updatedItems.Add((int)vaultSlot, backpackItem);
                updatedItems.Add((int)backpackSlot, vaultItem);
            }
        }

        private static bool IsBackpackSlot(eInventorySlot slot)
        {
            return slot is >= eInventorySlot.FirstBackpack and <= eInventorySlot.LastBackpack;
        }

        private static void SendErrorMessage(GamePlayer player, string method, eInventorySlot fromClientSlot, eInventorySlot toClientSlot, InventoryItem fromItem, InventoryItem toItem, ushort count)
        {
            player.Out.SendMessage($"Error while moving an item in '{method}':", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage($"- [{fromItem?.Name}] [{fromClientSlot}] ({count})", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage($"- [{toItem?.Name}] [{toClientSlot}]", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage($"The item may be lost or temporarily invisible.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        private static void SendUnsupportedActionMessage(GamePlayer player)
        {
            player.Out.SendMessage("This action isn't currently supported. Try a different source or destination slot.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }
    }
}
