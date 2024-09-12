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
using DOL.GS.PacketHandler;
using DOL.GS.RealmAbilities;
using DOL.AI.Brain;

namespace DOL.GS.Spells
{
    using DOL.GS.Scripts;
    using DOL.Language;
    using Effects;

    /// <summary>
    /// 
    /// </summary>
    [SpellHandlerAttribute("Heal")]
    public class HealSpellHandler : SpellHandler
    {
        // constructor
        public HealSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
        /// <summary>
        /// Execute heal spell
        /// </summary>
        /// <param name="target"></param>
        public override bool StartSpell(GameLiving target)
        {
            var targets = SelectTargets(target);
            if (targets.Count <= 0) return false;

            bool healed = false;
            int minHeal;
            int maxHeal;

            CalculateHealVariance(out minHeal, out maxHeal);

            foreach (GameLiving healTarget in targets)
            {
                int heal = Util.Random(minHeal, maxHeal);

                if (SpellLine.KeyName == GlobalSpellsLines.Item_Effects)
                {
                    heal = maxHeal;
                }

                if (healTarget.IsDiseased)
                {
                    int amnesiaChance = healTarget.TempProperties.getProperty<int>("AmnesiaChance", 50);
                    int healReductionPercentage = amnesiaChance > 0 ? amnesiaChance : 50;
                    heal -= (heal * healReductionPercentage) / 100;
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "Spell.LifeTransfer.TargetDiseased"), eChatType.CT_SpellResisted);
                }

                if (SpellHandler.FindEffectOnTarget(healTarget, "Damnation") != null)
                {
                    int harmvalue = healTarget.TempProperties.getProperty<int>("DamnationValue", 0);

                    if (harmvalue > 0)
                    {
                        int damageAmount = (heal * harmvalue) / 100;
                        heal = 0;
                        AttackData ad = new AttackData
                        {
                            Attacker = Caster,
                            Target = healTarget,
                            DamageType = eDamageType.Natural,
                            AttackType = AttackData.eAttackType.Spell,
                            Damage = damageAmount,
                            AttackResult = GameLiving.eAttackResult.HitUnstyled,
                            CausesCombat = false,
                        };
                        healTarget.TakeDamage(ad);

                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetDamnedDamaged", damageAmount), eChatType.CT_YouDied);
                    }
                    else if (harmvalue < 0)
                    {
                        heal = (heal * Math.Abs(harmvalue)) / 100;
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetDamnedPartiallyHealed"), eChatType.CT_SpellResisted);
                    }
                    else
                    {
                        heal = 0;
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.DamnedNoHeal"), eChatType.CT_Important);
                    }
                }

                if (SpellLine.KeyName == GlobalSpellsLines.Item_Effects)
                {
                    healed |= ProcHeal(healTarget, heal);
                }
                else
                {
                    healed |= HealTarget(healTarget, heal);
                }
            }

            // group heals seem to use full power even if no heals
            if (!healed && Spell.Target.ToLower() == "realm")
                m_caster.Mana -= PowerCost(target) >> 1; // only 1/2 power if no heal
            else
                m_caster.Mana -= PowerCost(target);

            // send animation for non pulsing spells only
            if (Spell.Pulse == 0)
            {
                // show resisted effect if not healed
                foreach (GameLiving healTarget in targets)
                    if (healTarget.IsAlive)
                        SendEffectAnimation(healTarget, 0, false, healed ? (byte)1 : (byte)0);
            }

            if (!healed && Spell.CastTime == 0) m_startReuseTimer = false;

            return true;
        }

        /// <summary>
        /// Heals hit points of one target and sends needed messages, no spell effects
        /// </summary>
        /// <param name="target"></param>
        /// <param name="amount">amount of hit points to heal</param>
        /// <returns>true if heal was done</returns>
        public virtual bool HealTarget(GameLiving target, double amount)
        {
            if (target == null || target.ObjectState != GameLiving.eObjectState.Active) return false;

            // we can't heal enemy people
            if (!(Caster is TextNPC) && !GameServer.ServerRules.IsSameRealm(Caster, target, true))
                return false;

            // no healing of keep components
            if (target is Keeps.GameKeepComponent || target is Keeps.GameKeepDoor)
                return false;

            if (!target.IsAlive)
            {
                //"You cannot heal the dead!" sshot550.tga
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetDead", target.GetName(0, true)), eChatType.CT_SpellResisted);
                return false;
            }

            if (target is GamePlayer && (target as GamePlayer)!.NoHelp && Caster is GamePlayer)
            {
                //player not grouped, anyone else
                //player grouped, different group
                if ((target as GamePlayer)!.Group == null ||
                    (Caster as GamePlayer)!.Group == null ||
                    (Caster as GamePlayer)!.Group != (target as GamePlayer)!.Group)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.NoHelp"), eChatType.CT_SpellResisted);
                    return false;
                }
            }

            //moc heal decrease
            double mocFactor = 1.0;
            MasteryofConcentrationEffect moc = Caster.EffectList.GetOfType<MasteryofConcentrationEffect>();
            if (moc != null)
            {
                GamePlayer playerCaster = Caster as GamePlayer;
                MasteryofConcentrationAbility ra = playerCaster!.GetAbility<MasteryofConcentrationAbility>();
                if (ra != null)
                    mocFactor = (double)ra.GetAmountForLevel(ra.Level) / 100.0;
                amount = amount * mocFactor;
            }
            double criticalvalue = 0;
            int criticalchance = Caster.GetModified(eProperty.CriticalHealHitChance);
            double effectiveness = 0;
            if (Caster is GamePlayer)
                effectiveness = (Caster as GamePlayer)!.Effectiveness + (double)(Caster.GetModified(eProperty.HealingEffectiveness)) * 0.01;
            if (Caster is GameNPC)
                effectiveness = 1.0;

            //USE DOUBLE !
            double cache = amount * effectiveness;

            amount = cache;

            if (Util.Chance(criticalchance))
            {
                double minValue = amount / 10;
                double maxValue = amount / 2 + 1;
                criticalvalue = Util.RandomDouble() * (maxValue - minValue) + minValue;
            }

            amount += criticalvalue;

            GamePlayer playerTarget = target as GamePlayer;
            if (playerTarget != null)
            {
                GameSpellEffect HealEffect = SpellHandler.FindEffectOnTarget(playerTarget, "EfficientHealing");
                if (HealEffect != null)
                {
                    double HealBonus = amount * ((int)HealEffect.Spell.Value * 0.01);
                    amount += (int)HealBonus;
                    playerTarget.Out.SendMessage(LanguageMgr.GetTranslation(playerTarget.Client, "SpellHandler.HealSpell.EfficientHealingBuff", HealBonus), eChatType.CT_Spell, eChatLoc.CL_ChatWindow);
                }
                GameSpellEffect EndEffect = SpellHandler.FindEffectOnTarget(playerTarget, "EfficientEndurance");
                if (EndEffect != null)
                {
                    double EndBonus = amount * ((int)EndEffect.Spell.Value * 0.01);
                    //600 / 10 = 60end
                    playerTarget.Endurance += (int)EndBonus;
                    playerTarget.Out.SendMessage(LanguageMgr.GetTranslation(playerTarget.Client, "SpellHandler.HealSpell.EfficientEnduranceBuff", EndBonus), eChatType.CT_Spell, eChatLoc.CL_ChatWindow);
                }
            }

            GameSpellEffect flaskHeal = FindEffectOnTarget(target, "HealFlask");
            if (flaskHeal != null)
            {
                amount += (int)((amount * flaskHeal.Spell.Value) * 0.01);
            }

            amount = Math.Round(amount);
            int heal = target.ChangeHealth(Caster, GameLiving.eHealthChangeType.Spell, (int)amount);

            #region PVP DAMAGE

            long healedrp = 0;
            
            if (target.DamageRvRMemory > 0 && (target is GamePlayer || (target as NecromancerPet)?.GetPlayerOwner() is not null))
            {
                healedrp = (long)Math.Max(heal, 0);
                target.DamageRvRMemory -= healedrp;
            }

            if (heal == 0)
            {
                if (Spell.Pulse == 0)
                {
                    if (target == m_caster)
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.FullyHealedSelf"), eChatType.CT_SpellResisted);
                    else
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.FullyHealedTarget", m_caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
                }
                return false;
            }

            if (healedrp > 0 && m_caster is GamePlayer casterPlayer && (target is GamePlayer || (target as NecromancerPet)?.GetPlayerOwner() is not null))
            {
                int POURCENTAGE_SOIN_RP = ServerProperties.Properties.HEAL_PVP_DAMAGE_VALUE_RP; // ...% de bonus RP pour les soins effectués

                if (m_spell.Pulse == 0 && m_caster.CurrentRegionID != 242 && // On Exclu zone COOP
                    m_spell.SpellType.ToLower() != "spreadheal" && target != m_caster &&
                    m_spellLine.KeyName != GlobalSpellsLines.Item_Spells &&
                    m_spellLine.KeyName != GlobalSpellsLines.Potions_Effects &&
                    m_spellLine.KeyName != GlobalSpellsLines.Combat_Styles_Effect &&
                    m_spellLine.KeyName != GlobalSpellsLines.Reserved_Spells)
                {
                    long Bonus_RP_Soin = Convert.ToInt64((double)healedrp * POURCENTAGE_SOIN_RP / 100.0);

                    if (Bonus_RP_Soin >= 1)
                    {
                        PlayerStatistics stats = casterPlayer.Statistics as PlayerStatistics;

                        if (stats != null)
                        {
                            stats.RPEarnedFromHitPointsHealed += (uint)Bonus_RP_Soin;
                            stats.HitPointsHealed += (uint)healedrp;
                        }

                        casterPlayer.GainRealmPoints(Bonus_RP_Soin, false);
                        casterPlayer.SendTranslatedMessage("SpellHandler.HealSpell.RealmPointsGained", eChatType.CT_Important, eChatLoc.CL_SystemWindow, Bonus_RP_Soin);
                    }
                }
            }

            #endregion PVP DAMAGE

            if (m_caster == target && (SpellHandler.FindEffectOnTarget(m_caster, "Damnation") != null))
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.SelfHealed", heal), eChatType.CT_Spell);
                if (heal < amount)
                {
                    #region PVP DAMAGE

                    if (target.DamageRvRMemory > 0 && (target is GamePlayer || (target as NecromancerPet)?.GetPlayerOwner() is not null))
                    {
                        target.DamageRvRMemory = 0; //Remise a zéro compteur dommages/heal rps
                    }

                    #endregion PVP DAMAGE

                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.FullyHealedSelf"), eChatType.CT_Spell);
                }
            }
            else
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetHealed", m_caster.GetPersonalizedName(target), heal), eChatType.CT_Spell);
                MessageToLiving(target, LanguageMgr.GetTranslation((target as GamePlayer)?.Client, "SpellHandler.HealSpell.YouAreHealed", target.GetPersonalizedName(m_caster), heal), eChatType.CT_Spell);
                if (heal < amount && (SpellHandler.FindEffectOnTarget(target, "Damnation") != null))
                {

                    #region PVP DAMAGE

                    if (target.DamageRvRMemory > 0 && (target is GamePlayer || (target as NecromancerPet)?.GetLivingOwner() is not null))
                    {
                        target.DamageRvRMemory = 0; //Remise a zéro compteur dommages/heal rps
                    }

                    #endregion PVP DAMAGE

                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.FullyHealedTarget", m_caster.GetPersonalizedName(target)), eChatType.CT_Spell);
                }
                if (heal > 0 && criticalvalue > 0)
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.CriticalHeal", criticalvalue), eChatType.CT_Spell);
            }

            return true;
        }

        /// <summary>
        /// A heal generated by an item proc.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public virtual bool ProcHeal(GameLiving target, double amount)
        {
            if (target == null || target.ObjectState != GameLiving.eObjectState.Active) return false;

            if (!target.IsAlive)
                return false;

            // no healing of keep components
            if (target is Keeps.GameKeepComponent || target is Keeps.GameKeepDoor)
                return false;

            int heal = target.ChangeHealth(Caster, GameLiving.eHealthChangeType.Spell, (int)Math.Round(amount));

            if (m_caster == target && heal > 0 && (SpellHandler.FindEffectOnTarget(m_caster, "Damnation") != null))
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.SelfHealed", heal), eChatType.CT_Spell);

                if (heal < amount)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.FullyHealedSelf"), eChatType.CT_Spell);
                    #region PVP DAMAGE

                    if (target.DamageRvRMemory > 0 && (target is GamePlayer || (target as NecromancerPet)?.GetLivingOwner() is not null))
                    {
                        target.DamageRvRMemory = 0; //Remise a zéro compteur dommages/heal rps
                    }

                    #endregion PVP DAMAGE
                }
            }
            else if (heal > 0)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetHealed", m_caster.GetPersonalizedName(target), heal), eChatType.CT_Spell);
                MessageToLiving(target, LanguageMgr.GetTranslation((target as GamePlayer)?.Client, "SpellHandler.HealSpell.YouAreHealed", target.GetPersonalizedName(m_caster), heal), eChatType.CT_Spell);

                #region PVP DAMAGE
                
                if (target.DamageRvRMemory > 0 && (target is GamePlayer || (target as NecromancerPet)?.GetLivingOwner() is not null))
                {
                    target.DamageRvRMemory -= (long)Math.Max(heal, 0);
                }
            }

            #endregion PVP DAMAGE

            return true;
        }


        /// <summary>
        /// Calculates heal variance based on spec
        /// </summary>
        /// <param name="min">store min variance here</param>
        /// <param name="max">store max variance here</param>
        public virtual void CalculateHealVariance(out int min, out int max)
        {
            double spellValue = m_spell.Value;
            GamePlayer casterPlayer = m_caster as GamePlayer;

            if (m_spellLine.KeyName == GlobalSpellsLines.Item_Effects)
            {
                if (m_spell.Value > 0)
                {
                    min = (int)(spellValue * 0.75);
                    max = (int)(spellValue * 1.25);
                    return;
                }
            }

            if (m_spellLine.KeyName == GlobalSpellsLines.Potions_Effects)
            {
                if (m_spell.Value > 0)
                {
                    min = (int)(spellValue * 1.00);
                    max = (int)(spellValue * 1.25);
                    return;
                }
            }

            if (m_spellLine.KeyName == GlobalSpellsLines.Combat_Styles_Effect)
            {
                if (m_spell.Value > 0)
                {
                    if (UseMinVariance)
                    {
                        min = (int)(spellValue * 1.25);
                    }
                    else
                    {
                        min = (int)(spellValue * 0.75);
                    }

                    max = (int)(spellValue * 1.25);
                    return;
                }
            }

            if (m_spellLine.KeyName == GlobalSpellsLines.Reserved_Spells)
            {
                min = max = (int)spellValue;
                return;
            }

            // percents if less than zero
            if (spellValue < 0)
            {
                spellValue = (spellValue / -100.0) * m_caster.MaxHealth;

                min = max = (int)spellValue;
                return;
            }

            int upperLimit = (int)(spellValue * 1.25);
            if (upperLimit < 1)
            {
                upperLimit = 1;
            }

            double lineSpec = Caster.GetModifiedSpecLevel(m_spellLine.Spec);
            if (lineSpec < 1)
                lineSpec = 1;
            double eff = 0.25;
            if (Spell.Level > 0)
            {
                eff += (lineSpec - 1.0) / Spell.Level;
                if (eff > 1.25)
                    eff = 1.25;
            }

            int lowerLimit = (int)(spellValue * eff);
            if (lowerLimit < 1)
            {
                lowerLimit = 1;
            }
            if (lowerLimit > upperLimit)
            {
                lowerLimit = upperLimit;
            }

            min = lowerLimit;
            max = upperLimit;
            return;
        }

        public override string ShortDescription => $"The target regains {Spell.Value} hit points.";
    }
}
