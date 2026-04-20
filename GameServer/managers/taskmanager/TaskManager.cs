/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

using System;
using DOL.GS;
using DOL.Database;
using DOLDatabase.Tables;
using DOL.GS.ServerProperties;
using Discord;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS
{
    public static class TaskManager
    {
        public static TaskXPlayer EnsureTaskData(GamePlayer player)
        {
            if (!Properties.ENABLE_TASK_SYSTEM)
                return null;

            var taskData = GameServer.Database.SelectObject<TaskXPlayer>(t => t.PlayerId == player.InternalID);
            if (taskData != null)
            {
                return taskData;
            }
            
            // TODO: remove this
            taskData = GameServer.Database.SelectObject<TaskXPlayer>(t => t.PlayerName == player.Name);
            if (taskData != null)
            {
                return taskData;
            }
            
            taskData = new TaskXPlayer
            {
                PlayerId = player.InternalID,
                PlayerName = player.Name
            };
            GameServer.Database.AddObject(taskData);
            return taskData;
        }

        public static void UpdateTaskProgress(GamePlayer player, string taskName, int progress)
        {
            if (!Properties.ENABLE_TASK_SYSTEM)
                return;

            var taskData = player.TaskXPlayer ?? EnsureTaskData(player);

            var prop = taskData.GetType().GetProperty(taskName);
            if (prop == null) { player?.Out?.SendMessage($"[TaskManager] Unknown task '{taskName}'.", eChatType.CT_System, eChatLoc.CL_SystemWindow); return; }

            string raw = Convert.ToString(prop.GetValue(taskData)) ?? "0|0";
            if (!raw.Contains("|")) raw = "0|0";

            var parts = raw.Split('|');
            int level = 0, currentProgress = 0;
            if (parts.Length > 0 && !int.TryParse(parts[0], out level)) level = 0;
            if (parts.Length > 1 && !int.TryParse(parts[1], out currentProgress)) currentProgress = 0;

            var maxLevel = GetMaxLevel(taskName);
            var nextLevelThreshold = GetNextLevelThreshold(player, taskName, level);

            switch (taskName)
            {
                case "KillEnemyPlayersGroup":
                    taskData.KillEnemyPlayersGroupStats++;
                    break;
                case "KillEnemyPlayersAlone":
                    taskData.KillEnemyPlayersAloneStats++;
                    break;
                case "KillKeepGuards":
                    taskData.KillKeepGuardsStats++;
                    break;
                case "RvRChampionOfTheDay":
                    taskData.RvRChampionOfTheDayStats++;
                    break;
                case "KillTerritoryGuards":
                    taskData.KillTerritoryGuardsStats++;
                    break;
                case "KillTerritoryBoss":
                    taskData.KillTerritoryBossStats++;
                    break;
                case "CapturedRelics":
                    taskData.CapturedRelicsStats++;
                    break;
                case "KillCreaturesInDungeons":
                    taskData.KillCreaturesInDungeonsStats++;
                    break;
                case "KillOutdoorsCreatures":
                    taskData.KillOutdoorsCreaturesStats++;
                    break;
                case "SuccessfulItemCombinations":
                    taskData.SuccessfulItemCombinationsStats++;
                    break;
                case "MasteredCrafts":
                    taskData.MasteredCraftsStats++;
                    break;
                case "MasterpieceCrafted":
                    taskData.MasterpieceCraftedStats++;
                    break;
                case "ItemsSoldToPlayers":
                    taskData.ItemsSoldToPlayersStats++;
                    break;
                case "SuccessfulPvPThefts":
                    taskData.SuccessfulPvPTheftsStats++;
                    break;
                case "OutlawPlayersSentToJail":
                    taskData.OutlawPlayersSentToJailStats++;
                    break;
                case "EnemiesKilledInAdrenalineMode":
                    taskData.EnemiesKilledInAdrenalineModeStats++;
                    break;
                case "EnemyKilledInDuel":
                    taskData.EnemyKilledInDuelStats++;
                    break;
                case "QuestsCompleted":
                    taskData.QuestsCompletedStats++;
                    break;
            }

            if (level >= maxLevel)
            {
                GameServer.Database.SaveObject(taskData);
                return;
            }

            currentProgress += progress;
            bool levelUp = false;
            if (level < TaskManager.GetMaxLevel(taskName) && currentProgress >= GetNextLevelThreshold(player, taskName, level))
            {
                level++;
                currentProgress = 0;
                levelUp = true;

                if (IsPvPTask(taskName))
                {
                    UpdateCompletionToken(player, taskData, "TurnInPvPGvGTaskToken", 1);
                }
                else if (IsPvETask(taskName))
                {
                    UpdateCompletionToken(player, taskData, "TurnInPvETaskToken", 1);
                }
                else if (IsCraftingTask(taskName))
                {
                    UpdateCompletionToken(player, taskData, "TurnInCraftingTaskToken", 1);
                }

                string specialTaskMessage = taskName switch
                {
                    "EpicBossesSlaughtered" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.EpicBossesSlaughteredSpecialToken", level),
                    "ItemsSoldToPlayers" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.ItemsSoldToPlayersSpecialToken", level),
                    "SuccessfulPvPThefts" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.SuccessfulPvPTheftsSpecialToken", level),
                    "OutlawPlayersSentToJail" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.OutlawPlayersSentToJailSpecialToken", level),
                    "EnemiesKilledInAdrenalineMode" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.EnemiesKilledInAdrenalineModeSpecialToken", level),
                    "EnemyKilledInDuel" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.EnemyKilledInDuelSpecialToken", level),
                    "QuestsCompleted" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.QuestsCompletedSpecialToken", level),
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(specialTaskMessage))
                {
                    player.Out.SendMessage(specialTaskMessage, eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
                }
                GiveTaskToken(player, taskName, level);
            }

            taskData.GetType().GetProperty(taskName)!.SetValue(taskData, $"{level}|{currentProgress}");
            GameServer.Database.SaveObject(taskData);

            string message = taskName switch
            {
                "KillEnemyPlayersGroup" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillEnemyPlayersGroupLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "KillEnemyPlayersAlone" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillEnemyPlayersAloneLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "TakeKeeps" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.TakeKeepsLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(message))
            {
                player.Out.SendMessage(message, eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
            }
            string logmessage = taskName switch
            {
                "KillKeepGuards" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillKeepGuardsLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "KillTerritoryGuards" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillTerritoryGuardsLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "KillTerritoryBoss" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.KillTerritoryBossLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "CapturedRelics" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.CapturedRelicsLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "SuccessfulItemCombinations" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.SuccessfulItemCombinationsLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "MasteredCrafts" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.MasteredCraftsLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "MasterpieceCrafted" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.MasterpieceCraftedLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "EpicBossesSlaughtered" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.EpicBossesSlaughteredLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "ItemsSoldToPlayers" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.ItemsSoldToPlayersLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "SuccessfulPvPThefts" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.SuccessfulPvPTheftsLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "OutlawPlayersSentToJail" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.OutlawPlayersSentToJailLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "EnemiesKilledInAdrenalineMode" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.EnemiesKilledInAdrenalineModeLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "EnemyKilledInDuel" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.EnemyKilledInDuelLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                "QuestsCompleted" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.QuestsCompletedLog", (levelUp ? nextLevelThreshold : currentProgress), nextLevelThreshold),
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(logmessage))
            {
                player.Out.SendMessage(logmessage, eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        private static void GiveTaskToken(GamePlayer player, string taskName, int level)
        {
            string itemTemplateId = taskName switch
            {
                "EpicBossesSlaughtered" => $"TaskToken_Demon_Slayer_lv{level}",
                "ItemsSoldToPlayers" => $"TaskToken_Trader_lv{level}",
                "SuccessfulPvPThefts" => $"TaskToken_Thief_lv{level}",
                "OutlawPlayersSentToJail" => $"TaskToken_Bounty_Hunter_lv{level}",
                "EnemiesKilledInAdrenalineMode" => $"TaskToken_Wrath_lv{level}",
                "EnemyKilledInDuel" => $"TaskToken_Duelist_lv{level}",
                "QuestsCompleted" => $"TaskToken_Adventurer_lv{level}",
                "TurnInPvPGvGTaskToken" => $"TaskToken_PvPGvG_lv{level}",
                "TurnInPvETaskToken" => $"TaskToken_PvE_lv{level}",
                "TurnInCraftingTaskToken" => $"TaskToken_Crafting_lv{level}",
                _ => null
            };

            if (itemTemplateId == null)
                return;

            var itemTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(itemTemplateId);

            if (itemTemplate == null)
                return;

            if (!player.Inventory.AddTemplate(GameInventoryItem.Create(itemTemplate), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TextNPC.InventoryFullItemGround"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                var invItem = GameInventoryItem.Create(itemTemplate);
                invItem.Count = 1;
                player.CreateItemOnTheGround(invItem);
                InventoryLogging.LogInventoryAction(player, "", $"(ground;{player.InternalID})", eInventoryActionType.Quest, invItem, 1);
            }
            else
            {
                InventoryLogging.LogInventoryAction(player, "", $"(quest;{player.InternalID})", eInventoryActionType.Quest, itemTemplate, 1);
            }
        }

        private static void UpdateCompletionToken(GamePlayer player, TaskXPlayer taskData, string tokenName, int points)
        {
            if (!Properties.ENABLE_TASK_SYSTEM)
                return;

            var prop = taskData.GetType().GetProperty(tokenName);
            if (prop == null) return;

            string raw = Convert.ToString(prop.GetValue(taskData)) ?? "0|0";
            if (!raw.Contains("|")) raw = "0|0";

            var parts = raw.Split('|');
            int level = 0, currentProgress = 0;
            if (parts.Length > 0 && !int.TryParse(parts[0], out level)) level = 0;
            if (parts.Length > 1 && !int.TryParse(parts[1], out currentProgress)) currentProgress = 0;

            currentProgress += points;
            if (level < TaskManager.GetMaxLevel(tokenName) && currentProgress >= GetNextLevelThreshold(player, tokenName, level))
            {
                level++;
                currentProgress = 0;
                GiveTaskToken(player, tokenName, level);

                string message = tokenName switch
                {
                    "TurnInPvPGvGTaskToken" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.TurnInPvPGvGTaskTokenMessage", level),
                    "TurnInPvETaskToken" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.TurnInPvETaskTokenMessage", level),
                    "TurnInCraftingTaskToken" => LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.TurnInCraftingTaskTokenMessage", level),
                    _ => string.Empty
                };
                if (!string.IsNullOrEmpty(message))
                {
                    player.Out.SendMessage(message, eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
                }
            }
            taskData.GetType().GetProperty(tokenName)!.SetValue(taskData, $"{level}|{currentProgress}");
        }

        private static bool IsPvPTask(string taskName)
        {
            return taskName == "KillEnemyPlayersGroup" || taskName == "KillEnemyPlayersAlone" ||
                   taskName == "KillKeepGuards" || taskName == "TakeKeeps" || taskName == "RvRChampionOfTheDay" ||
                   taskName == "KillTerritoryGuards" || taskName == "KillTerritoryBoss" || taskName == "CapturedRelics";
        }

        private static bool IsPvETask(string taskName)
        {
            return taskName == "KillCreaturesInDungeons" || taskName == "KillOutdoorsCreatures" ||
                   taskName == "KillAnimalCreatures" || taskName == "KillDemonCreatures" ||
                   taskName == "KillDragonCreatures" || taskName == "KillElementalCreatures" ||
                   taskName == "KillGiantCreatures" || taskName == "KillHumanoidCreatures" ||
                   taskName == "KillInsectCreatures" || taskName == "KillMagicalCreatures" ||
                   taskName == "KillReptileCreatures" || taskName == "KillPlantCreatures" ||
                   taskName == "KillUndeadCreatures";
        }

        private static bool IsCraftingTask(string taskName)
        {
            return taskName == "SuccessfulItemCombinations" || taskName == "MasteredCrafts" ||
                   taskName == "MasterpieceCrafted";
        }

        public static int GetMaxLevel(string taskName)
        {
            return taskName switch
            {
                "TurnInPvETaskToken" => 6,
                "TurnInPvPGvGTaskToken" => 5,
                "TurnInCraftingTaskToken" => 5,
                _ => 5
            };
        }

        public static int GetNextLevelThreshold(GamePlayer player, string taskName, int level)
        {
            if (player.Client.Account.PrivLevel > 1)
            {
                return 1;
            }

            return taskName switch
            {
                "KillEnemyPlayersGroup" => level switch
                {
                    0 => 25,
                    1 => 40,
                    2 => 80,
                    3 => 150,
                    4 => 300,
                    _ => int.MaxValue
                },
                "KillEnemyPlayersAlone" => level switch
                {
                    0 => 15,
                    1 => 30,
                    2 => 50,
                    3 => 80,
                    4 => 150,
                    _ => int.MaxValue
                },
                "KillKeepGuards" => level switch
                {
                    0 => 25,
                    1 => 50,
                    2 => 90,
                    3 => 150,
                    4 => 220,
                    _ => int.MaxValue
                },
                "TakeKeeps" => level switch
                {
                    0 => 3,
                    1 => 6,
                    2 => 12,
                    3 => 20,
                    4 => 30,
                    _ => int.MaxValue
                },
                "RvRChampionOfTheDay" => level switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 4,
                    3 => 7,
                    4 => 12,
                    _ => int.MaxValue
                },
                "KillTerritoryGuards" => level switch
                {
                    0 => 25,
                    1 => 50,
                    2 => 90,
                    3 => 150,
                    4 => 220,
                    _ => int.MaxValue
                },
                "KillTerritoryBoss" => level switch
                {
                    0 => 5,
                    1 => 10,
                    2 => 15,
                    3 => 30,
                    4 => 60,
                    _ => int.MaxValue
                },
                "CapturedRelics" => level switch
                {
                    0 => 4,
                    1 => 8,
                    2 => 12,
                    3 => 20,
                    4 => 30,
                    _ => int.MaxValue
                },
                "TurnInPvPGvGTaskToken" => level switch
                {
                    0 => 1,
                    1 => 3,
                    2 => 6,
                    3 => 12,
                    4 => 18,
                    _ => int.MaxValue
                },
                "KillCreaturesInDungeons" => level switch
                {
                    0 => 40,
                    1 => 80,
                    2 => 140,
                    3 => 220,
                    4 => 400,
                    _ => int.MaxValue
                },
                "KillOutdoorsCreatures" => level switch
                {
                    0 => 50,
                    1 => 100,
                    2 => 200,
                    3 => 350,
                    4 => 700,
                    _ => int.MaxValue
                },
                "KillAnimalCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillDemonCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillDragonCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillElementalCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillGiantCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillHumanoidCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillInsectCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillMagicalCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillReptileCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillPlantCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "KillUndeadCreatures" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 140,
                    _ => int.MaxValue
                },
                "TurnInPvETaskToken" => level switch
                {
                    0 => 1,
                    1 => 3,
                    2 => 5,
                    3 => 10,
                    4 => 18,
                    5 => 28,
                    _ => int.MaxValue
                },
                "SuccessfulItemCombinations" => level switch
                {
                    0 => 30,
                    1 => 50,
                    2 => 80,
                    3 => 110,
                    4 => 160,
                    _ => int.MaxValue
                },
                "MasteredCrafts" => level switch
                {
                    0 => 40,
                    1 => 60,
                    2 => 100,
                    3 => 150,
                    4 => 200,
                    _ => int.MaxValue
                },
                "MasterpieceCrafted" => level switch
                {
                    0 => 2,
                    1 => 4,
                    2 => 8,
                    3 => 15,
                    4 => 25,
                    _ => int.MaxValue
                },
                "TurnInCraftingTaskToken" => level switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 3,
                    3 => 4,
                    4 => 5,
                    _ => int.MaxValue
                },
                "EpicBossesSlaughtered" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 120,
                    _ => int.MaxValue
                },
                "ItemsSoldToPlayers" => level switch
                {
                    0 => 25,
                    1 => 40,
                    2 => 75,
                    3 => 110,
                    4 => 150,
                    _ => int.MaxValue
                },
                "SuccessfulPvPThefts" => level switch
                {
                    0 => 20,
                    1 => 40,
                    2 => 60,
                    3 => 85,
                    4 => 120,
                    _ => int.MaxValue
                },
                "OutlawPlayersSentToJail" => level switch
                {
                    0 => 20,
                    1 => 40,
                    2 => 60,
                    3 => 85,
                    4 => 120,
                    _ => int.MaxValue
                },
                "EnemiesKilledInAdrenalineMode" => level switch
                {
                    0 => 20,
                    1 => 40,
                    2 => 60,
                    3 => 85,
                    4 => 120,
                    _ => int.MaxValue
                },
                "EnemyKilledInDuel" => level switch
                {
                    0 => 40,
                    1 => 60,
                    2 => 100,
                    3 => 150,
                    4 => 200,
                    _ => int.MaxValue
                },
                "QuestsCompleted" => level switch
                {
                    0 => 15,
                    1 => 25,
                    2 => 50,
                    3 => 80,
                    4 => 120,
                    _ => int.MaxValue
                },
                _ => int.MaxValue
            };
        }
    }
}
