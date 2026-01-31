using DOL.GS;
using DOL.GS.Geometry;
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

        public override void Think()
        {
            if (!Body.IsAlive || Body.IsResetting || Body.IsReturningHome || Body.IsIncapacitated || Body.IsTurningDisabled)
            {
                base.Think();
                return;
            }

            if (!HasAggro)
            {
                base.Think();
                return;
            }

            GamePlayer closestThreat = GetClosestPlayerThreat();

            if (closestThreat == null)
            {
                if (Body.TargetObject != null)
                {
                    base.Think();
                }
                return;
            }

            float distToThreat = Body.GetDistanceTo(closestThreat);

            // --- PANIC ---
            if (distToThreat < DangerDistance)
            {
                if (Body.IsCasting)
                {
                    Body.StopCurrentSpellcast();
                }

                // Run FAST (160% Speed)
                PerformKiteMove(closestThreat, SpeedFast);
                TryCastInstantSpells(closestThreat);
            }
            // --- ADJUSTMENT ---
            else if (distToThreat < SafeDistanceMin)
            {
                if (!Body.IsCasting)
                {
                    // Reposition (130% Speed)
                    PerformKiteMove(closestThreat, SpeedSlow);
                    TryCastInstantSpells(closestThreat);
                }
            }
            // --- NUKE ---
            else if (distToThreat >= SafeDistanceMin && distToThreat <= MaxCombatRange)
            {
                if (Body.IsMoving)
                {
                    Body.StopMoving();
                }

                if (Body.TargetObject != closestThreat)
                {
                    Body.TargetObject = closestThreat;
                }
                Body.TurnTo(closestThreat);

                if (!Body.IsCasting)
                {
                    if (!CheckSpells(eCheckSpellType.Defensive))
                    {
                        CheckSpells(eCheckSpellType.Offensive);
                    }
                }
            }
            else
            {
                base.Think();
            }
        }

        private GamePlayer GetClosestPlayerThreat()
        {
            GamePlayer closest = null;
            double shortestDist = MaxCombatRange + 500;

            foreach (GamePlayer player in Body.GetPlayersInRadius((ushort)shortestDist))
            {
                if (IsPlayerIgnored(player)) continue;

                if (GameServer.ServerRules.IsAllowedToAttack(Body, player, true))
                {
                    double d = Body.GetDistanceTo(player);
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
            if (player.IsStealthed) return true;
            if (player.ObjectState != GameObject.eObjectState.Active) return true;
            return false;
        }

        private void PerformKiteMove(GameLiving enemy, double speedFactor)
        {
            var angleToEnemy = Body.Coordinate.GetOrientationTo(enemy.Coordinate);
            var angleAway = angleToEnemy + Angle.Degrees(180);

            // If we are panicking (SpeedFast), we run further, otherwise just a short hop
            int fleeDist = (speedFactor >= SpeedFast) ? 500 : 250;

            angleAway += Angle.Degrees(Util.Random(-15, 15));

            var targetPoint = Body.Coordinate + Vector.Create(angleAway, fleeDist);

            if (Body.MaxDistance > 0 && !targetPoint.IsWithinDistance(Body.Home, Body.MaxDistance))
            {
                var angleToHome = Body.Coordinate.GetOrientationTo(Body.Home.Coordinate);
                targetPoint = Body.Coordinate + Vector.Create(angleToHome, fleeDist);
            }

            var safePoint = PathingMgr.Instance.GetClosestPointAsync(Body.CurrentZone, targetPoint, 128, 128, 256);
            var destination = safePoint.HasValue ? Coordinate.Create(safePoint.Value) : targetPoint;

            // Apply the speed multiplier
            short speed = (short)(Body.MaxSpeed * speedFactor);
            Body.PathTo(destination, speed);
        }

        private void TryCastInstantSpells(GameLiving target)
        {
            if (Body.IsCasting) return;

            if (Body.InstantHarmfulSpells != null && Body.InstantHarmfulSpells.Count > 0)
            {
                foreach (Spell spell in Body.InstantHarmfulSpells)
                {
                    if (Body.GetSkillDisabledDuration(spell) > 0) continue;
                    if (!Body.IsWithinRadius(target, spell.Range)) continue;

                    Body.TurnTo(target, false);

                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    return;
                }
            }
        }
    }
}