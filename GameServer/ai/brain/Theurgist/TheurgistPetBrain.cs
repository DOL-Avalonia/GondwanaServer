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
using System.Collections.Generic;
using System.Text;
using DOL.Events;
using DOL.GS;
using log4net;
using System.Reflection;
using System.Collections;
using DOL.GS.Spells;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;

namespace DOL.AI.Brain
{
    public class TheurgistPetBrain : StandardMobBrain, IControlledBrain
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private GameLiving m_target;
        private bool m_melee = false;
        private bool m_active = true;
        private readonly GameLiving m_owner;

        public TheurgistPetBrain(GameLiving owner)
        {
            m_owner = owner;
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

        public override void Notify(DOLEvent e, object sender, EventArgs args)
        {
            if (!IsActive || m_melee || !m_active) return;

            if (args as AttackFinishedEventArgs != null)
            {
                m_melee = true;
                GameLiving target = m_target;
                if (target != null) Body.StartAttack(target);
                return;
            }
            if (e == GameLivingEvent.CastFailed)
            {

                GameLiving target = m_target;
                if (target != null) Body.StartAttack(target);
                return;

            }
        }

        protected override void AttackMostWanted()
        {
            if (!IsActive || !m_active) return;
            if (m_target == null) m_target = (GameLiving)Body.TempProperties.getProperty<object>("target", null);

            if (m_target == null || !m_target.IsAlive)
            {
                Body.Die(Body);
            }
            else
            {
                GameLiving target = m_target;
                Body.TargetObject = target;

                if (!CheckSpells(eCheckSpellType.Offensive))
                    Body.StartAttack(target);
            }
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (Body == null || Body.Spells == null || Body.Spells.Count < 1 || m_melee)
                return false;

            if (Body.IsCasting)
                return true;

            bool casted = false;
            if (type == eCheckSpellType.Defensive)
            {
                foreach (Spell spell in Body.Spells)
                {
                    if (!Body.IsBeingInterrupted && Body.GetSkillDisabledDuration(spell) == 0 && CheckDefensiveSpells(spell))
                    {
                        casted = true;
                        break;
                    }
                }
            }
            else
            {
                foreach (Spell spell in Body.Spells)
                {
                    if (Body.GetSkillDisabledDuration(spell) == 0)
                    {
                        if (spell.CastTime > 0)
                        {
                            if (!Body.IsBeingInterrupted && CheckOffensiveSpells(spell))
                            {
                                casted = true;
                                break;
                            }
                        }
                        else
                        {
                            CheckInstantSpells(spell);
                        }
                    }
                }
            }
            if (!Body.AttackState)
                Follow(Owner);
            return casted;
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
