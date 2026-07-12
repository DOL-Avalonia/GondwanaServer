using System;
using System.Collections.Generic;
using DOL.GS;
using DOL.Database;

namespace DOL.GS
{
    public class SmartLootGenerator : LootGeneratorBase
    {
        // Configuration: Base chance for equipped items to drop (e.g., 5%)
        public const int BASE_DROP_CHANCE = 25;
        public const int CLOAK_DROP_BONUS = 15; // Cloaks drop 15% more often

        public override LootList GenerateLoot(GameObject mobObj, GameObject killerObj)
        {
            GameNPC mob = mobObj as GameNPC;
            GamePlayer player = (killerObj as GameLiving)?.GetController() as GamePlayer;

            if (mob == null || player == null) return base.GenerateLoot(mobObj, killerObj);

            LootList list = new LootList();

            // Safety check: Grey mobs don't drop items
            if (player.GetConLevel(mob) <= -3) return list;

            // 1. Iterate over the Mob's Equipment
            if (mob.Inventory != null)
            {
                // Create a copy of items to avoid modification issues during iteration
                List<InventoryItem> mobItems = new List<InventoryItem>(mob.Inventory.EquippedItems);

                foreach (InventoryItem mobItem in mobItems)
                {
                    // 2. Determine Drop Chance
                    int chance = BASE_DROP_CHANCE + player.LootChance; // Add player luck

                    // Boost chance for Cloaks
                    if (mobItem.SlotPosition == (int)eInventorySlot.Cloak)
                        chance += CLOAK_DROP_BONUS;

                    if (Util.Chance(chance))
                    {
                        // 3. Generate the Smart Item
                        ItemTemplate dropItem = SmartUniqueItem.CreateFromMobItem(mob, mobItem, player);

                        if (dropItem != null)
                        {
                            list.AddFixed(dropItem, 1);
                        }
                    }
                }
            }

            // Optional: Fallback to standard random loot if nothing dropped?
            // base.GenerateLoot(...)

            return list;
        }
    }
}