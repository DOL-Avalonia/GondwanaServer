using System;
using System.Text;
using System.Collections;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using System.Reflection;
using DOL.AI.Brain;
using DOL.Events;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    //http://www.camelotherald.com/masterlevels/ma.php?ml=Banelord
    //shared timer 1
    #region Banelord-1
    [SpellHandlerAttribute("CastingSpeedDebuff")]
    public class CastingSpeedDebuff : MasterlevelDebuffHandling
    {
        public override eProperty Property1 { get { return eProperty.CastingSpeed; } }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (!base.ApplyEffectOnTarget(target, effectiveness))
                return false;

            target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            return true;
        }

        // constructor
        public CastingSpeedDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.CastingSpeedDebuff.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //shared timer 5 for ml2 - shared timer 3 for ml8
    #region Banelord-2/8
    [SpellHandlerAttribute("PBAEDamage")]
    public class PBAEDamage : MasterlevelHandling
    {
        // constructor
        public PBAEDamage(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= PowerCost(target);
            //For Banelord ML 8, it drains Life from the Caster
            if (Spell.Damage > 0)
            {
                int chealth;
                chealth = (m_caster.Health * (int)Spell.Damage) / 100;

                if (m_caster.Health < chealth)
                    chealth = 0;

                m_caster.Health -= chealth;
            }
            base.FinishSpellCast(target, force);
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (!target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active) return false;

            GamePlayer player = target as GamePlayer;
            if (target is GamePlayer)
            {
                int mana;
                int health;
                int end;

                int value = (int)Spell.Value;
                mana = (player!.Mana * value) / 100;
                end = (player.Endurance * value) / 100;
                health = (player.Health * value) / 100;

                //You don't gain RPs from this Spell
                if (player.Health < health)
                    player.Health = 1;
                else
                    player.Health -= health;

                if (player.Mana < mana)
                    player.Mana = 1;
                else
                    player.Mana -= mana;

                if (player.Endurance < end)
                    player.Endurance = 1;
                else
                    player.Endurance -= end;

                GameSpellEffect effect2 = SpellHandler.FindEffectOnTarget(target, "Mesmerize");
                if (effect2 != null)
                {
                    effect2.Cancel(true);
                    return true;
                }
                SendEffectAnimation(player, 0, false, 1);
                player.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            }
            return true;
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 25;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1;
            string mainDesc2;
            if (Spell.DamageType == 0) // ML2
            {
                mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.PBAEDamage.ML2.MainDescription", Spell.Value);
                mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.NbTargetAffected.MainDescription", Spell.TargetHardCap);
            }
            else // ML8
            {
                mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.PBAEDamage.ML8.MainDescription", Spell.Value, Spell.Damage);
                mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.NbTargetAffected.MainDescription", Spell.TargetHardCap);
            }

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion

    //shared timer 3
    #region Banelord-3
    [SpellHandlerAttribute("Oppression")]
    public class OppressionSpellHandler : MasterlevelHandling
    {
        public override bool IsOverwritable(GameSpellEffect compare)
        {
            return true;
        }
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target, force);
        }
        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }
        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            if (effect.Owner is GamePlayer)
                ((GamePlayer)effect.Owner).UpdateEncumberance();
            effect.Owner.StartInterruptTimer(effect.Owner.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect mezz = SpellHandler.FindEffectOnTarget(target, "Mesmerize");
            if (mezz != null)
                mezz.Cancel(false);
            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (effect.Owner is GamePlayer)
                ((GamePlayer)effect.Owner).UpdateEncumberance();
            return base.OnEffectExpires(effect, noMessages);
        }
        public OppressionSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.Oppression.MainDescription", Spell.Value);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.NbTargetAffected.MainDescription", Spell.TargetHardCap);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion

    //shared timer 1
    #region Banelord-4
    [SpellHandler("MLFatDebuff")]
    public class MLFatDebuffHandler : MasterlevelDebuffHandling
    {
        public override eProperty Property1 { get { return eProperty.FatigueConsumption; } }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            GameSpellEffect effect2 = SpellHandler.FindEffectOnTarget(target, "Mesmerize");
            if (effect2 != null)
            {
                effect2.Cancel(false);
                return true;
            }
            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            effect.Owner.StartInterruptTimer(effect.Owner.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            base.OnEffectStart(effect);
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        // constructor
        public MLFatDebuffHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.FatDebuff.MainDescription", Spell.Value);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.NbTargetAffected.MainDescription", Spell.TargetHardCap);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion

    //shared timer 5
    #region Banelord-5
    [SpellHandlerAttribute("MissHit")]
    public class MissHit : MasterlevelBuffHandling
    {
        public override eProperty Property1 { get { return eProperty.MissHit; } }

        // constructor
        public MissHit(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.MissHit.MainDescription", Spell.Value);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.NbTargetAffected.MainDescription", Spell.TargetHardCap);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion

    //shared timer 1
    #region Banelord-6
    #region ML6Snare
    [SpellHandler("MLUnbreakableSnare")]
    public class MLUnbreakableSnare : BanelordSnare
    {
        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            int duration = Spell.Duration;
            if (duration < 1)
                duration = 1;
            else if (duration > (Spell.Duration * 4))
                duration = (Spell.Duration * 4);
            return duration;
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        public MLUnbreakableSnare(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.MLUnbreakableSnare.MainDescription", Spell.Value);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.NbTargetAffected.MainDescription", Spell.TargetHardCap);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion
    #region ML6Stun
    [SpellHandler("UnrresistableNonImunityStun")]
    public class UnrresistableNonImunityStun : MasterlevelHandling
    {
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target, force);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.HasAbility(Abilities.CCImmunity) || target.HasAbility(Abilities.StunImmunity))
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DamageImmunity", target.Name), eChatType.CT_SpellResisted);
                return true;
            }

            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            effect.Owner.IsStunned = true;
            effect.Owner.StopAttack();
            effect.Owner.StopCurrentSpellcast();
            effect.Owner.DisableTurning(true);

            SendEffectAnimation(effect.Owner, 0, false, 1);

            MessageToLiving(effect.Owner, Spell.Message1, eChatType.CT_Spell);
            MessageToCaster(Util.MakeSentence(Spell.Message2, m_caster.GetPersonalizedName(effect.Owner)), eChatType.CT_Spell);
            foreach (GamePlayer player1 in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (!(effect.Owner == player1 || m_caster == player1))
                {
                    player1.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message2,
                        player1.GetPersonalizedName(effect.Owner)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }

            GamePlayer player = effect.Owner as GamePlayer;
            if (player != null)
            {
                player.Client.Out.SendUpdateMaxSpeed();
                if (player.Group != null)
                    player.Group.UpdateMember(player, false, false);
            }
            else
            {
                effect.Owner.StopAttack();
            }

            base.OnEffectStart(effect);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            effect.Owner.IsStunned = false;
            effect.Owner.DisableTurning(false);

            if (effect.Owner == null) return 0;

            GamePlayer player = effect.Owner as GamePlayer;

            if (player != null)
            {
                player.Client.Out.SendUpdateMaxSpeed();
                if (player.Group != null)
                    player.Group.UpdateMember(player, false, false);
            }
            else
            {
                GameNPC npc = effect.Owner as GameNPC;
                if (npc != null)
                {
                    IOldAggressiveBrain aggroBrain = npc.Brain as IOldAggressiveBrain;
                    if (aggroBrain != null)
                        aggroBrain.AddToAggroList(Caster, 1);
                }
            }
            return 0;
        }

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            return Spell.Duration;
        }

        public override bool IsOverwritable(GameSpellEffect compare)
        {
            if (Spell.EffectGroup != 0 || compare.Spell.EffectGroup != 0)
                return Spell.EffectGroup == compare.Spell.EffectGroup;
            if (compare.Spell.SpellType == "UnrresistableNonImunityStun") return true;
            return base.IsOverwritable(compare);
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            return 0;
        }

        public override bool HasPositiveEffect
        {
            get
            {
                return false;
            }
        }

        public UnrresistableNonImunityStun(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line)
        {
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.UnrresistableNonImunityStun.MainDescription", Spell.Duration);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion
    #endregion

    //shared timer 3
    #region Banelord-7
    [SpellHandlerAttribute("BLToHit")]
    public class BLToHit : MasterlevelBuffHandling
    {
        public override eProperty Property1 { get { return eProperty.ToHitBonus; } }

        // constructor
        public BLToHit(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.ToHitBuff.MainDescription", Spell.Value);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.NbTargetAffected.MainDescription", Spell.TargetHardCap);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc1 + "\n\n" + mainDesc2 + "\n\n" + secondDesc;
            }

            return mainDesc1 + "\n\n" + mainDesc2;
        }
    }
    #endregion

    //shared timer 5
    #region Banelord-9
    [SpellHandler("EffectivenessDebuff")]
    public class EffectivenessDeBuff : MasterlevelHandling
    {
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target, force);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            effect.Owner.DebuffCategory[eProperty.LivingEffectiveness] += (int)Spell.Value;
            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Out.SendStatusUpdate();
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);
            effect.Owner.DebuffCategory[eProperty.LivingEffectiveness] -= (int)Spell.Value;
            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Out.SendStatusUpdate();
            }
            return 0;
        }

        public EffectivenessDeBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.EffectivenessDebuff.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion

    //no shared timer
    #region Banelord-10
    [SpellHandlerAttribute("Banespike")]
    public class BanespikeHandler : MasterlevelBuffHandling
    {
        public override eProperty Property1 { get { return eProperty.MeleeDamage; } }

        public BanespikeHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Banelord.Banespike.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
    #endregion
}

#region MisshitCalc

namespace DOL.GS.PropertyCalc
{
    /// <summary>
    /// The melee damage bonus percent calculator
    ///
    /// BuffBonusCategory1 is used for buffs
    /// BuffBonusCategory2 unused
    /// BuffBonusCategory3 is used for debuff
    /// BuffBonusCategory4 unused
    /// BuffBonusMultCategory1 unused
    /// </summary>
    [PropertyCalculator(eProperty.MissHit)]
    public class MissHitPercentCalculator : PropertyCalculator
    {
        public override int CalcValue(GameLiving living, eProperty property)
        {
            return (int)(
                +living.BaseBuffBonusCategory[(int)property]
                + living.SpecBuffBonusCategory[(int)property]
                - living.DebuffCategory[(int)property]
                + living.BuffBonusCategory4[(int)property]);
        }
    }
}

#endregion