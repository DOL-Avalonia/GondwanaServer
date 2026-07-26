
using DOL.Database;
using DOL.Events;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace DOL.GS
{
    /// <summary>
    /// GeneratedUniqueItem is a subclass of UniqueItem used to create RoG object
    /// Using it as a class is much more extendable to other usage than just loot and inventory
    /// </summary>
    public class GeneratedUniqueItem : ItemUnique
    {
        //The following properties are weights for each roll
        //It is *not* a direct chance to receive the item. It is instead
        //a chance for that item type to be randomly selected as a valid generation type
        private static int ToaItemChance = Properties.ROG_TOA_ITEM_CHANCE;
        private static int ArmorWeight = Properties.ROG_ARMOR_WEIGHT;
        private static int JewelryWeight = Properties.ROG_MAGICAL_WEIGHT;
        private static int WeaponWeight = Properties.ROG_WEAPON_WEIGHT;
        //The following 5 weights are for EACH roll on an item
        //I do not recommend putting any of them above 45
        private static int ToaStatWeight = Properties.ROG_TOA_STAT_WEIGHT;
        private static int ItemStatWeight = Properties.ROG_ITEM_STAT_WEIGHT;
        private static int ItemResistWeight = Properties.ROG_ITEM_RESIST_WEIGHT;
        private static int ItemSkillWeight = Properties.ROG_ITEM_SKILL_WEIGHT;
        private static int ItemAllSkillWeight = Properties.ROG_STAT_ALLSKILL_WEIGHT;

        //base item quality for all rogs
        private static int RogStartingQual = Properties.ROG_STARTING_QUAL;
        //max possible quality for any rog
        private static int RogCapQuality = Properties.ROG_CAP_QUAL;
        //base Chance to get a magical RoG item, PlayerLevel*2 is added to get final value
        private static int MagicalItemOffset = Properties.ROG_MAGICAL_ITEM_OFFSET;

        public eCharacterClass charClass = eCharacterClass.Unknown;
        public string TargetLanguage { get; set; } = "EN";

        private static Dictionary<int, Spell> ProcSpells = new Dictionary<int, Spell>();

        protected static Dictionary<eProperty, string> hPropertyToMagicPrefix = new Dictionary<eProperty, string>();

        [ScriptLoadedEvent]
        public static void OnScriptLoaded(DOLEvent e, object sender, EventArgs args)
        { 
            InitializeHashtables();
        }

        private static readonly HashSet<eProperty> MeleeOnlyProperties = new HashSet<eProperty> 
        { 
            eProperty.AllArcherySkills, eProperty.AllDualWieldingSkills, eProperty.AllMeleeWeaponSkills, 
            eProperty.RangedDamage, eProperty.CriticalArcheryHitChance, eProperty.StyleDamage, 
            eProperty.ReactionaryStyleDamage, eProperty.StyleCostReduction, eProperty.MeleeSpeed, 
            eProperty.CriticalMeleeHitChance, eProperty.OffhandDamageAndChanceBonus, 
            eProperty.OffhandDamageBonus, eProperty.OffhandChanceBonus, eProperty.ArrowRecovery 
        };

        private static readonly HashSet<eProperty> MageOnlyProperties = new HashSet<eProperty> 
        { 
            eProperty.SpellRange, eProperty.SpellFumbleChance, eProperty.AllMagicSkills, 
            eProperty.AllFocusLevels, eProperty.CastingSpeed, eProperty.MaxMana, 
            eProperty.PowerPool, eProperty.SpellDamage, eProperty.SpellDuration, 
            eProperty.PowerPoolCapBonus, eProperty.SpellLevel, eProperty.SpellPowerCost, 
            eProperty.CriticalSpellHitChance, eProperty.Conversion, eProperty.ArcaneSyphon, 
            eProperty.DotDurationDecrease, eProperty.CriticalHealHitChance, 
            eProperty.CriticalDotHitChance, eProperty.DotDamageBonus 
        };

        public GeneratedUniqueItem()
            : this((eRealm)Util.Random(1, 3), (eCharacterClass)Util.Random(1, 32), (byte)Util.Random(1, 50))
        {

        }

        #region Constructor Randomized

        public GeneratedUniqueItem(eRealm realm, eCharacterClass charClass, byte level, int minUtility = 15, int regionID = 0, string language = null)
            : this(realm, charClass, level, GenerateObjectType(realm, charClass, level), minUtility, regionID, language)
        { }

        public GeneratedUniqueItem(eRealm realm, eCharacterClass charClass, byte level, eObjectType type, int minUtility = 15, int regionID = 0, string language = null)
            : this(realm, charClass, level, type, GenerateItemType(type), minUtility, regionID, language)
        { }

        public GeneratedUniqueItem(eRealm realm, eCharacterClass charClass, byte level, eObjectType type, eInventorySlot slot, int minUtility = 15, int regionID = 0, string language = null)
            : this(realm, charClass, level, type, slot, GenerateDamageType(type, charClass), minUtility, regionID, language)
        { }

        public GeneratedUniqueItem(eRealm realm, eCharacterClass charClass, byte level, eObjectType type, eInventorySlot slot, eDamageType dmg, int minUtility = 15, int regionID = 0, string language = null)
            : this(false, realm, charClass, level, type, slot, dmg, minUtility, regionID, language)
        { }

        public GeneratedUniqueItem(bool toa, int regionID = 0, string language = null)
            : this(toa, (eRealm)Util.Random(1, 3), (eCharacterClass)Util.Random(1, 32), (byte)Util.Random(1, 50), regionID, language)
        { }

        public GeneratedUniqueItem(bool toa, eRealm realm, eCharacterClass charClass, byte level, int regionID = 0, string language = null)
            : this(toa, realm, charClass, level, GenerateObjectType(realm, charClass, level), regionID, language)
        { }

        public GeneratedUniqueItem(bool toa, eRealm realm, eCharacterClass charClass, byte level, eObjectType type, int regionID = 0, string language = null)
            : this(toa, realm, charClass, level, type, GenerateItemType(type), regionID, language)
        { }

        public GeneratedUniqueItem(bool toa, eRealm realm, eCharacterClass charClass, byte level, eObjectType type, eInventorySlot slot, int regionID = 0, string language = null)
            : this(toa, realm, charClass, level, type, slot, GenerateDamageType(type, charClass), 15, regionID, language)
        { }

        public GeneratedUniqueItem(bool toa, eRealm realm, eCharacterClass charClass, byte level, eObjectType type, eInventorySlot slot, eDamageType dmg, int utilityMinimum = 15, int regionID = 0, string language = null)
            : base()
        {
            this.TargetLanguage = language ?? LanguageMgr.DefaultLanguage;
            this.DropRegionID = regionID;
            this.Realm = (int)realm;
            this.Level = level;
            this.Object_Type = (int)type;
            this.Item_Type = (int)slot;
            this.Type_Damage = (int)dmg;
            this.charClass = charClass;

            // shouldn't need more Randomized public set values

            //need stats before naming
            this.GenerateItemStats();

            //name item
            this.GenerateItemNameModel();

            //set item quality (this can be called again by any script with real mob values)
            this.GenerateItemQuality(Util.Random(0, 6) - 3);

            //item magical bonuses
            //if staff and magic..... focus
            this.GenerateMagicalBonuses(toa);

            this.Color = GetRandomColorForRealm(realm);

            this.IsDropable = true;
            this.IsPickable = true;
            this.IsTradable = true;
            this.CapUtility(this.Level, utilityMinimum);

            if (this.Level > 51) this.Level = 51;

            this.GenerateProc();

            //item bonus
            int temp = this.Level - 15;
            temp -= temp % 5;
            this.Bonus = temp;
            if (this.Bonus < 5)
                this.Bonus = 5;

            //constants
            int condition = this.Level * 2000;
            this.Condition = condition;
            this.MaxCondition = condition;
            this.Durability = condition;
            this.MaxDurability = condition;

            this.GenerateItemWeight();

            if (this.Level >= 15)
            {
                this.BonusLevel = (this.Level - 10);
            }
            else
            {
                this.BonusLevel = 0;
            }

            if (this.Level >= 30)
            {
                this.LevelRequirement = (this.Level - 20);
            }
            else
            {
                this.LevelRequirement = 0;
            }

            this.GenerateBonusConditions();
            this.ApplySmartPricing();

            string rogPrefix = Properties.ROG_SERVER_NAME ?? "Global ROG";
            this.Description = $"{rogPrefix} | {charClass}";

            this.AllowAdd = false;
        }

        #endregion

        #region generate item properties

        /// <summary>
        /// Replaces the obsolete Money.SetAutoPrice method with a custom exponential scaling curve.
        /// </summary>
        private long CalculateBasePrice(int itemLevel, int itemQuality)
        {
            // Exponential curve: scales from copper at low levels up to ~55 Gold at level 51
            double priceCurve = Math.Pow((double)itemLevel / 51.0, 3.82);
            double maxPrice = 290000.0;
            long calculatedPrice = (long)(priceCurve * maxPrice * ((double)itemQuality / 100.0));

            return Math.Max(2, calculatedPrice);
        }

        public void GenerateItemQuality(int conlevel)
        {
            // set base quality
            int minQuality = RogStartingQual + Math.Max(0, this.Level - 59);
            int maxQuality = (int)(1.310 * conlevel + 94.29 + 3);

            if (this.Level > 51 && minQuality < 97)
                minQuality = 97;

            // CAPS
            maxQuality = Math.Min(maxQuality, RogCapQuality);  // unique objects capped at 99 quality
            minQuality = Math.Min(minQuality, RogCapQuality);  // unique objects capped at 99 quality

            maxQuality = Math.Max(maxQuality, minQuality);

            this.Quality = Util.Random(minQuality, maxQuality);

            this.Price = CalculateBasePrice(this.Level, this.Quality);
            this.Price /= 8;
            if (this.Price <= 0)
                this.Price = 2; // 2c as sell price is 50%
        }

        protected void GenerateItemStats()
        {
            int templevel = 0;
            if (Level > 51)
            {
                templevel = this.Level;
                this.Level = 51;
            }

            eObjectType type = (eObjectType)this.Object_Type;

            //special property for instrument
            if (type == eObjectType.Instrument)
                this.DPS_AF = Util.Random(0, 3);

            //set hand
            switch (type)
            {
                //two handed weapons
                case eObjectType.CelticSpear:
                case eObjectType.CompositeBow:
                case eObjectType.Crossbow:
                case eObjectType.Fired:
                case eObjectType.Instrument:
                case eObjectType.LargeWeapons:
                case eObjectType.Longbow:
                case eObjectType.PolearmWeapon:
                case eObjectType.RecurvedBow:
                case eObjectType.Scythe:
                case eObjectType.Spear:
                case eObjectType.Staff:
                case eObjectType.TwoHandedWeapon:
                case eObjectType.MaulerStaff: //Maulers
                    {
                        this.Hand = 1;
                        break;
                    }
                //right or left handed weapons
                case eObjectType.Blades:
                case eObjectType.Blunt:
                case eObjectType.CrushingWeapon:
                case eObjectType.HandToHand:
                case eObjectType.Piercing:
                case eObjectType.SlashingWeapon:
                case eObjectType.ThrustWeapon:
                case eObjectType.FistWraps: //Maulers
                    {
                        if ((eInventorySlot)this.Item_Type == eInventorySlot.LeftHandWeapon)
                            this.Hand = 2;
                        break;
                    }
                //left handed weapons
                case eObjectType.LeftAxe:
                case eObjectType.Shield:
                    {
                        this.Hand = 2;
                        break;
                    }
                //right or two handed weapons
                case eObjectType.Sword:
                case eObjectType.Hammer:
                case eObjectType.Axe:
                    {
                        if ((eInventorySlot)this.Item_Type == eInventorySlot.TwoHandWeapon)
                            this.Hand = 1;
                        break;
                    }
            }

            //set dps_af and spd_abs
            if ((int)type >= (int)eObjectType._FirstArmor && (int)type <= (int)eObjectType._LastArmor)
            {
                if (type == eObjectType.Cloth)
                    this.DPS_AF = this.Level;
                else this.DPS_AF = this.Level * 2;
                this.SPD_ABS = GetAbsorb(type);
            }

            switch (type)
            {
                case eObjectType.Axe:
                case eObjectType.Blades:
                case eObjectType.Blunt:
                case eObjectType.CelticSpear:
                case eObjectType.CompositeBow:
                case eObjectType.Crossbow:
                case eObjectType.CrushingWeapon:
                case eObjectType.Fired:
                case eObjectType.Flexible:
                case eObjectType.Hammer:
                case eObjectType.HandToHand:
                case eObjectType.LargeWeapons:
                case eObjectType.LeftAxe:
                case eObjectType.Longbow:
                case eObjectType.Piercing:
                case eObjectType.PolearmWeapon:
                case eObjectType.RecurvedBow:
                case eObjectType.Scythe:
                case eObjectType.Shield:
                case eObjectType.SlashingWeapon:
                case eObjectType.Spear:
                case eObjectType.Staff:
                case eObjectType.Sword:
                case eObjectType.ThrustWeapon:
                case eObjectType.TwoHandedWeapon:
                case eObjectType.MaulerStaff: //Maulers
                case eObjectType.FistWraps: //Maulers
                    {
                        this.DPS_AF = (int)(((this.Level * 0.3) + 1.2) * 10);
                        SetWeaponSpeed();
                        break;
                    }
            }

            if (templevel != 0)
                this.Level = templevel;
        }

        protected void GenerateProc()
        {
            bool isBaseClassLowLevel = this.Level <= 8 &&
                                       GameEvents.StartAsBaseClass.START_AS_BASE_CLASS &&
                                       IsBaseClass(this.charClass);

            if (isBaseClassLowLevel)
                return;

            if (!Util.Chance(1)) return;
            if (this.Object_Type == (int)eObjectType.Magical)
                return;

            this.ProcChance = 10;

            if (((this.Object_Type >= (int)eObjectType._FirstWeapon && this.Object_Type <= (int)eObjectType._LastWeapon) || this.Object_Type == (int)eObjectType.Shield))
            {
                if (Util.Chance(50))
                {
                    //LT procs
                    if (Level < 10)
                    {
                        this.ProcSpellID = 8010;
                        this.LevelRequirement = 1;
                    }
                    else if (Level < 15)
                    {
                        this.ProcSpellID = 8011;
                        this.LevelRequirement = 10;
                    }
                    else if (Level < 20)
                    {
                        this.ProcSpellID = 8012;
                        this.LevelRequirement = 15;
                    }
                    else if (Level < 25)
                    {
                        this.ProcSpellID = 8013;
                        this.LevelRequirement = 20;
                    }
                    else if (Level < 30)
                    {
                        this.ProcSpellID = 8014;
                        this.LevelRequirement = 25;
                    }
                    else if (Level < 35)
                    {
                        this.ProcSpellID = 8015;
                        this.LevelRequirement = 30;
                    }
                    else if (Level < 40)
                    {
                        this.ProcSpellID = 8016;
                        this.LevelRequirement = 35;
                    }
                    else if (Level < 43)
                    {
                        this.ProcSpellID = 8017;
                        this.LevelRequirement = 40;
                    }
                }
                else
                {
                    //DD procs
                    if (Level < 10)
                    {
                        this.ProcSpellID = 8020;
                        this.LevelRequirement = 1;
                    }
                    else if (Level < 15)
                    {
                        this.ProcSpellID = 8021;
                        this.LevelRequirement = 10;
                    }
                    else if (Level < 20)
                    {
                        this.ProcSpellID = 8022;
                        this.LevelRequirement = 15;
                    }
                    else if (Level < 25)
                    {
                        this.ProcSpellID = 8023;
                        this.LevelRequirement = 20;
                    }
                    else if (Level < 30)
                    {
                        this.ProcSpellID = 8024;
                        this.LevelRequirement = 25;
                    }
                    else if (Level < 35)
                    {
                        this.ProcSpellID = 8025;
                        this.LevelRequirement = 30;
                    }
                    else if (Level < 40)
                    {
                        this.ProcSpellID = 8026;
                        this.LevelRequirement = 35;
                    }
                    else if (Level < 43)
                    {
                        this.ProcSpellID = 8027;
                        this.LevelRequirement = 40;
                    }
                }
            }
            else if (this.Object_Type >= (int)eObjectType._FirstArmor && this.Object_Type <= (int)eObjectType._LastArmor && this.Item_Type == Slot.TORSO)
            {
                if (Util.Chance(50))
                {
                    //Heal procs
                    if (Level < 10)
                    {
                        this.ProcSpellID = 8030;
                        this.LevelRequirement = 1;
                    }
                    else if (Level < 15)
                    {
                        this.ProcSpellID = 8031;
                        this.LevelRequirement = 10;
                    }
                    else if (Level < 20)
                    {
                        this.ProcSpellID = 8032;
                        this.LevelRequirement = 15;
                    }
                    else if (Level < 25)
                    {
                        this.ProcSpellID = 8033;
                        this.LevelRequirement = 20;
                    }
                    else if (Level < 30)
                    {
                        this.ProcSpellID = 8034;
                        this.LevelRequirement = 25;
                    }
                    else if (Level < 35)
                    {
                        this.ProcSpellID = 8035;
                        this.LevelRequirement = 30;
                    }
                    else if (Level < 40)
                    {
                        this.ProcSpellID = 8036;
                        this.LevelRequirement = 35;
                    }
                    else if (Level < 43)
                    {
                        this.ProcSpellID = 8037;
                        this.LevelRequirement = 40;
                    }
                }
                else
                {
                    //ABS procs
                    if (Level < 10)
                    {
                        this.ProcSpellID = 8040;
                        this.LevelRequirement = 1;
                    }
                    else if (Level < 15)
                    {
                        this.ProcSpellID = 8041;
                        this.LevelRequirement = 10;
                    }
                    else if (Level < 20)
                    {
                        this.ProcSpellID = 8042;
                        this.LevelRequirement = 15;
                    }
                    else if (Level < 25)
                    {
                        this.ProcSpellID = 8043;
                        this.LevelRequirement = 20;
                    }
                    else if (Level < 30)
                    {
                        this.ProcSpellID = 8044;
                        this.LevelRequirement = 25;
                    }
                    else if (Level < 35)
                    {
                        this.ProcSpellID = 8045;
                        this.LevelRequirement = 30;
                    }
                    else if (Level < 40)
                    {
                        this.ProcSpellID = 8046;
                        this.LevelRequirement = 35;
                    }
                    else if (Level < 43)
                    {
                        this.ProcSpellID = 8047;
                        this.LevelRequirement = 40;
                    }
                }

            }
        }

        private int GetRandomColorForRealm(eRealm realm)
        {
            List<int> validColors = new List<int>();
            validColors.Add(0); //white

            if (Level > 10)
            {
                validColors.Add(6); //grey
                validColors.Add(4); //old yellow
            }

            if (Level > 20)
            {
                validColors.Add(17); //iron
                validColors.Add(16); //bronze
            }

            if (Level > 30)
            {
                validColors.Add(18); //steel
                validColors.Add(19); //alloy
                validColors.Add(72); //grey1
            }

            if (Level > 40)
            {
                validColors.Add(22); //asterite
                validColors.Add(20); //fine alloy
                validColors.Add(73); //gray2
            }

            if (Level > 50)
            {
                validColors.Add(21); //mithril
                validColors.Add(25); //vaanum
                validColors.Add(26); //adamantium
                validColors.Add(43); //black cloth
                validColors.Add(74); //grey3
                validColors.Add(118); //charcoal
            }

            switch (realm)
            {
                case eRealm.Hibernia:
                    if (Level > 10)
                    {
                        validColors.Add(2); //old green
                    }

                    if (Level > 20)
                    {
                        validColors.Add(10); //leather green

                    }

                    if (Level > 30)
                    {
                        validColors.Add(31); //yellow green
                        validColors.Add(32); //green
                    }

                    if (Level > 40)
                    {
                        validColors.Add(33); //blue green
                        validColors.Add(68); //green1
                    }

                    if (Level > 50)
                    {
                        validColors.Add(70); //green3
                        validColors.Add(71); //green4
                        validColors.Add(142); //forest green
                    }
                    break;
                case eRealm.Albion:
                    if (Level > 10)
                    {
                        validColors.Add(1); //old red
                    }

                    if (Level > 20)
                    {
                        validColors.Add(9); //leather red

                    }

                    if (Level > 30)
                    {
                        validColors.Add(24); //yellow red
                        validColors.Add(27); //red
                    }

                    if (Level > 40)
                    {
                        validColors.Add(64); //red1
                        validColors.Add(65); //red2
                    }

                    if (Level > 50)
                    {
                        validColors.Add(66); //red3
                        validColors.Add(67); //red4
                        validColors.Add(143); //burgundy
                    }
                    break;
                case eRealm.Midgard:
                    if (Level > 10)
                    {
                        validColors.Add(3); //old red
                    }

                    if (Level > 20)
                    {
                        validColors.Add(14); //leather red

                    }

                    if (Level > 30)
                    {
                        validColors.Add(34); //turqoise cloth
                        validColors.Add(35); //light blue
                    }

                    if (Level > 40)
                    {
                        validColors.Add(36); //blue
                        validColors.Add(51); //blue1
                    }

                    if (Level > 50)
                    {
                        validColors.Add(52); //blue2
                        validColors.Add(54); //blue4
                        validColors.Add(86); //blue4 again?
                        validColors.Add(141); //navy blue
                    }
                    break;
            }

            return validColors[Util.Random(validColors.Count - 1)];
        }

        /// <summary>
        /// Combines Environments, Pools, Matrix, and Stat/resist caps Correlation!
        /// </summary>
        public void GenerateMagicalBonuses(bool toa)
        {
            eRegionCategory regionCat = RegionMapper.GetCategoryFromRegionID(this.DropRegionID);
            eRegionEnvironment regionEnv = RegionMapper.GetEnvironmentFromRegionID(this.DropRegionID);

            bool isBaseClassLowLevel = this.Level <= 8 &&
                                       GameEvents.StartAsBaseClass.START_AS_BASE_CLASS &&
                                       IsBaseClass(this.charClass);

            // Environmental Weapon Damage Type Override
            if (!isBaseClassLowLevel && this.Object_Type >= (int)eObjectType._FirstWeapon && this.Object_Type <= (int)eObjectType._LastWeapon)
            {
                var prioDamage = RegionMapper.GetPrioritizedDamageTypes(regionEnv);
                if (prioDamage.Count > 0 && Util.Chance(50))
                {
                    this.Type_Damage = (int)prioDamage[Util.Random(0, prioDamage.Count - 1)];
                }
            }

            // Dynamic Slot Allocation
            int maxSlots = GetTotalAllowedSlots(regionCat, this.Level);
            int number = 0;

            if (this.Level > 60 && Util.Chance(10)) number++;
            if (this.Level > 70 && Util.Chance(25)) number++;
            if (this.Level > 70 && Util.Chance(25)) number++;
            if (this.Level > 80 && Util.Chance(80)) number++;

            if (Util.Chance(MagicalItemOffset + this.Level * 2) || this.Object_Type == (int)eObjectType.Magical || isBaseClassLowLevel)
            {
                number++;
                if (Util.Chance(this.Level * 8 - 40) || (isBaseClassLowLevel && Util.Chance(25)))
                {
                    number++;
                    if (!isBaseClassLowLevel && Util.Chance(this.Level * 6 - 60))
                    {
                        number++;
                        if (Util.Chance(this.Level * 4 - 80))
                        {
                            number++;

                            // Roll for slots 5 up to maxSlots dynamically based on the region limits!
                            for (int i = 5; i <= maxSlots; i++)
                            {
                                if (Util.Chance((int)(this.Level * 1.5))) number++;
                            }
                        }
                    }
                }
            }

            if (this.Object_Type == (int)eObjectType.Magical && number < 1)
                number = 1;

            if (isBaseClassLowLevel)
            {
                maxSlots = 2;
                if (number > 2) number = 2;
            }

            number = Math.Min(number, maxSlots);

            bool fMagicScaled = false;
            bool fAddedBonus = false;

            double quality = (double)this.Quality * 0.01;
            double multiplier = (quality * quality * quality) + 0.20;

            if (toa || regionCat >= eRegionCategory.AtlantisOverworld) multiplier += 0.15;

            // Property Generation Loop
            for (int i = 1; i <= number; i++)
            {
                ePropertyPool pool = GetPoolForSlot(regionCat, i, this.Level, HasSkillCheck(), toa);
                eProperty prop = eProperty.Undefined;

                if (isBaseClassLowLevel)
                {
                    pool = Util.Chance(50) ? ePropertyPool.ClassicStats : ePropertyPool.ClassicResists;
                }

                if (this.DropMobBodyType == 8 && pool == ePropertyPool.ClassicStats && Util.Chance(20))
                {
                    pool = ePropertyPool.ClassicResists;
                }

                // 20% average chance for BodyType to dictate the specific stat/resist
                bool bodyTypeInfluence = Util.Chance(20);

                // Preserve classic Focus and Class-Specific Skills for the first 3 slots
                if (i == 1 && CanAddFocus() && !isBaseClassLowLevel)
                {
                    prop = eProperty.AllFocusLevels;
                }
                else if (i <= 3 && Util.Chance(15) && !HasSkillCheck() && !isBaseClassLowLevel)
                {
                    prop = GetClassSpecificSkill();
                }

                // Body Type Influence
                else if (bodyTypeInfluence && (pool == ePropertyPool.ClassicStats || pool == ePropertyPool.ClassicResists))
                {
                    var bodyProps = GetPrioritizedPropertiesForBodyType(this.DropMobBodyType, pool);
                    if (bodyProps.Count > 0)
                    {
                        prop = bodyProps[Util.Random(0, bodyProps.Count - 1)];
                    }
                }

                // Environmental Resist Priority
                else if (pool == ePropertyPool.ClassicResists && Util.Chance(70))
                {
                    var prioritizedResists = RegionMapper.GetPrioritizedResists(regionEnv);
                    if (prioritizedResists.Count > 0)
                    {
                        prop = prioritizedResists[Util.Random(0, prioritizedResists.Count - 1)];
                    }
                }
                // Smart Cap Correlation
                else if ((pool == ePropertyPool.ToaCaps || pool == ePropertyPool.MythicalCaps) && !isBaseClassLowLevel)
                {
                    prop = GetCorrelatedCap(pool);
                }

                // Standard Random Pool Selection
                if (prop == eProperty.Undefined || HasBonus(prop) || !IsPropertyAllowed(prop))
                {
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        prop = GetRandomPropertyFromPool(GetPropertiesFromPool(pool));
                        if (!HasBonus(prop) && IsPropertyAllowed(prop))
                            break;
                    }
                }

                // Write the Bonus
                if (prop != eProperty.Undefined && !HasBonus(prop) && IsPropertyAllowed(prop))
                {
                    int amount = GetDynamicBonusAmount(pool, prop, this.Level);
                    double tmpMulti = multiplier;

                    if (pool == ePropertyPool.ClassicStats)
                        tmpMulti = 1; // Base stats ignore the shrinking multiplier

                    amount = (int)Math.Ceiling(amount * tmpMulti);
                    if (amount < 1) amount = 1;

                    WriteBonus(prop, amount);
                    fAddedBonus = true;

                    // Reduce the quality multiplier slightly for subsequent rolls
                    if (!fMagicScaled && pool != ePropertyPool.ClassicStats)
                    {
                        fMagicScaled = true;
                        multiplier *= 0.75;
                    }
                }
            }

            // Non-magical items lose their capitalization styling
            if (number == 0 || !fAddedBonus)
                this.Name = this.Name.ToLower();

            ReorderBonuses();
        }

        private bool IsBaseClass(eCharacterClass charClass)
        {
            switch (charClass)
            {
                case eCharacterClass.Acolyte:
                case eCharacterClass.AlbionRogue:
                case eCharacterClass.Disciple:
                case eCharacterClass.Elementalist:
                case eCharacterClass.Fighter:
                case eCharacterClass.Forester:
                case eCharacterClass.Guardian:
                case eCharacterClass.Mage:
                case eCharacterClass.Magician:
                case eCharacterClass.MidgardRogue:
                case eCharacterClass.Mystic:
                case eCharacterClass.Naturalist:
                case eCharacterClass.Seer:
                case eCharacterClass.Stalker:
                case eCharacterClass.Viking:
                    return true;
            }
            return false;
        }

        private void ReorderBonuses()
        {
            List<KeyValuePair<int, int>> bonuses = new List<KeyValuePair<int, int>>();
            if (Bonus1Type > 0 && Bonus1 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus1Type, Bonus1));
            if (Bonus2Type > 0 && Bonus2 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus2Type, Bonus2));
            if (Bonus3Type > 0 && Bonus3 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus3Type, Bonus3));
            if (Bonus4Type > 0 && Bonus4 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus4Type, Bonus4));
            if (Bonus5Type > 0 && Bonus5 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus5Type, Bonus5));
            if (Bonus6Type > 0 && Bonus6 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus6Type, Bonus6));
            if (Bonus7Type > 0 && Bonus7 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus7Type, Bonus7));
            if (Bonus8Type > 0 && Bonus8 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus8Type, Bonus8));
            if (Bonus9Type > 0 && Bonus9 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus9Type, Bonus9));
            if (Bonus10Type > 0 && Bonus10 > 0) bonuses.Add(new KeyValuePair<int, int>(Bonus10Type, Bonus10));
            if (ExtraBonusType > 0 && ExtraBonus > 0) bonuses.Add(new KeyValuePair<int, int>(ExtraBonusType, ExtraBonus));

            int GetPriority(int type)
            {
                eProperty prop = (eProperty)type;

                if (prop == eProperty.MaxHealth) return 1;
                if (prop == eProperty.MaxMana) return 2;
                if (prop == eProperty.Acuity) return 3;
                if (Array.Exists(ClassicStats, p => p == prop)) return 4;
                if (Array.Exists(ClassicResists, p => p == prop)) return 5;
                if (IsSkillProperty(prop)) return 6;
                if (prop == eProperty.MaxHealthCapBonus) return 7;
                if (prop == eProperty.PowerPoolCapBonus) return 8;
                if (prop == eProperty.AcuCapBonus) return 9;
                if (Array.Exists(ToaCaps, p => p == prop)) return 10;

                return 11;
            }

            bonuses.Sort((a, b) => GetPriority(a.Key).CompareTo(GetPriority(b.Key)));

            Bonus1Type = 0; Bonus1 = 0; Bonus2Type = 0; Bonus2 = 0; Bonus3Type = 0; Bonus3 = 0;
            Bonus4Type = 0; Bonus4 = 0; Bonus5Type = 0; Bonus5 = 0; Bonus6Type = 0; Bonus6 = 0;
            Bonus7Type = 0; Bonus7 = 0; Bonus8Type = 0; Bonus8 = 0; Bonus9Type = 0; Bonus9 = 0;
            Bonus10Type = 0; Bonus10 = 0; ExtraBonusType = 0; ExtraBonus = 0;

            for (int i = 0; i < bonuses.Count; i++)
            {
                switch (i)
                {
                    case 0: Bonus1Type = bonuses[i].Key; Bonus1 = bonuses[i].Value; break;
                    case 1: Bonus2Type = bonuses[i].Key; Bonus2 = bonuses[i].Value; break;
                    case 2: Bonus3Type = bonuses[i].Key; Bonus3 = bonuses[i].Value; break;
                    case 3: Bonus4Type = bonuses[i].Key; Bonus4 = bonuses[i].Value; break;
                    case 4: Bonus5Type = bonuses[i].Key; Bonus5 = bonuses[i].Value; break;
                    case 5: Bonus6Type = bonuses[i].Key; Bonus6 = bonuses[i].Value; break;
                    case 6: Bonus7Type = bonuses[i].Key; Bonus7 = bonuses[i].Value; break;
                    case 7: Bonus8Type = bonuses[i].Key; Bonus8 = bonuses[i].Value; break;
                    case 8: Bonus9Type = bonuses[i].Key; Bonus9 = bonuses[i].Value; break;
                    case 9: Bonus10Type = bonuses[i].Key; Bonus10 = bonuses[i].Value; break;
                    case 10: ExtraBonusType = bonuses[i].Key; ExtraBonus = bonuses[i].Value; break;
                }
            }
        }

        private void GenerateBonusConditions()
        {
            eRegionCategory regionCat = RegionMapper.GetCategoryFromRegionID(this.DropRegionID);

            bool reqRen7 = false;
            bool reqRen8 = false;
            bool reqRen9 = false;
            bool reqRen10 = false;
            int reqChampExtra = 0;

            if (regionCat == eRegionCategory.AtlantisOverworld)
            {
                reqRen7 = true;
            }
            else if (regionCat == eRegionCategory.AtlantisDungeons || regionCat == eRegionCategory.Catacombs || regionCat == eRegionCategory.DeepCatacombs)
            {
                reqRen8 = true;
            }
            else if (regionCat == eRegionCategory.MythicalZones)
            {
                reqRen9 = true;
                reqRen10 = true;
                reqChampExtra = 2; // Slot 11 (ExtraBonus) needs Champ Level 2
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[");

            List<string> elements = new List<string>();
            string template = "{{\"BonusName\":\"{0}\",\"ChampionLevel\":{1},\"MlLevel\":0,\"IsRenaissanceRequired\":{2}}}";

            if (this.Bonus1Type > 0) elements.Add(string.Format(template, "Bonus1", 0, "false"));
            if (this.Bonus2Type > 0) elements.Add(string.Format(template, "Bonus2", 0, "false"));
            if (this.Bonus3Type > 0) elements.Add(string.Format(template, "Bonus3", 0, "false"));
            if (this.Bonus4Type > 0) elements.Add(string.Format(template, "Bonus4", 0, "false"));
            if (this.Bonus5Type > 0) elements.Add(string.Format(template, "Bonus5", 0, "false"));
            if (this.Bonus6Type > 0) elements.Add(string.Format(template, "Bonus6", 0, "false"));
            if (this.Bonus7Type > 0) elements.Add(string.Format(template, "Bonus7", 0, reqRen7 ? "true" : "false"));
            if (this.Bonus8Type > 0) elements.Add(string.Format(template, "Bonus8", 0, reqRen8 ? "true" : "false"));
            if (this.Bonus9Type > 0) elements.Add(string.Format(template, "Bonus9", 0, reqRen9 ? "true" : "false"));
            if (this.Bonus10Type > 0) elements.Add(string.Format(template, "Bonus10", 0, reqRen10 ? "true" : "false"));
            if (this.ExtraBonusType > 0) elements.Add(string.Format(template, "ExtraBonus", reqChampExtra, "false"));
            if (this.ProcSpellID > 0) elements.Add(string.Format(template, "ProcSpellID", 0, "false"));
            if (this.ProcSpellID1 > 0) elements.Add(string.Format(template, "ProcSpellID1", 0, "false"));

            sb.Append(string.Join(",", elements));
            sb.Append("]");

            this.BonusConditions = sb.ToString();
        }

        private void ApplySmartPricing()
        {
            long basePrice = CalculateBasePrice(this.Level, this.Quality);
            double expectedBaseUtility = Math.Max(15.0, this.Level - 5.0);

            // UtilityFactor naturally captures Level boosts, Dungeon +25%, and Mythical +15%
            double utilityFactor = Math.Max(1.0, GetTotalUtility() / expectedBaseUtility);

            this.Price = (long)(basePrice * 5.0 * utilityFactor);

            if (this.Price <= 0)
            {
                this.Price = 2;
            }
        }

        private eProperty GetPropertyFromBonusLine(int BonusLine)
        {
            int property = 0;

            switch (BonusLine)
            {
                case 1:
                    property = Bonus1Type;
                    break;
                case 2:
                    property = Bonus2Type;
                    break;
                case 3:
                    property = Bonus3Type;
                    break;
                case 4:
                    property = Bonus4Type;
                    break;
                case 5:
                    property = Bonus5Type;
                    break;
                case 6:
                    property = Bonus6Type;
                    break;
                case 7:
                    property = Bonus7Type;
                    break;
                case 8:
                    property = Bonus8Type;
                    break;
                case 9:
                    property = Bonus9Type;
                    break;
                case 10:
                    property = Bonus10Type;
                    break;
                case 11:
                    property = ExtraBonusType;
                    break;
            }

            return (eProperty)property;
        }

        private bool CanAddFocus()
        {
            if (this.Object_Type == (int)eObjectType.Staff)
            {
                if (this.Bonus1Type != 0)
                    return false;

                if (this.Realm == (int)eRealm.Albion && this.charClass == eCharacterClass.Friar)
                    return false;

                return true;
            }

            return false;
        }
        #endregion

        #region check valid stat
        /// <summary>
        /// Handles the complex filtering for Realm and Class-specific skills.
        /// </summary>
        private eProperty GetClassSpecificSkill()
        {
            ArrayList validSkills = new ArrayList();

            bool fIndividualSkill = false;

            // All Skills is never combined with any other skill
            if (!HasBonus(eProperty.AllSkills))
            {
                // All type skills never combined with individual skills
                if (!HasBonus(eProperty.AllMagicSkills) &&
                    !HasBonus(eProperty.AllMeleeWeaponSkills) &&
                    !HasBonus(eProperty.AllDualWieldingSkills) &&
                    !HasBonus(eProperty.AllArcherySkills))
                {
                    // individual realm specific skills
                    if ((eRealm)this.Realm == eRealm.Albion)
                    {
                        foreach (eProperty property in AlbSkillBonus)
                        {
                            if (!HasBonus(property) && SkillIsValidForClass(property) && !IsCompetingSkillLine(property))
                            {
                                if (SkillIsValidForObjectType(property))
                                    validSkills.Add(property);
                            }
                            else
                                fIndividualSkill = true;
                        }
                    }
                    else if ((eRealm)this.Realm == eRealm.Hibernia)
                    {
                        foreach (eProperty property in HibSkillBonus)
                        {
                            if (!HasBonus(property) && SkillIsValidForClass(property) && !IsCompetingSkillLine(property))
                            {
                                if (SkillIsValidForObjectType(property))
                                    validSkills.Add(property);
                            }
                            else
                                fIndividualSkill = true;
                        }
                    }
                    else if ((eRealm)this.Realm == eRealm.Midgard)
                    {
                        foreach (eProperty property in MidSkillBonus)
                        {
                            if (!HasBonus(property) && SkillIsValidForClass(property) && !IsCompetingSkillLine(property))
                            {
                                if (SkillIsValidForObjectType(property))
                                    validSkills.Add(property);
                            }
                            else
                                fIndividualSkill = true;
                        }
                    }

                    if (!fIndividualSkill)
                    {
                        // ok to add AllSkills, but reduce the chance
                        if (SkillIsValidForObjectType(eProperty.AllSkills) && Util.Chance(ItemAllSkillWeight))
                            validSkills.Add(eProperty.AllSkills);
                    }
                }

                // All type skills never combined with individual skills
                if (!fIndividualSkill)
                {
                    if (!HasBonus(eProperty.AllMagicSkills) && SkillIsValidForObjectType(eProperty.AllMagicSkills) && Util.Chance(ItemAllSkillWeight))
                        validSkills.Add(eProperty.AllMagicSkills);

                    if (!HasBonus(eProperty.AllMeleeWeaponSkills) && SkillIsValidForObjectType(eProperty.AllMeleeWeaponSkills) && Util.Chance(ItemAllSkillWeight))
                        validSkills.Add(eProperty.AllMeleeWeaponSkills);

                    if (!HasBonus(eProperty.AllDualWieldingSkills) && SkillIsValidForObjectType(eProperty.AllDualWieldingSkills) && Util.Chance(ItemAllSkillWeight))
                        validSkills.Add(eProperty.AllDualWieldingSkills);

                    if (!HasBonus(eProperty.AllArcherySkills) && SkillIsValidForObjectType(eProperty.AllArcherySkills) && Util.Chance(ItemAllSkillWeight))
                        validSkills.Add(eProperty.AllArcherySkills);
                }
            }

            int index = validSkills.Count - 1;
            if (index < 0)
            {
                return GetWeightedStatForClass(this.charClass);
            }

            return (eProperty)validSkills[Util.Random(0, index)];
        }

        private bool IsCompetingSkillLine(eProperty prop)
        {
            List<eProperty> skillsToCheck = new List<eProperty>();
            if (prop == eProperty.Skill_Slashing || prop == eProperty.Skill_Thrusting || prop == eProperty.Skill_Crushing)
            {
                skillsToCheck.Add(eProperty.Skill_Slashing);
                skillsToCheck.Add(eProperty.Skill_Thrusting);
                skillsToCheck.Add(eProperty.Skill_Crushing);
            }
            if (prop == eProperty.Skill_Blades || prop == eProperty.Skill_Piercing || prop == eProperty.Skill_Blunt)
            {
                skillsToCheck.Add(eProperty.Skill_Blades);
                skillsToCheck.Add(eProperty.Skill_Piercing);
                skillsToCheck.Add(eProperty.Skill_Blunt);
            }
            if (prop == eProperty.Skill_Axe || prop == eProperty.Skill_Sword || prop == eProperty.Skill_Hammer)
            {
                skillsToCheck.Add(eProperty.Skill_Axe);
                skillsToCheck.Add(eProperty.Skill_Sword);
                skillsToCheck.Add(eProperty.Skill_Hammer);
            }

            if (prop == eProperty.Skill_Matter || prop == eProperty.Skill_Body || prop == eProperty.Skill_Spirit || prop == eProperty.Skill_Mind)
            {
                skillsToCheck.Add(eProperty.Skill_Matter);
                skillsToCheck.Add(eProperty.Skill_Body);
                skillsToCheck.Add(eProperty.Skill_Spirit);
                skillsToCheck.Add(eProperty.Skill_Mind);
            }
            if (prop == eProperty.Skill_Earth || prop == eProperty.Skill_Cold || prop == eProperty.Skill_Fire || prop == eProperty.Skill_Wind)
            {
                skillsToCheck.Add(eProperty.Skill_Earth);
                skillsToCheck.Add(eProperty.Skill_Cold);
                skillsToCheck.Add(eProperty.Skill_Fire);
                skillsToCheck.Add(eProperty.Skill_Wind);
            }
            if (prop == eProperty.Skill_DeathSight || prop == eProperty.Skill_Death_Servant || prop == eProperty.Skill_Pain_working)
            {
                skillsToCheck.Add(eProperty.Skill_DeathSight);
                skillsToCheck.Add(eProperty.Skill_Death_Servant);
                skillsToCheck.Add(eProperty.Skill_Pain_working);
            }
            if (prop == eProperty.Skill_Wraithsight || prop == eProperty.Skill_Void_Acolyte || prop == eProperty.Skill_Tormentshaper)
            {
                skillsToCheck.Add(eProperty.Skill_Wraithsight);
                skillsToCheck.Add(eProperty.Skill_Void_Acolyte);
                skillsToCheck.Add(eProperty.Skill_Tormentshaper);
            }
            if (prop == eProperty.Skill_Light || prop == eProperty.Skill_Mana || prop == eProperty.Skill_Void || prop == eProperty.Skill_Enchantments || prop == eProperty.Skill_Mentalism)
            {
                skillsToCheck.Add(eProperty.Skill_Light);
                skillsToCheck.Add(eProperty.Skill_Mana);
                skillsToCheck.Add(eProperty.Skill_Void);
                skillsToCheck.Add(eProperty.Skill_Enchantments);
                skillsToCheck.Add(eProperty.Skill_Mentalism);
            }
            if (prop == eProperty.Skill_Arboreal || prop == eProperty.Skill_Creeping || prop == eProperty.Skill_Verdant)
            {
                skillsToCheck.Add(eProperty.Skill_Arboreal);
                skillsToCheck.Add(eProperty.Skill_Creeping);
                skillsToCheck.Add(eProperty.Skill_Verdant);
            }
            if (prop == eProperty.Skill_Darkness || prop == eProperty.Skill_Suppression || prop == eProperty.Skill_Runecarving || prop == eProperty.Skill_Summoning || prop == eProperty.Skill_BoneArmy)
            {
                skillsToCheck.Add(eProperty.Skill_Darkness);
                skillsToCheck.Add(eProperty.Skill_Suppression);
                skillsToCheck.Add(eProperty.Skill_Runecarving);
                skillsToCheck.Add(eProperty.Skill_Summoning);
                skillsToCheck.Add(eProperty.Skill_BoneArmy);
            }


            foreach (var propCheck in skillsToCheck)
            {
                if (Bonus1Type == (int)propCheck)
                    return true;
                if (Bonus2Type == (int)propCheck)
                    return true;
                if (Bonus3Type == (int)propCheck)
                    return true;
                if (Bonus4Type == (int)propCheck)
                    return true;
                if (Bonus5Type == (int)propCheck)
                    return true;
                if (Bonus6Type == (int)propCheck)
                    return true;
                if (Bonus7Type == (int)propCheck)
                    return true;
                if (Bonus8Type == (int)propCheck)
                    return true;
                if (Bonus9Type == (int)propCheck)
                    return true;
                if (Bonus10Type == (int)propCheck)
                    return true;
                if (ExtraBonusType == (int)propCheck)
                    return true;
            }

            return false;
        }

        private eProperty GetWeightedStatForClass(eCharacterClass charClass)
        {
            if (Util.Chance(10))
                return eProperty.MaxHealth;

            int rand = Util.Random(100);
            switch (charClass)
            {
                case eCharacterClass.Armsman:
                case eCharacterClass.Mercenary:
                case eCharacterClass.Infiltrator:
                case eCharacterClass.Scout:
                case eCharacterClass.Blademaster:
                case eCharacterClass.Hero:
                case eCharacterClass.Berserker:
                case eCharacterClass.Warrior:
                case eCharacterClass.Savage:
                case eCharacterClass.Hunter:
                case eCharacterClass.Shadowblade:
                case eCharacterClass.Nightshade:
                case eCharacterClass.Ranger:
                case eCharacterClass.Fighter:
                case eCharacterClass.Viking:
                case eCharacterClass.Guardian:
                case eCharacterClass.AlbionRogue:
                case eCharacterClass.MidgardRogue:
                case eCharacterClass.Stalker:
                    //25% chance of getting any useful stat
                    //for classes who do not need mana/acuity/casting stats
                    if (rand <= 25)
                        return eProperty.Strength;
                    else if (rand <= 50)
                        return eProperty.Dexterity;
                    else if (rand <= 75)
                        return eProperty.Constitution;
                    else return eProperty.Quickness;

                case eCharacterClass.Cabalist:
                case eCharacterClass.Sorcerer:
                case eCharacterClass.Theurgist:
                case eCharacterClass.Wizard:
                case eCharacterClass.Necromancer:
                case eCharacterClass.Occultist:
                case eCharacterClass.Eldritch:
                case eCharacterClass.Enchanter:
                case eCharacterClass.Mentalist:
                case eCharacterClass.Animist:
                    if (Util.Chance(20))
                        return eProperty.MaxMana;

                    //weight stats for casters towards dex, acu, con
                    //keep some 10% chance of str or quick since useful for carrying/occasional melee
                    if (rand <= 30)
                        return eProperty.Dexterity;
                    else if (rand <= 40)
                        return eProperty.Strength;
                    else if (rand <= 70)
                        return eProperty.Intelligence;
                    else if (rand <= 80)
                        return eProperty.Quickness;
                    else return eProperty.Constitution;

                case eCharacterClass.Runemaster:
                case eCharacterClass.Spiritmaster:
                case eCharacterClass.Bonedancer:
                    if (Util.Chance(20))
                        return eProperty.MaxMana;
                    //weight stats for casters towards dex, acu, con
                    //keep some 10% chance of str or quick since useful for carrying/occasional melee
                    if (rand <= 30)
                        return eProperty.Dexterity;
                    else if (rand <= 40)
                        return eProperty.Strength;
                    else if (rand <= 70)
                        return eProperty.Piety;
                    else if (rand <= 80)
                        return eProperty.Quickness;
                    else return eProperty.Constitution;

                case eCharacterClass.Paladin:
                    if (rand <= 25)
                        return eProperty.Strength;
                    else if (rand <= 40)
                        return eProperty.Dexterity;
                    else if (rand <= 60)
                        return eProperty.Quickness;
                    else if (rand <= 75)
                        return eProperty.Piety;
                    else return eProperty.Constitution;

                case eCharacterClass.Cleric:
                case eCharacterClass.Shaman:
                case eCharacterClass.Seer:
                    if (Util.Chance(20))
                        return eProperty.MaxMana;
                    if (rand <= 10)
                        return eProperty.Strength;
                    else if (rand <= 40)
                        return eProperty.Dexterity;
                    else if (rand <= 50)
                        return eProperty.Quickness;
                    else if (rand <= 80)
                        return eProperty.Piety;
                    else return eProperty.Constitution;

                case eCharacterClass.Thane:
                case eCharacterClass.Reaver:
                    if (Util.Chance(20))
                        return eProperty.MaxMana;
                    if (rand <= 20)
                        return eProperty.Strength;
                    else if (rand <= 40)
                        return eProperty.Dexterity;
                    else if (rand <= 65)
                        return eProperty.Quickness;
                    else if (rand <= 80)
                        return eProperty.Piety;
                    else return eProperty.Constitution;

                case eCharacterClass.Friar:
                    if (Util.Chance(20))
                        return eProperty.MaxMana;
                    if (rand <= 25)
                        return eProperty.Piety;
                    else if (rand <= 50)
                        return eProperty.Dexterity;
                    else if (rand <= 75)
                        return eProperty.Constitution;
                    else return eProperty.Quickness;


                case eCharacterClass.Druid:
                    if (Util.Chance(20))
                        return eProperty.MaxMana;
                    if (rand <= 10)
                        return eProperty.Strength;
                    else if (rand <= 40)
                        return eProperty.Dexterity;
                    else if (rand <= 50)
                        return eProperty.Quickness;
                    else if (rand <= 80)
                        return eProperty.Empathy;
                    else return eProperty.Constitution;

                case eCharacterClass.Warden:
                case eCharacterClass.Naturalist:
                    if (Util.Chance(10))
                        return eProperty.MaxMana;
                    if (rand <= 20)
                        return eProperty.Strength;
                    else if (rand <= 40)
                        return eProperty.Dexterity;
                    else if (rand <= 60)
                        return eProperty.Quickness;
                    else if (rand <= 80)
                        return eProperty.Empathy;
                    else return eProperty.Constitution;

                case eCharacterClass.Champion:
                case eCharacterClass.Valewalker:
                    if (Util.Chance(10))
                        return eProperty.MaxMana;
                    if (rand <= 22)
                        return eProperty.Strength;
                    else if (rand <= 44)
                        return eProperty.Dexterity;
                    else if (rand <= 66)
                        return eProperty.Quickness;
                    else if (rand <= 88)
                        return eProperty.Constitution;
                    else return eProperty.Intelligence;

                case eCharacterClass.Bard:
                case eCharacterClass.Skald:
                case eCharacterClass.Minstrel:
                    if (Util.Chance(20))
                        return eProperty.MaxMana;
                    if (rand <= 22)
                        return eProperty.Strength;
                    else if (rand <= 44)
                        return eProperty.Dexterity;
                    else if (rand <= 66)
                        return eProperty.Quickness;
                    else if (rand <= 88)
                        return eProperty.Constitution;
                    else return eProperty.Charisma;

                case eCharacterClass.Healer:
                    if (Util.Chance(15))
                        return eProperty.MaxMana;
                    if (rand <= 30)
                        return eProperty.Dexterity;
                    else if (rand <= 60)
                        return eProperty.Piety;
                    else if (rand <= 80)
                        return eProperty.Constitution;
                    else return eProperty.Strength;

                case eCharacterClass.Bainshee:
                case eCharacterClass.Acolyte:
                case eCharacterClass.Disciple:
                case eCharacterClass.Elementalist:
                case eCharacterClass.Forester:
                case eCharacterClass.Magician:
                case eCharacterClass.Mage:
                case eCharacterClass.Mystic:
                    if (Util.Chance(20)) return eProperty.MaxMana;
                    if (rand <= 30) return eProperty.Dexterity;
                    else if (rand <= 40) return eProperty.Strength;
                    else if (rand <= 70) return eProperty.Intelligence;
                    else if (rand <= 80) return eProperty.Quickness;
                    else return eProperty.Constitution;

                case eCharacterClass.Warlock:
                    if (Util.Chance(20)) return eProperty.MaxMana;
                    if (rand <= 30) return eProperty.Dexterity;
                    else if (rand <= 40) return eProperty.Strength;
                    else if (rand <= 70) return eProperty.Piety;
                    else if (rand <= 80) return eProperty.Quickness;
                    else return eProperty.Constitution;

                case eCharacterClass.Valkyrie:
                    if (Util.Chance(10)) return eProperty.MaxMana;
                    if (rand <= 20) return eProperty.Strength;
                    else if (rand <= 40) return eProperty.Dexterity;
                    else if (rand <= 65) return eProperty.Quickness;
                    else if (rand <= 80) return eProperty.Piety;
                    else return eProperty.Constitution;

                case eCharacterClass.Heretic:
                    if (Util.Chance(20)) return eProperty.MaxMana;
                    if (rand <= 25) return eProperty.Piety;
                    else if (rand <= 50) return eProperty.Dexterity;
                    else if (rand <= 75) return eProperty.Constitution;
                    else return eProperty.Quickness;

                case eCharacterClass.MaulerAlb:
                case eCharacterClass.MaulerMid:
                case eCharacterClass.MaulerHib:
                case eCharacterClass.Vampiir:
                    if (rand <= 25) return eProperty.Strength;
                    else if (rand <= 50) return eProperty.Dexterity;
                    else if (rand <= 75) return eProperty.Constitution;
                    else return eProperty.Quickness;
            }
            return eProperty.Constitution;

        }

        private bool SkillIsValidForClass(eProperty property)
        {
            if (property == eProperty.AllSkills) return true;

            switch (charClass)
            {
                // ALBION
                case eCharacterClass.Armsman:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Slashing || property == eProperty.Skill_Crushing || property == eProperty.Skill_Thrusting || property == eProperty.Skill_Two_Handed || property == eProperty.Skill_Shields || property == eProperty.Skill_Polearms || property == eProperty.Skill_Cross_Bows || property == eProperty.AllMeleeWeaponSkills;
                case eCharacterClass.Reaver:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Slashing || property == eProperty.Skill_Crushing || property == eProperty.Skill_Thrusting || property == eProperty.Skill_Flexible_Weapon || property == eProperty.Skill_Shields || property == eProperty.Skill_SoulRending || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Mercenary:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Slashing || property == eProperty.Skill_Crushing || property == eProperty.Skill_Thrusting || property == eProperty.Skill_Shields || property == eProperty.Skill_Dual_Wield || property == eProperty.AllDualWieldingSkills || property == eProperty.AllMeleeWeaponSkills;
                case eCharacterClass.Paladin:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Slashing || property == eProperty.Skill_Crushing || property == eProperty.Skill_Thrusting || property == eProperty.Skill_Two_Handed || property == eProperty.Skill_Shields || property == eProperty.Skill_Chants || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Cleric:
                    return property == eProperty.Skill_Rejuvenation || property == eProperty.Skill_Enhancement || property == eProperty.Skill_Smiting || property == eProperty.Skill_Shields || property == eProperty.Skill_Crushing || property == eProperty.AllMagicSkills;
                case eCharacterClass.Friar:
                    return property == eProperty.Skill_Rejuvenation || property == eProperty.Skill_Enhancement || property == eProperty.Skill_Parry || property == eProperty.Skill_Staff || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Infiltrator:
                    return property == eProperty.Skill_Stealth || property == eProperty.Skill_Envenom || property == eProperty.Skill_Slashing || property == eProperty.Skill_Thrusting || property == eProperty.Skill_Critical_Strike || property == eProperty.Skill_Dual_Wield || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllDualWieldingSkills;
                case eCharacterClass.Minstrel:
                    return property == eProperty.Skill_Stealth || property == eProperty.Skill_Instruments || property == eProperty.Skill_Slashing || property == eProperty.Skill_Thrusting || property == eProperty.Skill_Shields || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Scout:
                    return property == eProperty.Skill_Stealth || property == eProperty.Skill_Slashing || property == eProperty.Skill_Thrusting || property == eProperty.Skill_Shields || property == eProperty.Skill_Long_bows || property == eProperty.Skill_Cross_Bows || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllArcherySkills;
                case eCharacterClass.Cabalist:
                    return property == eProperty.Skill_Matter || property == eProperty.Skill_Body || property == eProperty.Skill_Spirit || property == eProperty.Focus_Matter || property == eProperty.Focus_Body || property == eProperty.Focus_Spirit || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Sorcerer:
                    return property == eProperty.Skill_Matter || property == eProperty.Skill_Body || property == eProperty.Skill_Mind || property == eProperty.Focus_Matter || property == eProperty.Focus_Body || property == eProperty.Focus_Mind || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Theurgist:
                    return property == eProperty.Skill_Earth || property == eProperty.Skill_Cold || property == eProperty.Skill_Wind || property == eProperty.Focus_Earth || property == eProperty.Focus_Cold || property == eProperty.Focus_Air || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Wizard:
                    return property == eProperty.Skill_Earth || property == eProperty.Skill_Cold || property == eProperty.Skill_Fire || property == eProperty.Focus_Earth || property == eProperty.Focus_Cold || property == eProperty.Focus_Fire || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Necromancer:
                    return property == eProperty.Skill_DeathSight || property == eProperty.Skill_Death_Servant || property == eProperty.Skill_Pain_working || property == eProperty.Focus_DeathSight || property == eProperty.Focus_DeathServant || property == eProperty.Focus_PainWorking || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Occultist:
                    return property == eProperty.Skill_Tormentshaper || property == eProperty.Skill_Wraithsight || property == eProperty.Skill_Void_Acolyte || property == eProperty.Focus_Tormentshaper || property == eProperty.Focus_Wraithsight || property == eProperty.Focus_Void_Acolyte || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Heretic:
                    return property == eProperty.Skill_Flexible_Weapon || property == eProperty.Skill_Crushing || property == eProperty.Skill_Shields || property == eProperty.Skill_Rejuvenation || property == eProperty.Skill_Enhancement || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.MaulerAlb:
                    return property == eProperty.Skill_FistWraps || property == eProperty.Skill_MaulerStaff || property == eProperty.Skill_Aura_Manipulation || property == eProperty.Skill_Magnetism || property == eProperty.Skill_Power_Strikes || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;

                // MIDGARD
                case eCharacterClass.Warrior:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Sword || property == eProperty.Skill_Axe || property == eProperty.Skill_Hammer || property == eProperty.Skill_Shields || property == eProperty.AllMeleeWeaponSkills;
                case eCharacterClass.Berserker:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Sword || property == eProperty.Skill_Axe || property == eProperty.Skill_Hammer || property == eProperty.Skill_Left_Axe || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllDualWieldingSkills;
                case eCharacterClass.Skald:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Sword || property == eProperty.Skill_Axe || property == eProperty.Skill_Hammer || property == eProperty.Skill_Battlesongs || property == eProperty.Skill_Shields || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Thane:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Sword || property == eProperty.Skill_Axe || property == eProperty.Skill_Hammer || property == eProperty.Skill_Stormcalling || property == eProperty.Skill_Shields || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Savage:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Sword || property == eProperty.Skill_Axe || property == eProperty.Skill_Hammer || property == eProperty.Skill_Savagery || property == eProperty.Skill_HandToHand || property == eProperty.AllMeleeWeaponSkills;
                case eCharacterClass.Valkyrie:
                    return property == eProperty.Skill_Parry || property == eProperty.Skill_Sword || property == eProperty.Skill_Spear || property == eProperty.Skill_Shields || property == eProperty.Skill_Mending || property == eProperty.Skill_OdinsWill || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Healer:
                    return property == eProperty.Skill_Mending || property == eProperty.Skill_Augmentation || property == eProperty.Skill_Pacification || property == eProperty.Skill_Shields || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Shaman:
                    return property == eProperty.Skill_Mending || property == eProperty.Skill_Augmentation || property == eProperty.Skill_Subterranean || property == eProperty.Skill_Shields || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Hunter:
                    return property == eProperty.Skill_BeastCraft || property == eProperty.Skill_Stealth || property == eProperty.Skill_Sword || property == eProperty.Skill_Spear || property == eProperty.Skill_Composite || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllArcherySkills;
                case eCharacterClass.Shadowblade:
                    return property == eProperty.Skill_Envenom || property == eProperty.Skill_Stealth || property == eProperty.Skill_Sword || property == eProperty.Skill_Axe || property == eProperty.Skill_Left_Axe || property == eProperty.Skill_Critical_Strike || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllDualWieldingSkills;
                case eCharacterClass.Runemaster:
                    return property == eProperty.Skill_Darkness || property == eProperty.Skill_Suppression || property == eProperty.Skill_Runecarving || property == eProperty.Focus_Darkness || property == eProperty.Focus_Suppression || property == eProperty.Focus_Runecarving || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Spiritmaster:
                    return property == eProperty.Skill_Darkness || property == eProperty.Skill_Suppression || property == eProperty.Skill_Summoning || property == eProperty.Focus_Darkness || property == eProperty.Focus_Suppression || property == eProperty.Focus_Summoning || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Bonedancer:
                    return property == eProperty.Skill_Darkness || property == eProperty.Skill_Suppression || property == eProperty.Skill_BoneArmy || property == eProperty.Focus_Darkness || property == eProperty.Focus_Suppression || property == eProperty.Focus_BoneArmy || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Warlock:
                    return property == eProperty.Skill_Hexing || property == eProperty.Skill_Cursing || property == eProperty.Skill_Witchcraft || property == eProperty.Focus_Hexing || property == eProperty.Focus_Cursing || property == eProperty.Focus_Witchcraft || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.MaulerMid:
                    return property == eProperty.Skill_FistWraps || property == eProperty.Skill_MaulerStaff || property == eProperty.Skill_Aura_Manipulation || property == eProperty.Skill_Magnetism || property == eProperty.Skill_Power_Strikes || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;

                // HIBERNIA
                case eCharacterClass.Bard:
                    return property == eProperty.Skill_Regrowth || property == eProperty.Skill_Nurture || property == eProperty.Skill_Music || property == eProperty.Skill_Blunt || property == eProperty.Skill_Blades || property == eProperty.Skill_Shields || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Druid:
                    return property == eProperty.Skill_Regrowth || property == eProperty.Skill_Nurture || property == eProperty.Skill_Nature || property == eProperty.Skill_Shields || property == eProperty.AllMagicSkills;
                case eCharacterClass.Warden:
                    return property == eProperty.Skill_Regrowth || property == eProperty.Skill_Nurture || property == eProperty.Skill_Blunt || property == eProperty.Skill_Blades || property == eProperty.Skill_Parry || property == eProperty.Skill_Shields || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Blademaster:
                    return property == eProperty.Skill_Blunt || property == eProperty.Skill_Blades || property == eProperty.Skill_Piercing || property == eProperty.Skill_Parry || property == eProperty.Skill_Shields || property == eProperty.Skill_Celtic_Dual || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllDualWieldingSkills;
                case eCharacterClass.Hero:
                    return property == eProperty.Skill_Blunt || property == eProperty.Skill_Blades || property == eProperty.Skill_Piercing || property == eProperty.Skill_Parry || property == eProperty.Skill_Shields || property == eProperty.Skill_Celtic_Spear || property == eProperty.Skill_Large_Weapon || property == eProperty.AllMeleeWeaponSkills;
                case eCharacterClass.Champion:
                    return property == eProperty.Skill_Blunt || property == eProperty.Skill_Blades || property == eProperty.Skill_Piercing || property == eProperty.Skill_Parry || property == eProperty.Skill_Shields || property == eProperty.Skill_Valor || property == eProperty.Skill_Large_Weapon || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.Eldritch:
                    return property == eProperty.Skill_Light || property == eProperty.Skill_Mana || property == eProperty.Skill_Void || property == eProperty.Focus_Light || property == eProperty.Focus_Mana || property == eProperty.Focus_Void || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Enchanter:
                    return property == eProperty.Skill_Light || property == eProperty.Skill_Mana || property == eProperty.Skill_Enchantments || property == eProperty.Focus_Light || property == eProperty.Focus_Mana || property == eProperty.Focus_Enchantments || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Mentalist:
                    return property == eProperty.Skill_Light || property == eProperty.Skill_Mana || property == eProperty.Skill_Mentalism || property == eProperty.Focus_Light || property == eProperty.Focus_Mana || property == eProperty.Focus_Mentalism || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Nightshade:
                    return property == eProperty.Skill_Envenom || property == eProperty.Skill_Blades || property == eProperty.Skill_Piercing || property == eProperty.Skill_Stealth || property == eProperty.Skill_Critical_Strike || property == eProperty.Skill_Celtic_Dual || property == eProperty.Skill_Shields || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllDualWieldingSkills;
                case eCharacterClass.Ranger:
                    return property == eProperty.Skill_RecurvedBow || property == eProperty.Skill_Blades || property == eProperty.Skill_Piercing || property == eProperty.Skill_Celtic_Dual || property == eProperty.Skill_Stealth || property == eProperty.Skill_Pathfinding || property == eProperty.Skill_Shields || property == eProperty.AllArcherySkills || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllDualWieldingSkills;
                case eCharacterClass.Animist:
                    return property == eProperty.Skill_Arboreal || property == eProperty.Skill_Creeping || property == eProperty.Skill_Verdant || property == eProperty.Focus_Arboreal || property == eProperty.Focus_CreepingPath || property == eProperty.Focus_Verdant || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Valewalker:
                    return property == eProperty.Skill_Arboreal || property == eProperty.Skill_Scythe || property == eProperty.Skill_Parry || property == eProperty.AllMagicSkills || property == eProperty.AllMeleeWeaponSkills;
                case eCharacterClass.Bainshee:
                    return property == eProperty.Skill_SpectralGuard || property == eProperty.Skill_PhantasmalWail || property == eProperty.Skill_EtherealShriek || property == eProperty.Skill_SpectralForce || property == eProperty.Focus_EtherealShriek || property == eProperty.Focus_PhantasmalWail || property == eProperty.Focus_SpectralForce || property == eProperty.AllFocusLevels || property == eProperty.AllMagicSkills;
                case eCharacterClass.Vampiir:
                    return property == eProperty.Skill_Piercing || property == eProperty.Skill_ShadowMastery || property == eProperty.Skill_VampiiricEmbrace || property == eProperty.Skill_Dementia || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
                case eCharacterClass.MaulerHib:
                    return property == eProperty.Skill_FistWraps || property == eProperty.Skill_MaulerStaff || property == eProperty.Skill_Aura_Manipulation || property == eProperty.Skill_Magnetism || property == eProperty.Skill_Power_Strikes || property == eProperty.AllMeleeWeaponSkills || property == eProperty.AllMagicSkills;
            }

            return false;
        }

        private bool StatIsValidForObjectType(eProperty property)
        {
            switch ((eObjectType)this.Object_Type)
            {
                case eObjectType.Magical:
                    return StatIsValidForRealm(property) && StatIsValidForClass(property);
                case eObjectType.Cloth:
                case eObjectType.Leather:
                case eObjectType.Studded:
                case eObjectType.Reinforced:
                case eObjectType.Chain:
                case eObjectType.Scale:
                case eObjectType.Plate:
                    return StatIsValidForArmor(property) && StatIsValidForClass(property);
                case eObjectType.Axe:
                case eObjectType.Blades:
                case eObjectType.Blunt:
                case eObjectType.CelticSpear:
                case eObjectType.CompositeBow:
                case eObjectType.Crossbow:
                case eObjectType.CrushingWeapon:
                case eObjectType.Fired:
                case eObjectType.Flexible:
                case eObjectType.Hammer:
                case eObjectType.HandToHand:
                case eObjectType.Instrument:
                case eObjectType.LargeWeapons:
                case eObjectType.LeftAxe:
                case eObjectType.Longbow:
                case eObjectType.Piercing:
                case eObjectType.PolearmWeapon:
                case eObjectType.RecurvedBow:
                case eObjectType.Scythe:
                case eObjectType.Shield:
                case eObjectType.SlashingWeapon:
                case eObjectType.Spear:
                case eObjectType.Staff:
                case eObjectType.Sword:
                case eObjectType.ThrustWeapon:
                case eObjectType.FistWraps: //Maulers
                case eObjectType.MaulerStaff: //Maulers
                case eObjectType.TwoHandedWeapon:
                    return StatIsValidForWeapon(property) && StatIsValidForClass(property);
            }
            return true;
        }

        private bool StatIsValidForClass(eProperty property)
        {
            switch (property)
            {
                case eProperty.MaxMana: //mana isn't a thing!! >:(
                case eProperty.PowerPool:
                case eProperty.PowerPoolCapBonus:
                    if (charClass == eCharacterClass.Armsman ||
                        charClass == eCharacterClass.Mercenary ||
                        charClass == eCharacterClass.Infiltrator ||
                        charClass == eCharacterClass.Scout ||
                        charClass == eCharacterClass.Paladin ||
                        charClass == eCharacterClass.Blademaster ||
                        charClass == eCharacterClass.Hero ||
                        charClass == eCharacterClass.Nightshade ||
                        charClass == eCharacterClass.Ranger ||
                        charClass == eCharacterClass.Berserker ||
                        charClass == eCharacterClass.Warrior ||
                        charClass == eCharacterClass.Savage ||
                        charClass == eCharacterClass.Shadowblade ||
                        charClass == eCharacterClass.Fighter ||
                        charClass == eCharacterClass.Viking ||
                        charClass == eCharacterClass.Guardian ||
                        charClass == eCharacterClass.AlbionRogue ||
                        charClass == eCharacterClass.MidgardRogue ||
                        charClass == eCharacterClass.Stalker)
                    {
                        return false;
                    }
                    return true;

                case eProperty.Acuity:
                    if (charClass == eCharacterClass.Armsman ||
                        charClass == eCharacterClass.Mercenary ||
                        charClass == eCharacterClass.Paladin ||
                        charClass == eCharacterClass.Reaver ||
                        charClass == eCharacterClass.Infiltrator ||
                        charClass == eCharacterClass.Scout ||
                        charClass == eCharacterClass.Warden ||
                        charClass == eCharacterClass.Champion ||
                        charClass == eCharacterClass.Nightshade ||
                        charClass == eCharacterClass.Ranger ||
                        charClass == eCharacterClass.Blademaster ||
                        charClass == eCharacterClass.Hero ||
                        charClass == eCharacterClass.Hunter ||
                        charClass == eCharacterClass.Berserker ||
                        charClass == eCharacterClass.Warrior ||
                        charClass == eCharacterClass.Savage ||
                        charClass == eCharacterClass.Shadowblade ||
                        charClass == eCharacterClass.Fighter ||
                        charClass == eCharacterClass.Viking ||
                        charClass == eCharacterClass.Guardian ||
                        charClass == eCharacterClass.AlbionRogue ||
                        charClass == eCharacterClass.MidgardRogue ||
                        charClass == eCharacterClass.Stalker)
                    {
                        return false;
                    }
                    return true;
                default:
                    return true;
            }
        }

        private bool SkillIsValidForObjectType(eProperty property)
        {
            switch ((eObjectType)this.Object_Type)
            {
                case eObjectType.Magical:
                    return SkillIsValidForMagical(property);
                case eObjectType.Cloth:
                case eObjectType.Leather:
                case eObjectType.Studded:
                case eObjectType.Reinforced:
                case eObjectType.Chain:
                case eObjectType.Scale:
                case eObjectType.Plate:
                    return SkillIsValidForArmor(property);
                case eObjectType.Axe:
                case eObjectType.Blades:
                case eObjectType.Blunt:
                case eObjectType.CelticSpear:
                case eObjectType.CompositeBow:
                case eObjectType.Crossbow:
                case eObjectType.CrushingWeapon:
                case eObjectType.Fired:
                case eObjectType.Flexible:
                case eObjectType.Hammer:
                case eObjectType.HandToHand:
                case eObjectType.Instrument:
                case eObjectType.LargeWeapons:
                case eObjectType.LeftAxe:
                case eObjectType.Longbow:
                case eObjectType.Piercing:
                case eObjectType.PolearmWeapon:
                case eObjectType.RecurvedBow:
                case eObjectType.Scythe:
                case eObjectType.Shield:
                case eObjectType.SlashingWeapon:
                case eObjectType.Spear:
                case eObjectType.Staff:
                case eObjectType.Sword:
                case eObjectType.ThrustWeapon:
                case eObjectType.MaulerStaff:
                case eObjectType.FistWraps:
                case eObjectType.TwoHandedWeapon:
                    return SkillIsValidForWeapon(property);
            }
            return true;
        }

        private bool SkillIsValidForMagical(eProperty property)
        {
            int level = this.Level;
            eRealm realm = (eRealm)this.Realm;
            eObjectType type = (eObjectType)this.Object_Type;
            eCharacterClass charClass = this.charClass;

            switch (property)
            {
                case eProperty.Skill_Augmentation:
                    {
                        if (charClass != eCharacterClass.Healer &&
                            charClass != eCharacterClass.Shaman)
                        {
                            return false;
                        }
                        else { return true; }

                    }
                case eProperty.Skill_Axe:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Shadowblade)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.Skill_Battlesongs:
                    {
                        if (charClass != eCharacterClass.Skald)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.Skill_Pathfinding:
                    {
                        if (charClass != eCharacterClass.Ranger)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_BeastCraft:
                    {
                        if (charClass != eCharacterClass.Hunter)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Blades:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.Skill_Blunt:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Bard &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden)
                        {
                            return false;
                        }
                        return true;
                    }
                //Cloth skills
                //witchcraft is unused except as a goto target for cloth checks
                case eProperty.Skill_Arboreal:
                    if (charClass != eCharacterClass.Valewalker &&
                        charClass != eCharacterClass.Animist)
                    {
                        return false;
                    }
                    return true;
                case eProperty.Skill_Matter:
                case eProperty.Skill_Body:
                    {
                        if (charClass != eCharacterClass.Cabalist &&
                            charClass != eCharacterClass.Sorcerer)
                        {
                            return false;
                        }
                        return true;
                    }

                case eProperty.Skill_Earth:
                case eProperty.Skill_Cold:
                    {
                        if (charClass != eCharacterClass.Theurgist &&
                            charClass != eCharacterClass.Wizard)
                        {
                            return false;
                        }
                        return true;
                    }

                case eProperty.Skill_Suppression:
                case eProperty.Skill_Darkness:
                    {
                        if (charClass != eCharacterClass.Spiritmaster &&
                            charClass != eCharacterClass.Runemaster &&
                            charClass != eCharacterClass.Bonedancer)
                        {
                            return false;
                        }
                        return true;
                    }

                case eProperty.Skill_Light:
                case eProperty.Skill_Mana:
                    {
                        if (charClass != eCharacterClass.Enchanter &&
                            charClass != eCharacterClass.Eldritch &&
                            charClass != eCharacterClass.Mentalist)
                        {
                            return false;
                        }
                        return true;
                    }


                case eProperty.Skill_Mind:
                    if (charClass != eCharacterClass.Sorcerer) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_Spirit:
                    if (charClass != eCharacterClass.Cabalist) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_Wind:
                    if (charClass != eCharacterClass.Theurgist) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_Fire:
                    if (charClass != eCharacterClass.Wizard) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_Death_Servant:
                case eProperty.Skill_DeathSight:
                case eProperty.Skill_Pain_working:
                    if (charClass != eCharacterClass.Necromancer) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_Void_Acolyte:
                case eProperty.Skill_Wraithsight:
                case eProperty.Skill_Tormentshaper:
                    if (charClass != eCharacterClass.Occultist) { return false; }
                    goto case eProperty.Skill_Witchcraft;

                case eProperty.Skill_Summoning:
                    if (charClass != eCharacterClass.Spiritmaster) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_Runecarving:
                    if (charClass != eCharacterClass.Runemaster) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_BoneArmy:
                    if (charClass != eCharacterClass.Bonedancer) { return false; }
                    goto case eProperty.Skill_Witchcraft;

                case eProperty.Skill_Void:
                    if (charClass != eCharacterClass.Eldritch) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_Enchantments:
                    if (charClass != eCharacterClass.Enchanter) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_Mentalism:
                    if (charClass != eCharacterClass.Mentalist) { return false; }
                    goto case eProperty.Skill_Witchcraft;
                case eProperty.Skill_Creeping:
                case eProperty.Skill_Verdant:
                    if (charClass != eCharacterClass.Animist) { return false; }
                    goto case eProperty.Skill_Witchcraft;

                case eProperty.Skill_Hexing:
                case eProperty.Skill_Cursing:
                case eProperty.Skill_Witchcraft:
                    if (property == eProperty.Skill_Witchcraft) return false;
                    return (charClass == eCharacterClass.Warlock);

                case eProperty.Skill_EtherealShriek:
                case eProperty.Skill_PhantasmalWail:
                case eProperty.Skill_SpectralGuard:
                case eProperty.Skill_SpectralForce:
                    if (property == eProperty.Skill_SpectralForce) return false;
                    return (charClass == eCharacterClass.Bainshee);
                case eProperty.Skill_Celtic_Dual:
                    {
                        if (charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Nightshade)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.Skill_Celtic_Spear:
                    {
                        if (charClass != eCharacterClass.Hero)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Chants:
                    {
                        if (charClass != eCharacterClass.Paladin)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Composite:
                case eProperty.Skill_RecurvedBow:
                case eProperty.Skill_Long_bows:
                case eProperty.Skill_Archery:
                    {
                        if (charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Hunter)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Critical_Strike:
                case eProperty.Skill_Envenom:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Shadowblade)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Dementia:
                case eProperty.Skill_ShadowMastery:
                case eProperty.Skill_VampiiricEmbrace:
                    {
                        if (charClass != eCharacterClass.Vampiir)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Nightshade:
                    {
                        if (charClass != eCharacterClass.Nightshade)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Cross_Bows:
                    {
                        if (charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.Skill_Crushing:
                    {
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Dual_Wield:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.Skill_Enhancement:
                    {
                        if (charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Cleric &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Flexible_Weapon:
                    {
                        if (charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Heretic) { return false; }
                        return true;
                    }
                case eProperty.Skill_Hammer:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_HandToHand:
                    {
                        if (charClass != eCharacterClass.Savage) { return false; }
                        return true;
                    }
                case eProperty.Skill_Instruments:
                    {
                        if (charClass != eCharacterClass.Minstrel) { return false; }
                        return true;
                    }
                case eProperty.Skill_Large_Weapon:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Left_Axe:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Shadowblade)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Music:
                    {
                        if (charClass != eCharacterClass.Bard) { return false; }
                        return true;
                    }
                case eProperty.Skill_Nature:
                    {
                        if (charClass != eCharacterClass.Druid) { return false; }
                        return true;
                    }
                case eProperty.Skill_Nurture:
                case eProperty.Skill_Regrowth:
                    {
                        if (charClass != eCharacterClass.Bard &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Druid)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_OdinsWill:
                    {
                        if (charClass != eCharacterClass.Valkyrie)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Pacification:
                    {
                        if (charClass != eCharacterClass.Healer) { return false; }
                        return true;
                    }
                case eProperty.Skill_Parry:
                    {
                        if (charClass != eCharacterClass.Berserker && //midgard
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Champion && //hibernia
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Armsman && //albion
                            charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Valkyrie)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.Skill_Piercing:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Vampiir)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Polearms:
                    {
                        if (charClass != eCharacterClass.Armsman) { return false; }
                        return true;
                    }
                case eProperty.Skill_Rejuvenation:
                    {
                        if (charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Cleric &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Savagery:
                    {
                        if (charClass != eCharacterClass.Savage) { return false; }
                        return true;
                    }
                case eProperty.Skill_Scythe:
                    {
                        if (charClass != eCharacterClass.Valewalker) { return false; }
                        return true;
                    }
                case eProperty.Skill_Shields:
                    {
                        if (charClass != eCharacterClass.Thane &&  //midgard
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Valkyrie &&
                            charClass != eCharacterClass.Champion && //hibernia
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Armsman && //albion
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_ShortBow:
                    {
                        return false;
                    }
                case eProperty.Skill_Smiting:
                    {
                        if (charClass != eCharacterClass.Cleric) { return false; }
                        return true;
                    }
                case eProperty.Skill_SoulRending:
                    {
                        if (charClass != eCharacterClass.Reaver) { return false; }
                        return true;
                    }
                case eProperty.Skill_Spear:
                    {
                        if (charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Valkyrie) { return false; }
                        return true;
                    }
                case eProperty.Skill_Staff:
                    {
                        if (charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Heretic) { return false; }
                        return true;
                    }
                case eProperty.Skill_Stealth:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Stormcalling:
                    {
                        if (charClass != eCharacterClass.Thane) { return false; }
                        return true;
                    }
                case eProperty.Skill_Subterranean:
                    {
                        if (charClass != eCharacterClass.Shaman) { return false; }
                        return true;
                    }
                case eProperty.Skill_Sword:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Valkyrie)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Slashing:
                    {
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.Skill_Thrusting:
                    {

                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.Skill_Two_Handed:
                    {
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Paladin)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.Skill_Valor:
                    {
                        if (charClass != eCharacterClass.Champion) { return false; }
                        return true;
                    }
                case eProperty.Skill_Mending:
                    {
                        if (charClass != eCharacterClass.Healer &&
                            charClass != eCharacterClass.Valkyrie)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.AllArcherySkills:
                    {
                        if (charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Ranger)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.AllDualWieldingSkills:
                    {
                        if (charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary)
                        {
                            return false;
                        }
                        return true;
                    }
                case eProperty.AllMagicSkills:
                    {
                        if (charClass != eCharacterClass.Cabalist && //albion
                            charClass != eCharacterClass.Cleric &&
                            charClass != eCharacterClass.Necromancer &&
                            charClass != eCharacterClass.Occultist &&
                            charClass != eCharacterClass.Sorcerer &&
                            charClass != eCharacterClass.Theurgist &&
                            charClass != eCharacterClass.Wizard &&
                            charClass != eCharacterClass.Heretic &&
                            charClass != eCharacterClass.Animist && //hibernia
                            charClass != eCharacterClass.Eldritch &&
                            charClass != eCharacterClass.Enchanter &&
                            charClass != eCharacterClass.Mentalist &&
                            charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Bainshee &&
                            charClass != eCharacterClass.Vampiir &&
                            charClass != eCharacterClass.Bonedancer && //midgard
                            charClass != eCharacterClass.Runemaster &&
                            charClass != eCharacterClass.Spiritmaster &&
                            charClass != eCharacterClass.Warlock &&
                            charClass != eCharacterClass.Valkyrie &&
                            charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.AllMeleeWeaponSkills:
                    {
                        if (charClass != eCharacterClass.Berserker &&  //midgard
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Valkyrie &&
                            charClass != eCharacterClass.Blademaster && //hibernia
                            charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Vampiir &&
                            charClass != eCharacterClass.Armsman && //albion
                            charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Heretic &&
                            charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }

                        return true;
                    }
                case eProperty.AllSkills:
                    {
                        return true;
                    }
                case eProperty.Skill_Power_Strikes:
                case eProperty.Skill_Magnetism:
                case eProperty.Skill_MaulerStaff:
                case eProperty.Skill_Aura_Manipulation:
                case eProperty.Skill_FistWraps:
                    {
                        if (charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }
                        return true;
                    }

            }

            return false;
        }

        private bool SkillIsValidForArmor(eProperty property)
        {
            int level = this.Level;
            eRealm realm = (eRealm)this.Realm;
            eObjectType type = (eObjectType)this.Object_Type;
            eCharacterClass charClass = this.charClass;

            switch (property)
            {
                case eProperty.Skill_Mending:
                case eProperty.Skill_Augmentation:
                    {
                        if (charClass == eCharacterClass.Valkyrie)
                        {
                            if (property == eProperty.Skill_Augmentation)
                                return false;
                            if (level < 10)
                            {
                                if (type == eObjectType.Studded)
                                    return true;
                                return false;
                            }
                            else
                            {
                                if (type == eObjectType.Chain)
                                    return true;
                                return false;
                            }
                        }

                        if (charClass != eCharacterClass.Healer &&
                            charClass != eCharacterClass.Shaman)
                        {
                            return false;
                        }
                        if (level < 10)
                        {
                            if (type == eObjectType.Leather)
                                return true;
                            return false;
                        }
                        else if (level < 20)
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Chain)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Axe:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Shadowblade)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather || type == eObjectType.Studded)
                            return true;
                        else if (type == eObjectType.Chain && level >= 10)
                            return true;

                        return false;
                    }
                case eProperty.Skill_Battlesongs:
                    {
                        if (charClass != eCharacterClass.Skald)
                        {
                            return false;
                        }
                        if (level < 20)
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Chain)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Pathfinding:
                    {
                        if (charClass != eCharacterClass.Ranger)
                        {
                            return false;
                        }
                        if (level < 10)
                        {
                            if (type == eObjectType.Leather)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Reinforced)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_BeastCraft:
                    {
                        if (charClass != eCharacterClass.Hunter)
                        {
                            return false;
                        }
                        if (level < 10)
                        {
                            if (type == eObjectType.Leather)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Blades:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Bard &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden)
                        {
                            return false;
                        }

                        if (type == eObjectType.Leather || type == eObjectType.Reinforced || type == eObjectType.Scale)
                            return true;
                        return false;
                    }
                case eProperty.Skill_Blunt:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Bard &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden)
                        {
                            return false;
                        }

                        if (type == eObjectType.Leather && level < 10)
                            return true;
                        else if (type == eObjectType.Reinforced || type == eObjectType.Scale)
                            return true;
                        return false;
                    }
                //Cloth skills
                case eProperty.Skill_Arboreal:
                    {
                        if (charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Animist)
                        {
                            return false;
                        }
                        if (type == eObjectType.Cloth)
                            return true;
                        return false;
                    }


                case eProperty.Skill_Matter:
                case eProperty.Skill_Body:
                    {
                        if (charClass != eCharacterClass.Cabalist &&
                            charClass != eCharacterClass.Sorcerer)
                        {
                            return false;
                        }
                        if (type == eObjectType.Cloth)
                            return true;
                        return false;
                    }

                case eProperty.Skill_Earth:
                case eProperty.Skill_Cold:
                    {
                        if (charClass != eCharacterClass.Theurgist &&
                            charClass != eCharacterClass.Wizard)
                        {
                            return false;
                        }
                        if (type == eObjectType.Cloth)
                            return true;
                        return false;
                    }

                case eProperty.Skill_Suppression:
                case eProperty.Skill_Darkness:
                    {
                        if (charClass != eCharacterClass.Spiritmaster &&
                            charClass != eCharacterClass.Runemaster &&
                            charClass != eCharacterClass.Bonedancer)
                        {
                            return false;
                        }
                        if (type == eObjectType.Cloth)
                            return true;
                        return false;
                    }

                case eProperty.Skill_Light:
                case eProperty.Skill_Mana:
                    {
                        if (charClass != eCharacterClass.Enchanter &&
                            charClass != eCharacterClass.Eldritch &&
                            charClass != eCharacterClass.Mentalist)
                        {
                            return false;
                        }
                        if (type == eObjectType.Cloth)
                            return true;
                        return false;
                    }

                case eProperty.Skill_Mind:
                    if (charClass != eCharacterClass.Sorcerer) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_Spirit:
                    if (charClass != eCharacterClass.Cabalist) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_Wind:
                    if (charClass != eCharacterClass.Theurgist) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_Fire:
                    if (charClass != eCharacterClass.Wizard) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_Death_Servant:
                case eProperty.Skill_DeathSight:
                case eProperty.Skill_Pain_working:
                    if (charClass != eCharacterClass.Necromancer) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_Void_Acolyte:
                case eProperty.Skill_Wraithsight:
                case eProperty.Skill_Tormentshaper:
                    if (charClass != eCharacterClass.Occultist) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;

                case eProperty.Skill_Summoning:
                    if (charClass != eCharacterClass.Spiritmaster) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_Runecarving:
                    if (charClass != eCharacterClass.Runemaster) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_BoneArmy:
                    if (charClass != eCharacterClass.Bonedancer) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;

                case eProperty.Skill_Void:
                    if (charClass != eCharacterClass.Eldritch) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_Enchantments:
                    if (charClass != eCharacterClass.Enchanter) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_Mentalism:
                    if (charClass != eCharacterClass.Mentalist) { return false; }
                    if (type == eObjectType.Cloth) { return true; }
                    return false;
                case eProperty.Skill_Creeping:
                case eProperty.Skill_Verdant:
                    {
                        if (charClass != eCharacterClass.Animist &&
                            charClass != eCharacterClass.Valewalker) { return false; }
                        if (type == eObjectType.Cloth) { return true; }
                        return false;
                    }

                case eProperty.Skill_Hexing:
                case eProperty.Skill_Cursing:
                case eProperty.Skill_Witchcraft:
                    if (property == eProperty.Skill_Witchcraft) return false;
                    return (charClass == eCharacterClass.Warlock && type == eObjectType.Cloth);

                case eProperty.Skill_EtherealShriek:
                case eProperty.Skill_PhantasmalWail:
                case eProperty.Skill_SpectralGuard:
                case eProperty.Skill_SpectralForce:
                    if (property == eProperty.Skill_SpectralForce) return false;
                    return (charClass == eCharacterClass.Bainshee && type == eObjectType.Cloth);
                case eProperty.Skill_Celtic_Dual:
                    {
                        if (charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Nightshade)
                        {
                            return false;
                        }

                        if (type == eObjectType.Leather ||
                            type == eObjectType.Reinforced)
                            return true;
                        return false;
                    }
                case eProperty.Skill_Celtic_Spear:
                    {
                        if (charClass != eCharacterClass.Hero)
                        {
                            return false;
                        }
                        if (level < 15)
                        {
                            if (type == eObjectType.Reinforced)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Scale)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Chants:
                    {
                        if (charClass != eCharacterClass.Paladin)
                        {
                            return false;
                        }
                        return false;
                    }
                case eProperty.Skill_Composite:
                case eProperty.Skill_RecurvedBow:
                case eProperty.Skill_Long_bows:
                case eProperty.Skill_Archery:
                    {
                        if (charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Hunter)
                        {
                            return false;
                        }
                        if (level < 10)
                        {
                            if (type == eObjectType.Leather)
                                return true;

                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Studded || type == eObjectType.Reinforced)
                                return true;

                            return false;
                        }
                    }
                case eProperty.Skill_Critical_Strike:
                case eProperty.Skill_Envenom:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Shadowblade)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather)
                            return true;
                        return false;
                    }
                case eProperty.Skill_Dementia:
                case eProperty.Skill_ShadowMastery:
                case eProperty.Skill_VampiiricEmbrace:
                    {
                        if (charClass != eCharacterClass.Vampiir)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather)
                            return true;
                        return false;
                    }
                case eProperty.Skill_Nightshade:
                    {
                        if (charClass != eCharacterClass.Nightshade)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather)
                            return true;
                        return false;
                    }
                case eProperty.Skill_Cross_Bows:
                    {
                        return false; // disabled for armor

                        //						if (level < 15)
                        //						{
                        //							if (type == eObjectType.Chain)
                        //								return true;
                        //							return false;
                        //						}
                        //						else
                        //						{
                        //							if (type == eObjectType.Plate)
                        //								return true;
                        //							return false;
                        //						}
                    }
                case eProperty.Skill_Crushing:
                    {
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        if (realm == eRealm.Albion && type == eObjectType.Cloth) // heretic
                            return true;

                        if (level < 15)
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Chain || type == eObjectType.Plate)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Dual_Wield:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary)
                        {
                            return false;
                        }

                        if (level < 20)
                        {
                            if (type == eObjectType.Leather || type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Leather || type == eObjectType.Chain)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Enhancement:
                    {
                        if (charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Cleric &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather || type == eObjectType.Cloth)
                            return true;

                        if (level < 20)
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Chain)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Flexible_Weapon:
                    {
                        if (charClass == eCharacterClass.Heretic && type == eObjectType.Cloth)
                        {
                            return true;
                        }
                        if (charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Heretic) { return false; }

                        if (level < 10)
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Chain)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Hammer:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior)
                        {
                            return false;
                        }
                        if (level < 10)
                        {
                            if (type == eObjectType.Leather)
                                return true;
                            return false;
                        }
                        if (level < 20)
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Chain)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_HandToHand:
                    {
                        if (charClass != eCharacterClass.Savage) { return false; }
                        if (type == eObjectType.Studded)
                            return true;
                        return false;
                    }
                case eProperty.Skill_Instruments:
                    {
                        if (charClass != eCharacterClass.Minstrel) { return false; }
                        if (level < 10)
                        {
                            if (type == eObjectType.Leather)
                                return true;
                            return false;
                        }
                        else if (level < 20)
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Chain)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Large_Weapon:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero)
                        {
                            return false;
                        }
                        if (level < 15)
                        {
                            if (type == eObjectType.Reinforced)
                                return true;

                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Scale)
                                return true;

                            return false;
                        }
                    }
                case eProperty.Skill_Left_Axe:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Shadowblade)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather || type == eObjectType.Studded)
                            return true;
                        break;
                    }
                case eProperty.Skill_Music:
                    {
                        if (charClass != eCharacterClass.Bard) { return false; }
                        if (level < 15)
                        {
                            if (type == eObjectType.Leather)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Reinforced)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Nature:
                    {
                        if (charClass != eCharacterClass.Druid) { return false; }
                        if (level < 10)
                        {
                            if (type == eObjectType.Leather)
                                return true;
                            return false;
                        }
                        else if (level < 20)
                        {
                            if (type == eObjectType.Reinforced)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Scale)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Nurture:
                case eProperty.Skill_Regrowth:
                    {
                        if (charClass != eCharacterClass.Bard &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Druid)
                        {
                            return false;
                        }
                        if (level < 10)
                        {
                            if (type == eObjectType.Leather)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Reinforced || type == eObjectType.Scale)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_OdinsWill:
                    {
                        if (charClass != eCharacterClass.Valkyrie)
                        {
                            return false;
                        }
                        if (level < 10)
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Chain)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Pacification:
                    {
                        if (charClass != eCharacterClass.Healer) { return false; }
                        if (level < 10)
                        {
                            if (type == eObjectType.Leather)
                                return true;
                            return false;
                        }
                        else if (level < 20)
                        {
                            if (type == eObjectType.Studded)
                                return true;
                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Chain)
                                return true;
                            return false;
                        }
                    }
                case eProperty.Skill_Parry:
                    {
                        if (charClass != eCharacterClass.Berserker && //midgard
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Champion && //hibernia
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Armsman && //albion
                            charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Valkyrie)
                        {
                            return false;
                        }

                        if (type == eObjectType.Cloth && realm == eRealm.Hibernia && level >= 5)
                            return true;
                        else if (realm == eRealm.Hibernia && level < 2)
                            return false;
                        else if (realm == eRealm.Albion && level < 5)
                            return false;
                        else if (realm == eRealm.Albion && level < 10 && type == eObjectType.Studded)
                            return true;
                        else if (realm == eRealm.Albion && level >= 10 && (type == eObjectType.Leather || type == eObjectType.Chain || type == eObjectType.Plate))
                            return true;
                        else if (realm == eRealm.Hibernia && level < 20 && type == eObjectType.Reinforced)
                            return true;
                        else if (realm == eRealm.Hibernia && level >= 15 && type == eObjectType.Scale)
                            return true;
                        else if (realm == eRealm.Midgard && (type == eObjectType.Studded || type == eObjectType.Chain))
                            return true;

                        break;
                    }
                case eProperty.Skill_Piercing:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Vampiir)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather || type == eObjectType.Reinforced || type == eObjectType.Scale)
                            return true;
                        return false;
                    }
                case eProperty.Skill_Polearms:
                    {
                        if (charClass != eCharacterClass.Armsman) { return false; }
                        if (level < 5 && type == eObjectType.Studded)
                        {
                            return true;
                        }
                        else if (level < 15)
                        {
                            if (type == eObjectType.Chain)
                                return true;

                            return false;
                        }
                        else
                        {
                            if (type == eObjectType.Plate)
                                return true;

                            return false;
                        }
                    }
                case eProperty.Skill_Rejuvenation:
                    {
                        if (charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Cleric &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        if (type == eObjectType.Cloth)
                            return true;
                        else if (type == eObjectType.Leather)
                            return true;
                        else if (type == eObjectType.Studded && level >= 10 && level < 20)
                            return true;
                        else if (type == eObjectType.Chain && level >= 20)
                            return true;
                        break;
                    }
                case eProperty.Skill_Savagery:
                    {
                        if (charClass != eCharacterClass.Savage) { return false; }
                        if (type == eObjectType.Studded)
                            return true;
                        break;
                    }
                case eProperty.Skill_Scythe:
                    {
                        if (charClass != eCharacterClass.Valewalker) { return false; }
                        if (type == eObjectType.Cloth)
                            return true;
                        break;
                    }
                case eProperty.Skill_Shields:
                    {
                        if (charClass != eCharacterClass.Thane &&  //midgard
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Valkyrie &&
                            charClass != eCharacterClass.Champion && //hibernia
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Armsman && //albion
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        if (type == eObjectType.Cloth && realm == eRealm.Albion)
                            return true;
                        else if (type == eObjectType.Studded || type == eObjectType.Chain || type == eObjectType.Reinforced || type == eObjectType.Scale || type == eObjectType.Plate)
                            return true;
                        break;
                    }
                case eProperty.Skill_ShortBow:
                    {
                        return false;
                    }
                case eProperty.Skill_Smiting:
                    {
                        if (charClass != eCharacterClass.Cleric) { return false; }
                        if (type == eObjectType.Leather && level < 10)
                            return true;
                        else if (type == eObjectType.Studded && level < 20)
                            return true;
                        else if (type == eObjectType.Chain && level >= 20)
                            return true;
                        break;
                    }
                case eProperty.Skill_SoulRending:
                    {
                        if (charClass != eCharacterClass.Reaver) { return false; }
                        if (type == eObjectType.Studded && level < 10)
                            return true;
                        else if (type == eObjectType.Chain && level >= 10)
                            return true;
                        break;
                    }
                case eProperty.Skill_Spear:
                    {
                        if (charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Valkyrie) { return false; }
                        if (type == eObjectType.Leather && level < 10)
                            return true;
                        else if (type == eObjectType.Studded)
                            return true;
                        else if (type == eObjectType.Chain && level >= 10)
                            return true;
                        break;
                    }
                case eProperty.Skill_Staff:
                    {
                        if (charClass != eCharacterClass.Friar) { return false; }
                        if (type == eObjectType.Leather && realm == eRealm.Albion)
                            return true;
                        break;
                    }
                case eProperty.Skill_Stealth:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather || type == eObjectType.Studded || type == eObjectType.Reinforced)
                            return true;
                        else if (realm == eRealm.Albion && level >= 20 && type == eObjectType.Chain)
                            return true;
                        break;
                    }
                case eProperty.Skill_Stormcalling:
                    {
                        if (charClass != eCharacterClass.Thane) { return false; }
                        if (type == eObjectType.Studded && level < 10)
                            return true;
                        else if (type == eObjectType.Chain && level >= 10)
                            return true;
                        break;
                    }
                case eProperty.Skill_Subterranean:
                    {
                        if (charClass != eCharacterClass.Shaman) { return false; }
                        if (type == eObjectType.Leather && level < 10)
                            return true;
                        else if (type == eObjectType.Studded && level < 20)
                            return true;
                        else if (type == eObjectType.Chain && level >= 20)
                            return true;
                        break;
                    }
                case eProperty.Skill_Sword:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Valkyrie)
                        {
                            return false;
                        }
                        if (type == eObjectType.Studded || type == eObjectType.Chain)
                            return true;
                        break;
                    }
                case eProperty.Skill_Slashing:
                    {
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }

                        if (type == eObjectType.Leather || type == eObjectType.Studded || type == eObjectType.Chain || type == eObjectType.Plate)
                            return true;
                        break;
                    }
                case eProperty.Skill_Thrusting:
                    {

                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }

                        if (type == eObjectType.Leather || type == eObjectType.Studded || type == eObjectType.Chain || type == eObjectType.Plate)
                            return true;
                        break;
                    }
                case eProperty.Skill_Two_Handed:
                    {
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Paladin)
                        {
                            return false;
                        }
                        if (type == eObjectType.Studded && level < 10)
                            return true;
                        else if (type == eObjectType.Chain && level < 20)
                            return true;
                        else if (type == eObjectType.Plate)
                            return true;
                        break;
                    }
                case eProperty.Skill_Valor:
                    {
                        if (charClass != eCharacterClass.Champion) { return false; }
                        if (type == eObjectType.Reinforced && level < 20)
                            return true;
                        else if (type == eObjectType.Scale)
                            return true;
                        break;
                    }
                case eProperty.AllArcherySkills:
                    {
                        if (charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Ranger)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather && level < 10)
                            return true;
                        else if (level >= 10 && (type == eObjectType.Reinforced || type == eObjectType.Studded))
                            return true;

                        break;
                    }
                case eProperty.AllDualWieldingSkills:
                    {
                        if (charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary)
                        {
                            return false;
                        }
                        //Dualwielders are always above level 4 and can wear better than cloth from the start.
                        if (type == eObjectType.Cloth)
                            return false;
                        //mercs are the only dualwielder who can wear chain
                        else if (realm == eRealm.Albion && type == eObjectType.Studded && level < 10)
                            return true;
                        else if (realm == eRealm.Albion && type == eObjectType.Chain)
                            return true;
                        //all assassins wear leather, blademasters and zerks wear studded.
                        else if (type == eObjectType.Leather || type == eObjectType.Reinforced || (type == eObjectType.Studded && realm == eRealm.Midgard))
                            return true;
                        break;
                    }
                case eProperty.AllMagicSkills:
                    {
                        if (charClass != eCharacterClass.Cabalist && //albion
                            charClass != eCharacterClass.Cleric &&
                            charClass != eCharacterClass.Necromancer &&
                            charClass != eCharacterClass.Occultist &&
                            charClass != eCharacterClass.Sorcerer &&
                            charClass != eCharacterClass.Theurgist &&
                            charClass != eCharacterClass.Wizard &&
                            charClass != eCharacterClass.Heretic &&
                            charClass != eCharacterClass.Animist && //hibernia
                            charClass != eCharacterClass.Eldritch &&
                            charClass != eCharacterClass.Enchanter &&
                            charClass != eCharacterClass.Mentalist &&
                            charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Bainshee &&
                            charClass != eCharacterClass.Vampiir &&
                            charClass != eCharacterClass.Bonedancer &&
                            charClass != eCharacterClass.Runemaster &&
                            charClass != eCharacterClass.Spiritmaster &&
                            charClass != eCharacterClass.Warlock &&
                            charClass != eCharacterClass.Valkyrie &&
                            charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid && 
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }

                        // not for scouts
                        if (realm == eRealm.Albion && type == eObjectType.Studded && level >= 20)
                            return false;
                        // Paladins can't use + magic skills
                        if (realm == eRealm.Albion && type == eObjectType.Plate)
                            return false;

                        return true;
                    }
                case eProperty.AllMeleeWeaponSkills:
                    {
                        if (charClass != eCharacterClass.Berserker &&  //midgard
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Valkyrie &&
                            charClass != eCharacterClass.Blademaster && //hibernia
                            charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Vampiir &&
                            charClass != eCharacterClass.Armsman && //albion
                            charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Heretic &&
                            charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }

                        if (realm == eRealm.Midgard && type == eObjectType.Cloth)
                            return false;
                        else if (level >= 5)
                            return true;

                        break;
                    }
                case eProperty.AllSkills:
                    {
                        return true;
                    }
                case eProperty.Skill_Power_Strikes:
                case eProperty.Skill_Magnetism:
                case eProperty.Skill_MaulerStaff:
                case eProperty.Skill_Aura_Manipulation:
                case eProperty.Skill_FistWraps:
                    {
                        if (charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }
                        if (type == eObjectType.Leather)
                        {
                            return true;
                        }
                        return false;
                    }
            }

            return false;
        }

        private bool SkillIsValidForWeapon(eProperty property)
        {
            int level = this.Level;
            eRealm realm = (eRealm)this.Realm;
            eObjectType type = (eObjectType)this.Object_Type;
            eCharacterClass charClass = this.charClass;

            switch (property)
            {
                case eProperty.Skill_Hexing:
                case eProperty.Skill_Cursing:
                case eProperty.Skill_Witchcraft:
                    if (property == eProperty.Skill_Witchcraft) return false;
                    return (charClass == eCharacterClass.Warlock && type == eObjectType.Staff);

                case eProperty.Skill_EtherealShriek:
                case eProperty.Skill_PhantasmalWail:
                case eProperty.Skill_SpectralGuard:
                case eProperty.Skill_SpectralForce:
                    if (property == eProperty.Skill_SpectralForce) return false;
                    return (charClass == eCharacterClass.Bainshee && type == eObjectType.Staff);

                case eProperty.Skill_Arboreal:
                    {
                        if (charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Animist)
                        {
                            return false;
                        }
                        if (type == eObjectType.Staff)
                        {
                            return true;
                        }
                        return false;
                    }

                case eProperty.Skill_Matter:
                case eProperty.Skill_Body:
                    {
                        if (charClass != eCharacterClass.Cabalist &&
                            charClass != eCharacterClass.Sorcerer)
                        {
                            return false;
                        }
                        if (type == eObjectType.Staff)
                        {
                            return true;
                        }
                        return false;
                    }

                case eProperty.Skill_Earth:
                case eProperty.Skill_Cold:
                    {
                        if (charClass != eCharacterClass.Theurgist &&
                            charClass != eCharacterClass.Wizard)
                        {
                            return false;
                        }
                        if (type == eObjectType.Staff)
                        {
                            return true;
                        }
                        return false;
                    }

                case eProperty.Skill_Suppression:
                case eProperty.Skill_Darkness:
                    {
                        if (charClass != eCharacterClass.Spiritmaster &&
                            charClass != eCharacterClass.Runemaster &&
                            charClass != eCharacterClass.Bonedancer)
                        {
                            return false;
                        }
                        if (type == eObjectType.Staff)
                        {
                            return true;
                        }
                        return false;
                    }

                case eProperty.Skill_Light:
                case eProperty.Skill_Mana:
                    {
                        if (charClass != eCharacterClass.Enchanter &&
                            charClass != eCharacterClass.Eldritch &&
                            charClass != eCharacterClass.Mentalist)
                        {
                            return false;
                        }
                        if (type == eObjectType.Staff)
                        {
                            return true;
                        }
                        return false;
                    }

                case eProperty.Skill_Mind:
                    if (charClass != eCharacterClass.Sorcerer) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_Spirit:
                    if (charClass != eCharacterClass.Cabalist) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_Wind:
                    if (charClass != eCharacterClass.Theurgist) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_Fire:
                    if (charClass != eCharacterClass.Wizard) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_Death_Servant:
                case eProperty.Skill_DeathSight:
                case eProperty.Skill_Pain_working:
                    if (charClass != eCharacterClass.Necromancer) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_Void_Acolyte:
                case eProperty.Skill_Wraithsight:
                case eProperty.Skill_Tormentshaper:
                    if (charClass != eCharacterClass.Occultist) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;

                case eProperty.Skill_Summoning:
                    if (charClass != eCharacterClass.Spiritmaster) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_Runecarving:
                    if (charClass != eCharacterClass.Runemaster) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_BoneArmy:
                    if (charClass != eCharacterClass.Bonedancer) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;

                case eProperty.Skill_Void:
                    if (charClass != eCharacterClass.Eldritch) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_Enchantments:
                    if (charClass != eCharacterClass.Enchanter) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_Mentalism:
                    if (charClass != eCharacterClass.Mentalist) { return false; }
                    if (type == eObjectType.Staff) { return true; } return false;
                case eProperty.Skill_Creeping:
                case eProperty.Skill_Verdant:
                    {
                        if (charClass != eCharacterClass.Animist &&
                            charClass != eCharacterClass.Valewalker) { return false; }
                        if (type == eObjectType.Staff) { return true; } return false;
                    }

                //healer things
                case eProperty.Skill_Smiting:
                    {
                        if (((type == eObjectType.Shield && this.Type_Damage < 3) || type == eObjectType.CrushingWeapon)
                            && charClass == eCharacterClass.Cleric)
                            return true;
                        break;
                    }
                case eProperty.Skill_Enhancement:
                case eProperty.Skill_Rejuvenation:
                    {
                        if (realm != eRealm.Albion || (charClass != eCharacterClass.Cleric && charClass != eCharacterClass.Friar && charClass != eCharacterClass.Heretic)) { return false; }
                        if ((type == eObjectType.Staff && this.charClass == eCharacterClass.Friar) || (type == eObjectType.Shield && this.Type_Damage < 3) || type == eObjectType.CrushingWeapon)
                            return true;
                        break;
                    }
                case eProperty.Skill_Augmentation:
                case eProperty.Skill_Mending:
                    {
                        if (charClass == eCharacterClass.Valkyrie)
                        {
                            if (type == eObjectType.Shield || type == eObjectType.Sword || type == eObjectType.Spear)
                                return true;
                            return false;
                        }
                        if (realm != eRealm.Midgard || (charClass != eCharacterClass.Healer && charClass != eCharacterClass.Shaman)) { return false; }
                        if ((type == eObjectType.Shield && this.Type_Damage < 2) || type == eObjectType.Hammer)
                        {
                            return true;
                        }
                        break;
                    }
                case eProperty.Skill_Subterranean:
                    {
                        if (realm != eRealm.Midgard || charClass != eCharacterClass.Shaman) { return false; }
                        if ((type == eObjectType.Shield && this.Type_Damage < 2) || type == eObjectType.Hammer)
                        {
                            return true;
                        }
                        break;
                    }
                case eProperty.Skill_Nurture:
                case eProperty.Skill_Nature:
                case eProperty.Skill_Regrowth:
                    {
                        if (realm != eRealm.Hibernia) { return false; }
                        if (type == eObjectType.Blunt || type == eObjectType.Blades || (type == eObjectType.Shield && this.Type_Damage < 2))
                            return true;
                        break;
                    }
                //archery things
                case eProperty.Skill_Archery:
                    if (type == eObjectType.CompositeBow || type == eObjectType.RecurvedBow || type == eObjectType.Longbow)
                        return true;
                    break;
                case eProperty.Skill_Composite:
                    {
                        if (type == eObjectType.CompositeBow)
                            return true;
                        break;
                    }
                case eProperty.Skill_RecurvedBow:
                    {
                        if (type == eObjectType.RecurvedBow)
                            return true;
                        break;
                    }
                case eProperty.Skill_Long_bows:
                    {
                        if (type == eObjectType.Longbow)
                            return true;
                        break;
                    }
                //other specifics
                case eProperty.Skill_Staff:
                    {
                        if (type == eObjectType.Staff && this.charClass == eCharacterClass.Friar)
                            return true;
                        break;
                    }
                case eProperty.Skill_Axe:
                    {
                        if (realm != eRealm.Midgard) { return false; }
                        if (type == eObjectType.Axe || type == eObjectType.LeftAxe)
                            return true;
                        break;
                    }
                case eProperty.Skill_Battlesongs:
                    {
                        if (charClass != eCharacterClass.Skald) { return false; }
                        if (type == eObjectType.Sword || type == eObjectType.Axe || type == eObjectType.Hammer || (type == eObjectType.Shield && this.Type_Damage < 3))
                            return true;
                        break;
                    }
                case eProperty.Skill_BeastCraft:
                    {
                        if (charClass != eCharacterClass.Hunter) { return false; }
                        if (type == eObjectType.Spear)
                            return true;
                        break;
                    }
                case eProperty.Skill_Blades:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden)
                        {
                            return false;
                        }

                        if (type == eObjectType.Blades)
                            return true;
                        break;
                    }
                case eProperty.Skill_Blunt:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Bard &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden)
                        {
                            return false;
                        }

                        if (type == eObjectType.Blunt)
                            return true;
                        break;
                    }
                case eProperty.Skill_Celtic_Dual:
                    {
                        if (charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Blademaster)
                        {
                            return false;
                        }
                        if (type == eObjectType.Piercing || type == eObjectType.Blades || type == eObjectType.Blunt)
                            return true;
                        break;
                    }
                case eProperty.Skill_Celtic_Spear:
                    {
                        if (charClass != eCharacterClass.Hero) { return false; }
                        if (type == eObjectType.CelticSpear)
                            return true;
                        break;
                    }
                case eProperty.Skill_Chants:
                    {
                        return false;
                    }
                case eProperty.Skill_Critical_Strike:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Shadowblade)
                        {
                            return false;
                        }
                        if (type == eObjectType.Piercing || type == eObjectType.SlashingWeapon || type == eObjectType.ThrustWeapon || type == eObjectType.Blades || type == eObjectType.Sword || type == eObjectType.Axe || type == eObjectType.LeftAxe)
                            return true;
                        break;
                    }
                case eProperty.Skill_Cross_Bows:
                    {
                        if (type == eObjectType.Crossbow)
                            return true;
                        break;
                    }
                case eProperty.Skill_Crushing:
                    {
                        if (realm != eRealm.Albion || type == eObjectType.Flexible) { return false; }
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        if (type == eObjectType.CrushingWeapon ||
                            ((type == eObjectType.TwoHandedWeapon || type == eObjectType.PolearmWeapon) && this.Type_Damage == (int)eDamageType.Crush))
                            return true;
                        break;
                    }
                case eProperty.Skill_Dual_Wield:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Berserker)
                        {
                            return false;
                        }

                        if (type == eObjectType.SlashingWeapon || type == eObjectType.ThrustWeapon || type == eObjectType.CrushingWeapon)
                            return true;
                        break;
                    }
                case eProperty.Skill_Envenom:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Shadowblade)
                        {
                            return false;
                        }
                        if (type == eObjectType.SlashingWeapon || type == eObjectType.ThrustWeapon)
                            return true;
                        break;
                    }
                case eProperty.Skill_Flexible_Weapon:
                    {
                        if (charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Heretic) { return false; }
                        if (type == eObjectType.Flexible || type == eObjectType.Shield)
                            return true;
                        break;
                    }
                case eProperty.Skill_Hammer:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior)
                        {
                            return false;
                        }
                        if (type == eObjectType.Hammer)
                            return true;
                        break;
                    }
                case eProperty.Skill_HandToHand:
                    {
                        if (charClass != eCharacterClass.Savage) { return false; }
                        if (type == eObjectType.HandToHand)
                            return true;
                        break;
                    }
                case eProperty.Skill_Instruments:
                    {
                        if (charClass != eCharacterClass.Minstrel) { return false; }
                        if (type == eObjectType.Instrument)
                            return true;
                        break;
                    }
                case eProperty.Skill_Large_Weapon:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero)
                        {
                            return false;
                        }
                        if (type == eObjectType.LargeWeapons)
                            return true;
                        break;
                    }
                case eProperty.Skill_Left_Axe:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Shadowblade)
                        {
                            return false;
                        }
                        if (this.Item_Type == Slot.TWOHAND) return false;
                        if (type == eObjectType.Axe || type == eObjectType.LeftAxe)
                            return true;
                        break;
                    }
                case eProperty.Skill_Music:
                    {
                        if (charClass != eCharacterClass.Bard)
                        {
                            return false;
                        }
                        if (type == eObjectType.Blades || type == eObjectType.Blunt || (type == eObjectType.Shield && this.Type_Damage == 1) || type == eObjectType.Instrument)
                            return true;
                        break;
                    }
                case eProperty.Skill_Nightshade:
                    {
                        if (charClass != eCharacterClass.Nightshade)
                        {
                            return false;
                        }
                        if (type == eObjectType.Blades || type == eObjectType.Piercing || type == eObjectType.Shield)
                            return true;
                        break;
                    }
                case eProperty.Skill_OdinsWill:
                    {
                        if (charClass != eCharacterClass.Valkyrie)
                        {
                            return false;
                        }
                        if (type == eObjectType.Sword || type == eObjectType.Spear || type == eObjectType.Shield)
                            return true;
                        break;
                    }
                case eProperty.Skill_Parry:
                    if (charClass != eCharacterClass.Berserker &&  //midgard
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Blademaster && //hibernia
                            charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Armsman && //albion
                            charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Valkyrie)
                    {
                        return false;
                    }
                    return true;
                case eProperty.Skill_Pathfinding:
                    {
                        if (charClass != eCharacterClass.Ranger)
                        {
                            return false;
                        }
                        if (type == eObjectType.RecurvedBow || type == eObjectType.Piercing || type == eObjectType.Blades)
                            return true;
                        break;
                    }
                case eProperty.Skill_Piercing:
                    {
                        if (charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Vampiir)
                        {
                            return false;
                        }
                        if (type == eObjectType.Piercing)
                            return true;
                        break;
                    }
                case eProperty.Skill_Polearms:
                    {
                        if (charClass != eCharacterClass.Armsman) { return false; }
                        if (type == eObjectType.PolearmWeapon)
                            return true;
                        break;
                    }
                case eProperty.Skill_Savagery:
                    {
                        if (charClass != eCharacterClass.Savage) { return false; }
                        if (type == eObjectType.Sword || type == eObjectType.Axe || type == eObjectType.Hammer || type == eObjectType.HandToHand)
                            return true;
                        break;
                    }
                case eProperty.Skill_Scythe:
                    {
                        if (charClass != eCharacterClass.Valewalker) { return false; }
                        if (type == eObjectType.Scythe)
                            return true;
                        break;
                    }

                case eProperty.Skill_VampiiricEmbrace:
                case eProperty.Skill_ShadowMastery:
                    {
                        if (charClass != eCharacterClass.Vampiir)
                        {
                            return false;
                        }
                        if (type == eObjectType.Piercing)
                        {
                            return true;
                        }
                        break;
                    }
                case eProperty.Skill_Shields:
                    {
                        if (charClass != eCharacterClass.Thane &&  //midgard
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Valkyrie &&
                            charClass != eCharacterClass.Champion && //hibernia
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Armsman && //albion
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Heretic)
                        {
                            return false;
                        }
                        if (type == eObjectType.Shield)
                            return true;
                        break;
                    }
                case eProperty.Skill_ShortBow:
                    {
                        return false;
                    }
                case eProperty.Skill_Slashing:
                    {
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }

                        if (type == eObjectType.Flexible)
                            return false;
                        if (type == eObjectType.SlashingWeapon ||
                            ((type == eObjectType.TwoHandedWeapon || type == eObjectType.PolearmWeapon) && this.Type_Damage == (int)eDamageType.Slash))
                            return true;
                        break;
                    }
                case eProperty.Skill_SoulRending:
                    {
                        if (charClass != eCharacterClass.Reaver) { return false; }
                        if (type == eObjectType.SlashingWeapon || type == eObjectType.CrushingWeapon || type == eObjectType.ThrustWeapon || type == eObjectType.Flexible || type == eObjectType.Shield)
                            return true;
                        break;
                    }
                case eProperty.Skill_Spear:
                    {
                        if (charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Valkyrie) { return false; }
                        if (type == eObjectType.Spear)
                            return true;
                        break;
                    }
                case eProperty.Skill_Stealth:
                    {
                        if (charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }
                        if (type == eObjectType.Longbow || type == eObjectType.RecurvedBow || type == eObjectType.CompositeBow || (realm == eRealm.Albion && type == eObjectType.Shield && this.Type_Damage == 1) || type == eObjectType.Spear || type == eObjectType.Sword || type == eObjectType.Axe || type == eObjectType.LeftAxe || type == eObjectType.SlashingWeapon || type == eObjectType.ThrustWeapon || type == eObjectType.Piercing || type == eObjectType.Blades || (realm == eRealm.Albion && type == eObjectType.Instrument))
                            return true;
                        break;
                    }
                case eProperty.Skill_Stormcalling:
                    {
                        if (charClass != eCharacterClass.Thane) { return false; }
                        if (type == eObjectType.Sword || type == eObjectType.Axe || type == eObjectType.Hammer || type == eObjectType.Shield)
                            return true;
                        break;
                    }
                case eProperty.Skill_Sword:
                    {
                        if (charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Valkyrie)
                        {
                            return false;
                        }
                        if (type == eObjectType.Sword)
                            return true;
                        break;
                    }
                case eProperty.Skill_Thrusting:
                    {
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout)
                        {
                            return false;
                        }
                        if (type == eObjectType.Flexible)
                            return false;
                        if (type == eObjectType.ThrustWeapon ||
                            ((type == eObjectType.TwoHandedWeapon || type == eObjectType.PolearmWeapon) && this.Type_Damage == (int)eDamageType.Thrust))
                            return true;
                        break;
                    }
                case eProperty.Skill_Two_Handed:
                    {
                        if (charClass != eCharacterClass.Armsman &&
                            charClass != eCharacterClass.Paladin)
                        {
                            return false;
                        }
                        if (type == eObjectType.TwoHandedWeapon)
                            return true;
                        break;
                    }
                case eProperty.Skill_Valor:
                    {
                        if (charClass != eCharacterClass.Champion) { return false; }
                        if (type == eObjectType.Blades || type == eObjectType.Piercing || type == eObjectType.Blunt || type == eObjectType.LargeWeapons || type == eObjectType.Shield)
                            return true;
                        break;
                    }
                case eProperty.Skill_Thrown_Weapons:
                    {
                        return false;
                    }
                case eProperty.Skill_Pacification:
                    {
                        if (charClass != eCharacterClass.Healer) { return false; }
                        if (type == eObjectType.Hammer)
                            return true;
                        break;
                    }
                case eProperty.Skill_Dementia:
                    {
                        if (charClass != eCharacterClass.Vampiir)
                        {
                            return false;
                        }
                        if (type == eObjectType.Piercing)
                        {
                            return true;
                        }
                        break;
                    }
                case eProperty.AllArcherySkills:
                    {
                        if (charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Ranger)
                        {
                            return false;
                        }
                        if (type == eObjectType.CompositeBow || type == eObjectType.Longbow || type == eObjectType.RecurvedBow)
                            return true;
                        break;
                    }
                case eProperty.AllDualWieldingSkills:
                    {
                        if (charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Berserker &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Blademaster &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary)
                        {
                            return false;
                        }
                        if (type == eObjectType.Axe || type == eObjectType.Sword || type == eObjectType.Hammer || type == eObjectType.LeftAxe || type == eObjectType.SlashingWeapon || type == eObjectType.CrushingWeapon || type == eObjectType.ThrustWeapon || type == eObjectType.Piercing || type == eObjectType.Blades || type == eObjectType.Blunt)
                            return true;
                        break;
                    }
                case eProperty.AllMagicSkills:
                    {
                        if (charClass != eCharacterClass.Cabalist && //albion
                            charClass != eCharacterClass.Cleric &&
                            charClass != eCharacterClass.Necromancer &&
                            charClass != eCharacterClass.Occultist &&
                            charClass != eCharacterClass.Sorcerer &&
                            charClass != eCharacterClass.Theurgist &&
                            charClass != eCharacterClass.Wizard &&
                            charClass != eCharacterClass.Heretic &&
                            charClass != eCharacterClass.Animist && //hibernia
                            charClass != eCharacterClass.Eldritch &&
                            charClass != eCharacterClass.Enchanter &&
                            charClass != eCharacterClass.Mentalist &&
                            charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Bainshee &&
                            charClass != eCharacterClass.Vampiir &&
                            charClass != eCharacterClass.Bonedancer && //midgard
                            charClass != eCharacterClass.Runemaster &&
                            charClass != eCharacterClass.Spiritmaster &&
                            charClass != eCharacterClass.Warlock &&
                            charClass != eCharacterClass.Valkyrie &&
                            charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }
                        //scouts, armsmen, paladins, mercs, blademasters, heroes, zerks, warriors do not need this.
                        if (type == eObjectType.Longbow || type == eObjectType.CelticSpear || type == eObjectType.PolearmWeapon || type == eObjectType.TwoHandedWeapon || type == eObjectType.Crossbow || (type == eObjectType.Shield && this.Type_Damage > 2))
                            return false;
                        else
                            return true;
                    }
                case eProperty.AllMeleeWeaponSkills:
                    {
                        if (charClass != eCharacterClass.Berserker &&  //midgard
                            charClass != eCharacterClass.Hunter &&
                            charClass != eCharacterClass.Savage &&
                            charClass != eCharacterClass.Shadowblade &&
                            charClass != eCharacterClass.Skald &&
                            charClass != eCharacterClass.Thane &&
                            charClass != eCharacterClass.Warrior &&
                            charClass != eCharacterClass.Valkyrie &&
                            charClass != eCharacterClass.Blademaster && //hibernia
                            charClass != eCharacterClass.Champion &&
                            charClass != eCharacterClass.Hero &&
                            charClass != eCharacterClass.Nightshade &&
                            charClass != eCharacterClass.Ranger &&
                            charClass != eCharacterClass.Valewalker &&
                            charClass != eCharacterClass.Warden &&
                            charClass != eCharacterClass.Vampiir &&
                            charClass != eCharacterClass.Armsman && //albion
                            charClass != eCharacterClass.Friar &&
                            charClass != eCharacterClass.Infiltrator &&
                            charClass != eCharacterClass.Mercenary &&
                            charClass != eCharacterClass.Minstrel &&
                            charClass != eCharacterClass.Paladin &&
                            charClass != eCharacterClass.Reaver &&
                            charClass != eCharacterClass.Scout &&
                            charClass != eCharacterClass.Heretic &&
                            charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }
                        if (type == eObjectType.Staff && realm != eRealm.Albion)
                            return false;
                        else if (type == eObjectType.Staff && this.charClass != eCharacterClass.Friar) // do not add if caster staff
                            return false;
                        else if (type == eObjectType.Longbow || type == eObjectType.CompositeBow || type == eObjectType.RecurvedBow || type == eObjectType.Crossbow || type == eObjectType.Fired || type == eObjectType.Instrument)
                            return false;
                        else
                            return true;
                    }
                case eProperty.Skill_Aura_Manipulation:
                    {
                        if (charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }
                        if (type == eObjectType.FistWraps || type == eObjectType.MaulerStaff)
                            return true;
                        break;
                    }
                case eProperty.Skill_Magnetism:
                    {
                        if (charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }
                        if (type == eObjectType.FistWraps || type == eObjectType.MaulerStaff)
                            return true;
                        break;
                    }
                case eProperty.Skill_MaulerStaff:
                    {
                        if (charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }
                        if (type == eObjectType.MaulerStaff)
                            return true;
                        break;
                    }
                case eProperty.Skill_Power_Strikes:
                    {
                        if (charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }
                        if (type == eObjectType.FistWraps || type == eObjectType.MaulerStaff)
                            return true;
                        break;
                    }
                case eProperty.Skill_FistWraps:
                    {
                        if (charClass != eCharacterClass.MaulerAlb &&
                            charClass != eCharacterClass.MaulerMid &&
                            charClass != eCharacterClass.MaulerHib)
                        {
                            return false;
                        }
                        if (type == eObjectType.FistWraps)
                            return true;
                        break;
                    }
            }
            return false;
        }

        private bool StatIsValidForRealm(eProperty property)
        {
            switch (property)
            {
                case eProperty.Piety:
                case eProperty.PieCapBonus:
                    {
                        if (this.Realm == (int)eRealm.Hibernia)
                            return false;
                        break;
                    }
                case eProperty.Empathy:
                case eProperty.EmpCapBonus:
                    {
                        if (this.Realm == (int)eRealm.Midgard || this.Realm == (int)eRealm.Albion)
                            return false;
                        break;
                    }
                case eProperty.Intelligence:
                case eProperty.IntCapBonus:
                    {
                        if (this.Realm == (int)eRealm.Midgard)
                            return false;
                        break;
                    }
            }
            return true;
        }

        private bool StatIsValidForArmor(eProperty property)
        {
            eRealm realm = (eRealm)this.Realm;
            eObjectType type = (eObjectType)this.Object_Type;

            switch (property)
            {
                case eProperty.Intelligence:
                case eProperty.IntCapBonus:
                    {
                        if (realm == eRealm.Midgard)
                            return false;

                        if (realm == eRealm.Hibernia && this.Level < 20 && type != eObjectType.Reinforced && type != eObjectType.Cloth)
                            return false;

                        if (realm == eRealm.Hibernia && this.Level >= 20 && type != eObjectType.Scale && type != eObjectType.Cloth)
                            return false;

                        if (type != eObjectType.Cloth)
                            return false;

                        break;
                    }
                case eProperty.Acuity:
                case eProperty.AcuCapBonus:
                case eProperty.PowerPool:
                case eProperty.PowerPoolCapBonus:
                    {
                        if (realm == eRealm.Albion && this.Level >= 20 && type == eObjectType.Studded)
                            return false;

                        if (realm == eRealm.Midgard && this.Level >= 10 && type == eObjectType.Leather)
                            return false;

                        if (realm == eRealm.Midgard && this.Level >= 20 && type == eObjectType.Studded)
                            return false;

                        break;
                    }
                case eProperty.Piety:
                case eProperty.PieCapBonus:
                    {
                        if (realm == eRealm.Albion)
                        {
                            if (type == eObjectType.Leather && this.Level >= 10)
                                return false;

                            if (type == eObjectType.Studded && this.Level >= 20)
                                return false;

                            if (type == eObjectType.Chain && this.Level < 10)
                                return false;
                        }
                        else if (realm == eRealm.Midgard)
                        {
                            if (type == eObjectType.Leather && this.Level >= 10)
                                return false;

                            if (type == eObjectType.Studded && this.Level >= 20)
                                return false;

                            if (type == eObjectType.Chain && this.Level < 10)
                                return false;
                        }
                        else if (realm == eRealm.Hibernia)
                        {
                            return false;
                        }
                        break;
                    }
                case eProperty.Charisma:
                case eProperty.ChaCapBonus:
                    {
                        if (realm == eRealm.Albion)
                        {
                            if (type == eObjectType.Leather && this.Level >= 10)
                                return false;

                            if (type == eObjectType.Studded && this.Level >= 20)
                                return false;

                            if (type == eObjectType.Chain && this.Level < 20)
                                return false;
                        }
                        if (realm == eRealm.Midgard)
                        {
                            if (type == eObjectType.Studded && this.Level >= 20)
                                return false;

                            if (type == eObjectType.Chain && this.Level < 20)
                                return false;
                        }
                        else if (realm == eRealm.Hibernia)
                        {
                            if (type == eObjectType.Leather && this.Level >= 15)
                                return false;

                            if (type == eObjectType.Reinforced && this.Level < 15)
                                return false;
                        }
                        break;
                    }
                case eProperty.Empathy:
                case eProperty.EmpCapBonus:
                    {
                        if (realm != eRealm.Hibernia)
                            return false;

                        if (type == eObjectType.Leather && this.Level >= 10)
                            return false;

                        if (type == eObjectType.Reinforced && this.Level >= 20)
                            return false;

                        if (type == eObjectType.Scale && this.Level < 20)
                            return false;

                        break;
                    }
            }
            return true;
        }

        private bool StatIsValidForWeapon(eProperty property)
        {
            eRealm realm = (eRealm)this.Realm;
            eObjectType type = (eObjectType)this.Object_Type;

            switch (type)
            {
                case eObjectType.Staff:
                    {
                        if ((property == eProperty.Piety || property == eProperty.PieCapBonus) && realm == eRealm.Hibernia)
                            return false;
                        else if ((property == eProperty.Piety || property == eProperty.PieCapBonus) && realm == eRealm.Albion && this.charClass != eCharacterClass.Friar)
                            return false; // caster staff
                        else if (property == eProperty.Charisma || property == eProperty.Empathy || property == eProperty.ChaCapBonus || property == eProperty.EmpCapBonus)
                            return false;
                        else if ((property == eProperty.Intelligence || property == eProperty.IntCapBonus || property == eProperty.AcuCapBonus) && this.charClass == eCharacterClass.Friar)
                            return false;
                        break;
                    }

                case eObjectType.Shield:
                    {
                        if ((realm == eRealm.Albion || realm == eRealm.Midgard) && (property == eProperty.Intelligence || property == eProperty.IntCapBonus || property == eProperty.Empathy || property == eProperty.EmpCapBonus))
                            return false;
                        else if (realm == eRealm.Hibernia && (property == eProperty.Piety || property == eProperty.PieCapBonus))
                            return false;
                        else if ((realm == eRealm.Albion || realm == eRealm.Hibernia) && this.Type_Damage > 1 && (property == eProperty.Charisma || property == eProperty.ChaCapBonus))
                            return false;
                        else if (realm == eRealm.Midgard && this.Type_Damage > 2 && (property == eProperty.Charisma || property == eProperty.ChaCapBonus))
                            return false;
                        else if (this.Type_Damage > 2 && property == eProperty.MaxMana)
                            return false;

                        break;
                    }
                case eObjectType.Blades:
                case eObjectType.Blunt:
                    {
                        if (property == eProperty.Piety || property == eProperty.PieCapBonus)
                            return false;
                        break;
                    }
                case eObjectType.LargeWeapons:
                case eObjectType.Piercing:
                case eObjectType.Scythe:
                    {
                        if (property == eProperty.Piety || property == eProperty.Empathy || property == eProperty.Charisma)
                            return false;
                        break;
                    }
                case eObjectType.CrushingWeapon:
                    {
                        if (property == eProperty.Intelligence || property == eProperty.IntCapBonus || property == eProperty.Empathy || property == eProperty.EmpCapBonus || property == eProperty.Charisma || property == eProperty.ChaCapBonus)
                            return false;
                        break;
                    }
                case eObjectType.SlashingWeapon:
                case eObjectType.ThrustWeapon:
                case eObjectType.Hammer:
                case eObjectType.Sword:
                case eObjectType.Axe:
                    {
                        if (property == eProperty.Intelligence || property == eProperty.IntCapBonus || property == eProperty.Empathy || property == eProperty.EmpCapBonus || property == eProperty.AcuCapBonus || property == eProperty.Acuity)
                            return false;
                        break;
                    }
                case eObjectType.TwoHandedWeapon:
                case eObjectType.Flexible:
                    {
                        if (property == eProperty.Intelligence || property == eProperty.IntCapBonus || property == eProperty.Empathy || property == eProperty.EmpCapBonus || property == eProperty.Charisma || property == eProperty.ChaCapBonus)
                            return false;
                        break;
                    }
                case eObjectType.RecurvedBow:
                case eObjectType.CompositeBow:
                case eObjectType.Longbow:
                case eObjectType.Crossbow:
                case eObjectType.Fired:
                    {
                        if (property == eProperty.Intelligence || property == eProperty.IntCapBonus || property == eProperty.Empathy || property == eProperty.EmpCapBonus || property == eProperty.Charisma || property == eProperty.ChaCapBonus ||
                            property == eProperty.MaxMana || property == eProperty.PowerPool || property == eProperty.PowerPoolCapBonus || property == eProperty.AcuCapBonus || property == eProperty.Acuity || property == eProperty.Piety || property == eProperty.PieCapBonus)
                            return false;
                        break;
                    }
                case eObjectType.Spear:
                case eObjectType.CelticSpear:
                case eObjectType.LeftAxe:
                case eObjectType.PolearmWeapon:
                case eObjectType.HandToHand:
                case eObjectType.FistWraps: //Maulers
                case eObjectType.MaulerStaff: //Maulers
                    {
                        if (property == eProperty.Intelligence || property == eProperty.IntCapBonus || property == eProperty.Empathy || property == eProperty.EmpCapBonus || property == eProperty.Charisma || property == eProperty.ChaCapBonus ||
                            property == eProperty.MaxMana || property == eProperty.PowerPool || property == eProperty.PowerPoolCapBonus || property == eProperty.AcuCapBonus || property == eProperty.Acuity || property == eProperty.Piety || property == eProperty.PieCapBonus)
                            return false;
                        break;
                    }
                case eObjectType.Instrument:
                    {
                        if (property == eProperty.Intelligence || property == eProperty.IntCapBonus || property == eProperty.Empathy || property == eProperty.EmpCapBonus || property == eProperty.Piety || property == eProperty.PieCapBonus)
                            return false;
                        break;
                    }
            }
            return true;
        }

        /// <summary>
        /// Safely writes up to 11 slots sequentially.
        /// </summary>
        private void WriteBonus(eProperty property, int amount)
        {
            if (property == eProperty.AllFocusLevels)
                amount = Math.Min(50, amount);

            if (this.Bonus1Type == 0) { this.Bonus1 = amount; this.Bonus1Type = (int)property; }
            else if (this.Bonus2Type == 0) { this.Bonus2 = amount; this.Bonus2Type = (int)property; }
            else if (this.Bonus3Type == 0) { this.Bonus3 = amount; this.Bonus3Type = (int)property; }
            else if (this.Bonus4Type == 0) { this.Bonus4 = amount; this.Bonus4Type = (int)property; }
            else if (this.Bonus5Type == 0) { this.Bonus5 = amount; this.Bonus5Type = (int)property; }
            else if (this.Bonus6Type == 0) { this.Bonus6 = amount; this.Bonus6Type = (int)property; }
            else if (this.Bonus7Type == 0) { this.Bonus7 = amount; this.Bonus7Type = (int)property; }
            else if (this.Bonus8Type == 0) { this.Bonus8 = amount; this.Bonus8Type = (int)property; }
            else if (this.Bonus9Type == 0) { this.Bonus9 = amount; this.Bonus9Type = (int)property; }
            else if (this.Bonus10Type == 0) { this.Bonus10 = amount; this.Bonus10Type = (int)property; }
            else if (this.ExtraBonusType == 0) { this.ExtraBonus = amount; this.ExtraBonusType = (int)property; }

            if (property == eProperty.AllFocusLevels && this.Bonus1Type == (int)property && !this.Name.StartsWith("Focus "))
                this.Name = $"Focus {this.Name}";
        }

        /// <summary>
        /// Mathematically calculates a valid bonus amount using the DAoC item utility scale.
        /// </summary>
        private int GetDynamicBonusAmount(ePropertyPool pool, eProperty prop, int level)
        {
            int lvl = Math.Min(50, Math.Max(1, level));

            double weight = ItemUtilityCalculator.GetSingleUtility((int)prop, 1);
            if (weight <= 0) return 1;

            if (prop == eProperty.AllFocusLevels)
                return Math.Min(50, level);

            // Stats & HP/Power
            if (weight <= 0.25) // HP, ExtraHP
            {
                int max = (int)Math.Ceiling(lvl * 4.0 / 4.0); // Classic HP scaling (~50 at 50)
                max = Math.Max(10, max);
                return Util.Random((int)Math.Ceiling(max / 2.0), max);
            }
            if (prop == eProperty.MaxMana || prop == eProperty.PowerPool)
            {
                int max = (int)Math.Ceiling((lvl / 2.0 + 1) / 4.0); // Classic Mana scaling (~6-7 at 50)
                return Math.Max(1, Util.Random((int)Math.Ceiling(max / 2.0), max));
            }
            if (weight <= 0.67) // Str, Dex, Acuity, etc
            {
                int max = (int)Math.Ceiling(lvl / 3.0); // Classic Stat scaling (~16-17 at 50)
                return Math.Max(1, Util.Random((int)Math.Ceiling(max / 2.0), max));
            }
            if (weight <= 1.0) // Speed, AF, MissHit
            {
                return Util.Random(1, 10);
            }
            if (weight <= 2.0)
            {
                if (prop.ToString().StartsWith("Resist_"))
                {
                    int max = (int)Math.Ceiling((lvl / 2.0 + 1) / 4.0); // Classic Resist scaling (~6-7 at 50)
                    return Math.Max(1, Util.Random((int)Math.Ceiling(max / 2.0), max));
                }
                return Util.Random(1, 5); // TOA multipliers
            }
            if (weight <= 4.0) // Caps
            {
                if (prop == eProperty.MaxHealthCapBonus) return Util.Random(5, 25);
                if (prop == eProperty.PowerPoolCapBonus) return Util.Random(1, 10);
                return Util.Random(1, 6);
            }

            // Skills/Mythicals
            int maxSkill = Util.Random(1, 4);
            if (prop == eProperty.AllSkills || prop == eProperty.AllMagicSkills || prop == eProperty.AllDualWieldingSkills || prop == eProperty.AllMeleeWeaponSkills || prop == eProperty.AllArcherySkills)
                maxSkill = (int)Math.Ceiling(maxSkill / 2.0);
            return Math.Max(1, Util.Random((int)Math.Ceiling(maxSkill / 2.0), maxSkill));
        }
        #endregion

        #region Utility Scaling & Assembly Loop

        /// <summary>
        /// Property to define where the item dropped. Defaults to 0 (Classic Overworld).
        /// </summary>
        public int DropRegionID { get; set; } = 0;

        /// <summary>
        /// Property to define the BodyType of the mob that dropped the item. Defaults to 0.
        /// </summary>
        public int DropMobBodyType { get; set; } = 0;

        /// <summary>
        /// Returns specific prioritized properties based on the monster's bodytype.
        /// </summary>
        private List<eProperty> GetPrioritizedPropertiesForBodyType(int bodyType, ePropertyPool pool)
        {
            List<eProperty> props = new List<eProperty>();

            if (pool == ePropertyPool.ClassicResists)
            {
                switch (bodyType)
                {
                    case 2: // Demon
                        props.Add(eProperty.Resist_Heat); props.Add(eProperty.Resist_Spirit); break;
                    case 3: // Dragon
                        props.Add(eProperty.Resist_Heat); props.Add(eProperty.Resist_Slash); props.Add(eProperty.Resist_Crush); break;
                    case 4: // Elemental
                        props.Add(eProperty.Resist_Heat); props.Add(eProperty.Resist_Cold); props.Add(eProperty.Resist_Matter); props.Add(eProperty.Resist_Energy); break;
                    case 5: // Giant
                        props.Add(eProperty.Resist_Crush); props.Add(eProperty.Resist_Slash); break;
                    case 7: // Insect
                        props.Add(eProperty.Resist_Thrust); props.Add(eProperty.Resist_Matter); break;
                    case 8: // Magical
                        props.Add(eProperty.Resist_Energy); props.Add(eProperty.Resist_Spirit); props.Add(eProperty.Resist_Natural); break;
                    case 9: // Reptile
                        props.Add(eProperty.Resist_Slash); props.Add(eProperty.Resist_Matter); break;
                    case 10: // Plant
                        props.Add(eProperty.Resist_Crush); props.Add(eProperty.Resist_Matter); break;
                    case 11: // Undead
                        props.Add(eProperty.Resist_Cold); props.Add(eProperty.Resist_Spirit); props.Add(eProperty.Resist_Matter); break;
                }
            }
            else if (pool == ePropertyPool.ClassicStats)
            {
                switch (bodyType)
                {
                    case 2: // Demon
                        props.Add(eProperty.Intelligence); props.Add(eProperty.Piety); props.Add(eProperty.MaxMana); break;
                    case 3: // Dragon
                        props.Add(eProperty.Strength); props.Add(eProperty.Constitution); props.Add(eProperty.MaxHealth); break;
                    case 4: // Elemental
                        props.Add(eProperty.Intelligence); props.Add(eProperty.MaxMana); break;
                    case 5: // Giant
                        props.Add(eProperty.Strength); props.Add(eProperty.Constitution); props.Add(eProperty.MaxHealth); break;
                    case 7: // Insect
                        props.Add(eProperty.Dexterity); props.Add(eProperty.Quickness); break;
                    case 8: // Magical
                        props.Add(eProperty.Intelligence); props.Add(eProperty.Acuity); props.Add(eProperty.MaxMana); break;
                    case 9: // Reptile
                        props.Add(eProperty.Constitution); props.Add(eProperty.Strength); props.Add(eProperty.Quickness); break;
                    case 10: // Plant
                        props.Add(eProperty.Constitution); props.Add(eProperty.MaxHealth); break;
                    case 11: // Undead
                        props.Add(eProperty.Constitution); props.Add(eProperty.Strength); props.Add(eProperty.Dexterity); break;
                }
            }

            return props;
        }

        private bool HasSkillCheck()
        {
            return IsSkillProperty((eProperty)Bonus1Type) || IsSkillProperty((eProperty)Bonus2Type) ||
                   IsSkillProperty((eProperty)Bonus3Type) || IsSkillProperty((eProperty)Bonus4Type) ||
                   IsSkillProperty((eProperty)Bonus5Type) || IsSkillProperty((eProperty)Bonus6Type) ||
                   IsSkillProperty((eProperty)Bonus7Type) || IsSkillProperty((eProperty)Bonus8Type) ||
                   IsSkillProperty((eProperty)Bonus9Type) || IsSkillProperty((eProperty)Bonus10Type) ||
                   IsSkillProperty((eProperty)ExtraBonusType);
        }

        private bool IsSkillProperty(eProperty prop)
        {
            int p = (int)prop;
            return (p >= 20 && p <= 58) || (p >= 60 && p <= 70) || (p >= 72 && p <= 115) || (p >= 163 && p <= 165) || (p >= 270 && p <= 309) || p  == 167 || p == 168 || p == 213;
        }

        protected void CapUtility(int mobLevel, int utilityMinimum)
        {
            int cap = 0;
            if (utilityMinimum < 1) utilityMinimum = 1;

            eRegionCategory regionCat = RegionMapper.GetCategoryFromRegionID(this.DropRegionID);

            cap = mobLevel - 5;

            if (mobLevel > 70)
                cap = mobLevel + (Util.Random(1, 5));

            if (mobLevel < 65)
                cap -= (Util.Random(1, 5));

            if (mobLevel > 70 && cap < 60)
                cap = mobLevel - 10;

            if (cap > 80) cap = 80;

            //randomize cap to be 80-105% of normal value
            double random = (80 + Util.Random(25)) / 100.0;
            cap = (int)Math.Floor(cap * random);

            // Apply regional Utility Boost scaling
            if (regionCat == eRegionCategory.ClassicDungeons || regionCat == eRegionCategory.AtlantisDungeons || regionCat == eRegionCategory.DeepCatacombs)
            {
                utilityMinimum = (int)(utilityMinimum * 1.25);
                cap = (int)(cap * 1.25);
            }
            else if (regionCat == eRegionCategory.MythicalZones)
            {
                utilityMinimum = (int)(utilityMinimum * 1.15);
                cap = (int)(cap * 1.15);
            }

            if (cap < 15)
                cap = 15; //all items can gen with up to 15 uti

            if (this.ProcSpellID != 0 || this.ProcSpellID1 != 0)
                cap = (int)Math.Floor(cap * .7); //proc items generate with lower utility

            //Console.WriteLine($"Cap: {cap} floor {utilityMinimum} startUti: {startUti}");
            //bring uti up to floor first
            if (GetTotalUtility() < utilityMinimum)
            {
                int worstline = 1;
                int numAttempts = 0;
                int utiScaleAttempts = 25;
                while (GetTotalUtility() < utilityMinimum && numAttempts < utiScaleAttempts)
                {
                    //find highest utility line on the item
                    worstline = GetLowestUtilitySingleLine();
                    //Console.WriteLine($"TotalUti: {GetTotalUtility()} worstline {worstline} ");
                    numAttempts++;

                    //lower the value of it by
                    //1-5% for resist
                    //1-15 for stat
                    //1-3 for skill
                    switch (worstline)
                    {
                        case 1:
                            Bonus1 = IncreaseSingleLineUtility(Bonus1Type, Bonus1);
                            break;
                        case 2:
                            Bonus2 = IncreaseSingleLineUtility(Bonus2Type, Bonus2);
                            break;
                        case 3:
                            Bonus3 = IncreaseSingleLineUtility(Bonus3Type, Bonus3);
                            break;
                        case 4:
                            Bonus4 = IncreaseSingleLineUtility(Bonus4Type, Bonus4);
                            break;
                        case 5:
                            Bonus5 = IncreaseSingleLineUtility(Bonus5Type, Bonus5);
                            break;
                        case 6:
                            Bonus6 = IncreaseSingleLineUtility(Bonus6Type, Bonus6);
                            break;
                        case 7:
                            Bonus7 = IncreaseSingleLineUtility(Bonus7Type, Bonus7);
                            break;
                        case 8:
                            Bonus8 = IncreaseSingleLineUtility(Bonus8Type, Bonus8);
                            break;
                        case 9:
                            Bonus9 = IncreaseSingleLineUtility(Bonus9Type, Bonus9);
                            break;
                        case 10:
                            Bonus10 = IncreaseSingleLineUtility(Bonus10Type, Bonus10);
                            break;
                        case 11:
                            ExtraBonus = IncreaseSingleLineUtility(ExtraBonusType, ExtraBonus);
                            break;
                    }
                    //then recalculate
                }
            }

            //then cap it down to cieling
            if (GetTotalUtility() > cap)
            {
                int bestline = 1;
                int maxReduceAttempts = 100;
                int reduceAttempts = 0;

                while (GetTotalUtility() > cap && reduceAttempts < maxReduceAttempts)
                {
                    //find highest utility line on the item
                    bestline = GetHighestUtilitySingleLine();
                    //Console.WriteLine($"TotalUti: {GetTotalUtility()} bestline {bestline} ");

                    //lower the value of it by
                    //1-5% for resist
                    //1-15 for stat
                    //1-3 for skill
                    switch (bestline)
                    {
                        case 1:
                            Bonus1 = ReduceSingleLineUtility(Bonus1Type, Bonus1);
                            break;
                        case 2:
                            Bonus2 = ReduceSingleLineUtility(Bonus2Type, Bonus2);
                            break;
                        case 3:
                            Bonus3 = ReduceSingleLineUtility(Bonus3Type, Bonus3);
                            break;
                        case 4:
                            Bonus4 = ReduceSingleLineUtility(Bonus4Type, Bonus4);
                            break;
                        case 5:
                            Bonus5 = ReduceSingleLineUtility(Bonus5Type, Bonus5);
                            break;
                        case 6:
                            Bonus6 = ReduceSingleLineUtility(Bonus6Type, Bonus6);
                            break;
                        case 7:
                            Bonus7 = ReduceSingleLineUtility(Bonus7Type, Bonus7);
                            break;
                        case 8:
                            Bonus8 = ReduceSingleLineUtility(Bonus8Type, Bonus8);
                            break;
                        case 9:
                            Bonus9 = ReduceSingleLineUtility(Bonus9Type, Bonus9);
                            break;
                        case 10:
                            Bonus10 = ReduceSingleLineUtility(Bonus10Type, Bonus10);
                            break;
                        case 11:
                            ExtraBonus = ReduceSingleLineUtility(ExtraBonusType, ExtraBonus);
                            break;
                    }
                    //then recalculate
                }
            }

            //Console.WriteLine($"Capped Uti: {GetTotalUtility()}");
            //write name of item based off of capped lines
            int utiLine = GetHighestUtilitySingleLine();
            eProperty bonus = GetPropertyFromBonusLine(utiLine);
            //Console.WriteLine($"HighUti: {utiLine} bonus: {bonus}");
            WriteMagicalName(bonus);
            //Console.WriteLine($"Item name: {Name}");
        }

        public int GetHighestUtilitySingleLine()
        {
            double highestUti = GetSingleUtility(Bonus1Type, Bonus1);
            int highestLine = highestUti > 0 ? 1 : 0; //if line1 had a bonus, set it as highest line, otherwise default to 0

            if (GetSingleUtility(Bonus2Type, Bonus2) > highestUti)
            {
                highestUti = GetSingleUtility(Bonus2Type, Bonus2);
                highestLine = 2;
            }

            if (GetSingleUtility(Bonus3Type, Bonus3) > highestUti)
            {
                highestUti = GetSingleUtility(Bonus3Type, Bonus3);
                highestLine = 3;
            }

            if (GetSingleUtility(Bonus4Type, Bonus4) > highestUti)
            {
                highestUti = GetSingleUtility(Bonus4Type, Bonus4);
                highestLine = 4;
            }

            if (GetSingleUtility(Bonus5Type, Bonus5) > highestUti)
            {
                highestUti = GetSingleUtility(Bonus5Type, Bonus5);
                highestLine = 5;
            }

            if (GetSingleUtility(Bonus6Type, Bonus6) > highestUti)
            {
                highestUti = GetSingleUtility(Bonus6Type, Bonus6);
                highestLine = 6;
            }

            if (GetSingleUtility(Bonus7Type, Bonus7) > highestUti)
            {
                highestUti = GetSingleUtility(Bonus7Type, Bonus7);
                highestLine = 7;
            }

            if (GetSingleUtility(Bonus8Type, Bonus8) > highestUti)
            {
                highestUti = GetSingleUtility(Bonus8Type, Bonus8);
                highestLine = 8;
            }

            if (GetSingleUtility(Bonus9Type, Bonus9) > highestUti)
            {
                highestUti = GetSingleUtility(Bonus9Type, Bonus9);
                highestLine = 9;
            }

            if (GetSingleUtility(Bonus10Type, Bonus10) > highestUti)
            {
                highestUti = GetSingleUtility(Bonus10Type, Bonus10);
                highestLine = 10;
            }

            if (GetSingleUtility(ExtraBonusType, ExtraBonus) > highestUti)
            {
                highestLine = 11;
            }

            return highestLine;
        }

        public int GetLowestUtilitySingleLine()
        {
            double lowestUti = GetSingleUtility(Bonus1Type, Bonus1);
            int lowestLine = lowestUti > 0 ? 1 : 0; //if line1 had a bonus, set it as highest line, otherwise default to 0

            if (GetSingleUtility(Bonus2Type, Bonus2) < lowestUti && IsValidUpscaleType(Bonus2Type))
            {
                lowestUti = GetSingleUtility(Bonus2Type, Bonus2);
                lowestLine = 2;
            }

            if (GetSingleUtility(Bonus3Type, Bonus3) < lowestUti && IsValidUpscaleType(Bonus3Type))
            {
                lowestUti = GetSingleUtility(Bonus3Type, Bonus3);
                lowestLine = 3;
            }

            if (GetSingleUtility(Bonus4Type, Bonus4) < lowestUti && IsValidUpscaleType(Bonus4Type))
            {
                lowestUti = GetSingleUtility(Bonus4Type, Bonus4);
                lowestLine = 4;
            }

            if (GetSingleUtility(Bonus5Type, Bonus5) < lowestUti && IsValidUpscaleType(Bonus5Type))
            {
                lowestUti = GetSingleUtility(Bonus5Type, Bonus5);
                lowestLine = 5;
            }

            if (GetSingleUtility(Bonus6Type, Bonus6) < lowestUti && IsValidUpscaleType(Bonus6Type))
            {
                lowestUti = GetSingleUtility(Bonus6Type, Bonus6);
                lowestLine = 6;
            }

            if (GetSingleUtility(Bonus7Type, Bonus7) < lowestUti && IsValidUpscaleType(Bonus7Type))
            {
                lowestUti = GetSingleUtility(Bonus7Type, Bonus7);
                lowestLine = 7;
            }

            if (GetSingleUtility(Bonus8Type, Bonus8) < lowestUti && IsValidUpscaleType(Bonus8Type))
            {
                lowestUti = GetSingleUtility(Bonus8Type, Bonus8);
                lowestLine = 8;
            }

            if (GetSingleUtility(Bonus9Type, Bonus9) < lowestUti && IsValidUpscaleType(Bonus9Type))
            {
                lowestUti = GetSingleUtility(Bonus9Type, Bonus9);
                lowestLine = 9;
            }

            if (GetSingleUtility(Bonus10Type, Bonus10) < lowestUti && IsValidUpscaleType(Bonus10Type))
            {
                lowestUti = GetSingleUtility(Bonus10Type, Bonus10);
                lowestLine = 10;
            }

            if (GetSingleUtility(ExtraBonusType, ExtraBonus) < lowestUti && IsValidUpscaleType(ExtraBonusType))
            {
                lowestLine = 11;
            }

            return lowestLine;
        }

        private bool IsValidUpscaleType(int BonusType)
        {
            return BonusType != 0
                   && BonusType != 163
                   && BonusType != 164
                   && BonusType != 167
                   && BonusType != 168
                   && BonusType != 213;
        }

        /// <summary>
        /// Decreases utility of a single line, automatically adjusting based on the property's cost from ItemUtilityCalculator.
        /// </summary>
        private int ReduceSingleLineUtility(int BonusType, int Bonus)
        {
            if (BonusType == 0 || Bonus == 0) return 0;

            if (BonusType == (int)eProperty.AllMagicSkills || BonusType == (int)eProperty.AllMeleeWeaponSkills ||
                BonusType == (int)eProperty.AllDualWieldingSkills || BonusType == (int)eProperty.AllArcherySkills ||
                BonusType == (int)eProperty.AllSkills)
            {
                return 0;
            }

            double weight = ItemUtilityCalculator.GetSingleUtility(BonusType, 1);
            if (weight <= 0) return Bonus;

            int reduction = 1;
            if (weight <= 0.3) reduction = Util.Random(1, Math.Max(1, Math.Min(Bonus, 15)));      // MaxHP
            else if (weight <= 0.7) reduction = Util.Random(1, Math.Max(1, Math.Min(Bonus, 6)));  // Base Stats
            else if (weight <= 1.5) reduction = Util.Random(1, Math.Max(1, Math.Min(Bonus, 3)));  // AF/Speed
            else if (weight <= 2.5) reduction = Util.Random(1, Math.Max(1, Math.Min(Bonus, 2)));  // Resists / TOA
            else reduction = 1;                                                                   // Skills/Caps

            int newBonus = Bonus - reduction;

            return Math.Max(0, newBonus);
        }

        /// <summary>
        /// Increases utility of a single line using the mathematical weight from ItemUtilityCalculator.
        /// </summary>
        private int IncreaseSingleLineUtility(int BonusType, int Bonus)
        {
            if (BonusType == 0 || Bonus == 0) return 0;
            if (!IsPropertyAllowed((eProperty)BonusType)) return Bonus;

            if (BonusType == (int)eProperty.AllMagicSkills || BonusType == (int)eProperty.AllMeleeWeaponSkills ||
                BonusType == (int)eProperty.AllDualWieldingSkills || BonusType == (int)eProperty.AllArcherySkills ||
                BonusType == (int)eProperty.AllSkills)
            {
                return Bonus;
            }

            double weight = ItemUtilityCalculator.GetSingleUtility(BonusType, 1);
            if (weight <= 0) return Bonus;

            int increase = 1;
            if (weight <= 0.3) increase = Util.Random(1, Math.Max(1, Math.Min(Bonus, 15)));
            else if (weight <= 0.7) increase = Util.Random(1, Math.Max(1, Math.Min(Bonus, 6)));
            else if (weight <= 1.5) increase = Util.Random(1, Math.Max(1, Math.Min(Bonus, 3)));
            else if (weight <= 2.5) increase = Util.Random(1, Math.Max(1, Math.Min(Bonus, 2)));
            else increase = 1;

            return Bonus + increase;
        }

        public double GetTotalUtility()
        {
            return ItemUtilityCalculator.GetTotalUtility(this);
        }

        public static double GetSingleUtility(int bonusType, int bonusValue)
        {
            return ItemUtilityCalculator.GetSingleUtility((eProperty)bonusType, bonusValue);
        }

        #endregion

        #region generate item type
        private static eObjectType GenerateObjectType(eRealm realm, eCharacterClass charClass, byte level)
        {
            eGenerateType type = GetObjectTypeByWeight(level);

            switch ((eRealm)realm)
            {
                case eRealm.Albion:
                    {
                        int maxArmor = AlbionArmor.Length - 1;
                        int maxWeapon = AlbionWeapons.Length - 1;

                        if (level < 15)
                            maxArmor--; // remove plate

                        if (level < 5)
                        {
                            maxArmor--; // remove chain
                            maxWeapon = 4; // remove all but base weapons and shield
                        }

                        switch (type)
                        {
                            case eGenerateType.Armor: return GetAlbionArmorType(charClass, level);//AlbionArmor[Util.Random(0, maxArmor)];
                            case eGenerateType.Weapon: return GetAlbionWeapon(charClass);//AlbionWeapons[Util.Random(0, maxWeapon)];
                            case eGenerateType.Magical: return eObjectType.Magical;
                        }
                        break;
                    }
                case eRealm.Midgard:
                    {
                        int maxArmor = MidgardArmor.Length - 1;
                        int maxWeapon = MidgardWeapons.Length - 1;

                        if (level < 10)
                            maxArmor--; // remove chain

                        if (level < 5)
                        {
                            maxWeapon = 4; // remove all but base weapons and shield
                        }

                        switch (type)
                        {
                            case eGenerateType.Armor: return GetMidgardArmorType(charClass, level); //MidgardArmor[Util.Random(0, maxArmor)];
                            case eGenerateType.Weapon: return GetMidgardWeapon(charClass); //MidgardWeapons[Util.Random(0, maxWeapon)];
                            case eGenerateType.Magical: return eObjectType.Magical;
                        }
                        break;
                    }
                case eRealm.Hibernia:
                    {
                        int maxArmor = HiberniaArmor.Length - 1;
                        int maxWeapon = HiberniaWeapons.Length - 1;

                        if (level < 15)
                            maxArmor--; // remove scale

                        if (level < 5)
                        {
                            maxWeapon = 4; // remove all but base weapons and shield
                        }

                        switch (type)
                        {
                            case eGenerateType.Armor: return GetHiberniaArmorType(charClass, level);//HiberniaArmor[Util.Random(0, maxArmor)];
                            case eGenerateType.Weapon: return GetHiberniaWeapon(charClass);//HiberniaWeapons[Util.Random(0, maxWeapon)];
                            case eGenerateType.Magical: return eObjectType.Magical;
                        }
                        break;
                    }
            }
            return eObjectType.GenericItem;
        }

        private static eGenerateType GetObjectTypeByWeight(byte level)
        {
            List<eGenerateType> genTypes = new List<eGenerateType>();

            //weighted so that early levels get many more weapons/armor
            if (level < 5)
            {
                if (Util.Chance(45))
                    return eGenerateType.Weapon;
                //else if (Util.Chance(15))
                //  return eGenerateType.Magical;
                else return eGenerateType.Armor;
            }
            else if (level < 10)
            {
                if (Util.Chance(ArmorWeight)) { genTypes.Add(eGenerateType.Armor); }
                //if (Util.Chance(ROG_MAGICAL_CHANCE)) { genTypes.Add(eGenerateType.Magical); }
                if (Util.Chance(WeaponWeight)) { genTypes.Add(eGenerateType.Weapon); }
            }
            else
            {
                if (Util.Chance(ArmorWeight + Util.Random(ArmorWeight))) { genTypes.Add(eGenerateType.Armor); }
                if (Util.Chance(JewelryWeight)) { genTypes.Add(eGenerateType.Magical); }
                if (Util.Chance(WeaponWeight + Util.Random(WeaponWeight)/2) ) { genTypes.Add(eGenerateType.Weapon); }
            }

            //if none of the object types were added, default to armor
            if (genTypes.Count < 1)
            {
                if (Util.Chance(50))
                    genTypes.Add(eGenerateType.Armor);
                else
                    genTypes.Add(eGenerateType.Weapon);
            }

            return genTypes[Util.Random(genTypes.Count - 1)];
        }

        public static eObjectType GetAlbionWeapon(eCharacterClass charClass)
        {
            List<eObjectType> weaponTypes = new List<eObjectType>();
            /*
			 * Albion Weapons
			eObjectType.ThrustWeapon,
			eObjectType.CrushingWeapon,
			eObjectType.SlashingWeapon, 
			eObjectType.Shield,
			eObjectType.Staff,//
			eObjectType.TwoHandedWeapon,
			eObjectType.Longbow,//
			eObjectType.Flexible,//
			eObjectType.PolearmWeapon,
			eObjectType.FistWraps, //Maulers//
			eObjectType.MaulerStaff,//Maulers//
			eObjectType.Instrument,//
			eObjectType.Crossbow,
			*/
            switch (charClass)
            {
                //staff classes
                case eCharacterClass.Cabalist:
                case eCharacterClass.Necromancer:
                case eCharacterClass.Occultist:
                case eCharacterClass.Sorcerer:
                case eCharacterClass.Theurgist:
                case eCharacterClass.Wizard:
                case eCharacterClass.Acolyte:
                case eCharacterClass.Disciple:
                case eCharacterClass.Elementalist:
                    weaponTypes.Add(eObjectType.Staff);
                    break;
                case eCharacterClass.Friar:
                    weaponTypes.Add(eObjectType.Staff);
                    weaponTypes.Add(eObjectType.Staff);
                    weaponTypes.Add(eObjectType.Staff);
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Armsman:
                    weaponTypes.Add(eObjectType.PolearmWeapon);
                    weaponTypes.Add(eObjectType.PolearmWeapon);
                    weaponTypes.Add(eObjectType.PolearmWeapon);
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.TwoHandedWeapon);
                    weaponTypes.Add(eObjectType.TwoHandedWeapon);
                    weaponTypes.Add(eObjectType.Crossbow);
                    weaponTypes.Add(eObjectType.Shield);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Paladin:
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.TwoHandedWeapon);
                    weaponTypes.Add(eObjectType.TwoHandedWeapon);
                    weaponTypes.Add(eObjectType.Shield);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Reaver:
                    weaponTypes.Add(eObjectType.Flexible);
                    weaponTypes.Add(eObjectType.Flexible);
                    weaponTypes.Add(eObjectType.Flexible);
                    weaponTypes.Add(eObjectType.Flexible);
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Minstrel:
                    weaponTypes.Add(eObjectType.Instrument);
                    weaponTypes.Add(eObjectType.Instrument);
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Infiltrator:
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.Crossbow);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Scout:
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.Longbow);
                    weaponTypes.Add(eObjectType.Longbow);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Mercenary:
                    weaponTypes.Add(eObjectType.Fired); //shortbow
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Cleric:
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.Staff);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Heretic:
                    weaponTypes.Add(eObjectType.Flexible);
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.MaulerAlb:
                    weaponTypes.Add(eObjectType.FistWraps);
                    weaponTypes.Add(eObjectType.MaulerStaff);
                    break;
                case eCharacterClass.AlbionRogue:
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.Piercing);
                    break;
                case eCharacterClass.Fighter:
                    weaponTypes.Add(eObjectType.SlashingWeapon);
                    weaponTypes.Add(eObjectType.CrushingWeapon);
                    weaponTypes.Add(eObjectType.ThrustWeapon);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                default:
                    return eObjectType.Staff;
            }

            //this list nonsense is kind of weird but we need to duplicate the 
            //items in the list to avoid apparent mid-number bias for random number gen

            //clone existing list
            List<eObjectType> outputList = new List<eObjectType>(weaponTypes);

            //add duplicate values
            foreach (eObjectType type in weaponTypes)
            {
                outputList.Add(type);
            }

            //get our random value from the list
            int randomGrab = Util.Random(0, outputList.Count - 1);

            //return a random type from our list of valid weapons
            return outputList[randomGrab];

        }

        public static eObjectType GetAlbionArmorType(eCharacterClass charClass, byte level)
        {

            switch (charClass)
            {
                //staff classes
                case eCharacterClass.Cabalist:
                case eCharacterClass.Necromancer:
                case eCharacterClass.Occultist:
                case eCharacterClass.Sorcerer:
                case eCharacterClass.Theurgist:
                case eCharacterClass.Wizard:
                case eCharacterClass.Heretic:
                case eCharacterClass.Acolyte:
                case eCharacterClass.Disciple:
                case eCharacterClass.Elementalist:
                    return eObjectType.Cloth;

                case eCharacterClass.Friar:
                case eCharacterClass.Infiltrator:
                case eCharacterClass.MaulerAlb:
                case eCharacterClass.AlbionRogue:
                    return eObjectType.Leather;

                case eCharacterClass.Armsman:
                    if (level < 5)
                    {
                        return eObjectType.Studded;
                    }
                    else if (level < 15)
                    {
                        return eObjectType.Chain;
                    }
                    else
                    {
                        return eObjectType.Plate;
                    }

                case eCharacterClass.Paladin:
                    if (level < 10)
                    {
                        return eObjectType.Studded;
                    }
                    else if (level < 20)
                    {
                        return eObjectType.Chain;
                    }
                    else
                    {
                        return eObjectType.Plate;
                    }

                case eCharacterClass.Reaver:
                case eCharacterClass.Mercenary:
                    if (level < 10)
                    {
                        return eObjectType.Studded;
                    }
                    else
                    {
                        return eObjectType.Chain;
                    }

                case eCharacterClass.Minstrel:
                    if (level < 10)
                    {
                        return eObjectType.Leather;
                    }
                    else if (level < 20)
                    {
                        return eObjectType.Studded;
                    }
                    else
                    {
                        return eObjectType.Chain;
                    }

                case eCharacterClass.Fighter:
                    return eObjectType.Studded;

                case eCharacterClass.Scout:
                    if (level < 10)
                    {
                        return eObjectType.Leather;
                    }
                    else { return eObjectType.Studded; }

                case eCharacterClass.Cleric:
                    if (level < 10)
                    {
                        return eObjectType.Leather;
                    }
                    else if (level < 20)
                    {
                        return eObjectType.Studded;
                    }
                    else
                    {
                        return eObjectType.Chain;
                    }

                default:
                    return eObjectType.Cloth;
            }
        }

        public static eObjectType GetMidgardWeapon(eCharacterClass charClass)
        {

            List<eObjectType> weaponTypes = new List<eObjectType>();
            /*
			 * Midgard Weapons
			eObjectType.Sword,
			eObjectType.Hammer,
			eObjectType.Axe,
			eObjectType.Shield,
			eObjectType.Staff,
			eObjectType.Spear,
			eObjectType.CompositeBow,
			eObjectType.LeftAxe,
			eObjectType.HandToHand,
			eObjectType.FistWraps,//Maulers
			eObjectType.MaulerStaff,//Maulers
			*/
            switch (charClass)
            {
                //staff classes
                case eCharacterClass.Bonedancer:
                case eCharacterClass.Runemaster:
                case eCharacterClass.Spiritmaster:
                case eCharacterClass.Warlock:
                case eCharacterClass.Mage:
                case eCharacterClass.Mystic:
                    weaponTypes.Add(eObjectType.Staff);
                    break;
                case eCharacterClass.Healer:
                case eCharacterClass.Shaman:
                    weaponTypes.Add(eObjectType.Staff);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Seer:
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Sword);
                    break;
                case eCharacterClass.Hunter:
                    weaponTypes.Add(eObjectType.Spear);
                    weaponTypes.Add(eObjectType.CompositeBow);
                    weaponTypes.Add(eObjectType.Spear);
                    weaponTypes.Add(eObjectType.CompositeBow);
                    weaponTypes.Add(eObjectType.Sword);
                    break;
                case eCharacterClass.Savage:
                    weaponTypes.Add(eObjectType.HandToHand);
                    weaponTypes.Add(eObjectType.HandToHand);
                    weaponTypes.Add(eObjectType.HandToHand);
                    weaponTypes.Add(eObjectType.HandToHand);
                    weaponTypes.Add(eObjectType.HandToHand);
                    weaponTypes.Add(eObjectType.HandToHand);
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.Hammer);
                    break;
                case eCharacterClass.Shadowblade:
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.LeftAxe);
                    weaponTypes.Add(eObjectType.LeftAxe);
                    weaponTypes.Add(eObjectType.LeftAxe);
                    weaponTypes.Add(eObjectType.LeftAxe);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.MidgardRogue:
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    break;
                case eCharacterClass.Berserker:
                    weaponTypes.Add(eObjectType.LeftAxe);
                    weaponTypes.Add(eObjectType.LeftAxe);
                    weaponTypes.Add(eObjectType.LeftAxe);
                    weaponTypes.Add(eObjectType.LeftAxe);
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Thane:
                case eCharacterClass.Warrior:
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Shield);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Skald:
                    //hi Catkain <3
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Viking:
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Axe);
                    weaponTypes.Add(eObjectType.Hammer);
                    weaponTypes.Add(eObjectType.TwoHandedWeapon);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Valkyrie:
                    weaponTypes.Add(eObjectType.Sword);
                    weaponTypes.Add(eObjectType.Spear);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.MaulerMid:
                    weaponTypes.Add(eObjectType.FistWraps);
                    weaponTypes.Add(eObjectType.MaulerStaff);
                    break;
                default:
                    return eObjectType.Staff;
            }

            //this list nonsense is kind of weird but we need to duplicate the 
            //items in the list to avoid apparent mid-number bias for random number gen

            //clone existing list
            List<eObjectType> outputList = new List<eObjectType>(weaponTypes);

            //add duplicate values
            foreach (eObjectType type in weaponTypes)
            {
                outputList.Add(type);
            }

            //get our random value from the list
            int randomGrab = Util.Random(0, outputList.Count - 1);


            //return a random type from our list of valid weapons
            return outputList[randomGrab];

        }

        public static eObjectType GetMidgardArmorType(eCharacterClass charClass, byte level)
        {

            switch (charClass)
            {
                //staff classes
                case eCharacterClass.Bonedancer:
                case eCharacterClass.Runemaster:
                case eCharacterClass.Spiritmaster:
                case eCharacterClass.Warlock:
                case eCharacterClass.Mage:
                case eCharacterClass.Mystic:
                case eCharacterClass.Seer:
                    return eObjectType.Cloth;

                case eCharacterClass.Shadowblade:
                case eCharacterClass.MaulerMid:
                case eCharacterClass.MidgardRogue:
                    return eObjectType.Leather;

                case eCharacterClass.Hunter:
                    if (level < 10)
                    {
                        return eObjectType.Leather;
                    }
                    else
                    {
                        return eObjectType.Studded;
                    }

                case eCharacterClass.Berserker:
                case eCharacterClass.Savage:
                case eCharacterClass.Viking:
                    return eObjectType.Studded;

                case eCharacterClass.Shaman:
                case eCharacterClass.Healer:
                    if (level < 10)
                    {
                        return eObjectType.Leather;
                    }
                    else if (level < 20)
                    {
                        return eObjectType.Studded;
                    }
                    else
                    {
                        return eObjectType.Chain;
                    }

                case eCharacterClass.Skald:
                    if (level < 20)
                    {
                        return eObjectType.Studded;
                    }
                    else { return eObjectType.Chain; }

                case eCharacterClass.Warrior:
                    if (level < 10)
                    {
                        return eObjectType.Studded;
                    }
                    else { return eObjectType.Chain; }

                case eCharacterClass.Thane:
                    if (level < 12)
                    {
                        return eObjectType.Studded;
                    }
                    else
                    {
                        return eObjectType.Chain;
                    }

                case eCharacterClass.Valkyrie:
                    if (level < 10) return eObjectType.Studded;
                    else return eObjectType.Chain;

                default:
                    return eObjectType.Cloth;
            }
        }

        public static eObjectType GetHiberniaWeapon(eCharacterClass charClass)
        {
            List<eObjectType> weaponTypes = new List<eObjectType>();
            /*
			 * Hibernia Weapons
			eObjectType.Blades,
			eObjectType.Blunt,
			eObjectType.Piercing,
			eObjectType.Shield,
			eObjectType.Staff,
			eObjectType.LargeWeapons,
			eObjectType.CelticSpear,
			eObjectType.Scythe,
			eObjectType.RecurvedBow,
			eObjectType.Instrument,
			eObjectType.FistWraps,//Maulers
			eObjectType.MaulerStaff,//Maulers
			*/
            switch (charClass)
            {
                //staff classes
                case eCharacterClass.Eldritch:
                case eCharacterClass.Enchanter:
                case eCharacterClass.Mentalist:
                case eCharacterClass.Animist:
                case eCharacterClass.Bainshee:
                case eCharacterClass.Forester:
                case eCharacterClass.Magician:
                    weaponTypes.Add(eObjectType.Staff);
                    break;
                case eCharacterClass.Valewalker:
                    weaponTypes.Add(eObjectType.Scythe);
                    break;
                case eCharacterClass.Stalker:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    break;
                case eCharacterClass.Nightshade:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Ranger:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.RecurvedBow);
                    weaponTypes.Add(eObjectType.RecurvedBow);
                    weaponTypes.Add(eObjectType.RecurvedBow);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Champion:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.LargeWeapons);
                    weaponTypes.Add(eObjectType.LargeWeapons);
                    weaponTypes.Add(eObjectType.LargeWeapons);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Hero:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.LargeWeapons);
                    weaponTypes.Add(eObjectType.CelticSpear);
                    weaponTypes.Add(eObjectType.LargeWeapons);
                    weaponTypes.Add(eObjectType.CelticSpear);
                    weaponTypes.Add(eObjectType.Shield);
                    weaponTypes.Add(eObjectType.Shield);
                    weaponTypes.Add(eObjectType.Shield);
                    weaponTypes.Add(eObjectType.Fired); //shortbow
                    break;
                case eCharacterClass.Blademaster:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Piercing);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.Fired); //shortbow
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Guardian:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.LargeWeapons);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Warden:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.Shield);
                    weaponTypes.Add(eObjectType.Fired); //shortbow
                    break;
                case eCharacterClass.Druid:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.Shield);
                    weaponTypes.Add(eObjectType.Staff);
                    break;
                case eCharacterClass.Bard:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.Shield);
                    weaponTypes.Add(eObjectType.Instrument);
                    weaponTypes.Add(eObjectType.Instrument);
                    break;
                case eCharacterClass.Naturalist:
                    weaponTypes.Add(eObjectType.Blades);
                    weaponTypes.Add(eObjectType.Blunt);
                    weaponTypes.Add(eObjectType.Shield);
                    break;
                case eCharacterClass.Vampiir:
                    weaponTypes.Add(eObjectType.Piercing);
                    break;
                case eCharacterClass.MaulerHib:
                    weaponTypes.Add(eObjectType.FistWraps);
                    weaponTypes.Add(eObjectType.MaulerStaff);
                    break;
                default:
                    return eObjectType.Staff;
            }

            //this list nonsense is kind of weird but we need to duplicate the 
            //items in the list to avoid apparent mid-number bias for random number gen

            //clone existing list
            List<eObjectType> outputList = new List<eObjectType>(weaponTypes);

            //add duplicate values
            foreach (eObjectType type in weaponTypes)
            {
                outputList.Add(type);
            }

            //get our random value from the list
            int randomGrab = Util.Random(0, outputList.Count - 1);


            //return a random type from our list of valid weapons
            return outputList[randomGrab];

        }

        public static eObjectType GetHiberniaArmorType(eCharacterClass charClass, byte level)
        {

            /* Hib Armor
			eObjectType.Cloth,
			eObjectType.Leather,
			eObjectType.Reinforced,
			eObjectType.Scale,
			 */
            switch (charClass)
            {
                //staff classes
                case eCharacterClass.Valewalker:
                case eCharacterClass.Animist:
                case eCharacterClass.Mentalist:
                case eCharacterClass.Enchanter:
                case eCharacterClass.Eldritch:
                case eCharacterClass.Bainshee:
                case eCharacterClass.Forester:
                case eCharacterClass.Magician:
                    return eObjectType.Cloth;

                case eCharacterClass.Nightshade:
                case eCharacterClass.MaulerHib:
                case eCharacterClass.Vampiir:
                case eCharacterClass.Stalker:
                case eCharacterClass.Naturalist:
                    return eObjectType.Leather;

                case eCharacterClass.Blademaster:
                    return eObjectType.Reinforced;

                case eCharacterClass.Ranger:
                    if (level < 10)
                    {
                        return eObjectType.Leather;
                    }
                    else
                    {
                        return eObjectType.Reinforced;
                    }

                case eCharacterClass.Champion:
                    if (level < 20)
                    {
                        return eObjectType.Reinforced;
                    }
                    else { return eObjectType.Scale; }

                case eCharacterClass.Hero:
                    if (level < 15)
                    {
                        return eObjectType.Reinforced;
                    }
                    else { return eObjectType.Scale; }

                case eCharacterClass.Warden:
                    if (level < 10)
                    {
                        return eObjectType.Leather;
                    }
                    else if (level < 20)
                    {
                        return eObjectType.Reinforced;
                    }
                    else { return eObjectType.Scale; }

                case eCharacterClass.Druid:
                    if (level < 10)
                    {
                        return eObjectType.Leather;
                    }
                    else if (level < 20)
                    {
                        return eObjectType.Reinforced;
                    }
                    else { return eObjectType.Scale; }

                case eCharacterClass.Bard:
                    if (level < 15)
                    {
                        return eObjectType.Leather;
                    }
                    else { return eObjectType.Reinforced; }

                case eCharacterClass.Guardian:
                    return eObjectType.Studded;

                default:
                    return eObjectType.Cloth;
            }
        }

        public static eInventorySlot GenerateItemType(eObjectType type)
        {
            if ((int)type >= (int)eObjectType._FirstArmor && (int)type <= (int)eObjectType._LastArmor)
                return (eInventorySlot)ArmorSlots[Util.Random(0, ArmorSlots.Length - 1)];
            switch (type)
            {
                //left or right standard
                //tolakram - left hand usable now set based on speed
                case eObjectType.FistWraps:
                case eObjectType.HandToHand:
                    return Util.Chance(50) ? (eInventorySlot)Slot.RIGHTHAND : (eInventorySlot)Slot.LEFTHAND;
                case eObjectType.Piercing:
                case eObjectType.Blades:
                case eObjectType.Blunt:
                case eObjectType.SlashingWeapon:
                case eObjectType.CrushingWeapon:
                case eObjectType.ThrustWeapon:
                case eObjectType.Flexible:
                    return (eInventorySlot)Slot.RIGHTHAND;
                //left or right or twohand
                case eObjectType.Sword:
                case eObjectType.Axe:
                case eObjectType.Hammer:
                    if (Util.Random(100) >= 50)
                        return (eInventorySlot)Slot.RIGHTHAND;
                    else
                        return (eInventorySlot)Slot.TWOHAND;
                //left
                case eObjectType.LeftAxe:
                case eObjectType.Shield:
                    return (eInventorySlot)Slot.LEFTHAND;
                //twohanded
                case eObjectType.LargeWeapons:
                case eObjectType.CelticSpear:
                case eObjectType.PolearmWeapon:
                case eObjectType.Spear:
                case eObjectType.Staff:
                case eObjectType.Scythe:
                case eObjectType.TwoHandedWeapon:
                case eObjectType.MaulerStaff:
                    return (eInventorySlot)Slot.TWOHAND;
                //ranged
                case eObjectType.CompositeBow:
                case eObjectType.Fired:
                case eObjectType.Longbow:
                case eObjectType.RecurvedBow:
                case eObjectType.Crossbow:
                    return (eInventorySlot)Slot.RANGED;
                case eObjectType.Magical:
                    return (eInventorySlot)MagicalSlots[Util.Random(0, MagicalSlots.Length - 1)];
                case eObjectType.Instrument:
                    return (eInventorySlot)Slot.RANGED;
            }
            return eInventorySlot.FirstEmptyBackpack;
        }

        private static eDamageType GenerateDamageType(eObjectType type, eCharacterClass charClass)
        {
            switch (type)
            {
                //all
                case eObjectType.TwoHandedWeapon:
                case eObjectType.PolearmWeapon:
                case eObjectType.Instrument:
                    return (eDamageType)Util.Random(1, 3);
                //slash
                case eObjectType.Axe:
                case eObjectType.Blades:
                case eObjectType.SlashingWeapon:
                case eObjectType.LeftAxe:
                case eObjectType.Sword:
                case eObjectType.Scythe:
                    return eDamageType.Slash;
                //thrust
                case eObjectType.ThrustWeapon:
                case eObjectType.Piercing:
                case eObjectType.CelticSpear:
                case eObjectType.Longbow:
                case eObjectType.RecurvedBow:
                case eObjectType.CompositeBow:
                case eObjectType.Fired:
                case eObjectType.Crossbow:
                    return eDamageType.Thrust;
                //crush
                case eObjectType.Hammer:
                case eObjectType.CrushingWeapon:
                case eObjectType.Blunt:
                case eObjectType.MaulerStaff: //Maulers
                case eObjectType.FistWraps: //Maulers
                case eObjectType.Staff:
                    return eDamageType.Crush;
                //specifics
                case eObjectType.HandToHand:
                case eObjectType.Spear:
                    return (eDamageType)Util.Random(2, 3);
                case eObjectType.LargeWeapons:
                case eObjectType.Flexible:
                    return (eDamageType)Util.Random(1, 2);
                //do shields return the shield size?
                case eObjectType.Shield:
                    return (eDamageType)Util.Random(1, GetMaxShieldSizeFromClass(charClass));
                    //return (eDamageType)Util.Random(1, 3);
            }
            return eDamageType.Natural;
        }

        private static int GetMaxShieldSizeFromClass(eCharacterClass charClass)
        {
            //shield size is based off of damage type
            //1 = small shield
            //2 = medium
            //3 = large
            switch (charClass)
            {
                case eCharacterClass.Berserker:
                case eCharacterClass.Skald:
                case eCharacterClass.Savage:
                case eCharacterClass.Healer:
                case eCharacterClass.Shaman:
                case eCharacterClass.Shadowblade:
                case eCharacterClass.Bard:
                case eCharacterClass.Druid:
                case eCharacterClass.Nightshade:
                case eCharacterClass.Ranger:
                case eCharacterClass.Infiltrator:
                case eCharacterClass.Minstrel:
                case eCharacterClass.Scout:
                case eCharacterClass.Heretic:
                case eCharacterClass.Naturalist:
                case eCharacterClass.Seer:
                    return 1;

                case eCharacterClass.Thane:
                case eCharacterClass.Warden:
                case eCharacterClass.Blademaster:
                case eCharacterClass.Champion:
                case eCharacterClass.Mercenary:
                case eCharacterClass.Cleric:
                case eCharacterClass.Viking:
                case eCharacterClass.Guardian:
                case eCharacterClass.Fighter:
                    return 2;

                case eCharacterClass.Warrior:
                case eCharacterClass.Hero:
                case eCharacterClass.Armsman:
                case eCharacterClass.Paladin:
                case eCharacterClass.Reaver:
                case eCharacterClass.Valkyrie:
                    return 3;
                default: return 1;
            }
        }

        #endregion

        #region generate item speed and abs

        private static int GetAbsorb(eObjectType type)
        {
            switch (type)
            {
                case eObjectType.Cloth: return 0;
                case eObjectType.Leather: return 10;
                case eObjectType.Studded: return 19;
                case eObjectType.Reinforced: return 19;
                case eObjectType.Chain: return 27;
                case eObjectType.Scale: return 27;
                case eObjectType.Plate: return 34;
                default: return 0;
            }
        }

        private void SetWeaponSpeed()
        {
            // tolakram - reset speeds based on data from allakhazam 1-26-2008
            // removed specific left hand speed - left hand usable set based on speed in GenerateItemNameModel

            switch ((eObjectType)this.Object_Type)
            {
                case eObjectType.SlashingWeapon:
                    {
                        this.SPD_ABS = Util.Random(26, 39);
                        return;
                    }
                case eObjectType.CrushingWeapon:
                    {
                        this.SPD_ABS = Util.Random(30, 40);
                        return;
                    }
                case eObjectType.ThrustWeapon:
                    {
                        this.SPD_ABS = Util.Random(25, 37);
                        return;
                    }
                case eObjectType.Fired:
                    {
                        this.SPD_ABS = Util.Random(40, 46);
                        return;
                    }
                case eObjectType.TwoHandedWeapon:
                    {
                        this.SPD_ABS = Util.Random(43, 51);
                        return;
                    }
                case eObjectType.PolearmWeapon:
                    {
                        this.SPD_ABS = Util.Random(53, 56);
                        return;
                    }
                case eObjectType.Staff:
                    {
                        this.SPD_ABS = Util.Random(30, 50);
                        return;
                    }
                case eObjectType.MaulerStaff: //Maulers
                    {
                        this.SPD_ABS = Util.Random(34, 54);
                        return;
                    }
                case eObjectType.Longbow:
                    {
                        this.SPD_ABS = Util.Random(40, 52);
                        return;
                    }
                case eObjectType.Crossbow:
                    {
                        this.SPD_ABS = Util.Random(33, 54);
                        return;
                    }
                case eObjectType.Flexible:
                    {
                        this.SPD_ABS = Util.Random(33, 39);
                        return;
                    }
                case eObjectType.Sword:
                    if (this.Hand == 1)
                    {
                        this.SPD_ABS = Util.Random(46, 51);  // two handed
                        return;
                    }
                    else
                    {
                        this.SPD_ABS = Util.Random(25, 38); // one handed
                        return;
                    }
                case eObjectType.Hammer:
                    {
                        if (this.Hand == 1)
                        {
                            this.SPD_ABS = Util.Random(49, 52);  // two handed
                            return;
                        }
                        else
                        {
                            this.SPD_ABS = Util.Random(31, 39); // one handed
                            return;
                        }
                    }
                case eObjectType.Axe:
                    {
                        if (this.Hand == 1)
                        {
                            this.SPD_ABS = Util.Random(49, 53);  // two handed
                            return;
                        }
                        else
                        {
                            this.SPD_ABS = Util.Random(37, 40); // one handed
                            return;
                        }
                    }
                case eObjectType.Spear:
                    {
                        this.SPD_ABS = Util.Random(43, 52);
                        return;
                    }
                case eObjectType.CompositeBow:
                    {
                        this.SPD_ABS = Util.Random(40, 47);
                        return;
                    }
                case eObjectType.LeftAxe:
                    {
                        this.SPD_ABS = Util.Random(27, 31);
                        return;
                    }
                case eObjectType.HandToHand:
                    {
                        this.SPD_ABS = Util.Random(27, 37);
                        return;
                    }
                case eObjectType.FistWraps:
                    {
                        this.SPD_ABS = Util.Random(28, 41);
                        return;
                    }
                case eObjectType.RecurvedBow:
                    {
                        this.SPD_ABS = Util.Random(45, 52);
                        return;
                    }
                case eObjectType.Blades:
                    {
                        this.SPD_ABS = Util.Random(27, 39);
                        return;
                    }
                case eObjectType.Blunt:
                    {
                        this.SPD_ABS = Util.Random(30, 40);
                        return;
                    }
                case eObjectType.Piercing:
                    {
                        this.SPD_ABS = Util.Random(25, 36);
                        return;
                    }
                case eObjectType.LargeWeapons:
                    {
                        this.SPD_ABS = Util.Random(47, 53);
                        return;
                    }
                case eObjectType.CelticSpear:
                    {
                        this.SPD_ABS = Util.Random(40, 56);
                        return;
                    }
                case eObjectType.Scythe:
                    {
                        this.SPD_ABS = Util.Random(40, 53);
                        return;
                    }
                case eObjectType.Shield:
                    {
                        switch (this.Type_Damage)
                        {
                            case 1:
                                this.SPD_ABS = 30;
                                return;
                            case 2:
                                this.SPD_ABS = 40;
                                return;
                            case 3:
                                this.SPD_ABS = 50;
                                return;
                        }
                        this.SPD_ABS = 50;
                        return;
                    }
            }
            // for unhandled types
            if (this.Hand == 1)
            {
                this.SPD_ABS = 50;  // two handed
                return;
            }
            else if (this.Hand == 2)
            {
                this.SPD_ABS = 30;  // left hand
                return;
            }
            else
            {
                this.SPD_ABS = 40; // right hand
                return;
            }
        }

        public void GenerateItemWeight()
        {
            eObjectType type = (eObjectType)this.Object_Type;
            eInventorySlot slot = (eInventorySlot)this.Item_Type;

            switch (type)
            {
                case eObjectType.LeftAxe:
                case eObjectType.Flexible:
                case eObjectType.Axe:
                case eObjectType.Blades:
                case eObjectType.HandToHand:
                case eObjectType.FistWraps: //Maulers
                    this.Weight = 20;
                    return;
                case eObjectType.CompositeBow:
                case eObjectType.RecurvedBow:
                case eObjectType.Longbow:
                case eObjectType.Blunt:
                case eObjectType.CrushingWeapon:
                case eObjectType.Fired:
                case eObjectType.Hammer:
                case eObjectType.Piercing:
                case eObjectType.SlashingWeapon:
                case eObjectType.Sword:
                case eObjectType.ThrustWeapon:
                    this.Weight = 30;
                    return;
                case eObjectType.Crossbow:
                case eObjectType.Spear:
                case eObjectType.CelticSpear:
                case eObjectType.Staff:
                case eObjectType.TwoHandedWeapon:
                case eObjectType.MaulerStaff: //Maulers
                    this.Weight = 40;
                    return;
                case eObjectType.Scale:
                case eObjectType.Chain:
                    {
                        switch (slot)
                        {
                            case eInventorySlot.ArmsArmor: this.Weight = 48; return;
                            case eInventorySlot.FeetArmor: this.Weight = 32; return;
                            case eInventorySlot.HandsArmor: this.Weight = 32; return;
                            case eInventorySlot.HeadArmor: this.Weight = 32; return;
                            case eInventorySlot.LegsArmor: this.Weight = 56; return;
                            case eInventorySlot.TorsoArmor: this.Weight = 80; return;
                        }
                        this.Weight = 0;
                        return;
                    }
                case eObjectType.Cloth:
                    {
                        switch (slot)
                        {
                            case eInventorySlot.ArmsArmor: this.Weight = 8; return;
                            case eInventorySlot.FeetArmor: this.Weight = 8; return;
                            case eInventorySlot.HandsArmor: this.Weight = 8; return;
                            case eInventorySlot.HeadArmor: this.Weight = 32; return;
                            case eInventorySlot.LegsArmor: this.Weight = 14; return;
                            case eInventorySlot.TorsoArmor: this.Weight = 20; return;
                        }
                        this.Weight = 0;
                        return;
                    }
                case eObjectType.Instrument:
                    this.Weight = 15;
                    return;
                case eObjectType.LargeWeapons:
                    this.Weight = 50;
                    return;
                case eObjectType.Leather:
                    {
                        switch (slot)
                        {
                            case eInventorySlot.ArmsArmor: this.Weight = 24; return;
                            case eInventorySlot.FeetArmor: this.Weight = 16; return;
                            case eInventorySlot.HandsArmor: this.Weight = 16; return;
                            case eInventorySlot.HeadArmor: this.Weight = 16; return;
                            case eInventorySlot.LegsArmor: this.Weight = 28; return;
                            case eInventorySlot.TorsoArmor: this.Weight = 40; return;
                        }
                        this.Weight = 0;
                        return;
                    }
                case eObjectType.Magical:
                    this.Weight = 5;
                    return;
                case eObjectType.Plate:
                    {
                        switch (slot)
                        {
                            case eInventorySlot.ArmsArmor: this.Weight = 54; return;
                            case eInventorySlot.FeetArmor: this.Weight = 36; return;
                            case eInventorySlot.HandsArmor: this.Weight = 36; return;
                            case eInventorySlot.HeadArmor: this.Weight = 40; return;
                            case eInventorySlot.LegsArmor: this.Weight = 63; return;
                            case eInventorySlot.TorsoArmor: this.Weight = 90; return;
                        }
                        this.Weight = 0;
                        return;
                    }
                case eObjectType.PolearmWeapon:
                    this.Weight = 60;
                    return;
                case eObjectType.Reinforced:
                case eObjectType.Studded:
                    {
                        switch (slot)
                        {
                            case eInventorySlot.ArmsArmor: this.Weight = 36; return;
                            case eInventorySlot.FeetArmor: this.Weight = 24; return;
                            case eInventorySlot.HandsArmor: this.Weight = 24; return;
                            case eInventorySlot.HeadArmor: this.Weight = 24; return;
                            case eInventorySlot.LegsArmor: this.Weight = 42; return;
                            case eInventorySlot.TorsoArmor: this.Weight = 60; return;
                        }
                        this.Weight = 0;
                        return;
                    }
                case eObjectType.Scythe:
                    this.Weight = 40;
                    return;
                case eObjectType.Shield:
                    switch (this.Type_Damage)
                    {
                        case 1:
                            this.Weight = 31;
                            return;
                        case 2:
                            this.Weight = 35;
                            return;
                        case 3:
                            this.Weight = 38;
                            return;
                    }
                    this.Weight = 31;
                    return;
            }
            this.Weight = 10;
            return;
        }

        #endregion
        bool m_named = false;
        #region Naming and Modeling
        public bool WriteMagicalName(eProperty property)
        {
            if (hPropertyToMagicPrefix.TryGetValue(property, out string prefix) && !m_named)
            {
                if (!string.IsNullOrEmpty(prefix) && this.Name.StartsWith("[ROG]|"))
                {
                    string safePrefix = prefix.Replace(" ", "").Replace("'", "");
                    string restOfName = this.Name.Substring(6);

                    // Construct the final formula (e.g., "[ROG]Mighty|1_CryptsAndUndead|Sword")
                    this.Name = $"[ROG]{safePrefix}|{restOfName}";
                }
                m_named = true;
                return true;
            }

            return false;
        }

        protected string GetQualityTierPrefixKey()
        {
            // Items below level 36 maintain naming process without "tier prefix"
            if (this.Level < 36) return "";

            eRegionCategory cat = RegionMapper.GetCategoryFromRegionID(this.DropRegionID);

            if (cat == eRegionCategory.Restricted) return "";

            int tier = 0;
            if (this.Level >= 36 && this.Level <= 48) tier = 1;
            else if (this.Level >= 49 && this.Level <= 64) tier = 2;
            else if (this.Level >= 65) tier = 3;

            eRegionEnvironment env = RegionMapper.GetEnvironmentFromRegionID(this.DropRegionID);

            // Creates the precise crossover key: e.g., "1_Catacombs_CryptsAndUndead"
            return $"{tier}_{cat}_{env}";
        }

        private void GenerateItemNameModel()
        {
            eInventorySlot slot = (eInventorySlot)this.Item_Type;
            eDamageType damage = (eDamageType)this.Type_Damage;
            eRealm realm = (eRealm)this.Realm;
            eObjectType type = (eObjectType)this.Object_Type;

            string name = "No Name";
            int model = 488;
            bool canAddExtension = false;

            switch (type)
            {
                //armor
                case eObjectType.Cloth:
                    {
                        name = "Cloth " + ArmorSlotToName(slot, type);

                        switch (realm)
                        {
                            case eRealm.Albion:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = 141; break;
                                    case eInventorySlot.LegsArmor: model = 140; break;
                                    case eInventorySlot.FeetArmor: model = 143; break;
                                    case eInventorySlot.HeadArmor:
                                        if (Util.Chance(30))
                                            model = 1278; //30% chance of wizard hat
                                        else
                                            model = 822;
                                        break;
                                    case eInventorySlot.HandsArmor: model = 142; break;
                                    case eInventorySlot.TorsoArmor:
                                        if (Util.Chance(60))
                                        {
                                            model = 139;
                                        }
                                        else
                                        {
                                            name = "Cloth Robe";

                                            switch (Util.Random(2))
                                            {
                                                case 0: model = 58; break;
                                                case 1: model = 65; break;
                                                case 2: model = 66; break;
                                            }
                                        }
                                        break;
                                }
                                break;

                            case eRealm.Midgard:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = 247; break;
                                    case eInventorySlot.LegsArmor: model = 246; break;
                                    case eInventorySlot.FeetArmor: model = 249; break;
                                    case eInventorySlot.HeadArmor:
                                        if (Util.Chance(30))
                                            model = 1280; //30% chance of wizard hat
                                        else
                                            model = 825;
                                        break;
                                    case eInventorySlot.HandsArmor: model = 248; break;
                                    case eInventorySlot.TorsoArmor:
                                        if (Util.Chance(60))
                                        {
                                            model = 245;
                                        }
                                        else
                                        {
                                            name = "Cloth Robe";

                                            switch (Util.Random(2))
                                            {
                                                case 0: model = 58; break;
                                                case 1: model = 65; break;
                                                case 2: model = 66; break;
                                            }
                                        }
                                        break;
                                }
                                break;

                            case eRealm.Hibernia:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = 380; break;
                                    case eInventorySlot.LegsArmor: model = 379; break;
                                    case eInventorySlot.FeetArmor: model = 382; break;
                                    case eInventorySlot.HeadArmor:
                                        if (Util.Chance(30))
                                            model = 1279; //30% chance of wizard hat
                                        else
                                            model = 826;
                                        break;
                                    case eInventorySlot.HandsArmor: model = 381; break;
                                    case eInventorySlot.TorsoArmor:
                                        if (Util.Chance(60))
                                        {
                                            model = 378;
                                        }
                                        else
                                        {
                                            name = "Cloth Robe";

                                            switch (Util.Random(2))
                                            {
                                                case 0: model = 58; break;
                                                case 1: model = 65; break;
                                                case 2: model = 66; break;
                                            }
                                        }
                                        break;
                                }
                                break;

                        }

                        if (slot != eInventorySlot.HeadArmor)
                            canAddExtension = true;

                        break;
                    }
                case eObjectType.Leather:
                    {
                        name = "Leather " + ArmorSlotToName(slot, type);

                        switch (realm)
                        {
                            case eRealm.Albion:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = GetLeatherSleevesForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.LegsArmor: model = GetLeatherPantsForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.FeetArmor: model = GetLeatherBootsForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.HeadArmor: model = GetLeatherHelmForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.TorsoArmor: model = GetLeatherTorsoForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.HandsArmor: model = GetLeatherHandsForLevel(Level, eRealm.Albion); break;
                                }
                                break;

                            case eRealm.Midgard:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = GetLeatherSleevesForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.LegsArmor: model = GetLeatherPantsForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.FeetArmor: model = GetLeatherBootsForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.HeadArmor: model = GetLeatherHelmForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.TorsoArmor: model = GetLeatherTorsoForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.HandsArmor: model = GetLeatherHandsForLevel(Level, eRealm.Midgard); break;
                                }
                                break;

                            case eRealm.Hibernia:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = GetLeatherSleevesForLevel(Level, eRealm.Hibernia); break;
                                    case eInventorySlot.LegsArmor: model = GetLeatherPantsForLevel(Level, eRealm.Hibernia); break;
                                    case eInventorySlot.FeetArmor: model = GetLeatherBootsForLevel(Level, eRealm.Hibernia); break;
                                    case eInventorySlot.HeadArmor: model = GetLeatherHelmForLevel(Level, eRealm.Hibernia); break;
                                    case eInventorySlot.TorsoArmor: model = GetLeatherTorsoForLevel(Level, eRealm.Hibernia); break;
                                    case eInventorySlot.HandsArmor: model = GetLeatherHandsForLevel(Level, eRealm.Hibernia); break;
                                }
                                break;

                        }

                        if (slot != eInventorySlot.HeadArmor
                            && slot != eInventorySlot.ArmsArmor
                            && slot != eInventorySlot.LegsArmor)
                            canAddExtension = true;

                        break;
                    }
                case eObjectType.Studded:
                    {
                        name = "Studded " + ArmorSlotToName(slot, type);
                        switch (realm)
                        {
                            case eRealm.Albion:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = GetStuddedSleevesForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.LegsArmor: model = GetStuddedPantsForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.FeetArmor: model = GetStuddedBootsForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.HeadArmor: model = GetStuddedHelmForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.TorsoArmor: model = GetStuddedTorsoForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.HandsArmor: model = GetStuddedHandsForLevel(Level, eRealm.Albion); break;
                                }
                                break;

                            case eRealm.Midgard:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = GetStuddedSleevesForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.LegsArmor: model = GetStuddedPantsForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.FeetArmor: model = GetStuddedBootsForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.HeadArmor: model = GetStuddedHelmForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.TorsoArmor: model = GetStuddedTorsoForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.HandsArmor: model = GetStuddedHandsForLevel(Level, eRealm.Midgard); break;
                                }
                                break;
                        }

                        if (slot != eInventorySlot.HeadArmor)
                            canAddExtension = true;

                        break;
                    }
                case eObjectType.Plate:
                    {
                        name = "Plate " + ArmorSlotToName(slot, type);
                        switch (slot)
                        {
                            case eInventorySlot.ArmsArmor: model = GetPlateSleevesForLevel(Level, eRealm.Albion); break;
                            case eInventorySlot.LegsArmor: model = GetPlatePantsForLevel(Level, eRealm.Albion); break;
                            case eInventorySlot.FeetArmor: model = GetPlateBootsForLevel(Level, eRealm.Albion); break;
                            case eInventorySlot.HeadArmor:
                                model = GetPlateHelmForLevel(Level, eRealm.Albion);
                                if (model == 93 || model == 95)
                                    name = "Plate Full Helm";
                                break;
                            case eInventorySlot.TorsoArmor: model = GetPlateTorsoForLevel(Level, eRealm.Albion); break;
                            case eInventorySlot.HandsArmor: model = GetPlateHandsForLevel(Level, eRealm.Albion); break;
                        }

                        if (slot != eInventorySlot.HeadArmor)
                            canAddExtension = true;

                        break;
                    }
                case eObjectType.Chain:
                    {
                        name = "Chain " + ArmorSlotToName(slot, type);
                        switch (realm)
                        {
                            case eRealm.Albion:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = GetChainSleevesForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.LegsArmor: model = GetChainPantsForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.FeetArmor: model = GetChainBootsForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.HeadArmor: model = GetChainHelmForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.TorsoArmor: model = GetChainTorsoForLevel(Level, eRealm.Albion); break;
                                    case eInventorySlot.HandsArmor: model = GetChainHandsForLevel(Level, eRealm.Albion); break;
                                }
                                break;

                            case eRealm.Midgard:
                                switch (slot)
                                {
                                    case eInventorySlot.ArmsArmor: model = GetChainSleevesForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.LegsArmor: model = GetChainPantsForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.FeetArmor: model = GetChainBootsForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.HeadArmor: model = GetChainHelmForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.TorsoArmor: model = GetChainTorsoForLevel(Level, eRealm.Midgard); break;
                                    case eInventorySlot.HandsArmor: model = GetChainHandsForLevel(Level, eRealm.Midgard); break;
                                }
                                break;
                        }

                        if (slot != eInventorySlot.HeadArmor)
                            canAddExtension = true;

                        break;
                    }
                case eObjectType.Reinforced:
                    {
                        name = "Reinforced " + ArmorSlotToName(slot, type);
                        switch (slot)
                        {
                            case eInventorySlot.ArmsArmor: model = GetReinforcedSleevesForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.LegsArmor: model = GetReinforcedPantsForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.FeetArmor: model = GetReinforcedBootsForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.HeadArmor: model = GetReinforcedHelmForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.TorsoArmor: model = GetReinforcedTorsoForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.HandsArmor: model = GetReinforcedHandsForLevel(Level, eRealm.Hibernia); break;
                        }

                        if (slot != eInventorySlot.HeadArmor)
                            canAddExtension = true;

                        break;
                    }
                case eObjectType.Scale:
                    {
                        name = "Scale " + ArmorSlotToName(slot, type);
                        switch (slot)
                        {
                            case eInventorySlot.ArmsArmor: model = GetScaleSleevesForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.LegsArmor: model = GetScalePantsForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.FeetArmor: model = GetScaleBootsForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.HeadArmor: model = GetScaleHelmForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.TorsoArmor: model = GetScaleTorsoForLevel(Level, eRealm.Hibernia); break;
                            case eInventorySlot.HandsArmor: model = GetScaleHandsForLevel(Level, eRealm.Hibernia); break;
                        }

                        if (slot != eInventorySlot.HeadArmor)
                            canAddExtension = true;

                        break;
                    }

                //weapons
                case eObjectType.Axe:
                    {
                        if (this.Hand == 1)
                        {
                            model = Get2HAxeModelForLevel(Level, realm);
                            name = GetNameFromId(model);
                        }
                        else // 1 handed axe; speed 28-45; 578 (hand), 316 (Bearded), 319 (War), 315 (Spiked), 573 (Double)
                        {
                            model = GetAxeModelForLevel(Level, realm);
                            name = GetNameFromId(model);
                        }
                        break;
                    }
                case eObjectType.Blades:
                    {
                        model = GetBladeModelForLevel(Level, eRealm.Hibernia);
                        // Blades; speed 22 - 45; Short Sword (445), Falcata (444), Broadsword (447), Longsword (446), Bastard Sword (473)
                        if (this.SPD_ABS <= 27)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 32)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else
                        {
                            name = GetNameFromId(model);
                        }
                        break;
                    }
                case eObjectType.Blunt:
                    {
                        // Blunt; speed 22 - 45; Club (449), Mace (450), Hammer (461), Spiked Mace (451), Pick Hammer (641)
                        model = GetBluntModelForLevel(Level, eRealm.Hibernia);
                        if (this.SPD_ABS < 31)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 35)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 40)
                        {
                            name = GetNameFromId(model);
                        }
                        else if (this.SPD_ABS < 43)
                        {
                            name = GetNameFromId(model);
                        }
                        else
                        {
                            name = GetNameFromId(model);
                        }

                        if (Util.Chance(1))
                            model = 3458; //1% chance of being a rolling pin
                        break;
                    }
                case eObjectType.CelticSpear:
                    {
                        model = GetSpearModelForLevel(Level, eRealm.Hibernia);
                        // Short Spear (470), Spear (469), Long Spear (476), War Spear (477)
                        if (this.SPD_ABS < 35)
                        {
                            name = "Short Spear";
                        }
                        else if (this.SPD_ABS < 45)
                        {
                            name = "Spear";
                        }
                        else if (this.SPD_ABS < 50)
                        {
                            name = "Long Spear";
                        }
                        else
                        {
                            name = "War Spear";
                        }
                        break;
                    }
                case eObjectType.CompositeBow:
                    {
                        if (this.SPD_ABS > 40)
                            name = "Great Composite Bow";
                        else
                            name = "Composite Bow";

                        model = GetBowModelForLevel(Level, eRealm.Midgard);
                        break;
                    }
                case eObjectType.Crossbow:
                    {
                        name = "Crossbow";
                        model = GetCrossbowModelForLevel(Level, eRealm.Albion);
                        break;
                    }
                case eObjectType.CrushingWeapon:
                    {
                        model = GetBluntModelForLevel(Level, eRealm.Albion);
                        // Hammer (12), Mace (13), Flanged Mace (14), War Hammer (15)
                        if (this.SPD_ABS < 33)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 35)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 40)
                        {
                            name = GetNameFromId(model);
                        }
                        else
                        {
                            name = GetNameFromId(model);
                        }
                        break;
                    }
                case eObjectType.Fired:
                    {
                        if (realm == eRealm.Albion)
                        {
                            name = "Short Bow";
                            model = 569;
                        }
                        else // hibernia
                        {
                            name = "Short Bow";
                            model = 922;
                        }
                        break;
                    }
                case eObjectType.Flexible:
                    {
                        model = GetFlexModelForLevel(Level, eRealm.Albion, damage);
                        switch (damage)
                        {
                            case eDamageType.Crush:
                                {
                                    if (this.SPD_ABS < 33)
                                    {
                                        name = "Morning Star";
                                    }
                                    else if (this.SPD_ABS < 40)
                                    {
                                        name = "Flail";
                                    }
                                    else
                                    {
                                        name = "Weighted Flail";
                                    }
                                    break;
                                }
                            case eDamageType.Slash:
                                {
                                    if (this.SPD_ABS < 33)
                                    {
                                        name = "Whip";
                                    }
                                    else if (this.SPD_ABS < 40)
                                    {
                                        name = "Chain";
                                    }
                                    else
                                    {
                                        name = "War Chain";
                                    }
                                    break;
                                }
                        }
                        break;

                    }
                case eObjectType.Hammer:
                    {
                        if (this.Hand == 1)
                        {
                            model = Get2HHammerForLevel(Level, eRealm.Midgard);
                            name = GetNameFromId(model);
                        }
                        else
                        {
                            model = GetBluntModelForLevel(Level, eRealm.Midgard);
                            name = GetNameFromId(model);
                        }
                        break;
                    }
                case eObjectType.HandToHand:
                    {
                        model = GetH2HModelForLevel(Level, eRealm.Midgard, damage);
                        switch (damage)
                        {
                            case eDamageType.Slash:
                                {
                                    name = GetNameFromId(model);
                                    break;
                                }
                            case eDamageType.Thrust:
                                {
                                    name = GetNameFromId(model);
                                    break;
                                }
                        }
                        // all hand to hand weapons usable in left hand
                        this.Hand = 2; // allow left hand
                        this.Item_Type = Slot.LEFTHAND;
                        break;
                    }
                case eObjectType.Instrument:
                    {
                        switch (this.DPS_AF)
                        {
                            case 0:
                            case 1:
                            case 2:
                            case 3:
                                {
                                    model = GetInstrumentModelForLevel(Level, realm);
                                    name = GetNameFromId(model);
                                    break;
                                }
                                /*
                                    {
                                        name = "Drum";
                                        model = 228;
                                        break;
                                    }

                                    {
                                        name = "Lute";
                                        model = 227;
                                        break;
                                    }

                                    {
                                        name = "Flute";
                                        model = 325;
                                        break;
                                    }*/
                        }
                        break;
                    }
                case eObjectType.LargeWeapons:
                    {
                        switch (damage)
                        {
                            case eDamageType.Slash:
                                {
                                    model = Get2HSwordForLevel(Level, eRealm.Hibernia);
                                    name = GetNameFromId(model);
                                    break;
                                }
                            case eDamageType.Crush:
                                {
                                    model = Get2HHammerForLevel(Level, eRealm.Hibernia);
                                    if (model == 474 || model == 912)
                                    {
                                        name = "Big Shillelagh";
                                    }
                                    else
                                    {
                                        name = GetNameFromId(model);
                                    }
                                    break;
                                }
                        }
                        break;
                    }
                case eObjectType.LeftAxe:
                    {
                        model = GetAxeModelForLevel(Level, eRealm.Midgard);
                        if (this.SPD_ABS < 25)
                        {
                            name = "Hand Axe";
                        }
                        else if (this.SPD_ABS < 30)
                        {
                            name = "Bearded Axe";
                        }
                        else
                        {
                            name = "War Axe";
                        }
                        break;
                    }
                case eObjectType.Longbow:
                    {
                        model = GetBowModelForLevel(Level, eRealm.Albion);
                        if (this.SPD_ABS < 44)
                        {
                            name = "Hunting Bow";
                        }
                        else if (this.SPD_ABS < 55)
                        {
                            name = "Longbow";
                        }
                        else
                        {
                            name = "Heavy Longbow";
                        }
                        break;
                    }
                case eObjectType.Magical:
                    {
                        switch (slot)
                        {
                            case eInventorySlot.Cloak:
                                {
                                    if (Util.Chance(50))
                                        name = "Mantle";
                                    else
                                        name = "Cloak";

                                    if (Util.Chance(50))
                                        model = 57;
                                    else if (Util.Chance(50))
                                        model = 559;
                                    else
                                        model = 560;

                                    break;
                                }
                            case eInventorySlot.Waist:
                                {
                                    if (Util.Chance(50))
                                        name = "Belt";
                                    else
                                        name = "Girdle";

                                    model = 597;
                                    break;
                                }
                            case eInventorySlot.Neck:
                                {
                                    if (Util.Chance(50))
                                        name = "Choker";
                                    else
                                        name = "Pendant";

                                    model = 101;
                                    break;
                                }
                            case eInventorySlot.Jewellery:
                                {
                                    if (Util.Chance(50))
                                        name = "Gem";
                                    else
                                        name = "Jewel";

                                    model = Util.Random(110, 119);
                                    break;
                                }
                            case eInventorySlot.LeftBracer:
                            case eInventorySlot.RightBracer:
                                {
                                    if (Util.Chance(50))
                                    {
                                        name = "Bracelet";
                                        model = 619;
                                    }
                                    else
                                    {
                                        name = "Bracer";
                                        model = 598;
                                    }

                                    break;
                                }
                            case eInventorySlot.LeftRing:
                            case eInventorySlot.RightRing:
                                {
                                    if (Util.Chance(50))
                                        name = "Ring";
                                    else
                                        name = "Wrap";

                                    model = 103;
                                    break;
                                }
                        }
                        break;
                    }
                case eObjectType.Piercing:
                    {
                        model = GetThrustModelForLevel(Level, eRealm.Hibernia);
                        if (this.SPD_ABS < 24)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 29)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 30)
                        {
                            name = GetNameFromId(model);
                        }
                        else
                        {
                            name = GetNameFromId(model);
                        }
                        break;
                    }
                case eObjectType.PolearmWeapon:
                    {
                        model = GetPolearmModelForLevel(Level, eRealm.Albion, damage);
                        switch (damage)
                        {
                            case eDamageType.Slash:
                                {
                                    name = "Lochaber Axe";
                                    break;
                                }
                            case eDamageType.Thrust:
                                {
                                    name = "Pike";
                                    break;
                                }
                            case eDamageType.Crush:
                                {
                                    name = "Lucerne Hammer";
                                    break;
                                }
                        }
                        break;
                    }
                case eObjectType.RecurvedBow:
                    {
                        model = GetBowModelForLevel(Level, eRealm.Hibernia);
                        if (this.SPD_ABS > 49)
                        {
                            name = "Great Recurve Bow";
                        }
                        else
                        {
                            name = "Recurve Bow";
                        }
                        break;
                    }
                case eObjectType.Scythe:
                    {
                        model = GetScytheModelForLevel(Level, eRealm.Hibernia);
                        if (this.SPD_ABS < 47)
                        {
                            name = "Scythe";
                        }
                        else if (this.SPD_ABS < 51)
                        {
                            name = "Martial Scythe";
                        }
                        else
                        {
                            name = "War Scythe";
                        }
                        break;
                    }
                case eObjectType.Shield:
                    {
                        model = GetShieldModelForLevel(Level, realm, (int)damage);
                        switch ((int)damage)
                        {
                            case 1:
                                {
                                    name = "Small Shield";
                                    break;
                                }
                            case 2:
                                {
                                    name = "Medium Shield";
                                    break;
                                }
                            case 3:
                                {
                                    name = "Large Shield";
                                    break;
                                }
                        }
                        break;
                    }
                case eObjectType.SlashingWeapon:
                    {
                        model = GetBladeModelForLevel(Level, eRealm.Albion);
                        if (this.SPD_ABS < 26)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 30)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 32)
                        {
                            name = GetNameFromId(model);
                        }
                        else if (this.SPD_ABS < 35)
                        {
                            name = GetNameFromId(model);
                        }
                        else if (this.SPD_ABS < 40)
                        {
                            name = GetNameFromId(model);
                        }
                        else
                        {
                            name = GetNameFromId(model);
                        }
                        break;
                    }
                case eObjectType.Spear:
                    {
                        model = GetSpearModelForLevel(Level, eRealm.Midgard);
                        name = GetNameFromId(model);
                        break;
                    }
                case eObjectType.MaulerStaff:
                    {
                        name = "Mauler Staff";
                        model = 19;
                        break;
                    }
                case eObjectType.Staff:
                    {
                        model = GetStaffModelForLevel(Level, realm);
                        switch (realm)
                        {
                            case eRealm.Albion:

                                if (Util.Chance(20))
                                {
                                    if (this.charClass == eCharacterClass.Friar)
                                    {
                                        if (this.SPD_ABS < 40)
                                        {
                                            name = "Quarterstaff";
                                        }
                                        else if (this.SPD_ABS < 50)
                                        {
                                            name = "Shod Quarterstaff";
                                        }
                                        else
                                        {
                                            name = "Heavy Shod Quarterstaff";
                                        }
                                    }
                                }
                                else
                                {
                                    name = GetNameFromId(model);
                                }
                                break;

                            case eRealm.Midgard:
                                name = GetNameFromId(model);
                                break;

                            case eRealm.Hibernia:
                                name = GetNameFromId(model);
                                break;
                        }
                        break;
                    }
                case eObjectType.Sword:
                    {
                        if (this.Hand == 1)
                        {
                            model = Get2HSwordForLevel(Level, eRealm.Midgard);
                            name = GetNameFromId(model);
                        }
                        else
                        {
                            model = GetBladeModelForLevel(Level, eRealm.Midgard);
                            name = GetNameFromId(model);
                        }
                        break;
                    }
                case eObjectType.ThrustWeapon:
                    {
                        model = GetThrustModelForLevel(Level, eRealm.Albion);
                        if (this.SPD_ABS < 24)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 28)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 30)
                        {
                            name = GetNameFromId(model);
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 36)
                        {
                            name = GetNameFromId(model);
                        }
                        else
                        {
                            name = GetNameFromId(model);
                        }
                        break;
                    }
                case eObjectType.TwoHandedWeapon:
                    {
                        switch (damage)
                        {
                            case eDamageType.Slash:
                                {
                                    model = Get2HSwordForLevel(Level, eRealm.Albion);
                                    name = GetNameFromId(model);
                                    break;
                                }
                            case eDamageType.Crush:
                                {
                                    model = Get2HHammerForLevel(Level, eRealm.Albion);
                                    name = GetNameFromId(model);
                                    break;
                                }
                            case eDamageType.Thrust:
                                {
                                    model = Get2HThrustForLevel(Level, eRealm.Albion);
                                    name = GetNameFromId(model);
                                    break;
                                }
                        }
                        break;
                    }
                case eObjectType.FistWraps: // Maulers
                    {
                        string str = "Fist";

                        if (Util.Chance(50))
                            str = "Hand";

                        if (this.SPD_ABS < 31)
                        {
                            name = str + " Wrap";
                            model = 3476;
                            this.Effect = 102; // smoke
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else if (this.SPD_ABS < 35)
                        {
                            name = "Studded " + str + " Wrap";
                            model = 3477;
                            this.Effect = 48; // fire
                            this.Hand = 2; // allow left hand
                            this.Item_Type = Slot.LEFTHAND;
                        }
                        else
                        {
                            name = "Spiked Fist Wrap";
                            model = 3478;
                            this.Effect = 49; // sparkle fire
                        }

                        break;
                    }
            }

            //each realm has a chance for special helmets during generation
            if (slot == eInventorySlot.HeadArmor)
            {
                switch (realm)
                {
                    case eRealm.Albion:
                        if (Util.Chance(1))
                            model = 1284; //1% chance of tarboosh
                        else if (Util.Chance(1))
                            model = 1281; //1% chance of robin hood hat
                        else if (Util.Chance(1))
                            model = 1287; //1% chance of jester hat
                        break;
                    case eRealm.Hibernia:
                        if (Util.Chance(1))
                            model = 1282; //1% chance of robin hood hat
                        else if (Util.Chance(1))
                            model = 1285; //1% chance of leaf hat
                        else if (Util.Chance(1))
                            model = 1288; //1% chance of stag helm
                        break;
                    case eRealm.Midgard:
                        if (Util.Chance(1))
                            model = 1289; //1% chance of wolf hat
                        else if (Util.Chance(1))
                            model = 1283; //1% chance of fur cap
                        else if (Util.Chance(1))
                            model = 1286; //1% chance of wing hat
                        break;
                }
            }

            // Create a safe key without spaces or apostrophes
            string baseNameKey = name.Replace(" ", "").Replace("-", "").Replace("'", "");

            // Fetch our dynamic tier key
            string tierKey = GetQualityTierPrefixKey();

            // Save as a formula with an empty prefix (e.g., "[ROG]|TierKey|Sword")
            this.Name = $"[ROG]|{tierKey}|{baseNameKey}";
            this.Model = model;

            if (canAddExtension)
            {
                byte ext = 0;
                if (slot == eInventorySlot.HandsArmor ||
                     slot == eInventorySlot.FeetArmor)
                    ext = GetNonTorsoExtensionForLevel(Level);
                else if (slot == eInventorySlot.TorsoArmor)
                    ext = GetTorsoExtensionForLevel(Level);

                this.Extension = ext;
            }

        }

        #region Leather Model Generation
        public static int GetLeatherTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(31);
                    if (Level > 20)
                        validModels.Add(36);
                    if (Level > 30)
                        validModels.Add(74);
                    if (Level > 40)
                        validModels.Add(134);
                    if (Level > 50)
                        validModels.Add(2797);
                    break;
                case eRealm.Midgard:
                    validModels.Add(240);
                    if (Level > 20)
                        validModels.Add(260);
                    if (Level > 30)
                        validModels.Add(280);
                    if (Level > 40)
                        validModels.Add(300);
                    if (Level > 50)
                        validModels.Add(2859);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(373);
                    if (Level >= 10)
                        validModels.Add(393);
                    if (Level >= 20)
                        validModels.Add(413);
                    if (Level >= 30)
                        validModels.Add(433);
                    if (Level >= 40)
                        validModels.Add(2988);
                    if (Level > 50)
                        validModels.Add(2828);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherPantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(32);
                    if (Level > 20)
                        validModels.Add(37);
                    if (Level > 30)
                        validModels.Add(75);
                    if (Level > 40)
                        validModels.Add(135);
                    if (Level > 50)
                        validModels.Add(2798);
                    break;
                case eRealm.Midgard:
                    validModels.Add(241);
                    if (Level > 20)
                        validModels.Add(261);
                    if (Level > 30)
                        validModels.Add(281);
                    if (Level > 40)
                        validModels.Add(301);
                    if (Level > 50)
                        validModels.Add(2860);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(374);
                    if (Level >= 10)
                        validModels.Add(394);
                    if (Level >= 20)
                        validModels.Add(414);
                    if (Level >= 30)
                        validModels.Add(434);
                    if (Level >= 40)
                        validModels.Add(1257);
                    if (Level > 50)
                        validModels.Add(2829);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(33);
                    if (Level > 20)
                        validModels.Add(38);
                    if (Level > 30)
                        validModels.Add(76);
                    if (Level > 40)
                        validModels.Add(136);
                    if (Level > 50)
                        validModels.Add(2799);
                    break;
                case eRealm.Midgard:
                    validModels.Add(242);
                    if (Level > 20)
                        validModels.Add(262);
                    if (Level > 30)
                        validModels.Add(282);
                    if (Level > 40)
                        validModels.Add(302);
                    if (Level > 50)
                        validModels.Add(2861);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(375);
                    if (Level >= 10)
                        validModels.Add(395);
                    if (Level >= 20)
                        validModels.Add(415);
                    if (Level >= 30)
                        validModels.Add(435);
                    if (Level >= 40)
                        validModels.Add(355);
                    if (Level > 50)
                        validModels.Add(2830);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(34);
                    if (Level > 20)
                        validModels.Add(39);
                    if (Level > 30)
                        validModels.Add(77);
                    if (Level > 40)
                        validModels.Add(137);
                    if (Level > 50)
                        validModels.Add(2802);
                    break;
                case eRealm.Midgard:
                    validModels.Add(243);
                    if (Level > 20)
                        validModels.Add(263);
                    if (Level > 30)
                        validModels.Add(283);
                    if (Level > 40)
                        validModels.Add(303);
                    if (Level > 50)
                        validModels.Add(2864);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(376);
                    if (Level >= 10)
                        validModels.Add(396);
                    if (Level >= 20)
                        validModels.Add(416);
                    if (Level >= 30)
                        validModels.Add(436);
                    if (Level >= 40)
                        validModels.Add(1259);
                    if (Level > 50)
                        validModels.Add(2833);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(40);
                    if (Level > 20)
                        validModels.Add(133);
                    if (Level > 30)
                        validModels.Add(78);
                    if (Level > 40)
                        validModels.Add(138);
                    if (Level > 50)
                        validModels.Add(2801);
                    break;
                case eRealm.Midgard:
                    validModels.Add(244);
                    if (Level > 20)
                        validModels.Add(264);
                    if (Level > 30)
                        validModels.Add(284);
                    if (Level > 40)
                        validModels.Add(304);
                    if (Level > 50)
                        validModels.Add(2863);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(377);
                    if (Level >= 10)
                        validModels.Add(397);
                    if (Level >= 20)
                        validModels.Add(417);
                    if (Level >= 30)
                        validModels.Add(437);
                    if (Level >= 40)
                        validModels.Add(1260);
                    if (Level > 50)
                        validModels.Add(2832);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetLeatherHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(62);
                    if (Level > 35)
                        validModels.Add(1231);
                    if (Level > 45)
                        validModels.Add(2800);
                    if (Level > 50)
                        validModels.Add(1232);
                    break;
                case eRealm.Midgard:
                    validModels.Add(335);
                    if (Level > 35)
                        validModels.Add(336);
                    if (Level > 45)
                        validModels.Add(337);
                    if (Level > 50)
                        validModels.Add(1214);
                    break;
                case eRealm.Hibernia:
                    validModels.Add(438);
                    if (Level > 35)
                        validModels.Add(439);
                    if (Level > 45)
                        validModels.Add(440);
                    if (Level > 50)
                        validModels.Add(1198);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Studded Model Generation
        public static int GetStuddedTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(51);
                    if (Level > 20)
                        validModels.Add(81);
                    if (Level > 30)
                        validModels.Add(156);
                    if (Level > 40)
                        validModels.Add(216);
                    if (Level > 50)
                        validModels.Add(2803);
                    break;
                case eRealm.Midgard:
                    validModels.Add(230);
                    if (Level > 20)
                        validModels.Add(250);
                    if (Level > 30)
                        validModels.Add(270);
                    if (Level > 40)
                        validModels.Add(3012);
                    if (Level > 50)
                        validModels.Add(2865);
                    break;
                default:
                    validModels.Add(0);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedPantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(52);
                    if (Level > 20)
                        validModels.Add(82);
                    if (Level > 30)
                        validModels.Add(217);
                    if (Level > 40)
                        validModels.Add(157);
                    if (Level > 50)
                        validModels.Add(2804);
                    break;
                case eRealm.Midgard:
                    validModels.Add(231);
                    if (Level > 20)
                        validModels.Add(251);
                    if (Level > 30)
                        validModels.Add(271);
                    if (Level > 40)
                        validModels.Add(291);
                    if (Level > 50)
                        validModels.Add(2866);
                    break;
                default:
                    validModels.Add(52);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(53);
                    if (Level > 20)
                        validModels.Add(83);
                    if (Level > 30)
                        validModels.Add(218);
                    if (Level > 40)
                        validModels.Add(158);
                    if (Level > 50)
                        validModels.Add(2805);
                    break;
                case eRealm.Midgard:
                    validModels.Add(232);
                    if (Level > 20)
                        validModels.Add(252);
                    if (Level > 30)
                        validModels.Add(272);
                    if (Level > 40)
                        validModels.Add(292);
                    if (Level > 50)
                        validModels.Add(2867);
                    break;
                default:
                    validModels.Add(53);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(80);
                    if (Level > 20)
                        validModels.Add(85);
                    if (Level > 30)
                        validModels.Add(219);
                    if (Level > 40)
                        validModels.Add(159);
                    if (Level > 50)
                        validModels.Add(2808);
                    break;
                case eRealm.Midgard:
                    validModels.Add(233);
                    if (Level > 20)
                        validModels.Add(253);
                    if (Level > 30)
                        validModels.Add(273);
                    if (Level > 40)
                        validModels.Add(293);
                    if (Level > 50)
                        validModels.Add(2870);
                    break;
                default:
                    validModels.Add(80);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(54);
                    if (Level > 20)
                        validModels.Add(84);
                    if (Level > 30)
                        validModels.Add(220);
                    if (Level > 40)
                        validModels.Add(160);
                    if (Level > 50)
                        validModels.Add(2807);
                    break;
                case eRealm.Midgard:
                    validModels.Add(234);
                    if (Level > 20)
                        validModels.Add(254);
                    if (Level > 30)
                        validModels.Add(274);
                    if (Level > 40)
                        validModels.Add(294);
                    if (Level > 50)
                        validModels.Add(2869);
                    break;
                default:
                    validModels.Add(54);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStuddedHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(824);
                    if (Level > 35)
                        validModels.Add(1233);
                    if (Level > 45)
                        validModels.Add(1234);
                    if (Level > 50)
                        validModels.Add(1235);
                    break;
                case eRealm.Midgard:
                    validModels.Add(829);
                    if (Level > 35)
                        validModels.Add(830);
                    if (Level > 45)
                        validModels.Add(831);
                    if (Level > 50)
                        validModels.Add(1215);
                    break;
                default:
                    validModels.Add(824);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Chain Model Generation
        public static int GetChainTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(41);
                    if (Level > 10)
                        validModels.Add(181);
                    if (Level > 20)
                        validModels.Add(186);
                    if (Level > 30)
                        validModels.Add(191);
                    if (Level > 40)
                        validModels.Add(1251);
                    if (Level > 50)
                        validModels.Add(1246);
                    break;
                case eRealm.Midgard:
                    validModels.Add(235);
                    if (Level > 10)
                        validModels.Add(255);
                    if (Level > 20)
                        validModels.Add(275);
                    if (Level > 30)
                        validModels.Add(295);
                    if (Level > 40)
                        validModels.Add(999);
                    if (Level > 50)
                        validModels.Add(1262);
                    break;
                default:
                    validModels.Add(41);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainPantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(42);
                    if (Level > 10)
                        validModels.Add(1252);
                    if (Level > 20)
                        validModels.Add(182);
                    if (Level > 30)
                        validModels.Add(187);
                    if (Level > 40)
                        validModels.Add(192);
                    if (Level > 50)
                        validModels.Add(1247);
                    break;
                case eRealm.Midgard:
                    validModels.Add(236);
                    if (Level > 20)
                        validModels.Add(256);
                    if (Level > 30)
                        validModels.Add(276);
                    if (Level > 40)
                        validModels.Add(998);
                    if (Level > 50)
                        validModels.Add(1261);
                    break;
                default:
                    validModels.Add(236);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(43);
                    if (Level > 20)
                        validModels.Add(183);
                    if (Level > 30)
                        validModels.Add(188);
                    if (Level > 40)
                        validModels.Add(193);
                    if (Level > 50)
                        validModels.Add(1265);
                    break;
                case eRealm.Midgard:
                    validModels.Add(237);
                    if (Level > 20)
                        validModels.Add(257);
                    if (Level > 30)
                        validModels.Add(277);
                    if (Level > 40)
                        validModels.Add(1002);
                    if (Level > 50)
                        validModels.Add(1265);
                    break;
                default:
                    validModels.Add(237);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(44);
                    if (Level > 20)
                        validModels.Add(184);
                    if (Level > 30)
                        validModels.Add(189);
                    if (Level > 40)
                        validModels.Add(194);
                    if (Level > 50)
                        validModels.Add(1249);
                    break;
                case eRealm.Midgard:
                    validModels.Add(238);
                    if (Level > 20)
                        validModels.Add(258);
                    if (Level > 30)
                        validModels.Add(278);
                    if (Level > 40)
                        validModels.Add(1000);
                    if (Level > 50)
                        validModels.Add(1263);
                    break;
                default:
                    validModels.Add(44);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(45);
                    if (Level > 20)
                        validModels.Add(185);
                    if (Level > 30)
                        validModels.Add(190);
                    if (Level > 40)
                        validModels.Add(1250);
                    if (Level > 50)
                        validModels.Add(1255);
                    break;
                case eRealm.Midgard:
                    validModels.Add(239);
                    if (Level > 20)
                        validModels.Add(259);
                    if (Level > 30)
                        validModels.Add(279);
                    if (Level > 40)
                        validModels.Add(1001);
                    if (Level > 50)
                        validModels.Add(1264);
                    break;
                default:
                    validModels.Add(45);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetChainHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(1236);
                    if (Level > 35)
                        validModels.Add(63);
                    if (Level > 45)
                        validModels.Add(2812);
                    break;
                case eRealm.Midgard:
                    validModels.Add(832);
                    if (Level > 35)
                        validModels.Add(833);
                    if (Level > 45)
                        validModels.Add(834);
                    if (Level > 50)
                        validModels.Add(1216);
                    break;
                default:
                    validModels.Add(1236);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Plate Model Generation
        public static int GetPlateTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(46);
                    if (Level > 20)
                        validModels.Add(86);
                    if (Level > 30)
                        validModels.Add(201);
                    if (Level > 40)
                        validModels.Add(206);
                    if (Level > 50)
                    {
                        validModels.Add(1272);
                        validModels.Add(2815);
                    }
                    break;
                default:
                    validModels.Add(0);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlatePantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(47);
                    if (Level > 20)
                        validModels.Add(87);
                    if (Level > 30)
                        validModels.Add(202);
                    if (Level > 40)
                        validModels.Add(207);
                    if (Level > 50)
                    {
                        validModels.Add(1273);
                        validModels.Add(2816);
                    }
                    break;
                default:
                    validModels.Add(47);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlateSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(48);
                    if (Level > 20)
                        validModels.Add(88);
                    if (Level > 30)
                        validModels.Add(203);
                    if (Level > 40)
                        validModels.Add(208);
                    if (Level > 50)
                    {
                        validModels.Add(1274);
                        validModels.Add(2817);
                    }
                    break;
                default:
                    validModels.Add(48);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlateHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(49);
                    if (Level > 20)
                        validModels.Add(89);
                    if (Level > 30)
                        validModels.Add(204);
                    if (Level > 40)
                        validModels.Add(209);
                    if (Level > 50)
                        validModels.Add(2820);
                    break;
                default:
                    validModels.Add(49);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlateBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(50);
                    if (Level > 20)
                        validModels.Add(90);
                    if (Level > 30)
                        validModels.Add(205);
                    if (Level > 40)
                        validModels.Add(210);
                    if (Level > 50)
                        validModels.Add(2819);
                    break;
                default:
                    validModels.Add(50);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPlateHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(64);
                    if (Level > 10)
                        validModels.Add(93);
                    if (Level > 35)
                        validModels.Add(1238);
                    if (Level > 45)
                        validModels.Add(1239);
                    if (Level > 50)
                        validModels.Add(95);
                    break;
                default:
                    validModels.Add(64);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Reinforced Model Generation
        public static int GetReinforcedTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(363);
                    if (Level > 10)
                        validModels.Add(383);
                    if (Level > 20)
                        validModels.Add(403);
                    if (Level > 30)
                        validModels.Add(423);
                    if (Level > 40)
                        validModels.Add(1256);
                    if (Level > 50)
                        validModels.Add(3012);
                    break;
                default:
                    validModels.Add(363);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedPantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(364);
                    if (Level > 10)
                        validModels.Add(384);
                    if (Level > 20)
                        validModels.Add(404);
                    if (Level > 30)
                        validModels.Add(424);
                    if (Level > 40)
                        validModels.Add(1257);
                    if (Level > 50)
                        validModels.Add(3013);
                    break;
                default:
                    validModels.Add(364);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(365);
                    if (Level > 10)
                        validModels.Add(385);
                    if (Level > 20)
                        validModels.Add(405);
                    if (Level > 30)
                        validModels.Add(425);
                    if (Level > 40)
                        validModels.Add(1258);
                    if (Level > 50)
                        validModels.Add(3014);
                    break;
                default:
                    validModels.Add(365);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(366);
                    if (Level > 10)
                        validModels.Add(386);
                    if (Level > 20)
                        validModels.Add(406);
                    if (Level > 30)
                        validModels.Add(426);
                    if (Level > 40)
                        validModels.Add(1259);
                    if (Level > 50)
                        validModels.Add(3016);
                    break;
                default:
                    validModels.Add(366);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(367);
                    if (Level > 10)
                        validModels.Add(387);
                    if (Level > 20)
                        validModels.Add(407);
                    if (Level > 30)
                        validModels.Add(427);
                    if (Level > 40)
                        validModels.Add(1260);
                    if (Level > 50)
                        validModels.Add(3015);
                    break;
                default:
                    validModels.Add(50);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetReinforcedHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(835);
                    if (Level > 10)
                        validModels.Add(836);
                    if (Level > 35)
                        validModels.Add(837);
                    if (Level > 45)
                        validModels.Add(1199);
                    if (Level > 50)
                        validModels.Add(2837);
                    break;
                default:
                    validModels.Add(64);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Scale Model Generation
        public static int GetScaleTorsoForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(368);
                    if (Level > 10)
                        validModels.Add(388);
                    if (Level > 20)
                        validModels.Add(408);
                    if (Level > 30)
                        validModels.Add(428);
                    if (Level > 40)
                        validModels.Add(988);
                    if (Level > 50)
                        validModels.Add(3000);
                    break;
                default:
                    validModels.Add(368);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScalePantsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(369);
                    if (Level > 10)
                        validModels.Add(389);
                    if (Level > 20)
                        validModels.Add(409);
                    if (Level > 30)
                        validModels.Add(429);
                    if (Level > 40)
                        validModels.Add(989);
                    if (Level > 50)
                        validModels.Add(3001);
                    break;
                default:
                    validModels.Add(369);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScaleSleevesForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(370);
                    if (Level > 10)
                        validModels.Add(390);
                    if (Level > 20)
                        validModels.Add(410);
                    if (Level > 30)
                        validModels.Add(430);
                    if (Level > 40)
                        validModels.Add(990);
                    if (Level > 50)
                        validModels.Add(3002);
                    break;
                default:
                    validModels.Add(365);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScaleHandsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(371);
                    if (Level > 10)
                        validModels.Add(391);
                    if (Level > 20)
                        validModels.Add(411);
                    if (Level > 30)
                        validModels.Add(431);
                    if (Level > 40)
                        validModels.Add(991);
                    if (Level > 50)
                        validModels.Add(3005);
                    break;
                default:
                    validModels.Add(371);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScaleBootsForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(372);
                    if (Level > 10)
                        validModels.Add(392);
                    if (Level > 20)
                        validModels.Add(412);
                    if (Level > 30)
                        validModels.Add(432);
                    if (Level > 40)
                        validModels.Add(992);
                    if (Level > 50)
                        validModels.Add(3004);
                    break;
                default:
                    validModels.Add(372);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScaleHelmForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(838);
                    if (Level > 10)
                        validModels.Add(839);
                    if (Level > 35)
                        validModels.Add(840);
                    if (Level > 45)
                        validModels.Add(1200);
                    if (Level > 50)
                        validModels.Add(2843);
                    break;
                default:
                    validModels.Add(838);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }
        #endregion

        #region Weapon Model Generation

        public static int Get2HAxeModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(9);
                    if (Level > 10)
                        validModels.Add(72);
                    if (Level > 30)
                        validModels.Add(73);
                    break;
                case eRealm.Midgard:
                    validModels.Add(317);
                    if (Level > 10)
                    {
                        validModels.Add(318);
                    }
                    if (Level > 20)
                        validModels.Add(1030);
                    if (Level > 30)
                    {
                        validModels.Add(955);
                        validModels.Add(1033);
                    }
                    if (Level > 40)
                        validModels.Add(1027);

                    if (Level > 50)
                        validModels.Add(660);

                    break;
                default:
                    validModels.Add(2);
                    break;
            }


            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3662);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3705);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }
        public static int GetAxeModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(2);
                    if (Level > 10)
                        validModels.Add(878);
                    if (Level > 30)
                        validModels.Add(880);
                    if (Level > 40)
                        validModels.Add(3681);
                    if (Level > 50)
                        validModels.Add(3724);
                    break;
                case eRealm.Midgard:
                    validModels.Add(315);
                    validModels.Add(316);
                    if (Level > 10)
                    {
                        validModels.Add(319);
                        validModels.Add(573);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(951);
                        validModels.Add(953);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(1010);
                        validModels.Add(1011);
                    }

                    if (Level > 50)
                    {
                        validModels.Add(1014);
                        validModels.Add(1018);
                        validModels.Add(654);
                    }

                    if (Util.Chance(1) && Level > 40)
                    {
                        validModels.Clear();
                        validModels.Add(3681);
                        validModels.Add(3680);
                    }
                    if (Util.Chance(1) && Level > 50)
                    {
                        validModels.Clear();
                        validModels.Add(3723);
                        validModels.Add(3724);
                    }

                    break;
                default:
                    validModels.Add(2);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3662);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3705);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int Get2HSwordForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(459);
                    if (Level > 10)
                        validModels.Add(448);
                    if (Level > 20)
                        validModels.Add(639);
                    if (Level > 30)
                        validModels.Add(907);
                    if (Level > 40)
                    {
                        validModels.Add(910);
                        validModels.Add(3658);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(911);
                        validModels.Add(3701);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(6);
                    if (Level > 10)
                        validModels.Add(7);
                    if (Level > 20)
                        validModels.Add(9);
                    if (Level > 30)
                        validModels.Add(72);
                    if (Level > 40)
                    {
                        validModels.Add(73);
                        validModels.Add(645);
                        validModels.Add(841);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(843);
                        validModels.Add(845);
                        validModels.Add(847);

                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(314);
                    if (Level > 10)
                        validModels.Add(572);
                    if (Level > 20)
                        validModels.Add(658);
                    if (Level > 30)
                        validModels.Add(1035);
                    if (Level > 40)
                    {
                        validModels.Add(957);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(1032);
                    }
                    break;
                default:
                    validModels.Add(6);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3658);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3701);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetBladeModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(444);
                    validModels.Add(445);
                    if (Level > 10)
                    {
                        validModels.Add(446);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(447);

                    }
                    if (Level > 40)
                    {
                        validModels.Add(460);
                    }

                    if (Level > 50)
                    {
                        validModels.Add(473);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(1);
                    validModels.Add(3);
                    if (Level > 10)
                    {
                        validModels.Add(4);
                        validModels.Add(5);
                    }
                    if (Level > 20)
                        validModels.Add(8);
                    if (Level > 30)
                    {
                        validModels.Add(10);
                        validModels.Add(652);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(877);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(879);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(311);
                    if (Level > 10)
                    {
                        validModels.Add(310);
                        validModels.Add(312);
                    }
                    if (Level > 20)
                        validModels.Add(313);
                    if (Level > 30)
                        validModels.Add(949);
                    if (Level > 40)
                    {
                        validModels.Add(948);
                        validModels.Add(952);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(655);
                        validModels.Add(1017);
                        validModels.Add(1015);
                    }
                    break;
                default:
                    validModels.Add(445);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3675);
                validModels.Add(3674);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3717);
                validModels.Add(3718);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int Get2HHammerForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(474);
                    validModels.Add(463);
                    if (Level > 10)
                    {
                        validModels.Add(462);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(640);
                        validModels.Add(912);

                    }
                    if (Level > 40)
                    {
                        validModels.Add(904);
                        validModels.Add(906);
                        validModels.Add(908);
                        validModels.Add(909);
                        validModels.Add(3661);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(905);
                        validModels.Add(917);
                        validModels.Add(905);
                        validModels.Add(3704);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(16);

                    if (Level > 20)
                        validModels.Add(17);
                    if (Level > 30)
                        validModels.Add(644);
                    if (Level > 40)
                        validModels.Add(842);
                    if (Level > 50)
                        validModels.Add(844);
                    break;
                case eRealm.Midgard:
                    validModels.Add(574);
                    if (Level > 10)
                        validModels.Add(575);

                    if (Level > 20)
                    {
                        validModels.Add(576);
                        validModels.Add(659);
                    }

                    if (Level > 30)
                        validModels.Add(956);

                    if (Level > 40)
                    {
                        validModels.Add(1031);
                        validModels.Add(1034);
                    }
                    if (Level > 50)
                        validModels.Add(1028);
                    break;
                default:
                    validModels.Add(449);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3661);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3704);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetBluntModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(449);
                    validModels.Add(450);
                    if (Level > 10)
                        validModels.Add(451);
                    if (Level > 20)
                        validModels.Add(452);
                    if (Level > 30)
                        validModels.Add(461);
                    if (Level > 40)
                    {
                        validModels.Add(913);
                        validModels.Add(914);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(916);
                        validModels.Add(915);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(11);
                    validModels.Add(12);
                    if (Level > 10)
                    {
                        validModels.Add(13);
                        validModels.Add(14);
                    }
                    if (Level > 20)
                        validModels.Add(15);
                    if (Level > 30)
                    {
                        validModels.Add(18);
                        validModels.Add(20);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(853);
                        validModels.Add(854);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(855);
                        validModels.Add(856);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(320);
                    validModels.Add(321);
                    if (Level > 10)
                    {
                        validModels.Add(322);
                        validModels.Add(323);
                    }
                    if (Level > 20)
                        validModels.Add(324);
                    if (Level > 30)
                    {
                        validModels.Add(950);
                        validModels.Add(954);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(1019);
                        validModels.Add(1016);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(1016);
                        validModels.Add(1009);
                    }
                    break;
                default:
                    validModels.Add(449);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3676);
                validModels.Add(3677);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3719);
                validModels.Add(3720);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int Get2HThrustForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(846);

                    if (Level > 20)
                        validModels.Add(646);
                    if (Level > 30)
                        validModels.Add(2661);
                    if (Level > 40)
                    {

                    }
                    if (Level > 50)
                    {
                        validModels.Add(2208);

                    }
                    break;
                default:
                    validModels.Add(846);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();

                validModels.Add(3657);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3700);
                validModels.Add(3817);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetThrustModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(71);
                    validModels.Add(454);
                    if (Level > 10)
                    {
                        validModels.Add(455);
                        validModels.Add(902);
                    }
                    if (Level > 20)
                    {
                        validModels.Add(456);
                        validModels.Add(898);
                        validModels.Add(940);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(457);
                        validModels.Add(472);
                        validModels.Add(895);
                        validModels.Add(941);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(460);
                        validModels.Add(643);
                        validModels.Add(947);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(453);
                        validModels.Add(942);
                        validModels.Add(943);
                        validModels.Add(944);
                        validModels.Add(945);
                        validModels.Add(946);
                        validModels.Add(2209);
                    }
                    break;
                case eRealm.Albion:
                    validModels.Add(21);
                    validModels.Add(71);
                    if (Level > 10)
                    {
                        validModels.Add(876);
                        validModels.Add(22);
                        //validModels.Add(23);
                    }
                    if (Level > 20)
                    {
                        validModels.Add(889);
                        validModels.Add(25);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(888);
                        validModels.Add(887);
                        //validModels.Add(29);
                        validModels.Add(30);
                    }
                    if (Level > 40)
                        validModels.Add(653);
                    if (Level > 50)
                    {
                        validModels.Add(885);
                        validModels.Add(886);
                        validModels.Add(2209);
                    }
                    break;
                default:
                    validModels.Add(1);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3721);
                validModels.Add(3722);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3721);
                validModels.Add(3722);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetPolearmModelForLevel(int Level, eRealm realm, eDamageType dtype)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    switch (dtype)
                    {
                        case eDamageType.Slash:
                            validModels.Add(67);
                            if (Level > 10)
                                validModels.Add(68);
                            if (Level > 20)
                                validModels.Add(648);
                            if (Level > 30)
                                validModels.Add(649);
                            if (Level > 40)
                            {
                                validModels.Add(873);
                                if (Util.Chance(1))
                                {
                                    validModels.Clear();
                                    validModels.Add(3672);
                                }
                            }
                            if (Level > 50)
                            {
                                validModels.Add(874);
                                if (Util.Chance(1))
                                {
                                    validModels.Clear();
                                    validModels.Add(3715);
                                }
                            }
                            break;
                        case eDamageType.Crush:
                            validModels.Add(70);
                            if (Level > 10)
                                validModels.Add(650);
                            if (Level > 20)
                                validModels.Add(870);
                            if (Level > 30)
                                validModels.Add(875);
                            if (Level > 40 && Util.Chance(1))
                                validModels.Add(3673);
                            if (Level > 50 && Util.Chance(1))
                            {

                                validModels.Add(3833);
                                validModels.Add(3716);
                            }
                            break;
                        case eDamageType.Thrust:
                            validModels.Add(26);
                            if (Level > 10)
                                validModels.Add(69);
                            if (Level > 20)
                                validModels.Add(458);
                            if (Level > 30)
                                validModels.Add(649);
                            if (Level > 40)
                            {
                                validModels.Add(871);
                                if (Util.Chance(1))
                                {
                                    validModels.Clear();
                                    validModels.Add(3671);
                                }
                            }
                            if (Level > 50)
                            {
                                validModels.Add(872);
                                if (Util.Chance(1))
                                {
                                    validModels.Clear();
                                    validModels.Add(3714);
                                }
                            }
                            break;
                    }
                    break;
                default:
                    validModels.Add(328);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetSpearModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(556);
                    validModels.Add(469);
                    if (Level > 10)
                    {
                        validModels.Add(470);
                        validModels.Add(475);
                    }
                    if (Level > 20)
                    {
                        validModels.Add(476);
                        validModels.Add(477);
                    }
                    if (Level > 30)
                    {
                        validModels.Add(934);
                        validModels.Add(935);
                    }
                    if (Level > 40)
                    {
                        validModels.Add(556);
                        validModels.Add(933);
                        validModels.Add(936);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(937);
                        validModels.Add(938);
                        validModels.Add(939);
                        validModels.Add(2689);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(328);
                    if (Level > 10)
                        validModels.Add(329);
                    if (Level > 20)
                        validModels.Add(330);
                    if (Level > 30)
                        validModels.Add(331);
                    if (Level > 40)
                    {
                        validModels.Add(332);
                        validModels.Add(958);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(657);
                        validModels.Add(1029);
                    }
                    break;
                default:
                    validModels.Add(328);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3660);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3703);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetBowModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(471);
                    if (Level > 10)
                        validModels.Add(918);
                    if (Level > 20)
                        validModels.Add(919);
                    if (Level > 30)
                        validModels.Add(920);
                    if (Level > 40)
                    {
                        validModels.Add(921);
                        validModels.Add(922);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(923);
                        validModels.Add(925);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(564);
                    if (Level > 30)
                        validModels.Add(1037);
                    if (Level > 40)
                        validModels.Add(1038);
                    if (Level > 50)
                        validModels.Add(1039);
                    break;
                case eRealm.Albion:
                    validModels.Add(132);
                    if (Level > 10)
                        validModels.Add(570);
                    if (Level > 20)
                        validModels.Add(848);
                    if (Level > 30)
                        validModels.Add(849);
                    if (Level > 40)
                        validModels.Add(850);
                    if (Level > 50)
                    {
                        validModels.Add(851);
                        validModels.Add(852);
                    }
                    break;
                default:
                    validModels.Add(132);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3824);
                validModels.Add(3663);
            }
            /*
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3706);
                validModels.Add(3823);
            }*/

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetFlexModelForLevel(int Level, eRealm realm, eDamageType dtype)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    switch (dtype)
                    {
                        case eDamageType.Crush:
                            validModels.Add(861);
                            if (Level > 10)
                                validModels.Add(862);
                            if (Level > 20)
                                validModels.Add(864);
                            if (Level > 30)
                                validModels.Add(869);
                            if (Level > 40)
                            {
                                validModels.Add(2669);
                                if (Util.Chance(1))
                                    validModels.Add(3653);
                            }
                            if (Level > 50 && Util.Chance(1))
                            {
                                validModels.Clear();
                                validModels.Add(3696);
                                validModels.Add(3815);
                                validModels.Add(3952);
                            }
                            break;
                        case eDamageType.Slash:
                            validModels.Add(857);
                            validModels.Add(859);
                            validModels.Add(865);
                            if (Level > 10)
                                validModels.Add(863);
                            if (Level > 20)
                                validModels.Add(867);
                            if (Level > 30)
                                validModels.Add(868);
                            if (Level > 40)
                            {
                                validModels.Add(2670);
                                if (Util.Chance(1))
                                    validModels.Add(3654);
                            }
                            if (Level > 50 && Util.Chance(1))
                            {
                                validModels.Add(3697);
                                validModels.Add(3814);
                                validModels.Add(3951);
                            }
                            break;
                    }
                    break;
                default:
                    validModels.Add(132);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetH2HModelForLevel(int Level, eRealm realm, eDamageType dtype)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Midgard:
                    switch (dtype)
                    {
                        case eDamageType.Thrust:
                            validModels.Add(960);
                            validModels.Add(962);
                            validModels.Add(964);
                            if (Level > 10)
                                validModels.Add(966);
                            if (Level > 20)
                                validModels.Add(968);
                            if (Level > 30)
                                validModels.Add(970);
                            if (Level > 40)
                            {
                                validModels.Add(972);
                                validModels.Add(974);
                                validModels.Add(976);
                                if (Util.Chance(1))
                                {
                                    validModels.Add(3686);
                                    validModels.Add(3687);
                                }
                            }
                            if (Level > 50)
                            {
                                validModels.Add(978);
                                validModels.Add(980);
                                validModels.Add(982);
                                if (Util.Chance(1))
                                {
                                    validModels.Add(3729);
                                    validModels.Add(3730);
                                }
                            }
                            break;
                        case eDamageType.Slash:
                            validModels.Add(959);
                            validModels.Add(961);
                            validModels.Add(963);
                            if (Level > 10)
                                validModels.Add(965);
                            if (Level > 20)
                                validModels.Add(967);
                            if (Level > 30)
                                validModels.Add(969);
                            if (Level > 40)
                            {
                                validModels.Add(971);
                                validModels.Add(973);
                                validModels.Add(975);
                                if (Util.Chance(1))
                                {
                                    validModels.Add(3682);
                                    validModels.Add(3683);
                                }
                            }
                            if (Level > 50)
                            {
                                validModels.Add(977);
                                validModels.Add(979);
                                validModels.Add(981);
                                if (Util.Chance(1))
                                {
                                    validModels.Add(3725);
                                    validModels.Add(3726);
                                }
                            }
                            break;
                    }
                    break;
                default:
                    validModels.Add(132);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetCrossbowModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(226);
                    if (Level > 10)
                        validModels.Add(890);
                    if (Level > 20)
                        validModels.Add(891);
                    if (Level > 30)
                        validModels.Add(892);
                    if (Level > 40)
                    {
                        validModels.Add(893);
                        validModels.Add(894);
                    }
                    break;
                default:
                    validModels.Add(226);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3656);
            }
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3816);
                validModels.Add(3953);
                validModels.Add(3699);
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetScytheModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    validModels.Add(931);
                    if (Level > 10)
                        validModels.Add(929);
                    if (Level > 20)
                        validModels.Add(928);
                    if (Level > 30)
                        validModels.Add(930);
                    if (Level > 40)
                    {
                        validModels.Add(932);
                        validModels.Add(926);
                        validModels.Add(927);
                        if (Util.Chance(1))
                            validModels.Add(3665);
                    }
                    if (Level > 50 && Util.Chance(1))
                    {
                        validModels.Clear();
                        validModels.Add(3825);
                        validModels.Add(3708);
                        validModels.Add(3885);
                    }
                    break;
                default:
                    validModels.Add(931);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetStaffModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Albion:
                    validModels.Add(19);
                    if (Level > 10)
                        validModels.Add(442);
                    if (Level > 20)
                        validModels.Add(567);
                    if (Level > 30)
                        validModels.Add(568);
                    if (Level > 40)
                    {
                        validModels.Add(882);
                        validModels.Add(883);
                        validModels.Add(1166);
                        validModels.Add(1169);

                    }
                    if (Level > 50)
                    {
                        validModels.Add(821);
                        validModels.Add(881);
                        validModels.Add(1168);
                        validModels.Add(1167);
                        validModels.Add(1170);
                    }
                    break;
                case eRealm.Hibernia:
                    validModels.Add(1180);
                    if (Level > 10)
                        validModels.Add(1181);
                    if (Level > 20)
                        validModels.Add(1185);
                    if (Level > 30)
                        validModels.Add(1184);
                    if (Level > 40)
                    {
                        validModels.Add(1178);
                        validModels.Add(1174);
                        validModels.Add(1175);
                    }
                    if (Level > 50)
                    {
                        validModels.Add(1179);
                        validModels.Add(1173);
                    }
                    break;
                case eRealm.Midgard:
                    validModels.Add(327);
                    if (Level > 10)
                        validModels.Add(565);
                    if (Level > 20)
                        validModels.Add(828);
                    if (Level > 30)
                        validModels.Add(1171);
                    if (Level > 40)
                    {
                        validModels.Add(1172);
                        validModels.Add(1176);
                    }
                    if (Level > 50)
                        validModels.Add(1177);
                    break;
                default:
                    validModels.Add(931);
                    break;
            }

            if (Util.Chance(1) && Level > 40)
            {
                validModels.Clear();
                validModels.Add(3667);
            }
            if (Util.Chance(1) && Level > 50)
            {
                validModels.Clear();
                validModels.Add(3710);
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetShieldModelForLevel(int Level, eRealm realm, int size)
        {
            List<int> validModels = new List<int>();
            switch (realm)
            {
                case eRealm.Hibernia:
                    switch (size)
                    {
                        case 1:
                            validModels.Add(1046);
                            validModels.Add(1047);
                            validModels.Add(1048);
                            if (Level > 10)
                            {
                                validModels.Add(1082);
                                validModels.Add(1083);
                                validModels.Add(1084);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1100);
                                validModels.Add(1101);
                                validModels.Add(1102);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1163);
                                validModels.Add(1164);
                                validModels.Add(1165);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3888);
                            break;
                        case 2:
                            validModels.Add(1055);
                            validModels.Add(1056);
                            validModels.Add(1057);
                            if (Level > 10)
                            {
                                validModels.Add(1091);
                                validModels.Add(1092);
                                validModels.Add(1093);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1145);
                                validModels.Add(1146);
                                validModels.Add(1147);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1148);
                                validModels.Add(1149);
                                validModels.Add(1150);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1160);
                                validModels.Add(1161);
                                validModels.Add(1162);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3889);
                            break;
                        case 3:
                            validModels.Add(1082);
                            validModels.Add(1083);
                            validModels.Add(1084);
                            if (Level > 10)
                            {
                                validModels.Add(1073);
                                validModels.Add(1074);
                                validModels.Add(1075);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1064);
                                validModels.Add(1065);
                                validModels.Add(1066);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1151);
                                validModels.Add(1152);
                                validModels.Add(1153);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1154);
                                validModels.Add(1155);
                                validModels.Add(1156);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3890);
                            break;
                    }
                    break;
                case eRealm.Albion:
                    switch (size)
                    {
                        case 1:
                            validModels.Add(1040);
                            validModels.Add(1041);
                            validModels.Add(1042);
                            if (Level > 10)
                            {
                                validModels.Add(1103);
                                validModels.Add(1104);
                                validModels.Add(1105);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1118);
                                validModels.Add(1119);
                                validModels.Add(1120);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3965);
                            break;
                        case 2:
                            validModels.Add(1094);
                            validModels.Add(1095);
                            validModels.Add(1096);
                            if (Level > 10)
                            {
                                validModels.Add(1049);
                                validModels.Add(1050);
                                validModels.Add(1051);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1085);
                                validModels.Add(1086);
                                validModels.Add(1087);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1106);
                                validModels.Add(1107);
                                validModels.Add(1108);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1115);
                                validModels.Add(1116);
                                validModels.Add(1117);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3966);
                            break;
                        case 3:
                            validModels.Add(1058);
                            validModels.Add(1059);
                            validModels.Add(1060);
                            if (Level > 10)
                            {
                                validModels.Add(1067);
                                validModels.Add(1068);
                                validModels.Add(1069);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1112);
                                validModels.Add(1113);
                                validModels.Add(1114);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1109);
                                validModels.Add(1110);
                                validModels.Add(1111);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1121);
                                validModels.Add(1122);
                                validModels.Add(1123);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3967);
                            break;
                    }
                    break;
                case eRealm.Midgard:
                    switch (size)
                    {
                        case 1:
                            validModels.Add(1043);
                            validModels.Add(1044);
                            validModels.Add(1045);
                            if (Level > 10)
                            {
                                validModels.Add(1124);
                                validModels.Add(1125);
                                validModels.Add(1126);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1139);
                                validModels.Add(1140);
                                validModels.Add(1141);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1130);
                                validModels.Add(1131);
                                validModels.Add(1132);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3929);
                            break;
                        case 2:
                            validModels.Add(1097);
                            validModels.Add(1098);
                            validModels.Add(1099);
                            if (Level > 10)
                            {
                                validModels.Add(1088);
                                validModels.Add(1089);
                                validModels.Add(1090);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1052);
                                validModels.Add(1053);
                                validModels.Add(1054);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1127);
                                validModels.Add(1128);
                                validModels.Add(1129);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3930);
                            break;
                        case 3:
                            validModels.Add(1079);
                            validModels.Add(1080);
                            validModels.Add(1081);
                            if (Level > 10)
                            {
                                validModels.Add(1061);
                                validModels.Add(1062);
                                validModels.Add(1063);
                            }
                            if (Level > 20)
                            {
                                validModels.Add(1133);
                                validModels.Add(1134);
                                validModels.Add(1135);
                            }
                            if (Level > 30)
                            {
                                validModels.Add(1136);
                                validModels.Add(1137);
                                validModels.Add(1138);
                            }
                            if (Level > 40)
                            {
                                validModels.Add(1142);
                                validModels.Add(1143);
                                validModels.Add(1144);
                            }
                            if (Level > 50 && Util.Chance(1))
                                validModels.Add(3931);
                            break;
                    }
                    break;
                default:
                    validModels.Add(59);
                    break;
            }

            return validModels[Util.Random(validModels.Count - 1)];
        }

        public static int GetInstrumentModelForLevel(int Level, eRealm realm)
        {
            List<int> validModels = new List<int>();
            validModels.Add(227);
            validModels.Add(228);
            validModels.Add(325);
            if (Level > 10)
            {
                validModels.Add(2974);
                validModels.Add(2975);
                validModels.Add(2973);
            }
            if (Level > 20)
            {
                validModels.Add(2970);
                validModels.Add(2971);
                validModels.Add(2972);
            }
            if (Level > 30)
            {
                if (realm == eRealm.Albion)
                {
                    validModels.Add(2976);
                    validModels.Add(2977);
                    validModels.Add(2978);
                }
                else if (realm == eRealm.Hibernia)
                {
                    validModels.Add(2979);
                    validModels.Add(2980);
                    validModels.Add(2981);
                }

            }
            if (Level > 40)
            {
                validModels.Add(2114);
                validModels.Add(2115);
                validModels.Add(2116);
                validModels.Add(2117);
            }
            if (Level > 50 && Util.Chance(1))
            {
                validModels.Add(3688);
                validModels.Add(3731);
                validModels.Add(3848);
                if (Util.Chance(50))
                {
                    if (realm == eRealm.Albion)
                        validModels.Add(3985);
                    if (realm == eRealm.Hibernia)
                        validModels.Add(3908);
                }
                if (Util.Chance(5))
                {
                    if (realm == eRealm.Albion)
                        validModels.Add(3280);
                    if (realm == eRealm.Hibernia)
                        validModels.Add(3239);
                }
            }
            return validModels[Util.Random(validModels.Count - 1)];
        }

        #endregion

        #region Naming
        private static string GetNameFromId(int modelId)
        {
            switch (modelId)
            {
                case 1:
                case 23:
                case 25:
                case 28:
                case 454:
                case 457:
                case 472:
                case 571:
                case 885:
                case 887:
                case 895:
                case 898:
                case 902:
                case 943:
                case 944:
                case 949:
                case 1013:
                case 1021:
                case 3678:
                case 3679:
                case 3721:
                case 3722:
                case 3838:
                case 3839:
                    return "Dagger";
                case 21:
                case 876:
                case 889:
                    return "Dirk";
                case 30:
                    return "Gladius";
                case 456:
                case 71:
                    return "Stiletto";
                case 2:
                case 315:
                case 316:
                case 319:
                case 573:
                case 578:
                case 878:
                case 880:
                case 951:
                case 953:
                case 1010:
                case 1011:
                case 1014:
                case 1018:
                case 1023:
                case 1025:
                case 2657:
                case 2672:
                case 3680:
                case 3681:
                case 3723:
                case 3724:
                case 3840:
                case 3841:
                    return "Axe";
                case 654:
                    return "Cleaver";
                case 22:
                case 24:
                case 29:
                case 455:
                case 643:
                case 653:
                case 886:
                case 888:
                case 945:
                case 946:
                case 2686:
                case 2687:
                case 2658:
                    return "Rapier";
                case 3:
                case 4:
                case 5:
                case 10:
                case 310:
                case 311:
                case 312:
                case 313:
                case 445:
                case 446:
                case 447:
                case 473:
                case 655:
                case 877:
                case 879:
                case 896:
                case 897:
                case 899:
                case 900:
                case 901:
                case 903:
                case 948:
                case 952:
                case 1015:
                case 1017:
                case 1020:
                case 1024:
                case 2671:
                case 2682:
                case 3674:
                case 3675:
                case 3717:
                case 3718:
                case 3834:
                case 3835:
                    return "Sword";
                case 460:
                    return "Hooked Sword";
                case 444:
                    return "Falcata";
                case 8:
                case 645:
                    return "Scimitar";
                case 651:
                    return "Jambiya";
                case 652:
                    return "Sabre";
                case 2195:
                    return "Khopesh";
                case 2209:
                    return "Wakazashi";
                case 6:
                case 7:
                case 314:
                case 448:
                case 459:
                case 572:
                case 658:
                case 841:
                case 843:
                case 907:
                case 911:
                case 957:
                case 1032:
                case 1035:
                case 2660:
                case 2674:
                case 2690:
                case 3657:
                case 3658:
                case 3700:
                case 3701:
                case 3817:
                case 3818:
                case 3954:
                case 3955:
                    return "Greatsword";
                case 660:
                    return "War Cleaver";
                case 639:
                    return "Great Falcata";
                case 847:
                    return "Great Falchion";
                case 910:
                    return "Troll Splitter";
                case 2208:
                    return "Katana";
                case 959:
                case 960:
                case 963:
                case 964:
                case 965:
                case 966:
                case 969:
                case 970:
                case 973:
                case 974:
                case 977:
                case 978:
                case 3684:
                case 3685:
                case 3725:
                case 3726:
                    return "Greave";
                case 961:
                case 967:
                case 971:
                case 975:
                case 979:
                case 981:
                case 3682:
                case 3683:
                case 3727:
                case 3728:
                    return "Claw";
                case 962:
                case 968:
                case 972:
                case 976:
                case 980:
                case 982:
                case 3686:
                case 3687:
                case 3729:
                case 3730:
                    return "Fang";
                case 9:
                case 72:
                case 73:
                case 317:
                case 318:
                case 577:
                case 845:
                case 955:
                case 1027:
                case 1030:
                case 1033:
                case 2675:
                case 2985:
                case 3662:
                case 3705:
                case 3822:
                case 3882:
                case 3923:
                case 3959:
                    return "Greataxe";
                case 16:
                case 17:
                case 462:
                case 463:
                case 574:
                case 575:
                case 576:
                case 640:
                case 644:
                case 659:
                case 842:
                case 844:
                case 904:
                case 905:
                case 906:
                case 908:
                case 909:
                case 917:
                case 956:
                case 1028:
                case 1031:
                case 1034:
                case 2215:
                case 2662:
                case 2676:
                case 2691:
                case 3661:
                case 3704:
                case 3821:
                case 3881:
                case 3922:
                    return "Great Hammer";
                case 474:
                case 912:
                    return "Shillelagh";
                case 846:
                case 2661:
                case 646:
                    return "War Mattock";
                case 11:
                case 13:
                case 14:
                case 18:
                case 20:
                case 450:
                case 451:
                case 647:
                case 853:
                case 854:
                case 855:
                case 856:
                case 914:
                case 915:
                case 2659:
                case 2683:
                    return "Mace";
                case 12:
                case 15:
                case 320:
                case 321:
                case 322:
                case 323:
                case 324:
                case 461:
                case 641:
                case 656:
                case 913:
                case 916:
                case 950:
                case 954:
                case 1009:
                case 1012:
                case 1022:
                case 1026:
                case 2673:
                case 3676:
                case 3677:
                case 3836:
                case 3837:
                    return "Hammer";
                case 940:
                case 941:
                case 942:
                case 947:
                case 2684:
                    return "Adze";
                case 449:
                case 452:
                case 1016:
                case 1019:
                    return "Club";
                case 453:
                    return "Sickle";
                case 227:
                case 2117:
                case 2970:
                case 2973:
                case 2976:
                case 2979:
                    return "Lute";
                case 3848:
                    return "Mandolin";
                case 228:
                case 2114:
                case 2971:
                case 2974:
                case 2977:
                case 2980:
                    return "Drum";
                case 325:
                case 2115:
                case 2972:
                case 2975:
                case 2978:
                case 2981:
                    return "Flute";
                case 2116:
                case 3908:
                case 3949:
                case 3985:
                case 3280:
                case 3239:
                case 3688:
                case 3731:
                    return "Harp";
                case 328:
                case 329:
                case 331:
                case 332:
                case 469:
                case 470:
                case 475:
                case 476:
                case 477:
                case 556:
                case 642:
                case 657:
                case 933:
                case 934:
                case 935:
                case 936:
                case 938:
                case 939:
                case 958:
                case 1029:
                case 1036:
                case 1661:
                case 3659:
                case 3660:
                case 3671:
                case 3672:
                case 3673:
                case 3702:
                case 3703:
                case 3714:
                case 3715:
                case 3716:
                case 3819:
                case 3820:
                case 3831:
                case 3832:
                case 3833:
                    return "Spear";
                case 937:
                    return "Harpoon";
                case 330:
                case 458:
                case 1004:
                    return "Trident";
                default:
                    return "Staff";
            }
        }
        #endregion

        private static byte GetTorsoExtensionForLevel(int Level)
        {
            int possibleExtensions = 1;
            byte appliedExtension = 0;

            if (Level > 10)
                possibleExtensions++;
            if (Level > 20)
                possibleExtensions++;
            if (Level > 30)
                possibleExtensions++;
            if (Level > 40)
                possibleExtensions++;
            appliedExtension = (byte)Util.Random(possibleExtensions);
            if (Level > 50)
                appliedExtension++; //increment by 1 to unlock special extension for lvl 51+, as well as remove possibility of getting extension 0
            return appliedExtension;
        }

        private static byte GetNonTorsoExtensionForLevel(int Level)
        {
            List<byte> possibleExt = new List<byte>();
            possibleExt.Add(0);
            if (Level > 10)
                possibleExt.Add(8);
            if (Level > 20)
                possibleExt.Add(7);
            if (Level > 30)
                possibleExt.Add(5);
            if (Level > 40)
                possibleExt.Add(6);
            if (Level > 50)
            {
                possibleExt.Add(4);
                possibleExt.Remove(0);
            }
            byte appliedExtension = possibleExt[Util.Random(possibleExt.Count - 1)];
            return appliedExtension;
        }

        private static string ArmorSlotToName(eInventorySlot slot, eObjectType type)
        {
            switch (slot)
            {
                case eInventorySlot.ArmsArmor:
                    if (type == eObjectType.Plate)
                        return "Arms";
                    else
                        return "Sleeves";

                case eInventorySlot.FeetArmor:
                    return "Boots";

                case eInventorySlot.HandsArmor:
                    if (type == eObjectType.Plate)
                        return "Gauntlets";
                    else
                        return "Gloves";

                case eInventorySlot.HeadArmor:
                    if (type == eObjectType.Cloth)
                        return "Cap";
                    else if (type == eObjectType.Scale)
                        return "Coif";
                    else
                        return "Helm";

                case eInventorySlot.LegsArmor:
                    if (type == eObjectType.Cloth)
                        return "Pants";
                    else if (type == eObjectType.Plate)
                        return "Legs";
                    else
                        return "Leggings";

                case eInventorySlot.TorsoArmor:
                    if (type == eObjectType.Chain || type == eObjectType.Scale)
                        return "Hauberk";
                    else if (type == eObjectType.Plate)
                        return "Breastplate";
                    else if ((type == eObjectType.Leather || type == eObjectType.Studded) && Util.Chance(50))
                        return "Jerkin";
                    else
                        return "Vest";

                default: return "Armor";
                    // return GlobalConstants.SlotToName((int)slot);
            }
        }

        private int GetProcFromLevel(byte level)
        {
            int procID = 0;
            if (Util.Chance(50))
                procID = GetLifetapProcFromLevel(Level);
            else
                procID = GetDDProcFromLevel(Level);

            return procID;
        }

        private int GetDDProcFromLevel(int level)
        {
            if (Level <= 10)
                return 8020;
            if (Level <= 15)
                return 8021;
            if (Level <= 20)
                return 8022;
            if (Level <= 25)
                return 8023;
            if (Level <= 30)
                return 8024;
            if (Level <= 35)
                return 8025;
            if (Level <= 40)
                return 8026;
            if (Level <= 43)
                return 8027;

            return 0;
        }

        private int GetLifetapProcFromLevel(int level)
        {
            if (Level <= 10)
                return 8010;
            if (Level <= 15)
                return 8011;
            if (Level <= 20)
                return 8012;
            if (Level <= 25)
                return 8013;
            if (Level <= 30)
                return 8014;
            if (Level <= 35)
                return 8015;
            if (Level <= 40)
                return 8016;
            if (Level <= 43)
                return 8017;

            return 0;

        }

        #endregion

        #region Strictly Categorized Property Pools & Blacklist

        /// <summary>
        /// Strict Developer Blacklist: These properties will NEVER roll dynamically on loot.
        /// Using a HashSet provides extremely fast O(1) lookup speed during the item generation loop.
        /// </summary>
        public static readonly HashSet<eProperty> Blacklist = new HashSet<eProperty>
        {
            eProperty.Undefined,
            eProperty.CraftingSkillGain,
            eProperty.CraftingSpeed,
            eProperty.MythicalCoin,
            eProperty.LootChance,
            eProperty.BountyPoints,
            eProperty.XpPoints,
            eProperty.RealmPoints,
            eProperty.RobberyResist,
            eProperty.RobberyChanceBonus,
            eProperty.RobberyDelayReduction,
            eProperty.DamnationEffectEnhancement,
            eProperty.StealthEffectivenessBonus,
            eProperty.StealthDetectionBonus,
            eProperty.KeepDamage,
            eProperty.DeathExpLoss
        };

        public static readonly eProperty[] ClassicStats = new eProperty[]
        {
            eProperty.Strength, eProperty.Dexterity, eProperty.Constitution,
            eProperty.Quickness, eProperty.Intelligence, eProperty.Piety,
            eProperty.Empathy, eProperty.Charisma, eProperty.Acuity, eProperty.MaxMana, eProperty.MaxHealth
        };

        public static readonly eProperty[] ClassicResists = new eProperty[]
        {
            eProperty.Resist_Body, eProperty.Resist_Cold, eProperty.Resist_Crush,
            eProperty.Resist_Energy, eProperty.Resist_Heat, eProperty.Resist_Matter,
            eProperty.Resist_Slash, eProperty.Resist_Spirit, eProperty.Resist_Thrust
        };

        public static readonly eProperty[] CoreCombatPerformance = new eProperty[]
        {
            eProperty.ArmorFactor, eProperty.ArmorAbsorption, eProperty.MeleeDamage,
            eProperty.WeaponSkill, eProperty.FumbleChance,
            eProperty.ArcheryRange, eProperty.RangedDamage, eProperty.CriticalArcheryHitChance,
            eProperty.SpellRange, eProperty.SpellFumbleChance,
            eProperty.EvadeChance, eProperty.BlockChance, eProperty.ParryChance,
            eProperty.HealthRegenerationRate, eProperty.PowerRegenerationRate,
            eProperty.EnduranceRegenerationRate, eProperty.MaxConcentration, eProperty.FatigueConsumption,
            eProperty.MesmerizeDuration, eProperty.StunDuration, eProperty.SpeedDecreaseDuration,
            eProperty.AllMagicSkills, eProperty.AllMeleeWeaponSkills,
            eProperty.AllFocusLevels, eProperty.AllDualWieldingSkills, eProperty.AllArcherySkills, eProperty.AllSkills
        };

        public static readonly eProperty[] ToaMultipliers = new eProperty[]
        {
            eProperty.ArcherySpeed, eProperty.ArrowRecovery, eProperty.BuffEffectiveness,
            eProperty.CastingSpeed, eProperty.DebuffEffectivness,
            eProperty.Fatigue, eProperty.HealingEffectiveness, eProperty.PowerPool,
            eProperty.ResistPierce, eProperty.SpellDamage, eProperty.SpellDuration,
            eProperty.StyleDamage, eProperty.MythicalTension, eProperty.Resist_Natural
        };

        public static readonly eProperty[] ToaCaps = new eProperty[]
        {
            eProperty.StrCapBonus, eProperty.DexCapBonus, eProperty.ConCapBonus,
            eProperty.QuiCapBonus, eProperty.IntCapBonus, eProperty.PieCapBonus,
            eProperty.EmpCapBonus, eProperty.ChaCapBonus, eProperty.AcuCapBonus, eProperty.MaxHealthCapBonus,
            eProperty.PowerPoolCapBonus,
            eProperty.BodyResCapBonus, eProperty.ColdResCapBonus, eProperty.CrushResCapBonus,
            eProperty.EnergyResCapBonus, eProperty.HeatResCapBonus, eProperty.MatterResCapBonus,
            eProperty.SlashResCapBonus, eProperty.SpiritResCapBonus, eProperty.ThrustResCapBonus
        };

        public static readonly eProperty[] CatacombsProperties = new eProperty[]
        {
            eProperty.PieceAblative, eProperty.MeleeSpeed, eProperty.CriticalMeleeHitChance,
            eProperty.CounterAttack, eProperty.BladeturnReinforcement, eProperty.DefensiveBonus,
            eProperty.ReactionaryStyleDamage, eProperty.StyleCostReduction, eProperty.SpellLevel,
            eProperty.SpellPowerCost, eProperty.CriticalSpellHitChance, eProperty.MaxSpeed,
            eProperty.WaterSpeed, eProperty.MissHit, eProperty.ToHitBonus, eProperty.NegativeReduction
        };

        public static readonly eProperty[] MythicalProperties = new eProperty[]
        {
            eProperty.DPS, eProperty.ExtraHP, eProperty.MagicAbsorption, eProperty.StyleAbsorb,
            eProperty.Conversion, eProperty.ArcaneSyphon, eProperty.DotDurationDecrease,
            eProperty.MythicalDebuffResistChance, eProperty.CriticalHealHitChance,
            eProperty.CriticalDotHitChance, eProperty.OffhandDamageAndChanceBonus,
            eProperty.OffhandDamageBonus, eProperty.OffhandChanceBonus, eProperty.DotDamageBonus,
            eProperty.TensionConservationBonus, eProperty.MythicalSafeFall,
            eProperty.MythicalDiscumbering, eProperty.MythicalCrowdDuration, eProperty.MythicalOmniRegen,
            eProperty.LivingEffectiveness, eProperty.LivingEffectiveLevel,
            eProperty.SpellShieldChance, eProperty.MythicalSpellReflect
        };

        public static readonly eProperty[] MythicalCaps = new eProperty[]
        {
            eProperty.MythicalStrCapBonus, eProperty.MythicalDexCapBonus, eProperty.MythicalConCapBonus,
            eProperty.MythicalQuiCapBonus, eProperty.MythicalIntCapBonus, eProperty.MythicalPieCapBonus,
            eProperty.MythicalEmpCapBonus, eProperty.MythicalChaCapBonus, eProperty.MythicalAcuCapBonus
        };

        private static eProperty[] AlbSkillBonus = new eProperty[]
        {
            eProperty.Skill_Two_Handed,
            eProperty.Skill_Body,
	        //eProperty.Skill_Chants, // bonus not used
	        eProperty.Skill_Critical_Strike,
            eProperty.Skill_Cross_Bows,
            eProperty.Skill_Crushing,
            eProperty.Skill_Death_Servant,
            eProperty.Skill_DeathSight,
            eProperty.Skill_Dual_Wield,
            eProperty.Skill_Earth,
            eProperty.Skill_Enhancement,
            eProperty.Skill_Envenom,
            eProperty.Skill_Fire,
            eProperty.Skill_Flexible_Weapon,
            eProperty.Skill_Cold,
            eProperty.Skill_Instruments,
            eProperty.Skill_Long_bows,
            eProperty.Skill_Matter,
            eProperty.Skill_Mind,
            eProperty.Skill_Pain_working,
            eProperty.Skill_Parry,
            eProperty.Skill_Polearms,
            eProperty.Skill_Rejuvenation,
            eProperty.Skill_Shields,
            eProperty.Skill_Slashing,
            eProperty.Skill_Smiting,
            eProperty.Skill_SoulRending,
            eProperty.Skill_Spirit,
            eProperty.Skill_Staff,
            eProperty.Skill_Stealth,
            eProperty.Skill_Thrusting,
            eProperty.Skill_Wind,
            eProperty.Skill_Tormentshaper,
            eProperty.Skill_Wraithsight,
            eProperty.Skill_Void_Acolyte,
            eProperty.Skill_Aura_Manipulation,
            eProperty.Skill_FistWraps,
            eProperty.Skill_MaulerStaff,
            eProperty.Skill_Magnetism,
            eProperty.Skill_Power_Strikes,
        };

        private static eProperty[] HibSkillBonus = new eProperty[]
        {
            eProperty.Skill_Critical_Strike,
            eProperty.Skill_Envenom,
            eProperty.Skill_Parry,
            eProperty.Skill_Shields,
            eProperty.Skill_Stealth,
            eProperty.Skill_Light,
            eProperty.Skill_Void,
            eProperty.Skill_Mana,
            eProperty.Skill_Blades,
            eProperty.Skill_Blunt,
            eProperty.Skill_Piercing,
            eProperty.Skill_Large_Weapon,
            eProperty.Skill_Mentalism,
            eProperty.Skill_Regrowth,
            eProperty.Skill_Nurture,
            eProperty.Skill_Nature,
            eProperty.Skill_Music,
            eProperty.Skill_Celtic_Dual,
            eProperty.Skill_Celtic_Spear,
            eProperty.Skill_RecurvedBow,
            eProperty.Skill_Valor,
            eProperty.Skill_Verdant,
            eProperty.Skill_Creeping,
            eProperty.Skill_Arboreal,
            eProperty.Skill_Scythe,
            eProperty.Skill_Nightshade, // bonus not used if old Archery is activated or in DAOC Classic
            eProperty.Skill_Pathfinding, // bonus not used if old Archery is activated or in DAOC Classic
            eProperty.Skill_Dementia,
            eProperty.Skill_ShadowMastery,
            eProperty.Skill_VampiiricEmbrace,
            eProperty.Skill_EtherealShriek,
            eProperty.Skill_PhantasmalWail,
            eProperty.Skill_SpectralForce,
            eProperty.Skill_SpectralGuard,
            eProperty.Skill_Aura_Manipulation,
            eProperty.Skill_FistWraps,
            eProperty.Skill_MaulerStaff,
            eProperty.Skill_Magnetism,
            eProperty.Skill_Power_Strikes,
        };

        private static eProperty[] MidSkillBonus = new eProperty[]
        {
            eProperty.Skill_Critical_Strike,
            eProperty.Skill_Envenom,
            eProperty.Skill_Parry,
            eProperty.Skill_Shields,
            eProperty.Skill_Stealth,
            eProperty.Skill_Sword,
            eProperty.Skill_Hammer,
            eProperty.Skill_Axe,
            eProperty.Skill_Left_Axe,
            eProperty.Skill_Spear,
            eProperty.Skill_Mending,
            eProperty.Skill_Augmentation,
	        //Skill_Cave_Magic = 59,
	        eProperty.Skill_Darkness,
            eProperty.Skill_Suppression,
            eProperty.Skill_Runecarving,
            eProperty.Skill_Stormcalling,
	        //eProperty.Skill_BeastCraft, // bonus not used
			eProperty.Skill_Composite,
            eProperty.Skill_Battlesongs,
            eProperty.Skill_Subterranean,
            eProperty.Skill_BoneArmy,
            eProperty.Skill_Thrown_Weapons,
            eProperty.Skill_HandToHand,
            eProperty.Skill_Pacification,
            eProperty.Skill_Savagery,
            eProperty.Skill_OdinsWill,
            eProperty.Skill_Cursing,
            eProperty.Skill_Hexing,
            eProperty.Skill_Witchcraft,
            eProperty.Skill_Summoning,
            eProperty.Skill_Aura_Manipulation,
            eProperty.Skill_FistWraps,
            eProperty.Skill_MaulerStaff,
            eProperty.Skill_Magnetism,
            eProperty.Skill_Power_Strikes,
        };

        /// <summary>
        /// Safety Helper Method: Instantly checks if a property is legally allowed to roll on generated loot.
        /// </summary>
        public bool IsPropertyAllowed(eProperty prop)
        {
            if (Blacklist.Contains(prop)) return false;
            if (!IsStatAllowedForClass(prop)) return false;
            if (!IsAllowedForMeleeMageHybrid(prop)) return false;
            if (!StatIsValidForObjectType(prop)) return false;

            return true;
        }

        private bool IsStatAllowedForClass(eProperty prop)
        {
            CharacterClass cClass = CharacterClass.GetClass((int)this.charClass);

            // Dynamically check stat eligibility based on the Database configurations!
            bool isIntClass = cClass.ManaStat == eStat.INT;
            bool isEmpClass = cClass.ManaStat == eStat.EMP;
            bool isPieClass = cClass.ManaStat == eStat.PIE;
            bool isCharismaClass = cClass.ManaStat == eStat.CHR;

            // Restrict base casting stats & caps
            if (prop == eProperty.Intelligence || prop == eProperty.IntCapBonus || prop == eProperty.MythicalIntCapBonus) return isIntClass;
            if (prop == eProperty.Empathy || prop == eProperty.EmpCapBonus || prop == eProperty.MythicalEmpCapBonus) return isEmpClass;
            if (prop == eProperty.Piety || prop == eProperty.PieCapBonus || prop == eProperty.MythicalPieCapBonus) return isPieClass;
            if (prop == eProperty.Charisma || prop == eProperty.ChaCapBonus || prop == eProperty.MythicalChaCapBonus) return isCharismaClass;

            // Acuity applies to Int, Emp, and Pie users, but never Pure Melee or Charisma users
            if (prop == eProperty.Acuity || prop == eProperty.AcuCapBonus || prop == eProperty.MythicalAcuCapBonus) return isIntClass || isEmpClass || isPieClass;

            return true;
        }

        private bool IsAllowedForMeleeMageHybrid(eProperty prop)
        {
            CharacterClass cClass = CharacterClass.GetClass((int)this.charClass);
            
            bool isPureMelee = cClass.ClassType == eClassType.PureTank;
            bool isPureMage = cClass.ClassType == eClassType.ListCaster;

            if (isPureMelee && MageOnlyProperties.Contains(prop)) return false;
            if (isPureMage && MeleeOnlyProperties.Contains(prop)) return false;

            return true;
        }

        #endregion

        #region Dynamic Slot Allocation & Pool Probability Logic

        /// <summary>
        /// Calculates the absolute maximum number of property slots an item can generate 
        /// based on the region tier and the level of the monster killed.
        /// </summary>
        public static int GetTotalAllowedSlots(eRegionCategory category, int mobLevel)
        {
            int slots = 5;

            switch (category)
            {
                case eRegionCategory.ClassicOverworld:
                case eRegionCategory.ClassicDungeons:
                    slots = 5;
                    if (mobLevel >= 70) slots = 6;
                    break;

                case eRegionCategory.Battlegrounds:
                    slots = 5;
                    break;

                case eRegionCategory.AtlantisOverworld:
                    slots = 6;
                    if (mobLevel >= 70) slots = 7;
                    break;

                case eRegionCategory.AtlantisDungeons:
                    slots = 7;
                    if (mobLevel >= 70) slots = 8;
                    break;

                case eRegionCategory.Catacombs:
                    slots = 6;
                    if (mobLevel >= 60) slots = 7;
                    if (mobLevel >= 75) slots = 8;
                    break;

                case eRegionCategory.DeepCatacombs:
                    slots = 7;
                    if (mobLevel >= 70) slots = 8;
                    break;

                case eRegionCategory.MythicalZones:
                    slots = 8;
                    if (mobLevel >= 65) slots = 9;
                    if (mobLevel >= 75) slots = 10;
                    if (mobLevel >= 95) slots = 11;
                    break;
            }

            return Math.Max(5, Math.Min(slots, 11));
        }

        public enum ePropertyPool
        {
            ClassicStats,
            ClassicResists,
            ClassicSkills,
            Focus,
            CoreCombatPerformance,
            ToaMultipliers,
            ToaCaps,
            CatacombsProperties,
            MythicalProperties,
            MythicalCaps
        }

        /// <summary>
        /// Determines the appropriate bonus pool based on the Region, slot number, and mob level.
        /// Slots 1-3 are ALWAYS reserved for Classic stats/resists.
        /// Slots 4-5 have a smaller chance for rare bonuses.
        /// Slots 6+ have a high chance for rare bonuses.
        /// </summary>
        public static ePropertyPool GetPoolForSlot(eRegionCategory regionCat, int slotNumber, int mobLevel, bool hasSkill, bool isToa)
        {
            // Slots 1, 2, and 3 are ALWAYS reserved for Classic stats & resists
            if (slotNumber <= 3)
            {
                if (Properties.ROG_USE_WEIGHTED_GENERATION)
                {
                    List<ePropertyPool> bonTypes = new List<ePropertyPool>();
                    if (Util.Chance(ItemStatWeight)) bonTypes.Add(ePropertyPool.ClassicStats);
                    if (Util.Chance(ItemResistWeight)) bonTypes.Add(ePropertyPool.ClassicResists);
                    if (Util.Chance(ItemSkillWeight) && !hasSkill) bonTypes.Add(ePropertyPool.ClassicSkills);

                    if (bonTypes.Count > 0)
                        return bonTypes[Util.Random(0, bonTypes.Count - 1)];
                }

                int r = Util.Random(100);
                if (r < 15 && !hasSkill) return ePropertyPool.ClassicSkills;
                if (r < 45) return ePropertyPool.ClassicResists;
                return ePropertyPool.ClassicStats;
            }

            int roll = Util.Random(1, 100);
            bool isDeepSlot = slotNumber >= 6;

            switch (regionCat)
            {
                case eRegionCategory.ClassicOverworld:
                    // Slots 4, 5, and 6 use the base ToaItemChance
                    if (isToa && Util.Chance(ToaItemChance))
                        return Util.Chance(50) ? ePropertyPool.ToaMultipliers : ePropertyPool.ToaCaps;

                    if (roll <= 30) return ePropertyPool.CoreCombatPerformance;
                    return Util.Chance(50) ? ePropertyPool.ClassicStats : ePropertyPool.ClassicResists;

                case eRegionCategory.ClassicDungeons:
                    int dungeonToaChance = (slotNumber >= 6) ? (int)(ToaItemChance * 3.6) : (int)(ToaItemChance * 2.4);

                    if (isToa && Util.Chance(dungeonToaChance))
                        return Util.Chance(50) ? ePropertyPool.ToaMultipliers : ePropertyPool.ToaCaps;

                    if (roll <= 40) return ePropertyPool.CoreCombatPerformance;
                    return Util.Chance(50) ? ePropertyPool.ClassicStats : ePropertyPool.ClassicResists;

                case eRegionCategory.Battlegrounds:
                    // Only standard pools, rare chance of TOA for high level bosses
                    if (mobLevel >= 70 && roll <= (isDeepSlot ? 15 : 5)) return ePropertyPool.ToaMultipliers;
                    if (roll <= 30) return ePropertyPool.CoreCombatPerformance;
                    return Util.Chance(50) ? ePropertyPool.ClassicStats : ePropertyPool.ClassicResists;

                case eRegionCategory.AtlantisOverworld:
                    if (!isDeepSlot) // Slots 4 & 5
                    {
                        if (roll <= 13) return ePropertyPool.ToaCaps;
                        if (roll <= 20) return ePropertyPool.ToaMultipliers;
                    }
                    else // Slots 6
                    {
                        if (roll <= 25) return ePropertyPool.ToaCaps;
                        if (roll <= 35) return ePropertyPool.ToaMultipliers;
                    }

                    if (roll <= 60) return ePropertyPool.CoreCombatPerformance;
                    return Util.Chance(50) ? ePropertyPool.ClassicStats : ePropertyPool.ClassicResists;

                case eRegionCategory.AtlantisDungeons:
                    if (!isDeepSlot) // Slots 4 & 5
                    {
                        if (roll <= 15) return ePropertyPool.ToaMultipliers;
                        if (roll <= 35) return ePropertyPool.ToaCaps;
                    }
                    else // Slots 6
                    {
                        if (roll <= 30) return ePropertyPool.ToaCaps;
                        if (roll <= 60) return ePropertyPool.ToaMultipliers;
                    }

                    if (roll <= 80) return ePropertyPool.CoreCombatPerformance;
                    return Util.Chance(50) ? ePropertyPool.ClassicStats : ePropertyPool.ClassicResists;

                case eRegionCategory.Catacombs:
                    if (roll <= (isDeepSlot ? 25 : 10)) return ePropertyPool.ToaMultipliers;
                    if (roll <= (isDeepSlot ? 55 : 30)) return ePropertyPool.CatacombsProperties;
                    if (roll <= 80) return ePropertyPool.CoreCombatPerformance;
                    return Util.Chance(50) ? ePropertyPool.ClassicStats : ePropertyPool.ClassicResists;

                case eRegionCategory.DeepCatacombs:
                    if (roll <= (isDeepSlot ? 20 : 10)) return ePropertyPool.ToaCaps;
                    if (roll <= (isDeepSlot ? 40 : 20)) return ePropertyPool.ToaMultipliers;
                    if (roll <= (isDeepSlot ? 70 : 40)) return ePropertyPool.CatacombsProperties;
                    if (roll <= 85) return ePropertyPool.CoreCombatPerformance;
                    return Util.Chance(50) ? ePropertyPool.ClassicStats : ePropertyPool.ClassicResists;

                case eRegionCategory.MythicalZones:
                    if (roll <= (isDeepSlot ? 25 : 10)) return ePropertyPool.MythicalCaps;
                    if (roll <= (isDeepSlot ? 50 : 25)) return ePropertyPool.MythicalProperties;
                    if (roll <= (isDeepSlot ? 65 : 40)) return ePropertyPool.ToaCaps;
                    if (roll <= (isDeepSlot ? 80 : 55)) return ePropertyPool.ToaMultipliers;
                    if (roll <= 90) return ePropertyPool.CatacombsProperties;
                    if (roll <= 95) return ePropertyPool.CoreCombatPerformance;
                    return Util.Chance(50) ? ePropertyPool.ClassicStats : ePropertyPool.ClassicResists;
            }

            return ePropertyPool.ClassicStats;
        }

        /// <summary>
        /// Helper to map the chosen pool enum back to the physical array defined in Phase 2.
        /// </summary>
        public static eProperty[] GetPropertiesFromPool(ePropertyPool pool)
        {
            switch (pool)
            {
                case ePropertyPool.ClassicStats: return ClassicStats;
                case ePropertyPool.ClassicResists: return ClassicResists;
                case ePropertyPool.CoreCombatPerformance: return CoreCombatPerformance;
                case ePropertyPool.ToaMultipliers: return ToaMultipliers;
                case ePropertyPool.ToaCaps: return ToaCaps;
                case ePropertyPool.CatacombsProperties: return CatacombsProperties;
                case ePropertyPool.MythicalProperties: return MythicalProperties;
                case ePropertyPool.MythicalCaps: return MythicalCaps;
                default: return ClassicStats;
            }
        }

        /// <summary>
        /// Selects a random eProperty from the given pool, cleanly executing the Blacklist check.
        /// </summary>
        public eProperty GetRandomPropertyFromPool(eProperty[] pool)
        {
            eProperty selectedProperty = eProperty.Undefined;
            int safetyCounter = 0; // Prevent infinite loops

            while (safetyCounter < 50)
            {
                selectedProperty = pool[Util.Random(0, pool.Length - 1)];

                if (IsPropertyAllowed(selectedProperty) && selectedProperty != eProperty.Undefined)
                {
                    return selectedProperty;
                }
                safetyCounter++;
            }

            return eProperty.MaxHealth;
        }

        #endregion

        #region definitions

        public enum eGenerateType
        {
            Weapon,
            Armor,
            Magical,
            None,
        }

        private static int[] ArmorSlots = new int[] { 21, 22, 23, 25, 27, 28, };
        private static int[] MagicalSlots = new int[] { 24, 26, 29, 32, 33, 34, 35, 36 };

        // the following are doubled up to work around an apparent mid-number bias to the random number generator

        // note that weapon array has been adjusted to add weight to more commonly used items
        private static eObjectType[] AlbionWeapons = new eObjectType[]
        {
            eObjectType.ThrustWeapon,
            eObjectType.CrushingWeapon,
            eObjectType.SlashingWeapon,
            eObjectType.Shield,
            eObjectType.Staff,
            eObjectType.TwoHandedWeapon,
            eObjectType.Longbow,
            eObjectType.Flexible,
            eObjectType.PolearmWeapon,
            eObjectType.FistWraps,
			eObjectType.MaulerStaff,
			eObjectType.Instrument,
            eObjectType.Crossbow,
            eObjectType.ThrustWeapon,
            eObjectType.CrushingWeapon,
            eObjectType.SlashingWeapon,
            eObjectType.Shield,
            eObjectType.Staff,
            eObjectType.TwoHandedWeapon,
            eObjectType.Longbow,
            eObjectType.Flexible,
            eObjectType.PolearmWeapon,
            eObjectType.FistWraps,
			eObjectType.MaulerStaff,
			eObjectType.Instrument,
            eObjectType.Crossbow,
        };

        private static eObjectType[] AlbionArmor = new eObjectType[]
        {
            eObjectType.Cloth,
            eObjectType.Leather,
            eObjectType.Studded,
            eObjectType.Chain,
            eObjectType.Plate,
            eObjectType.Cloth,
            eObjectType.Leather,
            eObjectType.Studded,
            eObjectType.Chain,
            eObjectType.Plate,
        };
        private static eObjectType[] MidgardWeapons = new eObjectType[]
        {
            eObjectType.Sword,
            eObjectType.Hammer,
            eObjectType.Axe,
            eObjectType.Shield,
            eObjectType.Staff,
            eObjectType.Spear,
            eObjectType.CompositeBow ,
            eObjectType.LeftAxe,
            eObjectType.HandToHand,
            eObjectType.Sword,
            eObjectType.Hammer,
            eObjectType.Axe,
            eObjectType.Shield,
            eObjectType.Staff,
            eObjectType.Spear,
            eObjectType.CompositeBow ,
            eObjectType.LeftAxe,
            eObjectType.HandToHand,
        };

        private static eObjectType[] MidgardArmor = new eObjectType[]
        {
            eObjectType.Cloth,
            eObjectType.Leather,
            eObjectType.Studded,
            eObjectType.Chain,
            eObjectType.Cloth,
            eObjectType.Leather,
            eObjectType.Studded,
            eObjectType.Chain,
        };

        private static eObjectType[] HiberniaWeapons = new eObjectType[]
        {
            eObjectType.Blades,
            eObjectType.Blunt,
            eObjectType.Piercing,
            eObjectType.Shield,
            eObjectType.Staff,
            eObjectType.LargeWeapons,
            eObjectType.CelticSpear,
            eObjectType.Scythe,
            eObjectType.RecurvedBow,
            eObjectType.Instrument,
            eObjectType.FistWraps,
			eObjectType.MaulerStaff,
			eObjectType.Blades,
            eObjectType.Blunt,
            eObjectType.Piercing,
            eObjectType.Shield,
            eObjectType.Staff,
            eObjectType.LargeWeapons,
            eObjectType.CelticSpear,
            eObjectType.Scythe,
            eObjectType.RecurvedBow,
            eObjectType.Instrument,
            eObjectType.FistWraps,
			eObjectType.MaulerStaff,
        };

        private static eObjectType[] HiberniaArmor = new eObjectType[]
        {
            eObjectType.Cloth,
            eObjectType.Leather,
            eObjectType.Reinforced,
            eObjectType.Scale,
            eObjectType.Cloth,
            eObjectType.Leather,
            eObjectType.Reinforced,
            eObjectType.Scale,
        };

        #endregion definitions

        #region Smart Stat/Resist Cap Correlation Engine

        /// <summary>
        /// Analyzes the foundational stats (Slots 1, 2, and 3) already placed on the item.
        /// If a TOA Cap or Mythical Cap is selected to roll for a higher slot (4+), this method 
        /// ensures the Cap correlates directly to a Base Stat or Base Resist already on the item.
        /// </summary>
        public eProperty GetCorrelatedCap(ePropertyPool capPoolToRoll)
        {
            // 1. Gather the foundational properties already placed on the item
            List<eProperty> foundationalProps = new List<eProperty>();

            if (this.Bonus1Type > 0) foundationalProps.Add((eProperty)this.Bonus1Type);
            if (this.Bonus2Type > 0) foundationalProps.Add((eProperty)this.Bonus2Type);
            if (this.Bonus3Type > 0) foundationalProps.Add((eProperty)this.Bonus3Type);

            // 2. Build a list of valid caps that match our foundational stats
            List<eProperty> correlatedCaps = new List<eProperty>();

            foreach (eProperty baseProp in foundationalProps)
            {
                eProperty matchingCap = GetMatchingCap(baseProp, capPoolToRoll);

                if (matchingCap != eProperty.Undefined && IsPropertyAllowed(matchingCap) && !HasBonus(matchingCap))
                {
                    if (!correlatedCaps.Contains(matchingCap))
                    {
                        correlatedCaps.Add(matchingCap);
                    }
                }
            }

            // 3. If we found correlated caps, pick one randomly!
            if (correlatedCaps.Count > 0)
            {
                return correlatedCaps[Util.Random(0, correlatedCaps.Count - 1)];
            }

            // 4. The first 3 slots were Health/Mana/Skills, meaning no base stats/resists to follow.
            // Or, we already capped everything we had.
            // In this case, we roll a random Cap from the specified pool, respecting the Blacklist.
            eProperty[] fallbackPool = GetPropertiesFromPool(capPoolToRoll);

            for (int i = 0; i < 20; i++)
            {
                eProperty fallbackCap = GetRandomPropertyFromPool(fallbackPool);
                if (!HasBonus(fallbackCap))
                {
                    return fallbackCap;
                }
            }

            return GetRandomPropertyFromPool(fallbackPool);
        }

        /// <summary>
        /// A robust check across all 11 slots to see if a specific property has already been placed on the item.
        /// It also strictly enforces DAoC Stat Conflict logic (e.g. You cannot have Acuity + Intelligence on the same item).
        /// </summary>
        public bool HasBonus(eProperty property)
        {
            int p = (int)property;

            // 1. Direct slot check for all 11 possible slots
            if (this.Bonus1Type == p || this.Bonus2Type == p || this.Bonus3Type == p ||
                this.Bonus4Type == p || this.Bonus5Type == p || this.Bonus6Type == p ||
                this.Bonus7Type == p || this.Bonus8Type == p || this.Bonus9Type == p ||
                this.Bonus10Type == p || this.ExtraBonusType == p)
            {
                return true;
            }

            // 2. Cache the current item bonuses to check for mechanical conflicts
            List<int> currentBonuses = new List<int>
            {
                this.Bonus1Type, this.Bonus2Type, this.Bonus3Type, this.Bonus4Type,
                this.Bonus5Type, this.Bonus6Type, this.Bonus7Type, this.Bonus8Type,
                this.Bonus9Type, this.Bonus10Type, this.ExtraBonusType
            };

            // --- Classic Base Stats (Acuity vs Int/Pie/Emp) ---
            bool hasAcuity = currentBonuses.Contains((int)eProperty.Acuity);
            bool hasBaseCastingStat = currentBonuses.Contains((int)eProperty.Intelligence) ||
                                      currentBonuses.Contains((int)eProperty.Piety) ||
                                      currentBonuses.Contains((int)eProperty.Empathy);

            if (property == eProperty.Acuity && hasBaseCastingStat) return true;
            if ((property == eProperty.Intelligence || property == eProperty.Piety || property == eProperty.Empathy) && hasAcuity) return true;

            // --- TOA Caps ---
            bool hasAcuityCap = currentBonuses.Contains((int)eProperty.AcuCapBonus);
            bool hasBaseCastingCap = currentBonuses.Contains((int)eProperty.IntCapBonus) ||
                                     currentBonuses.Contains((int)eProperty.PieCapBonus) ||
                                     currentBonuses.Contains((int)eProperty.EmpCapBonus);

            if (property == eProperty.AcuCapBonus && hasBaseCastingCap) return true;
            if ((property == eProperty.IntCapBonus || property == eProperty.PieCapBonus || property == eProperty.EmpCapBonus) && hasAcuityCap) return true;

            // --- Mythical Caps ---
            bool hasMythicalAcuityCap = currentBonuses.Contains((int)eProperty.MythicalAcuCapBonus);
            bool hasMythicalBaseCastingCap = currentBonuses.Contains((int)eProperty.MythicalIntCapBonus) ||
                                             currentBonuses.Contains((int)eProperty.MythicalPieCapBonus) ||
                                             currentBonuses.Contains((int)eProperty.MythicalEmpCapBonus);

            if (property == eProperty.MythicalAcuCapBonus && hasMythicalBaseCastingCap) return true;
            if ((property == eProperty.MythicalIntCapBonus || property == eProperty.MythicalPieCapBonus || property == eProperty.MythicalEmpCapBonus) && hasMythicalAcuityCap) return true;

            return false;
        }

        /// <summary>
        /// Private helper mapping a base Stat or Resist to its exact TOA or Mythical Cap equivalent.
        /// </summary>
        private eProperty GetMatchingCap(eProperty baseProperty, ePropertyPool targetPool)
        {
            if (targetPool == ePropertyPool.ToaCaps)
            {
                switch (baseProperty)
                {
                    // Core Stats -> TOA Stat Caps
                    case eProperty.Strength: return eProperty.StrCapBonus;
                    case eProperty.Dexterity: return eProperty.DexCapBonus;
                    case eProperty.Constitution: return eProperty.ConCapBonus;
                    case eProperty.Quickness: return eProperty.QuiCapBonus;
                    case eProperty.Intelligence: return eProperty.IntCapBonus;
                    case eProperty.Piety: return eProperty.PieCapBonus;
                    case eProperty.Empathy: return eProperty.EmpCapBonus;
                    case eProperty.Acuity: return eProperty.AcuCapBonus;

                    // Vitals matching
                    case eProperty.MaxHealth: return eProperty.MaxHealthCapBonus;
                    case eProperty.MaxMana: return eProperty.PowerPoolCapBonus;

                    // Core Resists -> TOA Resist Caps
                    case eProperty.Resist_Body: return eProperty.BodyResCapBonus;
                    case eProperty.Resist_Cold: return eProperty.ColdResCapBonus;
                    case eProperty.Resist_Crush: return eProperty.CrushResCapBonus;
                    case eProperty.Resist_Energy: return eProperty.EnergyResCapBonus;
                    case eProperty.Resist_Heat: return eProperty.HeatResCapBonus;
                    case eProperty.Resist_Matter: return eProperty.MatterResCapBonus;
                    case eProperty.Resist_Slash: return eProperty.SlashResCapBonus;
                    case eProperty.Resist_Spirit: return eProperty.SpiritResCapBonus;
                    case eProperty.Resist_Thrust: return eProperty.ThrustResCapBonus;
                }
            }
            else if (targetPool == ePropertyPool.MythicalCaps)
            {
                switch (baseProperty)
                {
                    // Core Stats -> Mythical Stat Caps
                    case eProperty.Strength: return eProperty.MythicalStrCapBonus;
                    case eProperty.Dexterity: return eProperty.MythicalDexCapBonus;
                    case eProperty.Constitution: return eProperty.MythicalConCapBonus;
                    case eProperty.Quickness: return eProperty.MythicalQuiCapBonus;
                    case eProperty.Intelligence: return eProperty.MythicalIntCapBonus;
                    case eProperty.Piety: return eProperty.MythicalPieCapBonus;
                    case eProperty.Empathy: return eProperty.MythicalEmpCapBonus;
                    case eProperty.Acuity: return eProperty.MythicalAcuCapBonus;
                }
            }

            return eProperty.Undefined;
        }

        #endregion

        #region Advanced Naming Engine Dictionary
        public static void InitializeHashtables()
        {
            hPropertyToMagicPrefix.Clear();

            // CLASSIC BASE STATS, HEALTH & MANA
            hPropertyToMagicPrefix.Add(eProperty.Strength, "Mighty");
            hPropertyToMagicPrefix.Add(eProperty.Dexterity, "Adroit");
            hPropertyToMagicPrefix.Add(eProperty.Constitution, "Fortifying");
            hPropertyToMagicPrefix.Add(eProperty.Quickness, "Speedy");
            hPropertyToMagicPrefix.Add(eProperty.Intelligence, "Insightful");
            hPropertyToMagicPrefix.Add(eProperty.Piety, "Willful");
            hPropertyToMagicPrefix.Add(eProperty.Empathy, "Attuned");
            hPropertyToMagicPrefix.Add(eProperty.Charisma, "Glib");
            hPropertyToMagicPrefix.Add(eProperty.MaxHealth, "Sturdy");
            hPropertyToMagicPrefix.Add(eProperty.MaxMana, "Arcane");
            hPropertyToMagicPrefix.Add(eProperty.Acuity, "Brilliant");

            // CLASSIC RESISTANCES
            hPropertyToMagicPrefix.Add(eProperty.Resist_Body, "Bodybender");
            hPropertyToMagicPrefix.Add(eProperty.Resist_Cold, "Icebender");
            hPropertyToMagicPrefix.Add(eProperty.Resist_Crush, "Bluntbender");
            hPropertyToMagicPrefix.Add(eProperty.Resist_Energy, "Energybender");
            hPropertyToMagicPrefix.Add(eProperty.Resist_Heat, "Heatbender");
            hPropertyToMagicPrefix.Add(eProperty.Resist_Matter, "Matterbender");
            hPropertyToMagicPrefix.Add(eProperty.Resist_Slash, "Edgebender");
            hPropertyToMagicPrefix.Add(eProperty.Resist_Spirit, "Spiritbender");
            hPropertyToMagicPrefix.Add(eProperty.Resist_Thrust, "Thrustbender");
            hPropertyToMagicPrefix.Add(eProperty.Resist_Natural, "Essencebender");

            // GLOBAL / ALL SKILLS
            hPropertyToMagicPrefix.Add(eProperty.AllSkills, "Skillful");
            hPropertyToMagicPrefix.Add(eProperty.AllMagicSkills, "Mystical");
            hPropertyToMagicPrefix.Add(eProperty.AllMeleeWeaponSkills, "Gladiator");
            hPropertyToMagicPrefix.Add(eProperty.AllDualWieldingSkills, "Duelist");
            hPropertyToMagicPrefix.Add(eProperty.AllArcherySkills, "Bowmaster");
            hPropertyToMagicPrefix.Add(eProperty.AllFocusLevels, "Focused");

            // CLASSIC SKILLS (Melee, Ranged, Magic, Utility)
            hPropertyToMagicPrefix.Add(eProperty.Skill_Slashing, "Honed");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Crushing, "Battering");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Thrusting, "Perforator");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Two_Handed, "Sundering");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Polearms, "Decimator");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Flexible_Weapon, "Tensile");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Blades, "Razored");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Blunt, "Crushing");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Piercing, "Lancenator");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Dual_Wield, "Whirling");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Spear, "Impaling");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Sword, "Serrated");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Hammer, "Demolishing");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Axe, "Swathe Cutter's");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Left_Axe, "Cleaving");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Scythe, "Reaper's");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Staff, "Thunderer");
            hPropertyToMagicPrefix.Add(eProperty.Skill_HandToHand, "Martial");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Critical_Strike, "Lifetaker");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Large_Weapon, "Sundering");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Celtic_Dual, "Whirling");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Celtic_Spear, "Impaling");

            hPropertyToMagicPrefix.Add(eProperty.Skill_Cross_Bows, "Truefire");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Long_bows, "Winged");
            hPropertyToMagicPrefix.Add(eProperty.Skill_RecurvedBow, "Hawk");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Composite, "Dragon");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Thrown_Weapons, "Catapult");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Archery, "Sniper's");

            hPropertyToMagicPrefix.Add(eProperty.Skill_Matter, "Earthsplitter");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Body, "Soul Crusher");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Spirit, "Spiritbound");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Mind, "Dominating");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Earth, "Earthborn");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Cold, "Iceborn");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Fire, "Flameborn");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Wind, "Airy");
            hPropertyToMagicPrefix.Add(eProperty.Skill_DeathSight, "Minionbound");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Death_Servant, "Death Binder");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Pain_working, "Painbound");
            hPropertyToMagicPrefix.Add(eProperty.Skill_SoulRending, "Soul Taker");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Smiting, "Earthshaker");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Enhancement, "Fervent");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Rejuvenation, "Rejuvenating");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Light, "Lightbender");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Void, "Voidbender");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Mana, "Starbinder");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Enchantments, "Chanter");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Mentalism, "Mindbinder");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Regrowth, "Forestbound");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Nurture, "Plantbound");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Nature, "Animalbound");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Creeping, "Withering");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Arboreal, "Arbor Defender");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Verdant, "Vale Defender");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Darkness, "Shadowbender");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Suppression, "Spiritbinder");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Runecarving, "Runebender");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Stormcalling, "Stormcaller");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Summoning, "Soulbinder");
            hPropertyToMagicPrefix.Add(eProperty.Skill_BoneArmy, "Blighted");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Mending, "Bodymender");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Augmentation, "Empowering");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Subterranean, "Ancestral");

            hPropertyToMagicPrefix.Add(eProperty.Skill_Stealth, "Shadowwalker");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Envenom, "Venomous");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Instruments, "Melodic");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Music, "Resonant");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Battlesongs, "Motivating");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Parry, "Bladeblocker");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Shields, "Protector's");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Valor, "Courageous");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Pacification, "Pacifying");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Savagery, "Savage");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Nightshade, "Nightshade");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Pathfinding, "Trail");
            hPropertyToMagicPrefix.Add(eProperty.Skill_BeastCraft, "Lifebender");
            hPropertyToMagicPrefix.Add(eProperty.Skill_OdinsWill, "Ardent");

            // Extension Classes (Heretic, Vampiir, Bainshee, Warlock)
            hPropertyToMagicPrefix.Add(eProperty.Skill_VampiiricEmbrace, "Deathly");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Dementia, "Feverish");
            hPropertyToMagicPrefix.Add(eProperty.Skill_ShadowMastery, "Ominous");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Tormentshaper, "Tormentbound");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Wraithsight, "Wraithbound");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Void_Acolyte, "Void Binder");
            hPropertyToMagicPrefix.Add(eProperty.Skill_EtherealShriek, "Shrill");
            hPropertyToMagicPrefix.Add(eProperty.Skill_PhantasmalWail, "Keening");
            hPropertyToMagicPrefix.Add(eProperty.Skill_SpectralForce, "Uncanny");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Cursing, "Infernal");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Hexing, "Bedeviled");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Witchcraft, "Diabolic");

            // Mauler (Kept intentional empty string mappings per original request)
            hPropertyToMagicPrefix.Add(eProperty.Skill_Aura_Manipulation, "Aural");
            hPropertyToMagicPrefix.Add(eProperty.Skill_FistWraps, "Brawling");
            hPropertyToMagicPrefix.Add(eProperty.Skill_MaulerStaff, "Mauler's");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Magnetism, "Magnetic");
            hPropertyToMagicPrefix.Add(eProperty.Skill_Power_Strikes, "Striking");

            // Melee modifiers
            hPropertyToMagicPrefix.Add(eProperty.ArmorFactor, "Hardened");
            hPropertyToMagicPrefix.Add(eProperty.ArmorAbsorption, "Impenetrable");
            hPropertyToMagicPrefix.Add(eProperty.MeleeSpeed, "Swift");
            hPropertyToMagicPrefix.Add(eProperty.MeleeDamage, "Devastating");
            hPropertyToMagicPrefix.Add(eProperty.WeaponSkill, "Masterful");
            hPropertyToMagicPrefix.Add(eProperty.CriticalMeleeHitChance, "Fatal");
            hPropertyToMagicPrefix.Add(eProperty.FumbleChance, "Unerring");
            hPropertyToMagicPrefix.Add(eProperty.ToHitBonus, "Accurate");
            hPropertyToMagicPrefix.Add(eProperty.CounterAttack, "Vengeful");
            hPropertyToMagicPrefix.Add(eProperty.BladeturnReinforcement, "Phalanx");
            hPropertyToMagicPrefix.Add(eProperty.DefensiveBonus, "Guarding");
            hPropertyToMagicPrefix.Add(eProperty.ReactionaryStyleDamage, "Retaliator's");
            hPropertyToMagicPrefix.Add(eProperty.StyleCostReduction, "Tireless");

            // Ranged modifiers
            hPropertyToMagicPrefix.Add(eProperty.ArcheryRange, "Far-Reaching");
            hPropertyToMagicPrefix.Add(eProperty.RangedDamage, "Piercing");
            hPropertyToMagicPrefix.Add(eProperty.CriticalArcheryHitChance, "Sniper's");

            // Magic modifiers
            hPropertyToMagicPrefix.Add(eProperty.SpellRange, "Projecting");
            hPropertyToMagicPrefix.Add(eProperty.SpellLevel, "Transcendent");
            hPropertyToMagicPrefix.Add(eProperty.SpellPowerCost, "Efficient");
            hPropertyToMagicPrefix.Add(eProperty.SpellFumbleChance, "Flawless");
            hPropertyToMagicPrefix.Add(eProperty.CriticalSpellHitChance, "Cataclysmic");

            // Defensive Checks
            hPropertyToMagicPrefix.Add(eProperty.EvadeChance, "Elusive");
            hPropertyToMagicPrefix.Add(eProperty.BlockChance, "Deflecting");
            hPropertyToMagicPrefix.Add(eProperty.ParryChance, "Parrying");

            // Regeneration & Pools
            hPropertyToMagicPrefix.Add(eProperty.HealthRegenerationRate, "Troll-blood");
            hPropertyToMagicPrefix.Add(eProperty.PowerRegenerationRate, "Invigorating");
            hPropertyToMagicPrefix.Add(eProperty.EnduranceRegenerationRate, "Relentless");
            hPropertyToMagicPrefix.Add(eProperty.MaxConcentration, "Focused");
            hPropertyToMagicPrefix.Add(eProperty.FatigueConsumption, "Enduring");

            // Crowd Control Handling
            hPropertyToMagicPrefix.Add(eProperty.MesmerizeDuration, "Awakening");
            hPropertyToMagicPrefix.Add(eProperty.StunDuration, "Unshaken");
            hPropertyToMagicPrefix.Add(eProperty.SpeedDecreaseDuration, "Unfettered");
            hPropertyToMagicPrefix.Add(eProperty.NegativeReduction, "Cleansing");

            // Utility
            hPropertyToMagicPrefix.Add(eProperty.MaxSpeed, "Fleet");
            hPropertyToMagicPrefix.Add(eProperty.WaterSpeed, "Amphibious");
            hPropertyToMagicPrefix.Add(eProperty.MissHit, "Sure-Strike");
            hPropertyToMagicPrefix.Add(eProperty.LivingEffectiveLevel, "Exalted");
            hPropertyToMagicPrefix.Add(eProperty.LivingEffectiveness, "Apex");
            hPropertyToMagicPrefix.Add(eProperty.PieceAblative, "Ablative");

            // TOA STAT MULTIPLIERS
            hPropertyToMagicPrefix.Add(eProperty.ArcherySpeed, "Volleying");
            hPropertyToMagicPrefix.Add(eProperty.ArrowRecovery, "Scavenger's");
            hPropertyToMagicPrefix.Add(eProperty.BuffEffectiveness, "Augmenting");
            hPropertyToMagicPrefix.Add(eProperty.CastingSpeed, "Alacritous");
            hPropertyToMagicPrefix.Add(eProperty.DeathExpLoss, "Redeeming");
            hPropertyToMagicPrefix.Add(eProperty.DebuffEffectivness, "Enfeebling");
            hPropertyToMagicPrefix.Add(eProperty.Fatigue, "Vigorous");
            hPropertyToMagicPrefix.Add(eProperty.HealingEffectiveness, "Restorative");
            hPropertyToMagicPrefix.Add(eProperty.PowerPool, "Expansive");
            hPropertyToMagicPrefix.Add(eProperty.ResistPierce, "Penetrating");
            hPropertyToMagicPrefix.Add(eProperty.SpellDamage, "Ruinous");
            hPropertyToMagicPrefix.Add(eProperty.SpellDuration, "Lingering");
            hPropertyToMagicPrefix.Add(eProperty.StyleDamage, "Vicious");
            hPropertyToMagicPrefix.Add(eProperty.MythicalTension, "Unleashed");

            // TOA CAPS & RESIST OVERCAPS
            hPropertyToMagicPrefix.Add(eProperty.BodyResCapBonus, "Corporeal");
            hPropertyToMagicPrefix.Add(eProperty.ColdResCapBonus, "Frost-warded");
            hPropertyToMagicPrefix.Add(eProperty.CrushResCapBonus, "Unshatterable");
            hPropertyToMagicPrefix.Add(eProperty.EnergyResCapBonus, "Grounded");
            hPropertyToMagicPrefix.Add(eProperty.HeatResCapBonus, "Fire-forged");
            hPropertyToMagicPrefix.Add(eProperty.MatterResCapBonus, "Immutable");
            hPropertyToMagicPrefix.Add(eProperty.SlashResCapBonus, "Unseverable");
            hPropertyToMagicPrefix.Add(eProperty.SpiritResCapBonus, "Inviolate");
            hPropertyToMagicPrefix.Add(eProperty.ThrustResCapBonus, "Unpierceable");
            hPropertyToMagicPrefix.Add(eProperty.StrCapBonus, "Mighty");
            hPropertyToMagicPrefix.Add(eProperty.DexCapBonus, "Adroit");
            hPropertyToMagicPrefix.Add(eProperty.ConCapBonus, "Fortifying");
            hPropertyToMagicPrefix.Add(eProperty.QuiCapBonus, "Speedy");
            hPropertyToMagicPrefix.Add(eProperty.IntCapBonus, "Insightful");
            hPropertyToMagicPrefix.Add(eProperty.PieCapBonus, "Willful");
            hPropertyToMagicPrefix.Add(eProperty.EmpCapBonus, "Attuned");
            hPropertyToMagicPrefix.Add(eProperty.ChaCapBonus, "Grandiose");
            hPropertyToMagicPrefix.Add(eProperty.AcuCapBonus, "Brilliant");
            hPropertyToMagicPrefix.Add(eProperty.MaxHealthCapBonus, "Sturdy");
            hPropertyToMagicPrefix.Add(eProperty.PowerPoolCapBonus, "Expansive");

            // MYTHICAL, CATACOMBS, AND SPECIAL BONUSES
            hPropertyToMagicPrefix.Add(eProperty.DPS, "Slaughtering");
            hPropertyToMagicPrefix.Add(eProperty.ExtraHP, "Colossal");
            hPropertyToMagicPrefix.Add(eProperty.MagicAbsorption, "Nullifying");
            hPropertyToMagicPrefix.Add(eProperty.StyleAbsorb, "Glancing");
            hPropertyToMagicPrefix.Add(eProperty.Conversion, "Transmuting");
            hPropertyToMagicPrefix.Add(eProperty.ArcaneSyphon, "Siphoning");
            hPropertyToMagicPrefix.Add(eProperty.DotDurationDecrease, "Purging");
            hPropertyToMagicPrefix.Add(eProperty.MythicalDebuffResistChance, "Untouchable");

            hPropertyToMagicPrefix.Add(eProperty.CriticalHealHitChance, "Miraculous");
            hPropertyToMagicPrefix.Add(eProperty.CriticalDotHitChance, "Agonizing");
            hPropertyToMagicPrefix.Add(eProperty.OffhandDamageAndChanceBonus, "Ambidextrous");
            hPropertyToMagicPrefix.Add(eProperty.OffhandDamageBonus, "Twin-Striking");
            hPropertyToMagicPrefix.Add(eProperty.OffhandChanceBonus, "Flurrying");
            hPropertyToMagicPrefix.Add(eProperty.DotDamageBonus, "Corrosive");
            hPropertyToMagicPrefix.Add(eProperty.TensionConservationBonus, "Composed");

            hPropertyToMagicPrefix.Add(eProperty.MythicalSafeFall, "Weightless");
            hPropertyToMagicPrefix.Add(eProperty.MythicalDiscumbering, "Unburdened");
            hPropertyToMagicPrefix.Add(eProperty.MythicalCrowdDuration, "Oppressive");
            hPropertyToMagicPrefix.Add(eProperty.MythicalOmniRegen, "Everlasting");
            hPropertyToMagicPrefix.Add(eProperty.SpellShieldChance, "Aegis");
            hPropertyToMagicPrefix.Add(eProperty.MythicalSpellReflect, "Mirroring");

            hPropertyToMagicPrefix.Add(eProperty.MythicalStrCapBonus, "Titan's");
            hPropertyToMagicPrefix.Add(eProperty.MythicalDexCapBonus, "Serpent's");
            hPropertyToMagicPrefix.Add(eProperty.MythicalConCapBonus, "Leviathan's");
            hPropertyToMagicPrefix.Add(eProperty.MythicalQuiCapBonus, "Zephyr's");
            hPropertyToMagicPrefix.Add(eProperty.MythicalIntCapBonus, "Omniscient");
            hPropertyToMagicPrefix.Add(eProperty.MythicalPieCapBonus, "Divine");
            hPropertyToMagicPrefix.Add(eProperty.MythicalEmpCapBonus, "Harmonic");
            hPropertyToMagicPrefix.Add(eProperty.MythicalChaCapBonus, "Sovereign's");
            hPropertyToMagicPrefix.Add(eProperty.MythicalAcuCapBonus, "Ascendant");
        }

        #endregion

        private static void CacheProcSpells()
        {
            //LT spells
            DBSpell Level5Lifetap = DOLDB<DBSpell>.SelectObject(DB.Column("Spell_ID").IsEqualTo(8010));
            DBSpell Level10Lifetap = DOLDB<DBSpell>.SelectObject(DB.Column("Spell_ID").IsEqualTo(8011));
            DBSpell Level15Lifetap = DOLDB<DBSpell>.SelectObject(DB.Column("Spell_ID").IsEqualTo(8012));

            ProcSpells.Add(8010, new Spell(Level5Lifetap, 0));
            ProcSpells.Add(8011, new Spell(Level10Lifetap, 0));
            ProcSpells.Add(8012, new Spell(Level15Lifetap, 0));

        }
    }
}
