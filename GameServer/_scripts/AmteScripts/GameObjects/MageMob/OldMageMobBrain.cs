using System;
using AmteScripts.Managers;
using DOL.gameobjects.CustomNPC;
using DOL.GS;
using DOL.GS.Scripts;
using System.Numerics;
using DOL.GS.Geometry;
using Vector = DOL.GS.Geometry.Vector;

namespace DOL.AI.Brain
{
    public class OldMageMobBrain : AmteMobBrain
    {
        /// <summary>
        /// Flee from close Players or on low mana on Brain Think
        /// </summary>
        public override void Think()
        {

            foreach (GamePlayer player in Body.GetPlayersInRadius(70))
            {
                CalculateFleeTarget(player);
                return;
            }

            if (Body.Mana < Body.MaxMana * 0.15)
            {
                foreach (GamePlayer player in Body.GetPlayersInRadius(750))
                {
                    CalculateFleeTarget(player);
                    return;
                }
            }
            base.Think();
        }

        ///<summary>
        /// Calculate flee target.
        /// </summary>
        ///<param name="target">The target to flee.</param>
        protected virtual void CalculateFleeTarget(GameLiving target)
        {
            var TargetAngle = Body.GetAngleTo(target.Coordinate);

            var fleePoint = Body.Coordinate + Vector.Create(TargetAngle, 300);
            var point = PathingMgr.Instance.GetClosestPointAsync(Body.CurrentZone, Coordinate.Create(fleePoint.X, fleePoint.Y, Body.Position.Z), 128, 128, 256);
            Body.StopFollowing();
            Body.StopAttack();
            Body.PathTo(point.HasValue ? Coordinate.Create(point.Value) : Coordinate.Create(fleePoint.X, fleePoint.Y, Body.Position.Z), Body.MaxSpeed);
        }

    }
}
