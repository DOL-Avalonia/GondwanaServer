using System;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOL.GS.Scripts;
using System.Text;

namespace DOL.GS.Spells
{
    [SpellHandler("ZombieHeal")]
    public class ZombieHealSpellHandler : DirectDamageSpellHandler
    {
        public ZombieHealSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        private static string T(GameClient client, string key, params object[] args)
        {
            return LanguageMgr.GetTranslation(client, key, args);
        }

        private static string T(GameLiving living, string key, params object[] args)
        {
            return T((living as GamePlayer)?.Client, key, args);
        }

        /// <summary>
        /// By default, DirectDamage checks for resists. Since this is "Self" cast or intended 
        /// to help a Damned player, we must force the resist chance to 0.
        /// </summary>
        public override int CalculateSpellResistChance(GameLiving target)
        {
            if (target == Caster || (target != null && SpellHandler.FindEffectOnTarget(target, "Damnation") != null))
            {
                return 0;
            }
            return base.CalculateSpellResistChance(target);
        }

        /// <summary>
        /// Ensure we don't "miss" the spell due to low stats or level differences.
        /// </summary>
        public override int CalculateToHitChance(GameLiving target)
        {
            if (target == Caster || (target != null && SpellHandler.FindEffectOnTarget(target, "Damnation") != null))
            {
                return 100;
            }
            return base.CalculateToHitChance(target);
        }

        /// <summary>
        /// Main logic execution. We override DealDamage to bifurcate logic based on target state.
        /// </summary>
        protected override void DealDamage(GameLiving target, double effectiveness)
        {
            if (target == null || !target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active)
                return;

            bool targetIsDamned = SpellHandler.FindEffectOnTarget(target, "Damnation") != null;

            if (targetIsDamned)
            {
                // Calculate Heal Amount based on Spell.Damage for damned players,
                // but we might want to ignore defensive resists since this is a "friendly" heal.
                AttackData ad = CalculateDamageToTarget(target, effectiveness);
                int healAmount = ad.Damage;

                if (healAmount > 0)
                {
                    int actualHealed = target.ChangeHealth(Caster, GameLiving.eHealthChangeType.Spell, healAmount);

                    MessageToCaster(T(Caster, "SpellHandler.HealSpell.TargetHealed", Caster.GetPersonalizedName(target), actualHealed), eChatType.CT_Spell);

                    if (Caster != target)
                    {
                        MessageToLiving(target, T(target, "SpellHandler.HealSpell.YouAreHealed", target.GetPersonalizedName(Caster), actualHealed), eChatType.CT_Spell);
                    }
                    else
                    {
                        MessageToLiving(target, T(target, "SpellHandler.ZombieHeal.SelfHeal", actualHealed), eChatType.CT_Spell);
                    }

                    SendEffectAnimation(target, 0, false, 1);
                }

                if (Spell.Value > 0)
                {
                    var damnationEffect = SpellHandler.FindEffectOnTarget(target, "Damnation");
                    if (damnationEffect != null)
                    {
                        int addedTime = (int)Spell.Value * 1000;
                        damnationEffect.AddRemainingTime(addedTime);

                        MessageToLiving(target, T(target, "SpellHandler.ZombieHeal.DecayExtendedTarget", Spell.Value), eChatType.CT_Spell);
                        if (Caster != target)
                        {
                            MessageToCaster(T(Caster, "SpellHandler.ZombieHeal.DecayExtendedCaster", target.Name, Spell.Value), eChatType.CT_Spell);
                        }
                    }
                }

                if (Spell.SubSpellID > 0)
                {
                    Spell subSpell = SkillBase.GetSpellByID((int)Spell.SubSpellID);
                    if (subSpell != null)
                    {
                        ISpellHandler subHandler = ScriptMgr.CreateSpellHandler(Caster, subSpell, SpellLine);
                        if (subHandler != null)
                        {
                            subHandler.StartSpell(target);
                        }
                    }
                }
            }
            else
            {
                // We call the base DirectDamageSpellHandler to handle damage calculation for non-damned players,
                // resists, shields, and interaction messages.
                base.DealDamage(target, effectiveness);
                bool casterIsDamned = SpellHandler.FindEffectOnTarget(Caster, "Damnation") != null;

                if (!casterIsDamned && Spell.AmnesiaChance > 0)
                {
                    int penaltySpellID = Spell.AmnesiaChance;
                    Spell penaltySpell = SkillBase.GetSpellByID(penaltySpellID);

                    if (penaltySpell != null)
                    {
                        ISpellHandler penaltyHandler = ScriptMgr.CreateSpellHandler(Caster, penaltySpell, SpellLine);
                        if (penaltyHandler != null)
                        {
                            penaltyHandler.StartSpell(Caster);
                        }
                    }
                }
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            var sb = new StringBuilder();
            string dmgType = LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType);

            if (Spell.Damage > 0 && Spell.Value > 0)
            {
                sb.AppendLine(T(delveClient, "SpellDescription.ZombieHeal.MainDescription.HealAndDecay", Spell.Damage, Spell.Value, dmgType));
            }
            else if (Spell.Damage > 0 && Spell.Value == 0)
            {
                sb.AppendLine(T(delveClient, "SpellDescription.ZombieHeal.MainDescription.HealOnly", Spell.Damage, dmgType));
            }
            else if (Spell.Damage == 0 && Spell.Value > 0)
            {
                sb.AppendLine(T(delveClient, "SpellDescription.ZombieHeal.MainDescription.DecayOnly", Spell.Value));
            }

            if (Spell.SubSpellID > 0)
            {
                Spell subSpell = SkillBase.GetSpellByID((int)Spell.SubSpellID);
                if (subSpell != null)
                {
                    sb.AppendLine();
                    sb.AppendLine(T(delveClient, "SpellDescription.ZombieHeal.Header.BonusEffect")); // "Bonus Effect (Damned Target Only)"

                    ISpellHandler subHandler = ScriptMgr.CreateSpellHandler(Caster, subSpell, null);
                    if (subHandler != null)
                    {
                        sb.AppendLine(subHandler.GetDelveDescription(delveClient));
                    }
                    else
                    {
                        sb.AppendLine(subSpell.Description);
                    }
                }
            }

            if (Spell.AmnesiaChance > 0)
            {
                Spell penaltySpell = SkillBase.GetSpellByID(Spell.AmnesiaChance);
                if (penaltySpell != null)
                {
                    sb.AppendLine();
                    sb.AppendLine(T(delveClient, "SpellDescription.ZombieHeal.Header.SideEffect")); // "Side Effect (If Caster is NOT Damned)"

                    ISpellHandler penaltyHandler = ScriptMgr.CreateSpellHandler(Caster, penaltySpell, null);
                    if (penaltyHandler != null)
                    {
                        sb.AppendLine(penaltyHandler.GetDelveDescription(delveClient));
                    }
                    else
                    {
                        sb.AppendLine(penaltySpell.Description);
                    }
                }
            }

            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            if (Spell.IsSecondary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(language, "SpellDescription.Warlock.SecondarySpell");
                sb.AppendLine().AppendLine(secondaryMessage);
            }

            if (Spell.IsPrimary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(language, "SpellDescription.Warlock.PrimarySpell");
                sb.AppendLine().AppendLine(secondaryMessage);
            }

            return sb.ToString();
        }
    }
}