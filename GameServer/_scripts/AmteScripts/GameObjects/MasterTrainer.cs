/* Updated by Taovil  // Instant 50 with RR1-13 
only do:      /mob create DOL.GS.Trainer.I50TrainerRR1_13
 */

using System;
using DOL;
using DOL.GS;
using DOL.Events;
using DOL.GS.PacketHandler;
using System.Reflection;
using System.Collections;
using DOL.Database;
using log4net;
using DOL.Language;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;
using System.Reactive.Joins;


namespace DOL.GS.Trainer
{
    [NPCGuildScript("Master Trainer")]
    public class MasterTrainer : GameTrainer
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public const string PRACTICE_WEAPON_ID1 = "training_mace";
        public const string PRACTICE_WEAPON_ID2 = "practice_sword";
        public const string PRACTICE_WEAPON_ID3 = "training_sword_hib";
        public const string PRACTICE_WEAPON_ID4 = "training_club";
        public const string PRACTICE_WEAPON_ID5 = "training_sword_mid";
        public const string PRACTICE_WEAPON_ID6 = "training_hammer";
        public const string PRACTICE_WEAPON_ID7 = "training_axe";
        public const string PRACTICE_WEAPON_ID8 = "practice_dirk";
        public const string PRACTICE_WEAPON_ID9 = "training_dirk";
        public const string PRACTICE_SHIELD_ID1 = "small_training_shield";
        public const string PRACTICE_SHIELD_ID2 = "training_shield";
        public const string PRACTICE_STAFF_ID1 = "training_staff";
        public const string PRACTICE_STAFF_ID2 = "trimmed_branch";

        public const string ALBWEAPON_ID1 = "slash_sword_item";
        public const string ALBWEAPON_ID2 = "crush_sword_item";
        public const string ALBWEAPON_ID3 = "thrust_sword_item";
        public const string ALBWEAPON_ID4 = "pike_polearm_item";
        public const string ALBWEAPON_ID5 = "twohand_sword_item";
        public const string ALBWEAPON_ID6 = "mauleralb_item_staff";
        public const string ALBWEAPON_ID7 = "mauleralb_item_fist";
        public const string ALBWEAPON_ID8 = "cabalist_item";
        public const string ALBWEAPON_ID9 = "cleric_item";
        public const string ALBWEAPON_ID10 = "friar_staff";
        public const string ALBWEAPON_ID11 = "reaver_item";
        public const string ALBWEAPON_ID12 = "minstrel_item";
        public const string ALBWEAPON_ID13 = "necromancer_item";
        public const string ALBWEAPON_ID14 = "scout_item";
        public const string ALBWEAPON_ID15 = "sorcerer_item";
        public const string ALBWEAPON_ID16 = "theurgist_item";
        public const string ALBWEAPON_ID17 = "wizard_item";
        public const string ALBMAULARMOR_ID1 = "vest_of_the_initiate_alb";
        public const string ALBMAULARMOR_ID2 = "stoneskin_vest_alb";
        public const string ALBMAULARMOR_ID3 = "vest_of_five_paws_alb";
        public const string ALBMAULARMOR_ID4 = "vest_of_the_pugilist_alb2";
        public const string ARMSARMOR_ID1 = "hauberk_of_the_neophyte_alb";
        public const string ARMSARMOR_ID2 = "footsoldiers_hauberk";
        public const string ARMSARMOR_ID3 = "infantry_hauberk_alb";
        public const string ARMSARMOR_ID4 = "soldiers_hauberk_alb";
        public const string CABAARMOR_ID1 = "robes_of_the_apprentice_alb";
        public const string CABAARMOR_ID2 = "journeymans_robe";
        public const string CABAARMOR_ID3 = "imbuers_robe_alb";
        public const string CABAARMOR_ID4 = "creators_robe_alb";
        public const string CLERARMOR_ID1 = "vest_of_the_apprentice_alb";
        public const string CLERARMOR_ID2 = "novices_vest";
        public const string CLERARMOR_ID3 = "curates_vest";
        public const string CLERARMOR_ID4 = "prelates_hauberk_alb";
        public const string FRIARARMOR_ID1 = "robes_of_the_novice_alb";
        public const string FRIARARMOR_ID2 = "chaplains_robe";
        public const string FRIARARMOR_ID3 = "fanatics_robes";
        public const string FRIARARMOR_ID4 = "zealots_robes";
        public const string HEREARMOR_ID1 = "robes_of_the_novice_alb";
        public const string HEREARMOR_ID2 = "robe_of_the_apprentice_heretic";
        public const string HEREARMOR_ID3 = "robe_of_the_initiate_alb";
        public const string HEREARMOR_ID4 = "proselytes_robe_alb";
        public const string INFIARMOR_ID1 = "vest_of_the_initiate_alb";
        public const string INFIARMOR_ID2 = "vest_of_the_lurker_alb";
        public const string INFIARMOR_ID3 = "blue_hands_vest";
        public const string INFIARMOR_ID4 = "spys_vest";
        public const string MERCARMOR_ID1 = "hauberk_of_the_neophyte_alb";
        public const string MERCARMOR_ID2 = "vest_of_the_pugilist_alb";
        public const string MERCARMOR_ID3 = "hauberk_of_the_escalader";
        public const string MERCARMOR_ID4 = "swashbucklers_hauberk_alb";
        public const string MINSARMOR_ID1 = "vest_of_the_initiate_alb";
        public const string MINSARMOR_ID2 = "vest_of_the_sonneteer";
        public const string MINSARMOR_ID3 = "vest_of_the_versesmith";
        public const string MINSARMOR_ID4 = "vest_of_the_lyricist";
        public const string NECRARMOR_ID1 = "robes_of_the_apprentice_alb";
        public const string NECRARMOR_ID2 = "servants_robe";
        public const string NECRARMOR_ID3 = "robe_of_the_summoner_alb";
        public const string NECRARMOR_ID4 = "adepts_robe_alb";
        public const string PALAARMOR_ID1 = "hauberk_of_the_neophyte_alb";
        public const string PALAARMOR_ID2 = "tyros_hauberk_alb";
        public const string PALAARMOR_ID3 = "protectors_hauberk_alb";
        public const string PALAARMOR_ID4 = "hauberk_of_the_defender_alb";
        public const string REAVARMOR_ID1 = "hauberk_of_the_neophyte_alb";
        public const string REAVARMOR_ID2 = "vest_of_the_strongarm";
        public const string REAVARMOR_ID3 = "hauberk_of_the_protector_alb";
        public const string REAVARMOR_ID4 = "soul_protectors_hauberk_alb";
        public const string SCOUARMOR_ID1 = "vest_of_the_initiate_alb";
        public const string SCOUARMOR_ID2 = "bowmans_vest";
        public const string SCOUARMOR_ID3 = "trackers_vest_alb";
        public const string SCOUARMOR_ID4 = "watchers_vest_alb";
        public const string SORCARMOR_ID1 = "robes_of_the_apprentice_alb";
        public const string SORCARMOR_ID2 = "journeymans_robe2";
        public const string SORCARMOR_ID3 = "befuddlers_robe_alb";
        public const string SORCARMOR_ID4 = "charmers_robe_alb";
        public const string THEUARMOR_ID1 = "robes_of_the_apprentice_alb";
        public const string THEUARMOR_ID2 = "journeymans_robe";
        public const string THEUARMOR_ID3 = "summoners_robe_alb";
        public const string THEUARMOR_ID4 = "sappers_robe_alb";
        public const string WIZARMOR_ID1 = "robes_of_the_apprentice_alb";
        public const string WIZARMOR_ID2 = "adepts_robe_alb2";
        public const string WIZARMOR_ID3 = "elementalists_robe";
        public const string WIZARMOR_ID4 = "spellbinders_robe_alb";

        public const string HIBWEAPON_ID1 = "animist_item";
        public const string HIBWEAPON_ID2 = "bainshee_item";
        public const string HIBWEAPON_ID3 = "bard_item";
        public const string HIBWEAPON_ID4 = "druid_item";
        public const string HIBWEAPON_ID5 = "eldritch_item";
        public const string HIBWEAPON_ID6 = "enchanter_item";
        public const string HIBWEAPON_ID7 = "maulerhib_item_staff";
        public const string HIBWEAPON_ID8 = "maulerhib_item_fist";
        public const string HIBWEAPON_ID9 = "mentalist_item";
        public const string HIBWEAPON_ID10 = "blunt_hib_item";
        public const string HIBWEAPON_ID11 = "blades_hib_item";
        public const string HIBWEAPON_ID12 = "piercing_hib_item";
        public const string HIBWEAPON_ID13 = "largeweap_hib_item";
        public const string HIBWEAPON_ID14 = "celticspear_hib_item";
        public const string HIBWEAPON_ID15 = "ranger_item";
        public const string HIBWEAPON_ID16 = "valewalker_item";
        public const string ANIMARMOR_ID1 = "robes_of_the_apprentice_hib";
        public const string ANIMARMOR_ID2 = "apprentices_robe_hib";
        public const string ANIMARMOR_ID3 = "friend_of_gaias_robe_hib";
        public const string ANIMARMOR_ID4 = "plantfriends_robe_hib";
        public const string BAINARMOR_ID1 = "robes_of_the_initiate_hib";
        public const string BAINARMOR_ID2 = "robe_of_the_wraith_apprentice_hib";
        public const string BAINARMOR_ID3 = "robe_of_the_phantom_adept_hib";
        public const string BAINARMOR_ID4 = "robe_of_the_phantom_reaper_hib";
        public const string BARDARMOR_ID1 = "vest_of_the_chanter_hib";
        public const string BARDARMOR_ID2 = "vocalists_vest";
        public const string BARDARMOR_ID3 = "carolers_vest";
        public const string BARDARMOR_ID4 = "choralists_vest";
        public const string BLADARMOR_ID1 = "vest_of_the_neophyte_hib";
        public const string BLADARMOR_ID2 = "stylists_vest";
        public const string BLADARMOR_ID3 = "sabreurs_vest";
        public const string BLADARMOR_ID4 = "bladeweavers_vest";
        public const string CHAMARMOR_ID1 = "vest_of_the_neophyte_hib";
        public const string CHAMARMOR_ID2 = "chargers_vest";
        public const string CHAMARMOR_ID3 = "vest_of_the_propugner";
        public const string CHAMARMOR_ID4 = "huberk_of_the_valiant_hib";
        public const string DRUIARMOR_ID1 = "vest_of_the_novice_hib";
        public const string DRUIARMOR_ID2 = "students_vest_hib";
        public const string DRUIARMOR_ID3 = "apprentices_vest_hib";
        public const string DRUIARMOR_ID4 = "grove_healer_hauberk_hib";
        public const string ELDRARMOR_ID1 = "robes_of_the_apprentice_hib";
        public const string ELDRARMOR_ID2 = "evokers_robe";
        public const string ELDRARMOR_ID3 = "conjurers_robe_hib";
        public const string ELDRARMOR_ID4 = "magius_robe_hib";
        public const string ENCHARMOR_ID1 = "robes_of_the_apprentice_hib";
        public const string ENCHARMOR_ID2 = "illusionists_robe_hib";
        public const string ENCHARMOR_ID3 = "glamourists_robe_hib";
        public const string ENCHARMOR_ID4 = "entrancers_robe_hib";
        public const string HEROARMOR_ID1 = "vest_of_the_neophyte_hib";
        public const string HEROARMOR_ID2 = "servitors_vest";
        public const string HEROARMOR_ID3 = "confidants_vest_hib";
        public const string HEROARMOR_ID4 = "henchmans_vest_hib";
        public const string HIBMAULARMOR_ID1 = "vest_of_the_novice_hib2";
        public const string HIBMAULARMOR_ID2 = "stoneskin_vest_hib";
        public const string HIBMAULARMOR_ID3 = "vest_of_five_paws_hib";
        public const string HIBMAULARMOR_ID4 = "vest_of_the_pugilist_hib";
        public const string MENTAARMOR_ID1 = "robes_of_the_apprentice_hib";
        public const string MENTAARMOR_ID2 = "adepts_robe_hib";
        public const string MENTAARMOR_ID3 = "robe_of_the_thought_walker_hib";
        public const string MENTAARMOR_ID4 = "robe_of_the_visionary_hib";
        public const string NIGHARMOR_ID1 = "vest_of_the_huntsman_hib";
        public const string NIGHARMOR_ID2 = "nightwalkers_vest";
        public const string NIGHARMOR_ID3 = "darkshades_vest";
        public const string NIGHARMOR_ID4 = "darkblades_vest";
        public const string RANGARMOR_ID1 = "vest_of_the_huntsman_hib";
        public const string RANGARMOR_ID2 = "vest_of_the_lurker_hib";
        public const string RANGARMOR_ID3 = "archers_vest";
        public const string RANGARMOR_ID4 = "trackers_vest_hib";
        public const string VALEARMOR_ID1 = "robes_of_the_apprentice_hib";
        public const string VALEARMOR_ID2 = "scythewielders_robe";
        public const string VALEARMOR_ID3 = "forestwalkers_robe";
        public const string VALEARMOR_ID4 = "reapers_robe_hib";
        public const string VAMPARMOR_ID1 = "vest_of_the_huntsman_hib";
        public const string VAMPARMOR_ID2 = "apprentices_vest_hib2";
        public const string VAMPARMOR_ID3 = "adepts_vest_hib";
        public const string VAMPARMOR_ID4 = "protectors_vest_hib";
        public const string WARDARMOR_ID1 = "vest_of_the_neophyte_hib";
        public const string WARDARMOR_ID2 = "woodsmans_vest";
        public const string WARDARMOR_ID3 = "guardians_vest";
        public const string WARDARMOR_ID4 = "vest_of_the_hunter";

        public const string MIDWEAPON_ID1 = "sword_mid_item";
        public const string MIDWEAPON_ID2 = "hammer_mid_item";
        public const string MIDWEAPON_ID3 = "axe_mid_item";
        public const string MIDWEAPON_ID4 = "leftaxe_mid_item";
        public const string MIDWEAPON_ID5 = "spear_mid_item";
        public const string MIDWEAPON_ID6 = "handtohand_mid_item";
        public const string MIDWEAPON_ID7 = "bonedancer_item";
        public const string MIDWEAPON_ID8 = "healer_item";
        public const string MIDWEAPON_ID9 = "hunter_item";
        public const string MIDWEAPON_ID10 = "maulermid_item_staff";
        public const string MIDWEAPON_ID11 = "maulermid_item_fist";
        public const string MIDWEAPON_ID12 = "runemaster_item";
        public const string MIDWEAPON_ID13 = "shadowblade_item";
        public const string MIDWEAPON_ID14 = "shaman_item";
        public const string MIDWEAPON_ID15 = "spiritmaster_item";
        public const string MIDWEAPON_ID16 = "warlock_item";
        public const string BERZARMOR_ID1 = "vest_of_the_huntsman_mid2";
        public const string BERZARMOR_ID2 = "seekers_vest";
        public const string BERZARMOR_ID3 = "fervents_vest_mid";
        public const string BERZARMOR_ID4 = "pillagers_vest2";
        public const string BONEARMOR_ID1 = "vest_of_the_eleve_mid";
        public const string BONEARMOR_ID2 = "bonegatherers_vest";
        public const string BONEARMOR_ID3 = "tribals_vest_mid";
        public const string BONEARMOR_ID4 = "apprentices_vest_mid";
        public const string HEALARMOR_ID1 = "vest_of_the_seer_mid";
        public const string HEALARMOR_ID2 = "practioners_vest2";
        public const string HEALARMOR_ID3 = "journeymans_vest_mid4";
        public const string HEALARMOR_ID4 = "vest_of_the_wise";
        public const string HUNTARMOR_ID1 = "vest_of_the_huntsman_mid2";
        public const string HUNTARMOR_ID2 = "vest_of_the_shadowed_seeker";
        public const string HUNTARMOR_ID3 = "journeymans_vest_mid3";
        public const string HUNTARMOR_ID4 = "vest_of_the_prey_stalker";
        public const string MIDMAULARMOR_ID1 = "vest_of_the_novice_mid";
        public const string MIDMAULARMOR_ID2 = "stoneskin_vest_mid";
        public const string MIDMAULARMOR_ID3 = "vest_of_five_paws_mid";
        public const string MIDMAULARMOR_ID4 = "vest_of_the_pugilist_mid";
        public const string RUNEARMOR_ID1 = "vest_of_the_eleve_mid";
        public const string RUNEARMOR_ID2 = "runic_practitioners_vest";
        public const string RUNEARMOR_ID3 = "runecarvers_vest";
        public const string RUNEARMOR_ID4 = "stonetellers_vest";
        public const string SAVAARMOR_ID1 = "vest_of_the_huntsman_mid2";
        public const string SAVAARMOR_ID2 = "apprentices_vest_mid2";
        public const string SAVAARMOR_ID3 = "servants_vest_mid";
        public const string SAVAARMOR_ID4 = "tribals_vest_mid2";
        public const string SHADOARMOR_ID1 = "vest_of_the_huntsman_mid";
        public const string SHADOARMOR_ID2 = "dark_seekers_vest";
        public const string SHADOARMOR_ID3 = "deceivers_vest_mid";
        public const string SHADOARMOR_ID4 = "shadow_lurkers_vest";
        public const string SHAMARMOR_ID1 = "vest_of_the_seer_mid";
        public const string SHAMARMOR_ID2 = "practitioners_vest3";
        public const string SHAMARMOR_ID3 = "journeymans_vest_mid2";
        public const string SHAMARMOR_ID4 = "medicines_vest_mid";
        public const string SKALARMOR_ID1 = "vest_of_the_huntsman_mid2";
        public const string SKALARMOR_ID2 = "chanters_vest";
        public const string SKALARMOR_ID3 = "song_weavers_vest";
        public const string SKALARMOR_ID4 = "saga_spinners_vest";
        public const string SPIRARMOR_ID1 = "vest_of_the_eleve_mid";
        public const string SPIRARMOR_ID2 = "practitioners_vest";
        public const string SPIRARMOR_ID3 = "journeymans_vest_mid1";
        public const string SPIRARMOR_ID4 = "summoners_vest_mid";
        public const string THANARMOR_ID1 = "hauberk_of_the_neophyte_mid";
        public const string THANARMOR_ID2 = "stormhammers_vest";
        public const string THANARMOR_ID3 = "storm_callers_vest";
        public const string THANARMOR_ID4 = "thors_hammers_vest";
        public const string VALKARMOR_ID1 = "hauberk_of_the_neophyte_mid";
        public const string VALKARMOR_ID2 = "handmaidens_hauberk_mid";
        public const string VALKARMOR_ID3 = "servants_hauberk_mid";
        public const string VALKARMOR_ID4 = "protectors_hauberk_mid";
        public const string WARLARMOR_ID1 = "vest_of_the_eleve_mid";
        public const string WARLARMOR_ID2 = "conjurers_vest";
        public const string WARLARMOR_ID3 = "hels_initiate_vest";
        public const string WARLARMOR_ID4 = "hels_spiritists_vest";
        public const string WARRARMOR_ID1 = "hauberk_of_the_neophyte_mid";
        public const string WARRARMOR_ID2 = "yeomans_vest";
        public const string WARRARMOR_ID3 = "footmans_vest";
        public const string WARRARMOR_ID4 = "veterans_vest_mid";

        public override eCharacterClass TrainedClass => eCharacterClass.Armsman;
        public bool CanTrainClass(eCharacterClass charClass)
        {
            var trainableClasses = new HashSet<eCharacterClass>
                {
                    eCharacterClass.Cleric,
                    eCharacterClass.Friar,
                    eCharacterClass.Heretic,
                    eCharacterClass.Infiltrator,
                    eCharacterClass.Minstrel,
                    eCharacterClass.Scout,
                    eCharacterClass.Necromancer,
                    eCharacterClass.Theurgist,
                    eCharacterClass.Wizard,
                    eCharacterClass.Armsman,
                    eCharacterClass.Mercenary,
                    eCharacterClass.MaulerAlb,
                    eCharacterClass.Cabalist,
                    eCharacterClass.Paladin,
                    eCharacterClass.Reaver,
                    eCharacterClass.Sorcerer,
                    eCharacterClass.Animist,
                    eCharacterClass.Bainshee,
                    eCharacterClass.Bard,
                    eCharacterClass.Blademaster,
                    eCharacterClass.Champion,
                    eCharacterClass.Druid,
                    eCharacterClass.Eldritch,
                    eCharacterClass.Enchanter,
                    eCharacterClass.Hero,
                    eCharacterClass.MaulerHib,
                    eCharacterClass.Mentalist,
                    eCharacterClass.Nightshade,
                    eCharacterClass.Ranger,
                    eCharacterClass.Valewalker,
                    eCharacterClass.Vampiir,
                    eCharacterClass.Warden,
                    eCharacterClass.Berserker,
                    eCharacterClass.Bonedancer,
                    eCharacterClass.Healer,
                    eCharacterClass.Hunter,
                    eCharacterClass.MaulerMid,
                    eCharacterClass.Runemaster,
                    eCharacterClass.Savage,
                    eCharacterClass.Shadowblade,
                    eCharacterClass.Shaman,
                    eCharacterClass.Skald,
                    eCharacterClass.Spiritmaster,
                    eCharacterClass.Thane,
                    eCharacterClass.Valkyrie,
                    eCharacterClass.Warlock,
                    eCharacterClass.Warrior
                };
            return trainableClasses.Contains(charClass);
        }

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            if (log.IsInfoEnabled)
                log.Info("Master Trainer Initializing...");

        }

        public override eQuestIndicator GetQuestIndicator(GamePlayer player)
        {
            return eQuestIndicator.Lesson;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 50);
            OfferTrainingItems(player);
            if (player.Level == 5)
            {
                DisplayClassDescriptions(player);
            }

            player.Out.SendTrainerWindow();
            return true;
        }

        private void OfferTrainingItems(GamePlayer player)
        {
            if (player.Level <= 5)
            {
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID1, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Acolyte))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID2, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Fighter))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID3, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Guardian))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID4, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Naturalist))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID5, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.MidgardRogue))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID6, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Seer))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID7, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Viking))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID8, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID9, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Stalker))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_SHIELD_ID1, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Fighter || player.CharacterClass.ID == (int)eCharacterClass.Viking || player.CharacterClass.ID == (int)eCharacterClass.Seer))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text3", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_SHIELD_ID2, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Naturalist || player.CharacterClass.ID == (int)eCharacterClass.Guardian))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }

                if ((player.Inventory.CountItemTemplate(PRACTICE_STAFF_ID1, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Magician || player.CharacterClass.ID == (int)eCharacterClass.Forester))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MagicianTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_STAFF_ID2, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Mage || player.CharacterClass.ID == (int)eCharacterClass.Mystic || player.CharacterClass.ID == (int)eCharacterClass.Elementalist || player.CharacterClass.ID == (int)eCharacterClass.Disciple))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MagicianTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
            }
        }

        private void DisplayClassDescriptions(GamePlayer player)
        {
            if (player.CharacterClass.ID == (int)eCharacterClass.Acolyte)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Acolyte1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.AlbionRogue1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Disciple)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Disciple1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Elementalist)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Elementalist1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Fighter1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Mage)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Mage1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Forester)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Forester1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Guardian)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Guardian1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Magician)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Magician1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Naturalist)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Naturalist1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Stalker)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Stalker1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.MidgardRogue)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.MidgardRogue1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Mystic)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Mystic1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Seer)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Seer1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            if (player.CharacterClass.ID == (int)eCharacterClass.Viking)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.Viking1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
        }

        private Dictionary<GamePlayer, string> playerLastClassOffers = new Dictionary<GamePlayer, string>();
        private Dictionary<GamePlayer, string> playerWeaponOffers = new Dictionary<GamePlayer, string>();

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str))
                return false;

            GamePlayer player = source as GamePlayer;

            if (player == null)
                return false;

            str = str.ToLower();

            if (str == "yes" || str == "oui")
            {
                if (playerLastClassOffers.ContainsKey(player))
                {
                    HandleClassSelectionResponse(player, true);
                    playerLastClassOffers.Remove(player);
                }
                else
                {
                    if (player.CharacterClass.ID == (int)eCharacterClass.Acolyte || player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue || player.CharacterClass.ID == (int)eCharacterClass.Disciple || player.CharacterClass.ID == (int)eCharacterClass.Elementalist || player.CharacterClass.ID == (int)eCharacterClass.Fighter || player.CharacterClass.ID == (int)eCharacterClass.Mage || player.CharacterClass.ID == (int)eCharacterClass.Forester || player.CharacterClass.ID == (int)eCharacterClass.Guardian || player.CharacterClass.ID == (int)eCharacterClass.Magician || player.CharacterClass.ID == (int)eCharacterClass.Stalker || player.CharacterClass.ID == (int)eCharacterClass.Naturalist || player.CharacterClass.ID == (int)eCharacterClass.MidgardRogue || player.CharacterClass.ID == (int)eCharacterClass.Mystic || player.CharacterClass.ID == (int)eCharacterClass.Seer || player.CharacterClass.ID == (int)eCharacterClass.Viking)
                    {
                        player.Out.SendMessage("There was no class selection to confirm. Please start over.", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        return true;
                    }
                }
            }

            if (str == "no" || str == "non")
            {
                if (player.CharacterClass.ID == (int)eCharacterClass.Acolyte || player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue || player.CharacterClass.ID == (int)eCharacterClass.Disciple || player.CharacterClass.ID == (int)eCharacterClass.Elementalist || player.CharacterClass.ID == (int)eCharacterClass.Fighter || player.CharacterClass.ID == (int)eCharacterClass.Mage || player.CharacterClass.ID == (int)eCharacterClass.Forester || player.CharacterClass.ID == (int)eCharacterClass.Guardian || player.CharacterClass.ID == (int)eCharacterClass.Magician || player.CharacterClass.ID == (int)eCharacterClass.Stalker || player.CharacterClass.ID == (int)eCharacterClass.Naturalist || player.CharacterClass.ID == (int)eCharacterClass.MidgardRogue || player.CharacterClass.ID == (int)eCharacterClass.Mystic || player.CharacterClass.ID == (int)eCharacterClass.Seer || player.CharacterClass.ID == (int)eCharacterClass.Viking)
                {
                    playerLastClassOffers.Remove(player);
                    player.Out.SendMessage("Come back later if you change your mind.", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    return true;
                }
            }

            string messageKey = null;
            if (str == "cleric" || str == "clerc" && player.CharacterClass.ID == (int)eCharacterClass.Acolyte)
            {
                messageKey = "AcolyteTrainer.Cleric.Explain";
                playerLastClassOffers[player] = "cleric";
            }
            else if ((str == "friar" || str == "moine") && player.CharacterClass.ID == (int)eCharacterClass.Acolyte)
            {
                messageKey = "AcolyteTrainer.Friar.Explain";
                playerLastClassOffers[player] = "friar";
            }
            else if ((str == "heretic" || str == "hérétique") && player.CharacterClass.ID == (int)eCharacterClass.Acolyte)
            {
                messageKey = "AcolyteTrainer.Heretic.Explain";
                playerLastClassOffers[player] = "heretic";
            }
            else if ((str == "infiltrator" || str == "sicaire") && player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue)
            {
                messageKey = "AlbionRogueTrainer.Infiltrator.Explain";
                playerLastClassOffers[player] = "infiltrator";
            }
            else if ((str == "minstrel" || str == "ménestrel") && player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue)
            {
                messageKey = "AlbionRogueTrainer.Minstrel.Explain";
                playerLastClassOffers[player] = "minstrel";
            }
            else if ((str == "scout" || str == "éclaireur") && player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue)
            {
                messageKey = "AlbionRogueTrainer.Scout.Explain";
                playerLastClassOffers[player] = "scout";
            }
            else if ((str == "necromancer" || str == "prêtre d'arawn") && player.CharacterClass.ID == (int)eCharacterClass.Disciple)
            {
                messageKey = "DiscipleTrainer.Necromancer.Explain";
                playerLastClassOffers[player] = "necromancer";
            }
            else if ((str == "theurgist" || str == "théurgiste") && player.CharacterClass.ID == (int)eCharacterClass.Elementalist)
            {
                messageKey = "ElementalistTrainer.Theurgist.Explain";
                playerLastClassOffers[player] = "theurgist";
            }
            else if ((str == "wizard" || str == "thaumaturge") && player.CharacterClass.ID == (int)eCharacterClass.Elementalist)
            {
                messageKey = "ElementalistTrainer.Wizard.Explain";
                playerLastClassOffers[player] = "wizard";
            }
            else if ((str == "armsman" || str == "maître d'armes") && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                messageKey = "FighterTrainer.Armsman.Explain";
                playerLastClassOffers[player] = "armsman";
            }
            else if ((str == "mercenary" || str == "mercenaire") && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                messageKey = "FighterTrainer.Mercenary.Explain";
                playerLastClassOffers[player] = "mercenary";
            }
            else if (str == "paladin" && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                messageKey = "FighterTrainer.Paladin.Explain";
                playerLastClassOffers[player] = "paladin";
            }
            else if ((str == "reaver" || str == "fléau d'arawn") && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                messageKey = "FighterTrainer.Reaver.Explain";
                playerLastClassOffers[player] = "reaver";
            }
            else if ((str == "mauler" || str == "kan-laresh") && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                messageKey = "Baseclass.Mauler.Explain";
                playerLastClassOffers[player] = "albmauler";
            }
            else if ((str == "cabalist" || str == "cabaliste") && player.CharacterClass.ID == (int)eCharacterClass.Mage)
            {
                messageKey = "MageTrainer.Cabalist.Explain";
                playerLastClassOffers[player] = "cabalist";
            }
            else if ((str == "sorcerer" || str == "sorcier") && player.CharacterClass.ID == (int)eCharacterClass.Mage)
            {
                messageKey = "MageTrainer.Sorcerer.Explain";
                playerLastClassOffers[player] = "sorcerer";
            }
            if ((str == "animist" || str == "animiste") && player.CharacterClass.ID == (int)eCharacterClass.Forester)
            {
                messageKey = "ForesterTrainer.Animist.Explain";
                playerLastClassOffers[player] = "animist";
            }
            if ((str == "valewalker" || str == "faucheur") && player.CharacterClass.ID == (int)eCharacterClass.Forester)
            {
                messageKey = "ForesterTrainer.Valewalker.Explain";
                playerLastClassOffers[player] = "valewalker";
            }
            if ((str == "hero" || str == "protecteur") && player.CharacterClass.ID == (int)eCharacterClass.Guardian)
            {
                messageKey = "GuardianTrainer.Hero.Explain";
                playerLastClassOffers[player] = "hero";
            }
            if (str == "champion" && player.CharacterClass.ID == (int)eCharacterClass.Guardian)
            {
                messageKey = "GuardianTrainer.Champion.Explain";
                playerLastClassOffers[player] = "champion";
            }
            if ((str == "blademaster" || str == "finelame") && player.CharacterClass.ID == (int)eCharacterClass.Guardian)
            {
                messageKey = "GuardianTrainer.Blademaster.Explain";
                playerLastClassOffers[player] = "blademaster";
            }
            if ((str == "mauler" || str == "kan-laresh") && player.CharacterClass.ID == (int)eCharacterClass.Guardian)
            {
                messageKey = "Baseclass.Mauler.Explain";
                playerLastClassOffers[player] = "hibmauler";
            }
            if (str == "eldritch" && player.CharacterClass.ID == (int)eCharacterClass.Magician)
            {
                messageKey = "MagicianTrainer.Eldritch.Explain";
                playerLastClassOffers[player] = "eldritch";
            }
            if ((str == "enchanter" || str == "enchanteur") && player.CharacterClass.ID == (int)eCharacterClass.Magician)
            {
                messageKey = "MagicianTrainer.Enchanter.Explain";
                playerLastClassOffers[player] = "enchanter";
            }
            if ((str == "mentalist" || str == "empathe") && player.CharacterClass.ID == (int)eCharacterClass.Magician)
            {
                messageKey = "MagicianTrainer.Mentalist.Explain";
                playerLastClassOffers[player] = "mentalist";
            }
            if ((str == "bainshee" || str == "banshee") && player.CharacterClass.ID == (int)eCharacterClass.Magician)
            {
                messageKey = "MagicianTrainer.Bainshee.Explain";
                playerLastClassOffers[player] = "bainshee";
            }
            if ((str == "bard" || str == "barde") && player.CharacterClass.ID == (int)eCharacterClass.Naturalist)
            {
                messageKey = "NaturalistTrainer.Bard.Explain";
                playerLastClassOffers[player] = "bard";
            }
            if ((str == "druid" || str == "druide") && player.CharacterClass.ID == (int)eCharacterClass.Naturalist)
            {
                messageKey = "NaturalistTrainer.Druid.Explain";
                playerLastClassOffers[player] = "druide";
            }
            if ((str == "warden" || str == "sentinelle") && player.CharacterClass.ID == (int)eCharacterClass.Naturalist)
            {
                messageKey = "NaturalistTrainer.Warden.Explain";
                playerLastClassOffers[player] = "warden";
            }
            if ((str == "ranger" && player.CharacterClass.ID == (int)eCharacterClass.Stalker)
            {
                messageKey = "StalkerTrainer.Ranger.Explain";
                playerLastClassOffers[player] = "ranger";
            }
            if ((str == "nightshade" || str == "ombre") && player.CharacterClass.ID == (int)eCharacterClass.Stalker)
            {
                messageKey = "StalkerTrainer.Nightshade.Explain";
                playerLastClassOffers[player] = "nightshade";
            }
            if ((str == "vampiir" || str == "séide de leanansidhe") && player.CharacterClass.ID == (int)eCharacterClass.Stalker)
            {
                messageKey = "StalkerTrainer.Vampiir.Explain";
                playerLastClassOffers[player] = "vampiir";
            }
            if ((str == "hunter" || str == "chasseur") && player.CharacterClass.ID == (int)eCharacterClass.MidgardRogue)
            {
                messageKey = "MidgardRogueTrainer.Hunter.Explain";
                playerLastClassOffers[player] = "hunter";
            }
            if ((str == "shadowblade" || str == "assassin") && player.CharacterClass.ID == (int)eCharacterClass.MidgardRogue)
            {
                messageKey = "MidgardRogueTrainer.Shadowblade.Explain";
                playerLastClassOffers[player] = "shadowblade";
            }
            if ((str == "runemaster" || str == "prêtre d'odin") && player.CharacterClass.ID == (int)eCharacterClass.Mystic)
            {
                messageKey = "MysticTrainer.Runemaster.Explain";
                playerLastClassOffers[player] = "runemaster";
            }
            if ((str == "spiritmaster" || str == "prêtre de hel") && player.CharacterClass.ID == (int)eCharacterClass.Mystic)
            {
                messageKey = "MysticTrainer.Spiritmaster.Explain";
                playerLastClassOffers[player] = "spiritmaster";
            }
            if ((str == "bonedancer" || str == "prêtre de bogdar") && player.CharacterClass.ID == (int)eCharacterClass.Mystic)
            {
                messageKey = "MystiTrainerc.Bonedancer.Explain";
                playerLastClassOffers[player] = "bonedancer";
            }
            if ((str == "warlock" || str == "helhaxa") && player.CharacterClass.ID == (int)eCharacterClass.Mystic)
            {
                messageKey = "MysticTrainer.Warlock.Explain";
                playerLastClassOffers[player] = "warlock";
            }
            if ((str == "shaman" || str == "chaman") && player.CharacterClass.ID == (int)eCharacterClass.Seer)
            {
                messageKey = "SeerTrainer.Shaman.Explain";
                playerLastClassOffers[player] = "shaman";
            }
            if ((str == "healer" || str == "guérisseur") && player.CharacterClass.ID == (int)eCharacterClass.Seer)
            {
                messageKey = "SeerTrainer.Healer.Explain";
                playerLastClassOffers[player] = "healer";
            }
            if ((str == "warrior" || str == "guerrier") && player.CharacterClass.ID == (int)eCharacterClass.Viking)
            {
                messageKey = "VikingTrainer.Warrior.Explain";
                playerLastClassOffers[player] = "warrior";
            }
            if (str == "berserker" && player.CharacterClass.ID == (int)eCharacterClass.Viking)
            {
                messageKey = "VikingTrainer.Berserker.Explain";
                playerLastClassOffers[player] = "berserker";
            }
            if (str == "skald" && player.CharacterClass.ID == (int)eCharacterClass.Viking)
            {
                messageKey = "VikingTrainer.Skald.Explain";
                playerLastClassOffers[player] = "skald";
            }
            if (str == "thane" && player.CharacterClass.ID == (int)eCharacterClass.Viking)
            {
                messageKey = "VikingTrainer.Thane.Explain";
                playerLastClassOffers[player] = "thane";
            }
            if ((str == "savage" || str == "sauvage") && player.CharacterClass.ID == (int)eCharacterClass.Viking)
            {
                messageKey = "VikingTrainer.Savage.Explain";
                playerLastClassOffers[player] = "savage";
            }
            if (str == "valkyrie" && player.CharacterClass.ID == (int)eCharacterClass.Viking)
            {
                messageKey = "VikingTrainer.Valkyrie.Explain";
                playerLastClassOffers[player] = "valkyrie";
            }
            if ((str == "mauler" || str == "kan-laresh") && player.CharacterClass.ID == (int)eCharacterClass.Viking)
            {
                messageKey = "Baseclass.Mauler.Explain";
                playerLastClassOffers[player] = "midmauler";
            }


            // For Choosing a weapon:
            if ((str == "slashing" || str == "tranchante") && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                if (playerWeaponOffers.ContainsKey(player))
                {
                    HandleWeaponChoiceResponse(player, true);
                    playerWeaponOffers.Remove(player);
                }
                playerWeaponOffers[player] = "slashing";
            }
            if ((str == "crushing" || str == "contondante") && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                if (playerWeaponOffers.ContainsKey(player))
                {
                    HandleWeaponChoiceResponse(player, true);
                    playerWeaponOffers.Remove(player);
                }
                playerWeaponOffers[player] = "crushing";
            }
            if ((str == "thrusting" || str == "perforante") && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                if (playerWeaponOffers.ContainsKey(player))
                {
                    HandleWeaponChoiceResponse(player, true);
                    playerWeaponOffers.Remove(player);
                }
                playerWeaponOffers[player] = "thrusting";
            }
            if ((str == "polearms" || str == "arme d'hast") && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                if (playerWeaponOffers.ContainsKey(player))
                {
                    HandleWeaponChoiceResponse(player, true);
                    playerWeaponOffers.Remove(player);
                }
                playerWeaponOffers[player] = "polearms";
            }
            if ((str == "two handed" || str == "deux mains") && player.CharacterClass.ID == (int)eCharacterClass.Fighter)
            {
                if (playerWeaponOffers.ContainsKey(player))
                {
                    HandleWeaponChoiceResponse(player, true);
                    playerWeaponOffers.Remove(player);
                }
                playerWeaponOffers[player] = "two handed";
            }

            else if (HandleItemRequests(str, player))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(messageKey) && (player.CharacterClass.ID == (int)eCharacterClass.Acolyte || player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue || player.CharacterClass.ID == (int)eCharacterClass.Disciple || player.CharacterClass.ID == (int)eCharacterClass.Elementalist || player.CharacterClass.ID == (int)eCharacterClass.Fighter || player.CharacterClass.ID == (int)eCharacterClass.Mage || player.CharacterClass.ID == (int)eCharacterClass.Forester || player.CharacterClass.ID == (int)eCharacterClass.Guardian || player.CharacterClass.ID == (int)eCharacterClass.Magician || player.CharacterClass.ID == (int)eCharacterClass.Stalker || player.CharacterClass.ID == (int)eCharacterClass.Naturalist || player.CharacterClass.ID == (int)eCharacterClass.MidgardRogue || player.CharacterClass.ID == (int)eCharacterClass.Mystic || player.CharacterClass.ID == (int)eCharacterClass.Seer || player.CharacterClass.ID == (int)eCharacterClass.Viking))
            {
                string message = LanguageMgr.GetTranslation(player.Client.Account.Language, messageKey, this.Name) + "\r\n\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.ChooseClass");
                player.Out.SendMessage(message, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return true;
            }

            return true;

            #region Respecs
            if (str == "Realmrank")
            {
                player.Out.SendMessage("You want to [delete] ?", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (str == "delete")
            {
                if (player.Level == 50)
                {
                    player.RealmPoints = 0;
                    player.RespecRealm();
                    player.RealmLevel = 0;
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(0, false);
                    player.Out.SendMessage("Now u can choose your RR again!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }


                return false;
            }
            if (str == "Realmranks")
            {
                player.Out.SendMessage("You can choose for: [RR1],[RR2],[RR3],[RR4],[RR5],[RR6],[RR7],[RR8],[RR9],[RR10],[RR11],[RR12] and [RR13]", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                player.Out.SendMessage("Which Rank you want to be ?", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (str == "RR1")
            {
                if (player.Level == 50)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(0, false);
                    player.Out.SendMessage("You will not get any Realmpoints for RR1!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }


                return false;
            }
            if (str == "RR2")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(7126, false);
                    player.Out.SendMessage("You got RR2 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR3")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(61755, false);
                    player.Out.SendMessage("You got RR3 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }

            if (str == "RR4")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(213880, false);
                    player.Out.SendMessage("You got RR4 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR5")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(513550, false);
                    player.Out.SendMessage("You got RR5 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR6")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(1010630, false);
                    player.Out.SendMessage("You got RR6 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR7")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(1755255, false);
                    player.Out.SendMessage("You got RR6 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR8")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(2797380, false);
                    player.Out.SendMessage("You got RR8 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR9")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(4187010, false);
                    player.Out.SendMessage("You got RR9 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR10")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(5974130, false);
                    player.Out.SendMessage("You got RR10 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR11")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(8208760, false);
                    player.Out.SendMessage("You got RR11 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR12")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(23308100, false);
                    player.Out.SendMessage("You got RR12 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
                return true;
            }
            if (str == "RR13")
            {
                if (player.Level == 50 && player.RealmPoints == 0)
                {
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(66181550, false);
                    player.Out.SendMessage("You got RR13 and cannot change it anymore!", eChatType.CT_System, eChatLoc.CL_PopupWindow);

                    return false;
                }
                return true;
            }
            #endregion RealmPoints

            #region Respecs

            if (str == "Respecs")
            {
                player.Out.SendMessage("You currently have:\n" + player.RespecAmountAllSkill + " Full     skill respecs\n" + player.RespecAmountSingleSkill + " Single skill respecs\n" + player.RespecAmountRealmSkill + " Realm skill respecs\n" + player.RespecAmountChampionSkill + " Champion skill respecs", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                player.Out.SendMessage("Which would you like to buy:\n[Full], [Single], [Realm], [MasterLevel] or [Champion]?", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (str == "Full")
            {
                //first check if the player has too many
                if (player.RespecAmountAllSkill >= 5)
                {
                    player.Out.SendMessage("You already have " + player.RespecAmountAllSkill + " Full skill respecs, to use them simply target me and type /respec ALL", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }
                //TODO next, check that the player can afford it

                //send dialog to player to confirm the purchase of the full skill respec
                player.Out.SendCustomDialog("Full Skill Respec price is: FREE Do you really want to buy one?", new CustomDialogResponse(RespecFullDialogResponse));

                return true;
            }
            if (str == "Single")
            {
                //first check if the player has too many
                if (player.RespecAmountSingleSkill >= 5)
                {
                    player.Out.SendMessage("You already have " + player.RespecAmountAllSkill + " Single skill Respecs, to use them simply target me and type /respec <line>", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }
                //TODO next, check that the player can afford it

                //send dialog to player to confirm the purchase of the single skill respec
                player.Out.SendCustomDialog("Single Skill Respec price is: FREE Do you really want to buy one?", new CustomDialogResponse(RespecSingleDialogResponse));

                return true;
            }
            if (str == "Realm")
            {
                //first check if the player has too many
                if (player.RespecAmountRealmSkill >= 5)
                {
                    player.Out.SendMessage("You already have " + player.RespecAmountRealmSkill + " Realm skill respecs, to use them simply target me and type /respec Realm", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }
                //TODO next, check that the player can afford it

                //send dialog to player to confirm the purchase of the full skill respec
                player.Out.SendCustomDialog("Realm Skill Respec price is: FREE Do you really want to buy one?", new CustomDialogResponse(RespecRealmDialogResponse));

                return true;
            }

            if (str == "ChampionLevel")
            {
                if (player.RespecAmountChampionSkill >= 5)
                {
                    player.Out.SendMessage("You already have " + player.RespecAmountChampionSkill + " Champion skill respecs, to use them please visit the Champion Level Master.", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }

                //TODO next, check that the player can afford it

                //send dialog to player to confirm the purchase of the Champion skill respec token
                player.Out.SendCustomDialog("CL Skill Respec price is: FREE Do you really want to buy one?", new CustomDialogResponse(RespecChampionDialogResponse));

                return true;
            }



            #endregion Respec

            player.Out.SendTrainerWindow();
            return true;
        }

        private bool HandleItemRequests(string str, GamePlayer player)
        {
            if (str.Contains("practice weapon") || str.Contains("arme d'entraînement"))
            {
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID1, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Acolyte))
                {
                    player.ReceiveItem(this, PRACTICE_WEAPON_ID1, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID2, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Fighter))
                {
                    player.ReceiveItem(this, PRACTICE_WEAPON_ID2, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID3, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Guardian))
                {
                    player.ReceiveItem(this, PRACTICE_WEAPON_ID3, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID4, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Naturalist))
                {
                    player.ReceiveItem(this, PRACTICE_WEAPON_ID4, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID5, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.MidgardRogue))
                {
                    player.ReceiveItem(this, PRACTICE_WEAPON_ID5, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID6, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Seer))
                {
                    player.ReceiveItem(this, PRACTICE_WEAPON_ID6, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID7, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Viking))
                {
                    player.ReceiveItem(this, PRACTICE_WEAPON_ID7, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID8, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue))
                {
                    player.ReceiveItem(this, PRACTICE_WEAPON_ID8, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID9, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Stalker))
                {
                    player.ReceiveItem(this, PRACTICE_WEAPON_ID9, eInventoryActionType.Other);
                }
            }

            if (str.Contains("training shield") || str.Contains("bouclier d'entraînement"))
            {
                if ((player.Inventory.CountItemTemplate(PRACTICE_SHIELD_ID1, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Fighter || player.CharacterClass.ID == (int)eCharacterClass.Viking || player.CharacterClass.ID == (int)eCharacterClass.Seer))
                {
                    player.ReceiveItem(this, PRACTICE_SHIELD_ID1, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_SHIELD_ID2, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Naturalist || player.CharacterClass.ID == (int)eCharacterClass.Guardian))
                {
                    player.ReceiveItem(this, PRACTICE_SHIELD_ID2, eInventoryActionType.Other);
                }
            }

            if (str.Contains("practice staff") || str.Contains("bâton d'entraînement"))
            {
                if ((player.Inventory.CountItemTemplate(PRACTICE_STAFF_ID1, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Magician || player.CharacterClass.ID == (int)eCharacterClass.Forester))
                {
                    player.ReceiveItem(this, PRACTICE_STAFF_ID1, eInventoryActionType.Other);
                }
                if ((player.Inventory.CountItemTemplate(PRACTICE_STAFF_ID2, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0) && (player.CharacterClass.ID == (int)eCharacterClass.Mage || player.CharacterClass.ID == (int)eCharacterClass.Mystic || player.CharacterClass.ID == (int)eCharacterClass.Elementalist || player.CharacterClass.ID == (int)eCharacterClass.Disciple))
                {
                    player.ReceiveItem(this, PRACTICE_STAFF_ID2, eInventoryActionType.Other);
                }
            }
            return false;
        }

        private void HandleClassSelectionResponse(GamePlayer player, bool accept)
        {
            if (accept)
            {
                string classOffer = playerLastClassOffers[player];
                if (CanPromotePlayer(player))
                // ALBION Classes
                    if (classOffer == "cleric")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Cleric, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID9, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ClericTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, CLERARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "friar")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Friar, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID10, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "FriarTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, FRIARARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "heretic")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Heretic, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID11, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HereticTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, HEREARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "infiltrator")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Infiltrator, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID3, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "InfiltratorTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, INFIARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "minstrel")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Minstrel, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon3", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID12, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MinstrelTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, MINSARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "scout")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Scout, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID14, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ScoutTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, SCOUARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "necromancer")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Necromancer, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID13, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NecromancerTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, NECRARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "theurgist")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Theurgist, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID16, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TheurgistTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, THEUARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "wizard")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Wizard, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID17, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WizardTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, WIZARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "armsman")
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ArmsmanTrainer.Interact.Text4", this.Name, player.CharacterClass.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else if (classOffer == "mercenary")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Mercenary, LanguageMgr.GetTranslation(player.Client.Account.Language, "MercenaryTrainer.WhisperReceive.Text1", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MercenaryTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, MERCARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "paladin")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Paladin, LanguageMgr.GetTranslation(player.Client.Account.Language, "PaladinTrainer.WhisperReceive.Text1", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PaladinTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, PALAARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "reaver")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Reaver, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID11, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ReaverTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, REAVARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "albmauler")
                    {
                        PromotePlayer(player, (int)eCharacterClass.MaulerAlb, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID7, eInventoryActionType.Other);
                        player.ReceiveItem(this, ALBWEAPON_ID7, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerAlbTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, ALBMAULARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "cabalist")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Cabalist, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID8, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "CabalistTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, CABAARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "sorcerer")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Sorcerer, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, ALBWEAPON_ID15, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SorcererTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, SORCARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    // HIBERNIA Classes
                    else if (classOffer == "animist")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Animist, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AnimistTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, ANIMARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "valewalker")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Valewalker, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID16, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ValewalkerTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, ANIMARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "hero")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Hero, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID14, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HeroTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, HEROARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "champion")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Champion, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID11, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ChampionTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, CHAMARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "blademaster")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Blademaster, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID11, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BlademasterTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, BLADARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "hibmauler")
                    {
                        PromotePlayer(player, (int)eCharacterClass.MaulerHib, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID7, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerHibTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, HIBMAULARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "eldritch")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Eldritch, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID5, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "EldritchTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, ELDRARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "enchanter")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Enchanter, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID6, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "EnchanterTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, ENCHARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "mentalist")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Mentalist, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID9, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MentalistTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, MENTARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "bainshee")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Bainshee, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID2, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BainsheeTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, BAINARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "bard")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Bard, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon3 ", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID2, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BardTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, BARDARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "druid")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Druid, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID4, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DruidTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, DRUIARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "warden")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Warden, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID10, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WardenTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, WARDARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "ranger")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Ranger, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID15, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RangerTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, RANGARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "nightshade")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Nightshade, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID12, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NightshadeTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, NIGHARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "vampiir")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Vampiir, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, HIBWEAPON_ID12, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VampiirTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, VAMPARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "hunter")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Hunter, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID9, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HunterTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, HUNTARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "shadowblade")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Shadowblade, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID13, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ShadowbladeTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, SHADOARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "runemaster")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Runemaster, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID12, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RunemasterTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, RUNEARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "spiritmaster")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Spiritmaster, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID15, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpiritmasterTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, SPIRARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "bonedancer")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Bonedancer, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID7, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BonedancerTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, BONEARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "warlock")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Warlock, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID16, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WarlockTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, WARLARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "shaman")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Shaman, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID14, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ShamanTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, SHAMARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "healer")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Healer, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID8, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HealerTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, HEALARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "warrior")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Warrior, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID3, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WarriorTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, WARRARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "berserker")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Berserker, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID3, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BerserkerTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, BERZARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "skald")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Skald, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID3, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SkaldTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, SKALARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "thane")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Thane, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID2, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ThaneTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, THANARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "savage")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Savage, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID6, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SavageTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, SAVAARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "valkyrie")
                    {
                        PromotePlayer(player, (int)eCharacterClass.Valkyrie, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon1 ", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID5, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ValkyrieTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, VALKARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else if (classOffer == "midmauler")
                    {
                        PromotePlayer(player, (int)eCharacterClass.MaulerMid, LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.GiveWeapon2", this.Name), null);
                        player.ReceiveItem(this, MIDWEAPON_ID10, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerMidTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        player.ReceiveItem(this, MIDMAULARMOR_ID1, eInventoryActionType.Other);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
            }
            else
            {
                if (player.CharacterClass.ID == (int)eCharacterClass.Acolyte || player.CharacterClass.ID == (int)eCharacterClass.AlbionRogue || player.CharacterClass.ID == (int)eCharacterClass.Disciple || player.CharacterClass.ID == (int)eCharacterClass.Elementalist || player.CharacterClass.ID == (int)eCharacterClass.Fighter || player.CharacterClass.ID == (int)eCharacterClass.Mage || player.CharacterClass.ID == (int)eCharacterClass.Forester || player.CharacterClass.ID == (int)eCharacterClass.Guardian || player.CharacterClass.ID == (int)eCharacterClass.Magician || player.CharacterClass.ID == (int)eCharacterClass.Stalker || player.CharacterClass.ID == (int)eCharacterClass.Naturalist || player.CharacterClass.ID == (int)eCharacterClass.MidgardRogue || player.CharacterClass.ID == (int)eCharacterClass.Mystic || player.CharacterClass.ID == (int)eCharacterClass.Seer || player.CharacterClass.ID == (int)eCharacterClass.Viking)
                {
                    player.Out.SendMessage("Come back later if you change your mind.", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
            }
        }

        private void HandleWeaponChoiceResponse(GamePlayer player, bool choice)
        {
            if (choice)
            {
                string classOffer = playerLastClassOffers[player];
                string weaponOffer = playerWeaponOffers[player];
                if (weaponOffer == "slashing")
                {
                    GiveWeaponAndArmor(player, ALBWEAPON_ID1, ARMSARMOR_ID1);
                }
                if (weaponOffer == "crushing")
                {
                    GiveWeaponAndArmor(player, ALBWEAPON_ID2, ARMSARMOR_ID1);
                }
                if (weaponOffer == "thrusting")
                {
                    GiveWeaponAndArmor(player, ALBWEAPON_ID3, ARMSARMOR_ID1);
                }
                if (classOffer == "armsman" && weaponOffer == "polearms")
                {
                    GiveWeaponAndArmor(player, ALBWEAPON_ID4, ARMSARMOR_ID1);
                }
                if (weaponOffer == "two handed")
                {
                    GiveWeaponAndArmor(player, ALBWEAPON_ID5, ARMSARMOR_ID1);
                }
            }
        }

        private void GiveWeaponAndArmor(GamePlayer player, string weaponId, string armorId)
        {
            // Logic to promote player if needed
            // Example:
            // if (classOffer == "armsman")
            // {
            // Promote logic here
            // }
            PromotePlayer(player, (int)eCharacterClass.Armsman, LanguageMgr.GetTranslation(player.Client.Account.Language, "ArmsmanTrainer.WhisperReceive.Text4", player.GetName(0, false)), null);

            player.ReceiveItem(this, weaponId, eInventoryActionType.Other); // Adjust as needed
            PromotePlayer(player, (int)eCharacterClass.Armsman, LanguageMgr.GetTranslation(player.Client.Account.Language, "ArmsmanTrainer.WhisperReceive.Text1", player.GetName(0, false)), null);

            player.ReceiveItem(this, armorId, eInventoryActionType.Other); // Adjust as needed
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ArmsmanTrainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.CharacterClass.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        #region TrainSpecLine
        public void TrainSpecLine(GamePlayer player, string line, int points)
        {
            if (player == null)
                return;

            if (!(player.TargetObject is GameTrainer))
            {
                player.Out.SendMessage("You must have your trainer targetted to be trained in a specialization line.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if ((points <= 0) || (points >= 51))
            {
                player.Out.SendMessage("An Error occurred, there was an invalid amount to train: " + points + " points is not valid!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            int target = points;
            Specialization spec = player.GetSpecialization(line);
            if (spec == null)
            {
                player.Out.SendMessage("An Error occurred, there was an invalid line name: " + line + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            int current = spec.Level;
            if (current >= player.Level)
            {
                player.Out.SendMessage("You can't train in " + line + " again this level.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (points <= current)
            {
                player.Out.SendMessage("You have already trained this amount in " + line + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            target = target - current;
            ushort skillspecialtypoints = 0;
            int speclevel = 0;
            bool changed = false;
            for (int i = 0; i < target; i++)
            {
                if (spec.Level + speclevel >= player.Level)
                {
                    player.Out.SendMessage("You can't train in " + line + " again this level!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;
                }

                if ((player.SkillSpecialtyPoints + player.GetAutoTrainPoints(spec, 3)) - skillspecialtypoints >= (spec.Level + speclevel) + 1)
                {
                    changed = true;
                    skillspecialtypoints += (ushort)((spec.Level + speclevel) + 1);
                    if (spec.Level + speclevel < player.Level / 4 && player.GetAutoTrainPoints(spec, 4) != 0)
                        skillspecialtypoints -= (ushort)((spec.Level + speclevel) + 1);
                    speclevel++;
                }
                else
                {
                    player.Out.SendMessage("That specialization costs " + (spec.Level + 1) + " specialization points!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    player.Out.SendMessage("You don't have that many specialization points left for this level.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;
                }
            }
            if (changed)
            {
                //            player.SkillSpecialtyPoints -= skillspecialtypoints;
                spec.Level += speclevel;
                player.OnSkillTrained(spec);
                player.Out.SendUpdatePoints();
                player.Out.SendTrainerWindow();
                player.Out.SendMessage("You now have " + points + " points in the " + line + " line!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
            return;

        }
        #endregion TrainSpecLine
        #region RespecDialogResponse
        protected void RespecFullDialogResponse(GamePlayer player, byte response)
        {
            if (response != 0x01) return; //declined
            player.RespecAmountAllSkill++;
            player.Out.SendMessage("You just bought a Full skill respec!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage("Target the trainer and type /respec ALL to use it!", eChatType.CT_System, eChatLoc.CL_SystemWindow);

        }
        protected void RespecSingleDialogResponse(GamePlayer player, byte response)
        {
            if (response != 0x01) return; //declined
            player.RespecAmountSingleSkill++;
            player.Out.SendMessage("You just bought a Single skill respec!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage("Target the trainer and type /respec <line> to use it!", eChatType.CT_System, eChatLoc.CL_SystemWindow);


        }
        protected void RespecRealmDialogResponse(GamePlayer player, byte response)
        {
            if (response != 0x01) return; //declined
            player.RespecAmountRealmSkill++;
            player.Out.SendMessage("You just bought a Realm skill respec!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage("Target the trainer and type /respec Realm to use it!", eChatType.CT_System, eChatLoc.CL_SystemWindow);


        }

        protected void RespecChampionDialogResponse(GamePlayer player, byte response)
        {
            if (response != 0x01) return; //declined
            player.RespecAmountChampionSkill++;
            player.Out.SendMessage("You just bought a Champion skill respec!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage("Please visit the Champion Level master to use it!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (source == null || item == null) return false;

            GamePlayer player = source as GamePlayer;

            if (player.Level >= 10 && player.Level < 15)
            {
                if (item.Id_nb == CLERARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Cleric)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ClericTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(CLERARMOR_ID2, player);
                }
                if (item.Id_nb == FRIARARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Friar)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "FriarTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(FRIARARMOR_ID2, player);
                }
                if (item.Id_nb == HEREARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Heretic)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HereticTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HEREARMOR_ID2, player);
                }
                if (item.Id_nb == INFIARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Infiltrator)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "InfiltratorTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(INFIARMOR_ID2, player);
                }
                if (item.Id_nb == MINSARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Minstrel)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MinstrelTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MINSARMOR_ID2, player);
                }
                if (item.Id_nb == SCOUARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Scout)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ScoutTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SCOUARMOR_ID2, player);
                }
                if (item.Id_nb == NECRARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Necromancer)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NecromancerTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(NECRARMOR_ID2, player);
                }
                if (item.Id_nb == THEUARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Theurgist)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TheurgistTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(THEUARMOR_ID2, player);
                }
                if (item.Id_nb == WIZARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Wizard)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WizardTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WIZARMOR_ID2, player);
                }
                if (item.Id_nb == ARMSARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Armsman)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ArmsmanTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ARMSARMOR_ID2, player);
                }
                if (item.Id_nb == MERCARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Mercenary)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MercenaryTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MERCARMOR_ID2, player);
                }
                if (item.Id_nb == PALAARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Paladin)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PaladinTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(PALAARMOR_ID2, player);
                }
                if (item.Id_nb == ALBMAULARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.MaulerAlb)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerAlbTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ALBMAULARMOR_ID2, player);
                }
                if (item.Id_nb == CABAARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Cabalist)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "CabalistTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(CABAARMOR_ID2, player);
                }
                if (item.Id_nb == SORCARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Sorcerer)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SorcererTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SORCARMOR_ID2, player);
                }
                if (item.Id_nb == REAVARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Reaver)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ReaverTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(REAVARMOR_ID2, player);
                }
                if (item.Id_nb == ANIMARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Animist)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AnimistTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ANIMARMOR_ID2, player);
                }
                if (item.Id_nb == BAINARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Bainshee)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BainsheeTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BAINARMOR_ID2, player);
                }
                if (item.Id_nb == BARDARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Bard)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BardTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BARDARMOR_ID2, player);
                }
                if (item.Id_nb == BLADARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Blademaster)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BlademasterTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BLADARMOR_ID2, player);
                }
                if (item.Id_nb == CHAMARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Champion)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ChampionTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(CHAMARMOR_ID2, player);
                }
                if (item.Id_nb == DRUIARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Druid)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DruidTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(DRUIARMOR_ID2, player);
                }
                if (item.Id_nb == ELDRARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Eldritch)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "EldritchTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ELDRARMOR_ID2, player);
                }
                if (item.Id_nb == ENCHARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Enchanter)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "EnchanterTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ENCHARMOR_ID2, player);
                }
                if (item.Id_nb == HEROARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Hero)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HeroTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HEROARMOR_ID2, player);
                }
                if (item.Id_nb == HIBMAULARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.MaulerHib)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerHibTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HIBMAULARMOR_ID2, player);
                }
                if (item.Id_nb == MENTARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Mentalist)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MentalistTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MENTARMOR_ID2, player);
                }
                if (item.Id_nb == NIGHARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Nightshade)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NightshadeTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(NIGHARMOR_ID2, player);
                }
                if (item.Id_nb == RANGARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Ranger)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RangerTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(RANGARMOR_ID2, player);
                }
                if (item.Id_nb == VALEARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Valewalker)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ValewalkerTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(VALEARMOR_ID2, player);
                }
                if (item.Id_nb == VAMPARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Vampiir)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VampiirTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(VAMPARMOR_ID2, player);
                }
                if (item.Id_nb == WARDARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Warden)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WardenTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WARDARMOR_ID2, player);
                }
                if (item.Id_nb == BERZARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Berserker)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BerserkerTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BERZARMOR_ID2, player);
                }
                if (item.Id_nb == BONEARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Bonedancer)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BonedancerTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BONEARMOR_ID2, player);
                }
                if (item.Id_nb == HEALARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Healer)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HealerTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HEALARMOR_ID2, player);
                }
                if (item.Id_nb == HUNTARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Hunter)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HunterTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HUNTARMOR_ID2, player);
                }
                if (item.Id_nb == MIDMAULARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.MaulerMid)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerMidTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MIDMAULARMOR_ID2, player);
                }
                if (item.Id_nb == RUNEARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Runemaster)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RunemasterTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(RUNEARMOR_ID2, player);
                }
                if (item.Id_nb == SAVAARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Savage)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SavageTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SAVAARMOR_ID2, player);
                }
                if (item.Id_nb == SHADOARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Shadowblade)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ShadowbladeTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SHADOARMOR_ID2, player);
                }
                if (item.Id_nb == SHAMARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Shaman)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ShamanTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SHAMARMOR_ID2, player);
                }
                if (item.Id_nb == SKALARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Skald)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SkaldTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SKALARMOR_ID2, player);
                }
                if (item.Id_nb == SPIRARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Spiritmaster)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpiritmasterTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SPIRARMOR_ID2, player);
                }
                if (item.Id_nb == THANARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Thane)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ThaneTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(THANARMOR_ID2, player);
                }
                if (item.Id_nb == VALKARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Valkyrie)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ValkyrieTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(VALKARMOR_ID2, player);
                }
                if (item.Id_nb == WARLARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Warlock)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WarlockTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WARLARMOR_ID2, player);
                }
                if (item.Id_nb == WARRARMOR_ID1 && player.CharacterClass.ID == (int)eCharacterClass.Warrior)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WarriorTrainer.ReceiveArmor.Text2", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WARRARMOR_ID2, player);
                }
            }
            if (player.Level >= 15 && player.Level < 20)
            {
                if (item.Id_nb == CLERARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ClericTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(CLERARMOR_ID3, player);
                }
                if (item.Id_nb == FRIARARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "FriarTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(FRIARARMOR_ID3, player);
                }
                if (item.Id_nb == HEREARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HereticTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HEREARMOR_ID3, player);
                }
                if (item.Id_nb == INFIARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "InfiltratorTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(INFIARMOR_ID3, player);
                }
                if (item.Id_nb == MINSARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MinstrelTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MINSARMOR_ID3, player);
                }
                if (item.Id_nb == SCOUARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ScoutTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SCOUARMOR_ID3, player);
                }
                if (item.Id_nb == NECRARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NecromancerTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(NECRARMOR_ID3, player);
                }
                if (item.Id_nb == THEUARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TheurgistTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(THEUARMOR_ID3, player);
                }
                if (item.Id_nb == WIZARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WizardTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WIZARMOR_ID3, player);
                }
                if (item.Id_nb == ARMSARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ArmsmanTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ARMSARMOR_ID3, player);
                }
                if (item.Id_nb == MERCARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MercenaryTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MERCARMOR_ID3, player);
                }
                if (item.Id_nb == PALAARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PaladinTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(PALAARMOR_ID3, player);
                }
                if (item.Id_nb == ALBMAULARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerAlbTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ALBMAULARMOR_ID3, player);
                }
                if (item.Id_nb == CABAARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "CabalistTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(CABAARMOR_ID3, player);
                }
                if (item.Id_nb == SORCARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SorcererTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SORCARMOR_ID3, player);
                }
                if (item.Id_nb == REAVARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ReaverTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(REAVARMOR_ID3, player);
                }
                if (item.Id_nb == ANIMARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AnimistTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ANIMARMOR_ID3, player);
                }
                if (item.Id_nb == BAINARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BainsheeTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BAINARMOR_ID3, player);
                }
                if (item.Id_nb == BARDARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BardTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BARDARMOR_ID3, player);
                }
                if (item.Id_nb == BLADARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BlademasterTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BLADARMOR_ID3, player);
                }
                if (item.Id_nb == CHAMARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ChampionTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(CHAMARMOR_ID3, player);
                }
                if (item.Id_nb == DRUIARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DruidTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(DRUIARMOR_ID3, player);
                }
                if (item.Id_nb == ELDRARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "EldritchTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ELDRARMOR_ID3, player);
                }
                if (item.Id_nb == ENCHARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "EnchanterTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ENCHARMOR_ID3, player);
                }
                if (item.Id_nb == HEROARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HeroTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HEROARMOR_ID3, player);
                }
                if (item.Id_nb == HIBMAULARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerHibTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HIBMAULARMOR_ID3, player);
                }
                if (item.Id_nb == MENTARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MentalistTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MENTARMOR_ID3, player);
                }
                if (item.Id_nb == NIGHARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NightshadeTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(NIGHARMOR_ID3, player);
                }
                if (item.Id_nb == RANGARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RangerTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(RANGARMOR_ID3, player);
                }
                if (item.Id_nb == VALEARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ValewalkerTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(VALEARMOR_ID3, player);
                }
                if (item.Id_nb == VAMPARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VampiirTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(VAMPARMOR_ID3, player);
                }
                if (item.Id_nb == WARDARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WardenTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WARDARMOR_ID3, player);
                }
                if (item.Id_nb == BERZARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BerserkerTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BERZARMOR_ID3, player);
                }
                if (item.Id_nb == BONEARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BonedancerTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BONEARMOR_ID3, player);
                }
                if (item.Id_nb == HEALARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HealerTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HEALARMOR_ID3, player);
                }
                if (item.Id_nb == HUNTARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HunterTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HUNTARMOR_ID3, player);
                }
                if (item.Id_nb == MIDMAULARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerMidTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MIDMAULARMOR_ID3, player);
                }
                if (item.Id_nb == RUNEARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RunemasterTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(RUNEARMOR_ID3, player);
                }
                if (item.Id_nb == SAVAARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SavageTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SAVAARMOR_ID3, player);
                }
                if (item.Id_nb == SHADOARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ShadowbladeTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SHADOARMOR_ID3, player);
                }
                if (item.Id_nb == SHAMARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ShamanTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SHAMARMOR_ID3, player);
                }
                if (item.Id_nb == SKALARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SkaldTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SKALARMOR_ID3, player);
                }
                if (item.Id_nb == SPIRARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpiritmasterTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SPIRARMOR_ID3, player);
                }
                if (item.Id_nb == THANARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ThaneTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(THANARMOR_ID3, player);
                }
                if (item.Id_nb == VALKARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ValkyrieTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(VALKARMOR_ID3, player);
                }
                if (item.Id_nb == WARLARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WarlockTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WARLARMOR_ID3, player);
                }
                if (item.Id_nb == WARRARMOR_ID2)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WarriorTrainer.ReceiveArmor.Text3", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WARRARMOR_ID3, player);
                }
            }
            if (player.Level >= 20 && player.Level < 50)
            {
                if (item.Id_nb == CLERARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ClericTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(CLERARMOR_ID4, player);
                }
                if (item.Id_nb == FRIARARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "FriarTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(FRIARARMOR_ID4, player);
                }
                if (item.Id_nb == HEREARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HereticTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HEREARMOR_ID4, player);
                }
                if (item.Id_nb == INFIARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "InfiltratorTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(INFIARMOR_ID4, player);
                }
                if (item.Id_nb == MINSARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MinstrelTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MINSARMOR_ID4, player);
                }
                if (item.Id_nb == SCOUARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ScoutTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SCOUARMOR_ID4, player);
                }
                if (item.Id_nb == NECRARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NecromancerTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(NECRARMOR_ID4, player);
                }
                if (item.Id_nb == THEUARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TheurgistTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(THEUARMOR_ID4, player);
                }
                if (item.Id_nb == WIZARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WizardTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WIZARMOR_ID4, player);
                }

                if (item.Id_nb == ARMSARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ArmsmanTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ARMSARMOR_ID4, player);
                }
                if (item.Id_nb == MERCARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MercenaryTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MERCARMOR_ID4, player);
                }
                if (item.Id_nb == PALAARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PaladinTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(PALAARMOR_ID4, player);
                }
                if (item.Id_nb == ALBMAULARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerAlbTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ALBMAULARMOR_ID4, player);
                }
                if (item.Id_nb == CABAARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "CabalistTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(CABAARMOR_ID4, player);
                }
                if (item.Id_nb == SORCARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SorcererTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SORCARMOR_ID4, player);
                }
                if (item.Id_nb == REAVARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ReaverTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(REAVARMOR_ID4, player);
                }



                if (item.Id_nb == ANIMARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AnimistTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ANIMARMOR_ID4, player);
                }
                if (item.Id_nb == BAINARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BainsheeTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BAINARMOR_ID4, player);
                }
                if (item.Id_nb == BARDARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BardTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BARDARMOR_ID4, player);
                }
                if (item.Id_nb == BLADARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BlademasterTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BLADARMOR_ID4, player);
                }
                if (item.Id_nb == CHAMARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ChampionTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(CHAMARMOR_ID4, player);
                }
                if (item.Id_nb == DRUIARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DruidTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(DRUIARMOR_ID4, player);
                }
                if (item.Id_nb == ELDRARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "EldritchTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ELDRARMOR_ID4, player);
                }
                if (item.Id_nb == ENCHARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "EnchanterTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(ENCHARMOR_ID4, player);
                }
                if (item.Id_nb == HEROARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HeroTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HEROARMOR_ID4, player);
                }
                if (item.Id_nb == HIBMAULARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerHibTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HIBMAULARMOR_ID4, player);
                }
                if (item.Id_nb == MENTARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MentalistTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MENTARMOR_ID4, player);
                }
                if (item.Id_nb == NIGHARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NightshadeTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(NIGHARMOR_ID4, player);
                }
                if (item.Id_nb == RANGARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RangerTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(RANGARMOR_ID4, player);
                }
                if (item.Id_nb == VALEARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ValewalkerTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(VALEARMOR_ID4, player);
                }
                if (item.Id_nb == VAMPARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VampiirTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(VAMPARMOR_ID4, player);
                }
                if (item.Id_nb == WARDARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WardenTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WARDARMOR_ID4, player);
                }
                if (item.Id_nb == BERZARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BerserkerTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BERZARMOR_ID4, player);
                }
                if (item.Id_nb == BONEARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "BonedancerTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(BONEARMOR_ID4, player);
                }
                if (item.Id_nb == HEALARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HealerTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HEALARMOR_ID4, player);
                }
                if (item.Id_nb == HUNTARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "HunterTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(HUNTARMOR_ID4, player);
                }
                if (item.Id_nb == MIDMAULARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MaulerMidTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(MIDMAULARMOR_ID4, player);
                }
                if (item.Id_nb == RUNEARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RunemasterTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(RUNEARMOR_ID4, player);
                }
                if (item.Id_nb == SAVAARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SavageTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SAVAARMOR_ID4, player);
                }
                if (item.Id_nb == SHADOARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ShadowbladeTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SHADOARMOR_ID4, player);
                }
                if (item.Id_nb == SHAMARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ShamanTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SHAMARMOR_ID4, player);
                }
                if (item.Id_nb == SKALARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SkaldTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SKALARMOR_ID4, player);
                }
                if (item.Id_nb == SPIRARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpiritmasterTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(SPIRARMOR_ID4, player);
                }
                if (item.Id_nb == THANARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ThaneTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(THANARMOR_ID4, player);
                }
                if (item.Id_nb == VALKARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "ValkyrieTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(VALKARMOR_ID4, player);
                }
                if (item.Id_nb == WARLARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WarlockTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WARLARMOR_ID4, player);
                }
                if (item.Id_nb == WARRARMOR_ID3)
                {
                    player.Inventory.RemoveCountFromStack(item, 1);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "WarriorTrainer.ReceiveArmor.Text4", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(WARRARMOR_ID4, player);
                }
            }
            return base.ReceiveItem(source, item);
        }
        #endregion RespecDialogResponse
    }
}