using DOL.Events;
using DOL.GS.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DOL.GS.Quests
{
    /// <summary>
    /// Progresses when the player successfully steals from another player (NOT chests).
    /// Only counts steals:
    /// - against non-grey targets
    /// - that actually succeeded (money or item)
    /// - optionally in a specific region / area
    /// </summary>
    public class StealGoal : DataQuestJsonGoal
    {
        private readonly int _stealCount = 1;
        private readonly Area.Circle _area;
        private readonly ushort _areaRegion;
        private readonly bool _hasArea;

        private readonly Region _region;
        private readonly ushort _regionId;

        public override eQuestGoalType Type => eQuestGoalType.Steal;
        public override int ProgressTotal => _stealCount;
        public override QuestZonePoint PointA { get; }

        public StealGoal(DataQuestJson quest, int goalId, dynamic db)
            : base(quest, goalId, (object)db)
        {
            _stealCount = db.StealCount;

            _regionId = db.TargetRegion != null ? (ushort)db.TargetRegion : (ushort)0;
            if (_regionId != 0)
                _region = WorldMgr.GetRegion(_regionId);

            if (db.AreaRadius != null && db.AreaRadius != "" &&
                db.AreaRegion != null && db.AreaRegion != "" &&
                db.AreaCenter != null)
            {
                _hasArea = true;
                _area = new Area.Circle(
                    $"{quest.Name} StealGoal {goalId}",
                    Coordinate.Create(
                        (int)((float)db.AreaCenter.X),
                        (int)((float)db.AreaCenter.Y),
                        (int)((float)db.AreaCenter.Z)),
                    (int)db.AreaRadius);

                _area.DisplayMessage = false;
                _areaRegion = db.AreaRegion;

                var reg = WorldMgr.GetRegion(_areaRegion);
                reg.AddArea(_area);
                PointA = new QuestZonePoint(reg.GetZone(_area.Coordinate), _area.Coordinate);
            }
            else
            {
                PointA = QuestZonePoint.None;
            }
        }

        public override Dictionary<string, object> GetDatabaseJsonObject()
        {
            var dict = base.GetDatabaseJsonObject();
            dict.Add("TargetRegion", _regionId);
            dict.Add("StealCount", _stealCount);
            dict.Add("AreaCenter", _area != null ? _area.Coordinate : null);
            dict.Add("AreaRadius", _area != null ? _area.Radius : 0);
            dict.Add("AreaRegion", _areaRegion);
            return dict;
        }

        public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
        {
            return false;
        }

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
        }

        private bool IsValidLocation(GamePlayer stealer)
        {
            if (_region != null && stealer.CurrentRegion != _region)
                return false;

            if (_hasArea && !_area.IsContaining(stealer.Coordinate, false))
                return false;

            return true;
        }

        internal void OnSuccessfulSteal(PlayerQuest questData, PlayerGoalState goalState, GamePlayer stealer, GamePlayer target)
        {
            if (goalState == null || !goalState.IsActive)
                return;

            if (stealer == null || target == null)
                return;

            if (!IsValidLocation(stealer))
                return;

            // Do not count steal on grey con
            if (stealer.IsPlayerGreyCon(target))
                return;

            AdvanceGoal(questData, goalState);
        }

        public override void Unload()
        {
            if (_area != null)
                WorldMgr.GetRegion(_areaRegion)?.RemoveArea(_area);
            base.Unload();
        }

        /// <summary>
        /// Helper called from StealCommandHandlerBase when a steal really succeeds.
        /// </summary>
        public static void OnPlayerSuccessfulSteal(GamePlayer stealer, GamePlayer target)
        {
            if (stealer == null || target == null)
                return;

            foreach (var pq in stealer.QuestList.OfType<PlayerQuest>())
            {
                foreach (var goal in pq.Quest.Goals.Values)
                {
                    if (goal is not StealGoal stealGoal)
                        continue;

                    var state = pq.GoalStates.Find(gs => gs.GoalId == goal.GoalId);
                    stealGoal.OnSuccessfulSteal(pq, state, stealer, target);
                }
            }
        }
    }
}
