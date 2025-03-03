using System;
using System.Collections;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using Google.Protobuf.WellKnownTypes;

namespace DOL.GS.Spells
{

    [SpellHandlerAttribute("HereticDamageOverTime")]
    public class HereticDoTSpellHandler : HereticPiercingMagic
    {

        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }


        public override double GetLevelModFactor()
        {
            return 0;
        }


        public override bool IsOverwritable(GameSpellEffect compare)
        {
            if (Spell.EffectGroup != 0 || compare.Spell.EffectGroup != 0)
                return Spell.EffectGroup == compare.Spell.EffectGroup;
            if (base.IsOverwritable(compare) == false) return false;
            if (compare.Spell.Duration != Spell.Duration) return false;
            return true;
        }


        public override AttackData CalculateDamageToTarget(GameLiving target, double effectiveness)
        {
            AttackData ad = base.CalculateDamageToTarget(target, effectiveness);
            int bonus = Caster.GetModified(eProperty.DotDamageBonus);
            int critChance = Caster.GetModified(eProperty.CriticalDotHitChance);

            if (bonus != 0)
            {
                ad.Damage += (int)Math.Ceiling(ad.Damage * 0.01 * bonus);
            }
            
            if (Util.Chance(critChance) && (ad.Damage >= 1))
            {
                int critMax = (ad.Target is GamePlayer) ? ad.Damage / 2 : ad.Damage;
                ad.CriticalDamage = Util.Random(ad.Damage / 10, critMax);
            }
            
            ad.AttackType = AttackData.eAttackType.DoT;
            return ad;
        }


        public override void CalculateDamageVariance(GameLiving target, out double min, out double max)
        {
            int speclevel = 1;
            if (m_caster is GamePlayer)
            {
                speclevel = ((GamePlayer)m_caster).GetModifiedSpecLevel(m_spellLine.Spec);
            }
            min = 1;
            max = 1;

            if (target.Level > 0)
            {
                min = 0.5 + (speclevel - 1) / (double)target.Level * 0.5;
            }

            if (speclevel - 1 > target.Level)
            {
                double overspecBonus = (speclevel - 1 - target.Level) * 0.005;
                min += overspecBonus;
                max += overspecBonus;
            }

            if (min > max) min = max;
            if (min < 0) min = 0;
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.EffectList.GetOfType<AdrenalineSpellEffect>() != null)
            {
                (m_caster as GamePlayer)?.SendTranslatedMessage("Adrenaline.Target.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                (target as GamePlayer)?.SendTranslatedMessage("Adrenaline.Self.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return true;
            }
            if (!base.ApplyEffectOnTarget(target, effectiveness))
                return false;
            target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            return true;
        }


        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            int duration = m_spell.Duration;
            int reduction = target.GetModified(eProperty.DotDurationDecrease);

            if (target is GamePlayer { Guild: not null } targetPlayer)
            {
                reduction += targetPlayer.Guild.GetDebuffDurationReduction(this);
            }

            if (reduction != 0)
                duration -= (int)Math.Round(0.01 * reduction * duration);

            if (duration < 1)
                duration = 1;
            
            // damage is not reduced with distance
            //return new GameSpellEffect(this, m_spell.Duration*10-1, m_spellLine.IsBaseLine ? 3000 : 2000, 1);
            return new GameSpellEffect(this, duration, m_spellLine.IsBaseLine ? 3000 : 2000, 1);
        }


        public override void OnEffectStart(GameSpellEffect effect)
        {
            SendEffectAnimation(effect.Owner, 0, false, 1);
        }


        public override void OnEffectPulse(GameSpellEffect effect)
        {
            if (!m_caster.IsAlive || !effect.Owner.IsAlive || m_caster.Mana < Spell.PulsePower || !m_caster.IsWithinRadius(effect.Owner, (int)(Spell.Range * m_caster.GetModified(eProperty.SpellRange) * 0.01)) || m_caster.IsMezzed || m_caster.IsStunned || (m_caster.TargetObject is GameLiving ? effect.Owner != m_caster.TargetObject as GameLiving : true))
            {
                effect.Cancel(false);
                return;
            }
            base.OnEffectPulse(effect);
            SendEffectAnimation(effect.Owner, 0, false, 1);
            // An acidic cloud surrounds you!
            MessageToLiving(effect.Owner, Spell.Message1, eChatType.CT_Spell);
            // {0} is surrounded by an acidic cloud!
            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (!(effect.Owner == player))
                {
                    player.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message2,
                        player.GetPersonalizedName(effect.Owner)), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                }
            }
            OnDirectEffect(effect.Owner, effect.Effectiveness);
        }


        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);
            if (!noMessages)
            {
                // The acidic mist around you dissipates.
                MessageToLiving(effect.Owner, Spell.Message3, eChatType.CT_SpellExpires);
                // The acidic mist around {0} dissipates.
                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player))
                    {
                        player.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message4,
                            player.GetPersonalizedName(effect.Owner)), eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            return 0;
        }


        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null) return false;
            if (!target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active) return false;

            // no interrupts on DoT direct effect
            // calc damage
            AttackData ad = CalculateDamageToTarget(target, effectiveness);

            // Attacked living may modify the attack data.
            ad.Target.ModifyAttack(ad);

            SendDamageMessages(ad);
            DamageTarget(ad);
            return true;
        }


        public virtual void DamageTarget(AttackData ad)
        {
            ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;
            ad.Target.OnAttackedByEnemy(ad);
            ad.Attacker.DealDamage(ad);
            foreach (GamePlayer player in ad.Attacker.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendCombatAnimation(null, ad.Target, 0, 0, 0, 0, 0x0A, ad.Target.HealthPercent);
            }
        }


        public HereticDoTSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
