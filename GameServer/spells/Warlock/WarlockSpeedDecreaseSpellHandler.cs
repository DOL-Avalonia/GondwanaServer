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
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Spell handler for speed decreasing spells
    /// </summary>
    [SpellHandler("WarlockSpeedDecrease")]
    public class WarlockSpeedDecreaseSpellHandler : AbstractMorphSpellHandler
    {
        private int m_effectivenessReduction;
        
        /// <inheritdoc />
        private struct RealmTriplet
        {
            public readonly ushort Alb, Mid, Hib;
            public RealmTriplet(ushort alb, ushort mid, ushort hib) { Alb = alb; Mid = mid; Hib = hib; }
        }

        private static class MorphModels
        {
            public static readonly RealmTriplet Frog = new RealmTriplet(581, 574, 594);
            public static readonly RealmTriplet Worm = new RealmTriplet(458, 454, 457);
            public static readonly RealmTriplet Lizard = new RealmTriplet(400, 398, 399);
            public static readonly RealmTriplet Wisp = new RealmTriplet(966, 966, 966);
            public static readonly RealmTriplet Fairy = new RealmTriplet(633, 632, 630);
            public static readonly RealmTriplet Scarab1 = new RealmTriplet(669, 670, 668);
            public static readonly RealmTriplet Scarab2 = new RealmTriplet(1201, 1200, 1202);
            public static readonly RealmTriplet Spider = new RealmTriplet(129, 1597, 131);
            public static readonly RealmTriplet Cyclop = new RealmTriplet(122, 121, 120);
            public static readonly RealmTriplet Mantis = new RealmTriplet(686, 684, 685);
            public static readonly RealmTriplet Flame = new RealmTriplet(908, 907, 909);
            public static readonly RealmTriplet Bird = new RealmTriplet(2354, 2353, 2352);
            public static readonly RealmTriplet Simulacrum = new RealmTriplet(242, 243, 244);
        }

        private static RealmTriplet GetTripletByMorphType(int morphType)
        {
            return morphType switch
            {
                1 => MorphModels.Worm,
                2 => MorphModels.Lizard,
                3 => MorphModels.Wisp,
                4 => MorphModels.Fairy,
                5 => MorphModels.Scarab1,
                6 => MorphModels.Scarab2,
                7 => MorphModels.Spider,
                8 => MorphModels.Cyclop,
                9 => MorphModels.Mantis,
                10 => MorphModels.Flame,
                11 => MorphModels.Bird,
                12 => MorphModels.Simulacrum,
                _ => MorphModels.Frog, // default: frog
            };
        }

        private static ushort SelectRealmModel(eRealm realm, RealmTriplet t)
        {
            return realm switch
            {
                eRealm.Albion => t.Alb,
                eRealm.Midgard => t.Mid,
                eRealm.Hibernia => t.Hib,
                _ => (ushort)0
            };
        }

        public override ushort GetModelFor(GameLiving living)
        {
            RealmTriplet triplet = GetTripletByMorphType(Spell.ResurrectMana);
            ushort model = SelectRealmModel(living.Realm, triplet);

            if (model == 0)
                model = SelectRealmModel(living.Realm, MorphModels.Frog);

            return model;
        }

        /// <inheritdoc />
        public override bool HasPositiveOrSpeedEffect()
        {
            return true;
        }

        // constructor
        public WarlockSpeedDecreaseSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line)
        {
            Priority = 80;
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target.HasAbility(Abilities.CCImmunity) || target.HasAbility(Abilities.RootImmunity))
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.DamageImmunity", m_caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
                return false;
            }
            
            if (target.EffectList.GetOfType<AdrenalineSpellEffect>() != null)
            {
                (m_caster as GamePlayer)?.SendTranslatedMessage("Adrenaline.Target.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, m_caster.GetPersonalizedName(target));
                (target as GamePlayer)?.SendTranslatedMessage("Adrenaline.Self.Immune", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return false;
            }
            
            if (target.EffectList.GetOfType<ChargeEffect>() != null)
            {
                MessageToCaster(LanguageMgr.GetTranslation((m_caster as GamePlayer)?.Client, "SpellHandler.Target.TooFast", m_caster.GetPersonalizedName(target)), eChatType.CT_SpellResisted);
                return false;
            }
            
            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        /// <summary>
        /// When an applied effect starts
        /// duration spells only
        /// </summary>
        /// <param name="effect"></param>
        public override void OnEffectStart(GameSpellEffect effect)
        {
            effect.Owner.BuffBonusMultCategory1.Set((int)eProperty.MaxSpeed, effect, 1.0 - Spell.Value * 0.01);

            SendUpdates(effect.Owner);

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? ServerProperties.Properties.SERV_LANGUAGE;
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

            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (!(effect.Owner == player))
                {
                    string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                    string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : Spell.GetFormattedMessage2(player, personalizedTargetName);
                    player.MessageFromArea(effect.Owner, message2, eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }

            var timer = new UnbreakableSpeedDecreaseSpellHandler.RestoreSpeedTimer(effect);
            effect.Owner.TempProperties.setProperty(effect, timer);
            timer.Interval = 650;
            timer.Start(1 + (effect.Duration >> 1));

            effect.Owner.StartInterruptTimer(effect.Owner.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);

            // Optional Effectiveness debuff (Vampiir-style)
            // Use Spell.LifeDrainReturn as percentage to reduce Effectiveness by, if > 0.
            if (Spell.LifeDrainReturn > 0 && effect.Owner is GamePlayer effPlayer)
            {
                bool vampAlreadyActive = SpellHandler.FindEffectOnTarget(effPlayer, "VampiirEffectivenessDeBuff") != null;
                if (!vampAlreadyActive)
                {
                    effPlayer.BuffBonusMultCategory1.Set((int)eProperty.LivingEffectiveness, this, 1.0 - (Spell.LifeDrainReturn / 100.0));

                    effPlayer.Out.SendUpdateWeaponAndArmorStats();
                    effPlayer.Out.SendStatusUpdate();
                }
            }

            // Control effects by AmnesiaChance  1 = Silence, 2 = Disarm, 3 = both
            if (Spell.AmnesiaChance == 1 || Spell.AmnesiaChance == 3)
            {
                if (effect.Owner is GamePlayer)
                {
                    effect.Owner.SilencedCount++;
                    effect.Owner.StopCurrentSpellcast();
                    effect.Owner.StartInterruptTimer(effect.Owner.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
                }
            }

            if (Spell.AmnesiaChance == 2 || Spell.AmnesiaChance == 3)
            {
                effect.Owner.DisarmedCount++;
                effect.Owner.StopAttack();
                effect.Owner.StartInterruptTimer(effect.Owner.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);

                // NPC aggro parity with Disarm handler
                if (effect.Owner is GameNPC)
                {
                    IOldAggressiveBrain aggroBrain = ((GameNPC)effect.Owner).Brain as IOldAggressiveBrain;
                    if (aggroBrain != null)
                        aggroBrain.AddToAggroList(Caster, 1);
                }
            }

            base.OnEffectStart(effect);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);
            GameTimer timer = (GameTimer)effect.Owner.TempProperties.getProperty<object>(effect, null);
            effect.Owner.TempProperties.removeProperty(effect);
            if (timer != null) timer.Stop();

            effect.Owner.BuffBonusMultCategory1.Remove((int)eProperty.MaxSpeed, effect);

            SendUpdates(effect.Owner);

            if (!noMessages)
            {
                string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
                GamePlayer ownerPlayer = effect.Owner as GamePlayer;

                if (ownerPlayer != null)
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : Spell.GetFormattedMessage3(ownerPlayer);
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }
                else
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message3, effect.Owner.GetName(0, false));
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }

                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player))
                    {
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                        string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                        player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }

            // --- Restore Effectiveness if we changed it here ---
            if (Spell.LifeDrainReturn > 0 && effect.Owner is GamePlayer effPlayer)
            {
                if (m_effectivenessReduction > 0)
                {
                    effect.Owner.BuffBonusMultCategory1.Remove((int)eProperty.LivingEffectiveness, this);
                    effPlayer.Out.SendUpdateWeaponAndArmorStats();
                    effPlayer.Out.SendStatusUpdate();
                }
            }

            // Remove Silence/Disarm if applied
            if (Spell.AmnesiaChance is 1 or 3 && effect.Owner is GamePlayer)
            {
                effect.Owner.SilencedCount--;
            }
            if (Spell.AmnesiaChance is 2 or 3)
            {
                effect.Owner.DisarmedCount--;
            }

            base.OnEffectExpires(effect, noMessages);
            return 60000;
        }

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = base.CalculateEffectDuration(target, effectiveness);
            duration *= target.GetModified(eProperty.MythicalCrowdDuration) * 0.01;
            duration *= target.GetModified(eProperty.SpeedDecreaseDuration) * 0.01;

            if (duration < 1)
                duration = 1;
            else if (duration > (Spell.Duration * 4))
                duration = (Spell.Duration * 4);
            return (int)duration;
        }

        protected static void SendUpdates(GameLiving owner)
        {
            if (owner.IsMezzed || owner.IsStunned)
                return;

            owner.UpdateMaxSpeed();
        }

        /// <inheritdoc cref="UnbreakableSpeedDecreaseSpellHandler.GetDelveDescription"/>
        public override string GetDelveDescription(GameClient delveClient)
        {
            string description;
            int durationSeconds = Spell.Duration / 1000;
            int recastSeconds = Spell.RecastDelay / 1000;

            if (Spell.Value > 0)
            {
                if (Spell.Value >= 99)
                    description = LanguageMgr.GetTranslation(delveClient, "SpellDescription.SpeedDecrease.Rooted");
                else
                    description = LanguageMgr.GetTranslation(delveClient, "SpellDescription.SpeedDecrease.MainDescription", Spell.Value);
            }
            else
            {
                description = string.Empty;
            }

            if (Spell.LifeDrainReturn > 0)
            {
                string appearancetype = LanguageMgr.GetWarlockMorphAppearance(delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE, Spell.ResurrectMana);

                string morphText = LanguageMgr.GetTranslation(delveClient, "SpellDescription.WarlockSpeedDecrease.Frog", appearancetype);
                string vampMain = LanguageMgr.GetTranslation(delveClient, "SpellDescription.VampiirEffectivenessDeBuff.MainDescription", (int)Spell.LifeDrainReturn);
                string vampExtra = LanguageMgr.GetTranslation(delveClient, "SpellDescription.VampiirEffectivenessDeBuff.CombatCastable");

                description += "\n\n" + morphText + "\n\n" + vampMain + "\n\n" + vampExtra;
            }

            if (Spell.AmnesiaChance == 1 || Spell.AmnesiaChance == 3)
            {
                string silenceMain = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Silence.MainDescription", durationSeconds);
                description += "\n\n" + silenceMain;
            }

            if (Spell.AmnesiaChance == 2 || Spell.AmnesiaChance == 3)
            {
                string disarmMain = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription1", durationSeconds);
                description += "\n\n" + disarmMain;
            }

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

            if (Spell.RecastDelay > 0)
            {
                string recastSecond = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                description += "\n\n" + recastSecond;
            }

            if (Spell.IsSecondary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Warlock.SecondarySpell");
                description += "\n\n" + secondaryMessage;
            }

            if (Spell.IsPrimary)
            {
                string secondaryMessage = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Warlock.PrimarySpell");
                description += "\n\n" + secondaryMessage;
            }

            return description;
        }
    }
}
