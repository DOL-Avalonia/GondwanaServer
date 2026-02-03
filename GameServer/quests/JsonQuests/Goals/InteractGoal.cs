using DOL.Events;
using DOL.GS.Behaviour;
using DOL.GS.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DOL.GS.Quests
{
    public class InteractGoal : DataQuestJsonGoal
    {
        private readonly string m_text;
        private readonly string m_responseText;

        public override eQuestGoalType Type => eQuestGoalType.Unknown;
        public override int ProgressTotal => 1;
        public override QuestZonePoint PointA => new(Target);

        public InteractGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            Target = WorldMgr.GetNPCsByNameFromRegion((string)db.TargetName ?? "", (ushort)db.TargetRegion, eRealm.None)
                .FirstOrDefault(quest.Npc);
            m_text = db.Text;
            m_responseText = db.ResponseText;
            hasInteraction = true;
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("TargetName", Target.Name);
            dict.Add("TargetRegion", Target.CurrentRegionID);
            dict.Add("Text", m_text);
            dict.Add("ResponseText", m_responseText);
            return dict;
        }

        public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
            => state?.IsActive == true && target.Name == Target.Name && target.CurrentRegion == Target.CurrentRegion;

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
            var player = quest.Owner;
            bool requiresResponse = !string.IsNullOrWhiteSpace(m_responseText);

            // CASE 1: Standard Interaction (Clicking the NPC)
            if (e == GameObjectEvent.InteractWith && args is InteractWithEventArgs interact)
            {
                if (requiresResponse)
                    return;

                if (interact.Target is ITextNPC textNPC && textNPC.CheckQuestAvailable(player, Quest.Name, GoalId))
                    return;

                if (interact.Target.Name == Target.Name && interact.Target.CurrentRegion == Target.CurrentRegion)
                {
                    CompleteInteractGoal(quest, goal, player);
                }
            }
            // CASE 2: Text Response (Whisper)
            else if (e == GameLivingEvent.Whisper && args is WhisperEventArgs whisper)
            {
                if (!requiresResponse)
                    return;

                if (whisper.Target is ITextNPC textNPC && textNPC.CheckQuestAvailable(player, Quest.Name, GoalId))
                    return;

                if (whisper.Target.Name == Target.Name && whisper.Target.CurrentRegion == Target.CurrentRegion && string.Equals(whisper.Text, m_responseText, StringComparison.OrdinalIgnoreCase))
                {
                    CompleteInteractGoal(quest, goal, player);
                }
            }
        }

        private void CompleteInteractGoal(PlayerQuest quest, PlayerGoalState goal, GamePlayer player)
        {
            if (AdvanceGoal(quest, goal))
            {
                if (!string.IsNullOrWhiteSpace(m_text))
                {
                    Task.Run(async () =>
                    {
                        string msg = await TranslateGoalText(player, m_text);
                        ChatUtil.SendPopup(player, msg);
                    });
                }
            }
        }
    }
}
