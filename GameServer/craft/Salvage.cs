using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.GS.SalvageCalc; // Injects the custom Salvage Calculator
using DOL.GS.ServerProperties;
using log4net;

namespace DOL.GS
{
    /// <summary>
    /// The class holding all salvage functions
    /// </summary>
    public class Salvage
    {
        protected static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        #region Declaration
        protected const string SALVAGE_YIELD = "SALVAGE_YIELD";
        protected const string SALVAGED_ITEM = "SALVAGED_ITEM";
        protected const string SALVAGE_YIELD_LIST = "SALVAGE_YIELD_LIST";
        #endregion

        #region First call function and callback

        /// <summary>
        /// Begin salvaging an inventory item
        /// </summary>
        public static int BeginWork(GamePlayer player, InventoryItem item)
        {
            if (!IsAllowedToBeginWork(player, item))
            {
                return 0;
            }

            var mineralRecipe = MineralSalvage.GetRecipe(item.Id_nb);
            if (mineralRecipe != null)
            {
                if (!MineralSalvage.HasRequiredSkill(player, mineralRecipe.Tier))
                {
                    int required = MineralSalvage.GetRequiredSkill(mineralRecipe.Tier);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.IsAllowedToBeginWork.MineralSkillReq",required, item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return 0;
                }
                var mineralYields = new List<SalvageYieldEntry>();
                foreach (var comp in mineralRecipe.Components)
                {
                    if (GameServer.Database.FindObjectByKey<ItemTemplate>(comp.ID) == null)
                    {
                        log.WarnFormat("Mineral Salvage: component '{0}' not found in database, skipped for {1}", comp.ID, item.Id_nb);
                        continue;
                    }
                    mineralYields.Add(comp);
                }
                if (mineralYields.Count == 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SalvageRecipeNotfound"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return 0;
                }
                if (player.IsMoving || player.IsStrafing)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.InterruptSalvage"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return 0;
                }
                if (player.IsStealthed)
                    player.Stealth(false);

                SalvageYield mineralYield = new SalvageYield
                {
                    MaterialId_nb = mineralYields[0].ID,
                    Count = mineralYields[0].Count
                };
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.BeginSalvage", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                player.Out.SendTimerWindow(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.Salvaging", item.Name), mineralYield.Count);
                player.CraftTimer = new RegionTimer(player)
                {
                    Callback = new RegionTimerCallback(Proceed)
                };
                player.CraftTimer.Properties.setProperty(AbstractCraftingSkill.PLAYER_CRAFTER, player);
                player.CraftTimer.Properties.setProperty(SALVAGED_ITEM, item);
                player.CraftTimer.Properties.setProperty(SALVAGE_YIELD, mineralYield);
                player.CraftTimer.Properties.setProperty(SALVAGE_YIELD_LIST, mineralYields);
                player.CraftTimer.Start(mineralYield.Count * 1000);
                return 1;
            }

            SalvageYield salvageYield = null;
            ItemTemplate material = null;
            List<SalvageYieldEntry> yieldList = null;

            int salvageLevel = CraftingMgr.GetItemCraftLevel(item) / 100;
            if (salvageLevel > 9) salvageLevel = 9;

            var whereClause = WhereClause.Empty;

            if (item.SalvageYieldID == 0)
            {
                whereClause = DB.Column(nameof(SalvageYield.ObjectType)).IsEqualTo(item.Object_Type).And(DB.Column(nameof(SalvageYield.SalvageLevel)).IsEqualTo(salvageLevel));
            }
            else
            {
                whereClause = DB.Column(nameof(SalvageYield.ID)).IsEqualTo(item.SalvageYieldID);
            }

            if (Properties.USE_SALVAGE_PER_REALM)
            {
                whereClause = whereClause.And(DB.Column(nameof(SalvageYield.Realm)).IsEqualTo((int)eRealm.None).Or(DB.Column(nameof(SalvageYield.Realm)).IsEqualTo(item.Realm)));
            }

            salvageYield = DOLDB<SalvageYield>.SelectObject(whereClause);

            if (salvageYield != null && !string.IsNullOrEmpty(salvageYield.MaterialId_nb))
            {
                material = GameServer.Database.FindObjectByKey<ItemTemplate>(salvageYield.MaterialId_nb);
            }

            SalvageYield yield = new SalvageYield();
            if (material == null || salvageYield == null || string.IsNullOrEmpty(salvageYield.MaterialId_nb) || salvageYield.Count == 0)
            {
                SalvageCalculator calc = new SalvageCalculator();
                SalvageReturn calcReturn = calc.GetSalvage(player, item);

                if (!string.IsNullOrEmpty(calcReturn.ID) && calcReturn.Count > 0)
                {
                    yield.MaterialId_nb = calcReturn.ID;
                    yield.Count = calcReturn.Count;
                    material = GameServer.Database.FindObjectByKey<ItemTemplate>(calcReturn.ID);

                    if (calcReturn.Yields != null && calcReturn.Yields.Count > 0)
                    {
                        yieldList = new List<SalvageYieldEntry>();
                        foreach (var entry in calcReturn.Yields)
                        {
                            if (entry.Count < 1 || string.IsNullOrEmpty(entry.ID))
                                continue;

                            if (GameServer.Database.FindObjectByKey<ItemTemplate>(entry.ID) == null)
                            {
                                log.WarnFormat("Salvage: material template '{0}' not found in database, entry skipped for item {1}", entry.ID, item.Name);
                                continue;
                            }
                            yieldList.Add(entry);
                        }
                        if (yieldList.Count == 0)
                            yieldList = null;
                    }
                }
            }
            else
            {
                yield = salvageYield.Clone() as SalvageYield;
            }

            if (material == null)
            {
                if (item.SalvageYieldID > 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ItemRecipeNotimplemented", item.SalvageYieldID), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    log.ErrorFormat("SalvageYield ID {0} not found for item: {1}", item.SalvageYieldID, item.Name);
                }
                else
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SalvageRecipeNotfound"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    log.ErrorFormat("Salvage Lookup Error: ObjectType: {0}, Item: {1}", item.Object_Type, item.Name);
                }
                return 0;
            }

            if (player.IsMoving || player.IsStrafing)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.InterruptSalvage"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return 0;
            }

            if (player.IsStealthed)
            {
                player.Stealth(false);
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.BeginSalvage", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            int originalPrimary = yield!.Count;
            int count = GetMaterialYield(player, item, yield, material);

            if (count < 1)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.NoSalvage", item.Name) + LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.ZeroReturn"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return 0;
            }

            // Scale all extra yields by the same skill-based ratio applied to the primary
            if (yieldList != null && originalPrimary > 0)
            {
                double ratio = (double)yield.Count / originalPrimary;
                bool primaryUpdated = false;
                for (int i = 0; i < yieldList.Count; i++)
                {
                    var e = yieldList[i];
                    if (!primaryUpdated && e.IsPrimary && e.ID == yield.MaterialId_nb)
                    {
                        e.Count = yield.Count;
                        primaryUpdated = true;
                    }
                    else
                    {
                        e.Count = Math.Max(1, (int)Math.Round(e.Count * ratio));
                    }
                    yieldList[i] = e;
                }
            }

            player.Out.SendTimerWindow(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.Salvaging", item.Name), yield.Count);
            player.CraftTimer = new RegionTimer(player)
            {
                Callback = new RegionTimerCallback(Proceed)
            };
            player.CraftTimer.Properties.setProperty(AbstractCraftingSkill.PLAYER_CRAFTER, player);
            player.CraftTimer.Properties.setProperty(SALVAGED_ITEM, item);
            player.CraftTimer.Properties.setProperty(SALVAGE_YIELD, yield);
            player.CraftTimer.Properties.setProperty(SALVAGE_YIELD_LIST, yieldList);

            player.CraftTimer.Start(yield.Count * 1000);
            return 1;
        }

        public static int GetMaterialYield(GamePlayer player, InventoryItem item, SalvageYield salvageYield, ItemTemplate rawMaterial)
        {
            int maxCount = salvageYield.Count;

            if (maxCount <= 0)
            {
                maxCount = (int)(item.Price * 0.45 / Math.Max(1, rawMaterial.Price)); // crafted item return max 45% of the item value in material
                if (item.IsCrafted)
                {
                    maxCount = (int)Math.Ceiling((double)maxCount / 2);
                }
            }

            int playerPercent = player.GetCraftingSkillValue(CraftingMgr.GetSecondaryCraftingSkillToWorkOnItem(item)) * 100 / Math.Max(1, CraftingMgr.GetItemCraftLevel(item));

            if (playerPercent > 100) playerPercent = 100;
            else if (playerPercent < 75) playerPercent = 75;

            int minCount = (int)(((maxCount - 1) / 25f) * playerPercent) - ((3 * maxCount) - 4);

            if (minCount < 1) minCount = 1;
            if (minCount > maxCount) minCount = maxCount;

            salvageYield.Count = Util.Random(minCount, maxCount);
            return salvageYield.Count;
        }

        /// <summary>
        /// Begin salvaging a siege weapon
        /// </summary>
        public static int BeginWork(GamePlayer player, GameSiegeWeapon siegeWeapon)
        {
            if (siegeWeapon == null)
                return 0;

            siegeWeapon.ReleaseControl();
            siegeWeapon.RemoveFromWorld();
            bool error = false;
            var recipe = DOLDB<DBCraftedItem>.SelectObject(DB.Column(nameof(DBCraftedItem.Id_nb)).IsEqualTo(siegeWeapon.ItemId));

            if (recipe == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ErrorRetrievingSalvage"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                log.Error("Salvage Siege Error: DBCraftedItem is null for" + siegeWeapon.ItemId);
                return 1;
            }

            var rawMaterials = DOLDB<DBCraftedXItem>.SelectObjects(DB.Column(nameof(DBCraftedXItem.CraftedItemId_nb)).IsEqualTo(recipe.Id_nb));

            if (rawMaterials == null || rawMaterials.Count == 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NoMaterialSiegeweapon"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                log.Error("Salvage Siege Error: No Raw Materials found for " + siegeWeapon.ItemId);
                return 1;
            }

            if (player.IsCrafting)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.IsAllowedToBeginWork.EndCurrentAction"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return 0;
            }
            InventoryItem item;
            ItemTemplate template;
            foreach (DBCraftedXItem materialItem in rawMaterials)
            {
                template = GameServer.Database.FindObjectByKey<ItemTemplate>(materialItem.IngredientId_nb);

                if (template == null)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.Proceed.MissingRawMaterial", materialItem.IngredientId_nb), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    log.Error("Salvage Siege Error: Raw Material not found " + materialItem.IngredientId_nb);
                    return 1;
                }

                item = GameInventoryItem.Create(template);
                item.Count = materialItem.Count;
                if (!player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, item))
                {
                    error = true;
                    break;
                }
                InventoryLogging.LogInventoryAction("", "(salvage)", player, eInventoryActionType.Craft, item, item.Count);
            }

            if (error)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.NoRoom"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            return 1;
        }

        /// <summary>
        /// Called when craft time is finished
        /// </summary>
        protected static int Proceed(RegionTimer timer)
        {
            GamePlayer player = timer.Properties.getProperty<GamePlayer>(AbstractCraftingSkill.PLAYER_CRAFTER, null);
            InventoryItem itemToSalvage = timer.Properties.getProperty<InventoryItem>(SALVAGED_ITEM, null);
            SalvageYield yield = timer.Properties.getProperty<SalvageYield>(SALVAGE_YIELD, null);
            List<SalvageYieldEntry> yieldList = timer.Properties.getProperty<List<SalvageYieldEntry>>(SALVAGE_YIELD_LIST, null);
            int materialCount = yield?.Count ?? 0;

            if (player == null || itemToSalvage == null || yield == null || materialCount == 0)
            {
                player?.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.Proceed.ErrorRetrievingSalvage2"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return 0;
            }

            if (yieldList == null || yieldList.Count == 0)
            {
                yieldList = new List<SalvageYieldEntry>
                {
                    new SalvageYieldEntry { ID = yield.MaterialId_nb, Count = materialCount, IsPrimary = true }
                };
            }

            var resolved = new List<(ItemTemplate template, int count)>();
            foreach (var entry in yieldList)
            {
                if (string.IsNullOrEmpty(entry.ID) || entry.Count < 1) continue;
                ItemTemplate tpl = GameServer.Database.FindObjectByKey<ItemTemplate>(entry.ID);
                if (tpl == null)
                {
                    log.WarnFormat("Salvage Proceed: material '{0}' not found, skipping.", entry.ID);
                    continue;
                }
                resolved.Add((tpl, entry.Count));
            }

            if (resolved.Count == 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.Proceed.ErrorFindingRawMaterial"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return 0;
            }

            player.CraftTimer.Stop();
            player.CraftTimer = null;
            player.Out.SendCloseTimerWindow();

            if (!player.Inventory.RemoveItem(itemToSalvage))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.Proceed.ErrorFindingItem"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return 0;
            }

            InventoryLogging.LogInventoryAction(player, "", "(salvage)", eInventoryActionType.Craft, itemToSalvage, itemToSalvage.Count);

            foreach (var (rawMaterial, count) in resolved)
            {
                DistributeMaterial(player, rawMaterial, count);

                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.Proceed.GetBackMaterial", count, rawMaterial.Name, itemToSalvage.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            return 0;
        }

        /// <summary>
        /// Delivers a salvaged raw material to the player.
        /// Priority: backpack stacks -> empty backpack slot -> storage bag (if the bag accepts the
        /// item, e.g. IngredientsBag + "craft_ingredient" PackageID) -> dropped on the ground.
        /// </summary>
        private static void DistributeMaterial(GamePlayer player, ItemTemplate rawMaterial, int materialCount)
        {
            int remainder = 0;
            Dictionary<int, int> changedSlots = new Dictionary<int, int>(5);
            lock (player.Inventory)
            {
                int count = materialCount;
                foreach (InventoryItem item in player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                {
                    if (item == null || item.Id_nb != rawMaterial.Id_nb || item.Count >= item.MaxCount)
                        continue;

                    int countFree = item.MaxCount - item.Count;
                    if (count > countFree)
                    {
                        changedSlots.Add(item.SlotPosition, countFree);
                        count -= countFree;
                    }
                    else
                    {
                        changedSlots.Add(item.SlotPosition, count);
                        count = 0;
                        break;
                    }
                }

                if (count > 0)
                {
                    eInventorySlot firstEmptySlot = player.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                    if (firstEmptySlot != eInventorySlot.Invalid)
                    {
                        changedSlots.Add((int)firstEmptySlot, -count);
                        count = 0;
                    }
                    else
                    {
                        remainder = count;
                    }
                }
            }

            if (changedSlots.Count > 0)
            {
                InventoryItem newItem;
                player.Inventory.BeginChanges();
                foreach (var de in changedSlots)
                {
                    int countToAdd = de.Value;
                    if (countToAdd > 0)
                    {
                        newItem = player.Inventory.GetItem((eInventorySlot)de.Key);
                        player.Inventory.AddCountToStack(newItem, countToAdd);
                        InventoryLogging.LogInventoryAction("", "(salvage)", player, eInventoryActionType.Craft, newItem, countToAdd);
                    }
                    else
                    {
                        newItem = GameInventoryItem.Create(rawMaterial);
                        newItem.Count = -countToAdd;
                        player.Inventory.AddItem((eInventorySlot)de.Key, newItem);
                        InventoryLogging.LogInventoryAction("", "(salvage)", player, eInventoryActionType.Craft, newItem, newItem.Count);
                    }
                }
                player.Inventory.CommitChanges();
            }
            if (remainder <= 0)
                return;

            InventoryItem overflowItem = GameInventoryItem.Create(rawMaterial);
            overflowItem.Count = remainder;

            if (string.IsNullOrEmpty(overflowItem.PackageID) && !string.IsNullOrEmpty(rawMaterial.PackageID))
                overflowItem.PackageID = rawMaterial.PackageID;

            if (player.TryAddToStorageBagTemplate(overflowItem, remainder))
            {
                InventoryLogging.LogInventoryAction("", "(salvage-bag)", player, eInventoryActionType.Craft, overflowItem, remainder);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.ReceiveItem.ReceiveAllInBag", rawMaterial.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return;
            }
            if (overflowItem is GameInventoryItem gameOverflow)
            {
                gameOverflow.Drop(player);
            }
            else
            {
                WorldInventoryItem worldItem = new WorldInventoryItem(overflowItem);
                worldItem.Position = player.Position;
                worldItem.AddOwner(player);
                worldItem.AddToWorld();
            }
            InventoryLogging.LogInventoryAction(player, "", "(salvage-ground)", eInventoryActionType.Other, overflowItem, remainder);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.NoRoom"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TextNPC.InventoryFullItemGround", rawMaterial.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        #endregion

        #region Requirement check

        public static bool IsAllowedToBeginWork(GamePlayer player, InventoryItem item)
        {
            string idnb = item.Id_nb ?? string.Empty;
            string iname = item.Name ?? string.Empty;

            if (idnb.StartsWith("vt_") || idnb.StartsWith("vf_") || idnb.StartsWith("empty_vial") ||
                iname.StartsWith("[ROG]BloodVial") || iname.StartsWith("[ROG]PlayerVial"))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.NoSalvage", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            bool isMineral = MineralSalvage.IsMineral(item);

            if (player.InCombat)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.NoSalvageCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (item.IsNotLosingDur || item.IsIndestructible)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.NoSalvage", item.Name) + LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.ItemIndestructible"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // using negative numbers to indicate item cannot be salvaged
            if (item.SalvageYieldID < 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.NoSalvage", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (item.SlotPosition < (int)eInventorySlot.FirstBackpack || item.SlotPosition > (int)eInventorySlot.LastBackpack)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.IsAllowedToBeginWork.BackpackItems"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (!isMineral)
            {
                eCraftingSkill skill = CraftingMgr.GetSecondaryCraftingSkillToWorkOnItem(item);
                if (skill == eCraftingSkill.NoCrafting)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.NoSalvage", item.Name) + LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.BeginWork.NoSecondarySkill"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                if (player.GetCraftingSkillValue(skill) < (0.75 * CraftingMgr.GetItemCraftLevel(item)))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.IsAllowedToBeginWork.NotEnoughSkill", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            if (player.IsCrafting)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Salvage.IsAllowedToBeginWork.EndCurrentAction"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            return true;
        }

        #endregion
    }
}