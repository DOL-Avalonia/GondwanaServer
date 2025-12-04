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
using DOL.GS.Effects;
using System.Linq;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Base class for all spells that remove effects
    /// </summary>
    public abstract class RemoveSpellEffectHandler : SpellHandler
    {

        /// <summary>
        /// Spell effect type that will be removed
        /// RR4: now its a list of effects to remove
        /// </summary>
        public IEnumerable<string> SpellTypesToRemove
        {
            get;
            set;
        }

        /// <summary>
        /// called when spell effect has to be started and applied to targets
        /// </summary>
        /// <param name="target"></param>
        /// <param name="force"></param>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        public override void OnEffectPulse(GameSpellEffect effect)
        {
            base.OnEffectPulse(effect);
            OnDirectEffect(effect.Owner, effect.Effectiveness);
        }
        /// <summary>
        /// execute non duration spell effect on target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="effectiveness"></param>
        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target is not { IsAlive: true, ObjectState: GameObject.eObjectState.Active }) return false;
            if (!base.OnDirectEffect(target, effectiveness)) return false;

            // RR4: we remove all the effects
            foreach (var effect in target.FindEffectsOnTarget(s => SpellTypesToRemove.Contains(s.Spell.SpellType)))
            {
                effect.Cancel(false);
            }
            SendEffectAnimation(target, 0, false, 1);
            return true;
        }

        // constructor
        protected RemoveSpellEffectHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}