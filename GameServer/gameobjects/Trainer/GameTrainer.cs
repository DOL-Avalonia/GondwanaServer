using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.GS.Trainer;

namespace DOL.GS
{
    /// <summary>
    /// The mother class for all class trainers
    /// </summary>
    public class GameTrainer : GameNPC
    {
        // List of disabled classes
        private static List<string> disabled_classes = null;

        public enum eChampionTrainerType : int
        {
            Acolyte = 4,
            AlbionRogue = 2,
            Disciple = 7,
            Elementalist = 5,
            Fighter = 1,
            Forester = 12,
            Guardian = 1,
            Mage = 6,
            Magician = 11,
            MidgardRogue = 3,
            Mystic = 9,
            Naturalist = 10,
            Seer = 8,
            Stalker = 2,
            Viking = 1,
            None = 0,
        }

        protected eChampionTrainerType m_championTrainerType = eChampionTrainerType.None;

        public virtual eCharacterClass TrainedClass => eCharacterClass.Unknown;

        public GameTrainer() { }

        public GameTrainer(eChampionTrainerType championTrainerType)
        {
            m_championTrainerType = championTrainerType;
        }

        #region GetExamineMessages
        /// <summary>
        /// Adds messages to ArrayList which are sent when object is targeted
        /// </summary>
        /// <param name="player">GamePlayer that is examining this object</param>
        /// <returns>list with string messages</returns>
        public override IList GetExamineMessages(GamePlayer player)
        {
            string TrainerClassName = "";
            switch (player.Client.Account.Language)
            {
                case "DE":
                    var translation = (DBLanguageNPC)LanguageMgr.GetTranslation(player.Client.Account.Language, this);
                    if (translation != null)
                    {
                        int index = translation.GuildName.IndexOf("-Ausbilder");
                        TrainerClassName = index >= 0 ? translation.GuildName.Substring(0, index) : GuildName;
                    }
                    else TrainerClassName = GuildName;
                    break;
                default:
                    int defaultIndex = GuildName.IndexOf(" Trainer");
                    if (defaultIndex >= 0) TrainerClassName = GuildName.Substring(0, defaultIndex);
                    break;
            }

            IList list = new ArrayList();
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.GetExamineMessages.YouTarget", GetName(0, false, player.Client.Account.Language, this)));
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.GetExamineMessages.YouExamine", GetName(0, false, player.Client.Account.Language, this), GetPronoun(0, true, player.Client.Account.Language), GetAggroLevelString(player, false), TrainerClassName));
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.GetExamineMessages.RightClick"));
            return list;
        }
        #endregion

        public virtual bool CanTrain(GamePlayer player)
        {
            return player.CharacterClass.ID == (int)TrainedClass || TrainedClass == eCharacterClass.Unknown;
        }

        #region Token Logic Gatekeepers
        /// <summary>
        /// Checks if this specific trainer should handle tokens for this specific player.
        /// </summary>
        protected virtual bool CanHandleTokens(GamePlayer player)
        {
            if (player.Level <= 5 && !player.IsRenaissance)
                return false;

            if (this.TrainedClass == eCharacterClass.Unknown)
                return true;

            if (IsBaseClass(this.TrainedClass))
                return false;

            if (player.CharacterClass.ID == (int)this.TrainedClass)
                return true;

            return false;
        }

        /// <summary>
        /// Helper to determine if a class is a Base Class
        /// </summary>
        protected virtual bool IsBaseClass(eCharacterClass characterClass)
        {
            switch (characterClass)
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
                default:
                    return false;
            }
        }

        #endregion Token Logic Gatekeepers

        protected virtual void CheckArmorUpgrades(GamePlayer player, ClassTrainerConfig config)
        {
            if (config == null || config.Armors == null || config.Armors.Length < 2) return;

            for (int i = 0; i < config.Armors.Length - 1; i++)
            {
                int minLvl = 10 + (i * 5);
                int maxLvl = (i == config.Armors.Length - 2) ? 50 : minLvl + 5;

                if (player.Level >= minLvl && player.Level < maxLvl)
                {
                    if (player.Inventory.CountItemTemplate(config.Armors[i], eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) > 0)
                    {
                        ItemTemplate itemTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(config.Armors[i]);
                        string itemName = itemTemplate != null ? itemTemplate.Name : config.Armors[i];

                        string msg = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.Interact.ArmorUpgradeHint", this.Name, itemName);

                        if (!string.IsNullOrEmpty(msg))
                        {
                            player.Out.SendMessage(msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        }
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Interact with trainer
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;
            TurnTo(player, 10000);

            if (CanHandleTokens(player))
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.TokenLookOver", Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);

            var config = TrainerDataManager.GetConfig(TrainedClass);
            string basePrefix = config?.TranslationPrefix ?? TrainedClass.ToString();
            
            // Subclass Trainers logic
            if (TrainedClass != eCharacterClass.Unknown && !IsBaseClass(TrainedClass))
            {
                if (player.CharacterClass.ID == (int)TrainedClass)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{basePrefix}Trainer.Interact.Text3", this.Name, player.GetName(0, false)), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    OfferTraining(player);
                    CheckArmorUpgrades(player, config);
                }
                else if (CanPromotePlayer(player))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{basePrefix}Trainer.Interact.Text1", this.Name, player.Salutation), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    if (!player.IsLevelRespecUsed) OfferRespecialize(player);
                }
                else CheckChampionTraining(player);
            }
            // Base Class Trainers Logic
            else if (IsBaseClass(TrainedClass) && player.CharacterClass.ID == (int)TrainedClass)
            {
                if (player.Level >= 5)
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{basePrefix}Trainer.Interact.Text1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                else OfferTraining(player);

                if (config != null && player.Level <= 5)
                {
                    if (!string.IsNullOrEmpty(config.PracticeWeapon) && player.Inventory.CountItemTemplate(config.PracticeWeapon, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{basePrefix}Trainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    if (!string.IsNullOrEmpty(config.PracticeShield) && player.Inventory.CountItemTemplate(config.PracticeShield, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{basePrefix}Trainer.Interact.Text3", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    if (!string.IsNullOrEmpty(config.PracticeStaff) && player.Inventory.CountItemTemplate(config.PracticeStaff, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{basePrefix}Trainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
            }

            if (CanTrain(player))
            {
                player.Out.SendTrainerWindow();
                player.GainExperience(GameLiving.eXPSource.Other, 0); 

                if (player.FreeLevelState == 2)
                {
                    player.LastFreeLevel = player.Level;
                    //long xp = GameServer.ServerRules.GetExperienceForLevel(player.PlayerCharacter.LastFreeLevel + 3) - GameServer.ServerRules.GetExperienceForLevel(player.PlayerCharacter.LastFreeLevel + 2);
                    long xp = player.GetExperienceNeededForLevel(player.LastFreeLevel + 1) - player.GetExperienceNeededForLevel(player.LastFreeLevel);
                    //player.PlayerCharacter.LastFreeLevel = player.Level;
                    player.GainExperience(GameLiving.eXPSource.Other, xp);
                    player.LastFreeLeveled = DateTime.Now;
                    player.Out.SendPlayerFreeLevelUpdate();
                }
            }

            if (CanTrainChampionLevels(player))
            {
                eChampionTrainerType champType = m_championTrainerType;
                if (champType == eChampionTrainerType.None)
                {
                    champType = GetChampionTrainerTypeID(player.CharacterClass.GetBaseClass());
                }

                if (champType != eChampionTrainerType.None)
                {
                    player.Out.SendChampionTrainerWindow((int)champType);
                }
            }

            return true;
        }

        /// <summary>
        /// Can we offer this player training for Champion levels?
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public virtual bool CanTrainChampionLevels(GamePlayer player)
        {
            if (player.Level < player.MaxLevel || !player.Champion) return false;

            if (TrainedClass != eCharacterClass.Unknown && !IsBaseClass(TrainedClass))
                return false;

            eChampionTrainerType champType = m_championTrainerType;
            if (champType == eChampionTrainerType.None)
                champType = GetChampionTrainerTypeID(player.CharacterClass.GetBaseClass());

            return champType != eChampionTrainerType.None;
        }

        protected virtual eChampionTrainerType GetChampionTrainerTypeID(CharacterClass charClass)
        {
            switch (charClass.ID)
            {
                case (int)eCharacterClass.Acolyte: return eChampionTrainerType.Acolyte;
                case (int)eCharacterClass.AlbionRogue: return eChampionTrainerType.AlbionRogue;
                case (int)eCharacterClass.Disciple: return eChampionTrainerType.Disciple;
                case (int)eCharacterClass.Elementalist: return eChampionTrainerType.Elementalist;
                case (int)eCharacterClass.Fighter: return eChampionTrainerType.Fighter;
                case (int)eCharacterClass.Forester: return eChampionTrainerType.Forester;
                case (int)eCharacterClass.Guardian: return eChampionTrainerType.Guardian;
                case (int)eCharacterClass.Mage: return eChampionTrainerType.Mage;
                case (int)eCharacterClass.Magician: return eChampionTrainerType.Magician;
                case (int)eCharacterClass.MidgardRogue: return eChampionTrainerType.MidgardRogue;
                case (int)eCharacterClass.Mystic: return eChampionTrainerType.Mystic;
                case (int)eCharacterClass.Naturalist: return eChampionTrainerType.Naturalist;
                case (int)eCharacterClass.Seer: return eChampionTrainerType.Seer;
                case (int)eCharacterClass.Stalker: return eChampionTrainerType.Stalker;
                case (int)eCharacterClass.Viking: return eChampionTrainerType.Viking;
                default: return eChampionTrainerType.None;
            }
        }

        /// <summary>
        /// Talk to trainer
        /// </summary>
        /// <param name="source"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text)) return false;
            GamePlayer player = source as GamePlayer;
            if (player == null) return false;

            string lowerText = text.ToLowerInvariant();

            // Level 5 Free Respecs
            if (CanTrain(player) && text == LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.Interact.CaseRespecialize"))
            {
                if (player.Level == 5 && !player.IsLevelRespecUsed)
                {
                    int specPoints = player.SkillSpecialtyPoints;
                    player.RespecAll();
                    if (player.SkillSpecialtyPoints > specPoints)
                    {
                        player.RemoveAllStyles();
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.Interact.RegainPoints", (player.SkillSpecialtyPoints - specPoints)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    player.RefreshSpecDependantSkills(false);
                    player.Out.SendUpdatePlayerSkills();
                    player.Out.SendUpdatePoints();
                    player.Out.SendUpdatePlayer();
                    player.Out.SendTrainerWindow();
                    player.SaveIntoDatabase();
                }
            }

            var config = TrainerDataManager.GetConfig(TrainedClass);

            // BASE CLASS LOGIC (Lore Queries & Practice Items)
            if (player.Level <= 5 && config != null && IsBaseClass(TrainedClass) && player.CharacterClass.ID == (int)TrainedClass)
            {
                if ((lowerText == "practice weapon" || lowerText == "arme d'entraînement" || lowerText == "practice staff" || lowerText == "bâton d'entraînement") && (!string.IsNullOrEmpty(config.PracticeWeapon) || !string.IsNullOrEmpty(config.PracticeStaff)))
                {
                    string wpn = !string.IsNullOrEmpty(config.PracticeWeapon) ? config.PracticeWeapon : config.PracticeStaff;
                    if (player.Inventory.CountItemTemplate(wpn, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                        addGift(wpn, player);
                    return true;
                }
                if ((lowerText == "training shield" || lowerText == "bouclier d'entraînement") && !string.IsNullOrEmpty(config.PracticeShield))
                {
                    if (player.Inventory.CountItemTemplate(config.PracticeShield, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                        addGift(config.PracticeShield, player);
                    return true;
                }

                // Native Class Lore Check
                foreach (var c in TrainerDataManager.Configs.Values)
                {
                    var targetClass = CharacterClass.GetClass((int)c.ClassID);
                    if (targetClass.GetBaseClass().ID == (int)TrainedClass && targetClass.ID != (int)TrainedClass &&
                       (string.Equals(c.ClassNameEN, lowerText, StringComparison.OrdinalIgnoreCase) || string.Equals(c.ClassNameFR, lowerText, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (targetClass.Name == "Mauler")
                        {
                            if (player.Realm == eRealm.Albion && c.ClassID != eCharacterClass.MaulerAlb) continue;
                            if (player.Realm == eRealm.Midgard && c.ClassID != eCharacterClass.MaulerMid) continue;
                            if (player.Realm == eRealm.Hibernia && c.ClassID != eCharacterClass.MaulerHib) continue;
                        }

                        string explainKey = !string.IsNullOrEmpty(c.CustomExplainKey) ? c.CustomExplainKey : $"{config.TranslationPrefix}Trainer.{c.TranslationPrefix}.Explain";
                        string refuseKey = !string.IsNullOrEmpty(c.CustomRefuseKey) ? c.CustomRefuseKey : $"{config.TranslationPrefix}Trainer.{c.TranslationPrefix}.Refuse";

                        if (targetClass.EligibleRaces.Exists(r => (short)r.ID == player.Race))
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, explainKey, this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        else
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, refuseKey, this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        return true;
                    }
                }
            }

            // SPECIALIZED CLASS LOGIC (Promotions & Weapons)
            if (CanPromotePlayer(player) && config != null && !IsBaseClass(TrainedClass))
            {
                if (config.PromotionKeywords.Contains(lowerText))
                {
                    if (config.WeaponChoices.Count > 0)
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, config.PromotionTextKey, this.Name, player.GetName(0, false), player.Salutation), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    else
                        PromotePlayerAndGiveGear(player, config, null);
                    return true;
                }

                if (config.WeaponChoices.TryGetValue(lowerText, out WeaponChoice choice))
                {
                    PromotePlayerAndGiveGear(player, config, choice);
                    return true;
                }
            }

            TurnTo(player, 10000);
            return true;
        }

        // Shared helper method to prevent code duplication across trainers
        public void PromotePlayerAndGiveGear(GamePlayer player, ClassTrainerConfig config, WeaponChoice choice)
        {
            string msgKey = choice != null ? choice.TranslationKey : config.PromotionTextKey;
            string msg = LanguageMgr.GetTranslation(player.Client.Account.Language, msgKey, player.GetName(0, false));
            
            if (PromotePlayer(player, (int)config.ClassID, msg, null))
            {
                if (choice != null && choice.WeaponIds != null)
                {
                    foreach (var w in choice.WeaponIds)
                        addGift(w, player);
                }
                else if (!string.IsNullOrEmpty(config.DefaultWeapon))
                {
                    addGift(config.DefaultWeapon, player);
                }

                if (config.Armors != null && config.Armors.Length > 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{config.TranslationPrefix}Trainer.ReceiveArmor.Text1", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    addGift(config.Armors[0], player);
                }

                if (config.ClassID == eCharacterClass.Vampiir)
                {
                    foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (plr != null) plr.Out.SendVampireEffect(player, true);
                    }
                }
            }
        }

        /// <summary>
        /// Offer respecialize to the player.
        /// </summary>
        /// <param name="player"></param>
        protected virtual void OfferRespecialize(GamePlayer player)
        {
            player.Out.SendMessage(String.Format(LanguageMgr.GetTranslation
                                                 (player.Client, "GameTrainer.Interact.Respecialize", this.Name, player.Name)),
                                   eChatType.CT_Say, eChatLoc.CL_PopupWindow);
        }

        /// <summary>
        /// Check Ability to use Item
        /// </summary>
        /// <param name="player"></param>
        protected virtual void CheckAbilityToUseItem(GamePlayer player)
        {
            // drop any equiped-non usable item, in inventory or on the ground if full
            lock (player.Inventory)
            {
                foreach (InventoryItem item in player.Inventory.EquippedItems)
                {
                    if (!player.HasAbilityToUseItem(item.Template))
                    {
                        if (player.Inventory.IsSlotsFree(item.Count, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack) == true)
                        {
                            player.Inventory.MoveItem((eInventorySlot)item.SlotPosition, player.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack), item.Count);
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.CheckAbilityToUseItem.Text1", item.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            player.Inventory.MoveItem((eInventorySlot)item.SlotPosition, eInventorySlot.Ground, item.Count);
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.CheckAbilityToUseItem.Text1", item.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                }
            }
        }

        // ITEM RECEIVING (Tokens, Respecs, & ARMORS)
        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (source == null || item == null) return false;
            GamePlayer player = source as GamePlayer;
            if (player != null)
            {
                // Handles all tokens securely
                if (item.Id_nb.StartsWith("TaskToken_PvE") || item.Id_nb.StartsWith("TaskToken_PvPGvG"))
                {
                    if (CanHandleTokens(player)) return HandleTaskToken(player, item);
                    else
                    {
                        if (player.Level <= 5 && !player.IsRenaissance && !IsBaseClass(this.TrainedClass))
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.TokenEagerness", Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        else if (!IsBaseClass(this.TrainedClass))
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.TokenOnlyStudents", Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        return false;
                    }
                }

                switch (item.Id_nb)
                {
                    case "respec_single":
                        player.Inventory.RemoveCountFromStack(item, 1);
                        InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Merchant, item, 1);
                        player.RespecAmountSingleSkill++;
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.ReceiveItem.RespecSingle"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return true;
                    case "respec_full":
                        player.Inventory.RemoveCountFromStack(item, 1);
                        InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Merchant, item, 1);
                        player.RespecAmountAllSkill++;
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.ReceiveItem.RespecFull", item.Name), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return true;
                    case "respec_realm":
                        player.Inventory.RemoveCountFromStack(item, 1);
                        InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Merchant, item, 1);
                        player.RespecAmountRealmSkill++;
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.ReceiveItem.RespecRealm"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return true;
                }

                // Armor Tier Upgrades
                if (player.Level >= 10 && player.Level < 50)
                {
                    // Only process armor upgrades if talking to their own class trainer, or a master trainer
                    if (this.TrainedClass == eCharacterClass.Unknown || (int)this.TrainedClass == player.CharacterClass.ID)
                    {
                        var config = TrainerDataManager.GetConfig((eCharacterClass)player.CharacterClass.ID);
                        if (config != null && config.Armors != null && config.Armors.Length >= 3)
                        {
                            for (int i = 0; i < config.Armors.Length - 1; i++)
                            {
                                if (item.Id_nb == config.Armors[i])
                                {
                                    int minLvl = 10 + (i * 5);
                                    int maxLvl = (i == config.Armors.Length - 2) ? 50 : minLvl + 5;
                                    if (player.Level >= minLvl && player.Level < maxLvl)
                                    {
                                        player.Inventory.RemoveCountFromStack(item, 1);
                                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{config.TranslationPrefix}Trainer.ReceiveArmor.Text{i + 2}", this.Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                                        addGift(config.Armors[i + 1], player);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return base.ReceiveItem(source, item);
        }

        #region Task Tokens Handler
        protected virtual bool HandleTaskToken(GamePlayer player, InventoryItem item)
        {
            int indexOfEnd = item.Id_nb.LastIndexOf('_');
            if (indexOfEnd == -1) return false;

            string end = item.Id_nb.Substring(indexOfEnd + 1);
            int level = 1;
            if (end.StartsWith("lv") && !Int32.TryParse(end.Substring(2), out level))
                return false;

            bool success = false;
            if (item.Id_nb.StartsWith("TaskToken_PvE"))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.TokenPvE", Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                success = TaskMaster.GrantTaskExperience(player, level);
            }
            else if (item.Id_nb.StartsWith("TaskToken_PvPGvG"))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.TokenPvPGvG", Name, player.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                success = TaskMaster.GrantTaskRealmPoints(player, level);
            }

            if (success)
            {
                player.Inventory.RemoveItem(item);
                return true;
            }
            else
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.TokenCannotGrant", Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return true;
            }
        }
        #endregion

        public void PromotePlayer(GamePlayer player)
        {
            if (TrainedClass != eCharacterClass.Unknown) PromotePlayer(player, (int)TrainedClass, "", null);
        }

        /// <summary>
        /// Check if Player can be Promoted
        /// </summary>
        /// <param name="player"></param>
        public virtual bool CanPromotePlayer(GamePlayer player)
        {
            var pickedClass = CharacterClass.GetClass((int)TrainedClass);
            var baseClass = pickedClass.GetBaseClass();

            if (baseClass.Equals(pickedClass)) return false;
            if (player.Level < 5 || player.CharacterClass.ID != baseClass.ID) return false;
            if (!pickedClass.EligibleRaces.Exists(s => (short)s.ID == player.Race)) return false;

            if (GlobalConstants.CLASS_GENDER_CONSTRAINTS_DICT.ContainsKey(TrainedClass) && GlobalConstants.CLASS_GENDER_CONSTRAINTS_DICT[TrainedClass] != player.Gender)
                return false;

            return true;
        }

        /// <summary>
        /// Called to promote a player
        /// </summary>
        public bool PromotePlayer(GamePlayer player, int classid, string messageToPlayer, InventoryItem[] gifts)
        {
            if (player == null) return false;
            var oldClass = player.CharacterClass;

            if (player.SetCharacterClass(CharacterClass.GetClass(classid)))
            {
                player.RemoveAllStyles();
                player.RemoveAllAbilities();
                player.RemoveAllSpellLines();

                if (!string.IsNullOrEmpty(messageToPlayer))
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Says", this.Name, messageToPlayer), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Upgraded", player.Salutation), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                player.RefreshSpecDependantSkills(true);
                player.StartPowerRegeneration();
                player.Out.SendUpdatePlayerSkills();
                player.Out.SendUpdatePlayer();
                CheckAbilityToUseItem(player);

                // Initiate equipment
                if (gifts != null && gifts.Length > 0)
                {
                    for (int i = 0; i < gifts.Length; i++)
                    {
                        player.ReceiveItem(this, gifts[i]);
                        InventoryLogging.LogInventoryAction(this, player, eInventoryActionType.Other, gifts[i], gifts[i].Count);
                    }
                }

                // after gifts
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.PromotePlayer.Accepted", player.CharacterClass.GetProfessionTitle(player)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                Notify(GameTrainerEvent.PlayerPromoted, this, new PlayerPromotedEventArgs(player, oldClass));
                player.SaveIntoDatabase();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Add a gift to the player
        /// </summary>
        public virtual bool addGift(String template, GamePlayer player)
        {
            ItemTemplate temp = GameServer.Database.FindObjectByKey<ItemTemplate>(template);
            if (temp != null)
            {
                if (!player.Inventory.AddTemplate(GameInventoryItem.Create(temp), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.AddGift.NotEnoughSpace"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                InventoryLogging.LogInventoryAction(this, player, eInventoryActionType.Other, temp);
            }
            return true;
        }

        /// <summary>
        /// If we can't train champion levels then dismiss this player
        /// </summary>
        /// <param name="player"></param>
        protected virtual void CheckChampionTraining(GamePlayer player)
        {
            if (CanTrainChampionLevels(player) == false)
            {
                _ = SayTo(player, eChatLoc.CL_ChatWindow, LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.Train.SeekElsewhere"));
            }
        }

        protected virtual void OfferTraining(GamePlayer player)
        {
            _ = SayTo(player, eChatLoc.CL_ChatWindow, LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.Train.WouldYouLikeTo"));
        }

        /// <summary>
        /// No trainer for disabled classes
        /// </summary>
        public override bool AddToWorld()
        {
            if (!string.IsNullOrEmpty(ServerProperties.Properties.DISABLED_CLASSES))
            {
                if (disabled_classes == null)
                {
                    // creation of disabled_classes list.
                    disabled_classes = Util.SplitCSV(ServerProperties.Properties.DISABLED_CLASSES).ToList();
                }

                if (disabled_classes.Contains(TrainedClass.ToString()))
                    return false;
            }
            return base.AddToWorld();
        }
    }
}