/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.Collections.Generic;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.PlayerClass;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    public abstract class SingleStatDebuff : SingleStatBuff
    {
        public override eBuffBonusCategory BonusCategory1 { get { return eBuffBonusCategory.Debuff; } }

        /// <inheritdoc />
        public override bool HasPositiveEffect => false;

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            int totalResistChance = 0;

            // 1. DebuffImmunity
            var immunityEffect = SpellHandler.FindEffectOnTarget(target, "DebuffImmunity") as DebuffImmunityEffect;
            if (immunityEffect != null)
            {
                totalResistChance += immunityEffect.AdditionalResistChance;
            }

            // 2. MythicalDebuffResistChance
            int mythicalResistChance = 0;
            if (target is GamePlayer gamePlayer)
            {
                mythicalResistChance = gamePlayer.GetModified(eProperty.MythicalDebuffResistChance);
                totalResistChance += mythicalResistChance;
            }

            // Apply the combined resist chance
            if (Util.Chance(totalResistChance))
            {
                (m_caster as GamePlayer)?.SendTranslatedMessage("DebuffImmunity.Target.Resisted", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                (target as GamePlayer)?.SendTranslatedMessage("DebuffImmunity.You.Resisted", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);

                SendSpellResistAnimation(target);

                return true;
            }

            if (target.EffectList.GetOfType<AdrenalineSpellEffect>() != null)
            {
                (m_caster as GamePlayer)?.SendTranslatedMessage("Adrenaline.Target.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                (target as GamePlayer)?.SendTranslatedMessage("Adrenaline.Self.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return true;
            }

            base.ApplyEffectOnTarget(target, effectiveness);

            if (target.Realm == 0 || Caster.Realm == 0)
            {
                target.LastAttackedByEnemyTickPvE = target.CurrentRegion.Time;
                Caster.LastAttackTickPvE = Caster.CurrentRegion.Time;
            }
            else
            {
                target.LastAttackedByEnemyTickPvP = target.CurrentRegion.Time;
                Caster.LastAttackTickPvP = Caster.CurrentRegion.Time;
            }
            if (target is GameNPC)
            {
                IOldAggressiveBrain aggroBrain = ((GameNPC)target).Brain as IOldAggressiveBrain;
                if (aggroBrain != null)
                    aggroBrain.AddToAggroList(Caster, (int)Spell.Value);
            }
            return true;
        }

        /// <inheritdoc />
        public override AttackData CalculateInitialAttack(GameLiving target, double effectiveness)
        {
            AttackData ad = base.CalculateInitialAttack(target, effectiveness);

            ad.TensionRate = 0.25;
            return ad;
        }

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = Spell.Duration;
            duration *= (1.0 + m_caster.GetModified(eProperty.SpellDuration) * 0.01);
            if (!(GameServer.ServerRules.IsPveOnlyBonus(eProperty.NegativeReduction) && GameServer.ServerRules.IsPvPAction(Caster, target)))
                duration *= (1.0 - target.GetModified(eProperty.NegativeReduction) * 0.01);
            duration -= duration * target.GetResist(Spell.DamageType) * 0.01;

            if (duration < 1)
                duration = 1;
            else if (duration > (Spell.Duration * 4))
                duration = (Spell.Duration * 4);


            if (target is GamePlayer { Guild: not null } targetPlayer)
            {
                int guildReduction = targetPlayer.Guild.GetDebuffDurationReduction(this);
                if (guildReduction != 0)
                    duration = (duration * (100 - Math.Min(100, guildReduction))) / 100;
            }
            return (int)duration;
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            int basechance = base.CalculateSpellResistChance(target);
            GameSpellEffect rampage = SpellHandler.FindEffectOnTarget(target, "Rampage");
            if (rampage != null)
            {
                basechance += (int)rampage.Spell.Value;
            }
            return Math.Min(100, basechance);
        }

        public SingleStatDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.SingleStatDebuff.MainDescription", LanguageMgr.GetProperty(delveClient, Property1), Spell.Value);
        }
    }

    [SpellHandler("StrengthDebuff")]
    public class StrengthDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.Strength; } }

        public StrengthDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("DexterityDebuff")]
    public class DexterityDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.Dexterity; } }

        public DexterityDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ConstitutionDebuff")]
    public class ConstitutionDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.Constitution; } }

        public ConstitutionDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ArmorFactorDebuff")]
    public class ArmorFactorDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.ArmorFactor; } }

        public ArmorFactorDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ArmorAbsorptionDebuff")]
    public class ArmorAbsorptionDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.ArmorAbsorption; } }

        protected override void SendUpdates(GameLiving target) { }

        public ArmorAbsorptionDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("CombatSpeedDebuff")]
    public class CombatSpeedDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.MeleeSpeed; } }

        protected override void SendUpdates(GameLiving target) { }

        public CombatSpeedDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.CombatSpeedDebuff.MainDescription", Math.Abs(Spell.Value));
        }
    }

    [SpellHandler("MeleeDamageDebuff")]
    public class MeleeDamageDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.MeleeDamage; } }

        protected override void SendUpdates(GameLiving target) { }

        public MeleeDamageDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.MeleeDamageDebuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("FatigueConsumptionDebuff")]
    public class FatigueConsumptionDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.FatigueConsumption; } }

        protected override void SendUpdates(GameLiving target) { }

        public FatigueConsumptionDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.FatigueConsumptionDebuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("FumbleChanceDebuff")]
    public class FumbleChanceDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.FumbleChance; } }

        protected override void SendUpdates(GameLiving target) { }

        public FumbleChanceDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.FumbleChanceDebuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("DPSDebuff")]
    public class DPSDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.DPS; } }

        protected override void SendUpdates(GameLiving target) { }

        public DPSDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.DPSDebuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("StealthSkillDebuff")]
    public class StealthSkillDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.Skill_Stealth; } }

        protected override void SendUpdates(GameLiving target)
        {
            base.SendUpdates(target);

            if (target is GamePlayer player)
            {
                player.Out.SendUpdatePlayerSkills();
            }
        }

        public StealthSkillDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.StealthSkillDebuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("AllSkillsDebuff")]
    public class AllSkillsDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.AllSkills; } }

        protected override void SendUpdates(GameLiving target)
        {
            base.SendUpdates(target);

            if (target is GamePlayer player)
            {
                player.Out.SendUpdatePlayerSkills();
            }
        }

        public AllSkillsDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.SkillsDebuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("WeaponSkillDebuff")]
    public class WeaponSkillDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.WeaponSkill; } }

        public WeaponSkillDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string description = LanguageMgr.GetTranslation(delveClient, "SpellDescription.WeaponSkillDebuff.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string thirdDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return description + "\n\n" + thirdDesc;
            }

            return description;
        }
    }

    [SpellHandler("AcuityDebuff")]
    public class AcuityDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.Acuity; } }

        public AcuityDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("QuicknessDebuff")]
    public class QuicknessDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.Quickness; } }

        public QuicknessDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ToHitDebuff")]
    public class ToHitSkillDebuff : SingleStatDebuff
    {
        public override eProperty Property1 { get { return eProperty.ToHitBonus; } }

        public ToHitSkillDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
