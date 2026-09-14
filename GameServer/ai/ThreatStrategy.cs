using System;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.Scripts;

namespace DOL.AI.Brain
{
    /// <summary>
    /// Gondwana threat rules used by AggroTable.UpdateAndGetHighestThreat().
    /// Effective aggro = base aggro * min(500/distance, 1) * FollowingFriendMob.AggroMultiplier
    /// </summary>
    public class GondwanaThreatStrategy : IThreatStrategy
    {
        protected readonly StandardMobBrain _owner;

        /// <summary>A new target must beat the current one by this factor before the mob switches.</summary>
        protected virtual double StickyTargetFactor => 1.10;

        /// <summary>Distance under which aggro is not reduced (Gondwana legacy value).</summary>
        protected virtual double DistanceThreshold => 500.0;

        public GondwanaThreatStrategy(StandardMobBrain owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        /// Pets only chase inside their AggroRange, normal mobs up to MAX_AGGRO_DISTANCE.
        /// </summary>
        protected virtual int MaxAggroDistance
            => _owner is IControlledBrain ? _owner.AggroRange : StandardMobBrain.MAX_AGGRO_DISTANCE;

        public virtual bool ShouldBeRemoved(GameLiving target)
        {
            GameNPC body = _owner.Body;
            if (target == null || body == null) return true;
            if (!target.IsAlive || target.ObjectState != GameObject.eObjectState.Active) return true;
            if (target.CurrentRegion != body.CurrentRegion) return true;
            if (target.IsStealthed) return true;
            if (!body.IsWithinRadius2D(target, StandardMobBrain.MAX_AGGRO_LIST_DISTANCE)) return true;
            return false;
        }

        public virtual long CalculateEffectiveAggro(long baseAggro, GameLiving target, out double distance)
        {
            distance = _owner.Body.GetDistanceTo(target.Position, 0);
            double aggro = baseAggro * Math.Min(DistanceThreshold / Math.Max(1.0, distance), 1.0);

            // Gondwana: a friend NPC escorting a player draws extra hate
            if (target is FollowingFriendMob { PlayerFollow: not null } friend && friend.AggroMultiplier > 0f)
                aggro *= friend.AggroMultiplier;

            return (long)Math.Ceiling(aggro);
        }

        public virtual bool ShouldBeIgnored(GameLiving target, double distance)
        {
            if (distance > MaxAggroDistance) return true;
            if (target.EffectList.GetOfType<NecromancerShadeEffect>() != null) return true;
            if (!GameServer.ServerRules.IsAllowedToAttack(_owner.Body, target, true)) return true;
            return false;
        }

        public virtual bool IsHigherThreat(long incumbentEffectiveAggro, long challengerEffectiveAggro)
            => challengerEffectiveAggro > incumbentEffectiveAggro;

        public virtual GameLiving SelectTarget(ReadOnlySpan<AggroTable.TargetCandidate> candidates)
        {
            if (candidates.Length == 0) return null;

            GameLiving current = _owner.Body.TargetObject as GameLiving;
            GameLiving best = null;
            long bestAggro = -1;
            long currentAggro = -1;

            foreach (AggroTable.TargetCandidate c in candidates)
            {
                if (c.Living == current) currentAggro = c.EffectiveAggro;
                if (c.EffectiveAggro > bestAggro) { bestAggro = c.EffectiveAggro; best = c.Living; }
            }

            // stay on the current target unless the challenger clearly out-threats it
            if (current != null && currentAggro > 0 && best != current && bestAggro <= currentAggro * StickyTargetFactor)
                return current;

            return best;
        }

        public virtual void OnTargetRemoved(GameLiving target)
        {
            _owner.Body?.RemoveAttacker(target);
        }
    }
}