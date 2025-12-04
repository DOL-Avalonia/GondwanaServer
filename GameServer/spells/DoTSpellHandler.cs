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
using DOL.GS.Effects;
using DOL.Language;
using DOL.AI.Brain;
using DOL.GS.PlayerClass;

namespace DOL.GS.Spells
{
    [SpellHandler("DamageOverTime")]
    public class DoTSpellHandler : SpellHandler
    {
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        public override double GetLevelModFactor()
        {
            return 0;
        }

        protected override double CalculateAreaVariance(GameLiving target, float distance, int radius)
        {
            return 0;
        }

        public override bool IsOverwritable(GameSpellEffect compare)
        {
            return Spell.SpellType == compare.Spell.SpellType && Spell.DamageType == compare.Spell.DamageType && SpellLine.IsBaseLine == compare.SpellHandler.SpellLine.IsBaseLine;
        }

        /// <inheritdoc />
        public override bool HasPositiveEffect => false;

        public override AttackData CalculateDamageToTarget(GameLiving target, double effectiveness)
        {
            AttackData ad = base.CalculateDamageToTarget(target, effectiveness);
            int bonus = Caster.GetModified(eProperty.DotDamageBonus);
            int critChance = Caster.GetModified(eProperty.CriticalDotHitChance);
            
            ad.AttackType = AttackData.eAttackType.DoT;
            if (bonus != 0)
            {
                ad.Damage += (int)Math.Ceiling(ad.Damage * 0.01 * bonus);
            }
            if (this.SpellLine.KeyName == GlobalSpellsLines.Mundane_Poisons)
            {
                RealmAbilities.L3RAPropertyEnhancer ra = Caster.GetAbility<RealmAbilities.ViperAbility>();
                if (ra != null)
                {
                    int additional = (int)((float)ad.Damage * ((float)ra.Amount / 100));
                    ad.Damage += additional;
                }
            }

            GameSpellEffect iWarLordEffect = SpellHandler.FindEffectOnTarget(target, "CleansingAura");
            if (iWarLordEffect != null)
                ad.Damage *= (int)(1.00 - (iWarLordEffect.Spell.Value * 0.01));
            
            if (Util.Chance(critChance) && (ad.Damage >= 1))
            {
                int critMax = (ad.Target is GamePlayer) ? ad.Damage / 2 : ad.Damage;
                ad.CriticalDamage = Util.Random(ad.Damage / 10, critMax);
            }
            else
            {
                ad.CriticalDamage = 0;
            }
            
            ad.AttackType = AttackData.eAttackType.DoT;
            return ad;
        }

        public override void CalculateDamageVariance(GameLiving target, out double min, out double max)
        {
            int speclevel = 1;
            min = 1.13;
            max = 1.13;

            if (m_spellLine.KeyName == GlobalSpellsLines.Mundane_Poisons)
            {
                speclevel = m_caster.GetModifiedSpecLevel(Specs.Envenom);
                min = 1.25;
                max = 1.25;

                if (target.Level > 0)
                {
                    min = 0.25 + (speclevel - 1) / (double)target.Level;
                }
            }
            else
            {
                speclevel = m_caster.GetModifiedSpecLevel(m_spellLine.Spec);

                if (target.Level > 0)
                {
                    min = 0.13 + (speclevel - 1) / (double)target.Level;
                }
            }

            // no overspec bonus for dots

            if (min > max) min = max;
            if (min < 0) min = 0;
        }

        public override void SendDamageMessages(AttackData ad)
        {
            // Graveen: only GamePlayer should receive messages :p
            GamePlayer PlayerReceivingMessages = null;
            if (m_caster is GamePlayer casterPlayer)
                PlayerReceivingMessages = casterPlayer;
            else if (m_caster is GamePet { Brain: IControlledBrain { Owner: GamePlayer owningPlayer } })
                PlayerReceivingMessages = owningPlayer;
            else
                return;


            if (Caster is GamePlayer && (ad.Target as GameNPC)?.IsInvincible(ad.DamageType) == true)
            {
                ad.Damage = 0;
                ad.CriticalDamage = 0;
            }

            string targetName = m_caster.GetPersonalizedName(ad.Target);

            if (Spell.Name.StartsWith("Proc"))
            {
                MessageToCaster(string.Format(LanguageMgr.GetTranslation(PlayerReceivingMessages.Client, "DoTSpellHandler.SendDamageMessages.YouHitFor", targetName, ad.Damage)), eChatType.CT_YouHit);
            }
            else
            {
                MessageToCaster(string.Format(LanguageMgr.GetTranslation(PlayerReceivingMessages.Client, "DoTSpellHandler.SendDamageMessages.YourHitsFor", Spell.Name, targetName, ad.Damage)), eChatType.CT_YouHit);
            }

            if (ad.CriticalDamage > 0)
                MessageToCaster(string.Format(LanguageMgr.GetTranslation(PlayerReceivingMessages.Client, "DoTSpellHandler.SendDamageMessages.YourCriticallyHits", Spell.Name, targetName, ad.CriticalDamage)), eChatType.CT_YouHit);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.EffectList.GetOfType<AdrenalineSpellEffect>() != null)
            {
                (m_caster as GamePlayer)?.SendTranslatedMessage("Adrenaline.Target.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                (target as GamePlayer)?.SendTranslatedMessage("Adrenaline.Self.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return true;
            }
            if (!base.ApplyEffectOnTarget(target, effectiveness))
                return false;
           
            target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            return true;
        }


        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            int duration = m_spell.Duration;
            int reduction = target.GetModified(eProperty.DotDurationDecrease);

            if (target is GamePlayer { Guild: not null } targetPlayer)
            {
                reduction += targetPlayer.Guild.GetDebuffDurationReduction(this);
            }

            if (reduction != 0)
                duration -= (int)Math.Round(0.01 * reduction * duration);

            if (duration < 1)
                duration = 1;
            // damage is not reduced with distance
            return new GameSpellEffect(this, duration, m_spell.Frequency, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            SendEffectAnimation(effect.Owner, 0, false, 1);
        }

        public override void OnEffectPulse(GameSpellEffect effect)
        {
            base.OnEffectPulse(effect);

            if (effect.Owner.IsAlive)
            {
                string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
                GamePlayer ownerPlayer = effect.Owner as GamePlayer;
                string targetName = effect.Owner.GetName(0, false);

                if (ownerPlayer != null)
                {
                    string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : Spell.GetFormattedMessage1(ownerPlayer);
                    MessageToLiving(effect.Owner, message1, eChatType.CT_Spell);
                }
                else
                {
                    string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message1, targetName);
                    MessageToLiving(effect.Owner, message1, eChatType.CT_Spell);
                }

                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player))
                    {
                        // Use the player's perspective to get the personalized name of the effect owner
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                        if (ownerPlayer != null)
                        {
                            string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : Spell.GetFormattedMessage2(player, personalizedTargetName);
                            player.MessageFromArea(effect.Owner, message2, eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message2, personalizedTargetName);
                            player.MessageFromArea(effect.Owner, message2, eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                        }
                    }
                }

                OnDirectEffect(effect.Owner, effect.Effectiveness);
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);
            if (!noMessages)
            {
                string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
                GamePlayer ownerPlayer = effect.Owner as GamePlayer;
                string targetName = effect.Owner.GetName(0, false);

                if (ownerPlayer != null)
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : Spell.GetFormattedMessage3(ownerPlayer);
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }
                else
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message3, targetName);
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }

                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player))
                    {
                        // Use the player's perspective to get the personalized name of the effect owner
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                        if (ownerPlayer != null)
                        {
                            string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                            player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message4, personalizedTargetName);
                            player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                        }
                    }
                }
            }
            return 0;
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null) return false;
            if (!target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active) return false;

            // no interrupts on DoT direct effect
            // calc damage
            AttackData ad = CalculateDamageToTarget(target, effectiveness);

            // Attacked living may modify the attack data.
            ad.Target.ModifyAttack(ad);

            DamageTarget(ad, false);
            SendDamageMessages(ad);
            return true;
        }

        public DoTSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string description = LanguageMgr.GetTranslation(delveClient, "SpellDescription.DoT.MainDescription", Spell.Damage, LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType), Spell.Frequency / 1000.0);

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
                string secondaryMessage = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Warlock.SecondarySpell");
                if (!string.IsNullOrEmpty(secondaryMessage))
                    description += "\n\n" + secondaryMessage;
            }

            if (Spell.IsPrimary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Warlock.PrimarySpell");
                description += "\n\n" + secondaryMessage;
            }

            switch (Spell.ID)
            {
                case 25206:
                case 25207:
                case 25208:
                case 25209:
                case 25210:
                    {
                        var viewer = delveClient?.Player;
                        if (viewer?.CharacterClass is ClassOccultist)
                        {
                            description += "\n\n" + LanguageMgr.GetTranslation(delveClient, "SpellDescription.Occultist.ConditionDescription2");
                        }
                        break;
                    }
            }

            return description;
        }
    }
}
