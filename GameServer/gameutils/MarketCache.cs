using System;
using System.Collections.Generic;
using DOL.Database;
using log4net;

namespace DOL.GS
{
    public static class MarketCache
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);
        private static MarketSearchEngine _searchEngine = new MarketSearchEngine();

        public static int ItemCount => _searchEngine.ItemCount;

        public static bool Initialize()
        {
            log.Info("Building Market Cache ....");
            if (_searchEngine != null)
            {
                _searchEngine.Dispose();
                _searchEngine = new MarketSearchEngine();
            }

            try
            {
                var filterBySlot = DB.Column(nameof(InventoryItem.SlotPosition)).IsGreaterOrEqualTo((int)eInventorySlot.Consignment_First)
                                     .And(DB.Column(nameof(InventoryItem.SlotPosition)).IsLessOrEqualTo((int)eInventorySlot.Consignment_Last));

                var list = DOLDB<InventoryItem>.SelectObjects(filterBySlot);

                foreach (InventoryItem item in list)
                {
                    if (string.IsNullOrEmpty(item.OwnerID)) continue;
                    InventoryItem playerItem = GameInventoryItem.Create(item) ?? item;
                    _searchEngine!.AddItem(playerItem);
                }

                log.Info($"Market Cache initialized with {_searchEngine!.ItemCount} items.");
            }
            catch (Exception ex)
            {
                log.Error("Failed to initialize Market Cache.", ex);
                return false;
            }

            return true;
        }

        public static bool AddItem(InventoryItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.OwnerID)) return false;
            InventoryItem playerItem = GameInventoryItem.Create(item) ?? item;

            if (!_searchEngine.AddItem(playerItem))
            {
                log.Error("Attempted to add duplicate item to Market Cache " + item.ObjectId);
                return false;
            }
            return true;
        }

        public static bool RemoveItem(InventoryItem item)
        {
            if (item == null) return false;
            return _searchEngine.RemoveItem(item);
        }

        public static IEnumerable<InventoryItem> SearchItems(in ItemQuery query)
        {
            return _searchEngine.Search(query);
        }
    }
}