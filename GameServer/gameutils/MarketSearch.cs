using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DOL.Database;
using log4net;

namespace DOL.GS
{
    public class MarketSearch
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public struct SearchData
        {
            public string name;
            public eRealm realm;
            public int slot;
            public int bonus1;
            public int bonus1Value;
            public int bonus2;
            public int bonus2Value;
            public int bonus3;
            public int bonus3Value;
            public int proc;
            public byte armorType;
            public byte damageType;
            public int levelMin;
            public int levelMax;
            public int minQual;
            public uint priceMin;
            public uint priceMax;
            public byte playerCrafted;
            public int visual;
            public byte page;
            public string clientVersion;
        }

        protected GamePlayer m_searchPlayer;
        protected bool m_searchHasError = false;

        public MarketSearch(GamePlayer player)
        {
            m_searchPlayer = player;
        }

        public virtual List<InventoryItem> FindItems(SearchData search)
        {
            m_searchHasError = false;
            Stopwatch watch = Stopwatch.StartNew();

            if (ServerProperties.Properties.MARKET_ENABLE_LOG && search.page == 0)
            {
                log.DebugFormat("MarketSearch: [{0}:{1}] SEARCHING MARKET: {2}", m_searchPlayer.Name, m_searchPlayer.Client.Account.Name, search.name);
            }

            ItemQuery query = new ItemQuery
            {
                IsCrafted = search.playerCrafted > 0 ? true : (bool?)null,
                HasVisual = search.visual > 0 ? true : (bool?)null,
            };

            IEnumerable<InventoryItem> inventoryItems = MarketCache.SearchItems(query);
            List<InventoryItem> items = new List<InventoryItem>();
            int count = 0;

            foreach (InventoryItem item in inventoryItems)
            {
                if (m_searchHasError)
                    break;

                if (item.IsTradable == false) continue;
                if (item.SellPrice < 1) continue;

                // Ensure the item belongs to someone
                if (string.IsNullOrEmpty(item.OwnerID)) continue;
                if (item.OwnerLot == 0) continue;

                // Search criteria
                if (!string.IsNullOrEmpty(search.name) && item.Name.IndexOf(search.name, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (search.levelMin > 1 && item.Level < search.levelMin) continue;
                if (search.levelMax > -1 && search.levelMax < 52 && item.Level > search.levelMax) continue;
                if (search.minQual > 84 && item.Quality < search.minQual) continue;
                if (search.priceMin > 1 && item.SellPrice < search.priceMin) continue;
                if (search.priceMax > 0 && item.SellPrice > search.priceMax) continue;
                if (search.visual > 0 && item.Effect == 0) continue;
                if (search.playerCrafted > 0 && item.IsCrafted == false) continue;
                if (search.proc > 0 && item.ProcSpellID == 0 && item.ProcSpellID1 == 0) continue;

                // Match 1.68 Client Slots
                if (search.slot > 0 && CheckSlot(item, search.slot) == false) continue;

                if (search.armorType > 0 && CheckForArmorType(item, search.armorType) == false) continue;
                if (search.damageType > 0 && CheckForDamageType(item, search.damageType) == false) continue;

                // Match generic 1.68 Client Bonus Dropdowns
                if (search.bonus1 > 0 && CheckBonusFilter(item, search.bonus1, search.bonus1Value) == false) continue;
                if (search.bonus2 > 0 && CheckBonusFilter(item, search.bonus2, search.bonus2Value) == false) continue;
                if (search.bonus3 > 0 && CheckBonusFilter(item, search.bonus3, search.bonus3Value) == false) continue;

                items.Add(item);

                if (++count >= ServerProperties.Properties.MARKET_SEARCH_LIMIT)
                    break;
            }

            watch.Stop();

            if (ServerProperties.Properties.MARKET_ENABLE_LOG && search.page == 0)
                log.Debug($"MarketSearch took {watch.ElapsedMilliseconds}ms on {MarketCache.ItemCount} items, got {items.Count} results!");

            return items;
        }

        private bool CheckBonusFilter(InventoryItem item, int bonusType, int bonusValue)
        {
            switch (bonusType)
            {
                case 1: return CheckForBonus(item, bonusValue);
                case 2: return CheckForSkill(item, bonusValue);
                case 3: return CheckForPower(item, bonusValue);
                case 4: return CheckForHP(item, bonusValue);
                case 5: return CheckForResist(item, bonusValue);
                default: return true;
            }
        }

        protected virtual bool CheckSlot(InventoryItem item, int slot)
        {
            // 0 is "Any", so if slot > 0, we check specifically mapping to the 1.68 client packet IDs
            switch (slot)
            {
                case 1: return item.Item_Type == (int)eInventorySlot.HeadArmor;
                case 2: return item.Item_Type == (int)eInventorySlot.HandsArmor;
                case 3: return item.Item_Type == (int)eInventorySlot.FeetArmor;
                case 4: return item.Item_Type == (int)eInventorySlot.Jewellery;
                case 5: return item.Item_Type == (int)eInventorySlot.TorsoArmor;
                case 6: return item.Item_Type == (int)eInventorySlot.Cloak;
                case 7: return item.Item_Type == (int)eInventorySlot.LegsArmor;
                case 8: return item.Item_Type == (int)eInventorySlot.ArmsArmor;
                case 9: return item.Item_Type == (int)eInventorySlot.Neck;
                case 12: return item.Item_Type == (int)eInventorySlot.Waist;
                case 13: return item.Item_Type == (int)eInventorySlot.RightBracer || item.Item_Type == (int)eInventorySlot.LeftBracer;
                case 15: return item.Item_Type == (int)eInventorySlot.RightRing || item.Item_Type == (int)eInventorySlot.LeftRing;
                case 17: return item.Object_Type == (int)eObjectType.Magical;
                case 100: return item.Item_Type == (int)eInventorySlot.RightHandWeapon || item.Item_Type == (int)eInventorySlot.LeftHandWeapon;
                case 101: return item.Item_Type == (int)eInventorySlot.LeftHandWeapon && item.Object_Type != (int)eObjectType.Shield;
                case 102: return item.Item_Type == (int)eInventorySlot.TwoHandWeapon || item.Item_Type == (int)eInventorySlot.DistanceWeapon;
                case 103: return item.Item_Type == (int)eInventorySlot.DistanceWeapon;
                case 104: return item.Object_Type == (int)eObjectType.Instrument;
                case 105: return item.Object_Type == (int)eObjectType.Shield;
                case 106: return item.Object_Type == (int)eObjectType.GenericItem;
                default: return false;
            }
        }

        protected virtual bool CheckForHP(InventoryItem item, int hp)
        {
            if (hp <= 0) return false;
            return ((item.ExtraBonusType == (int)eProperty.MaxHealth && item.ExtraBonus >= hp)
                || (item.Bonus1Type == (int)eProperty.MaxHealth && item.Bonus1 >= hp) ||
                (item.Bonus2Type == (int)eProperty.MaxHealth && item.Bonus2 >= hp) ||
                (item.Bonus3Type == (int)eProperty.MaxHealth && item.Bonus3 >= hp) ||
                (item.Bonus4Type == (int)eProperty.MaxHealth && item.Bonus4 >= hp) ||
                (item.Bonus5Type == (int)eProperty.MaxHealth && item.Bonus5 >= hp) ||
                (item.Bonus6Type == (int)eProperty.MaxHealth && item.Bonus6 >= hp) ||
                (item.Bonus7Type == (int)eProperty.MaxHealth && item.Bonus7 >= hp) ||
                (item.Bonus8Type == (int)eProperty.MaxHealth && item.Bonus8 >= hp) ||
                (item.Bonus9Type == (int)eProperty.MaxHealth && item.Bonus9 >= hp) ||
                (item.Bonus10Type == (int)eProperty.MaxHealth && item.Bonus10 >= hp));
        }

        protected virtual bool CheckForPower(InventoryItem item, int power)
        {
            if (power <= 0) return false;
            return ((item.ExtraBonusType == (int)eProperty.MaxMana && item.ExtraBonus >= power) ||
                (item.Bonus1Type == (int)eProperty.MaxMana && item.Bonus1 >= power) ||
                (item.Bonus2Type == (int)eProperty.MaxMana && item.Bonus2 >= power) ||
                (item.Bonus3Type == (int)eProperty.MaxMana && item.Bonus3 >= power) ||
                (item.Bonus4Type == (int)eProperty.MaxMana && item.Bonus4 >= power) ||
                (item.Bonus5Type == (int)eProperty.MaxMana && item.Bonus5 >= power) ||
                (item.Bonus6Type == (int)eProperty.MaxMana && item.Bonus6 >= power) ||
                (item.Bonus7Type == (int)eProperty.MaxMana && item.Bonus7 >= power) ||
                (item.Bonus8Type == (int)eProperty.MaxMana && item.Bonus8 >= power) ||
                (item.Bonus9Type == (int)eProperty.MaxMana && item.Bonus9 >= power) ||
                (item.Bonus10Type == (int)eProperty.MaxMana && item.Bonus10 >= power));
        }

        protected virtual bool CheckForArmorType(InventoryItem item, int type)
        {
            switch (type)
            {
                case 1: return item.Object_Type == (int)eObjectType.Cloth;
                case 2: return item.Object_Type == (int)eObjectType.Leather;
                case 3: return item.Object_Type == (int)eObjectType.Studded;
                case 4: return item.Object_Type == (int)eObjectType.Chain;
                case 5: return item.Object_Type == (int)eObjectType.Plate;
                case 6: return item.Object_Type == (int)eObjectType.Reinforced;
                case 7: return item.Object_Type == (int)eObjectType.Scale;
                default: return false;
            }
        }

        protected virtual bool CheckForResist(InventoryItem item, int resist)
        {
            switch (resist)
            {
                case 0: return CheckForProperty(item, (int)eProperty.Resist_Body);
                case 1: return CheckForProperty(item, (int)eProperty.Resist_Crush);
                case 2: return CheckForProperty(item, (int)eProperty.Resist_Slash);
                case 3: return CheckForProperty(item, (int)eProperty.Resist_Thrust);
                case 10: return CheckForProperty(item, (int)eProperty.Resist_Heat);
                case 12: return CheckForProperty(item, (int)eProperty.Resist_Cold);
                case 15: return CheckForProperty(item, (int)eProperty.Resist_Matter);
                case 16: return CheckForProperty(item, (int)eProperty.Resist_Body);
                case 17: return CheckForProperty(item, (int)eProperty.Resist_Spirit);
                case 22: return CheckForProperty(item, (int)eProperty.Resist_Energy);
                default: return false;
            }
        }

        protected virtual bool CheckForSkill(InventoryItem item, int skill)
        {
            if (skill <= 0) return false;
            switch (skill)
            {
                case 1: return CheckForProperty(item, (int)eProperty.Skill_Slashing);
                case 2: return CheckForProperty(item, (int)eProperty.Skill_Thrusting);
                case 8: return CheckForProperty(item, (int)eProperty.Skill_Parry);
                case 14: return CheckForProperty(item, (int)eProperty.Skill_Sword);
                case 16: return CheckForProperty(item, (int)eProperty.Skill_Hammer);
                case 17: return CheckForProperty(item, (int)eProperty.Skill_Axe);
                case 18: return CheckForProperty(item, (int)eProperty.Skill_Left_Axe);
                case 19: return CheckForProperty(item, (int)eProperty.Skill_Stealth);
                case 26: return CheckForProperty(item, (int)eProperty.Skill_Spear);
                case 29: return CheckForProperty(item, (int)eProperty.Skill_Mending);
                case 30: return CheckForProperty(item, (int)eProperty.Skill_Augmentation);
                case 33: return CheckForProperty(item, (int)eProperty.Skill_Crushing);
                case 34: return CheckForProperty(item, (int)eProperty.Skill_Pacification);
                case 37: return CheckForProperty(item, (int)eProperty.Skill_Subterranean);
                case 38: return CheckForProperty(item, (int)eProperty.Skill_Darkness);
                case 39: return CheckForProperty(item, (int)eProperty.Skill_Suppression);
                case 42: return CheckForProperty(item, (int)eProperty.Skill_Runecarving);
                case 43: return CheckForProperty(item, (int)eProperty.Skill_Shields);
                case 46: return CheckForProperty(item, (int)eProperty.Skill_Flexible_Weapon);
                case 47: return CheckForProperty(item, (int)eProperty.Skill_Staff);
                case 48: return CheckForProperty(item, (int)eProperty.Skill_Summoning);
                case 50: return CheckForProperty(item, (int)eProperty.Skill_Stormcalling);
                case 62: return CheckForProperty(item, (int)eProperty.Skill_BeastCraft);
                case 64: return CheckForProperty(item, (int)eProperty.Skill_Polearms);
                case 65: return CheckForProperty(item, (int)eProperty.Skill_Two_Handed);
                case 66: return CheckForProperty(item, (int)eProperty.Skill_Fire);
                case 67: return CheckForProperty(item, (int)eProperty.Skill_Wind);
                case 68: return CheckForProperty(item, (int)eProperty.Skill_Cold);
                case 69: return CheckForProperty(item, (int)eProperty.Skill_Earth);
                case 70: return CheckForProperty(item, (int)eProperty.Skill_Light);
                case 71: return CheckForProperty(item, (int)eProperty.Skill_Matter);
                case 72: return CheckForProperty(item, (int)eProperty.Skill_Body);
                case 73: return CheckForProperty(item, (int)eProperty.Skill_Spirit);
                case 74: return CheckForProperty(item, (int)eProperty.Skill_Mind);
                case 75: return CheckForProperty(item, (int)eProperty.Skill_Void);
                case 76: return CheckForProperty(item, (int)eProperty.Skill_Mana);
                case 77: return CheckForProperty(item, (int)eProperty.Skill_Dual_Wield);
                case 78: return CheckForProperty(item, (int)eProperty.Skill_Archery);
                case 82: return CheckForProperty(item, (int)eProperty.Skill_Battlesongs);
                case 83: return CheckForProperty(item, (int)eProperty.Skill_Enhancement);
                case 84: return CheckForProperty(item, (int)eProperty.Skill_Enchantments);
                case 88: return CheckForProperty(item, (int)eProperty.Skill_Rejuvenation);
                case 89: return CheckForProperty(item, (int)eProperty.Skill_Smiting);
                case 90: return CheckForProperty(item, (int)eProperty.Skill_Long_bows) || CheckForProperty(item, (int)eProperty.Skill_Composite) || CheckForProperty(item, (int)eProperty.Skill_RecurvedBow);
                case 91: return CheckForProperty(item, (int)eProperty.Skill_Cross_Bows);
                case 97: return CheckForProperty(item, (int)eProperty.Skill_Chants);
                case 98: return CheckForProperty(item, (int)eProperty.Skill_Instruments);
                case 101: return CheckForProperty(item, (int)eProperty.Skill_Blades);
                case 102: return CheckForProperty(item, (int)eProperty.Skill_Blunt);
                case 103: return CheckForProperty(item, (int)eProperty.Skill_Piercing);
                case 104: return CheckForProperty(item, (int)eProperty.Skill_Large_Weapon);
                case 105: return CheckForProperty(item, (int)eProperty.Skill_Mentalism);
                case 106: return CheckForProperty(item, (int)eProperty.Skill_Regrowth);
                case 107: return CheckForProperty(item, (int)eProperty.Skill_Nurture);
                case 108: return CheckForProperty(item, (int)eProperty.Skill_Nature);
                case 109: return CheckForProperty(item, (int)eProperty.Skill_Music);
                case 110: return CheckForProperty(item, (int)eProperty.Skill_Celtic_Dual);
                case 112: return CheckForProperty(item, (int)eProperty.Skill_Celtic_Spear);
                case 113: return CheckForProperty(item, (int)eProperty.Skill_Archery);
                case 114: return CheckForProperty(item, (int)eProperty.Skill_Valor);
                case 116: return CheckForProperty(item, (int)eProperty.Skill_Pathfinding);
                case 117: return CheckForProperty(item, (int)eProperty.Skill_Envenom);
                case 118: return CheckForProperty(item, (int)eProperty.Skill_Critical_Strike);
                case 120: return CheckForProperty(item, (int)eProperty.Skill_DeathSight);
                case 121: return CheckForProperty(item, (int)eProperty.Skill_Pain_working);
                case 122: return CheckForProperty(item, (int)eProperty.Skill_Death_Servant);
                case 123: return CheckForProperty(item, (int)eProperty.Skill_SoulRending);
                case 124: return CheckForProperty(item, (int)eProperty.Skill_HandToHand);
                case 125: return CheckForProperty(item, (int)eProperty.Skill_Scythe);
                case 126: return CheckForProperty(item, (int)eProperty.Skill_BoneArmy);
                case 127: return CheckForProperty(item, (int)eProperty.Skill_Arboreal);
                case 129: return CheckForProperty(item, (int)eProperty.Skill_Creeping);
                case 130: return CheckForProperty(item, (int)eProperty.Skill_Verdant);
                case 133: return CheckForProperty(item, (int)eProperty.Skill_OdinsWill);
                case 134: return CheckForProperty(item, (int)eProperty.Skill_SpectralGuard) || CheckForProperty(item, (int)eProperty.Skill_SpectralForce);
                case 135: return CheckForProperty(item, (int)eProperty.Skill_PhantasmalWail);
                case 136: return CheckForProperty(item, (int)eProperty.Skill_EtherealShriek);
                case 137: return CheckForProperty(item, (int)eProperty.Skill_ShadowMastery);
                case 138: return CheckForProperty(item, (int)eProperty.Skill_VampiiricEmbrace);
                case 139: return CheckForProperty(item, (int)eProperty.Skill_Dementia);
                case 140: return CheckForProperty(item, (int)eProperty.Skill_Witchcraft);
                case 141: return CheckForProperty(item, (int)eProperty.Skill_Cursing);
                case 142: return CheckForProperty(item, (int)eProperty.Skill_Hexing);
                case 147: return CheckForProperty(item, (int)eProperty.Skill_FistWraps);
                case 148: return CheckForProperty(item, (int)eProperty.Skill_MaulerStaff);

                case 300:
                    return CheckForProperty(item, (int)eProperty.Skill_Slashing) ||
                           CheckForProperty(item, (int)eProperty.Skill_Thrusting) ||
                           CheckForProperty(item, (int)eProperty.Skill_Crushing) ||
                           CheckForProperty(item, (int)eProperty.Skill_Shields) ||
                           CheckForProperty(item, (int)eProperty.Skill_Flexible_Weapon) ||
                           CheckForProperty(item, (int)eProperty.Skill_Staff) ||
                           CheckForProperty(item, (int)eProperty.Skill_Polearms) ||
                           CheckForProperty(item, (int)eProperty.Skill_Two_Handed) ||
                           CheckForProperty(item, (int)eProperty.Skill_Dual_Wield) ||
                           CheckForProperty(item, (int)eProperty.Skill_Long_bows) ||
                           CheckForProperty(item, (int)eProperty.Skill_Composite) ||
                           CheckForProperty(item, (int)eProperty.Skill_RecurvedBow) ||
                           CheckForProperty(item, (int)eProperty.Skill_Cross_Bows) ||
                           CheckForProperty(item, (int)eProperty.Skill_FistWraps) ||
                           CheckForProperty(item, (int)eProperty.Skill_Sword) ||
                           CheckForProperty(item, (int)eProperty.Skill_Hammer) ||
                           CheckForProperty(item, (int)eProperty.Skill_Axe) ||
                           CheckForProperty(item, (int)eProperty.Skill_Left_Axe) ||
                           CheckForProperty(item, (int)eProperty.Skill_Spear) ||
                           CheckForProperty(item, (int)eProperty.Skill_Archery) ||
                           CheckForProperty(item, (int)eProperty.Skill_Blades) ||
                           CheckForProperty(item, (int)eProperty.Skill_Blunt) ||
                           CheckForProperty(item, (int)eProperty.Skill_Piercing) ||
                           CheckForProperty(item, (int)eProperty.Skill_Large_Weapon) ||
                           CheckForProperty(item, (int)eProperty.Skill_Celtic_Dual) ||
                           CheckForProperty(item, (int)eProperty.Skill_Celtic_Spear) ||
                           CheckForProperty(item, (int)eProperty.Skill_Critical_Strike) ||
                           CheckForProperty(item, (int)eProperty.Skill_HandToHand) ||
                           CheckForProperty(item, (int)eProperty.Skill_Archery) ||
                           CheckForProperty(item, (int)eProperty.Skill_Scythe) ||
                           CheckForProperty(item, (int)eProperty.AllMeleeWeaponSkills) ||
                           CheckForProperty(item, (int)eProperty.AllDualWieldingSkills) ||
                           CheckForProperty(item, (int)eProperty.AllArcherySkills) ||
                           CheckForProperty(item, (int)eProperty.Skill_MaulerStaff);

                case 303:
                    return CheckForProperty(item, (int)eProperty.Skill_Fire) ||
                           CheckForProperty(item, (int)eProperty.Skill_Wind) ||
                           CheckForProperty(item, (int)eProperty.Skill_Cold) ||
                           CheckForProperty(item, (int)eProperty.Skill_Earth) ||
                           CheckForProperty(item, (int)eProperty.Skill_Matter) ||
                           CheckForProperty(item, (int)eProperty.Skill_Body) ||
                           CheckForProperty(item, (int)eProperty.Skill_Spirit) ||
                           CheckForProperty(item, (int)eProperty.Skill_Mind) ||
                           CheckForProperty(item, (int)eProperty.Skill_Enhancement) ||
                           CheckForProperty(item, (int)eProperty.Skill_Rejuvenation) ||
                           CheckForProperty(item, (int)eProperty.Skill_Smiting) ||
                           CheckForProperty(item, (int)eProperty.Skill_Chants) ||
                           CheckForProperty(item, (int)eProperty.Skill_DeathSight) ||
                           CheckForProperty(item, (int)eProperty.Skill_Pain_working) ||
                           CheckForProperty(item, (int)eProperty.Skill_Death_Servant) ||
                           CheckForProperty(item, (int)eProperty.Skill_SoulRending) ||
                           CheckForProperty(item, (int)eProperty.Skill_Mending) ||
                           CheckForProperty(item, (int)eProperty.Skill_Augmentation) ||
                           CheckForProperty(item, (int)eProperty.Skill_Pacification) ||
                           CheckForProperty(item, (int)eProperty.Skill_Subterranean) ||
                           CheckForProperty(item, (int)eProperty.Skill_Darkness) ||
                           CheckForProperty(item, (int)eProperty.Skill_Suppression) ||
                           CheckForProperty(item, (int)eProperty.Skill_Runecarving) ||
                           CheckForProperty(item, (int)eProperty.Skill_Summoning) ||
                           CheckForProperty(item, (int)eProperty.Skill_Stormcalling) ||
                           CheckForProperty(item, (int)eProperty.Skill_BeastCraft) ||
                           CheckForProperty(item, (int)eProperty.Skill_Light) ||
                           CheckForProperty(item, (int)eProperty.Skill_Void) ||
                           CheckForProperty(item, (int)eProperty.Skill_Mana) ||
                           CheckForProperty(item, (int)eProperty.Skill_Mentalism) ||
                           CheckForProperty(item, (int)eProperty.Skill_Regrowth) ||
                           CheckForProperty(item, (int)eProperty.Skill_Nurture) ||
                           CheckForProperty(item, (int)eProperty.Skill_Nature) ||
                           CheckForProperty(item, (int)eProperty.Skill_Music) ||
                           CheckForProperty(item, (int)eProperty.Skill_Valor) ||
                           CheckForProperty(item, (int)eProperty.Skill_Pathfinding) ||
                           CheckForProperty(item, (int)eProperty.Skill_Envenom) ||
                           CheckForProperty(item, (int)eProperty.Skill_BoneArmy) ||
                           CheckForProperty(item, (int)eProperty.Skill_Arboreal) ||
                           CheckForProperty(item, (int)eProperty.Skill_Creeping) ||
                           CheckForProperty(item, (int)eProperty.Skill_Verdant) ||
                           CheckForProperty(item, (int)eProperty.Skill_OdinsWill) ||
                           CheckForProperty(item, (int)eProperty.Skill_SpectralGuard) ||
                           CheckForProperty(item, (int)eProperty.Skill_SpectralForce) ||
                           CheckForProperty(item, (int)eProperty.Skill_PhantasmalWail) ||
                           CheckForProperty(item, (int)eProperty.Skill_EtherealShriek) ||
                           CheckForProperty(item, (int)eProperty.Skill_ShadowMastery) ||
                           CheckForProperty(item, (int)eProperty.Skill_VampiiricEmbrace) ||
                           CheckForProperty(item, (int)eProperty.Skill_Dementia) ||
                           CheckForProperty(item, (int)eProperty.Skill_Witchcraft) ||
                           CheckForProperty(item, (int)eProperty.Skill_Cursing) ||
                           CheckForProperty(item, (int)eProperty.Skill_Hexing) ||
                           CheckForProperty(item, (int)eProperty.AllMagicSkills);

                default: return false;
            }
        }

        protected virtual bool CheckForBonus(InventoryItem item, int bonus)
        {
            if (bonus <= 0) return false;
            switch (bonus)
            {
                case 0: return CheckForProperty(item, (int)eProperty.Strength);
                case 1: return CheckForProperty(item, (int)eProperty.Constitution);
                case 2: return CheckForProperty(item, (int)eProperty.Dexterity);
                case 3: return CheckForProperty(item, (int)eProperty.Quickness);
                case 4: return CheckForProperty(item, (int)eProperty.Piety);
                case 5: return CheckForProperty(item, (int)eProperty.Empathy);
                case 6: return CheckForProperty(item, (int)eProperty.Intelligence);
                case 7: return CheckForProperty(item, (int)eProperty.Charisma);

                case 8:
                    return CheckForProperty(item, (int)eProperty.Dexterity) ||
                           CheckForProperty(item, (int)eProperty.Intelligence) ||
                           CheckForProperty(item, (int)eProperty.Charisma) ||
                           CheckForProperty(item, (int)eProperty.Acuity) ||
                           CheckForProperty(item, (int)eProperty.Empathy) ||
                           CheckForProperty(item, (int)eProperty.Piety);

                case 9:
                    {
                        for (int i = (int)eProperty.ToABonus_First; i <= (int)eProperty.ToABonus_Last; i++)
                        {
                            if (CheckForProperty(item, i))
                                return true;
                        }
                        return false;
                    }

                case 10: return CheckForProperty(item, (int)eProperty.PowerPool);
                case 11: return CheckForProperty(item, (int)eProperty.HealingEffectiveness);
                case 12: return CheckForProperty(item, (int)eProperty.SpellDuration);
                case 13: return CheckForProperty(item, (int)eProperty.ArmorFactor);
                case 14: return CheckForProperty(item, (int)eProperty.CastingSpeed);
                case 15: return CheckForProperty(item, (int)eProperty.MeleeSpeed);
                case 16: return CheckForProperty(item, (int)eProperty.ArcherySpeed);

                case 17:
                    return CheckForProperty(item, (int)eProperty.StrCapBonus) ||
                           CheckForProperty(item, (int)eProperty.DexCapBonus) ||
                           CheckForProperty(item, (int)eProperty.ConCapBonus) ||
                           CheckForProperty(item, (int)eProperty.QuiCapBonus) ||
                           CheckForProperty(item, (int)eProperty.IntCapBonus) ||
                           CheckForProperty(item, (int)eProperty.PieCapBonus) ||
                           CheckForProperty(item, (int)eProperty.EmpCapBonus) ||
                           CheckForProperty(item, (int)eProperty.ChaCapBonus) ||
                           CheckForProperty(item, (int)eProperty.AcuCapBonus) ||
                           CheckForProperty(item, (int)eProperty.MaxHealthCapBonus) ||
                           CheckForProperty(item, (int)eProperty.PowerPoolCapBonus);

                case 18: return CheckForProperty(item, (int)eProperty.MaxHealthCapBonus);
                case 19: return CheckForProperty(item, (int)eProperty.BuffEffectiveness);

                case 20:
                    {
                        for (int i = (int)eProperty.MaxSpeed; i <= (int)eProperty.Acuity; i++)
                        {
                            if (CheckForProperty(item, i)) return true;
                        }

                        for (int i = (int)eProperty.EvadeChance; i <= (int)eProperty.ToHitBonus; i++)
                        {
                            if (CheckForProperty(item, i)) return true;
                        }

                        for (int i = (int)eProperty.DPS; i <= (int)eProperty.MaxProperty; i++)
                        {
                            if (CheckForProperty(item, i)) return true;
                        }
                        return false;
                    }

                default: return false;
            }
        }

        protected virtual bool CheckForDamageType(InventoryItem item, int damageType)
        {
            if (GlobalConstants.IsWeapon(item.Object_Type))
            {
                if (damageType >= 1 && damageType <= 3) return damageType == item.Type_Damage;
            }
            return false;
        }

        protected virtual bool CheckForProperty(InventoryItem item, int property)
        {
            if (property > 0)
            {
                if ((item.ExtraBonusType == property && item.ExtraBonus >= 0) ||
                    (item.Bonus1Type == property && item.Bonus1 >= 0) ||
                    (item.Bonus2Type == property && item.Bonus2 >= 0) ||
                    (item.Bonus3Type == property && item.Bonus3 >= 0) ||
                    (item.Bonus4Type == property && item.Bonus4 >= 0) ||
                    (item.Bonus5Type == property && item.Bonus5 >= 0) ||
                    (item.Bonus6Type == property && item.Bonus6 >= 0) ||
                    (item.Bonus7Type == property && item.Bonus7 >= 0) ||
                    (item.Bonus8Type == property && item.Bonus8 >= 0) ||
                    (item.Bonus9Type == property && item.Bonus9 >= 0) ||
                    (item.Bonus10Type == property && item.Bonus10 >= 0))
                    return true;
            }
            return false;
        }
    }
}