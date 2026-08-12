using DOL.Database;
using DOL.Events;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.Scripts
{
    /// <summary>
    /// Centralized engine to process dynamic [ROG], Minerals, and Unique Item trades for Text NPCs.
    /// Handles parsing, validation, bracket logic, and dynamic reward dispensation.
    /// </summary>
    public static class UniqueItemExchangerEngine
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// Intercepts the trade. Returns true if the trade was processed by this engine 
        /// </summary>
        public static bool TryProcessUniqueItemTrade(TextNPCPolicy policy, GamePlayer player, GameNPC npc, InventoryItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Name)) return false;

            // Stone Maker Module (Tailleur de pierres)
            if (policy.EchangeurDB.ContainsKey("STONEMAKER:ALL") && !string.IsNullOrEmpty(item.Id_nb) && StoneMakerModule.IsMineral(item.Id_nb))
            {
                return StoneMakerModule.ProcessTrade(policy, player, npc, item);
            }

            // Blood Vial Module
            if (item.Name.StartsWith("[ROG]PlayerVial|") || item.Name.StartsWith("[ROG]BloodVial|"))
            {
                return BloodVialModule.ProcessTrade(policy, player, npc, item);
            }

            // Future expansion: 
            // if (item.Name.StartsWith("[ROG]BountyHead")) return BountyModule.ProcessTrade(...);

            return false;
        }

        #region Shared Inventory Helpers

        /// <summary>
        /// Safely retrieves all items from the Player's Backpack, Saddlebags, and any internal StorageBags.
        /// </summary>
        public static List<InventoryItem> GetAllAccessibleItems(GamePlayer player)
        {
            List<InventoryItem> allItems = new List<InventoryItem>();
            if (player == null || player.Inventory == null) return allItems;

            List<StorageBagItem> storageBags = new List<StorageBagItem>();

            lock (player.Inventory)
            {
                var backpack = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (backpack != null)
                {
                    foreach (var i in backpack)
                    {
                        if (i != null)
                        {
                            allItems.Add(i);
                            if (i is StorageBagItem bag && !string.IsNullOrEmpty(bag.ObjectId))
                                storageBags.Add(bag);
                        }
                    }
                }

                var saddlebags = player.Inventory.GetItemRange(eInventorySlot.FirstBagHorse, eInventorySlot.LastBagHorse);
                if (saddlebags != null)
                {
                    foreach (var i in saddlebags)
                    {
                        if (i != null)
                        {
                            allItems.Add(i);
                            if (i is StorageBagItem bag && !string.IsNullOrEmpty(bag.ObjectId))
                                storageBags.Add(bag);
                        }
                    }
                }
            }

            foreach (var bag in storageBags)
            {
                try
                {
                    StorageBagVault vault = new StorageBagVault(player, bag);
                    var bagItems = vault.DBItems(player);
                    if (bagItems != null)
                    {
                        foreach (var i in bagItems)
                        {
                            if (i != null)
                                allItems.Add(i);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Error retrieving items from StorageBag {bag.Id_nb} for player {player.Name}", ex);
                }
            }

            return allItems;
        }

        public static int CountItems(GamePlayer player, string id_nb)
        {
            if (player == null || string.IsNullOrEmpty(id_nb)) return 0;
            int count = 0;
            foreach (var item in GetAllAccessibleItems(player))
            {
                if (item != null && item.Id_nb == id_nb)
                {
                    count += item.Count;
                }
            }
            return count;
        }

        public static void RemoveItems(GamePlayer player, string id_nb, int amount)
        {
            if (amount <= 0 || player == null || player.Inventory == null || string.IsNullOrEmpty(id_nb)) return;
            int remaining = amount;

            // 1. Check Standard Inventory & Saddlebags
            lock (player.Inventory)
            {
                var directItems = new List<InventoryItem>();

                var bp = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (bp != null) directItems.AddRange(bp.Where(i => i != null && i.Id_nb == id_nb));

                var sb = player.Inventory.GetItemRange(eInventorySlot.FirstBagHorse, eInventorySlot.LastBagHorse);
                if (sb != null) directItems.AddRange(sb.Where(i => i != null && i.Id_nb == id_nb));

                foreach (var invItem in directItems)
                {
                    if (remaining <= 0) break;
                    int toRemove = Math.Min(invItem.Count, remaining);
                    player.Inventory.RemoveCountFromStack(invItem, toRemove);
                    remaining -= toRemove;
                }
            }

            if (remaining <= 0) return;

            // 2. Check Storage Bags (nested database items)
            List<StorageBagItem> bags = new List<StorageBagItem>();
            lock (player.Inventory)
            {
                var bp = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (bp != null) bags.AddRange(bp.OfType<StorageBagItem>());

                var sb = player.Inventory.GetItemRange(eInventorySlot.FirstBagHorse, eInventorySlot.LastBagHorse);
                if (sb != null) bags.AddRange(sb.OfType<StorageBagItem>());
            }

            bool bagUpdated = false;
            foreach (var bag in bags)
            {
                if (remaining <= 0) break;
                if (string.IsNullOrEmpty(bag.ObjectId)) continue;

                try
                {
                    StorageBagVault vault = new StorageBagVault(player, bag);
                    var dbItems = vault.DBItems(player);
                    if (dbItems == null) continue;

                    var itemsInBag = dbItems.Where(i => i != null && i.Id_nb == id_nb).ToList();

                    if (itemsInBag.Count > 0)
                    {
                        bool bagChanged = false;
                        Dictionary<int, InventoryItem> updatedItems = new Dictionary<int, InventoryItem>();

                        lock (vault.LockObject())
                        {
                            foreach (var vItem in itemsInBag)
                            {
                                if (remaining <= 0) break;

                                int toRemove = Math.Min(vItem.Count, remaining);
                                vItem.Count -= toRemove;
                                remaining -= toRemove;

                                int clientSlot = vItem.SlotPosition - vault.FirstDBSlot + vault.FirstClientSlot;

                                if (vItem.Count <= 0)
                                {
                                    GameServer.Database.DeleteObject(vItem);
                                    InventoryItem emptyItem = new InventoryItem();
                                    emptyItem.SlotPosition = clientSlot;
                                    emptyItem.Count = 0;
                                    updatedItems[clientSlot] = emptyItem;
                                }
                                else
                                {
                                    GameServer.Database.SaveObject(vItem);
                                    updatedItems[clientSlot] = vItem;
                                }
                                bagChanged = true;

                                player.Notify(PlayerInventoryEvent.ItemCountChanged, player, new ItemCountChangedEventArgs(player, vItem, -toRemove));
                            }
                        }

                        if (bagChanged)
                        {
                            bag.InvalidateWeightCache();
                            bagUpdated = true;

                            if (player.ActiveInventoryObject is StorageBagVault activeVault && activeVault.GetOwner(player) == vault.GetOwner(player))
                            {
                                player.Out.SendInventoryItemsUpdate(updatedItems, eInventoryWindowType.Update);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Error removing items from StorageBag {bag.Id_nb} for player {player.Name}", ex);
                }
            }

            if (bagUpdated)
                player.UpdateEncumberance();
        }

        /// <summary>
        /// Highly stable, fast FNV-1a Hash algorithm to define minerals difficulty
        /// </summary>
        public static int ComputeStableHash(string str)
        {
            uint hash = 2166136261;
            foreach (char c in str)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }

        #endregion

        #region Stone Maker Module (Tailleur de pierres)

        public static class StoneMakerModule
        {
            private static readonly Dictionary<string, int> _mineralTiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Binders mapped by tier
            private static readonly Dictionary<int, string[]> BindersByTier = new Dictionary<int, string[]>
            {
                { 1, new[] { "bone_ash", "brimstone_powder" } },
                { 2, new[] { "peat_moss", "calcium_dust" } },
                { 3, new[] { "quicksilver_drop", "ectoplasmic_residue", "zirconium_dust", "chromium_dust" } },
                { 4, new[] { "ley_line_dust", "tungsten_ore", "molybdenum_dust", "bismuth_shard" } },
                { 5, new[] { "astral_dust", "chronal_ash" } }
            };

            private class StoneRecipe
            {
                public int RawCountNeeded;
                public long CoinFee;
                public string BinderId;
                public int BinderCountNeeded;
            }

            static StoneMakerModule()
            {
                string[] tier1 = { "malachite_nodule", "cassiterite_pebble", "hematite_chunk", "bog_iron_concretion", "azurite_crystal", "chalcocite_stone", "bornite_pebble", "cuprite_chunk", "stannite_rock", "goethite_ore", "limonite_crust", "siderite_nodule", "taconite_shard", "meteoric_iron_dust", "chalcopyrite_fragment", "tetrahedrite_pebble", "enargite_rock", "tenantite_stone", "chrysocolla_chunk", "covellite_flake", "arsenopyrite_stone", "pyrrhotite_pebble", "marcasite_nodule", "galena_shard", "cerussite_stone", "anglesite_crystal", "bauxite_pebble", "sphalerite_grit", "smithsonite_chunk", "hemimorphite_rock", "willemite_crystal", "zincite_nodule", "hydrozincite_crust", "franklinite_pebble" };
                string[] tier2 = { "bituminous_coal_lump", "quartz_geode", "dolomite_nodule", "pyrite_cluster", "anthracite_coal_chunk", "lignite_coal_pebble", "graphite_flake", "clear_quartz_shard", "smoky_quartz_crystal", "rose_quartz_pebble", "amethyst_geode_fragment", "citrine_crystal_shard", "agate_nodule", "onyx_pebble", "carnelian_stone", "chalcedony_chunk", "jasper_rock", "flint_nodule", "chert_shard", "magnesite_stone", "brucite_pebble", "talc_rock", "chlorite_flake", "epidote_crystal", "prehnite_stone", "wollastonite_chunk", "rhodonite_pebble", "rhodochrosite_crystal", "manganite_stone", "pyrolusite_nodule", "bixbyite_shard", "hausmannite_rock", "braunite_pebble", "psilomelane_crust", "barite_rose" };
                string[] tier3 = { "pentlandite_orepiece", "spinel_gemstone", "silvery_mithril_vein", "topaz_rough", "zircon_crystal", "smaltite_nodule", "cobaltite_rock", "skutterudite_stone", "erythrite_crystal", "glaucodot_pebble", "garnierite_chunk", "millerite_stone", "niccolite_nodule", "chloanthite_rock", "breithauptite_pebble", "beryl_crystal", "aquamarine_rough", "emerald_flaw", "heliodor_stone", "morganite_rough", "tourmaline_crystal", "schorl_rock", "dravite_pebble", "elbaite_stone", "garnet_rough", "almandine_crystal", "pyrope_stone", "spessartine_nodule", "grossular_pebble", "andradite_rock", "uvarovite_crystal", "spodumene_chunk", "kunzite_rough", "hiddenite_stone", "petalite_pebble", "lepidolite_flake", "amblygonite_rock", "turquoise_rough", "variscite_nodule", "wavellite_crystal" };
                string[] tier4 = { "corundum_fragment", "sapphire_rough", "stibnite_asterite", "diamond_rough", "alexandrite_shard", "ruby_rough", "padparadscha_stone", "rutile_crystal", "anatase_pebble", "brookite_rock", "ilmenite_chunk", "perovskite_stone", "titanite_crystal", "columbite_nodule", "tantalite_rock", "wolframite_chunk", "scheelite_crystal", "ferberite_pebble", "huebnerite_stone", "molybdenite_flake", "wulfenite_crystal", "powellite_rock", "bismutite_nodule", "bismuthinite_stone", "cinnabar_chunk", "realgar_crystal", "orpiment_rock", "stibnite_pebble", "kermesite_stone", "tetrahedrite_crystal", "bournonite_nodule", "pyrargyrite_rock", "proustite_crystal", "stephanite_pebble", "polybasite_stone", "acanthite_chunk", "argentite_crystal", "chlorargyrite_rock", "bromargyrite_pebble", "iodargyrite_stone" };
                string[] tier5 = { "ilmenite_nethershard", "black_opal_void", "labradorite_astral", "benitoite_astral", "painite_relic", "taaffeite_relic", "chaos_emerald_flaw", "law_stone_fragment", "runeblade_splinter", "ariyoch_blood_ruby", "xiombarg_tear_crystal", "mabden_soul_gem", "melnibonean_jade", "chronocrystal_shard", "entropy_geode", "void_touched_obsidian", "abyssal_pearl", "star_metal_slag", "astral_diamond_dust", "nether_quartz_anomaly", "eldritch_amethyst", "warped_space_spinel", "demonic_bloodstone", "celestial_celestite", "ethereal_moonstone", "phantom_quartz_core", "twilight_topaz", "eclipse_sunstone", "paradox_prism", "singularity_stone", "aetherial_amber", "pandemonium_pyrite", "leylined_lapis", "oblivion_onyx", "hyperdimensional_halite" };

                foreach (var s in tier1) _mineralTiers[s] = 1;
                foreach (var s in tier2) _mineralTiers[s] = 2;
                foreach (var s in tier3) _mineralTiers[s] = 3;
                foreach (var s in tier4) _mineralTiers[s] = 4;
                foreach (var s in tier5) _mineralTiers[s] = 5;
            }

            public static bool IsMineral(string id_nb)
            {
                if (string.IsNullOrEmpty(id_nb)) return false;
                return _mineralTiers.ContainsKey(id_nb) && !id_nb.StartsWith("carved_");
            }

            /// <summary>
            /// A realistic algorithm that evaluates structural "hardness" based on physical properties
            /// </summary>
            private static int GetRealisticMineralHardness(string id_nb, int stableHash)
            {
                int hardness = 50;
                string lower = id_nb.ToLower();

                // Add structural hardness modifiers based on Mohs scale logic
                if (lower.Contains("diamond") || lower.Contains("corundum") || lower.Contains("sapphire") || lower.Contains("ruby") || lower.Contains("emerald") || lower.Contains("alexandrite")) 
                    hardness += 40; // Hard gems
                else if (lower.Contains("quartz") || lower.Contains("topaz") || lower.Contains("beryl") || lower.Contains("garnet") || lower.Contains("spinel") || lower.Contains("tourmaline")) 
                    hardness += 20; // Medium hard
                else if (lower.Contains("coal") || lower.Contains("chalk") || lower.Contains("talc") || lower.Contains("dust") || lower.Contains("powder") || lower.Contains("ash") || lower.Contains("moss")) 
                    hardness -= 30; // Soft/fragile
                else if (lower.Contains("obsidian") || lower.Contains("flint") || lower.Contains("chert") || lower.Contains("glass"))
                    hardness += 10; // Brittle but hard

                // Add minor variance using the stable hash so not all quartz behave identically
                hardness += (stableHash % 20) - 10; 

                return Math.Max(1, hardness);
            }

            /// <summary>
            /// Deterministically generates the carving recipe (cost, raw counts, binders) 
            /// ensuring stability across server reboots.
            /// </summary>
            private static StoneRecipe GetRecipe(ItemTemplate rawMineral, int tier)
            {
                int hash = ComputeStableHash(rawMineral.Id_nb);
                int hardness = GetRealisticMineralHardness(rawMineral.Id_nb, hash);
                
                StoneRecipe recipe = new StoneRecipe();
                long basePrice = rawMineral.Price > 0 ? rawMineral.Price : (tier * 100);

                // Tier-based "Easy" Thresholds 
                int easyChance = tier switch { 1 => 50, 2 => 30, 3 => 18, 4 => 9, _ => 0 };

                // Adjust easy chance based on hardness (softer = slightly easier to craft simply)
                easyChance -= (hardness - 50) / 2;
                int roll = hash % 100;

                if (roll < easyChance)
                {
                    recipe.RawCountNeeded = 1;
                    recipe.CoinFee = basePrice * 3;
                    recipe.BinderId = null;
                    recipe.BinderCountNeeded = 0;
                }
                else
                {
                    recipe.RawCountNeeded = 1 + (hash % 3); 
                    recipe.CoinFee = basePrice * ((hardness + (hash % 20) > 60) ? 4 : 3);
                    
                    string[] availableBinders = BindersByTier.ContainsKey(tier) ? BindersByTier[tier] : BindersByTier[1];
                    recipe.BinderId = availableBinders[(hash / 10) % availableBinders.Length];
                    recipe.BinderCountNeeded = 1 + ((hash / 100) % 3); 
                }

                if (recipe.CoinFee < 10) recipe.CoinFee = 100;
                return recipe;
            }

            public static bool ProcessTrade(TextNPCPolicy policy, GamePlayer player, GameNPC npc, InventoryItem item)
            {
                var rule = policy.EchangeurDB["STONEMAKER:ALL"];
                int tier = _mineralTiers[item.Id_nb];
                StoneRecipe recipe = GetRecipe(item.Template, tier);

                int currentRawCount = CountItems(player, item.Id_nb);
                int currentBinderCount = string.IsNullOrEmpty(recipe.BinderId) ? 0 : CountItems(player, recipe.BinderId);

                if (currentRawCount < recipe.RawCountNeeded || currentBinderCount < recipe.BinderCountNeeded || player.CopperBalance < Currency.Copper.Mint(recipe.CoinFee).Amount)
                {
                    string msg = $"Ah, {item.Name}. I can work with that to make a fine jewel. ";
                    msg += $"However, I will need {recipe.RawCountNeeded}x of them";
                    
                    if (recipe.BinderCountNeeded > 0)
                    {
                        ItemTemplate binderTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(recipe.BinderId);
                        string binderName = binderTemplate != null ? binderTemplate.Name : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(recipe.BinderId.Replace("_", " "));
                        msg += $", plus {recipe.BinderCountNeeded}x '{binderName}' to treat the stone";
                    }

                    msg += $", and my jeweler's fee is {Currency.Copper.Mint(recipe.CoinFee).ToText(player.Client?.Account?.Language)}.";
                    
                    player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }

                lock (player.Inventory)
                {
                    if (CountItems(player, item.Id_nb) < recipe.RawCountNeeded) return true;
                    if (recipe.BinderCountNeeded > 0 && CountItems(player, recipe.BinderId) < recipe.BinderCountNeeded) return true;
                    
                    RemoveItems(player, item.Id_nb, recipe.RawCountNeeded);
                    if (recipe.BinderCountNeeded > 0)
                        RemoveItems(player, recipe.BinderId, recipe.BinderCountNeeded);
                }

                if (!player.RemoveMoney(Currency.Copper.Mint(recipe.CoinFee)))
                {
                    return true; 
                }
                
                // Generate or Fetch the Crafted ItemTemplate
                ItemTemplate carvedTemplate = GetOrCreateCarvedTemplate(item.Template, tier);
                InventoryItem carvedResult = GameInventoryItem.Create(carvedTemplate);
                
                // Grant Carved Stone to Player
                if (!player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, carvedResult))
                {
                    carvedResult.Count = 1;
                    player.CreateItemOnTheGround(carvedResult);
                    player.Out.SendMessage($"Your backpack is full! The {carvedTemplate.Name} falls to the ground.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    player.Out.SendMessage($"\"Excellent...\" {npc.Name} works quickly and with immense precision. \"Here is your {carvedTemplate.Name}. A true beauty, isn't it?\"", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    player.Out.SendPlaySound(eSoundType.Craft, 0x04); 
                }
                
                InventoryLogging.LogInventoryAction(player, npc, eInventoryActionType.Craft, carvedResult, 1);
                rule.ChangedItemCount++;
                GameServer.Database.SaveObject(rule);
                
                return true;
            }

            /// <summary>
            /// Analyzes the raw mineral's ID to provide a highly realistic real-world lapidary prefix.
            /// </summary>
            private static string GetJewelPrefix(string id_nb, int tier, int hash)
            {
                string raw = id_nb.ToLower();

                if (tier == 5)
                {
                    string[] t5Prefixes = { "Awakened", "Purified", "Resonant", "Celestial", "Flawless", "Ethereal" };
                    return t5Prefixes[hash % t5Prefixes.Length];
                }

                if (raw.Contains("diamond") || raw.Contains("ruby") || raw.Contains("sapphire") || raw.Contains("emerald") || raw.Contains("topaz") || raw.Contains("spinel") || raw.Contains("zircon") || raw.Contains("amethyst") || raw.Contains("alexandrite"))
                {
                    string[] gemPrefixes = { "Brilliant", "Faceted", "Flawless", "Radiant", "Perfect", "Luminous" };
                    return gemPrefixes[hash % gemPrefixes.Length];
                }

                if (raw.Contains("crystal") || raw.Contains("shard") || raw.Contains("prism"))
                {
                    string[] crystalPrefixes = { "Polished", "Honed", "Gleaming", "Faceted", "Refined" };
                    return crystalPrefixes[hash % crystalPrefixes.Length];
                }

                if (raw.Contains("rough") || raw.Contains("flaw"))
                {
                    string[] roughPrefixes = { "Cut", "Refined", "Polished", "Cleaved", "Shaped" };
                    return roughPrefixes[hash % roughPrefixes.Length];
                }

                if (raw.Contains("chunk") || raw.Contains("rock") || raw.Contains("ore") || raw.Contains("stone") || raw.Contains("crust") || raw.Contains("vein") || raw.Contains("lump"))
                {
                    string[] rockPrefixes = { "Carved", "Sculpted", "Engraved", "Chiseled", "Smooth", "Hewn" };
                    return rockPrefixes[hash % rockPrefixes.Length];
                }

                if (raw.Contains("pebble") || raw.Contains("nodule") || raw.Contains("geode") || raw.Contains("pearl"))
                {
                    string[] roundPrefixes = { "Tumbled", "Polished", "Cabochon", "Rounded" };
                    return roundPrefixes[hash % roundPrefixes.Length];
                }

                if (raw.Contains("dust") || raw.Contains("flake") || raw.Contains("ash") || raw.Contains("slag") || raw.Contains("grit") || raw.Contains("powder"))
                {
                    string[] powderPrefixes = { "Fused", "Sintered", "Molded", "Pressed", "Tempered" };
                    return powderPrefixes[hash % powderPrefixes.Length];
                }

                if (raw.Contains("fragment") || raw.Contains("splinter"))
                {
                    string[] fragmentPrefixes = { "Shaped", "Bound", "Reconstructed", "Forged" };
                    return fragmentPrefixes[hash % fragmentPrefixes.Length];
                }

                return (hash % 2 == 0) ? "Polished" : "Carved";
            }

            /// <summary>
            /// Automatically generates a REGULAR ItemTemplate in the DB for the Crafted Stone if it doesn't exist yet.
            /// </summary>
            private static ItemTemplate GetOrCreateCarvedTemplate(ItemTemplate raw, int tier)
            {
                int hash = ComputeStableHash(raw.Id_nb);
                string prefix = GetJewelPrefix(raw.Id_nb, tier, hash);

                string carvedId = $"carved_{raw.Id_nb}";
                if (carvedId.Length > 255) carvedId = carvedId.Substring(0, 255);

                ItemTemplate carved = GameServer.Database.FindObjectByKey<ItemTemplate>(carvedId);
                
                if (carved == null)
                {
                    carved = new ItemTemplate(raw);
                    carved.Id_nb = carvedId;

                    string rawName = raw.Name;
                    if (!string.IsNullOrEmpty(rawName))
                    {
                        rawName = char.ToUpper(rawName[0]) + rawName.Substring(1);
                    }

                    carved.Name = $"{prefix} {rawName}";
                    carved.Price = raw.Price * 3;
                    carved.PackageID = "craft_ingredient";
                    
                    GameServer.Database.AddObject(carved);
                    
                    try { GameServer.Database.UpdateInCache<ItemTemplate>(carvedId); } catch { }
                    log.Info($"[StoneMaker] Dynamically generated and cached new regular ItemTemplate: {carvedId} ({carved.Name})");
                }
                return carved;
            }
        }
        #endregion

        #region Blood Vials Module

        public static class BloodVialModule
        {
            public static bool ProcessTrade(TextNPCPolicy policy, GamePlayer player, GameNPC npc, InventoryItem item)
            {
                bool isPlayerVial = item.Name.StartsWith("[ROG]PlayerVial|");
                bool isNpcVial = item.Name.StartsWith("[ROG]BloodVial|");

                // Determine if NPC has a matching DBEchangeur rule FIRST
                DBEchangeur matchedRule = FindVialRule(policy, item, isPlayerVial, isNpcVial);

                if (matchedRule == null)
                {
                    return false; 
                }

                // Reject temporary partial (< 100%) vials.
                if (item.Id_nb != null && item.Id_nb.StartsWith("vt_"))
                {
                    player.Out.SendMessage("I only accept full, 100% blood vials. Extract completely before returning.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return true;
                }

                lock (player.Inventory)
                {
                    if (player.Inventory.GetItem((eInventorySlot)item.SlotPosition) != item)
                        return true;

                    if (item.Count > 1)
                        player.Inventory.RemoveCountFromStack(item, 1);
                    else
                        LootGeneratorBloodVials.RemoveVialItem(player, item);
                }

                InventoryLogging.LogInventoryAction(player, npc, eInventoryActionType.Quest, item, 1);

                if (isPlayerVial)
                    RewardPlayerVial(player, item, matchedRule, npc, policy);
                else if (isNpcVial)
                    RewardNpcVial(player, item, matchedRule, npc, policy);

                return true;
            }

            private static DBEchangeur FindVialRule(TextNPCPolicy policy, InventoryItem item, bool isPlayerVial, bool isNpcVial)
            {
                if (isPlayerVial) return policy.EchangeurDB.Values.FirstOrDefault(r => r.ItemRecvID != null && r.ItemRecvID.Equals("PLAYERVIAL:ALL", StringComparison.OrdinalIgnoreCase));
                
                if (isNpcVial)
                {
                    string[] parts = item.Name.Split('|');
                    if (parts.Length < 4) return null;
                    string bloodType = parts[2].Replace(" ", "");
                    string mobName = parts[3].Replace(" ", "");

                    var rule = policy.EchangeurDB.Values.FirstOrDefault(r => r.ItemRecvID != null && r.ItemRecvID.Replace(" ", "").Equals($"NPCVIAL:NAME:{mobName}", StringComparison.OrdinalIgnoreCase));
                    if (rule != null) return rule;

                    rule = policy.EchangeurDB.Values.FirstOrDefault(r => r.ItemRecvID != null && r.ItemRecvID.Replace(" ", "").Equals($"NPCVIAL:BLOOD:{bloodType}", StringComparison.OrdinalIgnoreCase));
                    if (rule != null) return rule;

                    return policy.EchangeurDB.Values.FirstOrDefault(r => r.ItemRecvID != null && r.ItemRecvID.Replace(" ", "").Equals("NPCVIAL:ALL", StringComparison.OrdinalIgnoreCase));
                }
                return null;
            }

            private static void RewardPlayerVial(GamePlayer player, InventoryItem item, DBEchangeur rule, GameNPC npc, TextNPCPolicy policy)
            {
                string[] parts = item.Name.Split('|');
                string prefix = parts.Length > 1 ? parts[1].Trim() : "";

                long copperReward = 4000;
                int sealCount = 1;

                switch (prefix)
                {
                    case "Exalted": copperReward = 100000; sealCount = 5; break;
                    case "Illustrous": copperReward = 50000; sealCount = 3; break;
                    case "Vigorous": copperReward = 20000; sealCount = 2; break;
                }

                int coinBonusPercent = player.GetModified(eProperty.MythicalCoin);
                long finalCopperReward = copperReward + ((copperReward * coinBonusPercent) / 100);

                if (finalCopperReward > 0)
                {
                    player.AddMoney(Currency.Copper.Mint(finalCopperReward));
                    player.Out.SendMessage($"The exchanger rewards you with {Currency.Copper.Mint(finalCopperReward).ToText(player.Client?.Account?.Language)}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }

                // Items ("sanguine_dreaded_seal")
                if (sealCount > 0)
                {
                    ItemTemplate sealTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>("sanguine_dreaded_seal");
                    if (sealTemplate != null)
                    {
                        InventoryItem rewardItem = GameInventoryItem.Create(sealTemplate);
                        lock (player.Inventory)
                        {
                            if (!player.Inventory.AddTemplate(rewardItem, sealCount, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                            {
                                rewardItem.Count = sealCount;
                                player.CreateItemOnTheGround(rewardItem);
                                player.Out.SendMessage("Your backpack is full! The seals fall to the ground.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                player.Out.SendMessage($"You receive {sealCount}x {sealTemplate.Name}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                        }
                    }
                }

                if (rule.GainMoney > 0)
                {
                    long finalGainMoney = rule.GainMoney + ((rule.GainMoney * coinBonusPercent) / 100);
                    player.AddMoney(Currency.Copper.Mint(finalGainMoney));
                    player.Out.SendMessage($"You receive an extra {Currency.Copper.Mint(finalGainMoney).ToText(player.Client?.Account?.Language)}.", eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
                }

                if (rule.GainXP > 0)
                {
                    int xpBonusPercent = player.GetModified(eProperty.XpPoints);
                    long finalGainXP = rule.GainXP + ((rule.GainXP * xpBonusPercent) / 100);

                    player.GainExperience(GameLiving.eXPSource.Quest, finalGainXP, 0, 0, 0, false, false, 1);
                }

                FinalizeExchange(player, rule, npc, policy);
            }

            private static void RewardNpcVial(GamePlayer player, InventoryItem item, DBEchangeur rule, GameNPC npc, TextNPCPolicy policy)
            {
                int effectiveLevel = item.Level;

                if (effectiveLevel > player.Level + 4)
                {
                    effectiveLevel = player.Level + 4;
                }

                if (effectiveLevel > 48)
                {
                    effectiveLevel = 48;
                }

                if (effectiveLevel < 1)
                {
                    effectiveLevel = 1;
                }

                long xpNextLevel = GamePlayer.GetExperienceAmountForLevel(effectiveLevel);
                long xpCurrentLevel = effectiveLevel > 1 ? GamePlayer.GetExperienceAmountForLevel(effectiveLevel - 1) : 0;
                long bracketXp = xpNextLevel - xpCurrentLevel;
                long xpReward = Math.Max(1L, bracketXp / 100);

                int xpBonusPercent = player.GetModified(eProperty.XpPoints);
                long finalXpReward = xpReward + ((xpReward * xpBonusPercent) / 100);

                if (finalXpReward > 0)
                {
                    player.GainExperience(GameLiving.eXPSource.Quest, finalXpReward, 0, 0, 0, false, false, 1);
                    player.Out.SendMessage($"The blood essence imparts {finalXpReward} experience to you.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }

                if (!string.IsNullOrEmpty(rule.ItemGiveID) && rule.ItemGiveCount > 0)
                {
                    ItemTemplate giveTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(rule.ItemGiveID);
                    if (giveTemplate != null)
                    {
                        InventoryItem rewardItem = GameInventoryItem.Create(giveTemplate);
                        lock (player.Inventory)
                        {
                            if (!player.Inventory.AddTemplate(rewardItem, rule.ItemGiveCount, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                            {
                                rewardItem.Count = rule.ItemGiveCount;
                                player.CreateItemOnTheGround(rewardItem);
                                player.Out.SendMessage($"Your backpack is full! The {giveTemplate.Name} falls to the ground.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                player.Out.SendMessage($"You receive {rule.ItemGiveCount}x {giveTemplate.Name}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                        }
                    }
                }

                if (rule.GainMoney > 0)
                {
                    int coinBonusPercent = player.GetModified(eProperty.MythicalCoin);
                    long finalGainMoney = rule.GainMoney + ((rule.GainMoney * coinBonusPercent) / 100);

                    player.AddMoney(Currency.Copper.Mint(finalGainMoney));
                    player.Out.SendMessage($"You receive an extra {Currency.Copper.Mint(finalGainMoney).ToText(player.Client?.Account?.Language)}.", eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
                }

                if (rule.GainXP > 0)
                {
                    long finalGainXP = rule.GainXP + ((rule.GainXP * xpBonusPercent) / 100);
                    player.GainExperience(GameLiving.eXPSource.Quest, finalGainXP, 0, 0, 0, false, false, 1);
                }

                FinalizeExchange(player, rule, npc, policy);
            }

            private static void FinalizeExchange(GamePlayer player, DBEchangeur rule, GameNPC npc, TextNPCPolicy policy)
            {
                rule.ChangedItemCount++;
                GameServer.Database.SaveObject(rule);

                if (policy.Reponses != null && policy.Reponses.TryGetValue(rule.ItemRecvID, out string rawResponse))
                {
                    string text = string.Format(rawResponse, player.Name, player.LastName, player.GuildName, player.Salutation, player.RaceName);
                    if (!string.IsNullOrEmpty(text))
                        player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
                else
                {
                    player.Out.SendMessage($"{npc.Name} nods and securely stows the vial away.", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
            }
        }
        #endregion
    }
}