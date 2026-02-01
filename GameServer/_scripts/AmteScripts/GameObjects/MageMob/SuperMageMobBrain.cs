using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.Scripts;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using static DOL.GS.Region;
using Vector = DOL.GS.Geometry.Vector;

namespace DOL.AI.Brain
{
    public class SuperMageMobBrain : MageMobBrain
    {
        private const int SafeDistanceMin = 850;
        private const int PanicDistance = 350;
        private const int MaxCombatRange = 1500;

        // Squad Parameters
        private const int SquadSpacing = 250;
        private const int EscortDistance = 450;
        private const int PlayerScanRadius = WorldMgr.VISIBILITY_DISTANCE / 2; // Player detection radius

        // Speed Modifiers
        private const double SpeedPanic = 1.6;
        private const double SpeedTactical = 1.3;

        public override int ThinkInterval => 800;

        protected override bool ThinkCombat()
        {
            GamePlayer threat = GetClosestPlayerThreat();
            if (threat != null)
            {
                // Use AggroRange to determine squad formation distance
                float distSquaredToThreat = Body.GetDistanceSquaredTo(threat);
                const float panicDistSquared = PanicDistance * PanicDistance;
                const float safeDistSquared = SafeDistanceMin * SafeDistanceMin;
                if (distSquaredToThreat < panicDistSquared)
                {
                    if (Body.IsCasting)
                        Body.StopCurrentSpellcast();

                    if (!Body.IsMoving)
                    {
                        // 160% Speed
                        TryCastInstantSpells(threat);
                        PerformKiteMove(threat, SpeedPanic, PanicDistance);
                    }
                }
                else if (distSquaredToThreat < safeDistSquared)
                {
                    if (!Body.IsCasting)
                    {
                        if (!Body.IsMoving)
                        {
                            // 130% Speed
                            TryCastInstantSpells(threat);
                            PerformKiteMove(threat, SpeedTactical, SafeDistanceMin);
                        }
                    }
                }
                return true;
            }
            
            AttackMostWanted();
            return HasAggro || Body.InCombat;
        }

        protected override bool ThinkIdle()
        {
            if (Body.IsReturningHome)
                return true;

            // Check if player/pet is nearby (alertness check)
            bool playerNearby = false;
            foreach (GameLiving living in Body.GetPlayersInRadius(PlayerScanRadius))
            {
                if (living is GamePlayer or GamePet
                    && living.IsVisibleTo(Body)
                    && !living.IsStealthed
                    && GameServer.ServerRules.IsAllowedToAttack(living, Body, true))
                {
                    playerNearby = true;
                    break;
                }
            }

            // Only form squads if players/pets are nearby
            if (!playerNearby)
            {
                return false;
            }

            // 1. Escort AmteMobs (Using AggroRange)
            int scanRange = AggroRange > 0 ? AggroRange : 1000;
            var infantry = Body.GetNPCsInRadius((ushort)scanRange)
                .Cast<GameNPC>()
                .Where(n => n is AmteMob { InCombat: false, IsAlive: true, ObjectState: GameObject.eObjectState.Active } and not MageMob)
                .Cast<AmteMob>()
                .OrderBy(n => Body.GetDistanceSquaredTo(n))
                .FirstOrDefault();
            if (infantry != null)
            {
                Angle angleBehind = infantry.Orientation + Angle.Degrees(180);
                angleBehind += Angle.Degrees(Util.Random(-30, 30));
                Coordinate dest = infantry.Coordinate + Vector.Create(angleBehind, EscortDistance);
                const int tolerance = EscortDistance + GameNPC.CONST_WALKTOTOLERANCE;
                if (Body.IsWithinRadius(dest, tolerance))
                {
                    if (Body.IsMoving)
                    {
                        Body.StopMoving();
                        Body.TurnTo(infantry.Orientation);
                    }
                }
                else if (!Body.IsMoving || !Body.Destination.IsWithinDistance(dest, tolerance))
                {
                    Body.PathTo(dest, Body.MaxSpeed);
                }
                return true;
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

                if (Body.IsWithinOrMovingIntoRadius(centerMass, 300))
                {
                    var closest = squadMates.OrderBy(m => Body.GetDistanceSquaredTo(m)).First();
                    if (Body.IsWithinOrMovingIntoRadius(closest, 120))
                    {
                        var angleAway = closest.Coordinate.GetOrientationTo(Body.Coordinate);
                        var dest = Body.Coordinate + Vector.Create(angleAway, 150);
                        Body.PathTo(dest, 50);
                        return true;
                    }
                }
                else
                {
                    Body.PathTo(centerMass, (short)(Body.MaxSpeed / 2));
                }
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        protected override void AttackMostWanted()
        {
            if (!IsActive)
                return;

            if (Body.IsCasting)
                return;

            var prevTarget = Body.TargetObject;
            Body.TargetObject = CalculateNextAttackTarget();
            
            bool hasSquad = GetSquadMembers(Body.Coordinate).Any();
            bool success = false;
            // Prioritize offense/defense based on squad status
            if (!hasSquad)
            {
                success = CheckSpells(eCheckSpellType.Defensive);
            }
            
            if (!success && Body.TargetObject != null)
            {
                success = CheckSpells(eCheckSpellType.Offensive);
            }

            if (hasSquad && !success)
            {
                success = CheckSpells(eCheckSpellType.Defensive);
            }
            
            if (!success && Body.TargetObject != null)
            {
                MoveInRange(Body.TargetObject);
            }
        }

        private IEnumerable<MageMob> GetSquadMembers(Coordinate where)
        {
            int squadRange = AggroRange > 0 ? AggroRange : 1000;
            return Body.CurrentRegion.GetNPCsInRadius(where, (ushort)squadRange, false, false)
                .Cast<GameNPC>()
                .Where(m => m is MageMob { IsAlive: true, ObjectState: GameObject.eObjectState.Active } mageMob && mageMob != Body)
                .Cast<MageMob>();
        }

        protected override void PerformKiteMove(GameLiving enemy, double speedFactor, double distance)
        {
            var myCoordinate = Body.Coordinate;
            var angleFromEnemy = enemy.Coordinate.GetOrientationTo(myCoordinate) + Angle.Degrees(Util.Random(-20, 20));
            var moveVector = Vector.Create(angleFromEnemy, distance);
            Coordinate targetLoc = myCoordinate + moveVector;
            var nearbyAllies = GetSquadMembers(targetLoc).ToList();
            if (nearbyAllies is { Count: > 0 })
            {
                Vector repulsion = Vector.Zero;
                int neighbors = 0;
                foreach (MageMob mate in nearbyAllies)
                {
                    moveVector += mate.Coordinate - myCoordinate;
                }
                moveVector /= (nearbyAllies.Count + 1);
                targetLoc = myCoordinate + moveVector;
                foreach (MageMob mate in nearbyAllies)
                {
                    if (targetLoc.IsWithinDistance(mate.Coordinate, SquadSpacing))
                    {
                        var angleFromNeighbor = mate.Coordinate.GetOrientationTo(targetLoc);
                        repulsion += Vector.Create(angleFromNeighbor, 150);
                        neighbors++;
                    }
                }

                if (neighbors > 0)
                {
                    targetLoc += repulsion;
                }
            }

            if (Body.MaxDistance > 0 && !targetLoc.IsWithinDistance(Body.Home, Body.MaxDistance))
            {
                var angleHome = myCoordinate.GetOrientationTo(Body.Home.Coordinate);
                targetLoc = myCoordinate + Vector.Create(angleHome, distance);
            }

            var safePoint = PathingMgr.Instance.GetClosestPointAsync(Body.CurrentZone, targetLoc, 128, 128, 256);
            Coordinate finalDest = safePoint.HasValue ? Coordinate.Create(safePoint.Value) : targetLoc;
            // Apply speed factor
            Body.PathTo(finalDest, (short)(Body.MaxSpeed * speedFactor));
        }
    }
}