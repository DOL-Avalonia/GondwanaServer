using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.Scripts;
using Vector = DOL.GS.Geometry.Vector;

namespace DOL.AI.Brain
{
    public class SuperMageMobBrain : AmteMobBrain
    {
        private const int SafeDistanceMin = 850;
        private const int PanicDistance = 350;
        private const int MaxCombatRange = 1500;

        // Squad Parameters
        private const int SquadSpacing = 250;
        private const int EscortDistance = 450;
        private const int PlayerScanRadius = 1200; // Player detection radius

        // Speed Modifiers
        private const double SpeedPanic = 1.6;
        private const double SpeedTactical = 1.3;

        public override int ThinkInterval => 800;

        public override void Think()
        {
            if (!Body.IsAlive || Body.IsResetting || Body.IsReturningHome || Body.IsIncapacitated || Body.IsTurningDisabled)
            {
                base.Think();
                return;
            }

            if (HasAggro || Body.InCombat)
                HandleCombatLogic();
            else
                HandlePeaceLogic();
        }

        private void HandleCombatLogic()
        {
            GamePlayer threat = GetClosestThreat();

            if (threat == null)
            {
                if (Body.TargetObject != null) base.Think();
                return;
            }

            float distToThreat = Body.GetDistanceTo(threat);
            // Use AggroRange to determine squad formation distance
            int squadRange = AggroRange > 0 ? AggroRange : 1000;

            var nearbyAllies = Body.GetNPCsInRadius((ushort)squadRange)
                                   .OfType<MageMob>()
                                   .Where(m => m != Body && m.IsAlive)
                                   .ToList();

            bool hasSquad = nearbyAllies.Count > 0;

            if (distToThreat < PanicDistance)
            {
                if (Body.IsCasting) Body.StopCurrentSpellcast();
                // 160% Speed
                MoveTactically(threat, SpeedPanic, hasSquad ? nearbyAllies : null);
                TryCastInstantSpells(threat);
            }
            else if (distToThreat < SafeDistanceMin)
            {
                if (!Body.IsCasting)
                {
                    // 130% Speed
                    MoveTactically(threat, SpeedTactical, hasSquad ? nearbyAllies : null);
                    TryCastInstantSpells(threat);
                }
            }
            else
            {
                if (Body.IsMoving) Body.StopMoving();

                if (Body.TargetObject != threat)
                {
                    Body.TargetObject = threat;
                    Body.TurnTo(threat);
                }

                if (!Body.IsCasting)
                {
                    // Prioritize offense/defense based on squad status
                    if (hasSquad)
                    {
                        if (!CheckSpells(eCheckSpellType.Offensive))
                            CheckSpells(eCheckSpellType.Defensive);
                    }
                    else
                    {
                        if (!CheckSpells(eCheckSpellType.Defensive))
                            CheckSpells(eCheckSpellType.Offensive);
                    }
                }
            }
        }

        private void HandlePeaceLogic()
        {
            if (Body.IsReturningHome) return;

            // Check if player/pet is nearby (alertness check)
            bool playerNearby = false;
            foreach (GameLiving living in Body.GetPlayersInRadius(PlayerScanRadius))
            {
                if (living is GamePlayer || living is GamePet)
                {
                    playerNearby = true;
                    break;
                }
            }

            // Only form squads if players/pets are nearby
            if (!playerNearby)
            {
                base.Think();
                return;
            }

            // 1. Escort AmteMobs (Using AggroRange)
            int scanRange = AggroRange > 0 ? AggroRange : 1000;

            var infantry = Body.GetNPCsInRadius((ushort)scanRange)
                               .OfType<AmteMob>()
                               .Where(n => !(n is MageMob) && !n.InCombat && n.IsAlive)
                               .OrderBy(n => Body.GetDistanceTo(n))
                               .FirstOrDefault();

            if (infantry != null)
            {
                Angle angleBehind = infantry.Orientation + Angle.Degrees(180);
                angleBehind += Angle.Degrees(Util.Random(-30, 30));

                Coordinate dest = infantry.Coordinate + Vector.Create(angleBehind, EscortDistance);

                if (!Body.IsWithinRadius(dest, 150))
                {
                    Body.PathTo(dest, Body.MaxSpeed);
                }
                else if (Body.IsMoving)
                {
                    Body.StopMoving();
                    Body.TurnTo(infantry.Orientation);
                }
                return;
            }

            // 2. Flock with other MageMobs (Using AggroRange)
            var squadMates = Body.GetNPCsInRadius((ushort)scanRange)
                                 .OfType<MageMob>()
                                 .Where(m => m != Body && !m.InCombat && m.IsAlive)
                                 .ToList();

            if (squadMates.Count > 0)
            {
                long totalX = Body.Coordinate.X;
                long totalY = Body.Coordinate.Y;
                foreach (var mate in squadMates)
                {
                    totalX += mate.Coordinate.X;
                    totalY += mate.Coordinate.Y;
                }

                int avgX = (int)(totalX / (squadMates.Count + 1));
                int avgY = (int)(totalY / (squadMates.Count + 1));
                Coordinate centerMass = Coordinate.Create(avgX, avgY, Body.Coordinate.Z);

                if (!Body.IsWithinRadius(centerMass, 300))
                {
                    Body.PathTo(centerMass, (short)(Body.MaxSpeed / 2));
                }
                else
                {
                    foreach (var mate in squadMates)
                    {
                        if (Body.IsWithinRadius(mate, 120))
                        {
                            var angleAway = mate.Coordinate.GetOrientationTo(Body.Coordinate);
                            var dest = Body.Coordinate + Vector.Create(angleAway, 150);
                            Body.PathTo(dest, 50);
                            return;
                        }
                    }
                }
            }
            else
            {
                base.Think();
            }
        }

        private void MoveTactically(GameLiving enemy, double speedFactor, List<MageMob> squadMates)
        {
            var angleFromEnemy = enemy.Coordinate.GetOrientationTo(Body.Coordinate);
            var moveVector = Vector.Create(angleFromEnemy, 300);

            if (squadMates != null && squadMates.Count > 0)
            {
                Vector repulsion = Vector.Zero;
                int neighbors = 0;

                foreach (MageMob mate in squadMates)
                {
                    if (Body.GetDistanceTo(mate) < SquadSpacing)
                    {
                        var angleFromNeighbor = mate.Coordinate.GetOrientationTo(Body.Coordinate);
                        repulsion += Vector.Create(angleFromNeighbor, 150);
                        neighbors++;
                    }
                }

                if (neighbors > 0)
                {
                    moveVector = (moveVector + repulsion);
                }
            }
            else
            {
                Angle wobble = Angle.Degrees(Util.Random(-20, 20));
                moveVector = Vector.Create(angleFromEnemy + wobble, 300);
            }

            Coordinate targetLoc = Body.Coordinate + moveVector;

            if (Body.MaxDistance > 0 && !targetLoc.IsWithinDistance(Body.Home, Body.MaxDistance))
            {
                var angleHome = Body.Coordinate.GetOrientationTo(Body.Home.Coordinate);
                targetLoc = Body.Coordinate + Vector.Create(angleHome, 250);
            }

            var safePoint = PathingMgr.Instance.GetClosestPointAsync(Body.CurrentZone, targetLoc, 128, 128, 256);
            Coordinate finalDest = safePoint.HasValue ? Coordinate.Create(safePoint.Value) : targetLoc;

            // Apply speed factor
            Body.PathTo(finalDest, (short)(Body.MaxSpeed * speedFactor));
        }

        private void TryCastInstantSpells(GameLiving target)
        {
            if (Body.IsCasting) return;

            if (Body.InstantHarmfulSpells != null && Body.InstantHarmfulSpells.Count > 0)
            {
                foreach (Spell s in Body.InstantHarmfulSpells)
                {
                    if (Body.GetSkillDisabledDuration(s) > 0) continue;
                    if (!Body.IsWithinRadius(target, s.Range)) continue;

                    Body.TurnTo(target, false);
                    Body.CastSpell(s, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    return;
                }
            }
        }

        private GamePlayer GetClosestThreat()
        {
            GamePlayer best = null;
            double bestDist = 2000;

            foreach (GamePlayer p in Body.GetPlayersInRadius(2000))
            {
                if (!p.IsAlive || p.IsStealthed || p.ObjectState != GameObject.eObjectState.Active) continue;
                if (!GameServer.ServerRules.IsAllowedToAttack(Body, p, true)) continue;

                double d = Body.GetDistanceTo(p);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }
    }
}