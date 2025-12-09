using DOL.Events;
using DOL.GS.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static DOL.GS.GameLiving;

namespace DOL.GS.Quests
{
    /// <summary>
    /// Negative quest goal: progresses when the player is hit by an enemy.
    /// Parameters in JSON (db):
    /// - TargetName:
    ///     * null/empty => any attacker (NPC or player)
    ///     * "Player"   => only enemy players (non-grey con)
    ///     * other      => a specific NPC name in TargetRegion
    /// - TargetRegion: ushort (for NPC or region restriction; 0 => any region)
    /// - HitCount: number of hits required before the quest is cancelled (>=1)
    /// - AreaCenter / AreaRadius / AreaRegion: optional area restriction
    /// When Progress reaches HitCount, the entire quest is aborted.
    /// </summary>
    public class GetHarmedGoal : DataQuestJsonGoal
    {
        public override bool IsNegativeGoal => true;
        private readonly int _hitCount = 1;

        private readonly string _targetName;
        private readonly bool _anyTarget;
        private readonly bool _playerOnly;
        private readonly GameNPC _targetNpc;

        private readonly Region _region;
        private readonly ushort _regionId;

        private readonly Area.Circle _area;
        private readonly ushort _areaRegion;
        private readonly bool _hasArea;

        public override eQuestGoalType Type => eQuestGoalType.GetHarmed;
        public override int ProgressTotal => _hitCount;
        public override QuestZonePoint PointA { get; }

        public GetHarmedGoal(DataQuestJson quest, int goalId, dynamic db)
            : base(quest, goalId, (object)db)
        {
            _targetName = db.TargetName;
            _anyTarget = string.IsNullOrWhiteSpace(_targetName);
            _playerOnly = string.Equals(_targetName, "Player", StringComparison.OrdinalIgnoreCase);

            if (db.HitCount != null && db.HitCount != "")
                _hitCount = (int)db.HitCount;
            if (_hitCount <= 0)
                _hitCount = 1;

            if (db.TargetRegion != null && db.TargetRegion != "")
            {
                _regionId = (ushort)db.TargetRegion;
                _region = WorldMgr.GetRegion(_regionId);
            }

            if (!_anyTarget && !_playerOnly)
            {
                if (_regionId == 0)
                    throw new Exception($"[DataQuestJson] Quest {quest.Id}: GetHarmedGoal {goalId} needs a TargetRegion when TargetName is an NPC.");

                _targetNpc = WorldMgr.GetNPCsByNameFromRegion(_targetName, _regionId, eRealm.None).FirstOrDefault();
                if (_targetNpc == null)
                    throw new Exception($"[DataQuestJson] Quest {quest.Id}: can't load GetHarmedGoal {goalId}, target npc (name: {_targetName}, reg: {_regionId}) is not found");
            }

            if (db.AreaRadius != null && db.AreaRadius != "" &&
                db.AreaRegion != null && db.AreaRegion != "" &&
                db.AreaCenter != null)
            {
                _hasArea = true;
                _area = new Area.Circle(
                    $"{quest.Name} GetHarmedGoal {goalId}",
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

            if (_area != null)
            {
                dict.Add("AreaCenter", _area.Coordinate);
                dict.Add("AreaRadius", _area.Radius);
                dict.Add("AreaRegion", _areaRegion);
            }
            else
            {
                dict.Add("AreaCenter", null);
                dict.Add("AreaRadius", 0);
                dict.Add("AreaRegion", (ushort)0);
            }

            return dict;
        }

        public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
        {
            return false;
        }

        protected override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
        {
        }

        /// <summary>
        /// Per-goal logic when the owner (victim) is hit.
        /// </summary>
        internal void OnHarmed(PlayerQuest questData, PlayerGoalState state, GamePlayer victim, GameLiving attacker, AttackData ad)
        {
            if (state == null || !state.IsActive)
                return;

            if (Conditions?.Validate(questData, this) == false)
                return;

            if (attacker == null || attacker == victim)
                return;

            if (_region != null && victim.CurrentRegion != _region)
                return;

            if (_hasArea && !_area.IsContaining(victim.Coordinate, false))
                return;

            if (ad.Damage <= 0 && ad.CriticalDamage <= 0)
                return;

            if (ad.AttackResult != eAttackResult.HitStyle &&
                ad.AttackResult != eAttackResult.HitUnstyled)
                return;

            if (attacker is GamePlayer attackerPlayer)
            {
                if (!GameServer.ServerRules.IsAllowedToAttack(attackerPlayer, victim, false))
                    return;

                if (attackerPlayer.IsPlayerGreyCon(victim))
                    return;
            }

            if (_playerOnly)
            {
                if (attacker is not GamePlayer)
                    return;
            }
            else if (!_anyTarget)
            {
                if (attacker is not GameNPC npc)
                    return;

                if (_targetNpc == null ||
                    npc.CurrentRegion != _targetNpc.CurrentRegion ||
                    npc.Name != _targetNpc.Name)
                    return;
            }
            // else: _anyTarget => any attacker (player or NPC) is allowed


            bool finished = AdvanceGoal(questData, state);

            if (finished)
            {
                questData.AbortQuest();
            }
        }

        public override void Unload()
        {
            if (_area != null)
                WorldMgr.GetRegion(_areaRegion)?.RemoveArea(_area);

            base.Unload();
        }

        /// <summary>
        /// Static entry point called from GamePlayer.OnAttackedByEnemy.
        /// Goes through all PlayerQuest with GetHarmedGoal for this victim
        /// and lets each one update.
        /// </summary>
        public static void OnPlayerHarmed(GamePlayer victim, GameLiving attacker, AttackData ad)
        {
            if (victim == null || attacker == null || ad == null)
                return;

            if (ad.AttackResult != eAttackResult.HitStyle &&
                ad.AttackResult != eAttackResult.HitUnstyled)
                return;

            foreach (var pq in victim.QuestList.OfType<PlayerQuest>())
            {
                foreach (var goal in pq.Quest.Goals.Values)
                {
                    if (goal is not GetHarmedGoal harmedGoal)
                        continue;

                    var state = pq.GoalStates.Find(gs => gs.GoalId == goal.GoalId);
                    harmedGoal.OnHarmed(pq, state, victim, attacker, ad);
                }
            }
        }
    }
}
