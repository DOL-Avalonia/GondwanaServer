using DOL.Events;
using DOL.GS.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DOL.GS.Quests
{
    public class KillPlayerGoal : DataQuestJsonGoal
    {
        private readonly int m_killCount = 1;
        private readonly Area.Circle m_area;
        private readonly ushort m_areaRegion;
        private readonly bool hasArea = false;

        public override eQuestGoalType Type => eQuestGoalType.Kill;
        public override int ProgressTotal => m_killCount;
        public override QuestZonePoint PointA { get; }
        Region m_region;
        ushort m_regionId;

        public KillPlayerGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            m_killCount = db.KillCount;
            m_regionId = (ushort)db.TargetRegion;
            if (db.TargetRegion != null && db.TargetRegion != "")
                m_region = WorldMgr.GetRegion(m_regionId);
            if (db.AreaRadius != null && db.AreaRadius != "" && db.AreaRegion != null && db.AreaRegion != "" && db.AreaCenter != null)
            {
                hasArea = true;
                m_area = new Area.Circle($"{quest.Name} KillPlayerGoal {goalId}", Coordinate.Create((int)((float)db.AreaCenter.X), (int)((float)db.AreaCenter.Y), (int)((float)db.AreaCenter.Z)), (int)db.AreaRadius);
                m_area.DisplayMessage = false;
                m_areaRegion = db.AreaRegion;

                var reg = WorldMgr.GetRegion(m_areaRegion);
                reg.AddArea(m_area);
                PointA = new QuestZonePoint(reg.GetZone(m_area.Coordinate), m_area.Coordinate);
            }
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("TargetRegion", m_regionId);
            dict.Add("KillCount", m_killCount);
            dict.Add("AreaCenter", m_area.Coordinate);
            dict.Add("AreaRadius", m_area.Radius);
            dict.Add("AreaRegion", m_areaRegion);
            return dict;
        }

        public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
            => state?.IsActive == true && target is GamePlayer && (m_region == null || target.CurrentRegion == m_region);

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
            if (e != GameLivingEvent.EnemyKilled || args is not EnemyKilledEventArgs killedArgs)
                return;

            var killed = killedArgs.Target;

            var killer = quest.Owner as GamePlayer;
            if (killer == null)
                return;

            if (killed == null
                || !(killed is GamePlayer killedPlayer)
                || (m_region != null && m_region != killed.CurrentRegion)
                || (hasArea && !m_area.IsContaining(killed.Coordinate, false)))
                return;

            if (killer.IsPlayerGreyCon(killedPlayer))
                return;

            AdvanceGoal(quest, goal);
        }

        public override void Unload()
        {
            WorldMgr.GetRegion(m_areaRegion)?.RemoveArea(m_area);
            base.Unload();
        }
    }
}
