/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.Numerics;
using DOL.GS;

namespace DOL.AI.Brain
{
    public class SuperFearBrain : FearBrain
    {
        short m_maxSpeedBuff = 0;
        ///<summary>
        /// Calculate flee target.
        /// </summary>
        ///<param name="target">The target to flee.</param>
        protected virtual void CalculateFleeTarget(GameLiving target)
        {
            ushort TargetAngle = (ushort)((Body.GetHeading(target) + 2048) % 4096);

            var fleePoint = Body.GetPointFromHeading(TargetAngle, 450);
            var point = PathingMgr.Instance.GetClosestPoint(Body.CurrentZone, new Vector3(fleePoint, Body.Position.Z), 128, 128, 256);
            Body.StopFollowing();
            Body.StopAttack();
            Body.PathTo(point.HasValue ? point.Value : new Vector3(fleePoint, Body.Position.Z), Body.MaxSpeed);
            //set speed to 130%
            m_maxSpeedBuff = (short)(Body.MaxSpeedBase * 0.3);
            Body.MaxSpeedBase = (short)(Body.MaxSpeedBase * 1.3);
        }
        //on removal of the brain set speed to normal
        public override void RemoveEffect()
        {
            Body.MaxSpeedBase = (short)(Body.MaxSpeedBase - m_maxSpeedBuff);
        }
    }
}
