using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Reflection;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOLDatabase.Tables;
using log4net;

namespace DOL.GS
{
    /// <summary>
    /// Data-driven task manager. One TaskDefinition per task (thresholds, category, messages, tokens),
    /// accessors bound once with delegates instead of reflection on every kill.
    /// Public API is unchanged: EnsureTaskData, UpdateTaskProgress, GetMaxLevel, GetNextLevelThreshold.
    /// </summary>
    public static class TaskManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public const string STANDARD_SAFEGUARD = "Standard";
        private const int DEFAULT_MAX_LEVEL = 5;

        private enum TaskCategory : byte { None, PvP, PvE, Crafting }

        private sealed class TaskDefinition
        {
            public string Name;
            public TaskCategory Category;
            public int MaxLevel;
            public int[] Thresholds;
            public string TokenIdPrefix;
            public string LogKey;
            public eChatType LogChatType;
            public string SpecialTokenKey;
            public string CompletionMessageKey;
            public bool TrackStats;
            public Func<TaskXPlayer, string> GetProgress;
            public Action<TaskXPlayer, string> SetProgress;
            public Func<TaskXPlayer, int> GetStats;
            public Action<TaskXPlayer, int> SetStats;
            public bool HasProgress => GetProgress != null && SetProgress != null;
            public bool HasStats => GetStats != null && SetStats != null;
        }

        private static readonly FrozenDictionary<string, TaskDefinition> _definitions = BuildDefinitions();

        private static FrozenDictionary<string, TaskDefinition> BuildDefinitions()
        {
            var d = new Dictionary<string, TaskDefinition>(StringComparer.Ordinal);
            int[] creature = [15, 25, 50, 80, 140];
            int[] special = [20, 40, 60, 85, 120];

            void A(string name, TaskCategory cat, int[] thresholds, bool stats = false, eChatType? logType = null,
                   string token = null, bool specialMsg = false, bool completionMsg = false, int maxLevel = DEFAULT_MAX_LEVEL)
            {
                d[name] = new TaskDefinition
                {
                    Name = name,
                    Category = cat,
                    Thresholds = thresholds,
                    MaxLevel = maxLevel,
                    TrackStats = stats,
                    TokenIdPrefix = token,
                    LogKey = logType.HasValue ? $"Tasks.{name}Log" : null,
                    LogChatType = logType ?? eChatType.CT_System,
                    SpecialTokenKey = specialMsg ? $"Tasks.{name}SpecialToken" : null,
                    CompletionMessageKey = completionMsg ? $"Tasks.{name}Message" : null
                };
            }

            // PvP / RvR / GvG
            A("KillEnemyPlayersGroup", TaskCategory.PvP, [30, 60, 100, 200, 400], true, eChatType.CT_ScreenCenterSmaller);
            A("KillEnemyPlayersAlone", TaskCategory.PvP, [20, 40, 80, 150, 300], true, eChatType.CT_ScreenCenterSmaller);
            A("KillKeepGuards", TaskCategory.PvP, [25, 50, 90, 150, 220], true, eChatType.CT_System);
            A("TakeKeeps", TaskCategory.PvP, [3, 6, 12, 20, 30], logType: eChatType.CT_ScreenCenterSmaller);
            A("RvRChampionOfTheDay", TaskCategory.PvP, [1, 2, 4, 7, 12], true);
            A("KillTerritoryGuards", TaskCategory.PvP, [25, 50, 90, 150, 220], true, eChatType.CT_System);
            A("KillTerritoryBoss", TaskCategory.PvP, [5, 10, 15, 30, 60], true, eChatType.CT_System);
            A("CapturedRelics", TaskCategory.PvP, [4, 8, 12, 20, 30], true, eChatType.CT_System);

            // PvE
            A("KillCreaturesInDungeons", TaskCategory.PvE, [40, 80, 140, 220, 400], true, eChatType.CT_System);
            A("KillOutdoorsCreatures", TaskCategory.PvE, [50, 100, 200, 350, 700], true, eChatType.CT_System);
            foreach (string t in new[] { "Animal", "Demon", "Dragon", "Elemental", "Giant", "Humanoid", "Insect", "Magical", "Reptile", "Plant", "Undead" })
                A($"Kill{t}Creatures", TaskCategory.PvE, creature);

            // Crafting
            A("SuccessfulItemCombinations", TaskCategory.Crafting, [30, 50, 80, 110, 160], true, eChatType.CT_System);
            A("MasteredCrafts", TaskCategory.Crafting, [40, 60, 100, 150, 200], true, eChatType.CT_System);
            A("MasterpieceCrafted", TaskCategory.Crafting, [2, 4, 8, 15, 25], true, eChatType.CT_System);

            // Special (give their own token)
            A("EpicBossesSlaughtered", TaskCategory.None, [15, 25, 50, 80, 120], logType: eChatType.CT_System, token: "TaskToken_Demon_Slayer_lv", specialMsg: true);
            A("ItemsSoldToPlayers", TaskCategory.None, [25, 40, 75, 110, 150], true, eChatType.CT_System, "TaskToken_Trader_lv", true);
            A("SuccessfulPvPThefts", TaskCategory.None, special, true, eChatType.CT_System, "TaskToken_Thief_lv", true);
            A("OutlawPlayersSentToJail", TaskCategory.None, special, true, eChatType.CT_System, "TaskToken_Bounty_Hunter_lv", true);
            A("EnemiesKilledInAdrenalineMode", TaskCategory.None, special, true, eChatType.CT_System, "TaskToken_Wrath_lv", true);
            A("EnemyKilledInDuel", TaskCategory.None, [40, 60, 100, 150, 200], true, eChatType.CT_System, "TaskToken_Duelist_lv", true);
            A("QuestsCompleted", TaskCategory.None, [15, 25, 50, 80, 120], true, eChatType.CT_System, "TaskToken_Adventurer_lv", true);

            // Completion tokens
            A("TurnInPvPGvGTaskToken", TaskCategory.None, [1, 3, 6, 12, 18], token: "TaskToken_PvPGvG_lv", completionMsg: true);
            A("TurnInPvETaskToken", TaskCategory.None, [1, 3, 5, 10, 18, 28], token: "TaskToken_PvE_lv", completionMsg: true, maxLevel: 6);
            A("TurnInCraftingTaskToken", TaskCategory.None, [1, 2, 3, 4, 5], token: "TaskToken_Crafting_lv", completionMsg: true);

            foreach (TaskDefinition def in d.Values)
                BindAccessors(def);

            return d.ToFrozenDictionary();
        }

        private static void BindAccessors(TaskDefinition d)
        {
            Type t = typeof(TaskXPlayer);
            PropertyInfo p = t.GetProperty(d.Name, BindingFlags.Public | BindingFlags.Instance);

            if (p == null || !p.CanRead || !p.CanWrite)
                log.Warn($"[TaskManager] TaskXPlayer has no read/write property '{d.Name}', task disabled.");
            else if (p.PropertyType == typeof(string))
            {
                d.GetProgress = (Func<TaskXPlayer, string>)Delegate.CreateDelegate(typeof(Func<TaskXPlayer, string>), p.GetGetMethod()!);
                d.SetProgress = (Action<TaskXPlayer, string>)Delegate.CreateDelegate(typeof(Action<TaskXPlayer, string>), p.GetSetMethod()!);
            }
            else
            {
                d.GetProgress = td => Convert.ToString(p.GetValue(td));
                d.SetProgress = (td, v) => p.SetValue(td, Convert.ChangeType(v, p.PropertyType));
            }

            if (!d.TrackStats) return;

            PropertyInfo s = t.GetProperty(d.Name + "Stats", BindingFlags.Public | BindingFlags.Instance);
            if (s == null || !s.CanRead || !s.CanWrite)
            {
                log.Warn($"[TaskManager] TaskXPlayer has no read/write property '{d.Name}Stats', statistics disabled for this task.");
                return;
            }

            if (s.PropertyType == typeof(int))
            {
                d.GetStats = (Func<TaskXPlayer, int>)Delegate.CreateDelegate(typeof(Func<TaskXPlayer, int>), s.GetGetMethod()!);
                d.SetStats = (Action<TaskXPlayer, int>)Delegate.CreateDelegate(typeof(Action<TaskXPlayer, int>), s.GetSetMethod()!);
            }
            else
            {
                d.GetStats = td => Convert.ToInt32(s.GetValue(td));
                d.SetStats = (td, v) => s.SetValue(td, Convert.ChangeType(v, s.PropertyType));
            }
        }

        public static bool IsKnownTask(string name) => name != null && _definitions.ContainsKey(name);

        public static TaskXPlayer EnsureTaskData(GamePlayer p)
        {
            if (!Properties.ENABLE_TASK_SYSTEM || p == null) return null;
            var db = GameServer.Database;
            var d = db.SelectObject<TaskXPlayer>(t => t.PlayerId == p.InternalID) ?? db.SelectObject<TaskXPlayer>(t => t.PlayerName == p.Name);
            if (d != null) return d;
            d = new TaskXPlayer { PlayerId = p.InternalID, PlayerName = p.Name };
            db.AddObject(d);
            return d;
        }

        /// <summary>Convenience: applies the con-level safeguard then awards progress. Returns false if the kill did not count.</summary>
        public static bool TryAwardKill(GamePlayer killer, GameLiving enemy, string taskName, int progress = 1, bool isArena = false)
        {
            if (!Properties.ENABLE_TASK_SYSTEM || killer == null || enemy == null) return false;
            if (!GameLiving.TaskSafeguardCheck(killer, enemy, taskName, isArena)) return false;
            UpdateTaskProgress(killer, taskName, progress);
            return true;
        }

        public static void UpdateTaskProgress(GamePlayer p, string taskName, int progress)
        {
            if (!Properties.ENABLE_TASK_SYSTEM || p == null) return;

            if (taskName == null || !_definitions.TryGetValue(taskName, out TaskDefinition d) || !d.HasProgress)
            {
                p.Out?.SendMessage($"[TaskManager] Unknown task '{taskName}'.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            TaskXPlayer data = p.TaskXPlayer ?? EnsureTaskData(p);
            if (data == null) return;

            ParseProgress(d.GetProgress(data), out int level, out int current);
            int nextThreshold = GetNextLevelThreshold(p, d, level);

            if (d.HasStats)
                d.SetStats(data, d.GetStats(data) + 1);

            if (level >= d.MaxLevel)
            {
                GameServer.Database.SaveObject(data);
                return;
            }

            current += progress;
            bool leveledUp = false;

            if (current >= nextThreshold)
            {
                level++;
                current = 0;
                leveledUp = true;

                switch (d.Category)
                {
                    case TaskCategory.PvP: UpdateCompletionToken(p, data, "TurnInPvPGvGTaskToken", 1); break;
                    case TaskCategory.PvE: UpdateCompletionToken(p, data, "TurnInPvETaskToken", 1); break;
                    case TaskCategory.Crafting: UpdateCompletionToken(p, data, "TurnInCraftingTaskToken", 1); break;
                }

                if (d.SpecialTokenKey != null)
                    p.Out.SendMessage(Translate(p, d.SpecialTokenKey, level), eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);

                GiveTaskToken(p, d, level);
            }

            d.SetProgress(data, $"{level}|{current}");
            GameServer.Database.SaveObject(data);

            if (d.LogKey != null)
                p.Out.SendMessage(Translate(p, d.LogKey, leveledUp ? nextThreshold : current, nextThreshold), d.LogChatType, eChatLoc.CL_SystemWindow);
        }

        public static int GetMaxLevel(string taskName)
            => taskName != null && _definitions.TryGetValue(taskName, out TaskDefinition d) ? d.MaxLevel : DEFAULT_MAX_LEVEL;

        public static int GetNextLevelThreshold(GamePlayer p, string taskName, int level)
        {
            if (p?.Client?.Account != null && p.Client.Account.PrivLevel > 1) return 1;
            return taskName != null && _definitions.TryGetValue(taskName, out TaskDefinition d) ? ThresholdFor(d, level) : int.MaxValue;
        }

        private static int GetNextLevelThreshold(GamePlayer p, TaskDefinition d, int level)
        {
            if (p?.Client?.Account != null && p.Client.Account.PrivLevel > 1) return 1;
            return ThresholdFor(d, level);
        }

        private static int ThresholdFor(TaskDefinition d, int level)
            => level >= 0 && level < d.Thresholds.Length ? d.Thresholds[level] : int.MaxValue;

        private static void UpdateCompletionToken(GamePlayer p, TaskXPlayer data, string tokenTask, int points)
        {
            if (!Properties.ENABLE_TASK_SYSTEM || !_definitions.TryGetValue(tokenTask, out TaskDefinition d) || !d.HasProgress) return;

            ParseProgress(d.GetProgress(data), out int level, out int current);
            current += points;

            if (level < d.MaxLevel && current >= GetNextLevelThreshold(p, d, level))
            {
                level++;
                current = 0;
                GiveTaskToken(p, d, level);
                if (d.CompletionMessageKey != null)
                    p.Out.SendMessage(Translate(p, d.CompletionMessageKey, level), eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
            }

            d.SetProgress(data, $"{level}|{current}");
        }

        private static void GiveTaskToken(GamePlayer p, TaskDefinition d, int level)
        {
            if (d.TokenIdPrefix == null) return;
            ItemTemplate template = GameServer.Database.FindObjectByKey<ItemTemplate>(d.TokenIdPrefix + level);
            if (template == null) return;

            if (!p.Inventory.AddTemplate(GameInventoryItem.Create(template), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
            {
                p.Out.SendMessage(Translate(p, "TextNPC.InventoryFullItemGround"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                var item = GameInventoryItem.Create(template);
                item.Count = 1;
                p.CreateItemOnTheGround(item);
                InventoryLogging.LogInventoryAction(p, "", $"(ground;{p.InternalID})", eInventoryActionType.Quest, item, 1);
            }
            else
                InventoryLogging.LogInventoryAction(p, "", $"(quest;{p.InternalID})", eInventoryActionType.Quest, template, 1);
        }

        private static void ParseProgress(string raw, out int level, out int current)
        {
            level = 0;
            current = 0;
            if (string.IsNullOrEmpty(raw)) return;
            int sep = raw.IndexOf('|');
            if (sep < 0) return;
            if (!int.TryParse(raw.AsSpan(0, sep), out level)) level = 0;
            if (!int.TryParse(raw.AsSpan(sep + 1), out current)) current = 0;
        }

        private static string Translate(GamePlayer p, string key, params object[] args)
            => LanguageMgr.GetTranslation(p.Client.Account.Language, key, args);
    }
}