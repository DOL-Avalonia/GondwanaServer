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
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("DamageSpeedDecrease")]
    public class DamageSpeedDecreaseSpellHandler : SpeedDecreaseSpellHandler
    {
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            // do damage even if immune to duration effect
            if (!OnDirectEffect(target, effectiveness))
            {
                return false;
            }

            if (target is Keeps.GameKeepDoor or Keeps.GameKeepComponent)
            {
                return true;
            }
            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (!base.OnDirectEffect(target, effectiveness))
                return false;
            
            // calc damage
            AttackData ad = CalculateDamageToTarget(target, effectiveness);

            // Attacked living may modify the attack data.
            ad.Target.ModifyAttack(ad);

            SendDamageMessages(ad);
            DamageTarget(ad, true);
            if (Spell.LifeDrainReturn != 0)
                StealLife(ad);
            return true;
        }

        public virtual bool StealLife(AttackData ad)
        {
            if (ad == null) return false;
            if (!m_caster.IsAlive) return false;

            if (ad.Target is Keeps.GameKeepDoor || ad.Target is Keeps.GameKeepComponent)
            {
                return false;
            }

            int heal = (ad.Damage + ad.CriticalDamage) * m_spell.LifeDrainReturn / 100;
            int totalHealReductionPercentage = 0;

            if (m_caster.IsDiseased)
            {
                int amnesiaChance = m_caster.TempProperties.getProperty<int>("AmnesiaChance", 50);
                int healReductionPercentage = amnesiaChance > 0 ? amnesiaChance : 50;
                totalHealReductionPercentage += healReductionPercentage;
                if (m_caster.Health < m_caster.MaxHealth && totalHealReductionPercentage < 100)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "Spell.LifeTransfer.TargetDiseased", healReductionPercentage), eChatType.CT_SpellResisted);
                }
            }

            foreach (GameSpellEffect effect in m_caster.EffectList)
            {
                if (effect.SpellHandler is HealDebuffSpellHandler)
                {
                    int debuffValue = (int)effect.Spell.Value;
                    int debuffEffectivenessBonus = 0;

                    if (m_caster is GamePlayer gamePlayer)
                    {
                        debuffEffectivenessBonus = gamePlayer.GetModified(eProperty.DebuffEffectivness);
                    }

                    int adjustedDebuffValue = debuffValue + (debuffValue * debuffEffectivenessBonus) / 100;
                    totalHealReductionPercentage += adjustedDebuffValue;
                    if (m_caster.Health < m_caster.MaxHealth && totalHealReductionPercentage < 100)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.HealSpell.HealingReduced", adjustedDebuffValue), eChatType.CT_SpellResisted);
                    }
                }
            }

            if (totalHealReductionPercentage >= 100)
            {
                totalHealReductionPercentage = 100;
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.HealSpell.HealingNull"), eChatType.CT_SpellResisted);
            }

            if (totalHealReductionPercentage > 0)
            {
                heal -= (heal * totalHealReductionPercentage) / 100;
            }

            if (SpellHandler.FindEffectOnTarget(m_caster, "Damnation") != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "Damnation.Self.CannotBeHealed"), eChatType.CT_SpellResisted);
                heal = 0;
            }

            if (heal <= 0) return true;
            heal = m_caster.ChangeHealth(m_caster, GameLiving.eHealthChangeType.Spell, heal);

            if (heal > 0)
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DamageSpeedDecrease.LifeSteal", heal, (heal == 1 ? "." : "s.")), eChatType.CT_Spell);
            }
            else
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DamageSpeedDecrease.NoMoreLife"), eChatType.CT_SpellResisted);
            }
            return true;
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);
            return 0;
        }

        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            int duration = CalculateEffectDuration(target, effectiveness);
            return new GameSpellEffect(this, duration, 0, effectiveness);
        }

        public override IList<string> DelveInfo
        {
            get
            {
                /*
				<Begin Info: Lesser Constricting Jolt>
				Function: damage/speed decrease

				Target is damaged, and also moves slower for the spell's duration.

				Speed decrease: 35%
				Damage: 64
				Target: Targetted
				Range: 1500
				Duration: 30 sec
				Power cost: 10
				Casting time:      3.0 sec
				Damage: Matter

				<End Info>
				*/

                var list = new List<string>();
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DamageSpeedDecrease.DelveInfo.Function"));
                list.Add(" "); //empty line
                list.Add(Spell.Description);
                list.Add(" "); //empty line
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DamageSpeedDecrease.DelveInfo.Decrease", Spell.Value));
                if (Spell.Damage != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Damage", Spell.Damage.ToString("0.###;0.###'%'")));
                if (Spell.LifeDrainReturn != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.HealthReturned", Spell.LifeDrainReturn));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Target", Spell.Target));
                if (Spell.Range != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Range", Spell.Range));
                if (Spell.Duration >= ushort.MaxValue * 1000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + " Permanent.");
                else if (Spell.Duration > 60000)
                    list.Add(string.Format(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + Spell.Duration / 60000 + ":" + (Spell.Duration % 60000 / 1000).ToString("00") + " min"));
                else if (Spell.Duration != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
                if (Spell.Frequency != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Frequency", (Spell.Frequency * 0.001).ToString("0.0")));
                if (Spell.Power != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.PowerCost", Spell.Power.ToString("0;0'%'")));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.CastingTime", (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'")));
                if (Spell.RecastDelay > 60000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.RecastTime") + (Spell.RecastDelay / 60000).ToString() + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
                else if (Spell.RecastDelay > 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.RecastTime") + (Spell.RecastDelay / 1000).ToString() + " sec");
                if (Spell.Concentration != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.ConcentrationCost", Spell.Concentration));
                if (Spell.Radius != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Radius", Spell.Radius));
                if (Spell.DamageType != eDamageType.Natural)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Damage", GlobalConstants.DamageTypeToName(Spell.DamageType)));

                return list;
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            string damageTypeName = LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType);
            string description = LanguageMgr.GetTranslation(language, "SpellDescription.DamageSpeedDecrease.MainDescription", Spell.Value, Spell.Damage, damageTypeName);

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

            return description;
        }

        public DamageSpeedDecreaseSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
