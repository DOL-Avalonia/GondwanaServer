using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("Demi")]
    public class DemiSpellHandler : SpellHandler
    {
        public DemiSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            AttackData ad = new AttackData();
            ad.Attacker = Caster;
            ad.Target = target;
            ad.AttackType = AttackData.eAttackType.Spell;
            ad.SpellHandler = this;
            ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;
            ad.IsSpellResisted = false;
            if (target.HealthPercent > 50)
            {
                ad.Damage = target.MaxHealth / 2 - (target.MaxHealth - target.Health);

                m_lastAttackData = ad;
                SendDamageMessages(ad);
                target.StartInterruptTimer(target.SpellInterruptDuration, ad.AttackType, Caster);
            }
            else
            {
                // Treat non-damaging effects as attacks to trigger an immediate response and BAF
                m_lastAttackData = ad;
                IOldAggressiveBrain aggroBrain = (ad.Target is GameNPC) ? ((GameNPC)ad.Target).Brain as IOldAggressiveBrain : null;
                if (aggroBrain != null)
                    aggroBrain.AddToAggroList(Caster, 1);
            }
            DamageTarget(ad, true);
            return true;
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            if (Spell.AmnesiaChance > 0 && target.Level > Spell.AmnesiaChance)
                return 100;

            var ResistChanceFactor = 2.6;
            if (DeathClawSpellHandler.IsResistingEntity(target))
                return base.CalculateSpellResistChance(target) * (int)ResistChanceFactor;

            return base.CalculateSpellResistChance(target);
        }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc;

            if (Spell.AmnesiaChance > 0)
            {
                string shortPart1 = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Demi.MainDescription", Spell.Name);
                string shortPart2 = LanguageMgr.GetTranslation(delveClient, "SpellDescription.DemiQuarter.AmnesiaDescription", Spell.AmnesiaChance);

                mainDesc = shortPart1 + " " + shortPart2;
            }
            else
            {
                mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Demi.MainDescription", Spell.Name);
            }

            string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.DemiQuarter.EndDescription");

            if (Spell.RecastDelay > 0)
            {
                string thirdDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc + "\n\n" + thirdDesc;
            }

            return mainDesc + "\n\n" + secondDesc;
        }
    }
}