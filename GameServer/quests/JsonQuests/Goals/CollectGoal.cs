using DOL.Database;
using DOL.Events;
using DOL.GS.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DOL.GS.Quests
{
    public class CollectGoal : DataQuestJsonGoal
    {
        private readonly string m_text;
        private readonly ItemTemplate m_item;
        private readonly int m_itemCount = 1;

        public override eQuestGoalType Type => eQuestGoalType.Unknown;
        public override int ProgressTotal => m_itemCount;
        public override QuestZonePoint PointA => new(Target);
        public override ItemTemplate QuestItem => m_item;

        public CollectGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            Target = WorldMgr.GetNPCsByNameFromRegion((string)db.TargetName ?? "", (ushort)db.TargetRegion, eRealm.None).FirstOrDefault();
            Target ??= quest.Npc;
            hasInteraction = true;
            m_text = db.Text;
            m_item = GameServer.Database.FindObjectByKey<ItemTemplate>((string)db.Item);
            m_itemCount = db.ItemCount;
            GameEventMgr.AddHandler(Target, GameObjectEvent.ReceiveItem, _Notify);
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("TargetName", Target.Name);
            dict.Add("TargetRegion", Target.CurrentRegionID);
            dict.Add("Text", m_text);
            dict.Add("Item", m_item.Id_nb);
            dict.Add("ItemCount", m_itemCount);
            return dict;
        }

        public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
            => state?.IsActive == true && target.Name == Target.Name && target.CurrentRegion == Target.CurrentRegion;

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
        }

        private void _Notify(DOLEvent e, object sender, EventArgs args)
        {
            if (e != GameObjectEvent.ReceiveItem || !(args is ReceiveItemEventArgs interact))
                return;
            
            if (!(interact.Source is GamePlayer player) || interact.Target != Target)
                return;

            if (interact.Item.Id_nb != m_item.Id_nb)
                return;

            var (quest, goal) = DataQuestJsonMgr.FindQuestAndGoalFromPlayer(player, Quest.Id, GoalId);
            if (quest == null || goal is not { IsActive: true })
                return;

            var questGoal = quest.Goals[goal.GoalId];
            if (questGoal is DataQuestJsonGoal jsonGoal && jsonGoal.Conditions?.Validate(quest, jsonGoal) == false)
                return;

            var itemsCountToRemove = m_itemCount - goal.Progress;
            if (interact.Item.Count < itemsCountToRemove)
            {
                itemsCountToRemove = interact.Item.Count;
            }
            if (!player.Inventory.RemoveCountFromStack(interact.Item, itemsCountToRemove))
            {
                ChatUtil.SendImportant(player, "An error happened, retry in a few seconds");
                return;
            }

            goal.Progress += itemsCountToRemove - 1;
            if (!string.IsNullOrWhiteSpace(m_text))
            {
                Task.Run(async () =>
                {
                    string msg = await TranslateGoalText(player, m_text);
                    ChatUtil.SendPopup(player, msg);
                });
            }
            AdvanceGoal(quest, goal, true);
        }

        public override void Unload()
        {
            if (Target != null)
                GameEventMgr.RemoveHandler(Target, GameObjectEvent.ReceiveItem, _Notify);
            base.Unload();
        }
    }
}
