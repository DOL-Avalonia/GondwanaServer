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
using DOL.AI.Brain;
using DOL.GS.Scripts;
using DOL.Language;
using DOL.GS.Effects;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Based on HealSpellHandler.cs
    /// Spell calculates a percentage of the caster's health.
    /// Heals target for the full amount, Caster loses half that amount in health.
    /// </summary>
    [SpellHandlerAttribute("LifeTransfer")]
    public class LifeTransferSpellHandler : SpellHandler
    {
        // constructor
        public LifeTransferSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        private bool m_healed = false;

        /// <inheritdoc />
        public override int CalculatePowerCost(GameLiving target)
        {
            // group heals seem to use full power even if no heals
            if (!m_healed && Spell.Target.ToLower() is "realm")
                return base.CalculatePowerCost(target) >> 1; // only 1/2 power if no heal
            else
                return base.CalculatePowerCost(target);
        }

        /// <summary>
        /// Execute lifetransfer spell
        /// </summary>
        protected override bool ExecuteSpell(GameLiving target, bool force = false)
        {
            var targets = SelectTargets(target, force);
            if (targets.Count <= 0) return false;

            double spellValue = m_spell.Value;

            int baseTransferHeal = (int)(Caster.MaxHealth / 100 * Math.Abs(spellValue));

            // Needed to prevent divide by zero error and ensure caster's health doesn't drop below 1
            if (baseTransferHeal <= 0)
                baseTransferHeal = 0;
            else
            {
                // Ensure caster doesn't die from health loss
                if ((baseTransferHeal >> 1) >= Caster.Health)
                    baseTransferHeal = ((Caster.Health - 1) << 1);
            }

            int totalHealedAmount = 0;

            foreach (GameLiving healTarget in targets)
            {
                int transferHeal = baseTransferHeal;

                int totalHealReductionPercentage = 0;

                if (healTarget.IsDiseased)
                {
                    int amnesiaChance = healTarget.TempProperties.getProperty<int>("AmnesiaChance", 50);
                    int healReductionPercentage = amnesiaChance > 0 ? amnesiaChance : 50;
                    totalHealReductionPercentage += healReductionPercentage;

                    if (Caster is GamePlayer player)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.LifeTransfer.TargetDiseased", healReductionPercentage), eChatType.CT_SpellResisted);
                    }
                }

                foreach (GameSpellEffect effect in healTarget.EffectList)
                {
                    if (effect.SpellHandler is HealDebuffSpellHandler)
                    {
                        int debuffValue = (int)effect.Spell.Value;
                        int debuffEffectivenessBonus = 0;

                        GameLiving debuffer = effect.SpellHandler.Caster;

                        if (debuffer is GamePlayer debufferPlayer)
                        {
                            debuffEffectivenessBonus = debufferPlayer.GetModified(eProperty.DebuffEffectivness);
                        }

                        int itemDebuffBonus = (debuffValue * debuffEffectivenessBonus) / 100;
                        int adjustedDebuffValue = debuffValue + itemDebuffBonus;
                        totalHealReductionPercentage += adjustedDebuffValue;

                        if (healTarget.Health < healTarget.MaxHealth && totalHealReductionPercentage < 100)
                        {
                            MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.HealingReduced", adjustedDebuffValue), eChatType.CT_SpellResisted);
                        }
                    }
                }

                if (totalHealReductionPercentage >= 100)
                {
                    totalHealReductionPercentage = 100;
                }

                if (totalHealReductionPercentage > 0)
                {
                    transferHeal -= (transferHeal * totalHealReductionPercentage) / 100;
                }

                if (transferHeal <= 0)
                {
                    if (Caster is GamePlayer player)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "SpellHandler.HealSpell.HealingNull"), eChatType.CT_SpellResisted);
                    }
                    continue;
                }

                // Handle Damnation effect
                if (SpellHandler.FindEffectOnTarget(healTarget, "Damnation") != null)
                {
                    int harmvalue = healTarget.TempProperties.getProperty<int>("DamnationValue", 0);

                    if (harmvalue > 0)
                    {
                        int damageAmount = (transferHeal * harmvalue) / 100;
                        transferHeal = 0;

                        AttackData ad = new AttackData
                        {
                            Attacker = Caster,
                            Target = healTarget,
                            DamageType = eDamageType.Natural,
                            AttackType = AttackData.eAttackType.Spell,
                            Damage = damageAmount,
                            AttackResult = GameLiving.eAttackResult.HitUnstyled,
                        };
                        healTarget.TakeDamage(ad);

                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "Spell.LifeTransfer.TargetDamnedDamages", damageAmount), eChatType.CT_YouDied);
                    }
                    else if (harmvalue < 0)
                    {
                        transferHeal = (transferHeal * Math.Abs(harmvalue)) / 100;
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "Spell.LifeTransfer.TargetDamnedReducedHeal"), eChatType.CT_SpellResisted);
                        m_healed |= HealTarget(healTarget, transferHeal);
                        totalHealedAmount += transferHeal;
                    }
                    else
                    {
                        transferHeal = 0;
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "Spell.LifeTransfer.TargetDamned"), eChatType.CT_Important);
                    }
                }
                else
                {
                    m_healed |= HealTarget(healTarget, transferHeal);
                    totalHealedAmount += transferHeal;
                }
            }
            
            ConsumePower(target);

            if (m_healed || Spell.Target != "realm")
            {
                if ((totalHealedAmount >> 1) >= Caster.Health)
                {
                    totalHealedAmount = ((Caster.Health - 1) << 1);
                }

                m_caster.Health -= totalHealedAmount >> 1;
            }

            // Send animation for non pulsing spells only
            if (Spell.Pulse == 0)
            {
                if (m_healed)
                {
                    // Send animation on all targets if healed
                    foreach (GameLiving healTarget in targets)
                        SendEffectAnimation(healTarget, 0, false, 1);
                }
                else
                {
                    // Show resisted effect if not healed
                    SendEffectAnimation(Caster, 0, false, 0);
                }
            }

            return true;
        }

        /// <summary>
        /// Heals hit points of one target and sends needed messages, no spell effects
        /// </summary>
        /// <param name="target"></param>
        /// <param name="amount">amount of hit points to heal</param>
        /// <returns>true if heal was done</returns>
        public virtual bool HealTarget(GameLiving target, int amount)
        {
            if (target == null || target.ObjectState != GameLiving.eObjectState.Active) return false;

            // we can't heal enemy people
            if (!(Caster is TextNPC) && !GameServer.ServerRules.IsSameRealm(Caster, target, true))
                return false;

            if (!target.IsAlive)
            {
                if (Caster is GamePlayer player)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.LifeTransfer.TargetDead", target.GetName(0, true)), eChatType.CT_SpellResisted);
                }
                return false;
            }

            if (m_caster == target)
            {
                if (Caster is GamePlayer player)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.LifeTransfer.SelfTransfer"), eChatType.CT_SpellResisted);
                }
                return false;
            }

            if (amount <= 0) //Player does not have enough health to transfer
            {
                if (Caster is GamePlayer player)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.LifeTransfer.InsufficientHealth"), eChatType.CT_SpellResisted);
                }
                return false;
            }


            int heal = target.ChangeHealth(Caster, GameLiving.eHealthChangeType.Spell, amount);

            #region PVP DAMAGE

            long healedrp = 0;
            GamePlayer playerOrNecroPetOwner = m_caster as GamePlayer ?? (m_caster as NecromancerPet)?.GetPlayerOwner();

            if (target.DamageRvRMemory > 0 && playerOrNecroPetOwner != null)
            {
                if (target is GamePlayer || (target as NecromancerPet)?.GetPlayerOwner() is not null)
                {
                    healedrp = (long)Math.Max(heal, 0);
                    target.DamageRvRMemory -= healedrp;
                }
            }

            if (healedrp > 0 && m_caster != target && m_spellLine.KeyName != GlobalSpellsLines.Item_Spells &&
                m_caster.CurrentRegionID != 242 && m_spell.Pulse == 0) // On Exclu zone COOP
            {
                int POURCENTAGE_SOIN_RP = ServerProperties.Properties.HEAL_PVP_DAMAGE_VALUE_RP; // ...% de bonus RP pour les soins effectués
                long Bonus_RP_Soin = Convert.ToInt64((double)healedrp * POURCENTAGE_SOIN_RP / 100);

                if (Bonus_RP_Soin >= 1)
                {
                    PlayerStatistics stats = playerOrNecroPetOwner!.Statistics as PlayerStatistics;

                    if (stats != null)
                    {
                        stats.RPEarnedFromHitPointsHealed += (uint)Bonus_RP_Soin;
                        stats.HitPointsHealed += (uint)healedrp;
                    }

                    playerOrNecroPetOwner.GainRealmPoints(Bonus_RP_Soin, false);
                    playerOrNecroPetOwner.Out.SendMessage(LanguageMgr.GetTranslation(playerOrNecroPetOwner.Client.Account.Language, "GameObjects.GamePlayer.GainRealmPoints.LifeTransferSpell", Bonus_RP_Soin), eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                }
            }

            #endregion PVP DAMAGE

            if (heal == 0)
            {
                if (Spell.Pulse == 0)
                {
                    if (Caster is GamePlayer player)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.LifeTransfer.TargetFullyHealed", target.GetName(0, true)), eChatType.CT_SpellResisted);
                    }
                }

                return false;
            }

            if (Caster is GamePlayer playercaster)
            {
                MessageToCaster(LanguageMgr.GetTranslation(playercaster.Client.Account.Language, "Spell.LifeTransfer.HealCaster", m_caster.GetPersonalizedName(target), heal), eChatType.CT_Spell);
            }
            if (target is GamePlayer targetplayer)
            {
                MessageToLiving(target, LanguageMgr.GetTranslation(targetplayer.Client.Account.Language, "Spell.LifeTransfer.HealTarget", target.GetPersonalizedName(m_caster), heal), eChatType.CT_Spell);
            }
            if (heal < amount)
                if (Caster is GamePlayer player)
                {
                    MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.LifeTransfer.TargetFullyHealed", target.GetName(0, true)), eChatType.CT_Spell);
                }

            return true;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.LifeTransfer.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}
