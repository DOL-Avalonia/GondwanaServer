using DOL.GS;
using DOL.GS.Geometry;
using System;
using System.Collections.Generic;
using Vector = DOL.GS.Geometry.Vector;

namespace DOL.AI.Brain
{
    public class MageMobBrain : AmteMobBrain
    {
        private const int SafeDistanceMin = 800;
        private const int DangerDistance = 350;
        private const int MaxCombatRange = 1500;

        // Speed Factors (Multipliers of MaxSpeed)
        private const double SpeedFast = 1.6;
        private const double SpeedSlow = 1.3;

        public override int ThinkInterval => 800;

        protected override bool ThinkCombat()
        {
            GamePlayer closestThreat = GetClosestPlayerThreat();
            if (closestThreat != null)
            {
                float distToThreatSquared = Body.GetDistanceSquaredTo(closestThreat);
                const float dangerDistSquared = (DangerDistance * DangerDistance);
                const float safeDistSquared = (SafeDistanceMin * SafeDistanceMin);
                // --- PANIC ---
                if (distToThreatSquared < dangerDistSquared)
                {
                    if (Body.IsCasting)
                    {
                        Body.StopCurrentSpellcast();
                    }

                    if (!Body.IsMoving || Body.Destination.IsWithinDistance(closestThreat.Position, Math.Ceiling(DangerDistance * 0.80)))
                    {
                        // Run FAST (160% Speed)
                        TryCastInstantSpells(closestThreat);
                        PerformKiteMove(closestThreat, DangerDistance, SpeedFast);
                    }
                }
                // --- ADJUSTMENT ---
                else if (distToThreatSquared < safeDistSquared)
                {
                    if (!Body.IsCasting)
                    {
                        if (!Body.IsMoving || Body.Destination.IsWithinDistance(closestThreat.Position, Math.Ceiling(SafeDistanceMin * 0.80)))
                        {
                            // Reposition (130% Speed)
                            TryCastInstantSpells(closestThreat);
                            PerformKiteMove(closestThreat, SafeDistanceMin, SpeedSlow);
                        }
                    }
                }
                return true;
            }

            AttackMostWanted();
            return HasAggro || Body.InCombat;
        }

        /// <summary>
        /// Selects and attacks the next target or does nothing
        /// </summary>
        protected override void AttackMostWanted()
        {
            if (!IsActive)
                return;

            Body.TargetObject = CalculateNextAttackTarget();
            if (Body.TargetObject != null)
            {
                // --- NUKE ---
                if (!Body.IsCasting)
                {
                    if (!CheckSpells(eCheckSpellType.Defensive))
                    {
                        CheckSpells(eCheckSpellType.Offensive);
                    }
                }
            }
        }

        protected GamePlayer GetClosestPlayerThreat()
        {
            GamePlayer closest = null;
            double shortestDist = double.MaxValue;

            foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)SafeDistanceMin))
            {
                if (IsPlayerIgnored(player)) continue;

                if (GameServer.ServerRules.IsAllowedToAttack(Body, player, true))
                {
                    double d = Body.GetDistanceSquaredTo(player);
                    if (d < shortestDist)
                    {
                        shortestDist = d;
                        closest = player;
                    }
                }
            }
            return closest;
        }

        private bool IsPlayerIgnored(GamePlayer player)
        {
            if (player == null) return true;
            if (!player.IsAlive) return true;
            if (player.IsStealthed || !player.IsVisibleTo(Body)) return true;
            if (player.ObjectState != GameObject.eObjectState.Active) return true;
            return false;
        }

        protected virtual void PerformKiteMove(GameLiving enemy, double distance, double speedFactor)
        {
            var curCoordinate = Body.Coordinate;
            var angleAway = enemy.Coordinate.GetOrientationTo(Body.Coordinate);

            angleAway += Angle.Degrees(Util.Random(-15, 15));

            distance += Util.Random(1, 50);
            var targetPoint = curCoordinate + Vector.Create(angleAway, distance);

            if (Body.MaxDistance > 0 && !targetPoint.IsWithinDistance(Body.Home, Body.MaxDistance))
            {
                var enemyCoordinate = enemy.Coordinate;
                var angleToHome = enemy.Coordinate.GetOrientationTo(Body.Home.Coordinate);
                targetPoint = enemyCoordinate + Vector.Create(angleToHome, distance + 1);
            }

            var safePoint = PathingMgr.Instance.GetClosestPointAsync(Body.CurrentZone, targetPoint, 128, 128, 256);
            var destination = safePoint.HasValue ? Coordinate.Create(safePoint.Value) : targetPoint;

            // Apply the speed multiplier
            short speed = (short)(Body.MaxSpeed * speedFactor);
            Body.PathTo(destination, speed, false);
        }

        protected virtual void TryCastInstantSpells(GameLiving target)
        {
            if (Body.IsCasting) return;

            if (!GameServer.ServerRules.IsAllowedToAttack(Body, target, true))
                return;

            var possibleSpells = new List<Spell>();
            if (Body.InstantHarmfulSpells is { Count: > 0 })
            {
                foreach (Spell spell in Body.InstantHarmfulSpells)
                {
                    if (Body.GetSkillDisabledDuration(spell) > 0) continue;
                    if (!Body.IsWithinRadius(target, spell.Range)) continue;
                    
                    possibleSpells.Add(spell);
                }
            }

            if (possibleSpells.Count > 0)
            {
                Body.StopMoving();
                Body.TurnTo(target, false);
                foreach (Spell spell in Body.InstantHarmfulSpells)
                {
                    if (Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)))
                        break;
                }
            }
        }
    }
}