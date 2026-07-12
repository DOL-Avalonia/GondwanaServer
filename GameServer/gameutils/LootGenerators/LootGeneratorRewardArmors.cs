using AmteScripts.PvP.Rewards;
using DOL.Database;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DOL.GS.Scripts
{
    public class LootGeneratorRewardArmors : LootGeneratorBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public override LootList GenerateLoot(GameObject mob, GameObject killer)
        {
            LootList loot = base.GenerateLoot(mob, killer);

            if (mob is RewardChest chest && killer is GamePlayer player)
            {
                try
                {
                    GeneratedUniqueItem generatedItem = BuildCustomClassItem(player, chest);

                    if (generatedItem != null)
                    {
                        string rarity = chest.ConfigRarityPrefix;
                        bool applyPattern = false;

                        if (chest.Tier == eRewardTier.RvRFinest)
                        {
                            if (rarity == "Mythical" || rarity == "Exalted" || rarity == "Fabled")
                            {
                                applyPattern = true;
                            }
                        }

                        else if (chest.Tier == eRewardTier.PvPTier1)
                        {
                            if (rarity == "Legendary" || rarity == "Eminent" || rarity == "Illustrious")
                            {
                                applyPattern = true;
                            }
                        }

                        // Apply only to armor types (Cloth=32, Leather=33, Studded=34, Chain=35, Plate=36, Reinforced=37, Scale=38)
                        if (applyPattern && generatedItem.Object_Type >= 32 && generatedItem.Object_Type <= 38)
                        {
                            InventoryItem dummyItem = new InventoryItem
                            {
                                Item_Type = generatedItem.Item_Type,
                                Object_Type = generatedItem.Object_Type,
                                Realm = generatedItem.Realm
                            };

                            List<ArmorPatternMgr.PatternType> validPatterns = new List<ArmorPatternMgr.PatternType>();

                            foreach (ArmorPatternMgr.PatternType pt in Enum.GetValues(typeof(ArmorPatternMgr.PatternType)))
                            {
                                if (pt == ArmorPatternMgr.PatternType.None) continue;

                                if (ArmorPatternMgr.GetModel(pt, dummyItem) != -1)
                                {
                                    validPatterns.Add(pt);
                                }
                            }

                            if (validPatterns.Count > 0)
                            {
                                ArmorPatternMgr.PatternType chosenPattern = validPatterns[Util.Random(0, validPatterns.Count - 1)];
                                generatedItem.Model = ArmorPatternMgr.GetModel(chosenPattern, dummyItem);
                            }
                        }

                        generatedItem.AllowAdd = true;
                        GameServer.Database.AddObject(generatedItem);
                        loot.AddFixed(generatedItem, 1);

                        string translatedName = LanguageMgr.GetItemNameMessage(player.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage, generatedItem.Name);

                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client!.Account.Language, "Lootgenerator.RewardChests.YouGetArmament", translatedName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        SendFormattedItemNotification(player, generatedItem, rarity, translatedName);
                    }
                }
                catch (Exception e)
                {
                    log.Error($"Error generating custom armor/weapon for {player.Name}: ", e);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Lootgenerator.RewardChests.ChestEmpty"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }

            return loot;
        }

        private GeneratedUniqueItem BuildCustomClassItem(GamePlayer player, RewardChest chest)
        {
            eCharacterClass cClass = (eCharacterClass)player.CharacterClass.ID;
            eRealm realm = player.Realm;
            byte level = 51;

            int regionID = chest.ConfigRegionID;

            bool isArmor = Util.Chance(50);
            eObjectType objType;

            if (isArmor)
            {
                objType = realm switch
                {
                    eRealm.Albion => GeneratedUniqueItem.GetAlbionArmorType(cClass, level),
                    eRealm.Midgard => GeneratedUniqueItem.GetMidgardArmorType(cClass, level),
                    eRealm.Hibernia => GeneratedUniqueItem.GetHiberniaArmorType(cClass, level),
                    _ => eObjectType.Cloth
                };
            }
            else
            {
                objType = realm switch
                {
                    eRealm.Albion => GeneratedUniqueItem.GetAlbionWeapon(cClass),
                    eRealm.Midgard => GeneratedUniqueItem.GetMidgardWeapon(cClass),
                    eRealm.Hibernia => GeneratedUniqueItem.GetHiberniaWeapon(cClass),
                    _ => eObjectType.Staff
                };
            }

            if (objType == eObjectType.GenericItem)
                objType = isArmor ? eObjectType.Leather : eObjectType.Sword;

            eInventorySlot slot = GeneratedUniqueItem.GenerateItemType(objType);

            string lang = player.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;

            GeneratedUniqueItem item = new GeneratedUniqueItem(false, realm, cClass, level, objType, slot, regionID, lang);

            item.Bonus1 = item.Bonus2 = item.Bonus3 = item.Bonus4 = item.Bonus5 = 0;
            item.Bonus6 = item.Bonus7 = item.Bonus8 = item.Bonus9 = item.Bonus10 = item.ExtraBonus = 0;
            item.Bonus1Type = item.Bonus2Type = item.Bonus3Type = item.Bonus4Type = item.Bonus5Type = 0;
            item.Bonus6Type = item.Bonus7Type = item.Bonus8Type = item.Bonus9Type = item.Bonus10Type = item.ExtraBonusType = 0;

            // Fills bonuses using Chest Utility config mapping
            FillClassSpecificBonuses(item, player, chest);
            GenerateBonusConditions(item, chest.Tier);

            item.Quality = 100;
            item.Condition = 100000;
            item.MaxCondition = 100000;
            item.Durability = 100000;
            item.MaxDurability = 100000;
            item.IsTradable = false;

            item.Color = chest.ConfigItemColor;

            string baseNameKey = item.Name;

            if (baseNameKey.StartsWith("[ROG]|"))
            {
                string[] parts = baseNameKey.Substring(6).Split('|');
                if (parts.Length >= 2)
                    baseNameKey = parts[1];
                else if (parts.Length == 1)
                    baseNameKey = parts[0];
            }
            else if (baseNameKey.StartsWith("[ROG]"))
            {
                string[] parts = baseNameKey.Substring(5).Split('|');
                if (parts.Length >= 3)
                    baseNameKey = parts[2];
                else if (parts.Length == 2)
                    baseNameKey = parts[1];
                else if (parts.Length == 1)
                    baseNameKey = parts[0];
            }

            item.Name = $"[ROG]|{chest.ConfigRarityPrefix}|{baseNameKey}";

            try
            {
                var namedField = typeof(GeneratedUniqueItem).GetField("m_named", BindingFlags.NonPublic | BindingFlags.Instance);
                namedField?.SetValue(item, false);

                int bestLine = item.GetHighestUtilitySingleLine();
                eProperty bestProp = GetPropertyFromBonusLine(item, bestLine);
                item.WriteMagicalName(bestProp);
            }
            catch (Exception ex)
            {
                log.Error("Reflection failed while updating Magic Prefix: ", ex);
            }

            return item;
        }

        private eProperty GetPropertyFromBonusLine(GeneratedUniqueItem item, int BonusLine)
        {
            switch (BonusLine)
            {
                case 1: return (eProperty)item.Bonus1Type;
                case 2: return (eProperty)item.Bonus2Type;
                case 3: return (eProperty)item.Bonus3Type;
                case 4: return (eProperty)item.Bonus4Type;
                case 5: return (eProperty)item.Bonus5Type;
                case 6: return (eProperty)item.Bonus6Type;
                case 7: return (eProperty)item.Bonus7Type;
                case 8: return (eProperty)item.Bonus8Type;
                case 9: return (eProperty)item.Bonus9Type;
                case 10: return (eProperty)item.Bonus10Type;
                case 11: return (eProperty)item.ExtraBonusType;
                default: return eProperty.Undefined;
            }
        }

        private void FillClassSpecificBonuses(GeneratedUniqueItem item, GamePlayer player, RewardChest chest)
        {
            int targetSlots = chest.ConfigTargetSlots;
            double targetMaxUti = chest.ConfigTargetMaxUti;
            int regionID = chest.ConfigRegionID;

            eRegionCategory regionCat = RegionMapper.GetCategoryFromRegionID(regionID);
            double currentUti = 0;
            HashSet<eProperty> usedProps = new HashSet<eProperty>();
            List<eProperty> guaranteedProps = new List<eProperty>();

            try
            {
                var getWeightedStat = typeof(GeneratedUniqueItem).GetMethod("GetWeightedStatForClass", BindingFlags.NonPublic | BindingFlags.Instance);
                if (getWeightedStat != null)
                {
                    object statResult = getWeightedStat.Invoke(item, new object[] { (eCharacterClass)player.CharacterClass.ID });
                    if (statResult is eProperty mainStat && mainStat != eProperty.Undefined)
                    {
                        guaranteedProps.Add(mainStat);
                    }
                }

                var getClassSkill = typeof(GeneratedUniqueItem).GetMethod("GetClassSpecificSkill", BindingFlags.NonPublic | BindingFlags.Instance);
                if (getClassSkill != null)
                {
                    object skillResult = getClassSkill.Invoke(item, null);
                    if (skillResult is eProperty mainSkill && mainSkill != eProperty.Undefined && !guaranteedProps.Contains(mainSkill))
                    {
                        guaranteedProps.Add(mainSkill);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Reflection failed while getting Base Stats/Skills: ", ex);
            }

            if (item.Object_Type == (int)eObjectType.Staff && player.CharacterClass.ID != (int)eCharacterClass.Friar)
            {
                if (!guaranteedProps.Contains(eProperty.AllFocusLevels))
                    guaranteedProps.Insert(0, eProperty.AllFocusLevels);
            }

            int currentSlot = 1;

            foreach (eProperty prop in guaranteedProps)
            {
                if (currentSlot > targetSlots) break;
                if (!usedProps.Contains(prop) && item.IsPropertyAllowed(prop))
                {
                    AssignValueToProp(item, prop, ref currentUti, targetMaxUti, targetSlots, currentSlot);
                    usedProps.Add(prop);
                    currentSlot++;
                }
            }

            for (int i = 1; i <= targetSlots * 3 && currentSlot <= targetSlots; i++)
            {
                eProperty prop = eProperty.Undefined;
                GeneratedUniqueItem.ePropertyPool pool = GeneratedUniqueItem.GetPoolForSlot(regionCat, currentSlot, 99, HasSkill(usedProps), true);
                eProperty[] poolProps = GeneratedUniqueItem.GetPropertiesFromPool(pool);

                prop = GetRandomAllowedProperty(item, poolProps, usedProps);

                if (prop != eProperty.Undefined)
                {
                    AssignValueToProp(item, prop, ref currentUti, targetMaxUti, targetSlots, currentSlot);
                    usedProps.Add(prop);
                    currentSlot++;
                }
            }

            try
            {
                var reorderMethod = typeof(GeneratedUniqueItem).GetMethod("ReorderBonuses", BindingFlags.NonPublic | BindingFlags.Instance);
                reorderMethod?.Invoke(item, null);
            }
            catch (Exception ex)
            {
                log.Error("Reflection failed while Reordering Bonuses: ", ex);
            }
        }

        private void AssignValueToProp(GeneratedUniqueItem item, eProperty prop, ref double currentUti, double targetMaxUti, int targetSlots, int currentSlot)
        {
            double remainingUti = targetMaxUti - currentUti;
            if (remainingUti <= 0) remainingUti = 5; // Safety margin

            double expectedUti = remainingUti / Math.Max(1, targetSlots - currentSlot + 1);
            double maxUtiForThisSlot = (currentSlot == targetSlots) ? remainingUti : expectedUti * 1.5;

            int chosenVal = 1;
            int maxCap = GetMaxCapForProperty(prop);

            for (int v = maxCap; v >= 1; v--)
            {
                double uti = GeneratedUniqueItem.GetSingleUtility((int)prop, v);
                if (currentUti + uti <= targetMaxUti && (uti <= maxUtiForThisSlot || v == 1))
                {
                    chosenVal = v;
                    break;
                }
            }

            SetBonus(item, currentSlot, prop, chosenVal);
            currentUti += GeneratedUniqueItem.GetSingleUtility((int)prop, chosenVal);
        }

        private eProperty GetRandomAllowedProperty(GeneratedUniqueItem item, eProperty[] pool, HashSet<eProperty> used)
        {
            for (int i = 0; i < 50; i++)
            {
                eProperty prop = pool[Util.Random(0, pool.Length - 1)];
                if (prop != eProperty.Undefined && !used.Contains(prop) && item.IsPropertyAllowed(prop) && !item.HasBonus(prop))
                {
                    return prop;
                }
            }
            return eProperty.Undefined;
        }

        private bool HasSkill(HashSet<eProperty> used)
        {
            foreach (var p in used)
            {
                int propInt = (int)p;
                if ((propInt >= 20 && propInt <= 58) || (propInt >= 60 && propInt <= 70) || (propInt >= 72 && propInt <= 115) || (propInt >= 163 && propInt <= 165) || (propInt >= 270 && propInt <= 309) || propInt == 167 || propInt == 168 || propInt == 213)
                    return true;
            }
            return false;
        }

        private void SetBonus(GeneratedUniqueItem item, int index, eProperty prop, int val)
        {
            switch (index)
            {
                case 1: item.Bonus1 = val; item.Bonus1Type = (int)prop; break;
                case 2: item.Bonus2 = val; item.Bonus2Type = (int)prop; break;
                case 3: item.Bonus3 = val; item.Bonus3Type = (int)prop; break;
                case 4: item.Bonus4 = val; item.Bonus4Type = (int)prop; break;
                case 5: item.Bonus5 = val; item.Bonus5Type = (int)prop; break;
                case 6: item.Bonus6 = val; item.Bonus6Type = (int)prop; break;
                case 7: item.Bonus7 = val; item.Bonus7Type = (int)prop; break;
                case 8: item.Bonus8 = val; item.Bonus8Type = (int)prop; break;
                case 9: item.Bonus9 = val; item.Bonus9Type = (int)prop; break;
                case 10: item.Bonus10 = val; item.Bonus10Type = (int)prop; break;
                case 11: item.ExtraBonus = val; item.ExtraBonusType = (int)prop; break;
            }
        }

        private int GetMaxCapForProperty(eProperty prop)
        {
            int propInt = (int)prop;
            if (propInt >= 1 && propInt <= 8 || propInt == 156) return 28;
            if (propInt == 10) return 88;
            if (propInt == 9 || propInt == 196) return 22;
            if (propInt >= 11 && propInt <= 19) return 11;
            if (propInt >= 201 && propInt <= 209) return 7;
            if (propInt >= 210 && propInt <= 212) return 88;
            if (propInt >= 214 && propInt <= 222) return 5;
            if (propInt >= 223 && propInt <= 231) return 5;
            if (propInt == (int)eProperty.AllFocusLevels) return 50;
            if ((propInt >= 163 && propInt <= 165) || propInt == 167 || propInt == 168 || propInt == 213) return 4;
            if (propInt >= 20 && propInt <= 115) return 4;
            if (propInt >= 116 && propInt <= 162) return 5;
            if (propInt >= 169 && propInt <= 194) return 5;
            return 10;
        }

        private void GenerateBonusConditions(GeneratedUniqueItem item, eRewardTier tier)
        {
            bool reqRen8 = false;
            bool reqRen9 = false;
            bool reqRen10 = false;
            int reqChampExtra = 0;

            switch (tier)
            {
                case eRewardTier.RvRFinest:
                    reqRen9 = true;
                    reqRen10 = true;
                    reqChampExtra = 2;
                    break;
                case eRewardTier.PvPTier1:
                    reqRen9 = true;
                    reqRen10 = true;
                    break;
                case eRewardTier.PvPTier2:
                case eRewardTier.PvPTier3:
                    reqRen8 = true;
                    break;
            }

            List<string> elements = new List<string>();
            string template = "{{\"BonusName\":\"{0}\",\"ChampionLevel\":{1},\"MlLevel\":0,\"IsRenaissanceRequired\":{2}}}";

            if (item.Bonus1Type > 0) elements.Add(string.Format(template, "Bonus1", 0, "false"));
            if (item.Bonus2Type > 0) elements.Add(string.Format(template, "Bonus2", 0, "false"));
            if (item.Bonus3Type > 0) elements.Add(string.Format(template, "Bonus3", 0, "false"));
            if (item.Bonus4Type > 0) elements.Add(string.Format(template, "Bonus4", 0, "false"));
            if (item.Bonus5Type > 0) elements.Add(string.Format(template, "Bonus5", 0, "false"));
            if (item.Bonus6Type > 0) elements.Add(string.Format(template, "Bonus6", 0, "false"));
            if (item.Bonus7Type > 0) elements.Add(string.Format(template, "Bonus7", 0, "false"));
            if (item.Bonus8Type > 0) elements.Add(string.Format(template, "Bonus8", 0, reqRen8 ? "true" : "false"));
            if (item.Bonus9Type > 0) elements.Add(string.Format(template, "Bonus9", 0, reqRen9 ? "true" : "false"));
            if (item.Bonus10Type > 0) elements.Add(string.Format(template, "Bonus10", 0, reqRen10 ? "true" : "false"));
            if (item.ExtraBonusType > 0) elements.Add(string.Format(template, "ExtraBonus", reqChampExtra, "false"));
            if (item.ProcSpellID > 0) elements.Add(string.Format(template, "ProcSpellID", 0, "false"));
            if (item.ProcSpellID1 > 0) elements.Add(string.Format(template, "ProcSpellID1", 0, "false"));

            item.BonusConditions = "[" + string.Join(",", elements) + "]";
        }

        private string GetArchetype(eCharacterClass c)
        {
            switch (c)
            {
                case eCharacterClass.Wizard:
                case eCharacterClass.Theurgist:
                case eCharacterClass.Cabalist:
                case eCharacterClass.Sorcerer:
                case eCharacterClass.Enchanter:
                case eCharacterClass.Eldritch:
                case eCharacterClass.Mentalist:
                case eCharacterClass.Animist:
                case eCharacterClass.Runemaster:
                case eCharacterClass.Spiritmaster:
                case eCharacterClass.Bonedancer:
                    return "Magical/Mage";
                case eCharacterClass.Scout:
                case eCharacterClass.Hunter:
                case eCharacterClass.Ranger:
                    return "Archery/Ranged";
                case eCharacterClass.Infiltrator:
                case eCharacterClass.Nightshade:
                case eCharacterClass.Shadowblade:
                    return "Stealth";
                case eCharacterClass.Armsman:
                case eCharacterClass.Mercenary:
                case eCharacterClass.Berserker:
                case eCharacterClass.Savage:
                case eCharacterClass.Warrior:
                case eCharacterClass.Blademaster:
                case eCharacterClass.Hero:
                case eCharacterClass.Champion:
                    return "Offensive";
                default:
                    return "Hybrid";
            }
        }

        private void SendFormattedItemNotification(GamePlayer player, GeneratedUniqueItem item, string rarity, string translatedName)
        {
            string slotName = ((eInventorySlot)item.Item_Type).ToString().Replace("Armor", "");
            string archetype = GetArchetype((eCharacterClass)player.CharacterClass.ID);

            if (player.Client?.Account?.PrivLevel > 1)
            {
                player.Out.SendMessage($"[{translatedName}] (Level {item.Level} | Rarity: {rarity} | Class Type: {archetype} | Slot: {slotName})", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            PrintBonusLine(player, 1, item.Bonus1Type, item.Bonus1);
            PrintBonusLine(player, 2, item.Bonus2Type, item.Bonus2);
            PrintBonusLine(player, 3, item.Bonus3Type, item.Bonus3);
            PrintBonusLine(player, 4, item.Bonus4Type, item.Bonus4);
            PrintBonusLine(player, 5, item.Bonus5Type, item.Bonus5);
            PrintBonusLine(player, 6, item.Bonus6Type, item.Bonus6);
            PrintBonusLine(player, 7, item.Bonus7Type, item.Bonus7);
            PrintBonusLine(player, 8, item.Bonus8Type, item.Bonus8);
            PrintBonusLine(player, 9, item.Bonus9Type, item.Bonus9);
            PrintBonusLine(player, 10, item.Bonus10Type, item.Bonus10);
            PrintBonusLine(player, 11, item.ExtraBonusType, item.ExtraBonus);
        }

        private void PrintBonusLine(GamePlayer player, int bonusNumber, int type, int value)
        {
            if (type == 0 || value == 0) return;
            eProperty prop = (eProperty)type;

            if (player.Client?.Account?.PrivLevel > 1)
            {
                player.Out.SendMessage($"Bonus {bonusNumber} : {prop.ToString()} (Bonus type {type}) : {value} pts", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
    }
}