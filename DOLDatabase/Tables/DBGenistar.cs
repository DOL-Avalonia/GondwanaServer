using System;
using DOL.Database.Attributes;

namespace DOL.Database
{
    [DataTable(TableName = "genistar")]
    public class DBGenistar : DataObject
    {
        #region Core Identity & Stasis Gate
        private string m_genistarID;
        private string m_ownerID;
        private int m_houseNumber;
        private int m_placeholderKey;
        private int m_state; // 0=Incubating, 1=Egg, 2=Hatched, 3=Dead, 4=Recovering (Dormant), 5=Archived
        private int m_remainingIncubationTime; // Stored natively in Milliseconds (Default: 3,600,000)
        private long m_timerEnd; // Strictly reserved for absolute-time corpse rotting deadlines
        private long m_genistarExperience;
        private string m_customName;
        private string m_lensHistory;
        private byte m_visibleWeaponSlot;
        private string m_equipmentTemplateID;
        #endregion

        #region DAoC Baseline DNA (The Genotype)
        private int m_baseTemplateID;
        private string m_name;
        private int m_bodyType;
        private ushort m_currentModel;
        private int m_size;
        private int m_eruditionAtBirth;
        private bool m_isGhost;
        private bool m_isStealthed;

        private short m_baseStrength, m_baseConstitution, m_baseDexterity, m_baseQuickness;
        private short m_baseIntelligence, m_baseEmpathy, m_basePiety, m_baseCharisma;
        private int m_baseAF, m_baseDPS;
        private short m_baseMaxSpeed;
        #endregion

        #region Live DAoC Mapping (The Phenotype)
        private int m_strength, m_constitution, m_dexterity, m_quickness;
        private int m_intelligence, m_piety, m_empathy, m_charisma;

        private int m_weaponDPS, m_weaponSpd, m_armorFactor, m_armorAbsorb;
        private int m_evadeChance, m_blockChance, m_parryChance, m_leftHandSwingChance;
        #endregion

        #region Live Resists & Boss Overrides
        private int m_resistBody, m_resistCold, m_resistCrush, m_resistEnergy, m_resistHeat;
        private int m_resistMatter, m_resistNatural, m_resistSlash, m_resistSpirit, m_resistThrust;

        private int m_spellmagicABS, m_dotABS, m_meleeABS, m_maxHealth, m_effectivenessMod, m_ccResist, m_debuffResist, m_castRange;
        private double m_ablativeShield;
        private int m_erodibleAblative;

        private string m_spells;
        private string m_styles;
        private string m_abilities;
        private string m_damageTypeConvert;
        private int m_procSpellChance;
        private string m_procSpellID;
        private int m_currentTension;
        private int m_maxTension;
        private string m_adrenalineSpellID;
        private int m_counterAttackChance;
        private string m_counterAttackStyle;
        #endregion

        #region Accumulators (10 Counts = +1 True Baked Point)
        private int m_countStrength, m_countConstitution, m_countDexterity, m_countQuickness;
        private int m_countIntelligence, m_countPiety, m_countEmpathy, m_countCharisma;
        private int m_countMaxHealth, m_countMaxTension;

        private int m_countWeaponDPS, m_countWeaponSpd, m_countArmorFactor, m_countArmorAbsorb;
        private int m_countEvadeChance, m_countBlockChance, m_countParryChance;
        private int m_countLeftHandSwingChance, m_countCounterAttackChance, m_countProcSpellChance;

        private int m_countResistBody, m_countResistCold, m_countResistCrush, m_countResistEnergy, m_countResistHeat;
        private int m_countResistMatter, m_countResistNatural, m_countResistSlash, m_countResistSpirit, m_countResistThrust;

        private int m_countSpellmagicABS, m_countDotABS, m_countMeleeABS, m_countEffectivenessMod, m_countAblativeShield, m_countCCResist, m_countDebuffResist, m_countCastRange;

        // Neural Skill Unlocks
        private int m_countEvolveCold, m_countEvolveSpirit, m_countEvolveMatter, m_countEvolveFire, m_countEvolveEnergy, m_countEvolveBody;
        private int m_countEvolveHeal, m_countEvolveDot, m_countEvolveDisease, m_countEvolveCC, m_countEvolveSpecial;
        private int m_countEvolveHammer, m_countEvolveSword, m_countEvolveThrust, m_countEvolve2Hand, m_countEvolveSpear;
        private int m_countEvolveStaves, m_countEvolveHand2Hand, m_countEvolveFists;
        #endregion

        public DBGenistar()
        {
            m_customName = "";
            m_lensHistory = "";
            m_name = "";
            m_spells = "";
            m_styles = "";
            m_abilities = "";
            m_damageTypeConvert = "";
            m_procSpellID = "";
            m_counterAttackStyle = "";
            m_adrenalineSpellID = "";
            m_equipmentTemplateID = "";
            m_visibleWeaponSlot = 0;
            m_remainingIncubationTime = 3600000; // 1 Hour Native Millisecond Stasis Gate
            m_size = 50;
            m_erodibleAblative = 1;
        }

        #region Persistent Data Elements
        [DataElement(AllowDbNull = false, Index = true)] public string GenistarID { get { return m_genistarID; } set { Dirty = true; m_genistarID = value; } }
        [DataElement(AllowDbNull = false, Index = true)] public string OwnerID { get { return m_ownerID; } set { Dirty = true; m_ownerID = value; } }
        [DataElement(AllowDbNull = false)] public int HouseNumber { get { return m_houseNumber; } set { Dirty = true; m_houseNumber = value; } }
        [DataElement(AllowDbNull = false)] public int PlaceholderKey { get { return m_placeholderKey; } set { Dirty = true; m_placeholderKey = value; } }
        [DataElement(AllowDbNull = false)] public int State { get { return m_state; } set { Dirty = true; m_state = value; } }
        [DataElement(AllowDbNull = false)] public int RemainingIncubationTime { get { return m_remainingIncubationTime; } set { Dirty = true; m_remainingIncubationTime = value; } }
        [DataElement(AllowDbNull = false)] public long TimerEnd { get { return m_timerEnd; } set { Dirty = true; m_timerEnd = value; } }
        [DataElement(AllowDbNull = false)] public long GenistarExperience { get { return m_genistarExperience; } set { Dirty = true; m_genistarExperience = value; } }
        [DataElement(AllowDbNull = true, Varchar = 100)] public string CustomName { get { return m_customName; } set { Dirty = true; m_customName = value; } }
        [DataElement(AllowDbNull = true)] public string LensHistory { get { return m_lensHistory; } set { Dirty = true; m_lensHistory = value; } }
        [DataElement(AllowDbNull = false)] public byte VisibleWeaponSlot { get { return m_visibleWeaponSlot; } set { Dirty = true; m_visibleWeaponSlot = value; } }
        [DataElement(AllowDbNull = true, Varchar = 100)] public string EquipmentTemplateID { get { return m_equipmentTemplateID; } set { Dirty = true; m_equipmentTemplateID = value; } }

        [DataElement(AllowDbNull = false)] public int BaseTemplateID { get { return m_baseTemplateID; } set { Dirty = true; m_baseTemplateID = value; } }
        [DataElement(AllowDbNull = true, Varchar = 100)] public string Name { get { return m_name; } set { Dirty = true; m_name = value; } }
        [DataElement(AllowDbNull = false)] public int BodyType { get { return m_bodyType; } set { Dirty = true; m_bodyType = value; } }
        [DataElement(AllowDbNull = false)] public ushort CurrentModel { get { return m_currentModel; } set { Dirty = true; m_currentModel = value; } }
        [DataElement(AllowDbNull = false)] public int Size { get { return m_size; } set { Dirty = true; m_size = value; } }
        [DataElement(AllowDbNull = false)] public int EruditionAtBirth { get { return m_eruditionAtBirth; } set { Dirty = true; m_eruditionAtBirth = value; } }
        [DataElement(AllowDbNull = false)] public bool IsGhost { get { return m_isGhost; } set { Dirty = true; m_isGhost = value; } }
        [DataElement(AllowDbNull = false)] public bool IsStealthed { get { return m_isStealthed; } set { Dirty = true; m_isStealthed = value; } }

        // Genotype Baselines
        [DataElement(AllowDbNull = false)] public short BaseStrength { get { return m_baseStrength; } set { Dirty = true; m_baseStrength = value; } }
        [DataElement(AllowDbNull = false)] public short BaseConstitution { get { return m_baseConstitution; } set { Dirty = true; m_baseConstitution = value; } }
        [DataElement(AllowDbNull = false)] public short BaseDexterity { get { return m_baseDexterity; } set { Dirty = true; m_baseDexterity = value; } }
        [DataElement(AllowDbNull = false)] public short BaseQuickness { get { return m_baseQuickness; } set { Dirty = true; m_baseQuickness = value; } }
        [DataElement(AllowDbNull = false)] public short BaseIntelligence { get { return m_baseIntelligence; } set { Dirty = true; m_baseIntelligence = value; } }
        [DataElement(AllowDbNull = false)] public short BaseEmpathy { get { return m_baseEmpathy; } set { Dirty = true; m_baseEmpathy = value; } }
        [DataElement(AllowDbNull = false)] public short BasePiety { get { return m_basePiety; } set { Dirty = true; m_basePiety = value; } }
        [DataElement(AllowDbNull = false)] public short BaseCharisma { get { return m_baseCharisma; } set { Dirty = true; m_baseCharisma = value; } }
        [DataElement(AllowDbNull = false)] public int BaseAF { get { return m_baseAF; } set { Dirty = true; m_baseAF = value; } }
        [DataElement(AllowDbNull = false)] public int BaseDPS { get { return m_baseDPS; } set { Dirty = true; m_baseDPS = value; } }
        [DataElement(AllowDbNull = false)] public short BaseMaxSpeed { get { return m_baseMaxSpeed; } set { Dirty = true; m_baseMaxSpeed = value; } }

        // Phenotype Live Outputs
        [DataElement(AllowDbNull = false)] public int Strength { get { return m_strength; } set { Dirty = true; m_strength = value; } }
        [DataElement(AllowDbNull = false)] public int Constitution { get { return m_constitution; } set { Dirty = true; m_constitution = value; } }
        [DataElement(AllowDbNull = false)] public int Dexterity { get { return m_dexterity; } set { Dirty = true; m_dexterity = value; } }
        [DataElement(AllowDbNull = false)] public int Quickness { get { return m_quickness; } set { Dirty = true; m_quickness = value; } }
        [DataElement(AllowDbNull = false)] public int Intelligence { get { return m_intelligence; } set { Dirty = true; m_intelligence = value; } }
        [DataElement(AllowDbNull = false)] public int Piety { get { return m_piety; } set { Dirty = true; m_piety = value; } }
        [DataElement(AllowDbNull = false)] public int Empathy { get { return m_empathy; } set { Dirty = true; m_empathy = value; } }
        [DataElement(AllowDbNull = false)] public int Charisma { get { return m_charisma; } set { Dirty = true; m_charisma = value; } }

        [DataElement(AllowDbNull = false)] public int WeaponDPS { get { return m_weaponDPS; } set { Dirty = true; m_weaponDPS = value; } }
        [DataElement(AllowDbNull = false)] public int WeaponSpd { get { return m_weaponSpd; } set { Dirty = true; m_weaponSpd = value; } }
        [DataElement(AllowDbNull = false)] public int ArmorFactor { get { return m_armorFactor; } set { Dirty = true; m_armorFactor = value; } }
        [DataElement(AllowDbNull = false)] public int ArmorAbsorb { get { return m_armorAbsorb; } set { Dirty = true; m_armorAbsorb = value; } }
        [DataElement(AllowDbNull = false)] public int EvadeChance { get { return m_evadeChance; } set { Dirty = true; m_evadeChance = value; } }
        [DataElement(AllowDbNull = false)] public int BlockChance { get { return m_blockChance; } set { Dirty = true; m_blockChance = value; } }
        [DataElement(AllowDbNull = false)] public int ParryChance { get { return m_parryChance; } set { Dirty = true; m_parryChance = value; } }
        [DataElement(AllowDbNull = false)] public int LeftHandSwingChance { get { return m_leftHandSwingChance; } set { Dirty = true; m_leftHandSwingChance = value; } }

        [DataElement(AllowDbNull = false)] public int ResistBody { get { return m_resistBody; } set { Dirty = true; m_resistBody = value; } }
        [DataElement(AllowDbNull = false)] public int ResistCold { get { return m_resistCold; } set { Dirty = true; m_resistCold = value; } }
        [DataElement(AllowDbNull = false)] public int ResistCrush { get { return m_resistCrush; } set { Dirty = true; m_resistCrush = value; } }
        [DataElement(AllowDbNull = false)] public int ResistEnergy { get { return m_resistEnergy; } set { Dirty = true; m_resistEnergy = value; } }
        [DataElement(AllowDbNull = false)] public int ResistHeat { get { return m_resistHeat; } set { Dirty = true; m_resistHeat = value; } }
        [DataElement(AllowDbNull = false)] public int ResistMatter { get { return m_resistMatter; } set { Dirty = true; m_resistMatter = value; } }
        [DataElement(AllowDbNull = false)] public int ResistNatural { get { return m_resistNatural; } set { Dirty = true; m_resistNatural = value; } }
        [DataElement(AllowDbNull = false)] public int ResistSlash { get { return m_resistSlash; } set { Dirty = true; m_resistSlash = value; } }
        [DataElement(AllowDbNull = false)] public int ResistSpirit { get { return m_resistSpirit; } set { Dirty = true; m_resistSpirit = value; } }
        [DataElement(AllowDbNull = false)] public int ResistThrust { get { return m_resistThrust; } set { Dirty = true; m_resistThrust = value; } }

        [DataElement(AllowDbNull = false)] public int SpellmagicABS { get { return m_spellmagicABS; } set { Dirty = true; m_spellmagicABS = value; } }
        [DataElement(AllowDbNull = false)] public int DotABS { get { return m_dotABS; } set { Dirty = true; m_dotABS = value; } }
        [DataElement(AllowDbNull = false)] public int MeleeABS { get { return m_meleeABS; } set { Dirty = true; m_meleeABS = value; } }
        [DataElement(AllowDbNull = false)] public int MaxHealth { get { return m_maxHealth; } set { Dirty = true; m_maxHealth = value; } }
        [DataElement(AllowDbNull = false)] public int EffectivenessMod { get { return m_effectivenessMod; } set { Dirty = true; m_effectivenessMod = value; } }
        [DataElement(AllowDbNull = false)] public double AblativeShield { get { return m_ablativeShield; } set { Dirty = true; m_ablativeShield = value; } }
        [DataElement(AllowDbNull = false)] public int ErodibleAblative { get { return m_erodibleAblative; } set { Dirty = true; m_erodibleAblative = value; } }
        [DataElement(AllowDbNull = false)] public int CCResist { get { return m_ccResist; } set { Dirty = true; m_ccResist = value; } }
        [DataElement(AllowDbNull = false)] public int DebuffResist { get { return m_debuffResist; } set { Dirty = true; m_debuffResist = value; } }
        [DataElement(AllowDbNull = false)] public int CastRange { get { return m_castRange; } set { Dirty = true; m_castRange = value; } }

        [DataElement(AllowDbNull = true)] public string Spells { get { return m_spells; } set { Dirty = true; m_spells = value; } }
        [DataElement(AllowDbNull = true)] public string Styles { get { return m_styles; } set { Dirty = true; m_styles = value; } }
        [DataElement(AllowDbNull = true)] public string Abilities { get { return m_abilities; } set { Dirty = true; m_abilities = value; } }
        [DataElement(AllowDbNull = true)] public string DamageTypeConvert { get { return m_damageTypeConvert; } set { Dirty = true; m_damageTypeConvert = value; } }
        [DataElement(AllowDbNull = true)] public int ProcSpellChance { get { return m_procSpellChance; } set { Dirty = true; m_procSpellChance = value; } }
        [DataElement(AllowDbNull = true)] public string ProcSpellID { get { return m_procSpellID; } set { Dirty = true; m_procSpellID = value; } }
        [DataElement(AllowDbNull = false)] public int CurrentTension { get { return m_currentTension; } set { Dirty = true; m_currentTension = value; } }
        [DataElement(AllowDbNull = false)] public int MaxTension { get { return m_maxTension; } set { Dirty = true; m_maxTension = value; } }
        [DataElement(AllowDbNull = false)] public string AdrenalineSpellID { get { return m_adrenalineSpellID; } set { Dirty = true; m_adrenalineSpellID = value; } }
        [DataElement(AllowDbNull = false)] public int CounterAttackChance { get { return m_counterAttackChance; } set { Dirty = true; m_counterAttackChance = value; } }
        [DataElement(AllowDbNull = true)] public string CounterAttackStyle { get { return m_counterAttackStyle; } set { Dirty = true; m_counterAttackStyle = value; } }

        // Ledger Accumulators
        [DataElement(AllowDbNull = false)] public int CountStrength { get { return m_countStrength; } set { Dirty = true; m_countStrength = value; } }
        [DataElement(AllowDbNull = false)] public int CountConstitution { get { return m_countConstitution; } set { Dirty = true; m_countConstitution = value; } }
        [DataElement(AllowDbNull = false)] public int CountDexterity { get { return m_countDexterity; } set { Dirty = true; m_countDexterity = value; } }
        [DataElement(AllowDbNull = false)] public int CountQuickness { get { return m_countQuickness; } set { Dirty = true; m_countQuickness = value; } }
        [DataElement(AllowDbNull = false)] public int CountIntelligence { get { return m_countIntelligence; } set { Dirty = true; m_countIntelligence = value; } }
        [DataElement(AllowDbNull = false)] public int CountPiety { get { return m_countPiety; } set { Dirty = true; m_countPiety = value; } }
        [DataElement(AllowDbNull = false)] public int CountEmpathy { get { return m_countEmpathy; } set { Dirty = true; m_countEmpathy = value; } }
        [DataElement(AllowDbNull = false)] public int CountCharisma { get { return m_countCharisma; } set { Dirty = true; m_countCharisma = value; } }

        [DataElement(AllowDbNull = false)] public int CountWeaponDPS { get { return m_countWeaponDPS; } set { Dirty = true; m_countWeaponDPS = value; } }
        [DataElement(AllowDbNull = false)] public int CountWeaponSpd { get { return m_countWeaponSpd; } set { Dirty = true; m_countWeaponSpd = value; } }
        [DataElement(AllowDbNull = false)] public int CountArmorFactor { get { return m_countArmorFactor; } set { Dirty = true; m_countArmorFactor = value; } }
        [DataElement(AllowDbNull = false)] public int CountArmorAbsorb { get { return m_countArmorAbsorb; } set { Dirty = true; m_countArmorAbsorb = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvadeChance { get { return m_countEvadeChance; } set { Dirty = true; m_countEvadeChance = value; } }
        [DataElement(AllowDbNull = false)] public int CountBlockChance { get { return m_countBlockChance; } set { Dirty = true; m_countBlockChance = value; } }
        [DataElement(AllowDbNull = false)] public int CountParryChance { get { return m_countParryChance; } set { Dirty = true; m_countParryChance = value; } }
        [DataElement(AllowDbNull = false)] public int CountLeftHandSwingChance { get { return m_countLeftHandSwingChance; } set { Dirty = true; m_countLeftHandSwingChance = value; } }
        [DataElement(AllowDbNull = true)] public int CountProcSpellChance { get { return m_countProcSpellChance; } set { Dirty = true; m_countProcSpellChance = value; } }
        [DataElement(AllowDbNull = false)] public int CountCounterAttackChance { get { return m_countCounterAttackChance; } set { Dirty = true; m_countCounterAttackChance = value; } }
        [DataElement(AllowDbNull = false)] public int CountMaxTension { get { return m_countMaxTension; } set { Dirty = true; m_countMaxTension = value; } }

        [DataElement(AllowDbNull = false)] public int CountResistBody { get { return m_countResistBody; } set { Dirty = true; m_countResistBody = value; } }
        [DataElement(AllowDbNull = false)] public int CountResistCold { get { return m_countResistCold; } set { Dirty = true; m_countResistCold = value; } }
        [DataElement(AllowDbNull = false)] public int CountResistCrush { get { return m_countResistCrush; } set { Dirty = true; m_countResistCrush = value; } }
        [DataElement(AllowDbNull = false)] public int CountResistEnergy { get { return m_countResistEnergy; } set { Dirty = true; m_countResistEnergy = value; } }
        [DataElement(AllowDbNull = false)] public int CountResistHeat { get { return m_countResistHeat; } set { Dirty = true; m_countResistHeat = value; } }
        [DataElement(AllowDbNull = false)] public int CountResistMatter { get { return m_countResistMatter; } set { Dirty = true; m_countResistMatter = value; } }
        [DataElement(AllowDbNull = false)] public int CountResistNatural { get { return m_countResistNatural; } set { Dirty = true; m_countResistNatural = value; } }
        [DataElement(AllowDbNull = false)] public int CountResistSlash { get { return m_countResistSlash; } set { Dirty = true; m_countResistSlash = value; } }
        [DataElement(AllowDbNull = false)] public int CountResistSpirit { get { return m_countResistSpirit; } set { Dirty = true; m_countResistSpirit = value; } }
        [DataElement(AllowDbNull = false)] public int CountResistThrust { get { return m_countResistThrust; } set { Dirty = true; m_countResistThrust = value; } }

        [DataElement(AllowDbNull = false)] public int CountSpellmagicABS { get { return m_countSpellmagicABS; } set { Dirty = true; m_countSpellmagicABS = value; } }
        [DataElement(AllowDbNull = false)] public int CountDotABS { get { return m_countDotABS; } set { Dirty = true; m_countDotABS = value; } }
        [DataElement(AllowDbNull = false)] public int CountMeleeABS { get { return m_countMeleeABS; } set { Dirty = true; m_countMeleeABS = value; } }
        [DataElement(AllowDbNull = false)] public int CountMaxHealth { get { return m_countMaxHealth; } set { Dirty = true; m_countMaxHealth = value; } }
        [DataElement(AllowDbNull = false)] public int CountEffectivenessMod { get { return m_countEffectivenessMod; } set { Dirty = true; m_countEffectivenessMod = value; } }
        [DataElement(AllowDbNull = false)] public int CountAblativeShield { get { return m_countAblativeShield; } set { Dirty = true; m_countAblativeShield = value; } }
        [DataElement(AllowDbNull = false)] public int CountCCResist { get { return m_countCCResist; } set { Dirty = true; m_countCCResist = value; } }
        [DataElement(AllowDbNull = false)] public int CountDebuffResist { get { return m_countDebuffResist; } set { Dirty = true; m_countDebuffResist = value; } }
        [DataElement(AllowDbNull = false)] public int CountCastRange { get { return m_countCastRange; } set { Dirty = true; m_countCastRange = value; } }

        [DataElement(AllowDbNull = false)] public int CountEvolveCold { get { return m_countEvolveCold; } set { Dirty = true; m_countEvolveCold = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveSpirit { get { return m_countEvolveSpirit; } set { Dirty = true; m_countEvolveSpirit = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveMatter { get { return m_countEvolveMatter; } set { Dirty = true; m_countEvolveMatter = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveFire { get { return m_countEvolveFire; } set { Dirty = true; m_countEvolveFire = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveEnergy { get { return m_countEvolveEnergy; } set { Dirty = true; m_countEvolveEnergy = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveBody { get { return m_countEvolveBody; } set { Dirty = true; m_countEvolveBody = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveHeal { get { return m_countEvolveHeal; } set { Dirty = true; m_countEvolveHeal = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveDot { get { return m_countEvolveDot; } set { Dirty = true; m_countEvolveDot = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveDisease { get { return m_countEvolveDisease; } set { Dirty = true; m_countEvolveDisease = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveCC { get { return m_countEvolveCC; } set { Dirty = true; m_countEvolveCC = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveSpecial { get { return m_countEvolveSpecial; } set { Dirty = true; m_countEvolveSpecial = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveHammer { get { return m_countEvolveHammer; } set { Dirty = true; m_countEvolveHammer = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveSword { get { return m_countEvolveSword; } set { Dirty = true; m_countEvolveSword = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveThrust { get { return m_countEvolveThrust; } set { Dirty = true; m_countEvolveThrust = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolve2Hand { get { return m_countEvolve2Hand; } set { Dirty = true; m_countEvolve2Hand = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveSpear { get { return m_countEvolveSpear; } set { Dirty = true; m_countEvolveSpear = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveStaves { get { return m_countEvolveStaves; } set { Dirty = true; m_countEvolveStaves = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveHand2Hand { get { return m_countEvolveHand2Hand; } set { Dirty = true; m_countEvolveHand2Hand = value; } }
        [DataElement(AllowDbNull = false)] public int CountEvolveFists { get { return m_countEvolveFists; } set { Dirty = true; m_countEvolveFists = value; } }
        #endregion
    }
}