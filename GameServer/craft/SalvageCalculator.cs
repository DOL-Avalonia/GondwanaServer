using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DOL.Database;
using DOL.GS;
using DOL.GS.ServerProperties;
using log4net;
using System.Reflection;

namespace DOL.GS.SalvageCalc
{
    public struct SalvageYieldEntry
    {
        public string ID;
        public int Count;
        public bool IsPrimary;
    }

    public struct SalvageReturn
    {
        public string ID;
        public string Material;
        public int Count;
        public int Tier;
        public int Level;
        public int Skill;
        public List<SalvageYieldEntry> Yields;
    }

    public class SalvageCalculator
    {
        protected static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private const double SECONDARY_RATIO = 0.25;        // secondary residues = 1/4 of primary
        private const double SECOND_PRIMARY_RATIO = 0.50;   // second primary (unspecified cases) = 1/2 of main primary
        private const double SHIELD_WOOD_SHARE = 0.55;      // shields: slightly more wood than metal
        private const double SCYTHE_METAL_SHARE = 0.55;     // scythes: slightly more metal than wood
        private const double FISTWRAP_LEATHER_RATIO = 0.90; // fist wraps: 10% less leather than metal
        private const double SPEAR_WOOD_RATIO = 0.50;       // spears/polearms: wood = ~50% of metal
        private const double ROG_BONUS_MIN = 0.20;          // +20% minimum
        private const double ROG_BONUS_MAX = 0.30;          // +30% maximum

        #region Material tables
        private static readonly string[] ClothList = { "woolen_cloth_squares", "linen_cloth_squares", "brocade_cloth_squares", "silk_cloth_squares", "gossamer_cloth_squares", "sylvan_cloth_squares", "seamist_cloth_squares", "nightshade_cloth_squares", "wyvernskin_cloth_squares", "silksteel_cloth_squares" };
        private static readonly string[] LeatherList = { "rawhide_leather_squares", "tanned_leather_squares", "cured_leather_squares", "hard_leather_squares", "rigid_leather_squares", "embossed_leather_squares", "imbued_leather_squares", "runed_leather_squares", "eldritch_leather_squares", "tempered_leather_squares" };
        private static readonly string[] StripsHibList = { "leaf_strips", "bone_strips", "vine_strips", "shell_strips", "fossil_strips", "amber_strips", "coral_strips", "chitin_strips", "petrified_strips", "crystalized_strips" };
        private static readonly string[] WoodList = { "rowan_wooden_boards", "elm_wooden_boards", "oaken_wooden_boards", "ironwood_wooden_boards", "heartwood_wooden_boards", "runewood_wooden_boards", "stonewood_wooden_boards", "ebonwood_wooden_boards", "dyrwood_wooden_boards", "duskwood_wooden_boards" };
        private static readonly string[] MetalList = { "bronze_metal_bars", "iron_metal_bars", "steel_metal_bars", "alloy_metal_bars", "fine_alloy_metal_bars", "mithril_metal_bars", "adamantium_metal_bars", "asterite_metal_bars", "netherium_metal_bars", "arcanium_metal_bars" };
        private static readonly string[] MetalHibList = { "copper_metal_bars", "ferrite_metal_bars", "quartz_metal_bars", "dolomite_metal_bars", "cobalt_metal_bars", "carbide_metal_bars", "sapphire_metal_bars", "diamond_metal_bars", "netherite_metal_bars", "arcanite_metal_bars" };
        private static readonly string[] ThreadList = { "woolen_heavy_thread", "linen_heavy_thread", "brocade_heavy_thread", "silk_heavy_thread", "gossamer_heavy_thread", "sylvan_heavy_thread", "seamist_heavy_thread", "nightshade_heavy_thread", "wyvernskin_heavy_thread", "silksteel_heavy_thread" };
        private static readonly string[] JewelList = { "Alexandrite", "Jade", "Water_Opal", "Rhodolite", "Peridot", "Yellow_Tourmaline", "Kornerupine", "Purple_Sapphire", "Chrysoberyl", "Black_Sapphire" };
        #endregion

        public SalvageReturn GetSalvage(GamePlayer Player, InventoryItem Item)
        {
            SalvageReturn yield = new SalvageReturn();
            yield.ID = string.Empty;
            yield.Count = 0;
            yield.Yields = new List<SalvageYieldEntry>();

            if (Item == null) return yield;

            string idnbGuard = Item.Id_nb ?? string.Empty;
            string nameGuard = Item.Name ?? string.Empty;
            if (idnbGuard.StartsWith("vt_") || idnbGuard.StartsWith("vf_") || idnbGuard.StartsWith("empty_vial") ||
                nameGuard.StartsWith("[ROG]BloodVial") || nameGuard.StartsWith("[ROG]PlayerVial"))
            {
                return yield;
            }

            int objectType = Item.Object_Type;
            string description = Item.Description ?? string.Empty;
            string name = Item.Name ?? string.Empty;
            int itemType = Item.Item_Type;

            yield.Level = Item.Level;
            yield.Tier = Math.Min(9, Math.Max(0, Item.Level / 5));
            int tier = yield.Tier;

            eRealm realm = (eRealm)Item.Realm;
            if (realm == eRealm.None) realm = (eRealm)(Player != null ? Player.Realm : eRealm.Albion);
            if (realm == eRealm.None) realm = eRealm.Albion;

            string rogPrefix = Properties.ROG_SERVER_NAME ?? "Global ROG";
            bool isRog = (!string.IsNullOrEmpty(description) && (description.Contains(rogPrefix) || description.Contains("ROG"))) ||
                         (!string.IsNullOrEmpty(name) && name.StartsWith("[ROG]"));

            bool isCloak = objectType == (int)eObjectType.Magical && itemType == (int)eInventorySlot.Cloak;
            bool isJewel = objectType == (int)eObjectType.Magical && !isCloak;

            int baseCount = 0;
            if (isJewel)
            {
                baseCount = 1;
            }
            else
            {
                switch ((eObjectType)objectType)
                {
                    case eObjectType.RecurvedBow:
                    case eObjectType.CompositeBow:
                    case eObjectType.Longbow:
                    case eObjectType.Crossbow:
                    case eObjectType.Staff:
                    case eObjectType.Fired:
                    case eObjectType.MaulerStaff:
                    case eObjectType.TwoHandedWeapon:
                    case eObjectType.PolearmWeapon:
                    case eObjectType.LargeWeapons:
                    case eObjectType.CelticSpear:
                    case eObjectType.Scythe:
                    case eObjectType.Spear:
                        baseCount = Item.DPS_AF > 520 ? 15 : 10;
                        break;

                    case eObjectType.CrushingWeapon:
                    case eObjectType.SlashingWeapon:
                    case eObjectType.ThrustWeapon:
                    case eObjectType.Flexible:
                    case eObjectType.Blades:
                    case eObjectType.Blunt:
                    case eObjectType.Piercing:
                    case eObjectType.Sword:
                    case eObjectType.Hammer:
                    case eObjectType.LeftAxe:
                    case eObjectType.Axe:
                    case eObjectType.HandToHand:
                    case eObjectType.FistWraps:
                    case eObjectType.Thrown:
                        baseCount = Item.DPS_AF > 520 ? 8 : 5;
                        break;

                    case eObjectType.Shield:
                    case eObjectType.Instrument:
                        baseCount = Item.Type_Damage == 3 ? 12 : (Item.Type_Damage == 2 ? 8 : 5);
                        break;

                    case eObjectType.Cloth:
                    case eObjectType.Leather:
                    case eObjectType.Reinforced:
                    case eObjectType.Studded:
                    case eObjectType.Scale:
                    case eObjectType.Chain:
                    case eObjectType.Plate:
                        switch (itemType)
                        {
                            case (int)eInventorySlot.HeadArmor: baseCount = 12; break;
                            case (int)eInventorySlot.TorsoArmor: baseCount = 17; break;
                            case (int)eInventorySlot.LegsArmor: baseCount = 15; break;
                            case (int)eInventorySlot.ArmsArmor: baseCount = 10; break;
                            case (int)eInventorySlot.HandsArmor: baseCount = 6; break;
                            case (int)eInventorySlot.FeetArmor: baseCount = 5; break;
                            default: baseCount = 5; break;
                        }
                        break;

                    case eObjectType.Magical:
                        if (isCloak) baseCount = 4;
                        break;
                }
            }

            // Quality & Price adjustments (exclude ROGs from price scaling)
            if (!isJewel)
            {
                int toadd = 0;
                if (Item.Quality > 97 && !Item.IsCrafted)
                    toadd += (Item.Quality - 97) * 3;

                if (Item.Price > 300000 && !Item.IsCrafted && !isRog)
                    toadd += (int)(Item.Price / 100000);

                baseCount += toadd;

                if (Item.Condition < Item.MaxCondition && Item.MaxCondition > 0)
                    baseCount = (int)(baseCount * ((double)Item.Condition / Item.MaxCondition));
            }

            if (!isRog && Item.Bonus8 > 0 && Item.Bonus8Type == 0)
                baseCount = Item.Bonus8;

            if (isRog)
                baseCount = Math.Max(2, baseCount / 2);

            if (baseCount < 1) baseCount = 1;
            if (baseCount > 500) baseCount = 500;

            if (isJewel)
            {
                AddYield(yield.Yields, JewelList[tier], 1, true);
            }
            else if (isRog)
            {
                BuildRogComposition(yield.Yields, Item, realm, tier, baseCount, isCloak);

                for (int i = 0; i < yield.Yields.Count; i++)
                {
                    var e = yield.Yields[i];
                    e.Count = ApplyRogBonus(e.Count);
                    yield.Yields[i] = e;
                }
            }
            else
            {
                AddYield(yield.Yields, GetLegacyMaterial((eObjectType)objectType, itemType, realm, tier), baseCount, true);
            }

            foreach (var e in yield.Yields)
            {
                if (e.IsPrimary) { yield.ID = e.ID; yield.Count = e.Count; break; }
            }
            if (string.IsNullOrEmpty(yield.ID) && yield.Yields.Count > 0)
            {
                yield.ID = yield.Yields[0].ID;
                yield.Count = yield.Yields[0].Count;
            }

            yield.Skill = (tier * 100) + 15;
            yield.Material = string.IsNullOrEmpty(yield.ID) ? string.Empty : Regex.Replace(yield.ID.Replace("_", " "), @"[\d-]", string.Empty);

            return yield;
        }

        #region Composition builders

        private static void AddYield(List<SalvageYieldEntry> list, string id, int count, bool primary)
        {
            if (string.IsNullOrEmpty(id) || count < 1) return;
            list.Add(new SalvageYieldEntry { ID = id, Count = count, IsPrimary = primary });
        }

        /// <summary>Secondary residue: 1/4 of primary. Below 0.3 -> 0, between 0.3 and 1 -> 1, else rounded.</summary>
        private static int ScaleSecondary(int primary)
        {
            double raw = primary * SECONDARY_RATIO;
            if (raw < 0.3) return 0;
            if (raw <= 1.0) return 1;
            return (int)Math.Round(raw);
        }

        private static int HalfPrimary(int primary)
        {
            return Math.Max(1, (int)Math.Round(primary * SECOND_PRIMARY_RATIO));
        }

        private void BuildRogComposition(List<SalvageYieldEntry> list, InventoryItem item, eRealm realm, int tier, int baseCount, bool isCloak)
        {
            string cloth = ClothList[tier];
            string leather = LeatherList[tier];
            string wood = WoodList[tier];
            string thread = ThreadList[tier];
            string stripsHib = StripsHibList[tier];
            string metalStd = MetalList[tier];
            string metalHib = MetalHibList[tier];
            string metalWeapon = (realm == eRealm.Hibernia) ? metalHib : metalStd;

            eObjectType type = (eObjectType)item.Object_Type;
            bool twoHanded = item.Hand == 1 || item.Item_Type == (int)eInventorySlot.TwoHandWeapon;

            if (isCloak)
            {
                AddYield(list, cloth, baseCount, true);
                return;
            }

            switch (type)
            {
                case eObjectType.Cloth:
                    AddYield(list, cloth, baseCount, true);
                    AddYield(list, thread, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Leather:
                    AddYield(list, leather, baseCount, true);
                    AddYield(list, thread, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Studded: // Alb/Mid: leather primary + thread & metal residues
                    AddYield(list, leather, baseCount, true);
                    AddYield(list, thread, ScaleSecondary(baseCount), false);
                    AddYield(list, metalStd, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Reinforced: // Hib: strips + leather primaries, thread residue
                    AddYield(list, stripsHib, baseCount, true);
                    AddYield(list, leather, HalfPrimary(baseCount), true);
                    AddYield(list, thread, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Scale: // Hib: hib metal + leather primaries, thread residue
                    AddYield(list, metalHib, baseCount, true);
                    AddYield(list, leather, HalfPrimary(baseCount), true);
                    AddYield(list, thread, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Chain: // Alb/Mid: std metal + leather primaries, thread residue
                    AddYield(list, metalStd, baseCount, true);
                    AddYield(list, leather, HalfPrimary(baseCount), true);
                    AddYield(list, thread, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Plate: // Alb: std metal + cloth primaries, thread & leather residues
                    AddYield(list, metalStd, baseCount, true);
                    AddYield(list, cloth, HalfPrimary(baseCount), true);
                    AddYield(list, thread, ScaleSecondary(baseCount), false);
                    AddYield(list, leather, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.SlashingWeapon:
                case eObjectType.ThrustWeapon:
                case eObjectType.Sword:
                case eObjectType.Blades:
                case eObjectType.Piercing:
                case eObjectType.LeftAxe:
                case eObjectType.Thrown:
                    AddYield(list, metalWeapon, baseCount, true);
                    break;

                case eObjectType.Axe:
                    AddYield(list, metalWeapon, baseCount, true);
                    if (twoHanded) AddYield(list, wood, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.CrushingWeapon:
                case eObjectType.Hammer:
                    AddYield(list, metalWeapon, baseCount, true);
                    AddYield(list, wood, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Blunt: // Hibernia clubs: wood primary, metal residue
                    AddYield(list, wood, baseCount, true);
                    AddYield(list, metalWeapon, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.TwoHandedWeapon: // Alb 2H: crush variants behave like hammers
                case eObjectType.LargeWeapons:    // Hib LW: crush variants behave like great hammers
                    AddYield(list, metalWeapon, baseCount, true);
                    if (item.Type_Damage == (int)eDamageType.Crush)
                        AddYield(list, wood, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Flexible:
                    AddYield(list, metalWeapon, baseCount, true);
                    AddYield(list, leather, ScaleSecondary(baseCount), false);
                    AddYield(list, wood, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Shield:
                    {
                        int woodC = Math.Max(1, (int)Math.Ceiling(baseCount * SHIELD_WOOD_SHARE));
                        int metalC = Math.Max(1, baseCount - woodC);
                        AddYield(list, wood, woodC, true);
                        AddYield(list, metalWeapon, metalC, true);
                    }
                    break;

                case eObjectType.Scythe:
                    {
                        int metalC = Math.Max(1, (int)Math.Ceiling(baseCount * SCYTHE_METAL_SHARE));
                        int woodC = Math.Max(1, baseCount - metalC);
                        AddYield(list, metalWeapon, metalC, true);
                        AddYield(list, wood, woodC, true);
                    }
                    break;

                case eObjectType.Spear:
                case eObjectType.CelticSpear:
                case eObjectType.PolearmWeapon:
                    AddYield(list, metalWeapon, baseCount, true);
                    AddYield(list, wood, Math.Max(1, (int)Math.Round(baseCount * SPEAR_WOOD_RATIO)), true);
                    break;

                case eObjectType.FistWraps:
                    AddYield(list, metalWeapon, baseCount, true);
                    AddYield(list, leather, Math.Max(1, (int)Math.Round(baseCount * FISTWRAP_LEATHER_RATIO)), true);
                    break;

                case eObjectType.HandToHand:
                    AddYield(list, metalWeapon, baseCount, true);
                    AddYield(list, leather, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Staff:
                    AddYield(list, wood, baseCount, true);
                    break;

                case eObjectType.MaulerStaff:
                    AddYield(list, wood, baseCount, true);
                    AddYield(list, metalWeapon, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Instrument:
                    AddYield(list, wood, baseCount, true);
                    AddYield(list, leather, ScaleSecondary(baseCount), false);
                    break;

                case eObjectType.Longbow:
                case eObjectType.RecurvedBow:
                case eObjectType.CompositeBow:
                case eObjectType.Crossbow:
                case eObjectType.Fired:
                    AddYield(list, wood, baseCount, true);
                    AddYield(list, thread, ScaleSecondary(baseCount), false);
                    break;

                default:
                    AddYield(list, metalWeapon, baseCount, true);
                    break;
            }
        }

        /// <summary>
        /// Applies a random +20% to +30% quantity bonus on top of all existing ratios.
        /// </summary>
        private static int ApplyRogBonus(int count)
        {
            if (count < 1) return count;
            double bonus = ROG_BONUS_MIN + (Util.RandomDouble() * (ROG_BONUS_MAX - ROG_BONUS_MIN));
            double raw = count * (1.0 + bonus);
            int result = (int)Math.Floor(raw);
            double fraction = raw - result;
            if (Util.RandomDouble() < fraction)
                result++;
            return result;
        }

        /// <summary>Legacy single-material mapping for non-ROG items (unchanged behavior).</summary>
        private string GetLegacyMaterial(eObjectType type, int itemType, eRealm realm, int tier)
        {
            switch (type)
            {
                case eObjectType.Staff:
                case eObjectType.MaulerStaff:
                case eObjectType.RecurvedBow:
                case eObjectType.CompositeBow:
                case eObjectType.Longbow:
                case eObjectType.Crossbow:
                case eObjectType.Fired:
                case eObjectType.Instrument:
                    return WoodList[tier];

                case eObjectType.FistWraps:
                case eObjectType.Leather:
                    return LeatherList[tier];

                case eObjectType.Cloth:
                    return ClothList[tier];

                case eObjectType.Studded:
                case eObjectType.Reinforced:
                    return (realm == eRealm.Hibernia) ? StripsHibList[tier] : MetalList[tier];

                case eObjectType.Magical:
                    return itemType == (int)eInventorySlot.Cloak ? ClothList[tier] : JewelList[tier];

                default:
                    return (realm == eRealm.Hibernia) ? MetalHibList[tier] : MetalList[tier];
            }
        }

        #endregion
    }
}