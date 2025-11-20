using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS.Spells
{
    public abstract class AbstractMorphSpellHandler : SpellHandler
    {
        /// <inheritdoc />
        protected AbstractMorphSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        public int Priority
        {
            get;
            set;
        } = 0;

        public bool OverwritesMorphs
        {
            get;
            set;
        } = true;

        public virtual bool IgnoreForRiding { get; } = false;

        /// <inheritdoc />
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target is IllusionPet)
                return false;

            bool quiet = Spell.Radius > 0;
            if (target.IsStealthed)
            {
                if (!quiet)
                    ErrorTranslationToCaster("SpellHandler.TargetIsStealthed");
                return false;
            }

            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public virtual ushort GetModelFor(GameLiving living)
        {
            return (ushort)Spell.LifeDrainReturn;
        }

        /// <inheritdoc />
        public override bool IsOverwritable(GameSpellEffect compare)
        {
            if (compare.SpellHandler is AbstractMorphSpellHandler { OverwritesMorphs: true })
                return true;

            return base.IsOverwritable(compare);
        }

        public override bool PreventsApplication(GameSpellEffect self, GameSpellEffect other)
        {
            if (other.SpellHandler is AbstractMorphSpellHandler otherMorph)
            {
                if (this.Priority > otherMorph.Priority)
                    return true;
            }
            return base.PreventsApplication(self, other);
        }

        public override void OnBetterThan(GameLiving target, GameSpellEffect oldEffect, GameSpellEffect newEffect)
        {
            SpellHandler attempt = (SpellHandler)newEffect.SpellHandler;
            if (attempt.Caster.GetController() is GamePlayer player)
                player.SendTranslatedMessage("SpellHandler.Morphed.Target.Resist", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, player.GetPersonalizedName(target));
            attempt.SendSpellResistAnimation(target);
        }

        public override bool ShouldCancelOldEffect(GameSpellEffect oldeffect, GameSpellEffect neweffect)
        {
            if (neweffect.SpellHandler is AbstractMorphSpellHandler newMorph && oldeffect.SpellHandler is AbstractMorphSpellHandler oldMorph)
            {
                if (newMorph.Priority > oldMorph.Priority)
                    return true;

                if (newMorph.Priority < oldMorph.Priority)
                    return false;
            }

            return base.ShouldCancelOldEffect(oldeffect, neweffect);
        }

        /// <inheritdoc />
        public override bool ShouldOverwriteOldEffect(GameSpellEffect oldeffect, GameSpellEffect neweffect)
        {
            if (oldeffect.SpellHandler is AbstractMorphSpellHandler otherMorph && neweffect.SpellHandler is AbstractMorphSpellHandler newMorph)
            {
                if (otherMorph.Priority > newMorph.Priority)
                    return false;
                else if (otherMorph.Priority < newMorph.Priority)
                    return true;
            }

            if (oldeffect.SpellHandler is IllusionSpell otherIllu && otherIllu.Priority <= Priority)
                return true;

            return base.ShouldOverwriteOldEffect(oldeffect, neweffect);
        }

        /// <inheritdoc />
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            ushort model = GetModelFor(effect.Owner);

            if (this is not IllusionSpell)
            {
                effect.Owner.CancelEffects<IllusionSpell>();
            }

            if (model != 0)
            {
                if (effect.Owner is GamePlayer playerOwner)
                {
                    playerOwner.CharacterClass.CancelClassStates();
                }
                effect.Owner.Model = model;
            }
        }

        protected virtual void Unmorph(GameSpellEffect effect)
        {
            GameSpellEffect bestEffect = null;
            ushort bestModel = 0;

            if (this is not IllusionSpell)
            {
                effect.Owner.CancelEffects<IllusionSpell>();
            }

            foreach (var otherEffect in effect.Owner.FindEffectsOnTarget(typeof(AbstractMorphSpellHandler)))
            {
                if (otherEffect == effect)
                    continue;

                var morph = otherEffect.SpellHandler as AbstractMorphSpellHandler;
                var model = morph.GetModelFor(effect.Owner);
                if (bestEffect == null)
                {
                    bestEffect = otherEffect;
                    bestModel = model;
                    continue;
                }

                if (model != 0 && ShouldOverwriteOldEffect(bestEffect, otherEffect))
                {
                    bestEffect = otherEffect;
                    bestModel = model;
                }
            }

            if (bestModel == 0)
            {
                if (effect.Owner is GamePlayer playerOwner)
                {
                    bestModel = playerOwner.CreationModel;
                }
                else if (effect.Owner is GameNPC livingOwner)
                {
                    bestModel = livingOwner.ModelDb;
                }
            }

            if (bestModel != 0 && bestModel != effect.Owner.Model)
            {
                effect.Owner.Model = bestModel;
            }
        }

        /// <inheritdoc />
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (GetModelFor(effect.Owner) != 0)
            {
                Unmorph(effect);
            }
            return base.OnEffectExpires(effect, noMessages);
        }
    }

    public abstract class AbstractMorphOffensiveProc : OffensiveProcSpellHandler
    {
        /// <inheritdoc />
        protected AbstractMorphOffensiveProc(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line)
        {
        }
    }
}
