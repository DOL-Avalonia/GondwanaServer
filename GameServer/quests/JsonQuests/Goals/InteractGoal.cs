using DOL.Events;
using DOL.GS.Behaviour;
using DOL.GS.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.Quests
{
    public class InteractGoal : DataQuestJsonGoal
    {
        private readonly string m_text;

        public override eQuestGoalType Type => eQuestGoalType.Unknown;
        public override int ProgressTotal => 1;
        public override QuestZonePoint PointA => new(Target);

        public InteractGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            Target = WorldMgr.GetNPCsByNameFromRegion((string)db.TargetName ?? "", (ushort)db.TargetRegion, eRealm.None)
                .FirstOrDefault(quest.Npc);
            m_text = db.Text;
            hasInteraction = true;
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("TargetName", Target.Name);
            dict.Add("TargetRegion", Target.CurrentRegionID);
            dict.Add("Text", m_text);
            return dict;
        }

        public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
            => state?.IsActive == true && target.Name == Target.Name && target.CurrentRegion == Target.CurrentRegion;

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
            var player = quest.Owner;

            if (args is InteractWithEventArgs interact)
            {
                if (interact.Target is ITextNPC textNPC && textNPC.CheckQuestAvailable(player, Quest.Name, GoalId))
                    return;

                if (e == GameObjectEvent.InteractWith && interact.Target.Name == Target.Name && interact.Target.CurrentRegion == Target.CurrentRegion)
                {
                    if (AdvanceGoal(quest, goal))
                    {
                        if (!string.IsNullOrWhiteSpace(m_text))
                        {
                            string msg = TranslateGoalText(player, m_text);
                            ChatUtil.SendPopup(player, msg);
                        }
                    }
                }
            }
        }
    }
}
