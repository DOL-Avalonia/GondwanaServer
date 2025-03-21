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
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Reduce range needed to cast the sepll
    /// </summary>
    [SpellHandler("Silence")]
    public class SilenceSpellHandler : SpellHandler
    {
        /// <summary>
        /// Apply effect on target or do spell action if non duration spell
        /// </summary>
        /// <param name="target">target that gets the effect</param>
        /// <param name="effectiveness">factor from 0..1 (0%-100%)</param>
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect effect;
            effect = SpellHandler.FindEffectOnTarget(target, "Silence");
            if (effect != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Silence.AlreadyAffected"), eChatType.CT_SpellResisted);
                return false;
            }
            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            if (effect.Owner is GamePlayer)
            {
                effect.Owner.SilencedCount++;
                effect.Owner.StopCurrentSpellcast();
                effect.Owner.StartInterruptTimer(effect.Owner.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            }
        }

        /// <inheritdoc />
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            effect.Owner.SilencedCount--;
            return base.OnEffectExpires(effect, noMessages);
        }

        // constructor
        public SilenceSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int durationSeconds = Spell.Duration / 1000;
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Silence.MainDescription", durationSeconds);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}
