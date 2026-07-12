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
        /// Checks if the target is Undead or Damned
        /// </summary>
        private bool IsUndeadOrDamned(GameLiving target)
        {
            if (target == null) return false;
            if (target.IsDamned) return true;
            if (SpellHandler.FindEffectOnTarget(target, "Damnation") != null) return true;
            if (target is GameNPC npc && npc.BodyType == (ushort)NpcTemplateMgr.eBodyType.Undead) return true;

            return false;
        }

        /// <summary>
        /// Is this a GHOST entity that lacks an Undead bodytype?
        /// </summary>
        private static bool IsInvalidGhostTarget(GameLiving target)
        {
            if (target is GameNPC npc)
            {
                bool isGhost = (npc.Flags & GameNPC.eFlags.GHOST) != 0;
                bool isUndead = npc.BodyType == (ushort)NpcTemplateMgr.eBodyType.Undead;

                if (isGhost && !isUndead) return true;
            }
            return false;
        }

        /// <summary>
        /// Intercept pre-cast targeting. If DB defines spell as "enemy", but caster is 
        /// targeting an allied Undead, we force-approve the cast.
        /// </summary>
        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (IsInvalidGhostTarget(selectedTarget))
            {
                if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.NoEffect", selectedTarget), eChatType.CT_SpellResisted);
                return false;
            }

            if (IsUndeadOrDamned(selectedTarget) && !GameServer.ServerRules.IsAllowedToAttack(Caster, selectedTarget, true))
            {
                if (!Caster.IsAlive || (selectedTarget != null && !selectedTarget.IsAlive)) return false;

                if (selectedTarget != null && !Caster.IsWithinRadius(selectedTarget, CalculateSpellRange()))
                {
                    if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetTooFar"), eChatType.CT_SpellResisted);
                    return false;
                }

                if (!CheckHasPower(selectedTarget, quiet)) return false;

                return true;
            }

            return base.CheckBeginCast(selectedTarget, quiet);
        }

        /// <summary>
        /// By default, DirectDamage checks for resists. Since this is "Self" cast or intended 
        /// to help a Damned player, we must force the resist chance to 0.
        /// </summary>
        public override int CalculateSpellResistChance(GameLiving target)
        {
            if (IsInvalidGhostTarget(target)) return 100;
            if (target == Caster || IsUndeadOrDamned(target)) return 0;

            return base.CalculateSpellResistChance(target);
        }

        /// <summary>
        /// Ensure we don't "miss" the spell due to low stats or level differences.
        /// </summary>
        public override int CalculateToHitChance(GameLiving target)
        {
            if (IsInvalidGhostTarget(target)) return 0;
            if (target == Caster || IsUndeadOrDamned(target)) return 100;

            return base.CalculateToHitChance(target);
        }

        /// <summary>
        /// Scales base output for both friendly heals and enemy nukes.
        /// </summary>
        public override AttackData CalculateDamageToTarget(GameLiving target, double effectiveness)
        {
            int damnationEnhancement = Caster.GetModified(eProperty.DamnationEffectEnhancement);

            if (damnationEnhancement > 0)
            {
                effectiveness += (damnationEnhancement * 0.01);
            }

            return base.CalculateDamageToTarget(target, effectiveness);
        }

        /// <summary>
        /// Main logic execution. We override DealDamage to bifurcate logic based on target state.
        /// </summary>
        protected override void DealDamage(GameLiving target, double effectiveness)
        {
            if (target == null || !target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active)
                return;

            if (IsInvalidGhostTarget(target))
                return;

            if (IsUndeadOrDamned(target))
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
                        double extendedSeconds = Spell.Value;
                        int damnationEnhancement = Caster.GetModified(eProperty.DamnationEffectEnhancement);

                        if (damnationEnhancement > 0)
                        {
                            extendedSeconds *= (1.0 + (damnationEnhancement * 0.01));
                        }

                        int addedTimeMs = (int)(extendedSeconds * 1000);
                        damnationEffect.AddRemainingTime(addedTimeMs);

                        double displaySecs = Math.Round(extendedSeconds, 1);
                        MessageToLiving(target, T(target, "SpellHandler.ZombieHeal.DecayExtendedTarget", displaySecs), eChatType.CT_Spell);
                        if (Caster != target)
                        {
                            MessageToCaster(T(Caster, "SpellHandler.ZombieHeal.DecayExtendedCaster", target.Name, displaySecs), eChatType.CT_Spell);
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
                bool casterIsDamned = Caster.IsDamned || SpellHandler.FindEffectOnTarget(Caster, "Damnation") != null || (Caster is GameNPC cNpc && cNpc.BodyType == (ushort)NpcTemplateMgr.eBodyType.Undead);

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