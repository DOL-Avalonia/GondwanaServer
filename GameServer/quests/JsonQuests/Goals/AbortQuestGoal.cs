using DOL.Events;
using DOL.GS.Behaviour;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace DOL.GS.Quests
{
    public class AbortQuestGoal : DataQuestJsonGoal
    {
        private string m_text;

        public override eQuestGoalType Type => eQuestGoalType.Unknown;
        public override int ProgressTotal => 1;

        public AbortQuestGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            m_text = db.Text;
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("Text", m_text);
            return dict;
        }

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
        }

        public override PlayerGoalState ForceStartGoal(PlayerQuest questData)
        {
            var state = base.ForceStartGoal(questData);
            string text = m_text;

            new ECSGameTimer(questData.Owner, _ =>
            {
                questData.AbortQuest();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    Task.Run(async () =>
                    {
                        string msg = await TranslateGoalText(questData.Owner, text);
                        ChatUtil.SendPopup(questData.Owner, msg);
                    });
                }

                return 0;
            }, 1);

            return state;
        }
    }
}
