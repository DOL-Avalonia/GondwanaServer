using DOL.Events;
using System;
using System.Collections.Generic;
using DOL.GS.Utils;

namespace DOL.GS.Quests
{
    public class TimerGoal : DataQuestJsonGoal
    {
        public override eQuestGoalType Type => eQuestGoalType.Unknown;
        public override int ProgressTotal => 1;
        public override bool Visible => false;

        private readonly int m_seconds;

        public TimerGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            m_seconds = db.Seconds;
            m_seconds = m_seconds.Clamp(1, int.MaxValue); // minimum 1 sec
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("Seconds", m_seconds);
            return dict;
        }

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
            if (e == GamePlayerEvent.GameEntered && sender == quest.Owner)
                StartTimer(quest, goal);
        }

        public override PlayerGoalState ForceStartGoal(PlayerQuest questData)
        {
            var state = base.ForceStartGoal(questData);
            StartTimer(questData, state);
            return state;
        }
        public override void AbortGoal(PlayerQuest questData)
        {
            var tempKey = $"QUEST_TIMER_{Quest.Id}-{GoalId}";
            var timer = questData.Owner.TempProperties.getProperty<ECSGameTimer>(tempKey);
            if (timer != null)
            {
                timer.Stop();
                questData.Owner.TempProperties.removeProperty(tempKey);
            }
            if (m_seconds <= 600)
                questData.Owner.Out.SendCloseTimerWindow();
            base.AbortGoal(questData);
        }

        private void StartTimer(PlayerQuest questData, PlayerGoalState goalData)
        {
            var tempKey = $"QUEST_TIMER_{Quest.Id}-{GoalId}";
            if (questData.Owner.TempProperties.getProperty<ECSGameTimer>(tempKey) != null)
                return;
            if (m_seconds <= 600)
                questData.Owner.Out.SendTimerWindow(Description, m_seconds);
            var timer = new ECSGameTimer(questData.Owner, _timer =>
            {
                    AdvanceGoal(questData, goalData);
                    if (m_seconds <= 600)
                        questData.Owner.Out.SendCloseTimerWindow();
                    questData.Owner.TempProperties.removeProperty(tempKey);
                    return 0;
                });
            timer.Start(m_seconds * 1000);
            questData.Owner.TempProperties.setProperty(tempKey, timer);
        }
    }
}
