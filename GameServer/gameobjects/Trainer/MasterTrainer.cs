using System;
using System.Collections;
using DOL.Events;
using DOL.GS.PacketHandler;
using System.Reflection;
using DOL.Database;
using log4net;
using DOL.Language;

namespace DOL.GS.Trainer
{
    [NPCGuildScript("Master Trainer")]
    public class MasterTrainer : GameTrainer
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            if (log.IsInfoEnabled) log.Info("Master Trainer Initializing...");
        }

        public override eQuestIndicator GetQuestIndicator(GamePlayer player) => eQuestIndicator.Lesson;

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            TurnTo(player, 50);
            var config = TrainerDataManager.GetConfig((eCharacterClass)player.CharacterClass.ID);

            // Distribution of Practice Equipment
            if (player.Level <= 5 && IsBaseClass((eCharacterClass)player.CharacterClass.ID))
            {
                if (config != null)
                {
                    string wpn = !string.IsNullOrEmpty(config.PracticeWeapon) ? config.PracticeWeapon : config.PracticeStaff;
                    if (!string.IsNullOrEmpty(wpn) && player.Inventory.CountItemTemplate(wpn, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{config.TranslationPrefix}Trainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    
                    if (!string.IsNullOrEmpty(config.PracticeShield) && player.Inventory.CountItemTemplate(config.PracticeShield, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{config.TranslationPrefix}Trainer.Interact.Text3", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
            }
            else if (!IsBaseClass((eCharacterClass)player.CharacterClass.ID))
            {
                CheckArmorUpgrades(player, config);
            }

            if (CanPromotePlayer(player))
                DisplayClassDescriptions(player);

            if (CanTrainChampionLevels(player))
            {
                eChampionTrainerType champType = GetChampionTrainerTypeID(player.CharacterClass.GetBaseClass());
                if (champType != eChampionTrainerType.None)
                {
                    player.Out.SendChampionTrainerWindow((int)champType);
                }
            }

            return true;
        }

        public override IList GetExamineMessages(GamePlayer player)
        {
            return new ArrayList()
            {
                LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.GetExamineMessages.YouTarget", GetName(0, false, player.Client.Account.Language, this)),
                LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.GetExamineMessages.YouExamine.Master", GetName(0, false, player.Client.Account.Language, this), GetPronoun(0, true, player.Client.Account.Language), GetAggroLevelString(player, false)),
                LanguageMgr.GetTranslation(player.Client.Account.Language, "GameTrainer.GetExamineMessages.RightClick")
            };
        }

        public override bool CanTrain(GamePlayer player) => true;

        public override bool CanPromotePlayer(GamePlayer player)
        {
            return player.Level >= 5 && IsBaseClass((eCharacterClass)player.CharacterClass.ID);
        }

        private void DisplayClassDescriptions(GamePlayer player)
        {
            var config = TrainerDataManager.GetConfig((eCharacterClass)player.CharacterClass.ID);
            if (config != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"MasterTrainer.Interact.{config.TranslationPrefix}1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str)) return false;
            GamePlayer player = source as GamePlayer;
            if (player == null) return false;

            string lowerText = str.ToLowerInvariant();

            if (player.Level <= 5 && IsBaseClass((eCharacterClass)player.CharacterClass.ID))
            {
                var baseConfig = TrainerDataManager.GetConfig((eCharacterClass)player.CharacterClass.ID);
                if (baseConfig != null)
                {
                    if ((lowerText == "practice weapon" || lowerText == "arme d'entraînement" || lowerText == "practice staff" || lowerText == "bâton d'entraînement") && (!string.IsNullOrEmpty(baseConfig.PracticeWeapon) || !string.IsNullOrEmpty(baseConfig.PracticeStaff)))
                    {
                        string wpn = !string.IsNullOrEmpty(baseConfig.PracticeWeapon) ? baseConfig.PracticeWeapon : baseConfig.PracticeStaff;
                        if (player.Inventory.CountItemTemplate(wpn, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                            addGift(wpn, player);
                        return true;
                    }
                    if ((lowerText == "training shield" || lowerText == "bouclier d'entraînement") && !string.IsNullOrEmpty(baseConfig.PracticeShield))
                    {
                        if (player.Inventory.CountItemTemplate(baseConfig.PracticeShield, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                            addGift(baseConfig.PracticeShield, player);
                        return true;
                    }
                }
            }

            // Handle State using memory-safe TempProperties instead of Dictionary
            int pendingClassId = (int)player.TempProperties.getProperty("MT_ClassOffer", (int)eCharacterClass.Unknown);
            int pendingWeaponChoiceId = (int)player.TempProperties.getProperty("MT_WeaponOffer", (int)eCharacterClass.Unknown);
            
            eCharacterClass pendingClass = (eCharacterClass)pendingClassId;
            eCharacterClass pendingWeaponChoice = (eCharacterClass)pendingWeaponChoiceId;

            if (CanPromotePlayer(player))
            {
                if (lowerText == "yes" || lowerText == "oui")
                {
                    if (pendingClass != eCharacterClass.Unknown)
                    {
                        player.TempProperties.removeProperty("MT_ClassOffer");
                        var config = TrainerDataManager.GetConfig(pendingClass);
                        if (config != null)
                        {
                            if (config.WeaponChoices.Count > 0)
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, $"{config.TranslationPrefix}Trainer.Interact.Text4", this.Name, player.Salutation), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                                player.TempProperties.setProperty("MT_WeaponOffer", (int)pendingClass);
                            }
                            else
                            {
                                PromotePlayerAndGiveGear(player, config, null);
                            }
                        }
                        return true;
                    }
                }
                
                if (lowerText == "no" || lowerText == "non")
                {
                    player.TempProperties.removeProperty("MT_ClassOffer");
                    player.TempProperties.removeProperty("MT_WeaponOffer");
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.ChooseLater", this.Name, player.Salutation), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    return true;
                }

                if (pendingWeaponChoice != eCharacterClass.Unknown)
                {
                    var config = TrainerDataManager.GetConfig(pendingWeaponChoice);
                    if (config != null && config.WeaponChoices.TryGetValue(lowerText, out WeaponChoice choice))
                    {
                        player.TempProperties.removeProperty("MT_WeaponOffer");
                        PromotePlayerAndGiveGear(player, config, choice);
                        return true;
                    }
                }

                // If user types a class name ("Armsman", "Cleric" etc.)
                foreach (var config in TrainerDataManager.Configs.Values)
                {
                    if (string.Equals(config.ClassNameEN, lowerText, StringComparison.OrdinalIgnoreCase) || 
                        string.Equals(config.ClassNameFR, lowerText, StringComparison.OrdinalIgnoreCase))
                    {
                        var targetClass = CharacterClass.GetClass((int)config.ClassID);
                        if (targetClass.GetBaseClass().ID == player.CharacterClass.ID && targetClass.ID != player.CharacterClass.ID)
                        {
                            if (targetClass.Name == "Mauler")
                            {
                                if (player.Realm == eRealm.Albion && config.ClassID != eCharacterClass.MaulerAlb) continue;
                                if (player.Realm == eRealm.Midgard && config.ClassID != eCharacterClass.MaulerMid) continue;
                                if (player.Realm == eRealm.Hibernia && config.ClassID != eCharacterClass.MaulerHib) continue;
                            }

                            string basePrefix = TrainerDataManager.GetConfig((eCharacterClass)player.CharacterClass.ID)?.TranslationPrefix ?? player.Salutation;

                            if (targetClass.EligibleRaces.Exists(r => (short)r.ID == player.Race))
                            {
                                player.TempProperties.setProperty("MT_ClassOffer", (int)config.ClassID);
                                string msgKey = !string.IsNullOrEmpty(config.CustomExplainKey) ? config.CustomExplainKey : $"{basePrefix}Trainer.{config.TranslationPrefix}.Explain";
                                string msg = LanguageMgr.GetTranslation(player.Client.Account.Language, msgKey, this.Name) + "\r\n\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.ChooseClass", this.Name);
                                player.Out.SendMessage(msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                            }
                            else
                            {
                                string refuseKey = !string.IsNullOrEmpty(config.CustomRefuseKey) ? config.CustomRefuseKey : $"{basePrefix}Trainer.{config.TranslationPrefix}.Refuse";
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, refuseKey, this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                            }
                            return true;
                        }
                    }
                }
            }

            // REALM RANK SETTINGS
            if (lowerText == "realmrank" || lowerText == "rang de royaume")
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RealmRankDeleteAsk"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }
            if (lowerText == "delete" || lowerText == "effacer")
            {
                if (player.Level == 50)
                {
                    player.RealmPoints = 0;
                    player.RespecRealm();
                    player.RealmLevel = 0;
                    player.SaveIntoDatabase();
                    player.Out.SendUpdatePlayer();
                    player.GainRealmPoints(0, false);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RealmRankDeleted"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
                return false;
            }

            if (lowerText == "realmranks" || lowerText == "rangs de royaume")
            {
                player.Out.SendMessage((LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RealmRanksList1") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RealmRanksList2")), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            long[] rrPoints = { 0, 7126, 61755, 213880, 513550, 1010630, 1755255, 2797380, 4187010, 5974130, 8208760, 23308100, 66181550 };
            for (int i = 1; i <= 13; i++)
            {
                if (lowerText == $"rr{i}")
                {
                    if (player.Level == 50 && player.RealmPoints == 0)
                    {
                        player.SaveIntoDatabase();
                        player.Out.SendUpdatePlayer();
                        player.GainRealmPoints(rrPoints[i - 1], false);
                        string rankMsg = i == 1
                            ? LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RealmRank1")
                            : string.Format(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RealmRankSet"), i);
                        player.Out.SendMessage(rankMsg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return false;
                    }
                    return true;
                }
            }

            // RESPECS
            if (lowerText == "respecs" || lowerText == "specialisations")
            {
                player.Out.SendMessage(string.Format((LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecsList1") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecsList2") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecsList3") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecsList4") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecsList5")), player.RespecAmountAllSkill, player.RespecAmountSingleSkill, player.RespecAmountRealmSkill, player.RespecAmountChampionSkill), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                player.Out.SendMessage((LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecsAsk1") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecsAsk2")), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (lowerText == "full" || lowerText == "entier")
            {
                if (player.RespecAmountAllSkill >= 5) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecFullMax"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                else player.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecFullBuy"), new CustomDialogResponse(RespecFullDialogResponse));
                return true;
            }
            if (lowerText == "single" || lowerText == "simple")
            {
                if (player.RespecAmountSingleSkill >= 5) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecSingleMax"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                else player.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecSingleBuy"), new CustomDialogResponse(RespecSingleDialogResponse));
                return true;
            }
            if (lowerText == "realm" || lowerText == "royaume")
            {
                if (player.RespecAmountRealmSkill >= 5) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecRealmMax"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                else player.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecRealmBuy"), new CustomDialogResponse(RespecRealmDialogResponse));
                return true;
            }
            if (lowerText == "championlevel" || lowerText == "champion")
            {
                if (player.RespecAmountChampionSkill >= 5) player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecCLMax"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                else player.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecCLBuy"), new CustomDialogResponse(RespecChampionDialogResponse));
                return true;
            }

            player.Out.SendTrainerWindow();
            return true;
        }

        #region TrainSpecLine
        public void TrainSpecLine(GamePlayer player, string line, int points)
        {
            if (player == null) return;

            if (!(player.TargetObject is GameTrainer))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.TrainSpecLine.NoTrainerTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if ((points <= 0) || (points >= 51))
            {
                player.Out.SendMessage(string.Format(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.TrainSpecLine.InvalidAmount"), points), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            int target = points;
            Specialization spec = player.GetSpecialization(line);
            if (spec == null)
            {
                player.Out.SendMessage(string.Format(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.TrainSpecLine.InvalidLine"), line), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            int current = spec.Level;
            if (current >= player.Level)
            {
                player.Out.SendMessage(string.Format(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.TrainSpecLine.MaxTrainedLevel"), line), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (points <= current)
            {
                player.Out.SendMessage(string.Format(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.TrainSpecLine.AlreadyTrained"), line), eChatType.CT_System, eChatLoc.CL_SystemWindow);
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
                    player.Out.SendMessage(string.Format(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.TrainSpecLine.MaxTrainedLevel"), line), eChatType.CT_System, eChatLoc.CL_SystemWindow);
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
                    player.Out.SendMessage(string.Format(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.TrainSpecLine.Cost"), (spec.Level + 1)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.TrainSpecLine.NotEnoughPoints"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;
                }
            }
            if (changed)
            {
                spec.Level += speclevel;
                player.OnSkillTrained(spec);
                player.Out.SendUpdatePoints();
                player.Out.SendTrainerWindow();
                player.Out.SendMessage(string.Format(LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.TrainSpecLine.Success"), points, line), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
        }
        #endregion TrainSpecLine

        #region RespecDialogResponse
        protected void RespecFullDialogResponse(GamePlayer player, byte response)
        {
            if (response != 0x01) return;
            player.RespecAmountAllSkill++;
            player.Out.SendMessage((LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecFullBought1") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecFullBought2")), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
        protected void RespecSingleDialogResponse(GamePlayer player, byte response)
        {
            if (response != 0x01) return;
            player.RespecAmountSingleSkill++;
            player.Out.SendMessage((LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecSingleBought1") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecSingleBought2")), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
        protected void RespecRealmDialogResponse(GamePlayer player, byte response)
        {
            if (response != 0x01) return;
            player.RespecAmountRealmSkill++;
            player.Out.SendMessage((LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecRealmBought1") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecRealmBought2")), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
        protected void RespecChampionDialogResponse(GamePlayer player, byte response)
        {
            if (response != 0x01) return;
            player.RespecAmountChampionSkill++;
            player.Out.SendMessage((LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecCLBought1") + "\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "MasterTrainer.Interact.RespecCLBought2")), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
        #endregion RespecDialogResponse
    }
}