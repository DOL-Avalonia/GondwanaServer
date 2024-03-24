
using DOL.Database;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using System;

namespace DOL.GS.Spells
{
    public class TensionBuff : SingleStatBuff
    {
        /// <inheritdoc />
        public TensionBuff(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        /// <inheritdoc />
        public override eProperty Property1
        {
            get => eProperty.MythicalTension;
        }

        /// <inheritdoc />
        public override eBuffBonusCategory BonusCategory1
        {
            get => eBuffBonusCategory.Other;
        }
    }
}
