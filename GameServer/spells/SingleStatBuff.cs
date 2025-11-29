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
using System.Reflection;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;

namespace DOL.GS.Spells
{
    public abstract class SingleStatBuff : PropertyChangingSpell
    {
        public override eBuffBonusCategory BonusCategory1 { get { return eBuffBonusCategory.BaseBuff; } }

        protected override void SendUpdates(GameLiving target)
        {
            target.UpdateHealthManaEndu();
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            int specLevel = Caster.GetModifiedSpecLevel(m_spellLine.Spec);

            if (Spell.Level > 0)
            {
                if (Caster is GamePlayer playerCaster)
                {
                    if (playerCaster.CharacterClass.ClassType != eClassType.ListCaster && playerCaster.CharacterClass.ID != (int)eCharacterClass.Savage)
                    {
                        effectiveness = 0.75; // This section is for self buffs, cleric buffs etc.
                        effectiveness += (specLevel - 1.0) * 0.5 / Spell.Level;
                        effectiveness = Math.Max(0.75, effectiveness);
                        effectiveness = Math.Min(1.25, effectiveness);
                    }
                    else if (Spell.IsHarmful)
                    {
                        if (playerCaster.CharacterClass.ClassType == eClassType.ListCaster)
                        {
                            effectiveness = 0.75; // This section is for list casters stat debuffs.
                            effectiveness += (specLevel - 1.0) * 0.5 / Spell.Level;
                            effectiveness = Math.Max(0.75, effectiveness);
                            effectiveness = Math.Min(1.25, effectiveness);
                        }
                        else
                        {
                            effectiveness = 1.0; // Non list casters debuffs. Reaver curses, Champ debuffs etc.
                        }
                    }
                    else
                    {
                        effectiveness = 1.0;
                    }
                }
                else
                {
                    // apply the basic formula for any NPC spell
                    effectiveness += (specLevel - 1.0) * 0.5 / Spell.Level;
                    effectiveness = Math.Max(0.75, effectiveness);
                    effectiveness = Math.Min(1.25, effectiveness);
                }
            }
            else
            {
                effectiveness = 1.0;
            }
            if (Spell.IsHarmful)
            {
                effectiveness *= (1.0 + Caster.GetModified(eProperty.DebuffEffectivness) * 0.01);
            }
            else
            {
                effectiveness *= (1.0 + Caster.GetModified(eProperty.BuffEffectiveness) * 0.01);
            }

            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override bool IsOverwritable(GameSpellEffect compare)
        {
            if (Spell.EffectGroup != 0 || compare.Spell.EffectGroup != 0)
                return Spell.EffectGroup == compare.Spell.EffectGroup;
            if (base.IsOverwritable(compare) == false) return false;
            if (Spell.Duration > 0 && compare.Concentration > 0)
                return compare.Spell.Value >= Spell.Value;
            return compare.SpellHandler.SpellLine.IsBaseLine ==
                SpellLine.IsBaseLine;
        }

        protected SingleStatBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.SingleStatBuff.MainDescription", LanguageMgr.GetProperty(delveClient, Property1), Spell.Value);
        }
    }

    [SpellHandler("StrengthBuff")]
    public class StrengthBuff : SingleStatBuff
    {
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.HasAbility(Abilities.VampiirStrength))
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Silence.AlreadyAffected"), eChatType.CT_Spell);
                return false;
            }
            return base.ApplyEffectOnTarget(target, effectiveness);
        }
        public override eProperty Property1 { get { return eProperty.Strength; } }

        public StrengthBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("DexterityBuff")]
    public class DexterityBuff : SingleStatBuff
    {
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.HasAbility(Abilities.VampiirDexterity))
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Silence.AlreadyAffected"), eChatType.CT_Spell);
                return false;
            }
            return base.ApplyEffectOnTarget(target, effectiveness);
        }
        public override eProperty Property1 { get { return eProperty.Dexterity; } }

        public DexterityBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ConstitutionBuff")]
    public class ConstitutionBuff : SingleStatBuff
    {
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.HasAbility(Abilities.VampiirConstitution))
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Silence.AlreadyAffected"), eChatType.CT_Spell);
                return false;
            }
            return base.ApplyEffectOnTarget(target, effectiveness);
        }
        public override eProperty Property1 { get { return eProperty.Constitution; } }

        public ConstitutionBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("AcuityBuff")]
    public class AcuityBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.Acuity; } }

        public AcuityBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("QuicknessBuff")]
    public class QuicknessBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.Quickness; } }

        public QuicknessBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ArmorFactorBuff")]
    public class ArmorFactorBuff : SingleStatBuff
    {
        public override eBuffBonusCategory BonusCategory1
        {
            get
            {
                if (Spell.Target.Equals("self", StringComparison.OrdinalIgnoreCase)) return eBuffBonusCategory.Other; // no caps for self buffs
                if (m_spellLine.IsBaseLine) return eBuffBonusCategory.BaseBuff; // baseline cap
                return eBuffBonusCategory.Other; // no caps for spec line buffs
            }
        }
        public override eProperty Property1 { get { return eProperty.ArmorFactor; } }

        public ArmorFactorBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ArmorAbsorptionBuff")]
    public class ArmorAbsorptionBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.ArmorAbsorption; } }

        protected override void SendUpdates(GameLiving target) { }

        public ArmorAbsorptionBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("CombatSpeedBuff")]
    public class CombatSpeedBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.MeleeSpeed; } }

        protected override void SendUpdates(GameLiving target) { }

        public CombatSpeedBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.CombatSpeedBuff.MainDescription", Math.Abs(Spell.Value));
        }
    }

    [SpellHandler("HasteBuff")]
    public class HasteBuff : CombatSpeedBuff
    {
        public HasteBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("CelerityBuff")]
    public class CelerityBuff : CombatSpeedBuff
    {
        public CelerityBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.CombatSpeedBuff.MainDescription", Math.Abs(Spell.Value));
        }
    }

    [SpellHandler("FatigueConsumptionBuff")]
    public class FatigueConsumptionBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.FatigueConsumption; } }

        protected override void SendUpdates(GameLiving target) { }

        public FatigueConsumptionBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.FatigueConsumptionBuff.MainDescription", Math.Abs(Spell.Value));
        }
    }

    [SpellHandler("MeleeDamageBuff")]
    public class MeleeDamageBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.MeleeDamage; } }

        protected override void SendUpdates(GameLiving target) { }

        public MeleeDamageBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.MeleeDamageBuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("MesmerizeDurationBuff")]
    public class MesmerizeDurationBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.MesmerizeDuration; } }

        protected override void SendUpdates(GameLiving target) { }

        public MesmerizeDurationBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient) => LanguageMgr.GetTranslation(delveClient, "SpellDescription.MesmerizeDurationBuff.MainDescription", Spell.Value);
    }

    [SpellHandler("DPSBuff")]
    public class DPSBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.DPS; } }

        protected override void SendUpdates(GameLiving target) { }

        public DPSBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.FatigueConsumptionBuff.MainDescription", Math.Abs(Spell.Value));
        }
    }

    [SpellHandler("EvadeBuff")]
    public class EvadeChanceBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.EvadeChance; } }

        protected override void SendUpdates(GameLiving target) { }

        public EvadeChanceBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
        
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.EvadeChanceBuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("ParryBuff")]
    public class ParryChanceBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.ParryChance; } }

        protected override void SendUpdates(GameLiving target) { }

        public ParryChanceBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
        
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.ParryChanceBuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("WeaponSkillBuff")]
    public class WeaponSkillBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.WeaponSkill; } }

        protected override void SendUpdates(GameLiving target)
        {
            base.SendUpdates(target);

            if (target is GamePlayer player)
            {
                player.Out.SendUpdatePlayerSkills();
            }
        }

        public WeaponSkillBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string description = LanguageMgr.GetTranslation(delveClient, "SpellDescription.WeaponSkillBuff.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string thirdDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return description + "\n\n" + thirdDesc;
            }

            return description;
        }
    }

    [SpellHandler("StealthSkillBuff")]
    public class StealthSkillBuff : SingleStatBuff
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

        public StealthSkillBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.StealthSkillBuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("ToHitBuff")]
    public class ToHitSkillBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.ToHitBonus; } }

        public ToHitSkillBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("MagicResistsBuff")]
    public class MagicResistsBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.MagicAbsorption; } }

        public MagicResistsBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("StyleAbsorbBuff")]
    public class StyleAbsorbBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.StyleAbsorb; } }
        public StyleAbsorbBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ExtraHP")]
    public class ExtraHP : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.ExtraHP; } }
        public ExtraHP(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("PaladinArmorFactorBuff")]
    public class PaladinArmorFactorBuff : SingleStatBuff
    {
        public override eBuffBonusCategory BonusCategory1
        {
            get
            {
                if (Spell.Target == "self") return eBuffBonusCategory.Other; // no caps for self buffs
                if (m_spellLine.IsBaseLine) return eBuffBonusCategory.BaseBuff; // baseline cap
                return eBuffBonusCategory.Other; // no caps for spec line buffs
            }
        }

        public override eProperty Property1 { get { return eProperty.ArmorFactor; } }

        public PaladinArmorFactorBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("FlexibleSkillBuff")]
    public class FlexibleSkillBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.Skill_Flexible_Weapon; } }

        protected override void SendUpdates(GameLiving target)
        {
            base.SendUpdates(target);

            if (target is GamePlayer player)
            {
                player.Out.SendUpdatePlayerSkills();
            }
        }

        public FlexibleSkillBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.FlexibleSkillBuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("ResiPierceBuff")]
    public class ResiPierceBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.ResistPierce; } }
        public ResiPierceBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.ResiPierceBuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("CriticalMagicalBuff")]
    public class CriticalMagicalBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.CriticalSpellHitChance; } }

        public override eBuffBonusCategory BonusCategory1
        {
            get => eBuffBonusCategory.UncappedBuff;
        }

        public CriticalMagicalBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.CriticalMagicalBuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("CriticalMeleeBuff")]
    public class CriticalMeleeBuff : SingleStatBuff
    {
        public override eProperty Property1 { get { return eProperty.CriticalMeleeHitChance; } }

        public override eBuffBonusCategory BonusCategory1
        {
            get => eBuffBonusCategory.UncappedBuff;
        }

        public CriticalMeleeBuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.CriticalMeleeBuff.MainDescription", Spell.Value);
        }
    }
}