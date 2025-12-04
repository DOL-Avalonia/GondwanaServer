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
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandlerAttribute("VampiirStealthDetection")]
    public class VampiirStealthDetection : SpellHandler
    {
        public VampiirStealthDetection(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {

            effect.Owner.BaseBuffBonusCategory[(int)eProperty.Skill_Stealth] += (int)m_spell.Value;
            base.OnEffectStart(effect);
            //		effect.Owner.BuffBonusCategory1[(int)eProperty.StealthRange] += (int)m_spell.Value;
        }


        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            //		effect.Owner.BuffBonusCategory1[(int)eProperty.StealthRange] -= (int)m_spell.Value;
            effect.Owner.BaseBuffBonusCategory[(int)eProperty.Skill_Stealth] -= (int)m_spell.Value;
            return base.OnEffectExpires(effect, noMessages);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            double detectPercent;
            if (Spell.Value <= 125) detectPercent = 19;
            else if (Spell.Value >= 225) detectPercent = 50;
            else detectPercent = 19 + (Spell.Value - 125) * 0.31;

            int roundedDetect = (int)Math.Round(detectPercent);
            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.VampiirStealthDetection.MainDescription", roundedDetect);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}























