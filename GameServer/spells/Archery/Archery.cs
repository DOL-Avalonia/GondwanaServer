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
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Events;
using DOL.AI.Brain;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    [SpellHandler("Archery")]
    public class Archery : ArrowSpellHandler
    {
        public enum eShotType
        {
            Other = 0,
            Critical = 1,
            Power = 2,
            PointBlank = 3,
            Rapid = 4
        }

        public override string CostType => "Endurance";

        public override bool UnstealthCasterOnStart
        {
            get { return false; }
        }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (m_caster.ObjectState != GameLiving.eObjectState.Active) return false;
            if (!m_caster.IsAlive)
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DeadCantCast"), eChatType.CT_System);
                return false;
            }

            // Is PS ?
            GameSpellEffect Phaseshift = SpellHandler.FindEffectOnTarget(Caster, "Phaseshift");
            if (Phaseshift != null && (Spell.InstrumentRequirement == 0 || Spell.SpellType == "Mesmerize"))
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.PhaseshiftedCantCast"), eChatType.CT_System);
                return false;
            }

            // Is Shield Disarm ?
            ShieldTripDisarmEffect shieldDisarm = Caster.EffectList.GetOfType<ShieldTripDisarmEffect>();
            if (shieldDisarm != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Archery.DisarmedCantCast"), eChatType.CT_System);
                return false;
            }

            // Is Mentalist RA5L ?
            SelectiveBlindnessEffect SelectiveBlindness = Caster.EffectList.GetOfType<SelectiveBlindnessEffect>();
            if (SelectiveBlindness != null)
            {
                GameLiving EffectOwner = SelectiveBlindness.EffectSource;
                if (EffectOwner == selectedTarget)
                {
                    if (m_caster is GamePlayer player)
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "GameLiving.AttackData.InvisibleToYou", selectedTarget.GetName(0, true)), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            // Is immune ?
            if (selectedTarget != null && selectedTarget.HasAbility("DamageImmunity"))
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DamageImmunity", m_caster.GetPersonalizedName(selectedTarget)), eChatType.CT_SpellResisted);
                return false;
            }

            if (m_caster.IsSitting)
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.CantCastWhileSitting"), eChatType.CT_SpellResisted);
                return false;
            }
            if (m_spell.RecastDelay > 0)
            {
                int left = m_caster.GetSkillDisabledDuration(m_spell);
                if (left > 0)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustWaitBeforeUse", (left / 1000 + 1).ToString()), eChatType.CT_System);
                    return false;
                }
            }
            String targetType = m_spell.Target.ToLower();
            if (targetType == "area")
            {
                if (!m_caster.IsWithinRadius(m_caster.GroundTargetPosition, CalculateSpellRange()))
                {
                    MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.AreaTargetOutOfRange"), eChatType.CT_SpellResisted);
                    return false;
                }
            }

            if (targetType == "enemy")
            {
                if (m_caster.IsObjectInFront(selectedTarget, 180) == false)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetNotInView"), eChatType.CT_SpellResisted);
                    Caster.Notify(GameLivingEvent.CastFailed, new CastFailedEventArgs(this, CastFailedEventArgs.Reasons.TargetNotInView));
                    return false;
                }

                if (m_caster.TargetInView == false)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetNotVisible"), eChatType.CT_SpellResisted);
                    Caster.Notify(GameLivingEvent.CastFailed, new CastFailedEventArgs(this, CastFailedEventArgs.Reasons.TargetNotInView));
                    return false;
                }
            }

            if (Caster != null && Caster is GamePlayer && Caster.AttackWeapon != null && GlobalConstants.IsBowWeapon((eObjectType)Caster.AttackWeapon.Object_Type))
            {
                if (Spell.LifeDrainReturn == (int)eShotType.Critical && (!(Caster.IsStealthed)))
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustBeStealthedBow"), eChatType.CT_SpellResisted);
                    return false;
                }

                return true;
            }
            else
            {
                if (Spell.LifeDrainReturn == (int)eShotType.Critical)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustBeStealthedBow"), eChatType.CT_SpellResisted);
                    return false;
                }

                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.MustWieldBow"), eChatType.CT_SpellResisted);
                return false;
            }
        }

        public override void SendSpellMessages()
        {
            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Archery.Prepare", Spell.Name), eChatType.CT_YouHit);
        }


        public override int CalculateToHitChance(GameLiving target)
        {
            int bonustohit = Caster.GetModified(eProperty.ToHitBonus);

            // miss rate is 0 on same level opponent
            int hitchance = 100 + bonustohit;

            if (!(GameServer.ServerRules.IsPveOnlyBonus(eProperty.DefensiveBonus) && GameServer.ServerRules.IsPvPAction(Caster, target)))
                hitchance -= target.GetModified(eProperty.DefensiveBonus);
            if ((Caster is GamePlayer && target is GamePlayer) == false)
            {
                hitchance -= (int)(Caster.GetConLevel(target) * ServerProperties.Properties.PVE_SPELL_CONHITPERCENT);
                hitchance += Math.Max(0, target.Attackers.Count - 1) * ServerProperties.Properties.MISSRATE_REDUCTION_PER_ATTACKERS;
            }

            return hitchance;
        }

        public override int AdjustDamageForHitChance(int damage, int hitChance)
        {
            int adjustedDamage = damage;

            if (hitChance < 85)
            {
                adjustedDamage += (int)(adjustedDamage * (hitChance - 85) * 0.038);
            }

            return adjustedDamage;
        }

        public override double GetLevelModFactor()
        {
            return 0.025;
        }


        public override AttackData CalculateDamageToTarget(GameLiving target, double effectiveness)
        {
            AttackData ad = base.CalculateDamageToTarget(target, effectiveness);
            GameSpellEffect bladeturn = FindEffectOnTarget(target, "Bladeturn");
            if (bladeturn != null)
            {
                if (BladeturnSpellHandler.BlockAttack(bladeturn, ad))
                    return ad;
            }

            if (ad.AttackResult != GameLiving.eAttackResult.Missed)
            {
                GameNPC npc = target as GameNPC;
                if (npc != null)
                {
                    if (npc.Brain != null && (npc.Brain is IControlledBrain) == false)
                    {
                        // boost for npc damage until we find exactly where calculation is going wrong -tolakram
                        ad.Damage = (int)(ad.Damage * 1.57);
                    }
                }

                // Volley damage reduction based on live testing - tolakram
                if (Spell.Target.ToLower() == "area")
                {
                    ad.Damage = (int)(ad.Damage * 0.815);
                }
            }

            return ad;
        }

        public override eDamageType DetermineSpellDamageType()
        {
            GameSpellEffect ef = FindEffectOnTarget(Caster, "ArrowDamageTypes");
            if (ef != null)
            {
                return ef.SpellHandler.Spell.DamageType;
            }
            else
            {
                return eDamageType.Slash;
            }
        }

        public override double CalculateDamageBase(GameLiving target)
        {
            double spellDamage = Spell.Damage;
            GamePlayer player = Caster as GamePlayer;

            int manaStatValue = 0;
            if (player != null)
            {
                manaStatValue = player.GetModified((eProperty)player.CharacterClass.ManaStat);
            }
            else
                manaStatValue = Caster.GetModified(eProperty.Intelligence);
            spellDamage *= (manaStatValue + 300) / 275.0;

            if (spellDamage < 0)
                spellDamage = 0;

            return spellDamage;
        }

        public override void FinishSpellCast(GameLiving target)
        {
            if (target == null && Spell.Target.ToLower() != "area") return;
            if (Caster == null) return;

            Caster.Stealth(false);

            if (Spell.Target.ToLower() == "area")
            {
                // always put archer into combat when using area (volley)
                Caster.LastAttackTickPvE = Caster.CurrentRegion.Time;
                Caster.LastAttackTickPvP = Caster.CurrentRegion.Time;

                foreach (GameLiving npc in WorldMgr.GetNPCsCloseToSpot(Caster.GroundTargetPosition, (ushort)Spell.Radius))
                {
                    if (npc.Realm == 0 || Caster.Realm == 0)
                    {
                        npc.LastAttackedByEnemyTickPvE = npc.CurrentRegion.Time;
                    }
                }
            }
            else
            {
                if (target!.Realm == 0 || Caster.Realm == 0)
                {
                    target.LastAttackedByEnemyTickPvE = target.CurrentRegion.Time;
                    Caster.LastAttackTickPvE = Caster.CurrentRegion.Time;
                }
                else
                {
                    target.LastAttackedByEnemyTickPvP = target.CurrentRegion.Time;
                    Caster.LastAttackTickPvP = Caster.CurrentRegion.Time;
                }
            }

            base.FinishSpellCast(target);
        }

        public override int CalculateCastingTime()
        {
            if (Spell.LifeDrainReturn == (int)eShotType.Power) return 6000;

            int ticks = m_spell.CastTime;

            double percent = 1.0;
            int dex = Caster.GetModified(eProperty.Dexterity);

            if (dex < 60)
            {
                //do nothing.
            }
            else if (dex < 250)
            {
                percent = 1.0 - (dex - 60) * 0.15 * 0.01;
            }
            else
            {
                percent = 1.0 - ((dex - 60) * 0.15 + (dex - 250) * 0.05) * 0.01;
            }

            percent *= 1.0 - Caster.GetModified(eProperty.CastingSpeed) * 0.01;

            ticks = (int)(ticks * Math.Max(m_caster.CastingSpeedReductionCap, percent));

            if (ticks < m_caster.MinimumCastingSpeed)
                ticks = m_caster.MinimumCastingSpeed;

            return ticks;
        }

        public override int PowerCost(GameLiving target) { return 0; }

        public override int CalculateEnduranceCost()
        {
            #region [Freya] Nidel: Arcane Syphon chance
            int syphon = Caster.GetModified(eProperty.ArcaneSyphon);
            if (syphon > 0)
            {
                if (Util.Chance(syphon))
                {
                    return 0;
                }
            }
            #endregion
            return (int)(Caster.MaxEndurance * (Spell.Power * .01));
        }

        public override bool CasterIsAttacked(GameLiving attacker)
        {
            if (Spell.Uninterruptible)
                return false;

            if (IsCasting && Stage < 2)
            {
                double mod = Caster.GetConLevel(attacker);
                double chance = 65;
                chance += mod * 10;
                chance = Math.Max(1, chance);
                chance = Math.Min(99, chance);
                if (attacker is GamePlayer) chance = 100;
                if (Util.Chance((int)chance))
                {
                    Caster.TempProperties.setProperty(INTERRUPT_TIMEOUT_PROPERTY, Caster.CurrentRegion.Time + Caster.SpellInterruptDuration);
                    MessageToLiving(Caster, LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Archery.Interrupted", attacker.GetName(0, true)), eChatType.CT_SpellResisted);
                    InterruptCasting();
                    return true;
                }
            }
            return true;
        }

        public override IList<string> DelveInfo
        {
            get
            {
                var list = new List<string>();
                list.Add(Spell.Description);
                list.Add(" ");
                if (Spell.InstrumentRequirement != 0)
                    list.Add("Instrument require: " + GlobalConstants.InstrumentTypeToName(Spell.InstrumentRequirement));
                if (Spell.Damage != 0)
                    list.Add("Damage: " + Spell.Damage.ToString("0.###;0.###'%'"));
                else if (Spell.Value != 0)
                    list.Add("Value: " + Spell.Value.ToString("0.###;0.###'%'"));
                list.Add("Target: " + Spell.Target);
                if (Spell.Range != 0)
                    list.Add("Range: " + Spell.Range);
                if (Spell.Duration >= ushort.MaxValue * 1000)
                    list.Add("Duration: Permanent.");
                else if (Spell.Duration > 60000)
                    list.Add(string.Format("Duration: {0}:{1} min", Spell.Duration / 60000, (Spell.Duration % 60000 / 1000).ToString("00")));
                else if (Spell.Duration != 0)
                    list.Add("Duration: " + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
                if (Spell.Frequency != 0)
                    list.Add("Frequency: " + (Spell.Frequency * 0.001).ToString("0.0"));
                if (Spell.Power != 0)
                    list.Add("Endurance cost: " + Spell.Power.ToString("0;0'%'"));
                list.Add("Casting time: " + (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'"));
                if (Spell.RecastDelay > 60000)
                    list.Add("Recast time: " + (Spell.RecastDelay / 60000).ToString() + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
                else if (Spell.RecastDelay > 0)
                    list.Add("Recast time: " + (Spell.RecastDelay / 1000).ToString() + " sec");
                if (Spell.Radius != 0)
                    list.Add("Radius: " + Spell.Radius);
                if (Spell.DamageType != eDamageType.Natural)
                    list.Add("Damage: " + GlobalConstants.DamageTypeToName(Spell.DamageType));
                return list;
            }
        }

        /// <summary>
        /// Do not trigger Subspells
        /// </summary>
        /// <param name="target"></param>
        public override bool CastSubSpells(GameLiving target)
        {
            return ServerProperties.Properties.ENABLE_SUB_SPELL_ALL_CLASS ? base.CastSubSpells(target) : false;
        }

        public Archery(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string damageTypeName = LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType);
            string mainDesc;

            if (Spell.Radius > 0)
            {
                mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Archery.AreaTarget", Spell.Damage, damageTypeName);
            }
            else
            {
                mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.Archery.SingleTarget", Spell.Damage, damageTypeName);
            }

            if (Spell.Value > 0)
            {
                string effectivenessLine = LanguageMgr.GetTranslation(language, "SpellDescription.Archery.SecondaryEffectiveness", Spell.Value);
                mainDesc += "\n\n" + effectivenessLine;
            }

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}
