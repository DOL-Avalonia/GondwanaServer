using System;
using DOL.Database;
using DOL.GS.ServerProperties;

namespace DOL.GS
{
    public class SmartUniqueItem : GeneratedUniqueItem
    {
        /// <summary>
        /// Factory Method: Creates a unique item based on what the Mob is wearing.
        /// </summary>
        public static ItemTemplate CreateFromMobItem(GameNPC mob, InventoryItem sourceItem, GamePlayer player)
        {
            // 1. Identify the item definition (Name, Slot, Type) from the Model Mapper
            LootModelMapper.ModelDefinition def;
            if (!LootModelMapper.TryGetDefinition(sourceItem.Model, out def))
            {
                return null;
            }

            // Initializes the dummy base item
            SmartUniqueItem item = new SmartUniqueItem();

            // 2. Set Lifecycle & Level Properties
            item.Level = mob.Level;
            if (item.Level > 51) item.Level = 51; // Cap at 51

            // Short ID to prevent DB truncation
            item.Id_nb = $"S_{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16)}";
            item.IsDropable = true;
            item.IsPickable = true;
            item.IsTradable = true;
            item.MaxCount = 1;
            item.PackSize = 1;

            // Retain original weight if known, otherwise standard fallback
            item.Weight = sourceItem.Weight > 0 ? sourceItem.Weight : 10;

            // Generate Quality
            item.GenerateItemQuality(0.0);

            // Calculate Exponential Custom Pricing
            // Curve: (Level / 51)^4.2 * 550,000 * Quality Modifier
            // Results in ~8 copper at Level 4, and ~50 Gold at Level 51
            double priceCurve = Math.Pow((double)item.Level / 51.0, 4.2);
            double maxPrice = 550000.0;
            item.Price = (long)(priceCurve * maxPrice * ((double)item.Quality / 100.0));
            if (item.Price < 1) item.Price = 1;

            // Set Condition/Durability
            int condition = item.Level * 1000;
            item.Condition = condition;
            item.MaxCondition = condition;
            item.Durability = condition;
            item.MaxDurability = condition;

            // 3. VISUALS & REALM CONVERSION
            item.Model = sourceItem.Model;
            item.Color = sourceItem.Color;
            item.Effect = sourceItem.Effect;
            item.Extension = sourceItem.Extension;

            item.Realm = (int)player.Realm;
            item.Item_Type = (int)def.Slot;

            // Translate ObjectType based on the mapping rules
            item.Object_Type = (int)GetConvertedObjectType(def.ObjType, player.Realm);

            // Set accurate Damage Type based on weapon rules and elemental names
            item.Type_Damage = GetDamageType((eObjectType)item.Object_Type, def.BaseName);

            // 4. Generate Stats
            item.charClass = (eCharacterClass)player.CharacterClass.ID;

            // Clear dummy base stats
            item.Bonus1 = item.Bonus2 = item.Bonus3 = item.Bonus4 = item.Bonus5 =
            item.Bonus6 = item.Bonus7 = item.Bonus8 = item.Bonus9 = item.Bonus10 = item.ExtraBonus = 0;
            item.Bonus1Type = item.Bonus2Type = item.Bonus3Type = item.Bonus4Type = item.Bonus5Type =
            item.Bonus6Type = item.Bonus7Type = item.Bonus8Type = item.Bonus9Type = item.Bonus10Type = item.ExtraBonusType = 0;

            // Calculate Base DPS/AF/SPD based on the converted ObjectType
            item.GenerateItemStats();

            bool isToa = Util.Chance(Properties.ROG_TOA_ITEM_CHANCE);
            item.GenerateMagicalBonuses(isToa);

            // USE OUR NEW CUSTOM PROC GENERATOR
            item.GenerateSmartProc();

            item.CapUtility(item.Level, 15);

            // 5. Naming Logic
            string mobName = mob.Name ?? "Unknown";
            if (mobName.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) mobName = mobName.Substring(4);
            else if (mobName.StartsWith("A ", StringComparison.OrdinalIgnoreCase)) mobName = mobName.Substring(2);
            else if (mobName.StartsWith("An ", StringComparison.OrdinalIgnoreCase)) mobName = mobName.Substring(3);

            string baseItemName = def.BaseName;

            // Cosmetic naming fix for Studded <-> Reinforced
            if (item.Object_Type == (int)eObjectType.Reinforced)
            {
                baseItemName = baseItemName.Replace("Studded", "Reinforced");
            }
            else if (item.Object_Type == (int)eObjectType.Studded)
            {
                baseItemName = baseItemName.Replace("Reinforced", "Studded");
            }

            item.Name = $"{mobName}'s {baseItemName}".Trim();

            // Add Magical Prefix
            int bestBonusLine = item.GetHighestUtilitySingleLine();
            eProperty bestProperty = GetPropertyFromBonusLine_Wrapper(item, bestBonusLine);

            if (hPropertyToMagicPrefix.ContainsKey(bestProperty))
            {
                string prefix = hPropertyToMagicPrefix[bestProperty];
                if (!string.IsNullOrEmpty(prefix))
                {
                    item.Name = $"{prefix} {item.Name}".Trim();
                }
            }

            // 6. Allow core DB insertion on pickup
            item.AllowAdd = true;

            return item;
        }

        /// <summary>
        /// Custom Proc Generator that bypasses the strict 1% chance in GeneratedUniqueItem
        /// </summary>
        private void GenerateSmartProc()
        {
            // ---> CONFIG: SET YOUR PROC CHANCE HERE <---
            // Defaulting to 25%. Change to 100 while testing so you can verify it works!
            if (!Util.Chance(15)) return;

            if (this.Object_Type == (int)eObjectType.Magical)
                return; // Magical items (cloaks, rings) don't get combat procs

            // Only Weapons, Shields, and Torso armors get procs
            bool isWeaponOrShield = (this.Object_Type >= (int)eObjectType._FirstWeapon && this.Object_Type <= (int)eObjectType._LastWeapon) || this.Object_Type == (int)eObjectType.Shield;
            bool isTorso = (this.Object_Type >= (int)eObjectType._FirstArmor && this.Object_Type <= (int)eObjectType._LastArmor && this.Item_Type == 25); // 25 is Slot.TORSO

            if (!isWeaponOrShield && !isTorso)
                return;

            this.ProcChance = 10; // 10% activation chance when used in combat

            if (isWeaponOrShield)
            {
                if (Util.Chance(50))
                {
                    // Lifetap Procs
                    if (this.Level < 10) this.ProcSpellID = 8010;
                    else if (this.Level < 15) this.ProcSpellID = 8011;
                    else if (this.Level < 20) this.ProcSpellID = 8012;
                    else if (this.Level < 25) this.ProcSpellID = 8013;
                    else if (this.Level < 30) this.ProcSpellID = 8014;
                    else if (this.Level < 35) this.ProcSpellID = 8015;
                    else if (this.Level < 40) this.ProcSpellID = 8016;
                    else this.ProcSpellID = 8017;
                }
                else
                {
                    // Direct Damage (DD) Procs
                    if (this.Level < 10) this.ProcSpellID = 8020;
                    else if (this.Level < 15) this.ProcSpellID = 8021;
                    else if (this.Level < 20) this.ProcSpellID = 8022;
                    else if (this.Level < 25) this.ProcSpellID = 8023;
                    else if (this.Level < 30) this.ProcSpellID = 8024;
                    else if (this.Level < 35) this.ProcSpellID = 8025;
                    else if (this.Level < 40) this.ProcSpellID = 8026;
                    else this.ProcSpellID = 8027;
                }
            }
            else if (isTorso)
            {
                if (Util.Chance(50))
                {
                    // Heal Procs
                    if (this.Level < 10) this.ProcSpellID = 8030;
                    else if (this.Level < 15) this.ProcSpellID = 8031;
                    else if (this.Level < 20) this.ProcSpellID = 8032;
                    else if (this.Level < 25) this.ProcSpellID = 8033;
                    else if (this.Level < 30) this.ProcSpellID = 8034;
                    else if (this.Level < 35) this.ProcSpellID = 8035;
                    else if (this.Level < 40) this.ProcSpellID = 8036;
                    else this.ProcSpellID = 8037;
                }
                else
                {
                    // Ablative / Absorb Procs
                    if (this.Level < 10) this.ProcSpellID = 8040;
                    else if (this.Level < 15) this.ProcSpellID = 8041;
                    else if (this.Level < 20) this.ProcSpellID = 8042;
                    else if (this.Level < 25) this.ProcSpellID = 8043;
                    else if (this.Level < 30) this.ProcSpellID = 8044;
                    else if (this.Level < 35) this.ProcSpellID = 8045;
                    else if (this.Level < 40) this.ProcSpellID = 8046;
                    else this.ProcSpellID = 8047;
                }
            }
        }

        /// <summary>
        /// Strictly maps weapon and armor Object Types based on player realm. 
        /// </summary>
        private static eObjectType GetConvertedObjectType(eObjectType originalType, eRealm playerRealm)
        {
            switch (originalType)
            {
                // --- CRUSHING WEAPONS ---
                case eObjectType.CrushingWeapon:
                case eObjectType.Hammer:
                case eObjectType.Blunt:
                    if (playerRealm == eRealm.Albion) return eObjectType.CrushingWeapon;
                    if (playerRealm == eRealm.Midgard) return eObjectType.Hammer;
                    if (playerRealm == eRealm.Hibernia) return eObjectType.Blunt;
                    break;

                // --- SLASHING WEAPONS ---
                case eObjectType.SlashingWeapon:
                case eObjectType.Sword:
                case eObjectType.Blades:
                    if (playerRealm == eRealm.Albion) return eObjectType.SlashingWeapon;
                    if (playerRealm == eRealm.Midgard) return eObjectType.Sword;
                    if (playerRealm == eRealm.Hibernia) return eObjectType.Blades;
                    break;

                // --- THRUSTING WEAPONS ---
                case eObjectType.ThrustWeapon:
                case eObjectType.Piercing:
                    if (playerRealm == eRealm.Albion) return eObjectType.ThrustWeapon;
                    if (playerRealm == eRealm.Hibernia) return eObjectType.Piercing;
                    break;

                // --- TWO-HANDED WEAPONS ---
                case eObjectType.TwoHandedWeapon:
                case eObjectType.LargeWeapons:
                    if (playerRealm == eRealm.Albion) return eObjectType.TwoHandedWeapon;
                    if (playerRealm == eRealm.Hibernia) return eObjectType.LargeWeapons;
                    break;

                // --- POLEARMS / SPEARS ---
                case eObjectType.PolearmWeapon:
                case eObjectType.Spear:
                case eObjectType.CelticSpear:
                    if (playerRealm == eRealm.Albion) return eObjectType.PolearmWeapon;
                    if (playerRealm == eRealm.Midgard) return eObjectType.Spear;
                    if (playerRealm == eRealm.Hibernia) return eObjectType.CelticSpear;
                    break;

                // --- BOWS ---
                case eObjectType.Longbow:
                case eObjectType.CompositeBow:
                case eObjectType.RecurvedBow:
                    if (playerRealm == eRealm.Albion) return eObjectType.Longbow;
                    if (playerRealm == eRealm.Midgard) return eObjectType.CompositeBow;
                    if (playerRealm == eRealm.Hibernia) return eObjectType.RecurvedBow;
                    break;

                // --- STUDDED / REINFORCED ---
                case eObjectType.Studded:
                case eObjectType.Reinforced:
                    if (playerRealm == eRealm.Hibernia) return eObjectType.Reinforced;
                    return eObjectType.Studded;
            }

            return originalType;
        }

        /// <summary>
        /// Analyzes the base weapon name and ObjectType to determine the exact DAoC damage type.
        /// </summary>
        private static int GetDamageType(eObjectType objType, string baseName)
        {
            string name = baseName.ToLower();

            // 1. Magical Element Overrides
            if (name.EndsWith("body") || name.Contains(" body")) return 10;
            if (name.EndsWith("water") || name.Contains(" water")) return 11;
            if (name.EndsWith("energy") || name.Contains(" energy")) return 12;
            if (name.EndsWith("fire") || name.Contains(" fire")) return 13;
            if (name.EndsWith("earth") || name.Contains(" earth")) return 14;
            if (name.EndsWith("air") || name.Contains(" air")) return 15;

            // 2. Flexible Weapons
            if (objType == eObjectType.Flexible)
            {
                if (name.Contains("mace") || name.Contains("crush") || name.Contains("blunt")) return 1;
                if (name.Contains("thrust") || name.Contains("dagger")) return 3;
                return 2;
            }

            // 3. Name-based Physical Overrides 
            if (name.Contains("hammer") || name.Contains("mace") || name.Contains("club") || name.Contains("shilelaugh") || name.Contains("maul"))
                return 1;
            if (name.Contains("spear") || name.Contains("pike") || name.Contains("fork") || name.Contains("trident"))
                return 3;
            if (name.Contains("sword") || name.Contains("axe") || name.Contains("scythe") || name.Contains("cleaver") || name.Contains("falcata") || name.Contains("scimitar"))
                return 2;

            // 4. Default Base Physical Mapping
            switch (objType)
            {
                case eObjectType.CrushingWeapon:
                case eObjectType.Hammer:
                case eObjectType.Blunt:
                    return 1;

                case eObjectType.SlashingWeapon:
                case eObjectType.Sword:
                case eObjectType.Blades:
                case eObjectType.Axe:
                case eObjectType.LeftAxe:
                case eObjectType.Scythe:
                case eObjectType.TwoHandedWeapon:
                case eObjectType.LargeWeapons:
                    return 2;

                case eObjectType.ThrustWeapon:
                case eObjectType.Piercing:
                case eObjectType.PolearmWeapon:
                case eObjectType.Spear:
                case eObjectType.CelticSpear:
                case eObjectType.Longbow:
                case eObjectType.CompositeBow:
                case eObjectType.RecurvedBow:
                case eObjectType.Crossbow:
                    return 3;
            }

            return 0;
        }

        // Helper to retrieve the actual Property enum from the Bonus Index
        private static eProperty GetPropertyFromBonusLine_Wrapper(SmartUniqueItem item, int bonusLine)
        {
            switch (bonusLine)
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

        public SmartUniqueItem() : base() { }
    }
}