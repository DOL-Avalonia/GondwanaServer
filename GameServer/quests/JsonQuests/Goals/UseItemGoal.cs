using DOL.Database;
using DOL.Events;
using DOL.GS.Behaviour;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace DOL.GS.Quests
{
    public class UseItemGoal : DataQuestJsonGoal
    {
        private readonly string m_text;
        private readonly ItemTemplate m_item;

        public override eQuestGoalType Type => eQuestGoalType.Unknown;
        public override int ProgressTotal => 1;
        public override ItemTemplate QuestItem => DummyItemTemplate ?? m_item;
        private readonly Area.Circle m_area;
        private readonly ushort m_areaRegion;
        private readonly bool hasArea = false;
        private readonly bool destroyItem = false;
        private QuestIndicatorNPC m_areaIndicator;
        public override QuestZonePoint PointA { get; }

        public UseItemGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            m_text = db.Text;
            m_item = GameServer.Database.FindObjectByKey<ItemTemplate>((string)db.Item);

            if (db.AreaRadius != null && db.AreaRadius != "" && db.AreaRegion != null && db.AreaRegion != "" && db.AreaCenter != null)
            {
                hasArea = true;
                m_area = new Area.Circle($"{quest.Name} EnterAreaGoal {goalId}", Coordinate.Create((int)((float)db.AreaCenter.X), (int)((float)db.AreaCenter.Y), (int)((float)db.AreaCenter.Z)), (int)db.AreaRadius);
                m_area.DisplayMessage = false;
                m_areaRegion = db.AreaRegion;

                var reg = WorldMgr.GetRegion(m_areaRegion);
                reg.AddArea(m_area);
                PointA = new QuestZonePoint(reg.GetZone(m_area.Coordinate), m_area.Coordinate);

                m_areaIndicator = new QuestIndicatorNPC(null);
                m_areaIndicator.StaticIndicator = eQuestIndicator.RedTarget;
                m_areaIndicator.IsVisibleCondition = (player) =>
                {
                    var pq = player.QuestList.OfType<PlayerQuest>().FirstOrDefault(q => q.QuestId == QuestId);
                    return pq != null && IsActive(pq) && !IsDone(pq);
                };
                m_areaIndicator.CurrentRegionID = m_areaRegion;
                m_areaIndicator.Position = Position.Create(m_areaRegion, m_area.Coordinate.X, m_area.Coordinate.Y, m_area.Coordinate.Z, 0) + DOL.GS.Geometry.Vector.Create(z: 1);
                m_areaIndicator.AddToWorld();
            }
            if (db.TargetName != null && db.TargetName != "" && db.TargetRegion != null && db.TargetRegion != "")
            {
                hasInteraction = true;
                Target = WorldMgr.GetNPCsByNameFromRegion((string)db.TargetName ?? "", (ushort)db.TargetRegion, eRealm.None)
                    .FirstOrDefault(quest.Npc);
            }
            if (db.DestroyItem != null && db.DestroyItem != "")
            {
                destroyItem = db.DestroyItem;
            }
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("Item", m_item.Id_nb);
            dict.Add("TargetName", Target?.Name ?? "");
            dict.Add("DestroyItem", destroyItem);
            dict.Add("TargetRegion", Target?.CurrentRegionID ?? 0);
            if (hasArea)
            {
                dict.Add("AreaCenter", m_area.Coordinate);
                dict.Add("AreaRadius", m_area.Radius);
                dict.Add("AreaRegion", m_areaRegion);
            }
            dict.Add("Text", m_text);
            return dict;
        }

        public override void RefreshCustomIndicators(PlayerQuest questData)
        {
            if (m_areaIndicator != null && questData != null)
            {
                questData.Owner.Out.SendModelChange(m_areaIndicator, m_areaIndicator.GetModelForPlayer(questData.Owner));
            }
        }

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
            var player = quest.Owner;
            if ((!hasArea || (hasArea && m_area.IsContaining(player.Coordinate, false))) && e == GamePlayerEvent.UseSlot && args is UseSlotEventArgs useSlot)
            {
                var usedItem = player.Inventory.GetItem((eInventorySlot)useSlot.Slot);
                if (usedItem != null && usedItem.Id_nb == QuestItem.Id_nb && (Target == null || player.TargetObject == Target))
                {
                    if (AdvanceGoal(quest, goal))
                    {
                        RefreshCustomIndicators(quest);

                        if (destroyItem)
                        {
                            player.Inventory.RemoveCountFromStack(usedItem, 1);
                        }
                        Task.Run(async () =>
                        {
                            string msg = await TranslateGoalText(player, m_text);
                            player.Client.Out.SendDialogBox(eDialogCode.CustomDialog, 0, 0, 0, 0, eDialogType.Ok, true, msg);
                        });
                    }
                }
            }

            if (hasArea && (sender as AbstractArea)?.ID == m_area.ID && args is AreaEventArgs arguments && arguments.GameObject == quest.Owner)
            {
                if (e == AreaEvent.PlayerEnter || e == AreaEvent.PlayerLeave)
                {
                    RefreshCustomIndicators(quest);
                }
            }
        }

        public override void Unload()
        {
            if (hasArea)
                WorldMgr.GetRegion(m_areaRegion)?.RemoveArea(m_area);

            if (m_areaIndicator != null)
                m_areaIndicator.RemoveFromWorld();

            base.Unload();
        }
    }
}
