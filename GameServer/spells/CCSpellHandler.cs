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
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Events;
using DOL.GS.RealmAbilities;
using DOL.Territories;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    public abstract class AbstractCCSpellHandler : ImmunityEffectSpellHandler
    {
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.HasAbility(Abilities.CCImmunity))
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DamageImmunity", m_caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
                return true;
            }
            if (target.EffectList.GetOfType<AdrenalineSpellEffect>() != null)
            {
                (m_caster as GamePlayer)?.SendTranslatedMessage("Adrenaline.Target.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                (target as GamePlayer)?.SendTranslatedMessage("Adrenaline.Self.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return true;
            }
            if (target.EffectList.GetOfType<ChargeEffect>() != null || target.TempProperties.getProperty("Charging", false))
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.Target.TooFast", m_caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
                return true;
            }

            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer ownerPlayer = effect.Owner as GamePlayer;

            if (ownerPlayer != null)
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : Spell.GetFormattedMessage1(ownerPlayer);
                MessageToLiving(effect.Owner, message1, eChatType.CT_Spell);
            }
            else
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message1, effect.Owner.GetName(0, false));
                MessageToLiving(effect.Owner, message1, eChatType.CT_Spell);
            }

            if (m_caster is GamePlayer casterPlayer)
            {
                string messageToCaster = Spell.GetFormattedMessage2(casterPlayer, effect.Owner.GetName(0, false));
                MessageToCaster(messageToCaster, eChatType.CT_Spell);
            }

            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (!(effect.Owner == player || m_caster == player))
                {
                    string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                    string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : Spell.GetFormattedMessage2(player, personalizedTargetName);
                    player.MessageFromArea(effect.Owner, message2, eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }

            // If owner is a player, update their max speed and group status
            if (ownerPlayer != null)
            {
                ownerPlayer.Client.Out.SendUpdateMaxSpeed();
                if (ownerPlayer.Group != null)
                    ownerPlayer.Group.UpdateMember(ownerPlayer, false, false);
            }
            else
            {
                effect.Owner.StopAttack();
            }

            effect.Owner.Notify(GameLivingEvent.CrowdControlled, effect.Owner);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (effect.Owner == null) return 0;

            base.OnEffectExpires(effect, noMessages);

            GamePlayer player = effect.Owner as GamePlayer;

            if (player != null)
            {
                player.Client.Out.SendUpdateMaxSpeed();
                if (player.Group != null)
                    player.Group.UpdateMember(player, false, false);
            }
            else
            {
                GameNPC npc = effect.Owner as GameNPC;
                if (npc != null)
                {
                    IOldAggressiveBrain aggroBrain = npc.Brain as IOldAggressiveBrain;
                    if (aggroBrain != null)
                        aggroBrain.AddToAggroList(Caster, 1);
                }
            }

            effect.Owner.Notify(GameLivingEvent.CrowdControlExpired, effect.Owner);

            return (effect.Name == "Pet Stun") ? 0 : 60000;
        }

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = base.CalculateEffectDuration(target, effectiveness);
            double mocFactor = 1.0;
            MasteryofConcentrationEffect moc = Caster.EffectList.GetOfType<MasteryofConcentrationEffect>();
            if (moc != null)
            {
                MasteryofConcentrationAbility ra = Caster.GetAbility<MasteryofConcentrationAbility>();
                if (ra != null)
                    mocFactor += ra.GetAmountForLevel(ra.Level) / 100.0;
                duration = (double)Math.Round(duration * mocFactor);
            }


            if (!Spell.SpellType.ToLower().StartsWith("style"))
            {
                // capping duration adjustment to 100%, live cap unknown - Tolakram
                int hitChance = Math.Min(200, CalculateToHitChance(target));

                if (hitChance <= 0)
                {
                    duration = 0;
                }
                else if (hitChance < 55)
                {
                    duration -= (int)(duration * (55 - hitChance) * 0.01);
                }
                else if (hitChance > 100)
                {
                    duration += (int)(duration * (hitChance - 100) * 0.01);
                }
                duration *= target.GetModified(eProperty.MythicalCrowdDuration) * 0.01;
            }


            return (int)duration;
        }

        public override int CalculateSpellResistChance(GameLiving target)
        {
            if (RollBossCCResist(target))
                return 100;

            int resistvalue = 0;
            int resist = 0;
            GameSpellEffect fury = SpellHandler.FindEffectOnTarget(target, "Fury");
            if (fury != null)
            {
                resist += (int)fury.Spell.Value;
            }

            //bonedancer rr5
            if (target.EffectList.GetOfType<AllureofDeathEffect>() != null)
            {
                return AllureofDeathEffect.ccchance;
            }

            if (m_spellLine.KeyName == GlobalSpellsLines.Combat_Styles_Effect)
                return 0;
            if (HasPositiveEffect)
                return 0;

            int hitchance = CalculateToHitChance(target);

            //Calculate the Resistchance
            resistvalue = (100 - hitchance + resist);
            if (resistvalue > 100)
                resistvalue = 100;
            //use ResurrectHealth=1 if the CC should not be resisted
            if (Spell.ResurrectHealth == 1) resistvalue = 0;
            //always 1% resistchance!
            else if (resistvalue < 1)
                resistvalue = 1;
            return resistvalue;
        }

        public AbstractCCSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("Mesmerize")]
    public class MesmerizeSpellHandler : AbstractCCSpellHandler
    {
        public override void OnEffectPulse(GameSpellEffect effect)
        {
            SendEffectAnimation(effect.Owner, 0, false, 1);
            base.OnEffectPulse(effect);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            effect.Owner.IsMezzed = true;
            effect.Owner.StopAttack();
            effect.Owner.StopCurrentSpellcast();
            effect.Owner.DisableTurning(true);
            effect.Owner.TempProperties.removeProperty(GamePlayer.PLAYER_MEZZED_BY_OTHER_PLAYER_ID);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttacked));
            base.OnEffectStart(effect);
        }

        protected override double CalculateAreaVariance(GameLiving target, float distance, int radius)
        {
            if (target is GamePlayer || (target is GameNPC && (target as GameNPC)!.Brain is IControlledBrain))
            {
                return ((double)distance / (double)radius) / 2.0;
            }

            return 0;
        }

        public override void OnSpellResisted(GameLiving target)
        {
            if (this.Spell.Pulse > 0)
            {
                if (target != null && (!target.IsAlive))
                {
                    GameSpellEffect effect = SpellHandler.FindEffectOnTarget(target, this);
                    if (effect != null)
                    {
                        effect.Cancel(false);//call OnEffectExpires
                        CancelPulsingSpell(Caster, this.Spell.SpellType);
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.StopPlayingSong"), eChatType.CT_Spell);
                    }
                    return;
                }

                if (this.Spell.Range != 0)
                {
                    if (!Caster.IsWithinRadius(target, this.Spell.Range))
                        return;
                }

                if (target != Caster.TargetObject)
                    return;
            }

            GameSpellEffect mezz = SpellHandler.FindEffectOnTarget(target, "Mesmerize");
            if (mezz != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetAlreadyMezz"), eChatType.CT_SpellResisted);
                return;
            }

            lock (target.EffectList)
            {
                foreach (IGameEffect effect in target.EffectList)
                {
                    if (effect is GameSpellEffect)
                    {
                        GameSpellEffect gsp = (GameSpellEffect)effect;
                        if (gsp is GameSpellAndImmunityEffect)
                        {
                            GameSpellAndImmunityEffect immunity = (GameSpellAndImmunityEffect)gsp;
                            if (immunity.ImmunityState
                                && target == immunity.Owner)
                            {
                                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.TargetCantHaveEffectAgainYet", m_caster.GetPersonalizedName(immunity.Owner)), eChatType.CT_SpellPulse);
                                return;
                            }
                        }
                    }
                }
            }
            SendEffectAnimation(target, 0, false, 0);
            MessageToCaster(LanguageMgr.GetTranslation((target as GamePlayer)?.Client, "SpellHandler.TargetResistsEffect", target.GetName(0, true)), eChatType.CT_SpellResisted);
            target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttacked));
            effect.Owner.IsMezzed = false;
            effect.Owner.DisableTurning(false);
            return base.OnEffectExpires(effect, noMessages);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (Spell.Pulse > 0)
            {
                if (Caster.IsWithinRadius(target, this.Spell.Range * 5) == false)
                {
                    CancelPulsingSpell(Caster, this.Spell.SpellType);
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TooFarStopPlayingSong"), eChatType.CT_Spell);
                    return false;
                }

                if (target is { IsAlive: false })
                {
                    GameSpellEffect effect = SpellHandler.FindEffectOnTarget(target, this);
                    if (effect != null)
                    {
                        effect.Cancel(false);//call OnEffectExpires
                        CancelPulsingSpell(Caster, this.Spell.SpellType);
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.StopPlayingSong"), eChatType.CT_Spell);
                    }
                    return false;
                }

                if (target != Caster.TargetObject)
                    return false;

                if (this.Spell.Range != 0)
                {
                    if (!Caster.IsWithinRadius(target, this.Spell.Range))
                        return false;
                }

            }

            if (target.HasAbility(Abilities.MezzImmunity))
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DamageImmunity", m_caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
                SendEffectAnimation(target, 0, false, 0);
                target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
                return true;
            }
            
            if (FindStaticEffectOnTarget(target, typeof(MezzRootImmunityEffect)) != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.TargetImmune"), eChatType.CT_System);
                SendEffectAnimation(target, 0, false, 0);
                target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
                return true;
            }
            //Do nothing when already mez, but inform caster
            GameSpellEffect mezz = SpellHandler.FindEffectOnTarget(target, "Mesmerize");
            if (mezz != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.TargetAlreadyMezz"), eChatType.CT_SpellResisted);
                //				SendEffectAnimation(target, 0, false, 0);
                return true;
            }

            var targetPlayer = target as GamePlayer;
            if (Caster is GamePlayer && targetPlayer != null && m_spell.SpellType.ToLowerInvariant().Equals("mesmerize"))
            {
                if (!(targetPlayer.isInBG || targetPlayer.CurrentRegion.IsRvR || targetPlayer.IsInPvP || TerritoryManager.GetCurrentTerritory(targetPlayer) != null))
                {
                    targetPlayer.TempProperties.setProperty(GamePlayer.PLAYER_MEZZED_BY_OTHER_PLAYER_ID, Caster.InternalID);
                }
            }

            GameSpellEffect mezblock = SpellHandler.FindEffectOnTarget(target, "CeremonialBracerMezz");
            if (mezblock != null)
            {
                mezblock.Cancel(false);
                if (target is GamePlayer)
                    (target as GamePlayer)?.Out.SendMessage(LanguageMgr.GetTranslation((target as GamePlayer)?.Client, "SpellHandler.MezIntercepted"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.CeremonialBracerInterceptMez"), eChatType.CT_SpellResisted);
                SendEffectAnimation(target, 0, false, 0);
                target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
                return true;
            }

            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = base.CalculateEffectDuration(target, effectiveness);
            duration *= target.GetModified(eProperty.MesmerizeDuration) * 0.01;
            if (duration < 1)
                duration = 1;
            else if (duration > (Spell.Duration * 4))
                duration = (Spell.Duration * 4);
            return (int)duration;
        }

        protected virtual void OnAttacked(DOLEvent e, object sender, EventArgs arguments)
        {
            AttackedByEnemyEventArgs attackArgs = arguments as AttackedByEnemyEventArgs;
            GameLiving living = sender as GameLiving;
            if (attackArgs == null) return;
            if (living == null) return;

            bool remove = false;

            if (attackArgs.AttackData is { AttackType: not AttackData.eAttackType.Spell and not AttackData.eAttackType.DoT })
            {
                switch (attackArgs.AttackData.AttackResult)
                {
                    case GameLiving.eAttackResult.HitStyle:
                    case GameLiving.eAttackResult.HitUnstyled:
                    case GameLiving.eAttackResult.Blocked:
                    case GameLiving.eAttackResult.Evaded:
                    case GameLiving.eAttackResult.Fumbled:
                    case GameLiving.eAttackResult.Missed:
                    case GameLiving.eAttackResult.Parried:
                        remove = true;
                        break;
                }
            }
            //If the spell was resisted - then we don't break mezz
            else if (!attackArgs.AttackData.IsSpellResisted)
            {
                //temporary fix for DirectDamageDebuff not breaking mez
                if (attackArgs.AttackData.SpellHandler is PropertyChangingSpell && attackArgs.AttackData.SpellHandler.HasPositiveEffect == false && attackArgs.AttackData.Damage > 0)
                    remove = true;
                //debuffs/shears dont interrupt mez, neither does recasting mez
                else if (attackArgs.AttackData.SpellHandler is PropertyChangingSpell || attackArgs.AttackData.SpellHandler is MesmerizeSpellHandler
                         || attackArgs.AttackData.SpellHandler is NearsightSpellHandler || attackArgs.AttackData.SpellHandler.HasPositiveEffect) return;

                if (attackArgs.AttackData.AttackResult == GameLiving.eAttackResult.Missed || attackArgs.AttackData.AttackResult == GameLiving.eAttackResult.HitUnstyled)
                    remove = true;
            }

            if (remove)
            {
                GameSpellEffect effect = SpellHandler.FindEffectOnTarget(living, this);
                if (effect != null)
                    effect.Cancel(false);//call OnEffectExpires
            }
        }

        public MesmerizeSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.Mesmerize.MainDescription");
        }
    }

    [SpellHandler("Stun")]
    public class StunSpellHandler : AbstractCCSpellHandler
    {
        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            //use ResurrectMana=1 if the Stun should not have immunity
            if (Spell.ResurrectMana == 1)
            {
                int freq = Spell != null ? Spell.Frequency : 0;
                return new GameSpellEffect(this, CalculateEffectDuration(target, effectiveness), freq, effectiveness);
            }
            else return new GameSpellAndImmunityEffect(this, CalculateEffectDuration(target, effectiveness), 0, effectiveness);
        }

        protected override double CalculateAreaVariance(GameLiving target, float distance, int radius)
        {
            if (target is GamePlayer || (target is GameNPC && (target as GameNPC)!.Brain is IControlledBrain))
            {
                return ((double)distance / (double)radius) / 2.0;
            }

            return 0;
        }


        public override void OnEffectStart(GameSpellEffect effect)
        {
            effect.Owner.IsStunned = true;
            effect.Owner.StopAttack();
            effect.Owner.StopCurrentSpellcast();
            effect.Owner.DisableTurning(true);
            base.OnEffectStart(effect);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            effect.Owner.IsStunned = false;
            effect.Owner.DisableTurning(false);
            //use ResurrectHealth>0 to calculate stun immunity timer (such pet stun spells), actually (1.90) pet stun immunity is 5x the stun duration
            if (Spell.ResurrectHealth > 0)
            {
                base.OnEffectExpires(effect, noMessages);
                return Spell.Duration * Spell.ResurrectHealth;
            }
            return base.OnEffectExpires(effect, noMessages);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.HasAbility(Abilities.StunImmunity))
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DamageImmunity", m_caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
                base.OnSpellResisted(target);
                return true;
            }
            //Ceremonial bracer dont intercept physical stun
            if (Spell.SpellType.ToLower() != "stylestun")
            {
                GameSpellEffect stunblock = SpellHandler.FindEffectOnTarget(target, "CeremonialBracerStun");
                if (stunblock != null)
                {
                    stunblock.Cancel(false);
                    if (target is GamePlayer)
                        (target as GamePlayer)?.Out.SendMessage(LanguageMgr.GetTranslation((target as GamePlayer)?.Client, "SpellHandler.StunIntercepted"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    base.OnSpellResisted(target);
                    return true;
                }
            }
            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = base.CalculateEffectDuration(target, effectiveness);
            duration *= target.GetModified(eProperty.StunDuration) * 0.01;

            if (duration < 1)
                duration = 1;
            else if (duration > (Spell.Duration * 4))
                duration = (Spell.Duration * 4);
            return (int)duration;
        }

        public override bool IsOverwritable(GameSpellEffect compare)
        {
            if (Spell.EffectGroup != 0 || compare.Spell.EffectGroup != 0)
                return Spell.EffectGroup == compare.Spell.EffectGroup;
            if (compare.Spell.SpellType == "StyleStun") return true;
            return base.IsOverwritable(compare);
        }

        public StunSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.Stun.MainDescription", Spell.Duration / 1000.0f);
        }
    }
}
