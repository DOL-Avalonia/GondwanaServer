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
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using System.Collections.Generic;
using System.Numerics;

namespace DOL.GS.Spells
{
    public abstract class AbstractResistDebuff : PropertyChangingSpell
    {
        public abstract string DebuffTypeName { get; }

        public override eBuffBonusCategory BonusCategory1 { get { return eBuffBonusCategory.Debuff; } }

        /// <inheritdoc />
        public override bool HasPositiveEffect => false;

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = Spell.Duration;

            duration *= (1.0 + m_caster.GetModified(eProperty.SpellDuration) * 0.01);
            if (!(GameServer.ServerRules.IsPveOnlyBonus(eProperty.NegativeReduction) && GameServer.ServerRules.IsPvPAction(Caster, target)))
                duration *= (1.0 - target.GetModified(eProperty.NegativeReduction) * 0.01);
            duration -= duration * target.GetResist(m_spell.DamageType) * 0.01;

            if (target is GamePlayer { Guild: not null } targetPlayer)
            {
                int guildReduction = targetPlayer.Guild.GetDebuffDurationReduction(this);
                if (guildReduction != 0)
                    duration = (duration * (100 - Math.Min(100, guildReduction))) / 100;
            }
            if (duration < 1)
                duration = 1;
            else if (duration > (Spell.Duration * 4))
                duration = (Spell.Duration * 4);
            return (int)duration;
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            //TODO: correct effectiveness formula
            // invoke direct effect if not resisted for DD w/ debuff spells
            if (Caster is GamePlayer && Spell.Level > 0)
            {
                if (((GamePlayer)Caster).CharacterClass.ClassType == eClassType.ListCaster)
                {
                    int specLevel = Caster.GetModifiedSpecLevel(m_spellLine.Spec);
                    effectiveness = 0.75;
                    effectiveness += (specLevel - 1.0) * 0.5 / Spell.Level;
                    effectiveness = Math.Max(0.75, effectiveness);
                    effectiveness = Math.Min(1.25, effectiveness);
                    effectiveness *= (1.0 + m_caster.GetModified(eProperty.BuffEffectiveness) * 0.01);
                }
                else
                {
                    effectiveness = 1.0;
                    effectiveness *= (1.0 + m_caster.GetModified(eProperty.DebuffEffectivness) * 0.01);
                }
            }

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

            if (!base.ApplyEffectOnTarget(target, effectiveness))
                return false;

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
                    aggroBrain.AddToAggroList(Caster, 1);
            }
            if (Spell.CastTime > 0) target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            return true;
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

        protected override void SendUpdates(GameLiving target)
        {
            base.SendUpdates(target);
            if (target is GamePlayer)
            {
                GamePlayer player = (GamePlayer)target;
                player.Out.SendCharResistsUpdate();
            }
        }

        public override IList<string> DelveInfo
        {
            get
            {
                /*
				<Begin Info: Nullify Dissipation>
				Function: resistance decrease
 
				Decreases the target's resistance to the listed damage type.
 
				Resist decrease Energy: 15
				Target: Targetted
				Range: 1500
				Duration: 15 sec
				Power cost: 13
				Casting time:      2.0 sec
				Damage: Cold
 
				<End Info>
				 */

                var list = new List<string>();
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "ResistDebuff.DelveInfo.Function"));
                list.Add(" "); //empty line
                list.Add(Spell.Description);
                list.Add(" "); //empty line
                list.Add(String.Format(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "ResistDebuff.DelveInfo.Decrease", DebuffTypeName, m_spell.Value)));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Target", Spell.Target));
                if (Spell.Range != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Range", Spell.Range));
                if (Spell.Duration >= ushort.MaxValue * 1000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + " Permanent.");
                else if (Spell.Duration > 60000)
                    list.Add(string.Format(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + Spell.Duration / 60000 + ":" + (Spell.Duration % 60000 / 1000).ToString("00") + " min"));
                else if (Spell.Duration != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
                if (Spell.Power != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.PowerCost", Spell.Power.ToString("0;0'%'")));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.CastingTime", (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'")));
                if (Spell.RecastDelay > 60000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.RecastTime") + Spell.RecastDelay / 60000 + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
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

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            // Here we could also just use SingleStatDebuff translation and GetProperty, since the property's name should be "[damage] resistance"
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.ResistDebuff.MainDescription", LanguageMgr.GetResistOfType(delveClient, (eResist)Property1), Spell.Value);
        }

        public AbstractResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("BodyResistDebuff")]
    public class BodyResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Body; } }
        public override string DebuffTypeName { get { return "Body"; } }

        public BodyResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ColdResistDebuff")]
    public class ColdResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Cold; } }
        public override string DebuffTypeName { get { return "Cold"; } }

        public ColdResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("EnergyResistDebuff")]
    public class EnergyResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Energy; } }
        public override string DebuffTypeName { get { return "Energy"; } }

        public EnergyResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("HeatResistDebuff")]
    public class HeatResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Heat; } }
        public override string DebuffTypeName { get { return "Heat"; } }

        public HeatResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("MatterResistDebuff")]
    public class MatterResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Matter; } }
        public override string DebuffTypeName { get { return "Matter"; } }

        public MatterResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("SpiritResistDebuff")]
    public class SpiritResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Spirit; } }
        public override string DebuffTypeName { get { return "Spirit"; } }

        public SpiritResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("SlashResistDebuff")]
    public class SlashResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Slash; } }
        public override string DebuffTypeName { get { return "Slash"; } }

        public SlashResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("ThrustResistDebuff")]
    public class ThrustResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Thrust; } }
        public override string DebuffTypeName { get { return "Thrust"; } }

        public ThrustResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("CrushResistDebuff")]
    public class CrushResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Crush; } }
        public override string DebuffTypeName { get { return "Crush"; } }

        public CrushResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("CrushSlashThrustDebuff")]
    public class CrushSlashThrustDebuff : AbstractResistDebuff
    {
        public override eBuffBonusCategory BonusCategory1 { get { return eBuffBonusCategory.Debuff; } }
        public override eBuffBonusCategory BonusCategory2 { get { return eBuffBonusCategory.Debuff; } }
        public override eBuffBonusCategory BonusCategory3 { get { return eBuffBonusCategory.Debuff; } }

        public override eProperty Property1 { get { return eProperty.Resist_Crush; } }
        public override eProperty Property2 { get { return eProperty.Resist_Slash; } }
        public override eProperty Property3 { get { return eProperty.Resist_Thrust; } }

        public override string DebuffTypeName { get { return "Crush/Slash/Thrust"; } }

        public CrushSlashThrustDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.CrushSlashThrustDebuff.MainDescription", Spell.Value);
        }
    }

    [SpellHandler("EssenceSear")]
    public class EssenceResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 { get { return eProperty.Resist_Natural; } }
        public override string DebuffTypeName { get { return "Essence"; } }

        public EssenceResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.EssenceDebuff.MainDescription", Spell.Value);
        }
    }
}
