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
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("PetConversion")]
    public class PetConversionSpellHandler : SpellHandler
    {
        public PetConversionSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        protected override bool ExecuteSpell(GameLiving target, bool force = false)
        {
            var targets = SelectTargets(target, force);
            if (targets.Count <= 0) return false;
            int mana = 0;

            foreach (GameLiving living in targets)
            {
                ApplyEffectOnTarget(living, 1.0);
                mana += (int)(living.Health * Spell.Value / 100);
            }

            int absorb = m_caster.ChangeMana(m_caster, GameLiving.eManaChangeType.Spell, mana);

            if (m_caster is GamePlayer)
            {
                if (absorb > 0)
                    MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.PetConversion.AbsorbPower", absorb), eChatType.CT_Spell);
                else
                    MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.PetConversion.ManaFull"), eChatType.CT_SpellResisted);
                ((GamePlayer)m_caster).CommandNpcRelease();
            }

            return true;
        }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.PetConversion.MainDescription", Spell.Value);
        }
    }
}
