using DOL.Events;
using DOL.GS.Geometry;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DOL.GS.Quests
{
    /// <summary>
    /// Progresses when the player successfully steals from another player (NOT chests).
    /// Supports modes via TargetName:
    /// - "Player" (default): count successful steals on enemy players (money OR item)
    /// - "gold": count GOLD stolen (1 progress = 1 gold = 10000 copper), accumulative
    /// - "item": count ITEMS stolen (1 progress = 1 inventory item stolen)
    /// Optional constraints:
    /// - TargetRegion: restrict by region
    /// - AreaCenter/AreaRadius/AreaRegion: restrict by circle area
    /// </summary>
    public class StealGoal : DataQuestJsonGoal
    {
        private const long COPPER_PER_GOLD = 10000;
        private readonly int _count = 1;
        private readonly string _targetName;
        private readonly Area.Circle _area;
        private readonly ushort _areaRegion;
        private readonly bool _hasArea;

        private readonly Region _region;
        private readonly ushort _regionId;

        public override eQuestGoalType Type => eQuestGoalType.Steal;
        public override int ProgressTotal => _count;
        public override QuestZonePoint PointA { get; }

        private enum StealMode
        {
            PlayerCount,
            GoldAmount,
            ItemCount
        }

        private readonly StealMode _mode;
        private class StealProgressData
        {
            public long CopperTotal { get; set; } = 0;
            public int ItemsTotal { get; set; } = 0;
            public int PlayersTotal { get; set; } = 0;
        }

        public StealGoal(DataQuestJson quest, int goalId, dynamic db)
            : base(quest, goalId, (object)db)
        {
            _count = db.StealCount != null ? (int)db.StealCount : 1;
            if (_count <= 0) _count = 1;

            _targetName = db.TargetName != null ? (string)db.TargetName : "Player";
            if (string.IsNullOrWhiteSpace(_targetName))
                _targetName = "Player";

            if (string.Equals(_targetName, "gold", StringComparison.OrdinalIgnoreCase))
                _mode = StealMode.GoldAmount;
            else if (string.Equals(_targetName, "item", StringComparison.OrdinalIgnoreCase))
                _mode = StealMode.ItemCount;
            else
                _mode = StealMode.PlayerCount;

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
                _areaRegion = (ushort)db.AreaRegion;

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
            dict.Add("TargetName", _targetName);
            dict.Add("TargetRegion", _regionId);
            dict.Add("StealCount", _count);
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

        private static StealProgressData GetOrCreateProgressData(PlayerGoalState goalState)
        {
            if (goalState.CustomData == null)
            {
                var fresh = new StealProgressData();
                goalState.CustomData = fresh;
                return fresh;
            }

            if (goalState.CustomData is JObject jo)
            {
                var parsed = jo.ToObject<StealProgressData>() ?? new StealProgressData();
                goalState.CustomData = parsed;
                return parsed;
            }

            if (goalState.CustomData is StealProgressData spd)
                return spd;

            var fallback = new StealProgressData();
            goalState.CustomData = fallback;
            return fallback;
        }

        internal void OnSuccessfulSteal(PlayerQuest questData, PlayerGoalState goalState, GamePlayer stealer, GamePlayer target, long stolenCopper, int stolenItems)
        {
            if (goalState == null || !goalState.IsActive)
                return;

            if (stealer == null || target == null)
                return;

            if (target is not GamePlayer)
                return;

            if (!IsValidLocation(stealer))
                return;

            // Do not count steal on grey con
            if (stealer.IsPlayerGreyCon(target))
                return;

            var data = GetOrCreateProgressData(goalState);

            switch (_mode)
            {
                case StealMode.GoldAmount:
                    {
                        if (stolenCopper <= 0)
                            return;

                        data.CopperTotal += stolenCopper;

                        int goldUnits = (int)(data.CopperTotal / COPPER_PER_GOLD);
                        int newProgress = Math.Min(goldUnits, ProgressTotal);

                        while (goalState.Progress < newProgress && !goalState.IsFinished)
                        {
                            AdvanceGoal(questData, goalState);
                        }
                        questData.SaveIntoDatabase();
                        break;
                    }

                case StealMode.ItemCount:
                    {
                        if (stolenItems <= 0)
                            return;

                        data.ItemsTotal += stolenItems;
                        int newProgress = Math.Min(data.ItemsTotal, ProgressTotal);

                        while (goalState.Progress < newProgress && !goalState.IsFinished)
                        {
                            AdvanceGoal(questData, goalState);
                        }
                        questData.SaveIntoDatabase();
                        break;
                    }

                default:
                    {
                        data.PlayersTotal += 1;
                        AdvanceGoal(questData, goalState);
                        questData.SaveIntoDatabase();
                        break;
                    }
            }
        }

        public override void Unload()
        {
            if (_area != null)
                WorldMgr.GetRegion(_areaRegion)?.RemoveArea(_area);
            base.Unload();
        }

        /// <summary>
        /// Helper called from StealCommandHandlerBase when a steal really succeeds (vs player).
        /// Pass details so goal can count gold or items correctly.
        /// </summary>
        public static void OnPlayerSuccessfulSteal(GamePlayer stealer, GamePlayer target, long stolenCopper, int stolenItems)
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
                    stealGoal.OnSuccessfulSteal(pq, state, stealer, target, stolenCopper, stolenItems);
                }
            }
        }
    }
}
