using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.AI.Brain;
using log4net;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.GS.Scripts;

namespace DOL.GS
{
    public class LootGeneratorBloodVials : LootGeneratorBase
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        // O(1) hashset for extremely fast model lookups
        private static readonly HashSet<ushort> SpecialSapModels = new HashSet<ushort>
        {
            97, 570, 1702, 1703, 1704, 1910, 767, 948, 946, 862,
            700, 701, 702, 703, 704, 705, 706, 707, 708, 709, 710, 711, 712, 713, 714, 715,
            732, 733, 734, 735, 736, 737, 738, 739, 740, 741, 742, 743, 744, 745, 746, 747,
            849, 850, 851, 852, 853, 854, 855, 856,
            1211, 1655, 1656, 1663, 1664, 1990, 1991
        };

        public override LootList GenerateLoot(GameObject mobObj, GameObject killerObj)
        {
            LootList loot = base.GenerateLoot(mobObj, killerObj);

            // Reliably get the player (handling pets, charms, spells, dots)
            GamePlayer killerPlayer = (killerObj as GameLiving)?.GetController() as GamePlayer;
            if (killerPlayer == null && killerObj is GameLiving living)
            {
                killerPlayer = living.GetLivingOwner() as GamePlayer;
            }

            // Group leader looting allowance
            if (killerPlayer != null && killerPlayer.Group != null)
            {
                killerPlayer = killerPlayer.Group.Leader;
            }

            if (killerPlayer == null)
                return loot;

            if (mobObj is GameNPC mob)
            {
                HandleMobVialLoot(killerPlayer, mob, loot);
            }

            return loot;
        }

        public static void HandlePlayerVialLoot(GamePlayer killer, GamePlayer victim, LootList loot = null)
        {
            if (killer.Reputation >= 0) return;
            if (killer.IsInPvP || killer.IsInRvR || killer.CurrentRegion.IsRvR) return;
            if (killer.DuelTarget == victim) return;
            if (killer.TempProperties.getProperty<bool>("ArenaParticipant", false)) return;

            InventoryItem emptyVial = GetEmptyVial(killer);
            if (emptyVial == null) return;

            string prefix = GetPlayerVialPrefix(victim);
            bool isSap = (victim.Race == (int)eRace.Sylvan);
            ushort model = GetPlayerVialModel((eRace)victim.Race);
            int price = CalculatePlayerVialPrice(prefix);

            string packageIdSafe = $"p_{victim.Race}_{prefix.Replace(" ", "")}";

            if (!RemoveEmptyVial(killer, emptyVial)) return;

            ItemTemplate baseVialTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>("empty_vial")
                                         ?? GameServer.Database.FindObjectByKey<ItemTemplate>("empty_vial01")
                                         ?? GameServer.Database.FindObjectByKey<ItemTemplate>("empty_vial02")
                                         ?? new ItemTemplate();

            GiveFullPlayerVial(killer, victim, prefix, isSap, model, price, baseVialTemplate, packageIdSafe, loot, emptyVial);
        }

        private void HandleMobVialLoot(GamePlayer player, GameNPC mob, LootList loot)
        {
            string restrictedMobs = ServerProperties.Properties.NO_BLOOD_DROP_MOBS;
            if (!string.IsNullOrEmpty(restrictedMobs))
            {
                var restrictedList = restrictedMobs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (restrictedList.Contains(mob.Name, StringComparer.OrdinalIgnoreCase))
                    return;
            }

            bool isSpecialSapMob = SpecialSapModels.Contains(mob.Model);
            bool isEpicBoss = mob.IsBoss && mob.Level > 69;
            string bloodType = "";
            string prefix = "";

            if (isSpecialSapMob)
            {
                if (mob.Level <= 10) bloodType = "Pale Sap";
                else if (mob.Level <= 20) bloodType = "Amber Resin";
                else if (mob.Level <= 30) bloodType = "Glowing Sap";
                else if (mob.Level <= 41) bloodType = "Petrified Resin";
                else bloodType = "Void Sap";

                if (isEpicBoss) prefix = GetBossPrefix(mob.BodyType);
            }
            else
            {
                bloodType = GetBloodType(mob);
                if (isEpicBoss) prefix = GetBossPrefix(mob.BodyType);
                else prefix = GetPrefix(mob.Level);
            }

            // Skeletons, constructs, inanimate items return null / "None"
            if (string.IsNullOrEmpty(bloodType) || bloodType == "None")
                return;

            int fillAmount = GetFillAmount(mob.Level);

            // If player OR mob is level 6 or below, instantly grant a full vial.
            if (player.Level <= 6 || mob.Level <= 6 || isEpicBoss)
            {
                fillAmount = 100;
                if (!isEpicBoss && mob.Level <= 6 && !isSpecialSapMob) prefix = "";
            }

            string safeMobName = mob.Name.Replace(" ", "").Replace("'", "");
            string safeBloodType = bloodType.Replace(" ", "");
            string prefixSafe = prefix.Replace(" ", "");

            string prefixKey = prefixSafe;
            string bloodTypeKey = safeBloodType;
            string rogNameBase = $"[ROG]BloodVial|{prefixKey}|{bloodTypeKey}|{mob.Name}|";

            string signature = $"{mob.Level}|{mob.Name}|{prefix}|{bloodType}|{mob.IsBoss}|{mob.BodyType}|{isSpecialSapMob}";
            string packageIdSafe = $"{mob.Level}_{safeMobName}_{prefixSafe}_{safeBloodType}";

            string baseVialId = isSpecialSapMob ? bloodType.Replace(" ", "_").ToLower() : "empty_vial";

            InventoryItem emptyVial = GetEmptyVial(player);
            InventoryItem tempVial = null;

            lock (player.Inventory)
            {
                List<InventoryItem> searchItems = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack).ToList();
                List<StorageBagItem> bags = searchItems.OfType<StorageBagItem>().ToList();
                foreach (var bag in bags)
                {
                    var vault = new StorageBagVault(player, bag);
                    searchItems.AddRange(vault.DBItems(player));
                }

                tempVial = searchItems.FirstOrDefault(i => i != null &&
                                         (i.PackageID == signature || i.PackageID == packageIdSafe || (i.Template != null && (i.Template.PackageID == signature || i.Template.PackageID == packageIdSafe)))
                                         && i.Charges < 100 && i.Id_nb != null && i.Id_nb.StartsWith("vt_"));
            }

            if (tempVial == null && emptyVial == null)
                return;

            ItemTemplate baseVialTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(baseVialId)
                                         ?? GameServer.Database.FindObjectByKey<ItemTemplate>("empty_vial")
                                         ?? GameServer.Database.FindObjectByKey<ItemTemplate>("empty_vial01")
                                         ?? GameServer.Database.FindObjectByKey<ItemTemplate>("empty_vial02")
                                         ?? new ItemTemplate();

            if (tempVial != null)
            {
                int newPct = Math.Min(100, tempVial.Charges + fillAmount);

                if (newPct >= 100)
                {
                    RemoveVialItem(player, tempVial);
                    GiveFullVial(player, mob.Level, mob.Name, prefix, bloodType, mob.IsBoss, mob.BodyType, baseVialTemplate, packageIdSafe, signature, loot, isSpecialSapMob, mob, tempVial);
                }
                else
                {
                    tempVial.Charges = newPct;
                    tempVial.Condition = mob.Level;
                    tempVial.MaxCondition = mob.Level;

                    string newVialName = $"{rogNameBase}{newPct}";
                    tempVial.Name = newVialName;

                    int price = CalculateVialPrice(mob.Level, newPct, mob.IsBoss, isSpecialSapMob);
                    tempVial.Price = price;

                    if (tempVial.Template is ItemUnique uT)
                    {
                        uT.Name = newVialName;
                        uT.Condition = mob.Level;
                        uT.MaxCondition = mob.Level;
                        uT.Price = price;
                        uT.Model = GetVialModel(bloodType, mob.BodyType);
                        uT.Dirty = true;
                        GameServer.Database.SaveObject(uT);
                    }

                    GameServer.Database.SaveObject(tempVial);
                    UpdateVialUI(player, tempVial);

                    string translatedName = LanguageMgr.GetItemNameMessage(player.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage, newVialName);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "Lootgenerator.LootGeneratorBloodVials.Filled", translatedName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
            else
            {
                if (!RemoveEmptyVial(player, emptyVial))
                    return;

                int newPct = Math.Min(100, fillAmount);

                if (newPct >= 100)
                {
                    GiveFullVial(player, mob.Level, mob.Name, prefix, bloodType, mob.IsBoss, mob.BodyType, baseVialTemplate, packageIdSafe, signature, loot, isSpecialSapMob, mob, emptyVial);
                }
                else
                {
                    GiveTempVial(player, mob.Level, mob.Name, prefix, bloodType, mob.IsBoss, mob.BodyType, baseVialTemplate, newPct, signature, loot, isSpecialSapMob, mob, emptyVial);
                }
            }
        }

        private static InventoryItem GetEmptyVial(GamePlayer player)
        {
            lock (player.Inventory)
            {
                List<InventoryItem> searchItems = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack).ToList();
                List<StorageBagItem> bags = searchItems.OfType<StorageBagItem>().ToList();
                foreach (var bag in bags)
                {
                    var vault = new StorageBagVault(player, bag);
                    searchItems.AddRange(vault.DBItems(player));
                }

                return searchItems.FirstOrDefault(i => i != null && i.Id_nb != null &&
                                         (i.Id_nb.Equals("empty_vial", StringComparison.OrdinalIgnoreCase) ||
                                          i.Id_nb.Equals("empty_vial01", StringComparison.OrdinalIgnoreCase) ||
                                          i.Id_nb.Equals("empty_vial02", StringComparison.OrdinalIgnoreCase)));
            }
        }

        private static string GetPlayerVialPrefix(GamePlayer victim)
        {
            int lvl = victim.Level;
            bool isRen = victim.IsRenaissance;
            int cl = victim.ChampionLevel;

            if (lvl == 50 && isRen && cl >= 4)
                return "Exalted";

            if (lvl >= 36 && lvl <= 50 && isRen && cl < 4)
                return "Illustrous";

            if (lvl < 36 && isRen && cl == 0)
                return "Vigorous";

            if (lvl >= 40 && lvl <= 50 && !isRen && cl == 0)
                return "Vigorous";

            return "";
        }

        private static ushort GetPlayerVialModel(eRace race)
        {
            switch (race)
            {
                case eRace.AlbionMinotaur:
                case eRace.MidgardMinotaur:
                case eRace.HiberniaMinotaur:
                    return 532;
                case eRace.HalfOgre:
                case eRace.Shar:
                case eRace.Troll:
                case eRace.Firbolg:
                    return 527;
                case eRace.Sylvan:
                    return 526;
                case eRace.Frostalf:
                case eRace.Kobold:
                    return 530;
                case eRace.Inconnu:
                    return 531;
                default:
                    return 529;
            }
        }

        public static int CalculatePlayerVialPrice(string prefix)
        {
            switch (prefix)
            {
                case "Vigorous": return 11000;
                case "Illustrous": return 63000;
                case "Exalted": return 93000;
                default: return 1100;
            }
        }

        private static bool RemoveEmptyVial(GamePlayer player, InventoryItem emptyVial)
        {
            var bag = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack).OfType<StorageBagItem>().FirstOrDefault(b => b.ObjectId == emptyVial.OwnerID);
            StorageBagVault vault = bag != null ? new StorageBagVault(player, bag) : null;

            if (emptyVial.SlotPosition >= (int)eInventorySlot.HouseVault_First)
            {
                if (emptyVial.Count > 1)
                {
                    emptyVial.Count--;
                    GameServer.Database.SaveObject(emptyVial);
                    if (vault != null) vault.OnAddItem(player, emptyVial);
                }
                else
                {
                    GameServer.Database.DeleteObject(emptyVial);
                    if (vault != null) vault.OnRemoveItem(player, emptyVial);
                }

                if (bag != null) bag.InvalidateWeightCache();
                return true;
            }
            else
            {
                return player.Inventory.RemoveCountFromStack(emptyVial, 1);
            }
        }

        public static void RemoveVialItem(GamePlayer player, InventoryItem item)
        {
            if (item.SlotPosition >= (int)eInventorySlot.HouseVault_First)
            {
                var bag = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack).OfType<StorageBagItem>().FirstOrDefault(b => b.ObjectId == item.OwnerID);
                StorageBagVault activeVault = null;

                if (bag != null)
                {
                    activeVault = new StorageBagVault(player, bag);
                    activeVault.OnRemoveItem(player, item);
                    bag.InvalidateWeightCache();
                }

                GameServer.Database.DeleteObject(item);
                if (item.Template is ItemUnique u && u.Id_nb != null && u.Id_nb.StartsWith("vt_")) { try { if (u.IsPersisted) GameServer.Database.DeleteObject(u); } catch { } }

                if (activeVault != null && player.ActiveInventoryObject is StorageBagVault currentActive && currentActive.GetOwner(player) == item.OwnerID)
                {
                    int clientSlot = item.SlotPosition - activeVault.FirstDBSlot + activeVault.FirstClientSlot;
                    var dict = new Dictionary<int, InventoryItem> { { clientSlot, null } };
                    player.Out.SendInventoryItemsUpdate(dict, eInventoryWindowType.Update);
                }
            }
            else
            {
                if (player.Inventory.RemoveItem(item))
                {
                    if (item.Template is ItemUnique u && u.Id_nb != null && u.Id_nb.StartsWith("vt_")) { try { if (u.IsPersisted) GameServer.Database.DeleteObject(u); } catch { } }
                }
            }
        }

        private static void UpdateVialUI(GamePlayer player, InventoryItem item)
        {
            if (item.SlotPosition >= (int)eInventorySlot.HouseVault_First)
            {
                if (player.ActiveInventoryObject is StorageBagVault activeVault && activeVault.GetOwner(player) == item.OwnerID)
                {
                    int clientSlot = item.SlotPosition - activeVault.FirstDBSlot + activeVault.FirstClientSlot;
                    var dict = new Dictionary<int, InventoryItem> { { clientSlot, item } };
                    player.Out.SendInventoryItemsUpdate(dict, eInventoryWindowType.Update);
                }
            }
            else
            {
                player.Out.SendInventoryItemsUpdate(new InventoryItem[] { item });
            }
        }

        public static void GiveFullPlayerVial(GamePlayer player, GamePlayer victim, string prefix, bool isSap, ushort model, int price, ItemTemplate baseVial, string packageIdSafe, LootList loot, InventoryItem originVial = null, eInventorySlot preferredSlot = eInventorySlot.Invalid)
        {
            string fullId = $"vf_{packageIdSafe}";
            if (fullId.Length > 100) fullId = fullId.Substring(0, 100);

            string prefixKey = prefix.Replace(" ", "");
            string vialName = $"[ROG]PlayerVial|{prefixKey}|{victim.Race}|{isSap}";

            ItemTemplate fullTemplate = GameServer.Database.FindObjectByKey<ItemUnique>(fullId)
                                     ?? GameServer.Database.FindObjectByKey<ItemTemplate>(fullId);

            if (fullTemplate == null)
            {
                ItemUnique newFullTemplate = new ItemUnique(baseVial);
                newFullTemplate.Id_nb = fullId;
                newFullTemplate.Name = vialName;
                newFullTemplate.Level = 50;
                newFullTemplate.Condition = 50;
                newFullTemplate.MaxCondition = 50;
                newFullTemplate.IsDropable = true;
                newFullTemplate.IsPickable = true;
                newFullTemplate.IsTradable = true;
                newFullTemplate.MaxCount = 10;
                newFullTemplate.Weight = 3;
                newFullTemplate.Item_Type = (int)eInventorySlot.FirstBackpack;
                newFullTemplate.Object_Type = (int)eObjectType.GenericItem;
                newFullTemplate.Model = model;
                newFullTemplate.Price = price;

                GameServer.Database.AddObject(newFullTemplate);
                fullTemplate = newFullTemplate;
            }
            else if (fullTemplate is ItemUnique uF)
            {
                uF.Price = price;
                uF.Model = model;
                uF.Dirty = true;
                GameServer.Database.SaveObject(uF);
            }

            InventoryItem fullInvItem = GameInventoryItem.Create(fullTemplate);
            fullInvItem.Condition = 50;
            fullInvItem.MaxCondition = 50;
            fullInvItem.PackageID = packageIdSafe;
            fullInvItem.Price = price;

            bool added = false;

            if (originVial != null && originVial.SlotPosition >= (int)eInventorySlot.HouseVault_First)
            {
                var bag = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack).OfType<StorageBagItem>().FirstOrDefault(b => b.ObjectId == originVial.OwnerID);
                if (bag != null)
                {
                    var vault = new StorageBagVault(player, bag);

                    bool slotIsEmpty = true;
                    lock (bag.CachedItems)
                    {
                        if (bag.CachedItems.ContainsKey(originVial.SlotPosition))
                            slotIsEmpty = false;
                    }

                    if (slotIsEmpty)
                    {
                        fullInvItem.SlotPosition = originVial.SlotPosition;
                        fullInvItem.OwnerID = originVial.OwnerID;

                        GameServer.Database.AddObject(fullInvItem);
                        vault.OnAddItem(player, fullInvItem);
                        bag.InvalidateWeightCache();
                        added = true;
                        UpdateVialUI(player, fullInvItem);
                    }
                    else
                    {
                        if (vault.AddItem(player, fullInvItem))
                        {
                            added = true;
                            bag.InvalidateWeightCache();
                        }
                    }
                }
            }
            else
            {
                if (preferredSlot != eInventorySlot.Invalid)
                    added = player.Inventory.AddItem(preferredSlot, fullInvItem);

                if (!added)
                    added = player.Inventory.AddTemplate(fullInvItem, 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
            }

            string translatedName = LanguageMgr.GetItemNameMessage(player.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage, vialName);

            if (added)
            {
                InventoryLogging.LogInventoryAction(player, "", "Player Vial Extraction", eInventoryActionType.Loot, fullInvItem, 1);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "Lootgenerator.LootGeneratorBloodVials.Extracted", translatedName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else
            {
                if (loot != null)
                {
                    loot.AddFixed(fullTemplate, 1);
                }
                else
                {
                    WorldInventoryItem drop = new WorldInventoryItem(fullInvItem);
                    drop.Position = player.Position;
                    drop.AddToWorld();
                }
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "TextNPC.InventoryFullItemGround", translatedName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        public static void GiveFullVial(GamePlayer player, int mobLevel, string mobName, string prefix, string bloodType, bool isBoss, int bodyType, ItemTemplate baseVial, string packageIdSafe, string signature, LootList loot, bool isSap, GameObject sourceObj = null, InventoryItem originVial = null, eInventorySlot preferredSlot = eInventorySlot.Invalid)
        {
            string fullId = $"vf_{packageIdSafe}";
            if (fullId.Length > 100) fullId = fullId.Substring(0, 100);

            string prefixKey = prefix.Replace(" ", "");
            string bloodTypeKey = bloodType.Replace(" ", "");
            string vialName = $"[ROG]BloodVial|{prefixKey}|{bloodTypeKey}|{mobName}|100";

            ItemTemplate fullTemplate = GameServer.Database.FindObjectByKey<ItemUnique>(fullId)
                                     ?? GameServer.Database.FindObjectByKey<ItemTemplate>(fullId);

            int price = CalculateVialPrice(mobLevel, 100, isBoss, isSap);

            if (fullTemplate == null)
            {
                ItemUnique newFullTemplate = new ItemUnique(baseVial);
                newFullTemplate.Id_nb = fullId;
                newFullTemplate.Name = vialName;
                newFullTemplate.Level = mobLevel;
                newFullTemplate.Condition = mobLevel;
                newFullTemplate.MaxCondition = mobLevel;
                newFullTemplate.IsDropable = true;
                newFullTemplate.IsPickable = true;
                newFullTemplate.IsTradable = true;
                newFullTemplate.MaxCount = 10;
                newFullTemplate.Weight = 3;
                newFullTemplate.Item_Type = (int)eInventorySlot.FirstBackpack;
                newFullTemplate.Object_Type = (int)eObjectType.GenericItem;
                newFullTemplate.Model = GetVialModel(bloodType, bodyType);
                newFullTemplate.Price = price;

                GameServer.Database.AddObject(newFullTemplate);
                fullTemplate = newFullTemplate;
            }
            else if (fullTemplate is ItemUnique uF)
            {
                uF.Condition = mobLevel;
                uF.MaxCondition = mobLevel;
                uF.Price = price;
                uF.Model = GetVialModel(bloodType, bodyType);
                uF.Dirty = true;
                GameServer.Database.SaveObject(uF);
            }

            InventoryItem fullInvItem = GameInventoryItem.Create(fullTemplate);
            fullInvItem.Condition = mobLevel;
            fullInvItem.MaxCondition = mobLevel;
            fullInvItem.PackageID = signature;
            fullInvItem.Price = price;

            bool added = false;

            if (originVial != null && originVial.SlotPosition >= (int)eInventorySlot.HouseVault_First)
            {
                var bag = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack).OfType<StorageBagItem>().FirstOrDefault(b => b.ObjectId == originVial.OwnerID);
                if (bag != null)
                {
                    var vault = new StorageBagVault(player, bag);

                    bool slotIsEmpty = true;
                    lock (bag.CachedItems)
                    {
                        if (bag.CachedItems.ContainsKey(originVial.SlotPosition))
                            slotIsEmpty = false;
                    }

                    if (slotIsEmpty)
                    {
                        fullInvItem.SlotPosition = originVial.SlotPosition;
                        fullInvItem.OwnerID = originVial.OwnerID;

                        GameServer.Database.AddObject(fullInvItem);
                        vault.OnAddItem(player, fullInvItem);
                        bag.InvalidateWeightCache();
                        added = true;
                        UpdateVialUI(player, fullInvItem);
                    }
                    else
                    {
                        if (vault.AddItem(player, fullInvItem))
                        {
                            added = true;
                            bag.InvalidateWeightCache();
                        }
                    }
                }
            }
            else
            {
                if (preferredSlot != eInventorySlot.Invalid)
                    added = player.Inventory.AddItem(preferredSlot, fullInvItem);

                if (!added)
                    added = player.Inventory.AddTemplate(fullInvItem, 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
            }

            string translatedName = LanguageMgr.GetItemNameMessage(player.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage, vialName);

            if (added)
            {
                if (sourceObj != null)
                    InventoryLogging.LogInventoryAction(player, sourceObj, eInventoryActionType.Loot, fullInvItem, 1);
                else
                    InventoryLogging.LogInventoryAction(player, "", "Combine Vials", eInventoryActionType.Other, fullInvItem, 1);

                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "Lootgenerator.LootGeneratorBloodVials.Collected", translatedName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else
            {
                if (loot != null)
                {
                    loot.AddFixed(fullTemplate, 1);
                }
                else
                {
                    WorldInventoryItem drop = new WorldInventoryItem(fullInvItem);
                    drop.Position = player.Position;
                    drop.AddToWorld();
                }
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "TextNPC.InventoryFullItemGround", translatedName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        public static void GiveTempVial(GamePlayer player, int mobLevel, string mobName, string prefix, string bloodType, bool isBoss, int bodyType, ItemTemplate baseVial, int newPct, string signature, LootList loot, bool isSap, GameObject sourceObj = null, InventoryItem originVial = null)
        {
            string prefixKey = prefix.Replace(" ", "");
            string bloodTypeKey = bloodType.Replace(" ", "");
            string vialName = $"[ROG]BloodVial|{prefixKey}|{bloodTypeKey}|{mobName}|{newPct}";

            ItemUnique newTempTemplate = new ItemUnique(baseVial);
            newTempTemplate.Id_nb = "vt_" + Guid.NewGuid().ToString("N").Substring(0, 16);
            newTempTemplate.PackageID = signature;
            newTempTemplate.Name = vialName;
            newTempTemplate.Level = mobLevel;
            newTempTemplate.Condition = mobLevel;
            newTempTemplate.MaxCondition = mobLevel;
            newTempTemplate.IsDropable = true;
            newTempTemplate.IsPickable = true;
            newTempTemplate.IsTradable = true;
            newTempTemplate.MaxCount = 1;
            newTempTemplate.Weight = 2;
            newTempTemplate.Item_Type = (int)eInventorySlot.FirstBackpack;
            newTempTemplate.Object_Type = (int)eObjectType.GenericItem;
            newTempTemplate.Model = GetVialModel(bloodType, bodyType);
            newTempTemplate.Price = CalculateVialPrice(mobLevel, newPct, isBoss, isSap);

            GameServer.Database.AddObject(newTempTemplate);

            InventoryItem tempInvItem = GameInventoryItem.Create(newTempTemplate);
            tempInvItem.PackageID = signature;
            tempInvItem.Charges = newPct;
            tempInvItem.Condition = mobLevel;
            tempInvItem.MaxCondition = mobLevel;
            tempInvItem.Price = newTempTemplate.Price;

            string translatedName = LanguageMgr.GetItemNameMessage(player.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage, vialName);

            if (originVial != null && originVial.SlotPosition >= (int)eInventorySlot.HouseVault_First)
            {
                var bag = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack).OfType<StorageBagItem>().FirstOrDefault(b => b.ObjectId == originVial.OwnerID);
                if (bag != null)
                {
                    var vault = new StorageBagVault(player, bag);

                    bool slotIsEmpty = true;
                    lock (bag.CachedItems)
                    {
                        if (bag.CachedItems.ContainsKey(originVial.SlotPosition))
                            slotIsEmpty = false;
                    }

                    if (slotIsEmpty)
                    {
                        tempInvItem.SlotPosition = originVial.SlotPosition;
                        tempInvItem.OwnerID = originVial.OwnerID;
                        GameServer.Database.AddObject(tempInvItem);

                        vault.OnAddItem(player, tempInvItem);
                        bag.InvalidateWeightCache();
                        UpdateVialUI(player, tempInvItem);

                        if (sourceObj != null) InventoryLogging.LogInventoryAction(player, sourceObj, eInventoryActionType.Loot, tempInvItem, 1);
                        else InventoryLogging.LogInventoryAction(player, "", "Combine Vials", eInventoryActionType.Other, tempInvItem, 1);

                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "Lootgenerator.LootGeneratorBloodVials.StartedCollecting", translatedName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        if (vault.AddItem(player, tempInvItem))
                        {
                            bag.InvalidateWeightCache();

                            if (sourceObj != null) InventoryLogging.LogInventoryAction(player, sourceObj, eInventoryActionType.Loot, tempInvItem, 1);
                            else InventoryLogging.LogInventoryAction(player, "", "Combine Vials", eInventoryActionType.Other, tempInvItem, 1);

                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "Lootgenerator.LootGeneratorBloodVials.StartedCollecting", translatedName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            if (loot != null) loot.AddFixed(newTempTemplate, 1);
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "TextNPC.InventoryFullItemGround", translatedName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        }
                    }
                }
            }
            else
            {
                eInventorySlot emptySlot = player.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (emptySlot != eInventorySlot.Invalid && player.Inventory.AddItem(emptySlot, tempInvItem))
                {
                    if (sourceObj != null)
                        InventoryLogging.LogInventoryAction(player, sourceObj, eInventoryActionType.Loot, tempInvItem, 1);
                    else
                        InventoryLogging.LogInventoryAction(player, "", "Combine Vials", eInventoryActionType.Other, tempInvItem, 1);

                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Lootgenerator.LootGeneratorBloodVials.StartedCollecting", translatedName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    if (loot != null)
                    {
                        loot.AddFixed(newTempTemplate, 1);
                    }
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TextNPC.InventoryFullItemGround", translatedName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
        }

        public static bool CombineVials(GamePlayer player, InventoryItem fromItem, InventoryItem toItem)
        {
            if (fromItem == null || toItem == null) return false;

            if (fromItem.Id_nb == null || toItem.Id_nb == null ||
                !fromItem.Id_nb.StartsWith("vt_") || !toItem.Id_nb.StartsWith("vt_"))
                return false;

            if (string.IsNullOrEmpty(fromItem.PackageID) || fromItem.PackageID != toItem.PackageID)
                return false;

            string[] pkg = fromItem.PackageID.Split('|');
            if (pkg.Length < 7) return false;

            int mobLevel = int.Parse(pkg[0]);
            string mobName = pkg[1];
            string prefix = pkg[2];
            string bloodType = pkg[3];
            bool isBoss = bool.Parse(pkg[4]);
            int bodyType = int.Parse(pkg[5]);
            bool isSap = bool.Parse(pkg[6]);

            int totalPct = fromItem.Charges + toItem.Charges;

            string prefixKey = prefix.Replace(" ", "");
            string bloodTypeKey = bloodType.Replace(" ", "");
            string rogNameBase = $"[ROG]BloodVial|{prefixKey}|{bloodTypeKey}|{mobName}|";

            if (totalPct < 100)
            {
                toItem.Charges = totalPct;
                string newName = $"{rogNameBase}{totalPct}";
                toItem.Name = newName;
                int newPrice = CalculateVialPrice(mobLevel, totalPct, isBoss, isSap);
                toItem.Price = newPrice;

                if (toItem.Template is ItemUnique uTo)
                {
                    uTo.Name = newName;
                    uTo.Price = newPrice;
                    uTo.Dirty = true;
                    GameServer.Database.SaveObject(uTo);
                }
                GameServer.Database.SaveObject(toItem);

                RemoveVialItem(player, fromItem);

                if (toItem.SlotPosition >= (int)eInventorySlot.HouseVault_First)
                {
                    var bag = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack).OfType<StorageBagItem>().FirstOrDefault(b => b.ObjectId == toItem.OwnerID);
                    if (bag != null) new StorageBagVault(player, bag).OnAddItem(player, toItem);
                }

                UpdateVialUI(player, toItem);
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Lootgenerator.LootGeneratorBloodVials.CombinedPartial", totalPct), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }
            else
            {
                int remainder = totalPct - 100;
                eInventorySlot toSlot = (eInventorySlot)toItem.SlotPosition;

                RemoveVialItem(player, toItem);

                string baseVialId = isSap ? bloodType.Replace(" ", "_").ToLower() : "empty_vial";
                ItemTemplate baseVialTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(baseVialId)
                                                ?? GameServer.Database.FindObjectByKey<ItemTemplate>("empty_vial")
                                                ?? new ItemTemplate();

                string safeMobName = mobName.Replace(" ", "").Replace("'", "");
                string safeBloodType = bloodType.Replace(" ", "");
                string prefixSafe = prefix.Replace(" ", "");
                string packageIdSafe = $"{mobLevel}_{safeMobName}_{prefixSafe}_{safeBloodType}";

                GiveFullVial(player, mobLevel, mobName, prefix, bloodType, isBoss, bodyType, baseVialTemplate, packageIdSafe, fromItem.PackageID, null, isSap, null, toItem, toSlot);

                if (remainder > 0)
                {
                    fromItem.Charges = remainder;
                    string newName = $"{rogNameBase}{remainder}";
                    fromItem.Name = newName;
                    int newPrice = CalculateVialPrice(mobLevel, remainder, isBoss, isSap);
                    fromItem.Price = newPrice;

                    if (fromItem.Template is ItemUnique uFrom)
                    {
                        uFrom.Name = newName;
                        uFrom.Price = newPrice;
                        uFrom.Dirty = true;
                        GameServer.Database.SaveObject(uFrom);
                    }
                    GameServer.Database.SaveObject(fromItem);

                    UpdateVialUI(player, fromItem);

                    string translatedRemainder = LanguageMgr.GetItemNameMessage(player.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage, newName);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "Lootgenerator.LootGeneratorBloodVials.CombinedRemainder", translatedRemainder), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    RemoveVialItem(player, fromItem);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Lootgenerator.LootGeneratorBloodVials.CombinedFull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }

                return true;
            }
        }

        public static int CalculateVialPrice(int mobLevel, int pct, bool isBoss, bool isSap)
        {
            int basePrice = 50;
            int maxAdded = 0;
            if (isSap)
            {
                if (mobLevel <= 10) maxAdded = 150;
                else if (mobLevel <= 20) maxAdded = 1500;
                else if (mobLevel <= 30) maxAdded = 6000;
                else if (mobLevel <= 41) maxAdded = 25000;
                else maxAdded = 75000;
            }
            else
            {
                if (mobLevel <= 6) maxAdded = 75;
                else if (mobLevel <= 12) maxAdded = 300;
                else if (mobLevel <= 18) maxAdded = 800;
                else if (mobLevel <= 24) maxAdded = 1800;
                else if (mobLevel <= 30) maxAdded = 4000;
                else if (mobLevel <= 37) maxAdded = 8500;
                else if (mobLevel <= 45) maxAdded = 18000;
                else if (mobLevel <= 52) maxAdded = 36000;
                else if (mobLevel <= 60) maxAdded = 63000;
                else maxAdded = 87000;
            }

            if (isBoss) maxAdded *= 2;

            return basePrice + (maxAdded * pct / 100);
        }

        public static ushort GetVialModel(string bloodType, int bodyType)
        {
            if (string.IsNullOrEmpty(bloodType)) return 605;

            if (bloodType == "Blood" || bloodType == "Rabid Blood")
            {
                if (bodyType == (int)NpcTemplateMgr.eBodyType.Giant) return 527;
                return 529; // Red
            }

            if (bloodType == "Insectoid Ichor" || bloodType == "Acidic Slime" || bloodType == "Corrosive Acid" || bloodType == "Venom" || bloodType == "Deadly Venom" || bloodType == "Spore Extract")
                return 526; // Green

            if (bloodType == "Boiling Dragonblood" || bloodType == "Liquid Hellfire" || bloodType == "Liquid Magma" || bloodType == "Amber Resin" || bloodType == "Petrified Resin")
                return 527; // Orange

            if (bloodType == "Cold Blood" || bloodType == "Liquid Frost" || bloodType == "Pure Elemental Water" || bloodType == "Faerie Dew" || bloodType == "Ectoplasm")
                return 528; // Cyan

            if (bloodType == "Machine Oil" || bloodType == "Magical Blood")
                return 530; // Blue

            if (bloodType == "Demonic Ichor" || bloodType == "Putrefied Blood" || bloodType == "Void Sap" || bloodType == "Abyssal Ichor")
                return 531; // Purple

            if (bloodType == "Plant Nectar" || bloodType == "Sticky Sap" || bloodType == "Pale Sap" || bloodType == "Glowing Sap" || bloodType == "Mineral Slurry")
                return 532; // Yellow

            return 605; // Base empty vial
        }

        private static string GetBossPrefix(int bodyType)
        {
            switch ((NpcTemplateMgr.eBodyType)bodyType)
            {
                case NpcTemplateMgr.eBodyType.Animal: return "Bestial";
                case NpcTemplateMgr.eBodyType.Giant: return "Colossal";
                case NpcTemplateMgr.eBodyType.Humanoid: return "Ancestral";
                case NpcTemplateMgr.eBodyType.Insect: return "Swarm";
                case NpcTemplateMgr.eBodyType.Reptile: return "Primeval";
                case NpcTemplateMgr.eBodyType.Dragon: return "Draconic";
                case NpcTemplateMgr.eBodyType.Plant: return "Overgrown";
                case NpcTemplateMgr.eBodyType.Undead: return "Deathless";
                case NpcTemplateMgr.eBodyType.Demon: return "Infernal";
                case NpcTemplateMgr.eBodyType.Elemental: return "Primal";
                case NpcTemplateMgr.eBodyType.Magical: return "Ethereal";
                default: return "Supreme";
            }
        }

        private int GetFillAmount(int mobLevel)
        {
            if (mobLevel <= 6) return 100;
            if (mobLevel <= 12) return 50;
            if (mobLevel <= 18) return 40;
            if (mobLevel <= 24) return 34;
            if (mobLevel <= 30) return 28;
            if (mobLevel <= 37) return 22;
            if (mobLevel <= 45) return 16;
            if (mobLevel <= 52) return 10;
            if (mobLevel <= 60) return 6;
            return 4;
        }

        private string GetPrefix(int mobLevel)
        {
            if (mobLevel <= 6) return "";
            if (mobLevel <= 12) return "Minor";
            if (mobLevel <= 18) return "Lesser";
            if (mobLevel <= 24) return "Flimsy";
            if (mobLevel <= 30) return "Average";
            if (mobLevel <= 37) return "Greater";
            if (mobLevel <= 45) return "Major";
            if (mobLevel <= 52) return "Superior";
            if (mobLevel <= 60) return "Exceptional";
            return "Legendary";
        }

        public static string GetBloodType(GameNPC mob)
        {
            if (mob == null) return null;

            if (ModelBloodTypes.TryGetValue(mob.Model, out string specificType))
            {
                return specificType == "None" ? null : specificType;
            }

            switch ((NpcTemplateMgr.eBodyType)mob.BodyType)
            {
                case NpcTemplateMgr.eBodyType.Animal:
                case NpcTemplateMgr.eBodyType.Humanoid:
                case NpcTemplateMgr.eBodyType.Giant:
                    return "Blood";
                case NpcTemplateMgr.eBodyType.Insect:
                    return "Insectoid Ichor";
                case NpcTemplateMgr.eBodyType.Reptile:
                    return "Cold Blood";
                case NpcTemplateMgr.eBodyType.Dragon:
                    return "Boiling Dragonblood";
                case NpcTemplateMgr.eBodyType.Plant:
                    return "Plant Nectar";
                case NpcTemplateMgr.eBodyType.Undead:
                    return "Putrefied Blood";
                case NpcTemplateMgr.eBodyType.Demon:
                    return "Demonic Ichor";
                case NpcTemplateMgr.eBodyType.Elemental:
                    return "Mineral Slurry";
                case NpcTemplateMgr.eBodyType.Magical:
                    return "Magical Blood";
                case NpcTemplateMgr.eBodyType.None:
                    return null;
            }

            return null;
        }

        private static readonly Dictionary<ushort, string> ModelBloodTypes = new Dictionary<ushort, string>();

        static LootGeneratorBloodVials()
        {
            var bloodDict = new Dictionary<string, ushort[]>
            {
                { "Blood", new ushort[] { 47, 56, 96, 99, 100, 101, 102, 104, 133, 134, 135, 413, 414, 447, 448, 449, 450, 459, 463, 464, 465, 569, 572, 580, 585, 586, 607, 608, 626, 647, 648, 650, 688, 689, 691, 693, 694, 695, 770, 828, 843, 846, 888, 924, 939, 1193, 1256, 2027, 2028, 2029, 2030, 2031, 2033, 2034, 2035, 2037, 2047, 2056, 2057, 2127, 2128, 2129, 2130, 2131, 2132, 2153, 2157, 2164, 2172, 2173, 2174, 2179, 2180, 2181, 2182, 2183, 2184, 2223, 2224, 2235, 2257, 2346, 2352, 2353, 2354, 2360, 119, 120, 121, 122, 253, 401, 402, 403, 412, 460, 601, 602, 609, 610, 611, 615, 616, 657, 661, 662, 663, 768, 772, 825, 826, 827, 830, 845, 861, 918, 919, 950, 1220, 1221, 1222, 1223, 1224, 1225, 1226, 1227, 1228, 1229, 1230, 1231, 1439, 1698, 1699, 1700, 1701, 1770, 2165, 2185 } },
                { "Cold Blood", new ushort[] { 118, 574, 581, 594, 816, 823, 967, 968, 969, 970, 971, 972, 974, 975, 983, 999, 1000, 1001, 1002, 1003, 1004, 1005, 1006, 105, 398, 399, 400, 597, 891, 925, 926, 1258, 1259, 1260, 1261, 1687 } },
                { "Rabid Blood", new ushort[] { 567, 892, 1596, 1747, 1869, 1870, 1886, 1956, 2001, 2002, 2003, 2287, 2288, 2289, 2290, 2351, 2356 } },
                { "Demonic Ichor", new ushort[] { 605, 606, 634, 637, 638, 639, 642, 644, 645, 646, 649, 652, 653, 690, 697, 1049, 2040, 2041, 2042, 2043, 2118, 2121, 2126, 2134, 2151, 2152, 2156, 2166, 2168, 2175, 2187, 2188, 2189, 2190, 2191, 2192, 2193, 2194, 2195, 2196, 2197, 2198, 2199, 2200, 2201, 2202, 2203, 2204, 2205, 2206, 2207, 2208, 2209, 2210, 2214, 2236, 2237, 2248, 2259, 2271, 2272, 2301 } },
                { "Liquid Hellfire", new ushort[] { 629, 635, 636, 2117 } },
                { "Boiling Dragonblood", new ushort[] { 455, 456, 576, 612, 613, 614, 765, 858, 863, 1235, 1266, 1933, 2296, 2297, 2298, 2308, 2309, 2320, 2321, 2322, 2323, 2324, 2325, 2329, 2330, 2331, 2333, 2334, 2335, 2336, 2337, 2338, 2340, 2341, 2342 } },
                { "Mineral Slurry", new ushort[] { 124, 2044, 2049, 2054, 2055, 2159, 2229, 2230, 2233, 2234, 2332, 2339 } },
                { "Liquid Magma", new ushort[] { 125, 1194, 1195, 1196, 1197, 1198, 1199, 1276, 1349, 2000, 2160, 2231, 2232, 2264, 2278 } },
                { "Liquid Frost", new ushort[] { 126, 2161, 2225, 2226, 2263 } },
                { "Pure Elemental Water", new ushort[] { 913, 928, 941, 944, 1269, 1275, 1393 } },
                { "Corrosive Acid", new ushort[] { 930, 931, 932, 933, 934, 935, 936, 937, 940, 942, 943 } },
                { "Venom", new ushort[] { 60, 71, 72, 116, 117, 129, 130, 131, 132, 404, 405, 406, 407, 453, 466, 470, 584, 598, 599, 640, 641, 684, 685, 686, 1257, 1262, 1263, 1597, 1606, 1607, 1674, 1675, 2150, 2293, 2294 } },
                { "Insectoid Ichor", new ushort[] { 115, 469, 573, 577, 587, 590, 591, 592, 593, 658, 668, 669, 670, 769, 771, 819, 824, 1007, 1200, 1201, 1202, 1205, 1206, 1207, 1682, 1879, 2020, 2021, 2260, 2261, 2286, 2291, 2292 } },
                { "Acidic Slime", new ushort[] { 123, 454, 457, 458, 654, 842, 951, 953, 1683, 1887, 2171, 2284, 2285 } },
                { "Faerie Dew", new ushort[] { 112, 127, 128, 136, 575, 595, 596, 603, 630, 631, 632, 633, 678, 679, 821, 947, 966, 1585, 1586, 1623, 1748, 2038, 2039, 2355 } },
                { "Machine Oil", new ushort[] { 831, 847, 1181, 2238, 2239, 2240, 2241, 2242, 2251, 2252, 2255, 2256, 2268, 2275 } },
                { "Magical Blood", new ushort[] { 241, 242, 243, 244, 696, 698, 699, 764, 813, 857, 904, 905, 915, 973, 1171, 1172, 1173, 1174, 1175, 1176, 1177, 1178, 1179, 1180, 1212, 1219, 1236, 1264, 1350, 2026, 2058, 2158, 2250, 2258, 2265, 2266 } },
                { "Sticky Sap", new ushort[] { 97, 570, 844, 848, 946, 1702, 1703, 1704, 1910 } },
                { "Plant Nectar", new ushort[] { 571, 818, 862, 1584 } },
                { "Spore Extract", new ushort[] { 820, 860, 903, 906 } },
                { "Abyssal Ichor", new ushort[] { 1690, 1691, 1692, 1693, 1694, 1695, 1696, 1697, 1866, 1867, 1868, 2269, 2270 } },
                { "Deadly Venom", new ushort[] { 29, 30, 31, 766, 829, 1182, 1183, 1184, 1185, 1186, 1187, 1188, 1189, 1209, 1233, 1234, 2162, 2163, 2280, 2281, 2282, 2283 } },
                { "Putrefied Blood", new ushort[] { 23, 103, 106, 107, 108, 109, 110, 111, 440, 441, 442, 443, 444, 445, 446, 451, 452, 467, 468, 568, 604, 619, 643, 651, 659, 660, 664, 674, 675, 676, 677, 680, 681, 682, 683, 692, 921, 922, 923, 948, 949, 1208, 1210, 1277, 1278, 1279, 1280, 1281, 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289, 1290, 1291, 1292, 1293, 1294, 1295, 1296, 1297, 1298, 1299, 1300, 1301, 1302, 1303, 1304, 1305, 1306, 1307, 1308, 1309, 1310, 1311, 1312, 1387, 1388, 1431, 1432, 1433, 1434, 1435, 1436, 1437, 1440, 1575, 1578, 1581, 1673, 1888, 1889, 1890, 1891, 1892, 1893, 1944, 1945, 1946, 1947, 1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955, 1994, 1995, 1996, 1997, 1998, 1999, 2036, 2045, 2060, 2061, 2062, 2063, 2064, 2065, 2066, 2075, 2078, 2079, 2080, 2081, 2082, 2083, 2084, 2085, 2086, 2087, 2088, 2089, 2090, 2091, 2092, 2093, 2094, 2095, 2096, 2097, 2098, 2099, 2100, 2101, 2102, 2103, 2104, 2105, 2106, 2107, 2108, 2109, 2133, 2135, 2167, 2176, 2177, 2178, 2186, 2215, 2216, 2218, 2219, 2222, 2227, 2228, 2243, 2249, 2274, 2318, 2326, 2327, 2328 } },
                { "Ectoplasm", new ushort[] { 655, 656, 817, 822, 889, 890, 902, 907, 908, 909, 910, 911, 912, 914, 929, 1211, 1351, 1352, 1353, 1354, 1355, 1356, 1357, 1358, 1359, 1360, 1361, 1362, 1363, 1364, 1365, 1366, 1367, 1368, 1369, 1370, 1371, 1372, 1373, 1374, 1375, 1376, 1377, 1378, 1379, 1380, 1381, 1382, 1383, 1384, 1385, 1386, 1389, 1390, 1576, 1579, 1582, 1670, 1745, 1746, 1771, 1772, 1827, 1828, 1829, 1830, 1831, 1832, 1833, 1834, 1835, 1836, 1837, 1838, 1839, 1840, 1841, 1842, 1843, 1844, 1845, 1846, 1847, 1848, 1849, 1850, 1851, 1852, 1853, 1854, 1855, 1856, 1857, 1858, 1859, 1860, 1861, 1883, 1884, 1885, 1929, 2253, 2273, 2343, 2344, 2345 } },
                { "None", new ushort[] { 22, 24, 25, 26, 916, 920, 927, 938, 952, 1938, 2046, 2213, 2217, 2220, 2221, 2262, 1438, 1583, 1823, 1863, 1672, 1878, 1681, 1684, 1685, 1686, 1822, 1905, 1906, 1957, 1958, 1959, 2009, 2010, 2011, 2012, 2074, 2279 } }
            };

            foreach (var pair in bloodDict)
            {
                foreach (var id in pair.Value)
                {
                    ModelBloodTypes[id] = pair.Key;
                }
            }
        }
    }
}