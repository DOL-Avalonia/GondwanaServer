using System;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using DOL.GS.Effects;

namespace DOL.GS.Spells
{
    [SpellHandler("OmniHeal")]
    public class OmniHealSpellHandler : SpellHandler
    {
        public OmniHealSpellHandler(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line) { }

        protected override bool ExecuteSpell(GameLiving baseTarget, bool force = false)
        {
            IList<GameLiving> targets = SelectTargets(baseTarget, force);
            if (targets == null || targets.Count == 0)
                return false;

            bool anyHealed = false;
            foreach (GameLiving living in targets)
            {
                if (OnDirectEffect(living, 1.0))
                    anyHealed = true;
            }

            bool consume = anyHealed;
            if (!anyHealed && Spell.Target.Equals("Realm", StringComparison.OrdinalIgnoreCase))
            {
                Caster.Mana -= (CalculatePowerCost(baseTarget) >> 1);
                consume = true; // For AOEs, mark the spell as success even if nobody was healed, so item charges can be consumed similar to mana
            }
            else
            {
                Caster.Mana -= CalculatePowerCost(baseTarget);
            }

            if (Spell.Pulse == 0)
            {
                if (anyHealed)
                {
                    foreach (GameLiving living in targets)
                        SendEffectAnimation(living, 0, false, 1);
                }
                else
                {
                    SendEffectAnimation(Caster, 0, false, 0);
                }
            }
            return consume;
        }

        /// <summary>
        /// 1) Heal HP via HealSpellHandler logic (disease halving, HealDebuff, Damnation).
        /// 2) Heal End and Power with simpler “diseased => 25%, normal => 100%” approach.
        /// </summary>
        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null || !target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active)
                return false;

            bool didHP = HealHPviaHealSpellHandlerLogic(target);
            bool didEndPow = HealEnduAndPower(target);

            return (didHP || didEndPow);
        }

        #region HEAL HP

        private bool HealHPviaHealSpellHandlerLogic(GameLiving target)
        {
            if (!IsValidHealTargetForHP(target))
                return false;

            int minHeal, maxHeal;
            CalculateHealVarianceForHP(out minHeal, out maxHeal);
            int hpAmount = Util.Random(minHeal, maxHeal);

            if (SpellLine != null && SpellLine.KeyName == GlobalSpellsLines.Item_Effects)
                hpAmount = maxHeal;

            int totalHealReduction = 0;
            if (target.IsDiseased)
            {
                int amnesiaChance = target.TempProperties.getProperty<int>("AmnesiaChance", 50);
                int reducePct = (amnesiaChance > 0) ? amnesiaChance : 50;
                totalHealReduction += reducePct;

                if (target.Health < target.MaxHealth && totalHealReduction < 100)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "Spell.LifeTransfer.TargetDiseased", reducePct), eChatType.CT_SpellResisted);
                }
            }

            foreach (GameSpellEffect eff in target.EffectList)
            {
                if (eff.SpellHandler is HealDebuffSpellHandler)
                {
                    int debuffValue = (int)eff.Spell.Value;
                    int extra = 0;
                    if (Caster is GamePlayer gp)
                        extra = gp.GetModified(eProperty.DebuffEffectivness);
                    int adjustedDebuff = debuffValue + (debuffValue * extra) / 100;
                    totalHealReduction += adjustedDebuff;

                    if (target.Health < target.MaxHealth && totalHealReduction < 100)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.HealingReduced", adjustedDebuff), eChatType.CT_SpellResisted);
                    }
                }
            }

            if (totalHealReduction >= 100)
                totalHealReduction = 100;
            if (totalHealReduction > 0)
                hpAmount -= (hpAmount * totalHealReduction) / 100;

            var damnationEff = SpellHandler.FindEffectOnTarget(target, "Damnation");
            if (damnationEff != null)
            {
                int harmvalue = target.TempProperties.getProperty<int>("DamnationValue", 0);
                if (harmvalue > 0)
                {
                    int dmg = (hpAmount * harmvalue) / 100;
                    hpAmount = 0;

                    var ad = new AttackData
                    {
                        Attacker = Caster,
                        Target = target,
                        DamageType = eDamageType.Natural,
                        AttackType = AttackData.eAttackType.Spell,
                        Damage = dmg,
                        AttackResult = GameLiving.eAttackResult.HitUnstyled,
                        CausesCombat = false,
                    };
                    target.TakeDamage(ad);
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetDamnedDamaged", dmg), eChatType.CT_YouDied);
                }
                else if (harmvalue < 0)
                {
                    hpAmount = (hpAmount * Math.Abs(harmvalue)) / 100;
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetDamnedPartiallyHealed"), eChatType.CT_SpellResisted);
                }
                else
                {
                    hpAmount = 0;
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.DamnedNoHeal"), eChatType.CT_Important);
                }
            }

            if (hpAmount <= 0)
                return false;

            return HealTargetForHP(target, hpAmount);
        }

        private bool IsValidHealTargetForHP(GameLiving target)
        {
            if (!target.IsAlive) return false;
            if (target is Keeps.GameKeepComponent || target is Keeps.GameKeepDoor) return false;

            if (!GameServer.ServerRules.IsSameRealm(Caster, target, true) && !(Caster is GameNPC))
                return false;
            return true;
        }

        private bool HealTargetForHP(GameLiving target, int amount)
        {
            if (target == null || target.ObjectState != GameLiving.eObjectState.Active)
                return false;
            if (!GameServer.ServerRules.IsSameRealm(Caster, target, true) && !(Caster is GameNPC))
                return false;
            if (!target.IsAlive)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetDead", target.GetPersonalizedName(Caster)), eChatType.CT_SpellResisted);
                return false;
            }

            if (target is GamePlayer pTarget && pTarget.NoHelp && Caster is GamePlayer pCaster)
            {
                if (pTarget.Group == null || pCaster.Group == null || pTarget.Group != pCaster.Group)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.NoHelp"), eChatType.CT_SpellResisted);
                    return false;
                }
            }

            int healed = target.ChangeHealth(Caster, GameLiving.eHealthChangeType.Spell, amount);
            if (healed <= 0)
            {
                if (Spell.Pulse == 0)
                {
                    if (target == Caster)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.FullyHealedSelf"), eChatType.CT_SpellResisted);
                    }
                    else
                    {
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.FullyHealedTarget", Caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
                    }
                }
                return false;
            }

            if (target.DamageRvRMemory > 0 && (target is GamePlayer || (target as NecromancerPet)?.GetLivingOwner() != null))
            {
                long dec = Math.Max(healed, 0);
                target.DamageRvRMemory -= dec;
            }

            if (Caster == target)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.SelfHealed", healed), eChatType.CT_Spell);

                if (healed < amount && amount > 0)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.FullyHealedSelf"), eChatType.CT_Spell);
                }
            }
            else
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetHealed", Caster.GetPersonalizedName(target), healed), eChatType.CT_Spell);

                if (target is GamePlayer tP)
                {
                    MessageToLiving(target, LanguageMgr.GetTranslation(tP.Client, "SpellHandler.HealSpell.YouAreHealed", tP.GetPersonalizedName(Caster), healed), eChatType.CT_Spell);
                }

                if (healed < amount && amount > 0)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.FullyHealedTarget", Caster.GetPersonalizedName(target)), eChatType.CT_Spell);
                }
            }
            return true;
        }

        private void CalculateHealVarianceForHP(out int min, out int max)
        {
            double val = Spell.Value;
            if (val < 0)
            {
                val = (val / -100.0) * Caster.MaxHealth;
                min = max = (int)val;
                return;
            }

            int upperLimit = (int)(val * 1.25);
            if (upperLimit < 1) upperLimit = 1;

            double lineSpec = Caster.GetModifiedSpecLevel(SpellLine?.Spec);
            if (lineSpec < 1) lineSpec = 1;

            double eff = 0.25;
            if (Spell.Level > 0)
            {
                eff += (lineSpec - 1.0) / Spell.Level;
                if (eff > 1.25) eff = 1.25;
            }

            int lowerLimit = (int)(val * eff);
            if (lowerLimit < 1) lowerLimit = 1;
            if (lowerLimit > upperLimit) lowerLimit = upperLimit;

            min = lowerLimit;
            max = upperLimit;
        }

        #endregion

        #region HEAL ENDURANCE & POWER (DISEASE => 25%, ELSE 100%)

        /// <summary>
        /// End/Mana each get:
        ///   - if Spell.Value<0 => that % of target’s MaxEnd/MaxMana
        ///   - if Spell.Value>0 => direct points
        ///   - if target diseased => only 25% of that base
        ///   - else => 100% of base
        /// </summary>
        private bool HealEnduAndPower(GameLiving target)
        {
            int endBase = (Spell.Value < 0)
                ? (int)(Math.Abs(Spell.Value) * 0.01 * target.MaxEndurance)
                : (int)Spell.Value;
            int manaBase = (Spell.Value < 0)
                ? (int)(Math.Abs(Spell.Value) * 0.01 * target.MaxMana)
                : (int)Spell.Value;

            if (target.IsDiseased)
            {
                endBase = (int)(endBase * 0.25);
                manaBase = (int)(manaBase * 0.25);
            }

            var damnationEff = SpellHandler.FindEffectOnTarget(target, "Damnation");
            if (damnationEff != null)
            {
                int harmvalue = target.TempProperties.getProperty<int>("DamnationValue", 0);

                if (harmvalue > 0 || harmvalue == 0)
                {
                    endBase = 0;
                    manaBase = 0;
                }
                else
                {
                    double partialFactor = harmvalue * 0.25;
                    endBase = (int)(endBase * (partialFactor / 100.0));
                    manaBase = (int)(manaBase * (partialFactor / 100.0));
                }
            }

            if (endBase <= 0 && manaBase <= 0)
                return false;

            int endHealed = (endBase > 0)
                ? target.ChangeEndurance(Caster, GameLiving.eEnduranceChangeType.Spell, endBase)
                : 0;
            int manaHealed = (manaBase > 0)
                ? target.ChangeMana(Caster, GameLiving.eManaChangeType.Spell, manaBase)
                : 0;

            bool changedAny = (endHealed > 0 || manaHealed > 0);
            if (!changedAny)
                return false;

            if (Caster == target)
            {
                string msg = LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OmniHeal.SelfHealed", endHealed, manaHealed);
                MessageToCaster(msg, eChatType.CT_Spell);
            }
            else
            {
                string msgCaster = LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.OmniHeal.TargetHealed", target.GetPersonalizedName(Caster), endHealed, manaHealed);
                MessageToCaster(msgCaster, eChatType.CT_Spell);

                if (target is GamePlayer tP)
                {
                    string msgTarget = LanguageMgr.GetTranslation(tP.Client, "SpellHandler.OmniHeal.YouAreHealed", Caster.GetPersonalizedName(target), endHealed, manaHealed);
                    tP.Out.SendMessage(msgTarget, eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }
            return true;
        }

        #endregion

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            double valueForDescription = Spell.Value >= 0 ? Spell.Value : Math.Abs(Spell.Value);
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.OmniHeal.MainDescription", valueForDescription);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}