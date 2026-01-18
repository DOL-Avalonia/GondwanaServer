using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.Events;
using DOL.GS.Behaviour;
using System.Threading.Tasks;

namespace DOL.GS.Quests
{
    /// <summary>
    /// Variant of CollectGoal where progress is updated automatically
    /// whenever the player gains/loses a specific item in his inventory.
    /// - Gain item -> progress++ (up to ItemCount)
    /// - Lose item (drop/destroy/etc.) -> progress-- (down to 0)
    /// </summary>
    public class CollectGetGoal : DataQuestJsonGoal
    {
        private readonly string m_text;
        private readonly ItemTemplate m_item;
        private readonly int m_itemCount = 1;

        public override eQuestGoalType Type => eQuestGoalType.Unknown;
        public override int ProgressTotal => m_itemCount;

        public override QuestZonePoint PointA =>
            Quest.Npc != null ? new QuestZonePoint(Quest.Npc) : QuestZonePoint.None;

        public override ItemTemplate QuestItem => m_item;

        public CollectGetGoal(DataQuestJson quest, int goalId, dynamic db)
            : base(quest, goalId, (object)db)
        {
            m_text = db.Text;

            string itemId = db.Item;
            if (string.IsNullOrWhiteSpace(itemId))
                throw new Exception($"[DataQuestJson] Quest {quest.Id}: CollectGetGoal {goalId} has no Item defined in JSON.");

            m_item = GameServer.Database.FindObjectByKey<ItemTemplate>(itemId);
            if (m_item == null)
                throw new Exception($"[DataQuestJson] Quest {quest.Id}: CollectGetGoal {goalId} can't find item template '{itemId}'.");

            m_itemCount = db.ItemCount;
            if (m_itemCount <= 0)
                m_itemCount = 1;
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("Text", m_text);
            dict.Add("Item", m_item.Id_nb);
            dict.Add("ItemCount", m_itemCount);
            return dict;
        }

        public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
        {
            return false;
        }

        /// <summary>
        /// Called for every event while this goal is active.
        /// We only care about PlayerInventoryEvent.ItemCountChanged
        /// for the quest owner and the tracked item ID.
        /// </summary>
        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
            if (goal == null || !goal.IsActive || goal.IsFinished)
                return;

            if (e != PlayerInventoryEvent.ItemCountChanged || args is not ItemCountChangedEventArgs invArgs)
                return;

            if (quest.Owner == null || !ReferenceEquals(quest.Owner, invArgs.Player))
                return;

            var item = invArgs.Item;
            if (item == null || item.Id_nb != m_item.Id_nb)
                return;

            int delta = invArgs.Delta;
            if (delta == 0)
                return;

            var player = quest.Owner;

            if (player != null)
            {
                player.HasCollectGetGoalInProgress = true;
            }

            if (delta > 0)
            {
                if (!string.IsNullOrWhiteSpace(m_text))
                {
                    Task.Run(async () =>
                    {
                        string msg = await TranslateGoalText(player, m_text);
                        ChatUtil.SendPopup(player, msg);
                    });
                }

                for (int i = 0; i < delta && !goal.IsFinished && goal.Progress < ProgressTotal; i++)
                {
                    AdvanceGoal(quest, goal);
                }

                if (player != null && (goal.IsFinished || goal.Progress >= ProgressTotal))
                {
                    player.HasCollectGetGoalInProgress = false;
                }

                return;
            }

            // LOSE ITEMS (delta < 0)
            delta = -delta; // make it positive for loops
            int removable = Math.Min(delta, goal.Progress);
            for (int i = 0; i < removable; i++)
            {
                DecreaseGoal(quest, goal);
            }

            if (player != null && (goal.IsFinished || goal.Progress >= ProgressTotal))
            {
                player.HasCollectGetGoalInProgress = false;
            }
        }
    }
}
