using System;
using System.Collections.Generic;
using System.Threading;
using DOL.Database;

namespace DOL.GS
{
    public readonly record struct ItemQuery(
        int? Slot = null,
        bool? IsCrafted = null,
        bool? HasVisual = null,
        string Owner = null)
    {
        public bool HasAny => Slot.HasValue || IsCrafted.HasValue || HasVisual.HasValue || !string.IsNullOrEmpty(Owner);
    }

    public class MarketSearchEngine : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly Dictionary<InventoryItem, IndexKeys> _itemKeyCache = new Dictionary<InventoryItem, IndexKeys>();
        private readonly Dictionary<int, HashSet<InventoryItem>> _bySlot = new Dictionary<int, HashSet<InventoryItem>>();
        private readonly Dictionary<bool, HashSet<InventoryItem>> _byCrafted = new Dictionary<bool, HashSet<InventoryItem>>();
        private readonly Dictionary<bool, HashSet<InventoryItem>> _byVisual = new Dictionary<bool, HashSet<InventoryItem>>();
        private readonly Dictionary<string, HashSet<InventoryItem>> _byOwner = new Dictionary<string, HashSet<InventoryItem>>();

        public int ItemCount
        {
            get
            {
                _lock.EnterReadLock();
                try { return _itemKeyCache.Count; }
                finally { _lock.ExitReadLock(); }
            }
        }

        public bool AddItem(InventoryItem item)
        {
            _lock.EnterWriteLock();
            try
            {
                IndexKeys keys = GetIndexKeys(item);
                if (!_itemKeyCache.TryAdd(item, keys)) return false;

                AddToIndex(_bySlot, keys.Slot, item);
                AddToIndex(_byCrafted, keys.IsCrafted, item);
                AddToIndex(_byVisual, keys.HasVisual, item);
                AddToIndex(_byOwner, keys.Owner, item);
                return true;
            }
            finally { _lock.ExitWriteLock(); }
        }

        public bool RemoveItem(InventoryItem item)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_itemKeyCache.Remove(item, out IndexKeys keys)) return false;

                RemoveFromIndex(_bySlot, keys.Slot, item);
                RemoveFromIndex(_byCrafted, keys.IsCrafted, item);
                RemoveFromIndex(_byVisual, keys.HasVisual, item);
                RemoveFromIndex(_byOwner, keys.Owner, item);
                return true;
            }
            finally { _lock.ExitWriteLock(); }
        }

        public IEnumerable<InventoryItem> Search(in ItemQuery query)
        {
            _lock.EnterReadLock();
            try
            {
                List<HashSet<InventoryItem>> resultSets = GetMatchingSets(query);

                if (resultSets == null || resultSets.Count == 0)
                    return query.HasAny ? Array.Empty<InventoryItem>() : new List<InventoryItem>(_itemKeyCache.Keys);

                HashSet<InventoryItem> smallestSet = resultSets[0];
                int minCount = smallestSet.Count;

                for (int i = 1; i < resultSets.Count; i++)
                {
                    if (resultSets[i].Count < minCount)
                    {
                        minCount = resultSets[i].Count;
                        smallestSet = resultSets[i];
                    }
                }

                if (smallestSet.Count == 0) return Array.Empty<InventoryItem>();

                HashSet<InventoryItem> finalResult = new HashSet<InventoryItem>(smallestSet);

                for (int i = 0; i < resultSets.Count; i++)
                {
                    if (resultSets[i] == smallestSet) continue;
                    finalResult.IntersectWith(resultSets[i]);
                    if (finalResult.Count == 0) break;
                }

                return finalResult;
            }
            finally { _lock.ExitReadLock(); }
        }

        private static IndexKeys GetIndexKeys(InventoryItem item)
        {
            return new IndexKeys(GetClientSlot(item), item.IsCrafted, item.Effect > 0, item.OwnerID);
        }

        private static int GetClientSlot(InventoryItem item)
        {
            if (item.Item_Type == (int)eInventorySlot.TorsoArmor) return 5;
            if (item.Item_Type == (int)eInventorySlot.HeadArmor) return 1;
            if (item.Item_Type == (int)eInventorySlot.ArmsArmor) return 8;
            if (item.Item_Type == (int)eInventorySlot.HandsArmor) return 2;
            if (item.Item_Type == (int)eInventorySlot.LegsArmor) return 7;
            if (item.Item_Type == (int)eInventorySlot.FeetArmor) return 3;
            if (item.Item_Type == (int)eInventorySlot.Neck) return 9;
            if (item.Item_Type == (int)eInventorySlot.Cloak) return 6;
            if (item.Item_Type == (int)eInventorySlot.Jewellery) return 4;
            if (item.Item_Type == (int)eInventorySlot.Waist) return 12;
            if (item.Item_Type == (int)eInventorySlot.RightBracer || item.Item_Type == (int)eInventorySlot.LeftBracer) return 13;
            if (item.Item_Type == (int)eInventorySlot.RightRing || item.Item_Type == (int)eInventorySlot.LeftRing) return 15;
            if (item.Item_Type == (int)eInventorySlot.RightHandWeapon) return 100;
            if (item.Item_Type == (int)eInventorySlot.LeftHandWeapon)
            {
                if (item.Object_Type == (int)eObjectType.Shield) return 105;
                return 101;
            }
            if (item.Item_Type == (int)eInventorySlot.TwoHandWeapon) return 102;
            if (item.Item_Type == (int)eInventorySlot.DistanceWeapon)
            {
                if (item.Object_Type == (int)eObjectType.Instrument) return 104;
                return 103;
            }
            if (item.Object_Type == (int)eObjectType.GenericItem) return 106;
            return 0;
        }

        private List<HashSet<InventoryItem>> GetMatchingSets(in ItemQuery query)
        {
            List<HashSet<InventoryItem>> sets = new List<HashSet<InventoryItem>>();

            if (query.Slot.HasValue)
            {
                if (_bySlot.TryGetValue(query.Slot.Value, out var slotSet)) sets.Add(slotSet);
                else return null;
            }
            if (query.IsCrafted.HasValue)
            {
                if (_byCrafted.TryGetValue(query.IsCrafted.Value, out var craftedSet)) sets.Add(craftedSet);
                else return null;
            }
            if (query.HasVisual.HasValue)
            {
                if (_byVisual.TryGetValue(query.HasVisual.Value, out var visualSet)) sets.Add(visualSet);
                else return null;
            }
            if (!string.IsNullOrEmpty(query.Owner))
            {
                if (_byOwner.TryGetValue(query.Owner, out var ownerSet)) sets.Add(ownerSet);
                else return null;
            }
            return sets;
        }

        private static void AddToIndex<TKey>(Dictionary<TKey, HashSet<InventoryItem>> index, TKey key, InventoryItem item)
        {
            if (!index.TryGetValue(key, out var set))
            {
                set = new HashSet<InventoryItem>();
                index[key] = set;
            }
            set.Add(item);
        }

        private static void RemoveFromIndex<TKey>(Dictionary<TKey, HashSet<InventoryItem>> index, TKey key, InventoryItem item)
        {
            if (index.TryGetValue(key, out var set))
            {
                set.Remove(item);
                if (set.Count == 0) index.Remove(key);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _lock.Dispose();
        }

        private readonly record struct IndexKeys(int Slot, bool IsCrafted, bool HasVisual, string Owner);
    }
}