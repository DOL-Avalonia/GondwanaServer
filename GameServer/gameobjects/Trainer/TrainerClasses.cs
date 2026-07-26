using DOL.Database;

namespace DOL.GS.Trainer
{
    #region Albion Base Classes

    [NPCGuildScript("Acolyte Trainer", eRealm.Albion)]
    public class AcolyteTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Acolyte;
        public AcolyteTrainer() : base(eChampionTrainerType.Acolyte) { }
    }

    [NPCGuildScript("Rogue Trainer", eRealm.Albion)]
    public class AlbionRogueTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.AlbionRogue;
        public AlbionRogueTrainer() : base(eChampionTrainerType.AlbionRogue) { }
    }

    [NPCGuildScript("Disciple Trainer", eRealm.Albion)]
    public class DiscipleTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Disciple;
        public DiscipleTrainer() : base(eChampionTrainerType.Disciple) { }
    }

    [NPCGuildScript("Elementalist Trainer", eRealm.Albion)]
    public class ElementalistTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Elementalist;
        public ElementalistTrainer() : base(eChampionTrainerType.Elementalist) { }
    }

    [NPCGuildScript("Fighter Trainer", eRealm.Albion)]
    public class FighterTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Fighter;
        public FighterTrainer() : base(eChampionTrainerType.Fighter) { }
    }

    [NPCGuildScript("Mage Trainer", eRealm.Albion)]
    public class MageTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Mage;
        public MageTrainer() : base(eChampionTrainerType.Mage) { }
    }

    #endregion Albion Base Classes

    #region Albion Specialized Classes

    [NPCGuildScript("Armsman Trainer", eRealm.Albion)]
    public class ArmsmanTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Armsman;
    }

    [NPCGuildScript("Cabalist Trainer", eRealm.Albion)]
    public class CabalistTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Cabalist;
    }

    [NPCGuildScript("Cleric Trainer", eRealm.Albion)]
    public class ClericTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Cleric;
    }

    [NPCGuildScript("Friar Trainer", eRealm.Albion)]
    public class FriarTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Friar;
    }

    [NPCGuildScript("Heretic Trainer", eRealm.Albion)]
    public class HereticTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Heretic;
    }

    [NPCGuildScript("Infiltrator Trainer", eRealm.Albion)]
    public class InfiltratorTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Infiltrator;
    }

    [NPCGuildScript("Mercenary Trainer", eRealm.Albion)]
    public class MercenaryTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Mercenary;
    }

    [NPCGuildScript("Minstrel Trainer", eRealm.Albion)]
    public class MinstrelTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Minstrel;
    }

    [NPCGuildScript("Necromancer Trainer", eRealm.Albion)]
    public class NecromancerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Necromancer;
    }

    [NPCGuildScript("Occultist Trainer", eRealm.Albion)]
    public class OccultistTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Occultist;
    }

    [NPCGuildScript("Paladin Trainer", eRealm.Albion)]
    public class PaladinTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Paladin;
    }

    [NPCGuildScript("Reaver Trainer", eRealm.Albion)]
    public class ReaverTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Reaver;
    }

    [NPCGuildScript("Scout Trainer", eRealm.Albion)]
    public class ScoutTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Scout;
    }

    [NPCGuildScript("Sorcerer Trainer", eRealm.Albion)]
    public class SorcererTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Sorcerer;
    }

    [NPCGuildScript("Theurgist Trainer", eRealm.Albion)]
    public class TheurgistTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Theurgist;
    }

    [NPCGuildScript("Wizard Trainer", eRealm.Albion)]
    public class WizardTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Wizard;
    }

    [NPCGuildScript("Mauler Trainer", eRealm.Albion)]
    public class AlbionMaulerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.MaulerAlb;
    }

    #endregion Albion Specialized Classes

    #region Midgard Base Classes

    [NPCGuildScript("Rogue Trainer", eRealm.Midgard)]
    public class MidgardRogueTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.MidgardRogue;
        public MidgardRogueTrainer() : base(eChampionTrainerType.MidgardRogue) { }
    }

    [NPCGuildScript("Mystic Trainer", eRealm.Midgard)]
    public class MysticTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Mystic;
        public MysticTrainer() : base(eChampionTrainerType.Mystic) { }
    }

    [NPCGuildScript("Seer Trainer", eRealm.Midgard)]
    public class SeerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Seer;
        public SeerTrainer() : base(eChampionTrainerType.Seer) { }
    }

    [NPCGuildScript("Viking Trainer", eRealm.Midgard)]
    public class VikingTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Viking;
        public VikingTrainer() : base(eChampionTrainerType.Viking) { }
    }

    #endregion Midgard Base Classes

    #region Midgard Specialized Classes

    [NPCGuildScript("Berserker Trainer", eRealm.Midgard)]
    public class BerserkerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Berserker;
    }

    [NPCGuildScript("Bonedancer Trainer", eRealm.Midgard)]
    public class BonedancerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Bonedancer;
    }

    [NPCGuildScript("Healer Trainer", eRealm.Midgard)]
    public class HealerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Healer;
    }

    [NPCGuildScript("Hunter Trainer", eRealm.Midgard)]
    public class HunterTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Hunter;
    }

    [NPCGuildScript("Runemaster Trainer", eRealm.Midgard)]
    public class RunemasterTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Runemaster;
    }

    [NPCGuildScript("Savage Trainer", eRealm.Midgard)]
    public class SavageTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Savage;
    }

    [NPCGuildScript("Shadowblade Trainer", eRealm.Midgard)]
    public class ShadowbladeTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Shadowblade;
    }

    [NPCGuildScript("Shaman Trainer", eRealm.Midgard)]
    public class ShamanTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Shaman;
    }

    [NPCGuildScript("Skald Trainer", eRealm.Midgard)]
    public class SkaldTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Skald;
    }

    [NPCGuildScript("Spiritmaster Trainer", eRealm.Midgard)]
    public class SpiritmasterTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Spiritmaster;
    }

    [NPCGuildScript("Thane Trainer", eRealm.Midgard)]
    public class ThaneTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Thane;
    }

    [NPCGuildScript("Valkyrie Trainer", eRealm.Midgard)]
    public class ValkyrieTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Valkyrie;
    }

    [NPCGuildScript("Warlock Trainer", eRealm.Midgard)]
    public class WarlockTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Warlock;
    }

    [NPCGuildScript("Warrior Trainer", eRealm.Midgard)]
    public class WarriorTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Warrior;
    }

    [NPCGuildScript("Mauler Trainer", eRealm.Midgard)]
    public class MidgardMaulerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.MaulerMid;
    }

    #endregion Midgard Specialized Classes

    #region Hibernia Base Classes

    [NPCGuildScript("Forester Trainer", eRealm.Hibernia)]
    public class ForesterTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Forester;
        public ForesterTrainer() : base(eChampionTrainerType.Forester) { }
    }

    [NPCGuildScript("Guardian Trainer", eRealm.Hibernia)]
    public class GuardianTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Guardian;
        public GuardianTrainer() : base(eChampionTrainerType.Guardian) { }
    }

    [NPCGuildScript("Magician Trainer", eRealm.Hibernia)]
    public class MagicianTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Magician;
        public MagicianTrainer() : base(eChampionTrainerType.Magician) { }
    }

    [NPCGuildScript("Naturalist Trainer", eRealm.Hibernia)]
    public class NaturalistTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Naturalist;
        public NaturalistTrainer() : base(eChampionTrainerType.Naturalist) { }
    }

    [NPCGuildScript("Stalker Trainer", eRealm.Hibernia)]
    public class StalkerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Stalker;
        public StalkerTrainer() : base(eChampionTrainerType.Stalker) { }
    }

    #endregion Hibernia Base Classes

    #region Hibernia Specialized Classes

    [NPCGuildScript("Animist Trainer", eRealm.Hibernia)]
    public class AnimistTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Animist;
    }

    [NPCGuildScript("Bainshee Trainer", eRealm.Hibernia)]
    public class BainsheeTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Bainshee;
    }

    [NPCGuildScript("Bard Trainer", eRealm.Hibernia)]
    public class BardTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Bard;
    }

    [NPCGuildScript("Blademaster Trainer", eRealm.Hibernia)]
    public class BlademasterTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Blademaster;
    }

    [NPCGuildScript("Champion Trainer", eRealm.Hibernia)]
    public class ChampionTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Champion;
    }

    [NPCGuildScript("Druid Trainer", eRealm.Hibernia)]
    public class DruidTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Druid;
    }

    [NPCGuildScript("Eldritch Trainer", eRealm.Hibernia)]
    public class EldritchTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Eldritch;
    }

    [NPCGuildScript("Enchanter Trainer", eRealm.Hibernia)]
    public class EnchanterTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Enchanter;
    }

    [NPCGuildScript("Hero Trainer", eRealm.Hibernia)]
    public class HeroTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Hero;
    }

    [NPCGuildScript("Mentalist Trainer", eRealm.Hibernia)]
    public class MentalistTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Mentalist;
    }

    [NPCGuildScript("Nightshade Trainer", eRealm.Hibernia)]
    public class NightshadeTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Nightshade;
    }

    [NPCGuildScript("Ranger Trainer", eRealm.Hibernia)]
    public class RangerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Ranger;
    }

    [NPCGuildScript("Valewalker Trainer", eRealm.Hibernia)]
    public class ValewalkerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Valewalker;
    }

    [NPCGuildScript("Vampiir Trainer", eRealm.Hibernia)]
    public class VampiirTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Vampiir;
    }

    [NPCGuildScript("Warden Trainer", eRealm.Hibernia)]
    public class WardenTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.Warden;
    }

    [NPCGuildScript("Mauler Trainer", eRealm.Hibernia)]
    public class HiberniaMaulerTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass => eCharacterClass.MaulerHib;
    }

    #endregion Hibernia Specialized Classes
}