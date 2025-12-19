using DOL.Database;
using DOL.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.Behaviour;
using DOL.GameEvents;
using System.Threading.Tasks;
using DOL.MobGroups;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace DOL.GS.Quests
{
    public abstract class DataQuestJsonGoal
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        
        public readonly ushort QuestId;
        public DataQuestJson Quest => DataQuestJsonMgr.GetQuest(QuestId);
        public readonly int GoalId;
        public virtual bool IsNegativeGoal => false;

        public GameNPC Target { get; set; }
        public string Description { get; set; }
        public abstract eQuestGoalType Type { get; }
        public abstract int ProgressTotal { get; }
        public virtual QuestZonePoint PointA => QuestZonePoint.None;
        public virtual QuestZonePoint PointB => QuestZonePoint.None;
        public virtual ItemTemplate QuestItem => null;
        public virtual bool Visible => true;
        public ItemTemplate GiveItemTemplate { get; set; }
        public ItemTemplate StartItemTemplate { get; set; }
        public bool hasInteraction { get; set; } = false;

        public string MessageStarted { get; set; }
        public string MessageAborted { get; set; }
        public string MessageDone { get; set; }
        public string MessageCompleted { get; set; }

        public List<int> StartGoalsDone { get; set; } = new();
        public List<int> EndWhenGoalsDone { get; set; } = new();

        public bool StartEvent { get; set; } = false;
        public bool ResetEvent { get; set; } = false;
        public string EventId { get; set; } = "";

        public record GoalConditions(bool? IsDamned = null, ushort ModelId = 0)
        {
            public bool Validate(PlayerQuest quest, DataQuestJsonGoal goal)
            {
                if (IsDamned != null && IsDamned.Value != quest.Owner.IsDamned)
                    return false;

                if (ModelId != 0 && quest.Owner.Model != ModelId)
                    return false;

                return true;
            }
        }

        public GoalConditions Conditions { get; protected set; } = new();

        public DataQuestJsonGoal(DataQuestJson quest, int goalId, dynamic db)
        {
            QuestId = quest.Id;
            GoalId = goalId;
            Description = db.Description;
            MessageStarted = db.MessageStarted ?? "";
            MessageAborted = db.MessageAborted ?? "";
            MessageDone = db.MessageDone ?? "";
            MessageCompleted = db.MessageCompleted ?? "";
            string item = db.GiveItem ?? "";
            if (!string.IsNullOrWhiteSpace(item))
                GiveItemTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(item);
            if (db.StartGoalsDone != null)
                item = db.StartItem ?? "";
            if (!string.IsNullOrWhiteSpace(item))
                StartItemTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(item);
            if (db.StartGoalsDone != null)
                foreach (var id in db.StartGoalsDone)
                    StartGoalsDone.Add((int)id);
            else if (GoalId > 1)
                StartGoalsDone.Add((int)goalId - 1);
            if (db.EndWhenGoalsDone != null)
                foreach (var id in db.EndWhenGoalsDone)
                    EndWhenGoalsDone.Add((int)id);
            if (db.StartEvent != null && db.StartEvent != "")
                StartEvent = (bool)db.StartEvent;
            if (db.ResetEvent != null && db.ResetEvent != "")
                ResetEvent = (bool)db.ResetEvent;
            if (db.EventId != null && db.EventId != "")
                EventId = db.EventId;
            if (db.Conditions != null)
            {
                // var condObj = JsonConvert.DeserializeObject<GoalConditions>(db.Conditions);
                Conditions = db.Conditions.ToObject<GoalConditions>();
            }
        }

        /// <summary>
        /// Personalize a goal text (placeholders) and translate it
        /// from server language (FR) to the player's language using AutoTranslate.
        /// Returns original text if translation is disabled or fails.
        /// </summary>
        protected string TranslateGoalText(GamePlayer player, string template, bool personalize = true)
        {
            if (player == null || string.IsNullOrWhiteSpace(template))
                return template;

            string text = template;

            if (personalize)
                text = BehaviourUtils.GetPersonalizedMessage(text, player);

            return string.Empty; // AutoTranslateManager.MaybeTranslateServerText(player, text);
        }

        public bool IsActive(PlayerQuest questData) => questData.GoalStates.Any(gs => gs.GoalId == GoalId && gs.IsActive) && Conditions?.Validate(questData, this) != false;
        public bool IsDone(PlayerQuest questData) => questData.GoalStates.Any(gs => gs.GoalId == GoalId && gs.IsDone);
        public bool IsFinished(PlayerQuest questData) => questData.GoalStates.Any(gs => gs.GoalId == GoalId && gs.IsFinished);

        public void NotifyActive(PlayerQuest quest, DOLEvent e, object sender, EventArgs args)
        {
            var goalData = quest.GoalStates.Find(gs => gs.GoalId == GoalId);
            NotifyActive(quest, goalData, e, sender, args);
        }
        protected abstract void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args);

        // this one is always called, useful if you want to start a goal with some hidden task
        public void Notify(PlayerQuest questData, DOLEvent e, object sender, EventArgs args)
        {
            var goalData = questData.GoalStates.Find(gs => gs.GoalId == GoalId);
            Notify(questData, goalData, e, sender, args);
        }
        // this one is always called, useful if you want to start a goal with some hidden task
        public virtual void Notify(PlayerQuest questData, PlayerGoalState goalData, DOLEvent e, object sender, EventArgs args) { }

        public virtual bool CanStart(PlayerQuest questData)
        {
            if (IsActive(questData) || IsFinished(questData))
                return false;
            return StartGoalsDone.All(gId => questData.GoalStates.Any(gs => gs.GoalId == gId && gs.IsDone));
        }

        public virtual bool CanComplete(PlayerQuest questData)
        {
            var gs = questData.GoalStates.Find(s => s.GoalId == GoalId);
            return gs?.State == eQuestGoalStatus.DoneAndActive && Conditions?.Validate(questData, this) != false && EndWhenGoalsDone.All(id => questData.GoalStates.Any(s => s.GoalId == id && s.IsDone));
        }

        public virtual bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target) => false;

        public PlayerGoalState StartGoal(PlayerQuest questData)
        {
            if (CanStart(questData))
                return ForceStartGoal(questData);
            return null;
        }
        public virtual PlayerGoalState ForceStartGoal(PlayerQuest questData)
        {
            var goalData = new PlayerGoalState
            {
                GoalId = GoalId,
                State = eQuestGoalStatus.Active,
            };
            questData.GoalStates.Add(goalData);
            var player = questData.Owner;
            if (StartItemTemplate != null)
                GiveItem(player, StartItemTemplate);
            if (Visible)
            {
                player.Out.SendQuestUpdate(questData);
                string desc = TranslateGoalText(player, Description);

                if (ProgressTotal == 1)
                    ChatUtil.SendScreenCenter(player, desc);
                else
                    ChatUtil.SendScreenCenter(player, $"{desc} - {goalData.Progress}/{ProgressTotal}");
            }
            if (!string.IsNullOrWhiteSpace(MessageStarted))
            {
                string msg = TranslateGoalText(player, MessageStarted);
                ChatUtil.SendImportant(player, $"[Quest {Quest.Name}] " + msg);
            }
            return goalData;
        }

        public virtual bool AdvanceGoal(PlayerQuest questData, PlayerGoalState goalData, bool force = false)
        {
            if (!force && Conditions?.Validate(questData, this) == false)
                return false;
            
            goalData.Progress += 1;
            if (goalData.Progress >= ProgressTotal)
            {
                return EndGoal(questData, goalData);
            }
            questData.SaveIntoDatabase();
            if (Visible)
            {
                var player = questData.Owner;
                questData.Owner.Out.SendQuestUpdate(questData);

                string desc = TranslateGoalText(player, Description);

                if (ProgressTotal == 1)
                    ChatUtil.SendScreenCenter(player, desc);
                else
                    ChatUtil.SendScreenCenter(player, $"{desc} - {goalData.Progress}/{ProgressTotal}");
            }
            return false;
        }
        public virtual void DecreaseGoal(PlayerQuest questData, PlayerGoalState goalData)
        {
            goalData.Progress -= 1;
            questData.SaveIntoDatabase();
            if (Visible)
            {
                var player = questData.Owner;
                questData.Owner.Out.SendQuestUpdate(questData);

                string desc = TranslateGoalText(player, Description);

                if (ProgressTotal == 1)
                    ChatUtil.SendScreenCenter(player, desc);
                else
                    ChatUtil.SendScreenCenter(player, $"{desc} - {goalData.Progress}/{ProgressTotal}");
            }
        }

        public virtual void AbortGoal(PlayerQuest questData)
        {
            var goalState = questData.GoalStates.Find(gs => gs.GoalId == GoalId);
            if (goalState == null)
            {
                goalState = new PlayerGoalState
                {
                    GoalId = GoalId,
                    Progress = 0,
                    State = eQuestGoalStatus.Aborted,
                };
                questData.GoalStates.Add(goalState);
            }
            else if (!goalState.IsFinished)
                goalState.State = eQuestGoalStatus.Aborted;

            var player = questData.Owner;
            if (goalState.State == eQuestGoalStatus.Aborted && !string.IsNullOrWhiteSpace(MessageAborted))
            {
                string msg = TranslateGoalText(player, MessageAborted);
                ChatUtil.SendImportant(player, $"[Quest {Quest.Name}] " + msg);
            }
        }

        public virtual bool EndGoal(PlayerQuest questData, PlayerGoalState goalData, bool force = false)
        {
            if (EndGoal(questData, goalData, null, force))
            {
                questData.SaveIntoDatabase();
                questData.Owner.Out.SendQuestUpdate(questData);
                return true;
            }
            return false;
        }

        /// <summary>Recursive call</summary>
        private bool EndGoal(PlayerQuest questData, PlayerGoalState goalData, List<DataQuestJsonGoal> except, bool force = false)
        {
            if (!force && Conditions?.Validate(questData, this) == false)
                return false;

            goalData.Progress = ProgressTotal;
            goalData.State = eQuestGoalStatus.DoneAndActive;

            var player = questData.Owner;
            if (Visible)
            {
                string desc = TranslateGoalText(player, Description);

                if (ProgressTotal == 1)
                    ChatUtil.SendScreenCenter(player, desc);
                else
                    ChatUtil.SendScreenCenter(player, $"{desc} - {goalData.Progress}/{ProgressTotal}");
            }
            if (!string.IsNullOrWhiteSpace(MessageDone))
            {
                string msg = TranslateGoalText(player, MessageDone);
                ChatUtil.SendImportant(player, $"[Quest {Quest.Name}] " + msg);
            }
            EndOtherGoals(questData, except ?? new List<DataQuestJsonGoal>());

            CompleteGoal(questData, goalData);
            return true;
        }

        private void EndOtherGoals(PlayerQuest questData, List<DataQuestJsonGoal> except)
        {
            except.Add(this);
            foreach (var goal in Quest.Goals.Values)
                if (!except.Contains(goal) && goal.CanComplete(questData))
                    goal.EndGoal(questData, questData.GoalStates.Find(s => s.GoalId == goal.GoalId), except);
        }

        private void CompleteGoal(PlayerQuest questData, PlayerGoalState goalData)
        {
            // try starting new goals
            foreach (var goal in Quest.Goals.Values)
                goal.StartGoal(questData);

            if (!CanComplete(questData))
                return;

            var player = questData.Owner;
            if (GiveItemTemplate != null)
                GiveItem(player, GiveItemTemplate);
            goalData.State = eQuestGoalStatus.Completed;
            if (!string.IsNullOrWhiteSpace(MessageCompleted))
            {
                string msg = TranslateGoalText(player, MessageCompleted);
                ChatUtil.SendImportant(player, $"[Quest {Quest.Name}] " + msg);
            }

            foreach (var e in GameEventManager.Instance.GetEventsStartedByQuest(Quest.Id + "-" + GoalId)!.Where(e => e.IsReady && e.StartConditionType == StartingConditionType.Quest))
            {
                Task.Run(() => e.Start(player));
            }
            if (!string.IsNullOrEmpty(EventId))
            {
                var questEvent = GameEventManager.Instance.GetEventByID(EventId);
                if (questEvent == null)
                {
                    log.Warn($"Quest {QuestId} ({Quest.Name}) goal {GoalId}  is linked to event {Quest.StartEventId} which was not found");
                }
                else
                {
                    if (ResetEvent)
                    {
                        questEvent.GetInstance(player)?.Reset();
                    }
                    if (StartEvent)
                    {
                        Task.Run(() => questEvent.Start(player));
                    }
                }
            }
            if (Quest.RelatedMobGroups != null)
            {
                foreach (MobGroup group in Quest.RelatedMobGroups.Where(g => g.CompletedStepQuestID == goalData.GoalId))
                {
                    questData.UpdateGroupMob(group);
                }
            }
            questData.Quest.SendNPCsQuestEffects(questData.Owner);
        }

        public virtual IQuestGoal ToQuestGoal(PlayerQuest questData, PlayerGoalState goalData)
            => new GenericDataQuestGoal(this, questData, goalData?.Progress ?? 0, goalData?.State ?? eQuestGoalStatus.NotStarted);

        /// <summary>
        /// Returns the object to be saved as JSON given back as third argument in the constructor for loading
        /// </summary>
        /// <returns>A serialisable object</returns>
        public virtual Dictionary<string, object> GetDatabaseJsonObject()
        {
            return new Dictionary<string, object>
            {
                { "Description", Description },
                { "GiveItem", GiveItemTemplate?.Id_nb },
                { "StartItem", StartItemTemplate?.Id_nb },
                { "MessageStarted", MessageStarted },
                { "MessageAborted", MessageAborted },
                { "MessageDone", MessageDone },
                { "MessageCompleted", MessageCompleted },
                { "StartGoalsDone", StartGoalsDone.Count > 0 ? StartGoalsDone : null },
                { "EndWhenGoalsDone", EndWhenGoalsDone.Count > 0 ? EndWhenGoalsDone : null },
                { "StartEvent", StartEvent ? "1" : "0" },
                { "ResetEvent", ResetEvent ? "1" : "0"  },
                { "EventId", EventId },
                { "Conditions", Conditions }
            };
        }

        public virtual void Unload()
        {
            // nothing to do but we need to remove some handler sometimes
        }

        protected static void GiveItem(GamePlayer player, ItemTemplate itemTemplate)
        {
            var item = GameInventoryItem.Create(itemTemplate);
            if (!player.ReceiveItem(null, item))
            {
                player.CreateItemOnTheGround(item);
                ChatUtil.SendImportant(player, $"Your backpack is full, {itemTemplate.Name} is dropped on the ground.");
            }
        }

        public class GenericDataQuestGoal : IQuestGoal
        {
            private readonly string _description;
            public string Description => _description ?? Goal.Description ?? string.Empty;
            public eQuestGoalType Type => Goal.Type;
            public int Progress { get; set; }
            public int ProgressTotal => Goal.ProgressTotal;
            public QuestZonePoint PointA => Goal.PointA;
            public QuestZonePoint PointB => Goal.PointB;
            public eQuestGoalStatus Status { get; set; }
            public ItemTemplate QuestItem => Goal.QuestItem;

            public DataQuestJsonGoal Goal { get; init; }

            public GenericDataQuestGoal(DataQuestJsonGoal goal, PlayerQuest questData, int progress, eQuestGoalStatus status)
            {
                Goal = goal;
                Progress = progress;
                Status = status;

                var player = questData?.Owner;
                _description = goal.TranslateGoalText(player, goal.Description) ?? goal.Description ?? string.Empty;
            }
        }
    }
}
