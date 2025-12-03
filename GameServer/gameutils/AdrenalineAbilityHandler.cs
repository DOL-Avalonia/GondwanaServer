using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using DOL.Database;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using log4net;
using DOL.AI.Brain;
using DOL.GS.Spells;

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
            var player = living as GamePlayer;
            if (player == null) return;

            // Dynamically pick the adrenaline spell NOW (respects ChtonicShapeShift)
            Spell sp = ResolveAdrenalineSpell(player);
            if (sp == null) return;

            // Cast adrenaline on the player first (Mage adrenaline for Necromancer).
            player.CastSpell(sp, SpellLine);

            // If this is a Necromancer in shade form with an active pet,
            // also apply the tank adrenaline to the pet.
            if (player.CharacterClass is CharacterClassNecromancer &&
                player.IsShade &&
                player.ControlledBrain is IControlledBrain controlledBrain &&
                controlledBrain.Body is GameLiving pet &&
                pet != null)
            {
                Spell petAdrenaline = SkillBase.GetSpellByID(AdrenalineSpellHandler.TANK_ADRENALINE_SPELL_ID);
                SpellLine adrenalineLine = SpellLine ?? SkillBase.GetSpellLine("Adrenaline");

                if (petAdrenaline != null && adrenalineLine != null)
                {
                    pet.CastSpell(petAdrenaline, adrenalineLine);
                }
            }
        }

        private static Spell ResolveAdrenalineSpell(GamePlayer player)
        {
            if (player.CharacterClass is CharacterClassBase ccb)
            {
                var dyn = ccb.GetAdrenalineSpell(player);
                if (dyn != null) return dyn;

                if (ccb.AdrenalineSpell != null) return ccb.AdrenalineSpell;
            }

            return player.CharacterClass.AdrenalineSpell;
        }
    }
}
