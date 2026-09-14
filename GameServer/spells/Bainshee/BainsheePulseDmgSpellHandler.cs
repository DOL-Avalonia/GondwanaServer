using System;
using System.Collections;
using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using DOL.GS.Keeps;
using DOL.Events;
using DOL.GS.Effects;
using DOL.Language;
using DOL.GS.ServerProperties;
namespace DOL.GS.Spells
{
    /// <summary>
    /// 
    /// </summary>
    [SpellHandlerAttribute("BainsheePulseDmg")]
    public class BainsheePulseDmgSpellHandler : SpellHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public const string FOCUS_WEAK = "FocusSpellHandler.Online";

        /// <summary>
        /// Execute direct damage spell
        /// </summary>
        /// <param name="target"></param>
        /// <param name="force"></param>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            if (Spell.Pulse != 0)
            {
                GameEventMgr.AddHandler(Caster, GamePlayerEvent.Moving, new DOLEventHandler(EventAction));
                GameEventMgr.AddHandler(Caster, GamePlayerEvent.Dying, new DOLEventHandler(EventAction));
            }
            ConsumePower(target);
            base.FinishSpellCast(target, force);
        }

        public override PulsingSpellEffect? CancelPulsingSpell(GameLiving living, string spellType)
        {
            lock (living.ConcentrationEffects)
            {
                for (int i = 0; i < living.ConcentrationEffects.Count; i++)
                {
                    PulsingSpellEffect effect = living.ConcentrationEffects[i] as PulsingSpellEffect;
                    if (effect == null)
                        continue;
                    if (effect.SpellHandler.Spell.SpellType == spellType)
                    {
                        effect.Cancel(false);
                        GameEventMgr.RemoveHandler(Caster, GamePlayerEvent.Moving, new DOLEventHandler(EventAction));
                        GameEventMgr.RemoveHandler(Caster, GamePlayerEvent.Dying, new DOLEventHandler(EventAction));
                        return effect;
                    }
                }
            }
            return null;
        }

        public void EventAction(DOLEvent e, object sender, EventArgs args)
        {
            GameLiving player = sender as GameLiving;

            if (player == null)
                return;

            if (Spell.Pulse != 0 && CancelPulsingSpell(Caster, Spell.SpellType) != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.CancelEffect"), eChatType.CT_Spell);
                return;
            }
        }

        /// <summary>
        /// execute direct effect
        /// </summary>
        /// <param name="target">target that gets the damage</param>
        /// <param name="effectiveness">factor from 0..1 (0%-100%)</param>
        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null)
                return false;

            // PBAoE pulse: each pulse asks (cached) whether the target is really in view before hurting it.
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
                return;

            DealDamage(target, effectiveness);
        }

        private void DealDamage(GameLiving target, double effectiveness)
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
                return;

            base.OnSpellResisted(target);
        }

        // constructor
        public BainsheePulseDmgSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string damageTypeName = LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType);
            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.BainsheePulseDmg.MainDescription", Spell.Damage, damageTypeName);

            if (Spell.Radius > 0)
            {
                string areaDesc = LanguageMgr.GetTranslation(language, "SpellDescription.BainsheePulseDmg.AreaDescription");
                mainDesc += "\n\n" + areaDesc;
            }

            string secondaryDesc = LanguageMgr.GetTranslation(language, "SpellDescription.BainsheePulseDmg.SecondaryDescription");
            mainDesc += "\n\n" + secondaryDesc;

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}
