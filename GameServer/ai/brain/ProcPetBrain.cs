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
using DOL.GS;

namespace DOL.AI.Brain
{
    public class ProcPetBrain : StandardMobBrain, IControlledBrain
    {
        private GameLiving m_target;
        private readonly GameLiving m_owner;

        public ProcPetBrain(GameLiving owner)
        {
            m_owner = owner;
            m_target = owner.TargetObject as GameLiving;
            AggroLevel = 100;
            IsMainPet = false;
        }

        /// <inheritdoc />
        protected override void SetBody(GameNPC npc)
        {
            base.SetBody(npc);
            npc.Owner = m_owner;
        }

        public override int ThinkInterval { get { return 1500; } }

        public override void Think() { AttackMostWanted(); }

        public void SetAggressionState(eAggressionState state) { }

        protected override void AttackMostWanted()
        {
            if (!IsActive || m_target == null) return;
            GameLiving target = m_target;
            if (target != null && target.IsAlive)
                Body.StartAttack(target);
            else
            {
                m_target = null;
                Body.LastAttackTickPvP = 0;
                Body.LastAttackTickPvE = 0;
            }
        }

        #region IControlledBrain Members
        public eWalkState WalkState { get { return eWalkState.Stay; } }
        public eAggressionState AggressionState { get { return eAggressionState.Aggressive; } set { } }
        public GameLiving Owner => Body.Owner;
        public void Attack(GameObject target) { }
        public void Follow(GameObject target) { }
        public void FollowOwner() { }
        public void Stay() { }
        public void ComeHere() { }
        public void Goto(GameObject target) { }
        public void UpdatePetWindow() { }
        public bool IsMainPet { get { return false; } set { } }
        #endregion
    }
}
