using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using DOL.Database;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
    public static class VaultItemCacheManager
    {
        private static readonly ConcurrentDictionary<string, VaultItemCache> _caches = new ConcurrentDictionary<string, VaultItemCache>();

        public static VaultItemCache GetCache(IGameInventoryObject vault, GamePlayer player)
        {
            string ownerId = vault.GetOwner(player);
            if (string.IsNullOrEmpty(ownerId)) return null;
            return _caches.GetOrAdd($"{ownerId}_{vault.FirstDBSlot}", id => new VaultItemCache(id));
        }

        public static void RemoveCache(string key) => _caches.TryRemove(key, out _);
    }

    public class VaultItemCache
    {
        private const int EXPIRES_AFTER = 30000;
        private readonly string _key;
        private readonly object _lock = new object();
        private Dictionary<int, InventoryItem> _items;
        private bool _isDisposed;
        private Timer _cleanupTimer;

        public object SyncRoot => _lock;

        public VaultItemCache(string key)
        {
            _key = key;
            _cleanupTimer = new Timer(OnTick, null, EXPIRES_AFTER, Timeout.Infinite);
        }

        private void ExtendTimer()
        {
            if (!_isDisposed && _cleanupTimer != null)
                try { _cleanupTimer.Change(EXPIRES_AFTER, Timeout.Infinite); } catch { }
        }

        private void OnTick(object state)
        {
            lock (_lock)
            {
                if (_isDisposed) return;
                _items = null;
                _isDisposed = true;
                if (_cleanupTimer != null) { _cleanupTimer.Dispose(); _cleanupTimer = null; }
                VaultItemCacheManager.RemoveCache(_key);
            }
        }

        public Dictionary<int, InventoryItem> GetItems(IGameInventoryObject vault, GamePlayer player)
        {
            lock (_lock)
            {
                if (_isDisposed) return VaultItemCacheManager.GetCache(vault, player)?.GetItems(vault, player);

                ExtendTimer();
                if (_items == null)
                {
                    _items = new Dictionary<int, InventoryItem>();
                    foreach (InventoryItem item in vault.DBItems(player))
                    {
                        int slotPosition = (-vault.FirstDBSlot + vault.FirstClientSlot) + item.SlotPosition;
                        _items[slotPosition] = GameInventoryItem.Create(item) ?? item;
                    }
                }
                return new Dictionary<int, InventoryItem>(_items);
            }
        }

        public void ForceValidateCache()
        {
            lock (_lock)
            {
                _items = null;
                _isDisposed = true;
                if (_cleanupTimer != null) { _cleanupTimer.Dispose(); _cleanupTimer = null; }
                VaultItemCacheManager.RemoveCache(_key);
            }
        }
    }
}