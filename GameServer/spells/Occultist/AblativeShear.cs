using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;

namespace DOL.GS.Spells
{
    [SpellHandler("AblativeShear")]
    public class AblativeShear : SpellHandler
    {
        // This array contains each Ablative SpellHandler we want to remove.
        // If you add another ablative type in the future, just include it here.
        private static readonly Type[] AblativeTypes =
        {
            typeof(AblativeArmorSpellHandler),
            typeof(MagicAblativeArmorSpellHandler),
            typeof(BothAblativeArmorSpellHandler)
        };

        public AblativeShear(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line) { }

        /// <summary>
        /// Deducts the power cost after the normal cast completes
        /// </summary>
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        /// <summary>
        /// Executes the shear on the target, specifically removing
        /// any Ablative-style buff/effect (Melee, Magic, or Both).
        /// </summary>
        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (!base.OnDirectEffect(target, effectiveness))
                return false;
            if (target == null || !target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active)
                return false;

            target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            if (target is GameNPC npc)
            {
                if (npc.Brain is IOldAggressiveBrain aggroBrain) aggroBrain.AddToAggroList(Caster, 1);
            }

            bool foundAblative = false;

            foreach (GameSpellEffect effect in target.EffectList.GetAllOfType<GameSpellEffect>())
            {
                Type handlerType = effect.SpellHandler?.GetType();
                if (handlerType == null)
                    continue;

                foreach (Type ablativeType in AblativeTypes)
                {
                    if (handlerType == ablativeType)
                    {
                        foundAblative = true;
                        SendEffectAnimation(target, 0, false, 1);

                        effect.Cancel(false);
                    }
                }
            }

            if (foundAblative)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.AblativeShear.ShearSuccess"), eChatType.CT_Spell);
                MessageToLiving(target,LanguageMgr.GetTranslation((target as GamePlayer)?.Client, "SpellHandler.AblativeShear.EnhancingMagicRipped"), eChatType.CT_Spell);
            }
            else
            {
                SendEffectAnimation(target, 0, false, 0);
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.AblativeShear.NoEnhancementFound"), eChatType.CT_SpellResisted);
            }

            return foundAblative;
        }

        /// <summary>
        /// Handle the case where spell is resisted
        /// </summary>
        public override void OnSpellResisted(GameLiving target)
        {
            base.OnSpellResisted(target);
            if (Spell.Damage == 0 && Spell.CastTime == 0)
            {
                target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            }
        }

        /// <summary>
        /// Optionally describe the shear for delve info
        /// </summary>
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.BuffShear.Ablative");
        }
    }
}
