using DOL.Events;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DOL.GS.Quests
{
    public class KillGoal : DataQuestJsonGoal
    {
        private readonly int m_killCount = 1;
        private readonly string m_targetName;
        private readonly ushort m_targetRegion;
        private readonly NpcTemplateMgr.eBodyType? m_bodyType = null;

        // Con settings: -3 (Grey) to +3 (Purple)
        private readonly int? m_minCon = null;
        private readonly int? m_maxCon = null;

        private readonly Area.Circle m_area;
        private readonly ushort m_areaRegion;
        private readonly bool hasArea = false;

        private readonly GameNPC m_referenceTarget;

        public override eQuestGoalType Type => eQuestGoalType.Kill;
        public override int ProgressTotal => m_killCount;
        public override QuestZonePoint PointA { get; }

        public KillGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
        {
            m_killCount = (int?)db.KillCount ?? 1;
            m_targetName = (string)db.TargetName;
            m_targetRegion = (ushort?)db.TargetRegion ?? 0;

            if (db.BodyType != null)
            {
                string btStr = (string)db.BodyType;
                if (!string.IsNullOrWhiteSpace(btStr) && Enum.TryParse(btStr, true, out NpcTemplateMgr.eBodyType parsedBT))
                {
                    m_bodyType = parsedBT;
                }
            }

            if (db.MinCon != null) m_minCon = (int)db.MinCon;
            if (db.MaxCon != null) m_maxCon = (int)db.MaxCon;

            if (db.AreaRadius != null && db.AreaRadius != "" && db.AreaRegion != null && db.AreaRegion != "" && db.AreaCenter != null)
            {
                hasArea = true;
                m_area = new Area.Circle($"{quest.Name} KillGoal {goalId}", Coordinate.Create((int)((float)db.AreaCenter.X), (int)((float)db.AreaCenter.Y), (int)((float)db.AreaCenter.Z)), (int)db.AreaRadius);
                m_area.DisplayMessage = false;
                m_areaRegion = db.AreaRegion;

                var reg = WorldMgr.GetRegion(m_areaRegion);
                reg.AddArea(m_area);
                PointA = new QuestZonePoint(reg.GetZone(m_area.Coordinate), m_area.Coordinate);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(m_targetName))
                {
                    m_referenceTarget = WorldMgr.GetNPCsByNameFromRegion(m_targetName, m_targetRegion, eRealm.None).FirstOrDefault();
                    if (m_referenceTarget != null)
                    {
                        PointA = new QuestZonePoint(m_referenceTarget);
                        Target = m_referenceTarget;
                    }
                }
            }
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("TargetName", m_targetName);
            dict.Add("TargetRegion", m_targetRegion);
            dict.Add("KillCount", m_killCount);

            if (m_bodyType.HasValue) dict.Add("BodyType", m_bodyType.Value.ToString());
            if (m_minCon.HasValue) dict.Add("MinCon", m_minCon.Value);
            if (m_maxCon.HasValue) dict.Add("MaxCon", m_maxCon.Value);

            if (hasArea)
            {
                dict.Add("AreaCenter", m_area.Coordinate);
                dict.Add("AreaRadius", m_area.Radius);
                dict.Add("AreaRegion", m_areaRegion);
            }
            return dict;
        }

        /// <summary>
        /// Logic for showing the quest indicator over the mob and validating kills.
        /// </summary>
        public bool IsMobMatch(GamePlayer player, GameNPC mob)
        {
            if (mob == null) return false;

            // 1. Area Check
            if (hasArea)
            {
                if (mob.CurrentRegionID != m_areaRegion) return false;
                if (!m_area.IsContaining(mob.Coordinate, false)) return false;
            }
            else if (m_referenceTarget != null)
            {
                if (mob.CurrentRegionID != m_referenceTarget.CurrentRegionID) return false;
            }

            // 2. Con (Level Difference) Check
            if (m_minCon.HasValue || m_maxCon.HasValue)
            {
                // GetConLevel usually returns: -3 (Grey), -2 (Green), -1 (Blue), 0 (Yellow), 1 (Orange), 2 (Red), 3 (Purple)
                int conLevel = (int)player.GetConLevel(mob);

                if (m_minCon.HasValue && conLevel < m_minCon.Value) return false;
                if (m_maxCon.HasValue && conLevel > m_maxCon.Value) return false;
            }

            // 3. BodyType Check
            if (m_bodyType.HasValue)
            {
                if ((NpcTemplateMgr.eBodyType)mob.BodyType != m_bodyType.Value)
                    return false;
            }

            // 4. Name Check
            // If names are defined, the mob MUST match it.
            // If NO names are defined (but BodyType was), any name is accepted.
            if (!string.IsNullOrWhiteSpace(m_targetName))
            {
                if (mob.Name != m_targetName)
                    return false;
            }

            return true;
        }

        public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
        {
            if (target is not GameNPC npc) return false;
            return state?.IsActive == true && IsMobMatch(questData.Owner, npc);
        }

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
            if (e == GameLivingEvent.EnemyKilled && args is EnemyKilledEventArgs killedArgs)
            {
                var killedMob = killedArgs.Target as GameNPC;
                if (killedMob == null) return;

                if (IsMobMatch(quest.Owner, killedMob))
                {
                    AdvanceGoal(quest, goal);
                }
            }
        }

        public override void Unload()
        {
            if (hasArea)
            {
                WorldMgr.GetRegion(m_areaRegion)?.RemoveArea(m_area);
            }
            base.Unload();
        }
    }
}