using System;
using System.Collections.Generic;
using System.Reflection;
using DOL.Database;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.GS.Quests;
using DOL.GS.Scripts;
using log4net;
using DOL.GS.PlayerTitles;
using Discord;
using DOL.AI;
using DOL.GS.Finance;
using DOL.GS.ServerProperties;
using System.Diagnostics;

namespace DOL.GS
{
    public class TaskMaster : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);
        public TextNPCCondition Condition { get; private set; }

        public override eQuestIndicator GetQuestIndicator(GamePlayer player)
        {
            if (player.Reputation >= 0)
                return eQuestIndicator.Lore;
            else return eQuestIndicator.None;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player) && player.Reputation < 0)
                return false;

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.Greetings", player.RaceName), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.BonusInfo"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.Taskline"), eChatType.CT_System, eChatLoc.CL_PopupWindow);

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str))
                return false;

            GamePlayer player = source as GamePlayer;
            if (player == null)
                return false;

            switch (str.ToLower())
            {
                case "services":
                    {
                        IList<DBTextNPC> taskGivingNPCs =
                            GameServer.Database.SelectObjects<DBTextNPC>(DB.Column("IsInTaskMaster").IsEqualTo("1"));

                        bool foundAnyServices = false;

                        foreach (var taskNPC in taskGivingNPCs)
                        {
                            Condition = new TextNPCCondition(taskNPC.Condition);

                            if (!Condition.CheckAccess(player))
                                continue;

                            var mob = DOLDB<Mob>.SelectObject(DB.Column("Mob_ID").IsEqualTo(taskNPC.MobID));
                            if (mob == null)
                                continue;

                            if (mob.Region != this.CurrentRegionID)
                                continue;

                            // Retrieve the region and zone for the final text
                            var region = WorldMgr.GetRegion(mob.Region);
                            if (region == null)
                                continue;

                            var zone = region.GetZone(mob.X, mob.Y);
                            if (zone == null)
                                continue;

                            foundAnyServices = true;
                            var text = $"{taskNPC.MobName}\n";

                            if (player.Client.Account.Language == "EN")
                                text += taskNPC.TaskDescEN;
                            else if (player.Client.Account.Language == "FR")
                                text += taskNPC.TaskDescFR;

                            player.Out.SendMessage(text + "\n" + zone.Description + "\n", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        }

                        if (!foundAnyServices)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.NoServicesAround"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        }
                        break;
                    }
                case "tokens":
                case "jetons":
                    {
                        List<string> tokenMessages = new List<string>
                        {
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.TokenInfo"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.PvPTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.PvETokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.CraftTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.SpecialTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.DemonSlayerTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.TraderTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.ThiefTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.BountyHunterTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.WrathTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.DuelistTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.AdventurerTokens"),
                        LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.TokensToDeliver")
                        };

                        foreach (string message in tokenMessages)
                        {
                            player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        }
                        break;
                    }
            }
            return true;
        }

        public static bool RefuseItem(GamePlayer source, InventoryItem item)
        {
            source.SendTranslatedMessage("TaskMaster.Disabled", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
            return true;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            var player = source as GamePlayer;
            if (item == null || player == null)
            {
                return base.ReceiveItem(source, item);
            }

            string titleKey = null;
            bool success = false;
            bool remove = true;
            int indexOfEnd = item.Id_nb.LastIndexOf('_');
            if (indexOfEnd == -1)
            {
                return RefuseItem(player, item);
            }
            string end = item.Id_nb.Substring(indexOfEnd + 1);
            string key;
            int level = 1;
            if (end.StartsWith("lv"))
            {
                if (!Int32.TryParse(end.Substring(2), out level))
                {
                    return RefuseItem(player, item);
                }
                key = item.Id_nb.Substring(0, indexOfEnd);
            }
            else
            {
                key = item.Id_nb;
            }
            
            switch (key)
            {
                case "TaskToken_Thief":
                    success = AssignTitle(player, PlayerTitleMgr.TaskTitles.Thief, level, "Titles.Thief");
                    break;
                case "TaskToken_Trader":
                    success = AssignTitle(player, PlayerTitleMgr.TaskTitles.Trader, level, "Titles.Trader");
                    break;
                case "TaskToken_Demon_Slayer":
                    success = AssignTitle(player, PlayerTitleMgr.TaskTitles.Demonslayer, level, "Titles.Demonslayer");
                    break;
                case "TaskToken_Bounty_Hunter":
                    success = AssignTitle(player, PlayerTitleMgr.TaskTitles.Bountyhunter, level, "Titles.Bountyhunter");
                    break;
                case "TaskToken_Wrath":
                    success = AssignTitle(player, PlayerTitleMgr.TaskTitles.Wrath, level, "Titles.Wrath");
                    break;
                case "TaskToken_Duelist":
                    success = AssignTitle(player, PlayerTitleMgr.TaskTitles.Duelist, level, "Titles.Duelist");
                    break;
                case "TaskToken_Adventurer":
                    success = AssignTitle(player, PlayerTitleMgr.TaskTitles.Adventurer, level, "Titles.Adventurer");
                    break;

                // PvE Xp Tokens
                case "TaskToken_PvE":
                    success = GrantTaskExperience(player, level);
                    break;

                // Realm Point Tokens
                case "TaskToken_PvPGvG":
                    success = GrantTaskRealmPoints(player, level);
                    break;

                // Crafting Tokens
                case "TaskToken_Crafting":
                    success = level switch
                    {
                        0 => false,
                        1 => GrantCraftingPoints(player, 15, 8, 4, item),
                        2 => GrantCraftingPoints(player, 25, 13, 6, item),
                        3 => GrantCraftingPoints(player, 45, 23, 11, item),
                        4 => GrantCraftingPoints(player, 70, 35, 17, item),
                        >= 5 => GrantCraftingPoints(player, 99, 50, 25, item)
                    };
                    remove = false;
                    break;

                default:
                    return base.ReceiveItem(source, item);
            }

            if (!success)
            {
                player.SendTranslatedMessage("TaskMaster.Disabled", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                return true;
            }

            if (remove)
            {
                player.Inventory.RemoveItem(item);
            }
            return true;
        }

        public static bool AssignTitle<T>(GamePlayer player, T[] titleArray, int level, string translation) where T : IPlayerTitle
        {
            IPlayerTitle? title = level <= 0 || level > titleArray.Length ? null : titleArray[level - 1];
            if (title == null)
            {
                return false;
            }
            else if (!player.Titles.Add(title))
            {
                return GrantTaskExperience(player, level);
            }
            title.OnTitleGained(player);
            player.UpdateCurrentTitle();
            string titleName = LanguageMgr.GetTranslation(player.Client.Account.Language, translation + ".Level" + level);
            string message = LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.GiveTitle", titleName);
            player.Out.SendMessage(message, eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
            return true;
        }

        public static bool GrantTaskExperience(GamePlayer player, int level)
        {
            int percentage = level switch
            {
                <= 0 => 0,
                1 => 15,
                2 => 25,
                3 => 40,
                4 => 60,
                5 => 85,
                >= 6 => 125
            };
            return GrantPvEExperience(player, percentage);
        }

        public static bool GrantPvEExperience(GamePlayer player, int percentage)
        {
            if (!player.GainXP || percentage <= 0)
            {
                return false;
            }
            
            try
            {
                double factor = (percentage / 100.0d) * player.GetXPFactor(false, false);
                if (factor <= 0)
                {
                    return false;
                }
                
                if (player.Level >= player.MaxLevel)
                {
                    if (Properties.XP_TO_COPPER_RATE <= 0)
                        return false;
                    
                    // Arbitrary number for 50 to 51
                    var imaginaryXpToNextLevel = 200000000000L;
                    var copper = Finance.Money.XpToCopper((long)(Math.Round(imaginaryXpToNextLevel * factor)));
                    player.AddMoney(copper);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.GainMoney", LanguageMgr.TranslateMoneyLong(player, copper.Amount)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return true;
                }
                
                long xpNeeded = (long)Math.Round((player.ExperienceForNextLevel - player.ExperienceForCurrentLevel) * factor);

                while (xpNeeded > 0)
                {
                    long currentLevelXpNeeded = player.ExperienceForNextLevel - player.Experience;

                    if (xpNeeded >= currentLevelXpNeeded)
                    {
                        player.GainExperience(GameLiving.eXPSource.Quest, currentLevelXpNeeded, false);
                        xpNeeded -= currentLevelXpNeeded;
                    }
                    else
                    {
                        player.GainExperience(GameLiving.eXPSource.Quest, xpNeeded, false);
                        xpNeeded = 0;
                    }
                }
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.GainExperience", factor * 100), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }
            catch (OverflowException ex)
            {
                log.Error("OverflowException in GrantPvEExperience: " + ex.Message);
                return false;
            }
        }

        public static bool GrantTaskRealmPoints(GamePlayer player, int level)
        {
            int percentage = level switch
            {
                0 => 0,
                1 => 15,
                2 => 25,
                3 => 45,
                4 => 75,
                >= 5 => 100
                
            };
            return GrantRealmPoints(player, percentage);
        }

        public static bool GrantRealmPoints(GamePlayer player, int percentage)
        {
            if (!player.GainRP)
            {
                return false;
            }
            
            try
            {
                double factor = (percentage / 100.0d) * player.GetRPFactor(false);
                if (factor <= 0)
                {
                    return false;
                }

                long rpToAdd = (long)Math.Round(player.CalculateRPsToGainRealmRank() * factor);

                player.GainRealmPoints(rpToAdd, false);

                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.GainRealmPoints", factor * 100), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }
            catch (OverflowException ex)
            {
                log.Error("OverflowException in GrantRealmPoints: " + ex.Message);
                return false;
            }
        }

        private bool GrantCraftingPoints(GamePlayer player, int mainPoints, int secondaryPoints, int tertiaryPoints, InventoryItem oldItem)
        {
            if (player.CraftingPrimarySkill == eCraftingSkill.BasicCrafting)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.NoProfession"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            InventoryItem newInventoryItem = null;

            switch (oldItem.Id_nb)
            {
                case "TaskToken_Crafting_lv1":
                    newInventoryItem = CreateNewInventoryItem("Task_Parch_CraftingBuffBonus_lv1");
                    break;
                case "TaskToken_Crafting_lv2":
                    newInventoryItem = CreateNewInventoryItem("Task_Parch_CraftingBuffBonus_lv2");
                    break;
                case "TaskToken_Crafting_lv3":
                    newInventoryItem = CreateNewInventoryItem("Task_Parch_CraftingBuffBonus_lv3");
                    break;
                case "TaskToken_Crafting_lv4":
                    newInventoryItem = CreateNewInventoryItem("Task_Parch_CraftingBuffBonus_lv4");
                    break;
                case "TaskToken_Crafting_lv5":
                    newInventoryItem = CreateNewInventoryItem("Task_Parch_CraftingBuffBonus_lv5");
                    break;
            }

            if (newInventoryItem != null)
            {
                player.Inventory.RemoveItem(oldItem);
                if (!player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, newInventoryItem))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TextNPC.InventoryFullItemGround"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    player.CreateItemOnTheGround(newInventoryItem);
                }
            }

            UpdateCraftingSkills(player, mainPoints, secondaryPoints, tertiaryPoints);

            player.Out.SendUpdateCraftingSkills();
            player.SaveIntoDatabase();
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TaskMaster.GainCraftingPoints", mainPoints, secondaryPoints), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return true;
        }

        private void UpdateCraftingSkills(GamePlayer player, int mainPoints, int secondaryPoints, int tertiaryPoints)
        {
            Dictionary<eCraftingSkill, int> craftingSkills = player.CraftingSkills;
            List<eCraftingSkill> secondarySkills = new List<eCraftingSkill>();
            List<eCraftingSkill> tertiarySkills = new List<eCraftingSkill>();

            switch (player.CraftingPrimarySkill)
            {
                case eCraftingSkill.Fletching:
                    secondarySkills.Add(eCraftingSkill.WoodWorking);
                    secondarySkills.Add(eCraftingSkill.ClothWorking);
                    secondarySkills.Add(eCraftingSkill.MetalWorking);
                    secondarySkills.Add(eCraftingSkill.LeatherCrafting);
                    break;
                case eCraftingSkill.ArmorCrafting:
                case eCraftingSkill.Tailoring:
                    secondarySkills.Add(eCraftingSkill.ClothWorking);
                    secondarySkills.Add(eCraftingSkill.MetalWorking);
                    secondarySkills.Add(eCraftingSkill.LeatherCrafting);
                    tertiarySkills.Add(eCraftingSkill.SiegeCrafting);
                    break;
                case eCraftingSkill.WeaponCrafting:
                    secondarySkills.Add(eCraftingSkill.WoodWorking);
                    secondarySkills.Add(eCraftingSkill.MetalWorking);
                    secondarySkills.Add(eCraftingSkill.LeatherCrafting);
                    tertiarySkills.Add(eCraftingSkill.SiegeCrafting);
                    break;
                case eCraftingSkill.Alchemy:
                    secondarySkills.Add(eCraftingSkill.SpellCrafting);
                    secondarySkills.Add(eCraftingSkill.GemCutting);
                    secondarySkills.Add(eCraftingSkill.HerbalCrafting);
                    break;
                case eCraftingSkill.SpellCrafting:
                    secondarySkills.Add(eCraftingSkill.Alchemy);
                    secondarySkills.Add(eCraftingSkill.GemCutting);
                    secondarySkills.Add(eCraftingSkill.HerbalCrafting);
                    break;
            }

            player.GainCraftingSkill(player.CraftingPrimarySkill, mainPoints);

            foreach (var skill in secondarySkills)
            {
                player.GainCraftingSkill(skill, secondaryPoints);
            }

            foreach (var skill in tertiarySkills)
            {
                player.GainCraftingSkill(skill, tertiaryPoints);
            }
        }

        private InventoryItem CreateNewInventoryItem(string templateId)
        {
            ItemTemplate itemTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(templateId);
            if (itemTemplate != null)
            {
                return GameInventoryItem.Create(itemTemplate);
            }
            return null;
        }
    }
}
