using DOL.GS.Spells;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Events;
using DOL.AI.Brain;
using DOL.Language;
using System;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    [SpellHandler("MagicHealAbsorb")]
    public class MagicHealAbsorbSpellHandler : SpellHandler
    {
        public MagicHealAbsorbSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine)
            : base(caster, spell, spellLine)
        {
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            GameLiving living = effect.Owner;
            GameEventMgr.AddHandler(living, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventHandler));

            if (!string.IsNullOrEmpty(Spell.Message1))
                MessageToLiving(effect.Owner, Spell.GetFormattedMessage1(effect.Owner as GamePlayer), eChatType.CT_Spell);

            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (player != effect.Owner && !string.IsNullOrEmpty(Spell.Message2))
                    player.Out.SendMessage(Spell.GetFormattedMessage2(player, player.GetPersonalizedName(effect.Owner)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);

            GameLiving living = effect.Owner;
            GameEventMgr.RemoveHandler(living, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventHandler));

            if (!noMessages && !string.IsNullOrEmpty(Spell.Message3))
                MessageToLiving(effect.Owner, Spell.GetFormattedMessage3(effect.Owner as GamePlayer), eChatType.CT_SpellExpires);

            if (!noMessages && !string.IsNullOrEmpty(Spell.Message4))
            {
                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (player != effect.Owner && !string.IsNullOrEmpty(Spell.Message4))
                        player.Out.SendMessage(Spell.GetFormattedMessage4(player, player.GetPersonalizedName(effect.Owner)), eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                }
            }

            return base.OnEffectExpires(effect, noMessages);
        }

        private void EventHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            if (!(arguments is AttackedByEnemyEventArgs args))
                return;

            AttackData ad = args.AttackData;
            GameLiving target = ad.Target;

            if (target == null || !target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active)
                return;

            bool isApplicableSpell = false;

            if (ad.AttackType == AttackData.eAttackType.Spell || ad.AttackType == AttackData.eAttackType.DoT)
            {
                if (ad.SpellHandler != null && ad.SpellHandler.Spell != null)
                {
                    string spellType = ad.SpellHandler.Spell.SpellType;
                    if (spellType == "DirectDamage" || spellType == "Bolt" || spellType == "Bomber" || spellType == "HereticDamageSpeedDecreaseLOP" || spellType == "HereticDoTLostOnPulse" || spellType == "DirectDamageWithDebuff" || spellType == "Lifedrain" || spellType == "OmniLifedrain")
                    {
                        isApplicableSpell = true;
                    }

                    else if (ad.SpellHandler is DoTSpellHandler)
                    {
                        isApplicableSpell = true;
                    }
                }
            }

            if (!isApplicableSpell)
            {
                return;
            }

            int absorbPercent = (int)Spell.Value;
            int remainingDamagePercent = 100 - absorbPercent;
            int originalDamage = ad.Damage + ad.CriticalDamage;

            int damageToAbsorb = (originalDamage * absorbPercent) / 100;

            ad.Damage = (ad.Damage * remainingDamagePercent) / 100;
            ad.CriticalDamage = (ad.CriticalDamage * remainingDamagePercent) / 100;

            int healAmount = damageToAbsorb;
            int totalHealReductionPercentage = 0;

            if (target.IsDiseased)
            {
                int amnesiaChance = target.TempProperties.getProperty<int>("AmnesiaChance", 50);
                int healReductionPercentage = amnesiaChance > 0 ? amnesiaChance : 50;
                totalHealReductionPercentage += healReductionPercentage;
                if (target is GamePlayer playerTarget)
                {
                    if (target.Health < target.MaxHealth && totalHealReductionPercentage < 100)
                    {
                        MessageToLiving(playerTarget, LanguageMgr.GetTranslation(playerTarget.Client, "SpellHandler.HealSpell.YouDiseased", healReductionPercentage), eChatType.CT_SpellResisted);
                    }
                }
            }

            foreach (GameSpellEffect debuffEffect in target.EffectList)
            {
                if (debuffEffect.SpellHandler is HealDebuffSpellHandler)
                {
                    int debuffValue = (int)debuffEffect.Spell.Value;
                    int debuffEffectivenessBonus = 0;

                    if (target is GamePlayer gamePlayer)
                    {
                        debuffEffectivenessBonus = gamePlayer.GetModified(eProperty.DebuffEffectivness);
                    }

                    int adjustedDebuffValue = debuffValue + (debuffValue * debuffEffectivenessBonus) / 100;
                    totalHealReductionPercentage += adjustedDebuffValue;
                    if (target is GamePlayer player)
                    {
                        if (target.Health < target.MaxHealth && totalHealReductionPercentage < 100)
                        {
                            MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellHandler.HealSpell.HealingReduced", adjustedDebuffValue), eChatType.CT_SpellResisted);
                        }
                    }
                }
            }

            if (totalHealReductionPercentage > 100)
                totalHealReductionPercentage = 100;

            healAmount -= (healAmount * totalHealReductionPercentage) / 100;

            if (healAmount <= 0)
            {
                if (target is GamePlayer player)
                {
                    MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellHandler.HealSpell.HealingNullYou"), eChatType.CT_SpellResisted);
                }
            }
            else
            {
                bool applyDamnation = Spell.AmnesiaChance == 1;
                bool targetIsDamned = applyDamnation && SpellHandler.FindEffectOnTarget(target, "Damnation") != null;

                if (applyDamnation && targetIsDamned)
                {
                    int targetHarmValue = target.TempProperties.getProperty<int>("DamnationValue", 0);

                    if (targetHarmValue < 0)
                    {
                        healAmount = (healAmount * Math.Abs(targetHarmValue)) / 100;
                        if (target is GamePlayer player)
                        {
                            MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellHandler.HealSpell.TargetDamnedPartiallyHealed", Math.Abs(targetHarmValue)), eChatType.CT_SpellResisted);
                        }
                    }
                    else if (targetHarmValue == 0)
                    {
                        healAmount = 0;
                        if (target is GamePlayer player)
                        {
                            MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellHandler.HealSpell.DamnedNoHeal"), eChatType.CT_SpellResisted);
                        }
                    }
                    else if (targetHarmValue > 0)
                    {
                        int damageAmount = (healAmount * targetHarmValue) / 100;
                        target.TakeDamage(target, eDamageType.Natural, damageAmount, 0);

                        if (target is GamePlayer player)
                        {
                            MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellHandler.HealSpell.TargetDamnedDamagedYou", damageAmount), eChatType.CT_YouDied);
                        }

                        if (Caster != target && Caster is GamePlayer casterPlayer)
                        {
                            MessageToLiving(casterPlayer, LanguageMgr.GetTranslation(casterPlayer.Client, "SpellHandler.HealSpell.TargetDamnedDamaged", m_caster.GetPersonalizedName(target), damageAmount), eChatType.CT_Important);
                        }

                        healAmount = 0;
                    }
                }

                if (healAmount > 0)
                {
                    int healedAmount = target.ChangeHealth(target, GameLiving.eHealthChangeType.Spell, healAmount);

                    if (healedAmount > 0)
                    {
                        if (target is GamePlayer player)
                        {
                            string attackerName = player.GetPersonalizedName(ad.Attacker);

                            MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellHandler.HealSpell.YouAreHealed", attackerName, healedAmount), eChatType.CT_Spell);

                            if (ad.Attacker is GamePlayer attackerPlayer)
                            {
                                string targetName = attackerPlayer.GetPersonalizedName(player);
                                MessageToLiving(attackerPlayer, LanguageMgr.GetTranslation(attackerPlayer.Client, "SpellHandler.HealSpell.TargetDamageToHeal", targetName, healedAmount), eChatType.CT_Spell);
                            }
                        }
                    }
                }
            }

            int additionalHealPercent = (int)Spell.Damage;

            if (additionalHealPercent > 0)
            {
                int additionalHealAmount = (originalDamage * additionalHealPercent) / 100;

                int powerHeal = additionalHealAmount / 2;
                int endoHeal = additionalHealAmount - powerHeal;
                int replenishedPower = target.ChangeMana(target, GameLiving.eManaChangeType.Spell, powerHeal);

                if (replenishedPower > 0)
                {
                    if (target is GamePlayer player)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.OmniLifeDrain.StealPower", replenishedPower), eChatType.CT_Spell);
                    }
                }
                else
                {
                    if (target is GamePlayer player)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.OmniLifeDrain.PowerFull"), eChatType.CT_SpellResisted);
                    }
                }

                int replenishedEndo = target.ChangeEndurance(target, GameLiving.eEnduranceChangeType.Spell, endoHeal);

                if (replenishedEndo > 0)
                {
                    if (target is GamePlayer player)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.OmniLifeDrain.StealEndurance", replenishedEndo), eChatType.CT_Spell);
                    }
                }
                else
                {
                    if (target is GamePlayer player)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation(player.Client.Account.Language, "Spell.OmniLifeDrain.CannotStealEndurance"), eChatType.CT_SpellResisted);
                    }
                }
            }

            bool cancelactiveEffect = Spell.LifeDrainReturn == 1;
            GameSpellEffect activeEffect = FindEffectOnTarget(target, "MagicHealAbsorb");
            if (activeEffect != null && cancelactiveEffect)
            {
                activeEffect.Cancel(false);
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string spellValue = Spell.Value.ToString();
            string spellDamage = Spell.Damage.ToString();
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.MagicHealAbsorb.MainDescription", spellValue, spellDamage);
        }
    }
}