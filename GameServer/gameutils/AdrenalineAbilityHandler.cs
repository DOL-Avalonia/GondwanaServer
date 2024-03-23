using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using DOL.Database;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using log4net;

namespace DOL.GS
{
    class AdrenalineAbilityHandler : Ability
    {
        public AdrenalineAbilityHandler(DBAbility ability, int level) : base(ability, level)
        {
        }

        private SpellLine m_spellLine;

        public SpellLine SpellLine
        {
            get
            {
                m_spellLine ??= SkillBase.GetSpellLine("Adrenaline");
                return m_spellLine;
            }
        }

        /// <inheritdoc />
        public override void Execute(GameLiving living)
        {
            Spell sp = living.AdrenalineSpell;

            if (sp == null)
            {
                return;
            }

            living.CastSpell(sp, SpellLine);
        }
    }
}
