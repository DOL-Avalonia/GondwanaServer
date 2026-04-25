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
using AmteScripts.PvP.CTF;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;
using System;
using System.Collections.Generic;
using static AmteScripts.PvP.PvPScore;

namespace DOL.GS
{

    /// <summary>
    /// A vault.
    /// </summary>
    /// <author>Aredhel, Tolakram</author>
    public class GameVault : GameStaticItem, IGameInventoryObject
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// This list holds all the players that are currently viewing
        /// the vault; it is needed to update the contents of the vault
        /// for any one observer if there is a change.
        /// </summary>
        protected readonly Dictionary<string, GamePlayer> _observers = new Dictionary<string, GamePlayer>();

        /// <summary>
        /// Number of items a single vault can hold.
        /// </summary>
        private const int VAULT_SIZE = 40;

        protected int m_vaultIndex;

        /// <summary>
        /// This is used to synchronize actions on the vault.
        /// </summary>
        protected object m_vaultSync = new object();

        public object LockObject()
        {
            return m_vaultSync;
        }

        /// <summary>
        /// Index of this vault.
        /// </summary>
        public int Index
        {
            get { return m_vaultIndex; }
            set { m_vaultIndex = value; }
        }

        /// <summary>
        /// Gets the number of items that can be held in the vault.
        /// </summary>
        public virtual int VaultSize
        {
            get { return VAULT_SIZE; }
        }

        /// <summary>
        /// What is the first client slot this inventory object uses? This is client window dependent, and for 
        /// housing vaults we use the housing vault window
        /// </summary>
        public virtual int FirstClientSlot
        {
            get { return (int)eInventorySlot.FirstVault; }
        }

        /// <summary>
        /// Last slot of the client window that shows this inventory
        /// </summary>
        public virtual int LastClientSlot
        {
            get { return (int)eInventorySlot.LastVault; }
        }

        /// <summary>
        /// First slot in the DB.
        /// </summary>
        public virtual int FirstDBSlot
        {
            get { return (int)(eInventorySlot.HouseVault_First) + VaultSize * Index; }
        }

        /// <summary>
        /// Last slot in the DB.
        /// </summary>
        public virtual int LastDBSlot
        {
            get { return (int)(eInventorySlot.HouseVault_First) + VaultSize * (Index + 1) - 1; }
        }

        public virtual string GetOwner(GamePlayer player = null)
        {
            if (player == null)
            {
                log.Error("GameVault GetOwner(): player cannot be null!");
                return "PlayerIsNullError";
            }

            return player.InternalID;
        }

        /// <summary>
        /// Do we handle a search?
        /// </summary>
        public bool SearchInventory(GamePlayer player, MarketSearch.SearchData searchData)
        {
            return false; // not applicable
        }

        /// <summary>
        /// Inventory for this vault.
        /// </summary>
        public virtual Dictionary<int, InventoryItem> GetClientInventory(GamePlayer player)
        {
            var inventory = new Dictionary<int, InventoryItem>();
            int slotOffset = -FirstDBSlot + FirstClientSlot;
            foreach (InventoryItem item in DBItems(player))
            {
                if (item != null)
                {
                    if (!inventory.ContainsKey(item.SlotPosition + slotOffset))
                    {
                        inventory.Add(item.SlotPosition + slotOffset, GameInventoryItem.Create(item) ?? item);
                    }
                    else
                    {
                        log.ErrorFormat("GAMEVAULT: Duplicate item {0}, owner {1}, position {2}", item.Name, item.OwnerID, (item.SlotPosition + slotOffset));
                    }
                }
            }

            return inventory;
        }

        /// <summary>
        /// Player interacting with this vault.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            if (!CanView(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameVault.NoPermissionView"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.ActiveInventoryObject != null)
            {
                player.ActiveInventoryObject.RemoveObserver(player);
            }

            player.ActiveInventoryObject = this;
            player.Out.SendInventoryItemsUpdate(GetClientInventory(player), eInventoryWindowType.HouseVault);

            return true;
        }

        /// <inheritdoc />
        public bool IsVaultInventorySlot(ushort clientSlot)
        {
            return clientSlot >= FirstClientSlot && clientSlot <= LastClientSlot;
        }

        /// <summary>
        /// List of items in the vault.
        /// </summary>
        public virtual IList<InventoryItem> DBItems(GamePlayer player = null)
        {
            var filterBySlot = DB.Column(nameof(InventoryItem.SlotPosition)).IsGreaterOrEqualTo(FirstDBSlot).And(DB.Column(nameof(InventoryItem.SlotPosition)).IsLessOrEqualTo(LastDBSlot));
            return DOLDB<InventoryItem>.SelectObjects(DB.Column(nameof(InventoryItem.OwnerID)).IsEqualTo(GetOwner(player)).And(filterBySlot));
        }

        /// <summary>
        /// Is this a move request for a vault?
        /// </summary>
        /// <param name="player"></param>
        /// <param name="fromSlot"></param>
        /// <param name="toSlot"></param>
        /// <returns></returns>
        public virtual bool CanHandleMove(GamePlayer player, ushort fromSlot, ushort toSlot)
        {
            if (player == null || player.ActiveInventoryObject != this)
                return false;

            // House Vaults and GameConsignmentMerchant Merchants deliver the same slot numbers
            return IsVaultInventorySlot(fromSlot) || IsVaultInventorySlot(toSlot);
        }

        /// <summary>
        /// Move an item from, to or inside a house vault.  From IGameInventoryObject
        /// </summary>
        public virtual bool MoveItem(GamePlayer player, ushort fromSlot, ushort toSlot, ushort count)
        {
            if (fromSlot == toSlot)
            {
                return false;
            }

            bool fromHousing = IsVaultInventorySlot(fromSlot);
            bool toHousing = IsVaultInventorySlot(toSlot);

            if (fromHousing == false && toHousing == false)
            {
                return false;
            }

            GameVault gameVault = player.ActiveInventoryObject as GameVault;
            if (gameVault == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameVault.NotViewingVault"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                player.Out.SendInventoryItemsUpdate(null);
                return false;
            }

            InventoryItem itemInFromSlot = player.Inventory.GetItem((eInventorySlot)fromSlot);
            InventoryItem itemInToSlot = player.Inventory.GetItem((eInventorySlot)toSlot);

            if (toHousing && !gameVault.CanAddItem(player, itemInFromSlot))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameVault.NoPermissionAdd"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (fromHousing && !gameVault.CanRemoveItem(player, itemInToSlot))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameVault.NoPermissionRemove"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Check for a swap to get around not allowing non-tradables in a housing vault - Tolakram
            if (fromHousing && !CanHoldItem(itemInToSlot))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameVault.CannotSwapUntradable"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                log.DebugFormat("GameVault: {0} attempted to swap untradable item {2} with {1}", player.Name, itemInFromSlot.Name, itemInToSlot.Name);
                player.Out.SendInventoryItemsUpdate(null);
                return false;
            }

            // Allow people to get untradables out of their house vaults (old bug) but 
            // block placing untradables into housing vaults from any source - Tolakram
            if (toHousing && !CanHoldItem(itemInFromSlot))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameVault.CannotPutUntradable"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                player.Out.SendInventoryItemsUpdate(null);
                return false;
            }

            // let's move it

            lock (m_vaultSync)
            {
                this.NotifyPlayers(this, player, _observers, this.MoveItem(player, (eInventorySlot)fromSlot, (eInventorySlot)toSlot, count));
            }

            return true;
        }

        /// <inheritdoc />
        public virtual bool AddItem(GamePlayer player, InventoryItem item, bool quiet = false)
        {
            if (!CanAddItem(player, item))
            {
                if (!quiet)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameVault.NoPermissionAdd"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                return false;
            }

            var updated = GameInventoryObjectExtensions.AddItem(this, player, item);
            if (updated.Count == 0)
            {
                if (!quiet)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameVault.VaultFull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                return false;
            }

            lock (m_vaultSync)
            {
                this.NotifyPlayers(this, player, _observers, updated);
            }
            return true;
        }

        /// <summary>
        /// Whether a vault has the ability to hold the item at any point (not counting e.g. current vault space)
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public virtual bool CanHoldItem(InventoryItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item is StorageBagItem || item is FlagInventoryItem || item is PvPTreasure || item is AmteScripts.Managers.TerritoryRelicInventoryItem)
            {
                return false;
            }

            if (!item.IsTradable) // Prevent storing untradable items
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Add an item to this object
        /// </summary>
        public virtual bool OnAddItem(GamePlayer player, InventoryItem item)
        {
            return true;
        }

        /// <summary>
        /// Remove an item from this object
        /// </summary>
        public virtual bool OnRemoveItem(GamePlayer player, InventoryItem item)
        {
            return true;
        }


        /// <summary>
        /// Not applicable for vaults
        /// </summary>
        public virtual bool SetSellPrice(GamePlayer player, ushort clientSlot, uint price)
        {
            return true;
        }


        /// <summary>
        /// Send inventory updates to all players actively viewing this vault;
        /// players that are too far away will be considered inactive.
        /// </summary>
        /// <param name="updateItems"></param>
        protected virtual void NotifyObservers(GamePlayer player, IDictionary<int, InventoryItem> updateItems)
        {
            player.Client.Out.SendInventoryItemsUpdate(updateItems, eInventoryWindowType.Update);
        }

        /// <summary>
        /// Whether or not this player can view the contents of this vault.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public virtual bool CanView(GamePlayer player)
        {
            return true;
        }

        /// <summary>
        /// Whether or not this player can move items inside the vault
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public virtual bool CanAddItem(GamePlayer player, InventoryItem item)
        {
            return true;
        }

        /// <summary>
        /// Whether or not this player can move items inside the vault
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public virtual bool CanRemoveItem(GamePlayer player, InventoryItem item)
        {
            return true;
        }

        public virtual void AddObserver(GamePlayer player)
        {
            if (_observers.ContainsKey(player.Name) == false)
            {
                _observers.Add(player.Name, player);
            }
        }

        public virtual void RemoveObserver(GamePlayer player)
        {
            if (_observers.ContainsKey(player.Name))
            {
                _observers.Remove(player.Name);
            }
        }
    }
}