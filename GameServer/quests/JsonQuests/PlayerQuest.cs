﻿using DOL.Database;
using DOL.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.GameEvents;

namespace DOL.GS.Quests
{
    public class PlayerGoalState
    {
        public int GoalId;
        public int Progress = 0;
        public object CustomData = null;
        public eQuestGoalStatus State = eQuestGoalStatus.NotStarted;

        public bool IsActive => (State & eQuestGoalStatus.FlagActive) != 0;
        public bool IsDone => (State & eQuestGoalStatus.FlagDone) != 0;
        public bool IsFinished => (State & eQuestGoalStatus.FlagFinished) != 0;
    }

    /// <summary>
    /// This class hold the data about progression of a player for a DataQuestJson
    /// </summary>
    public class PlayerQuest : IQuestPlayerData
    {
        private ushort m_questId;
        public ushort QuestId => m_questId;
        public readonly List<PlayerGoalState> GoalStates = new();

        public DataQuestJson Quest => DataQuestJsonMgr.Quests[m_questId];

        public GamePlayer Owner { get; }
        public DBQuest DbQuest;


        public eQuestStatus Status => (eQuestStatus)DbQuest.Step;

        public IList<IQuestGoal> Goals => Quest.Goals.Values.Select(g => g.ToQuestGoal(this, GoalStates.Find(gs => gs.GoalId == g.GoalId))).ToList();
        public IList<IQuestGoal> VisibleGoals => Quest.GetVisibleGoals(this);

        public IQuestRewards FinalRewards => new QuestRewards(Quest);

        public static PlayerQuest CreateQuestPreview(DataQuestJson quest, GamePlayer owner)
        {
            var dbQuest = new DBQuest(quest.Name, (int)eQuestStatus.NotDoing, owner.InternalID);
            return new PlayerQuest(owner)
            {
                m_questId = quest.Id,
                DbQuest = dbQuest,
            };
        }

        private PlayerQuest(GamePlayer owner)
        {
            Owner = owner;
        }
        public PlayerQuest(GamePlayer owner, DBQuest dbquest)
        {
            Owner = owner;
            DbQuest = dbquest;
            var json = JsonConvert.DeserializeObject<JsonState>(dbquest.CustomPropertiesString);
            m_questId = json.QuestId;
            if (!DataQuestJsonMgr.Quests.ContainsKey(m_questId))
                DataQuestJsonMgr.Quests.Add(m_questId, new DataQuestJson { Name = "ERROR" });

            if (json.Goals != null)
                GoalStates = json.Goals;
            else
                // start the quest
                Quest.Goals.Values.Where(g => g.CanStart(this)).Foreach(g => g.StartGoal(this));

            // shoudn't happen, we start the next goal
            if (VisibleGoals.Count == 0)
                Quest.Goals.Values.Foreach(g => g.StartGoal(this));

            // happen when the quest has been removed
            if (VisibleGoals.Count == 0)
                new RegionTimer(owner, _timer =>
                {
                    AbortQuest();
                    return 0;
                }).Start(1);
        }

        public bool CheckQuestQualification(GamePlayer player) => Quest.CheckQuestQualification(player);

        public void Notify(DOLEvent e, object sender, EventArgs args) => Quest.Notify(this, e, sender, args);

        public void SaveIntoDatabase()
        {
            DbQuest.CustomPropertiesString = JsonConvert.SerializeObject(new JsonState { QuestId = QuestId, Goals = GoalStates });
            if (DbQuest.IsPersisted)
                GameServer.Database.SaveObject(DbQuest);
            else
                GameServer.Database.AddObject(DbQuest);
        }

        public bool CanInteractWith(object actor)
        {
            if (actor is not GameObject gameObject)
                return false;
            foreach (var goal in Quest.Goals.Values)
                if (goal.CanInteractWith(this, GoalStates.Find(gs => gs.GoalId == goal.GoalId), gameObject))
                    return true;
            return false;
        }

        /// <summary>
        /// Check if last in GoalStates isActive and EndGoal
        /// </summary>
        public bool CanFinish()
        {
            foreach (var goal in Quest.Goals.Values.Where(g => g is EndGoal))
                if (GoalStates.Find(gs => gs.GoalId == goal.GoalId)?.IsActive ?? false)
                    return true;
            return false;
        }

        public void FinishQuest()
        {
            DbQuest.Step = (int)eQuestStatus.Done;
            Owner.Out.SendMessage(String.Format(LanguageMgr.GetTranslation(Owner.Client, "AbstractQuest.FinishQuest.Completed", Quest.Name)), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);

            // move quest from active list to finished list...
            lock (Owner.QuestList)
            {
                Owner.QuestList.Remove(this);
                Owner.QuestListFinished.Add(this);
            }
            Owner.SaveIntoDatabase();
            Owner.Out.SendQuestListUpdate();

            var questEvent = GameEventManager.Instance.Events.FirstOrDefault(e =>
            e.QuestStartingId?.Equals(Quest.Id + "-end") == true &&
           !e.StartedTime.HasValue &&
            e.Status == EventStatus.NotOver &&
            e.StartConditionType == StartingConditionType.Quest);
            if (questEvent != null)
            {
                if (questEvent.InstancedConditionType != InstancedConditionTypes.All)
                    questEvent.Owner = Owner;
                System.Threading.Tasks.Task.Run(() => GameEventManager.Instance.StartEvent(questEvent));
            }
            if (Quest.EndStartEvent)
            {
                questEvent = GameEventManager.Instance.Events.FirstOrDefault(e =>
                e.ID?.Equals(Quest.EndEventId) == true &&
                !e.StartedTime.HasValue &&
                e.Status == EventStatus.NotOver &&
                e.StartConditionType == StartingConditionType.Quest);
                if (questEvent != null)
                {
                    if (questEvent.InstancedConditionType != InstancedConditionTypes.All)
                        questEvent.Owner = Owner;
                    System.Threading.Tasks.Task.Run(() => GameEventManager.Instance.StartEvent(questEvent));
                }
            }
            else if (Quest.EndResetEvent)
            {
                questEvent = GameEventManager.Instance.Events.FirstOrDefault(e =>
                e.ID?.Equals(Quest.EndEventId) == true &&
                e.StartedTime.HasValue &&
                e.StartConditionType == StartingConditionType.Quest);
                if (questEvent != null)
                {
                    System.Threading.Tasks.Task.Run(() => GameEventManager.Instance.ResetEvent(questEvent));
                }
            }
            UpdateGroupMob(Owner);
        }
        public void AbortQuest()
        {
            foreach (var goal in Quest.Goals.Values)
                if (goal is TimerGoal)
                    goal.AbortGoal(this);
            DbQuest.Step = (int)eQuestStatus.Done;
            lock (Owner.QuestList)
                Owner.QuestList.Remove(this);
            GameServer.Database.DeleteObject(DbQuest);

            Owner.Out.SendQuestListUpdate();
            foreach (GameNPC mob in WorldMgr.GetRegion(Owner.CurrentRegionID)?.Objects?.Where(o => o != null && o is GameNPC))
            {
                Owner.Out.SendNPCsQuestEffect(mob, mob.GetQuestIndicator(Owner));
            }
            Owner.Out.SendMessage(LanguageMgr.GetTranslation(Owner.Client, "AbstractQuest.AbortQuest", Quest.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            UpdateGroupMob(Owner);
        }
        public void UpdateGroupMob(GamePlayer owner)
        {
            var mobs = owner.GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE);
            foreach (var mob in mobs)
            {
                if (mob is GameNPC groupMob && groupMob.CurrentGroupMob != null)
                {
                    owner.Out.SendNPCCreate(groupMob);
                    owner.Out.SendModelChange(groupMob, groupMob.Model);
                }
            }
        }

        public class QuestRewards : IQuestRewards
        {
            public readonly ushort QuestId;
            public DataQuestJson Quest => DataQuestJsonMgr.GetQuest(QuestId);
            public List<ItemTemplate> BasicItems => Quest.FinalRewardItemTemplates;
            public List<ItemTemplate> OptionalItems => Quest.OptionalRewardItemTemplates;
            public int ChoiceOf => Quest.NbChooseOptionalItems;
            public long Money => Quest.RewardMoney;
            public long Experience => Quest.RewardXP;

            public QuestRewards(DataQuestJson quest)
            {
                QuestId = quest.Id;
            }
        }

        internal class JsonState
        {
            public ushort QuestId;
            public List<PlayerGoalState> Goals;
        }
    }
}
