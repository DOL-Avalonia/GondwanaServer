using System;
using System.Collections.Generic;

namespace DOL.GS.Trainer
{
    public class WeaponChoice
    {
        public string TranslationKey;
        public string[] WeaponIds;

        public WeaponChoice(string translationKey, params string[] weaponIds)
        {
            TranslationKey = translationKey;
            WeaponIds = weaponIds;
        }
    }

    public class ClassTrainerConfig
    {
        public eCharacterClass ClassID;
        public string TranslationPrefix;

        // Base class practice items
        public string PracticeWeapon;
        public string PracticeShield;
        public string PracticeStaff;

        // Spec class armor progressions: [0]=Lvl 5, [1]=Lvl 10, [2]=Lvl 15, [3]=Lvl 20
        public string[] Armors;
        public string DefaultWeapon; // Given if class has no weapon selection

        // Promotion Lore & Trigger Keywords
        public HashSet<string> PromotionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string PromotionTextKey;

        public string ClassNameEN;
        public string ClassNameFR;
        public string CustomExplainKey;
        public string CustomRefuseKey;

        public Dictionary<string, WeaponChoice> WeaponChoices = new Dictionary<string, WeaponChoice>(StringComparer.OrdinalIgnoreCase);

        public ClassTrainerConfig AddChoice(string keywordEn, string keywordFr, string translationKey, params string[] weaponIds)
        {
            var choice = new WeaponChoice(translationKey, weaponIds);
            if (!string.IsNullOrEmpty(keywordEn)) WeaponChoices[keywordEn] = choice;
            if (!string.IsNullOrEmpty(keywordFr)) WeaponChoices[keywordFr] = choice;
            return this;
        }

        public ClassTrainerConfig AddPromoKeyword(string kwEn, string kwFr)
        {
            if (!string.IsNullOrEmpty(kwEn)) PromotionKeywords.Add(kwEn);
            if (!string.IsNullOrEmpty(kwFr)) PromotionKeywords.Add(kwFr);
            return this;
        }
    }

    public static class TrainerDataManager
    {
        public static readonly Dictionary<eCharacterClass, ClassTrainerConfig> Configs = new Dictionary<eCharacterClass, ClassTrainerConfig>();

        static TrainerDataManager()
        {
            // BASE CLASSES (Practice Equipment & Lore)
            Configs[eCharacterClass.Acolyte] = new ClassTrainerConfig { ClassID = eCharacterClass.Acolyte, TranslationPrefix = "Acolyte", PracticeWeapon = "training_mace", PracticeShield = "small_training_shield" };
            Configs[eCharacterClass.AlbionRogue] = new ClassTrainerConfig { ClassID = eCharacterClass.AlbionRogue, TranslationPrefix = "AlbionRogue", PracticeWeapon = "practice_dirk" };
            Configs[eCharacterClass.Disciple] = new ClassTrainerConfig { ClassID = eCharacterClass.Disciple, TranslationPrefix = "Disciple", PracticeStaff = "trimmed_branch" };
            Configs[eCharacterClass.Elementalist] = new ClassTrainerConfig { ClassID = eCharacterClass.Elementalist, TranslationPrefix = "Elementalist", PracticeStaff = "trimmed_branch" };
            Configs[eCharacterClass.Fighter] = new ClassTrainerConfig { ClassID = eCharacterClass.Fighter, TranslationPrefix = "Fighter", PracticeWeapon = "practice_sword", PracticeShield = "small_training_shield" };
            Configs[eCharacterClass.Mage] = new ClassTrainerConfig { ClassID = eCharacterClass.Mage, TranslationPrefix = "Mage", PracticeStaff = "trimmed_branch" };
            
            Configs[eCharacterClass.Forester] = new ClassTrainerConfig { ClassID = eCharacterClass.Forester, TranslationPrefix = "Forester", PracticeStaff = "training_staff" };
            Configs[eCharacterClass.Guardian] = new ClassTrainerConfig { ClassID = eCharacterClass.Guardian, TranslationPrefix = "Guardian", PracticeWeapon = "training_sword_hib", PracticeShield = "training_shield" };
            Configs[eCharacterClass.Magician] = new ClassTrainerConfig { ClassID = eCharacterClass.Magician, TranslationPrefix = "Magician", PracticeStaff = "training_staff" };
            Configs[eCharacterClass.Naturalist] = new ClassTrainerConfig { ClassID = eCharacterClass.Naturalist, TranslationPrefix = "Naturalist", PracticeWeapon = "training_club", PracticeShield = "training_shield" };
            Configs[eCharacterClass.Stalker] = new ClassTrainerConfig { ClassID = eCharacterClass.Stalker, TranslationPrefix = "Stalker", PracticeWeapon = "training_dirk" };

            Configs[eCharacterClass.MidgardRogue] = new ClassTrainerConfig { ClassID = eCharacterClass.MidgardRogue, TranslationPrefix = "MidgardRogue", PracticeWeapon = "training_sword_mid" };
            Configs[eCharacterClass.Mystic] = new ClassTrainerConfig { ClassID = eCharacterClass.Mystic, TranslationPrefix = "Mystic", PracticeStaff = "trimmed_branch" };
            Configs[eCharacterClass.Seer] = new ClassTrainerConfig { ClassID = eCharacterClass.Seer, TranslationPrefix = "Seer", PracticeWeapon = "training_hammer", PracticeShield = "small_training_shield" };
            Configs[eCharacterClass.Viking] = new ClassTrainerConfig { ClassID = eCharacterClass.Viking, TranslationPrefix = "Viking", PracticeWeapon = "training_axe", PracticeShield = "small_training_shield" };

            // ALBION SPECIALIZED CLASSES
            Configs[eCharacterClass.Armsman] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Armsman, TranslationPrefix = "Armsman", ClassNameEN = "armsman", ClassNameFR = "maître d'armes",
                Armors = new[] { "hauberk_of_the_neophyte_alb", "footsoldiers_hauberk", "infantry_hauberk_alb", "soldiers_hauberk_alb" },
                PromotionTextKey = "ArmsmanTrainer.Interact.Text4"
            }.AddPromoKeyword("Defenders of Albion", "Défenseurs d'Albion")
             .AddChoice("slashing", "tranchante", "ArmsmanTrainer.WhisperReceive.Text1", "slash_sword_item")
             .AddChoice("crushing", "contondante", "ArmsmanTrainer.WhisperReceive.Text2", "crush_sword_item")
             .AddChoice("thrusting", "perforante", "ArmsmanTrainer.WhisperReceive.Text3", "thrust_sword_item")
             .AddChoice("polearms", "arme d'hast", "ArmsmanTrainer.WhisperReceive.Text4", "pike_polearm_item")
             .AddChoice("two handed", "deux mains", "ArmsmanTrainer.WhisperReceive.Text5", "twohand_sword_item");

            Configs[eCharacterClass.Cabalist] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Cabalist, TranslationPrefix = "Cabalist", ClassNameEN = "cabalist", ClassNameFR = "cabaliste",
                Armors = new[] { "robes_of_the_apprentice_alb", "journeymans_robe", "imbuers_robe_alb", "creators_robe_alb" },
                PromotionTextKey = "CabalistTrainer.Interact.Text4", DefaultWeapon = "cabalist_item"
            }.AddPromoKeyword("Guild of Shadows", "Guilde des Ombres");

            Configs[eCharacterClass.Cleric] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Cleric, TranslationPrefix = "Cleric", ClassNameEN = "cleric", ClassNameFR = "clerc",
                Armors = new[] { "vest_of_the_apprentice_alb", "novices_vest", "curates_vest", "prelates_hauberk_alb" },
                DefaultWeapon = "cleric_item", PromotionTextKey = "ClericTrainer.Interact.Text4"
            }.AddPromoKeyword("Church of Albion", "Eglise d'Albion");

            Configs[eCharacterClass.Friar] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Friar, TranslationPrefix = "Friar", ClassNameEN = "friar", ClassNameFR = "moine",
                Armors = new[] { "robes_of_the_novice_alb", "chaplains_robe", "fanatics_robes", "zealots_robes" },
                DefaultWeapon = "friar_staff", PromotionTextKey = "FriarTrainer.Interact.Text4"
            }.AddPromoKeyword("Defenders of Albion", "Défenseurs d'Albion");

            Configs[eCharacterClass.Heretic] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Heretic, TranslationPrefix = "Heretic", ClassNameEN = "heretic", ClassNameFR = "hérétique",
                Armors = new[] { "robes_of_the_novice_alb", "robe_of_the_apprentice_heretic", "robe_of_the_initiate_alb", "proselytes_robe_alb" },
                PromotionTextKey = "HereticTrainer.Interact.Text4"
            }.AddPromoKeyword("Temple of Arawn", "Temple d'Arawn")
             .AddChoice("crushing", "contondante", "HereticTrainer.WhisperReceive.Text1", "cleric_item")
             .AddChoice("flexible", "flexible", "HereticTrainer.WhisperReceive.Text2", "reaver_item");

            Configs[eCharacterClass.Infiltrator] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Infiltrator, TranslationPrefix = "Infiltrator", ClassNameEN = "infiltrator", ClassNameFR = "sicaire",
                Armors = new[] { "vest_of_the_initiate_alb", "vest_of_the_lurker_alb", "blue_hands_vest", "spys_vest" },
                PromotionTextKey = "InfiltratorTrainer.Interact.Text4"
            }.AddPromoKeyword("Guild of Shadows", "Guilde des Ombres")
             .AddChoice("slashing", "tranchante", "InfiltratorTrainer.WhisperReceive.Text1", "slash_sword_item")
             .AddChoice("thrusting", "perforante", "InfiltratorTrainer.WhisperReceive.Text2", "thrust_sword_item");

            Configs[eCharacterClass.Mercenary] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Mercenary, TranslationPrefix = "Mercenary", ClassNameEN = "mercenary", ClassNameFR = "mercenaire",
                Armors = new[] { "hauberk_of_the_neophyte_alb", "vest_of_the_pugilist_alb", "hauberk_of_the_escalader", "swashbucklers_hauberk_alb" },
                PromotionTextKey = "MercenaryTrainer.Interact.Text4"
            }.AddPromoKeyword("Guild of Shadows", "Guilde des Ombres")
             .AddChoice("slashing", "tranchante", "MercenaryTrainer.WhisperReceive.Text1", "slash_sword_item")
             .AddChoice("crushing", "contondante", "MercenaryTrainer.WhisperReceive.Text2", "crush_sword_item")
             .AddChoice("thrusting", "perforante", "MercenaryTrainer.WhisperReceive.Text3", "thrust_sword_item");

            Configs[eCharacterClass.Minstrel] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Minstrel, TranslationPrefix = "Minstrel", ClassNameEN = "minstrel", ClassNameFR = "ménestrel",
                Armors = new[] { "vest_of_the_initiate_alb", "vest_of_the_sonneteer", "vest_of_the_versesmith", "vest_of_the_lyricist" },
                DefaultWeapon = "minstrel_item", PromotionTextKey = "MinstrelTrainer.Interact.Text4"
            }.AddPromoKeyword("the Academy", "l'Académie");

            Configs[eCharacterClass.Necromancer] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Necromancer, TranslationPrefix = "Necromancer", ClassNameEN = "necromancer", ClassNameFR = "prêtre d'arawn",
                Armors = new[] { "robes_of_the_apprentice_alb", "servants_robe", "robe_of_the_summoner_alb", "adepts_robe_alb" },
                DefaultWeapon = "necromancer_item", PromotionTextKey = "NecromancerTrainer.Interact.Text4"
            }.AddPromoKeyword("Temple of Arawn", "Temple d'Arawn");

            Configs[eCharacterClass.Occultist] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Occultist, TranslationPrefix = "Occultist", ClassNameEN = "occultist", ClassNameFR = "conjurateur des ombres",
                Armors = new[] { "robes_of_the_apprentice_alb", "servants_robe", "robe_of_the_summoner_alb", "adepts_robe_alb" },
                DefaultWeapon = "necromancer_item", PromotionTextKey = "OccultistTrainer.Interact.Text4"
            }.AddPromoKeyword("Temple of Shadows", "Temple des Ombres");

            Configs[eCharacterClass.Paladin] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Paladin, TranslationPrefix = "Paladin", ClassNameEN = "paladin", ClassNameFR = "paladin",
                Armors = new[] { "hauberk_of_the_neophyte_alb", "tyros_hauberk_alb", "protectors_hauberk_alb", "hauberk_of_the_defender_alb" },
                PromotionTextKey = "PaladinTrainer.Interact.Text4"
            }.AddPromoKeyword("Church of Albion", "Eglise d'Albion")
             .AddChoice("slashing", "tranchante", "PaladinTrainer.WhisperReceive.Text1", "slash_sword_item")
             .AddChoice("crushing", "contondante", "PaladinTrainer.WhisperReceive.Text2", "crush_sword_item")
             .AddChoice("thrusting", "perforante", "PaladinTrainer.WhisperReceive.Text3", "thrust_sword_item")
             .AddChoice("two handed", "deux mains", "PaladinTrainer.WhisperReceive.Text4", "twohand_sword_item");

            Configs[eCharacterClass.Reaver] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Reaver, TranslationPrefix = "Reaver", ClassNameEN = "reaver", ClassNameFR = "fléau d'arawn",
                Armors = new[] { "hauberk_of_the_neophyte_alb", "vest_of_the_strongarm", "hauberk_of_the_protector_alb", "soul_protectors_hauberk_alb" },
                PromotionTextKey = "ReaverTrainer.Interact.Text4"
            }.AddPromoKeyword("Temple of Arawn", "Temple d'Arawn")
             .AddChoice("slashing", "tranchante", "ReaverTrainer.WhisperReceive.Text1", "slash_sword_item")
             .AddChoice("crushing", "contondante", "ReaverTrainer.WhisperReceive.Text2", "crush_sword_item")
             .AddChoice("thrusting", "perforante", "ReaverTrainer.WhisperReceive.Text3", "thrust_sword_item")
             .AddChoice("flexible", "flexible", "ReaverTrainer.WhisperReceive.Text4", "reaver_item");

            Configs[eCharacterClass.Scout] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Scout, TranslationPrefix = "Scout", ClassNameEN = "scout", ClassNameFR = "éclaireur",
                Armors = new[] { "vest_of_the_initiate_alb", "bowmans_vest", "trackers_vest_alb", "watchers_vest_alb" },
                DefaultWeapon = "scout_item", PromotionTextKey = "ScoutTrainer.Interact.Text4"
            }.AddPromoKeyword("Defenders of Albion", "Défenseurs d'Albion");

            Configs[eCharacterClass.Sorcerer] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Sorcerer, TranslationPrefix = "Sorcerer", ClassNameEN = "sorcerer", ClassNameFR = "sorcier",
                Armors = new[] { "robes_of_the_apprentice_alb", "journeymans_robe2", "befuddlers_robe_alb", "charmers_robe_alb" },
                DefaultWeapon = "sorcerer_item", PromotionTextKey = "SorcererTrainer.Interact.Text4"
            }.AddPromoKeyword("the Academy", "l'Académie");

            Configs[eCharacterClass.Theurgist] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Theurgist, TranslationPrefix = "Theurgist", ClassNameEN = "theurgist", ClassNameFR = "théurgiste",
                Armors = new[] { "robes_of_the_apprentice_alb", "journeymans_robe", "summoners_robe_alb", "sappers_robe_alb" },
                DefaultWeapon = "theurgist_item", PromotionTextKey = "TheurgistTrainer.Interact.Text4"
            }.AddPromoKeyword("Defenders of Albion", "Défenseurs d'Albion");

            Configs[eCharacterClass.Wizard] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Wizard, TranslationPrefix = "Wizard", ClassNameEN = "wizard", ClassNameFR = "thaumaturge",
                Armors = new[] { "robes_of_the_apprentice_alb", "adepts_robe_alb2", "elementalists_robe", "spellbinders_robe_alb" },
                DefaultWeapon = "wizard_item", PromotionTextKey = "WizardTrainer.Interact.Text4"
            }.AddPromoKeyword("the Academy", "l'Académie");

            Configs[eCharacterClass.MaulerAlb] = new ClassTrainerConfig {
                ClassID = eCharacterClass.MaulerAlb, TranslationPrefix = "MaulerAlb", ClassNameEN = "mauler", ClassNameFR = "kan-laresh",
                CustomExplainKey = "Baseclass.Mauler.Explain", CustomRefuseKey = "Baseclass.Mauler.Refuse",
                Armors = new[] { "vest_of_the_initiate_alb", "stoneskin_vest_alb", "vest_of_five_paws_alb", "vest_of_the_pugilist_alb2" },
                PromotionTextKey = "MaulerAlbTrainer.Interact.Text4"
            }.AddPromoKeyword("Temple of the Iron Fist", "Temple de la Main de Fer")
             .AddChoice("staff", "bâton", "MaulerAlbTrainer.WhisperReceive.Text1", "mauleralb_item_staff")
             .AddChoice("fists", "poings", "MaulerAlbTrainer.WhisperReceive.Text2", "mauleralb_item_fist", "mauleralb_item_fist");

            // HIBERNIA SPECIALIZED CLASSES
            Configs[eCharacterClass.Animist] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Animist, TranslationPrefix = "Animist", ClassNameEN = "animist", ClassNameFR = "animiste",
                Armors = new[] { "robes_of_the_apprentice_hib", "apprentices_robe_hib", "friend_of_gaias_robe_hib", "plantfriends_robe_hib" },
                DefaultWeapon = "animist_item", PromotionTextKey = "AnimistTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Affinity", "Voie de l'Affinité");

            Configs[eCharacterClass.Bainshee] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Bainshee, TranslationPrefix = "Bainshee", ClassNameEN = "bainshee", ClassNameFR = "banshee",
                Armors = new[] { "robes_of_the_initiate_hib", "robe_of_the_wraith_apprentice_hib", "robe_of_the_phantom_adept_hib", "robe_of_the_phantom_reaper_hib" },
                DefaultWeapon = "bainshee_item", PromotionTextKey = "BainsheeTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Spectral Force", "Voie de la Force Spectrale");

            Configs[eCharacterClass.Bard] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Bard, TranslationPrefix = "Bard", ClassNameEN = "bard", ClassNameFR = "barde",
                Armors = new[] { "vest_of_the_chanter_hib", "vocalists_vest", "carolers_vest", "choralists_vest" },
                DefaultWeapon = "bard_item", PromotionTextKey = "BardTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Essence", "Voie de l'Essence");

            Configs[eCharacterClass.Blademaster] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Blademaster, TranslationPrefix = "Blademaster", ClassNameEN = "blademaster", ClassNameFR = "finelame",
                Armors = new[] { "vest_of_the_neophyte_hib", "stylists_vest", "sabreurs_vest", "bladeweavers_vest" },
                PromotionTextKey = "BlademasterTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Harmony", "Voie de l'Harmonie")
             .AddChoice("blunt", "contondante", "BlademasterTrainer.WhisperReceive.Text2", "blunt_hib_item")
             .AddChoice("blades", "épées", "BlademasterTrainer.WhisperReceive.Text1", "blades_hib_item")
             .AddChoice("piercing", "perforante", "BlademasterTrainer.WhisperReceive.Text3", "piercing_hib_item");

            Configs[eCharacterClass.Champion] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Champion, TranslationPrefix = "Champion", ClassNameEN = "champion", ClassNameFR = "champion",
                Armors = new[] { "vest_of_the_neophyte_hib", "chargers_vest", "vest_of_the_propugner", "huberk_of_the_valiant_hib" },
                PromotionTextKey = "ChampionTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Essence", "Voie de l'Essence")
             .AddChoice("blades", "épées", "ChampionTrainer.WhisperReceive.Text1", "blades_hib_item")
             .AddChoice("blunt", "contondante", "ChampionTrainer.WhisperReceive.Text2", "blunt_hib_item")
             .AddChoice("piercing", "perforante", "ChampionTrainer.WhisperReceive.Text3", "piercing_hib_item")
             .AddChoice("large weapons", "grandes armes", "ChampionTrainer.WhisperReceive.Text4", "largeweap_hib_item");

            Configs[eCharacterClass.Druid] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Druid, TranslationPrefix = "Druid", ClassNameEN = "druid", ClassNameFR = "druide",
                Armors = new[] { "vest_of_the_novice_hib", "students_vest_hib", "apprentices_vest_hib", "grove_healer_hauberk_hib" },
                DefaultWeapon = "druid_item", PromotionTextKey = "DruidTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Harmony", "Voie de l'Harmonie");

            Configs[eCharacterClass.Eldritch] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Eldritch, TranslationPrefix = "Eldritch", ClassNameEN = "eldritch", ClassNameFR = "eldritch",
                Armors = new[] { "robes_of_the_apprentice_hib", "evokers_robe", "conjurers_robe_hib", "magius_robe_hib" },
                DefaultWeapon = "eldritch_item", PromotionTextKey = "EldritchTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Focus", "Voie des Anciens");

            Configs[eCharacterClass.Enchanter] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Enchanter, TranslationPrefix = "Enchanter", ClassNameEN = "enchanter", ClassNameFR = "enchanteur",
                Armors = new[] { "robes_of_the_apprentice_hib", "illusionists_robe_hib", "glamourists_robe_hib", "entrancers_robe_hib" },
                DefaultWeapon = "enchanter_item", PromotionTextKey = "EnchanterTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Essence", "Voie de l'Essence");

            Configs[eCharacterClass.Hero] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Hero, TranslationPrefix = "Hero", ClassNameEN = "hero", ClassNameFR = "protecteur",
                Armors = new[] { "vest_of_the_neophyte_hib", "servitors_vest", "confidants_vest_hib", "henchmans_vest_hib" },
                PromotionTextKey = "HeroTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Focus", "Voie des Anciens")
             .AddChoice("blades", "épées", "HeroTrainer.WhisperReceive.Text1", "blades_hib_item")
             .AddChoice("blunt", "contondante", "HeroTrainer.WhisperReceive.Text2", "blunt_hib_item")
             .AddChoice("piercing", "perforante", "HeroTrainer.WhisperReceive.Text3", "piercing_hib_item")
             .AddChoice("large weapons", "grandes armes", "HeroTrainer.WhisperReceive.Text4", "largeweap_hib_item")
             .AddChoice("celtic spear", "lance celtique", "HeroTrainer.WhisperReceive.Text5", "celticspear_hib_item");

            Configs[eCharacterClass.Mentalist] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Mentalist, TranslationPrefix = "Mentalist", ClassNameEN = "mentalist", ClassNameFR = "empathe",
                Armors = new[] { "robes_of_the_apprentice_hib", "adepts_robe_hib", "robe_of_the_thought_walker_hib", "robe_of_the_visionary_hib" },
                DefaultWeapon = "mentalist_item", PromotionTextKey = "MentalistTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Harmony", "Voie de l'Harmonie");

            Configs[eCharacterClass.Nightshade] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Nightshade, TranslationPrefix = "Nightshade", ClassNameEN = "nightshade", ClassNameFR = "ombre",
                Armors = new[] { "vest_of_the_huntsman_hib", "nightwalkers_vest", "darkshades_vest", "darkblades_vest" },
                PromotionTextKey = "NightshadeTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Essence", "Voie de l'Essence")
             .AddChoice("blades", "épées", "NightshadeTrainer.WhisperReceive.Text1", "blades_hib_item")
             .AddChoice("piercing", "perforante", "NightshadeTrainer.WhisperReceive.Text2", "piercing_hib_item");

            Configs[eCharacterClass.Ranger] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Ranger, TranslationPrefix = "Ranger", ClassNameEN = "ranger", ClassNameFR = "ranger",
                Armors = new[] { "vest_of_the_huntsman_hib", "vest_of_the_lurker_hib", "archers_vest", "trackers_vest_hib" },
                DefaultWeapon = "ranger_item", PromotionTextKey = "RangerTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Focus", "Voie des Anciens");

            Configs[eCharacterClass.Valewalker] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Valewalker, TranslationPrefix = "Valewalker", ClassNameEN = "valewalker", ClassNameFR = "faucheur",
                Armors = new[] { "robes_of_the_apprentice_hib", "scythewielders_robe", "forestwalkers_robe", "reapers_robe_hib" },
                DefaultWeapon = "valewalker_item", PromotionTextKey = "ValewalkerTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Affinity", "Voie de l'Affinité");

            Configs[eCharacterClass.Vampiir] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Vampiir, TranslationPrefix = "Vampiir", ClassNameEN = "vampiir", ClassNameFR = "séide de leanansidhe",
                Armors = new[] { "vest_of_the_huntsman_hib", "apprentices_vest_hib2", "adepts_vest_hib", "protectors_vest_hib" },
                DefaultWeapon = "piercing_hib_item", PromotionTextKey = "VampiirTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Affinity", "Voie de l'Affinité");

            Configs[eCharacterClass.Warden] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Warden, TranslationPrefix = "Warden", ClassNameEN = "warden", ClassNameFR = "sentinelle",
                Armors = new[] { "vest_of_the_neophyte_hib", "woodsmans_vest", "guardians_vest", "vest_of_the_hunter" },
                PromotionTextKey = "WardenTrainer.Interact.Text4"
            }.AddPromoKeyword("Path of Focus", "Voie des Anciens")
             .AddChoice("blades", "épées", "WardenTrainer.WhisperReceive.Text1", "blades_hib_item")
             .AddChoice("blunt", "contondante", "WardenTrainer.WhisperReceive.Text2", "blunt_hib_item");

            Configs[eCharacterClass.MaulerHib] = new ClassTrainerConfig {
                ClassID = eCharacterClass.MaulerHib, TranslationPrefix = "MaulerHib", ClassNameEN = "mauler", ClassNameFR = "kan-laresh",
                CustomExplainKey = "Baseclass.Mauler.Explain", CustomRefuseKey = "Baseclass.Mauler.Refuse",
                Armors = new[] { "vest_of_the_novice_hib2", "stoneskin_vest_hib", "vest_of_five_paws_hib", "vest_of_the_pugilist_hib" },
                PromotionTextKey = "MaulerHibTrainer.Interact.Text4"
            }.AddPromoKeyword("Temple of the Iron Fist", "Temple de la Main de Fer")
             .AddChoice("staff", "bâton", "MaulerHibTrainer.WhisperReceive.Text1", "maulerhib_item_staff")
             .AddChoice("fists", "poings", "MaulerHibTrainer.WhisperReceive.Text2", "maulerhib_item_fist", "maulerhib_item_fist");

            // MIDGARD SPECIALIZED CLASSES
            Configs[eCharacterClass.Berserker] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Berserker, TranslationPrefix = "Berserker", ClassNameEN = "berserker", ClassNameFR = "berserker",
                Armors = new[] { "vest_of_the_huntsman_mid2", "seekers_vest", "fervents_vest_mid", "pillagers_vest2" },
                PromotionTextKey = "BerserkerTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Modi", "Panthéon de Modi")
             .AddChoice("sword", "épée", "BerserkerTrainer.WhisperReceive.Text1", "sword_mid_item")
             .AddChoice("hammer", "marteau", "BerserkerTrainer.WhisperReceive.Text2", "hammer_mid_item")
             .AddChoice("axe", "hache", "BerserkerTrainer.WhisperReceive.Text3", "axe_mid_item")
             .AddChoice("left axe", "hache main gauche", "BerserkerTrainer.WhisperReceive.Text4", "leftaxe_mid_item");

            Configs[eCharacterClass.Bonedancer] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Bonedancer, TranslationPrefix = "Bonedancer", ClassNameEN = "bonedancer", ClassNameFR = "prêtre de bogdar",
                Armors = new[] { "vest_of_the_eleve_mid", "bonegatherers_vest", "tribals_vest_mid", "apprentices_vest_mid" },
                DefaultWeapon = "bonedancer_item", PromotionTextKey = "BonedancerTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Bodgar", "Panthéon de Bogdar").AddPromoKeyword("House of Bogdar", "Panthéon de Bogdar");

            Configs[eCharacterClass.Healer] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Healer, TranslationPrefix = "Healer", ClassNameEN = "healer", ClassNameFR = "guérisseur",
                Armors = new[] { "vest_of_the_seer_mid", "practioners_vest2", "journeymans_vest_mid4", "vest_of_the_wise" },
                DefaultWeapon = "healer_item", PromotionTextKey = "HealerTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Eir", "Panthéon d'Eir");

            Configs[eCharacterClass.Hunter] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Hunter, TranslationPrefix = "Hunter", ClassNameEN = "hunter", ClassNameFR = "chasseur",
                Armors = new[] { "vest_of_the_huntsman_mid2", "vest_of_the_shadowed_seeker", "journeymans_vest_mid3", "vest_of_the_prey_stalker" },
                DefaultWeapon = "hunter_item", PromotionTextKey = "HunterTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Skadi", "Panthéon de Skadi");

            Configs[eCharacterClass.Runemaster] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Runemaster, TranslationPrefix = "Runemaster", ClassNameEN = "runemaster", ClassNameFR = "prêtre d'odin",
                Armors = new[] { "vest_of_the_eleve_mid", "runic_practitioners_vest", "runecarvers_vest", "stonetellers_vest" },
                DefaultWeapon = "runemaster_item", PromotionTextKey = "RunemasterTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Odin", "Panthéon d'Odin'");

            Configs[eCharacterClass.Savage] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Savage, TranslationPrefix = "Savage", ClassNameEN = "savage", ClassNameFR = "sauvage",
                Armors = new[] { "vest_of_the_huntsman_mid2", "apprentices_vest_mid2", "servants_vest_mid", "tribals_vest_mid2" },
                PromotionTextKey = "SavageTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Kelgor", "Panthéon de Kelgor")
             .AddChoice("sword", "épée", "SavageTrainer.WhisperReceive.Text1", "sword_mid_item")
             .AddChoice("hammer", "marteau", "SavageTrainer.WhisperReceive.Text2", "hammer_mid_item")
             .AddChoice("axe", "hache", "SavageTrainer.WhisperReceive.Text3", "axe_mid_item")
             .AddChoice("hand to hand", "griffe", "SavageTrainer.WhisperReceive.Text4", "handtohand_mid_item");

            Configs[eCharacterClass.Shadowblade] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Shadowblade, TranslationPrefix = "Shadowblade", ClassNameEN = "shadowblade", ClassNameFR = "assassin",
                Armors = new[] { "vest_of_the_huntsman_mid", "dark_seekers_vest", "deceivers_vest_mid", "shadow_lurkers_vest" },
                DefaultWeapon = "shadowblade_item", PromotionTextKey = "ShadowbladeTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Loki", "Panthéon de Loki");

            Configs[eCharacterClass.Shaman] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Shaman, TranslationPrefix = "Shaman", ClassNameEN = "shaman", ClassNameFR = "chaman",
                Armors = new[] { "vest_of_the_seer_mid", "practitioners_vest3", "journeymans_vest_mid2", "medicines_vest_mid" },
                DefaultWeapon = "shaman_item", PromotionTextKey = "ShamanTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Ymir", "Panthéon d'Ymir");

            Configs[eCharacterClass.Skald] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Skald, TranslationPrefix = "Skald", ClassNameEN = "skald", ClassNameFR = "skald",
                Armors = new[] { "vest_of_the_huntsman_mid2", "chanters_vest", "song_weavers_vest", "saga_spinners_vest" },
                PromotionTextKey = "SkaldTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Bragi", "Panthéon de Bragi")
             .AddChoice("sword", "épée", "SkaldTrainer.WhisperReceive.Text1", "sword_mid_item")
             .AddChoice("hammer", "marteau", "SkaldTrainer.WhisperReceive.Text2", "hammer_mid_item")
             .AddChoice("axe", "hache", "SkaldTrainer.WhisperReceive.Text3", "axe_mid_item");

            Configs[eCharacterClass.Spiritmaster] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Spiritmaster, TranslationPrefix = "Spiritmaster", ClassNameEN = "spiritmaster", ClassNameFR = "prêtre de hel",
                Armors = new[] { "vest_of_the_eleve_mid", "practitioners_vest", "journeymans_vest_mid1", "summoners_vest_mid" },
                DefaultWeapon = "spiritmaster_item", PromotionTextKey = "SpiritmasterTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Hel", "Panthéon de Hel");

            Configs[eCharacterClass.Thane] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Thane, TranslationPrefix = "Thane", ClassNameEN = "thane", ClassNameFR = "thane",
                Armors = new[] { "hauberk_of_the_neophyte_mid", "stormhammers_vest", "storm_callers_vest", "thors_hammers_vest" },
                PromotionTextKey = "ThaneTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Thor", "Panthéon de Thor")
             .AddChoice("sword", "épée", "ThaneTrainer.WhisperReceive.Text1", "sword_mid_item")
             .AddChoice("hammer", "marteau", "ThaneTrainer.WhisperReceive.Text2", "hammer_mid_item")
             .AddChoice("axe", "hache", "ThaneTrainer.WhisperReceive.Text3", "axe_mid_item");

            Configs[eCharacterClass.Valkyrie] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Valkyrie, TranslationPrefix = "Valkyrie", ClassNameEN = "valkyrie", ClassNameFR = "valkyrie",
                Armors = new[] { "hauberk_of_the_neophyte_mid", "handmaidens_hauberk_mid", "servants_hauberk_mid", "protectors_hauberk_mid" },
                PromotionTextKey = "ValkyrieTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Odin", "Panthéon d'Odin")
             .AddChoice("sword", "épée", "ValkyrieTrainer.WhisperReceive.Text1", "sword_mid_item")
             .AddChoice("spear", "lance", "ValkyrieTrainer.WhisperReceive.Text2", "spear_mid_item");

            Configs[eCharacterClass.Warlock] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Warlock, TranslationPrefix = "Warlock", ClassNameEN = "warlock", ClassNameFR = "helhaxa",
                Armors = new[] { "vest_of_the_eleve_mid", "conjurers_vest", "hels_initiate_vest", "hels_spiritists_vest" },
                DefaultWeapon = "warlock_item", PromotionTextKey = "WarlockTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Hel", "Panthéon de Hel");

            Configs[eCharacterClass.Warrior] = new ClassTrainerConfig {
                ClassID = eCharacterClass.Warrior, TranslationPrefix = "Warrior", ClassNameEN = "warrior", ClassNameFR = "guerrier",
                Armors = new[] { "hauberk_of_the_neophyte_mid", "yeomans_vest", "footmans_vest", "veterans_vest_mid" },
                PromotionTextKey = "WarriorTrainer.Interact.Text4"
            }.AddPromoKeyword("House of Tyr", "Panthéon de Tyr")
             .AddChoice("sword", "épée", "WarriorTrainer.WhisperReceive.Text1", "sword_mid_item")
             .AddChoice("hammer", "marteau", "WarriorTrainer.WhisperReceive.Text2", "hammer_mid_item")
             .AddChoice("axe", "hache", "WarriorTrainer.WhisperReceive.Text3", "axe_mid_item");

            Configs[eCharacterClass.MaulerMid] = new ClassTrainerConfig {
                ClassID = eCharacterClass.MaulerMid, TranslationPrefix = "MaulerMid", ClassNameEN = "mauler", ClassNameFR = "kan-laresh",
                CustomExplainKey = "Baseclass.Mauler.Explain", CustomRefuseKey = "Baseclass.Mauler.Refuse",
                Armors = new[] { "vest_of_the_novice_mid", "stoneskin_vest_mid", "vest_of_five_paws_mid", "vest_of_the_pugilist_mid" },
                PromotionTextKey = "MaulerMidTrainer.Interact.Text4"
            }.AddPromoKeyword("Temple of the Iron Fist", "Temple de la Main de Fer")
             .AddChoice("staff", "bâton", "MaulerMidTrainer.WhisperReceive.Text1", "maulermid_item_staff")
             .AddChoice("fists", "poings", "MaulerMidTrainer.WhisperReceive.Text2", "maulermid_item_fist", "maulermid_item_fist");
        }

        public static ClassTrainerConfig GetConfig(eCharacterClass classId)
        {
            Configs.TryGetValue(classId, out var config);
            return config;
        }
    }
}