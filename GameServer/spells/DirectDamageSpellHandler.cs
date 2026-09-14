using System;
using System.Collections;
using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using DOL.GS.Keeps;
using DOL.Events;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    [SpellHandlerAttribute("DirectDamage")]
    public class DirectDamageSpellHandler : SpellHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        private bool m_castFailed = false;

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            if (!m_castFailed)
            {
                ConsumePower(target);
            }

            base.FinishSpellCast(target, force);
        }

        public override double CalculateDamageBase(GameLiving target)
        {
            // % damage procs
            if (Spell.Damage < 0)
            {
                double spellDamage = 0;

                // This equation is used to simulate live values - Tolakram
                spellDamage = (target.MaxHealth * -Spell.Damage * .01) / 2.5;

                if (spellDamage < 0)
                    spellDamage = 0;

                return spellDamage;
            }

            return base.CalculateDamageBase(target);
        }


        public override double DamageCap(double effectiveness)
        {
            if (Spell.Damage < 0)
            {
                return (m_spellTarget.MaxHealth * -Spell.Damage * .01) * 3.0 * effectiveness;
            }

            return base.DamageCap(effectiveness);
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null) return false;

            if (NeedsEndOfCastLosCheck(target) && StartEndOfCastLosCheck(target, effectiveness))
                return true;

            DealDamage(target, effectiveness);
            return true;
        }

        public override void OnEndOfCastLosCheck(GameLiving target, double effectiveness, LosCheckResponse response)
        {
            if (Caster == null || Caster.ObjectState != GameObject.eObjectState.Active)
                return;

            if (response == LosCheckResponse.False)
            {
                // single target spell whose target is hidden: tell the caster why nothing happened
                if (Spell.Target.ToLower() == "enemy" && Spell.Radius == 0 && Spell.Range != 0)
                    MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.CheckLOSPlayerToTarget.CantSeeTarget"), eChatType.CT_SpellResisted);
                return;
            }

            DealDamage(target, effectiveness);
            GameEventMgr.Notify(GameLivingEvent.CastFinished, m_caster, new CastingEventArgs(this, target, m_lastAttackData));
        }

        protected virtual void DealDamage(GameLiving target, double effectiveness)
        {
            if (!target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active)
                return;

            // calc damage
            AttackData ad = CalculateDamageToTarget(target, effectiveness);

            // Attacked living may modify the attack data.
            ad.Target.ModifyAttack(ad);

            SendDamageMessages(ad);
            DamageTarget(ad, true);
            target.StartInterruptTimer(target.SpellInterruptDuration, ad.AttackType, Caster);
        }

        public override void OnSpellResisted(GameLiving target)
        {
            if (NeedsResistLosCheck(target) && StartResistLosCheck(target))
                return; // the base class applies the resist once the client confirmed the target is in view

            base.OnSpellResisted(target);
        }

        public DirectDamageSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            string description = LanguageMgr.GetTranslation(language, "SpellDescription.DirectDamage.MainDescription", Spell.Damage, LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType));

            if (Spell.SubSpellID != 0)
            {
                Spell subSpell = SkillBase.GetSpellByID((int)Spell.SubSpellID);
                if (subSpell != null)
                {
                    ISpellHandler subSpellHandler = ScriptMgr.CreateSpellHandler(m_caster, subSpell, null);
                    if (subSpellHandler != null)
                    {
                        string subspelldesc = subSpellHandler.GetDelveDescription(delveClient);
                        description += "\n\n" + subspelldesc;
                    }
                }
            }

            if (Spell.IsSecondary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(language, "SpellDescription.Warlock.SecondarySpell");
                description += "\n\n" + secondaryMessage;
            }

            if (Spell.IsPrimary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(language, "SpellDescription.Warlock.PrimarySpell");
                description += "\n\n" + secondaryMessage;
            }

            switch (Spell.ID)
            {
                case 25075:
                case 25076:
                case 25077:
                case 25078:
                case 25079:
                    {
                        var viewer = delveClient?.Player;
                        if (viewer?.CharacterClass.ID == (int)eCharacterClass.Occultist)
                        {
                            description += "\n\n" + LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.ConditionDescription1");
                        }
                        break;
                    }
            }

            return description;
        }
    }
}
