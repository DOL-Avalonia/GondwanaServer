using DOL.Events;
using DOL.GS.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DOL.GS.Quests
{
    /// <summary>
    /// Progresses when the player lands a critical hit (melee or spell, NOT DoTs)
    /// on a specific target type:
    /// - TargetName == "Player"  => any enemy player (non-grey)
    /// - TargetName == some NPC name => that NPC in a given region
    /// Optional region / area restrictions like KillGoal.
    /// </summary>
    public class CriticalHitGoal : DataQuestJsonGoal
    {
        private readonly int _hitCount = 1;
        private readonly string _targetName;
        private readonly GameNPC _targetNpc;

        private readonly Region _region;
        private readonly ushort _regionId;

        private readonly Area.Circle _area;
        private readonly ushort _areaRegion;
        private readonly bool _hasArea;

        public override eQuestGoalType Type => eQuestGoalType.CriticalHit;
        public override int ProgressTotal => _hitCount;
        public override QuestZonePoint PointA { get; }

        public CriticalHitGoal(DataQuestJson quest, int goalId, dynamic db)
            : base(quest, goalId, (object)db)
        {
            _targetName = db.TargetName;
            _hitCount = db.HitCount;

            _regionId = db.TargetRegion != null ? (ushort)db.TargetRegion : (ushort)0;
            if (_regionId != 0)
                _region = WorldMgr.GetRegion(_regionId);

            // If target is an NPC, resolve it now
            if (!string.Equals(_targetName, "Player", StringComparison.OrdinalIgnoreCase))
            {
                if (_regionId == 0)
                    throw new Exception($"[DataQuestJson] Quest {quest.Id}: CriticalHitGoal {goalId} needs a TargetRegion when TargetName is an NPC.");

                _targetNpc = WorldMgr.GetNPCsByNameFromRegion(_targetName, _regionId, eRealm.None).FirstOrDefault();
                if (_targetNpc == null)
                    throw new Exception($"[DataQuestJson] Quest {quest.Id}: can't load CriticalHitGoal {goalId}, target npc (name: {_targetName}, reg: {_regionId}) is not found");
            }

            if (db.AreaRadius != null && db.AreaRadius != "" &&
                db.AreaRegion != null && db.AreaRegion != "" &&
                db.AreaCenter != null)
            {
                _hasArea = true;
                _area = new Area.Circle(
                    $"{quest.Name} CriticalHitGoal {goalId}",
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
            else if (_targetNpc != null)
            {
                PointA = new QuestZonePoint(_targetNpc);
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
            dict.Add("HitCount", _hitCount);
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

        private bool IsValidLocation(GamePlayer attacker, GameLiving target)
        {
            if (_region != null && target.CurrentRegion != _region)
                return false;

            if (_hasArea && !_area.IsContaining(target.Coordinate, false))
                return false;

            return true;
        }

        private bool IsValidTarget(GamePlayer attacker, GameLiving target)
        {
            if (target == null)
                return false;

            // Special case: "Player" means any enemy player
            if (string.Equals(_targetName, "Player", StringComparison.OrdinalIgnoreCase))
            {
                var targetPlayer = target as GamePlayer;
                if (targetPlayer == null)
                    return false;

                // Do not count grey cons
                if (attacker.IsPlayerGreyCon(targetPlayer))
                    return false;

                return true;
            }

            if (target is not GameNPC npc)
                return false;

            if (_targetNpc == null)
                return false;

            return npc.CurrentRegion == _targetNpc.CurrentRegion &&
                   npc.Name == _targetNpc.Name;
        }

        internal void OnCriticalHit(PlayerQuest questData, PlayerGoalState state, GamePlayer attacker, GameLiving target)
        {
            if (state == null || !state.IsActive)
                return;

            if (!IsValidTarget(attacker, target))
                return;

            if (!IsValidLocation(attacker, target))
                return;

            AdvanceGoal(questData, state);
        }

        public override void Unload()
        {
            if (_area != null)
                WorldMgr.GetRegion(_areaRegion)?.RemoveArea(_area);

            base.Unload();
        }

        /// <summary>
        /// Helper called from GamePlayer.ModifyAttack when a crit occurs.
        /// </summary>
        public static void OnPlayerCriticalHit(GamePlayer attacker, GameLiving target, AttackData ad)
        {
            if (attacker == null || target == null || ad == null)
                return;

            if (ad.CriticalDamage <= 0)
                return;

            // Only Spell or Melee (no DoT, no weird attack types)
            switch (ad.AttackType)
            {
                case AttackData.eAttackType.Spell:
                case AttackData.eAttackType.MeleeOneHand:
                case AttackData.eAttackType.MeleeTwoHand:
                case AttackData.eAttackType.MeleeDualWield:
                    break;
                default:
                    return;
            }

            foreach (var pq in attacker.QuestList.OfType<PlayerQuest>())
            {
                foreach (var goal in pq.Quest.Goals.Values)
                {
                    if (goal is not CriticalHitGoal critGoal)
                        continue;

                    var state = pq.GoalStates.Find(gs => gs.GoalId == goal.GoalId);
                    critGoal.OnCriticalHit(pq, state, attacker, target);
                }
            }
        }
    }
}
