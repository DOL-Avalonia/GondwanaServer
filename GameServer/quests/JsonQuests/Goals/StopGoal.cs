using DOL.Events;
using System;
using System.Collections.Generic;

namespace DOL.GS.Quests
{
    public class StopGoal : DataQuestJsonGoal
    {
        private readonly List<int> m_stopGoals = new();

        public override eQuestGoalType Type => eQuestGoalType.Unknown;
        public override int ProgressTotal => 1;
        public override bool Visible => false;

        public StopGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            foreach (int id in db.StopGoals)
                m_stopGoals.Add(id);
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("StopGoals", m_stopGoals);
            return dict;
        }

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
        }

        public override PlayerGoalState ForceStartGoal(PlayerQuest questData)
        {
            var state = base.ForceStartGoal(questData);

            new ECSGameTimer(questData.Owner, _ =>
            {
                foreach (int stopId in m_stopGoals)
                    if (Quest.Goals.TryGetValue(stopId, out var stopGoal))
                        stopGoal.AbortGoal(questData);

                EndGoal(questData, state, true);
                return 0;
            }, 1);

            return state;
        }
    }
}
