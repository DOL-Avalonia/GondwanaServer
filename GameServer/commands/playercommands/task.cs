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

using System.Collections.Generic;
using DOL.GS.PacketHandler;
using DOL.GS.Quests;
using DOL.GS.ServerProperties;
using DOL.Database;
using DOLDatabase.Tables;
using DOL.Language;
using System.Linq;
using System.Timers;
using System;

namespace DOL.GS.Commands
{
    //[CmdAttribute("&task", ePrivLevel.Player, "Ask for a Task from Guards or Merchants", "/task")]
    [CmdAttribute(
        "&task",
        ePrivLevel.Player,
        "Commands.Players.Task.Description",
        "Commands.Players.Task.Usage")]
    public class TaskCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private static List<string> allCreatureTypes = new List<string>
        {
            "KillAnimalCreatures",
            "KillDemonCreatures",
            "KillDragonCreatures",
            "KillElementalCreatures",
            "KillGiantCreatures",
            "KillHumanoidCreatures",
            "KillInsectCreatures",
            "KillMagicalCreatures",
            "KillReptileCreatures",
            "KillPlantCreatures",
            "KillUndeadCreatures"
        };

        private static List<string> activeCreatureTypes = new List<string>();
        private static Timer rotationTimer;

        static TaskCommandHandler()
        {
            InitializeTimer();
            RotateCreatureTypes(null, null);
        }

        private static void InitializeTimer()
        {
            rotationTimer = new Timer(Properties.TASK_PVE_MOBTYPE_ROTATION * 60 * 1000);
            rotationTimer.Elapsed += RotateCreatureTypes;
            rotationTimer.AutoReset = true;
            rotationTimer.Enabled = true;
        }

        private static void RotateCreatureTypes(object source, ElapsedEventArgs e)
        {
            var random = new Random();
            activeCreatureTypes = allCreatureTypes.OrderBy(x => random.Next()).Take(4).ToList();
        }

        public static List<string> GetActiveCreatureTypes()
        {
            return new List<string>(activeCreatureTypes);
        }

        public void OnCommand(GameClient client, string[] args)
        {
            if (!Properties.ENABLE_TASK_SYSTEM)
            {
                client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Tasks.SystemDisabled"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (IsSpammingCommand(client.Player, "task"))
                return;

            GamePlayer player = client.Player;
            string playerName = player.Name;
            var taskData = player.TaskXPlayer ?? TaskManager.EnsureTaskData(player);

            var messages = new List<string>
            {
                Properties.PVP_ALLOW_TASKSPOINTS
                    ? LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.PvPRvRGvGTasks")
                    : LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.RvRGvGTasksNoPvP"),

                FormatTaskStatus(player, "KillEnemyPlayersGroup", taskData.KillEnemyPlayersGroup),
                FormatTaskStatus(player, "KillEnemyPlayersAlone", taskData.KillEnemyPlayersAlone),
                FormatTaskStatus(player, "KillKeepGuards", taskData.KillKeepGuards),
                FormatTaskStatus(player, "TakeKeeps", taskData.TakeKeeps),
                FormatTaskStatus(player, "RvRChampionOfTheDay", taskData.RvRChampionOfTheDay),
                "",
                FormatTaskStatus(player, "KillTerritoryGuards", taskData.KillTerritoryGuards),
                FormatTaskStatus(player, "KillTerritoryBoss", taskData.KillTerritoryBoss),
                FormatTaskStatus(player, "CapturedRelics", taskData.CapturedRelics),
                "",
                FormatTaskStatus(player, "TurnInPvPGvGTaskToken", taskData.TurnInPvPGvGTaskToken),
                "",
                "",
                LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.PvETasks"),
                FormatTaskStatus(player, "KillCreaturesInDungeons", taskData.KillCreaturesInDungeons),
                FormatTaskStatus(player, "KillOutdoorsCreatures", taskData.KillOutdoorsCreatures),
                "",
            };

            foreach (var creatureType in activeCreatureTypes)
            {
                messages.Add(FormatTaskStatus(player, creatureType, taskData.GetType().GetProperty(creatureType)!.GetValue(taskData)!.ToString()));
            }

            messages.Add("");
            messages.Add(FormatTaskStatus(player, "TurnInPvETaskToken", taskData.TurnInPvETaskToken));
            messages.Add("");
            messages.Add("");
            messages.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.CraftingTasks", Properties.CRAFTING_TASKTOKEN_MINRECIPELVL));
            messages.Add(FormatTaskStatus(player, "SuccessfulItemCombinations", taskData.SuccessfulItemCombinations));
            messages.Add(FormatTaskStatus(player, "MasteredCrafts", taskData.MasteredCrafts));
            messages.Add(FormatTaskStatus(player, "MasterpieceCrafted", taskData.MasterpieceCrafted));
            messages.Add("");
            messages.Add(FormatTaskStatus(player, "TurnInCraftingTaskToken", taskData.TurnInCraftingTaskToken));
            messages.Add("");
            messages.Add("");
            messages.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.SpecialTasks"));
            messages.Add(FormatTaskStatus(player, "EpicBossesSlaughtered", taskData.EpicBossesSlaughtered));
            messages.Add(FormatTaskStatus(player, "ItemsSoldToPlayers", taskData.ItemsSoldToPlayers));
            // Conditionally add "SuccessfulPvPThefts" task
            if (IsClassAllowedToSteal(player))
            {
                messages.Add(FormatTaskStatus(player, "SuccessfulPvPThefts", taskData.SuccessfulPvPThefts));
            }
            messages.Add(FormatTaskStatus(player, "OutlawPlayersSentToJail", taskData.OutlawPlayersSentToJail));
            messages.Add(FormatTaskStatus(player, "EnemiesKilledInAdrenalineMode", taskData.EnemiesKilledInAdrenalineMode));
            messages.Add(FormatTaskStatus(player, "EnemyKilledInDuel", taskData.EnemyKilledInDuel));
            messages.Add(FormatTaskStatus(player, "QuestsCompleted", taskData.QuestsCompleted));
            messages.Add("");
            messages.Add("");
            messages.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.TaskCompletionTokenInfo"));
            messages.Add("");
            messages.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.SpecialTaskCompletionTokenInfo"));
            messages.Add("");
            messages.Add("");

            player.Out.SendCustomTextWindow(LanguageMgr.GetTranslation(player.Client.Account.Language, "Tasks.SnapshotTitle"), messages);
        }

        private string FormatTaskStatus(GamePlayer player, string taskName, string taskData)
        {
            // Default & sanitize
            if (string.IsNullOrWhiteSpace(taskData)) taskData = "0|0";
            var parts = taskData.Split('|');

            int level = 0, currentProgress = 0;
            if (parts.Length > 0 && !int.TryParse(parts[0], out level)) level = 0;
            if (parts.Length > 1 && !int.TryParse(parts[1], out currentProgress)) currentProgress = 0;

            var maxLevel = TaskManager.GetMaxLevel(taskName);
            string language = player.Client.Account.Language;

            if (level >= maxLevel)
                return $"+ {LanguageMgr.GetTranslation(language, $"Tasks.{taskName.Replace(" ", "")}")} ({LanguageMgr.GetTranslation(language, "Tasks.MaxLevelReached")})";

            var maxProgress = TaskManager.GetNextLevelThreshold(player, taskName, level);
            return $"+ {LanguageMgr.GetTranslation(language, $"Tasks.{taskName.Replace(" ", "")}")} ({LanguageMgr.GetTranslation(language, "Tasks.Level", level)}) : {currentProgress}/{maxProgress}";
        }

        private bool IsClassAllowedToSteal(GamePlayer player)
        {
            switch (player.CharacterClass.ID)
            {
                case (byte)eCharacterClass.AlbionRogue:
                case (byte)eCharacterClass.MidgardRogue:
                case (byte)eCharacterClass.Stalker:
                case (byte)eCharacterClass.Minstrel:
                case (byte)eCharacterClass.Infiltrator:
                case (byte)eCharacterClass.Scout:
                case (byte)eCharacterClass.Hunter:
                case (byte)eCharacterClass.Shadowblade:
                case (byte)eCharacterClass.Ranger:
                case (byte)eCharacterClass.Nightshade:
                    return true;

                default:
                    return false;
            }
        }
    }
}
