using AmteScripts.PvP.Rewards;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;

namespace DOL.GS.Scripts
{
    public class LootGeneratorRewardScrolls : LootGeneratorBase
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        // ==============================================================================
        // TIER LISTS: Add your database 'Id_nb' strings here for your custom items /// TODO: add subtiers as well !
        // ==============================================================================
        private static readonly List<string> _rvrFinestIds = new List<string> { "arthur_parch_spelltest35", "arthur_parch_spelltest36" };
        private static readonly List<string> _pvpTier1Ids = new List<string> { "arthur_parch_spelltest37", "arthur_parch_spelltest38" };
        private static readonly List<string> _pvpTier2Ids = new List<string> { "arthur_parch_spelltest39", "arthur_parch_spelltest40" };
        private static readonly List<string> _pvpTier3Ids = new List<string> { "arthur_parch_spelltest41", "arthur_parch_spelltest42" };

        private readonly ItemTemplate[] _rvrFinestItems;
        private readonly ItemTemplate[] _pvpTier1Items;
        private readonly ItemTemplate[] _pvpTier2Items;
        private readonly ItemTemplate[] _pvpTier3Items;

        public LootGeneratorRewardScrolls() : base()
        {
            try
            {
                _rvrFinestItems = LoadItemsFromDB(_rvrFinestIds);
                _pvpTier1Items = LoadItemsFromDB(_pvpTier1Ids);
                _pvpTier2Items = LoadItemsFromDB(_pvpTier2Ids);
                _pvpTier3Items = LoadItemsFromDB(_pvpTier3Ids);
            }
            catch (Exception e)
            {
                log.Error("Error loading Scroll/Buff items from Database in LootGeneratorRewardScrolls: ", e);
            }
        }

        private ItemTemplate[] LoadItemsFromDB(List<string> ids)
        {
            var items = new List<ItemTemplate>();
            foreach (var id in ids)
            {
                var item = GameServer.Database.SelectObject<ItemTemplate>(DB.Column("Id_nb").IsEqualTo(id));
                if (item != null)
                {
                    items.Add(item);
                }
                else
                {
                    log.Warn($"[Reward System] Scroll Item ID '{id}' was not found in the ItemTemplate database.");
                }
            }
            return items.ToArray();
        }

        public override LootList GenerateLoot(GameObject mob, GameObject killer)
        {
            LootList loot = base.GenerateLoot(mob, killer);

            if (mob is RewardChest chest && killer is GamePlayer player)
            {
                ItemTemplate[] itemPool = null;

                switch (chest.Tier)
                {
                    case eRewardTier.RvRFinest:
                        itemPool = _rvrFinestItems;
                        break;
                    case eRewardTier.PvPTier1:
                        itemPool = _pvpTier1Items;
                        break;
                    case eRewardTier.PvPTier2:
                        itemPool = _pvpTier2Items;
                        break;
                    case eRewardTier.PvPTier3:
                        itemPool = _pvpTier3Items;
                        break;
                }

                if (itemPool != null && itemPool.Length > 0)
                {
                    List<ItemTemplate> unownedItems = new List<ItemTemplate>();
                    foreach (var item in itemPool)
                    {
                        int count = player.Inventory.CountItemTemplate(item.Id_nb, eInventorySlot.MinEquipable, eInventorySlot.PlayerPaperDoll);

                        if (count == 0)
                        {
                            unownedItems.Add(item);
                        }
                    }

                    ItemTemplate selectedItem;

                    if (unownedItems.Count > 0)
                    {
                        selectedItem = unownedItems[Util.Random(0, unownedItems.Count - 1)];
                    }
                    else
                    {
                        selectedItem = itemPool[Util.Random(0, itemPool.Length - 1)];
                    }

                    loot.AddFixed(selectedItem, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Lootgenerator.RewardChests.YouGetScroll", selectedItem.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    log.Warn($"[Reward System] The Magic Scrolls chest is empty! No valid items configured for tier {chest.Tier}.");
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Lootgenerator.RewardChests.ChestEmpty"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }

            return loot;
        }
    }
}