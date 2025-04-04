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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DOL.Database;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;

namespace DOL.GS.Spells
{
    // Melee ablative
    [SpellHandlerAttribute("AblativeArmor")]
    public class AblativeArmorSpellHandler : SpellHandler
    {
        public const string ABLATIVE_HP = "ablative hp";

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            effect.Owner.TempProperties.setProperty(ABLATIVE_HP, (int)Spell.Value);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));

            eChatType toLiving = (Spell.Pulse == 0) ? eChatType.CT_Spell : eChatType.CT_SpellPulse;
            eChatType toOther = (Spell.Pulse == 0) ? eChatType.CT_System : eChatType.CT_SpellPulse;

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer ownerPlayer = effect.Owner as GamePlayer;

            if (ownerPlayer != null)
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : Spell.GetFormattedMessage1(ownerPlayer);
                MessageToLiving(effect.Owner, message1, toLiving);
            }
            else
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message1, effect.Owner.GetName(0, false));
                MessageToLiving(effect.Owner, message1, toLiving);
            }

            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (effect.Owner != player)
                {
                    string personalizedTargetName = player.GetPersonalizedName(effect.Owner);
                    string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : Spell.GetFormattedMessage2(player, personalizedTargetName);
                    player.MessageFromArea(effect.Owner, message2, toOther, eChatLoc.CL_SystemWindow);
                }
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));
            effect.Owner.TempProperties.removeProperty(ABLATIVE_HP);

            if (!noMessages && Spell.Pulse == 0)
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
                    if (effect.Owner != player)
                    {
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);
                        string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                        player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            return 0;
        }

        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        /// <summary>
        /// Calculates the effect duration in milliseconds
        /// </summary>
        /// <param name="target">The effect target</param>
        /// <param name="effectiveness">The effect effectiveness</param>
        /// <returns>The effect duration in milliseconds</returns>
        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = Spell.Duration;
            duration *= (1.0 + m_caster.GetModified(eProperty.SpellDuration) * 0.01);
            return (int)duration;
        }

        private void OnAttack(DOLEvent e, object sender, EventArgs arguments)
        {
            GameLiving living = sender as GameLiving;
            if (living == null) return;
            AttackedByEnemyEventArgs attackedByEnemy = arguments as AttackedByEnemyEventArgs;
            AttackData ad = null;
            if (attackedByEnemy != null)
                ad = attackedByEnemy.AttackData;

            //			Log.DebugFormat("sender:{0} res:{1} IsMelee:{2} Type:{3}", living.Name, ad.AttackResult, ad.IsMeleeAttack, ad.AttackType);

            // Melee or Magic or Both ?
            if (!MatchingDamageType(ref ad)) return;

            int ablativehp = living.TempProperties.getProperty<int>(ABLATIVE_HP);
            double absorbPercent = 25;
            if (Spell.Damage > 0)
                absorbPercent = Spell.Damage;
            //because albatives can reach 100%
            if (absorbPercent > 100)
                absorbPercent = 100;
            int damageAbsorbed = (int)(0.01 * absorbPercent * (ad.Damage + ad.CriticalDamage));
            if (damageAbsorbed > ablativehp)
                damageAbsorbed = ablativehp;
            ablativehp -= damageAbsorbed;
            ad.Damage -= damageAbsorbed;
            OnDamageAbsorbed(ad, damageAbsorbed);

            if (ad.Target is GamePlayer)
                (ad.Target as GamePlayer)?.Out.SendMessage(LanguageMgr.GetTranslation((ad.Target as GamePlayer)?.Client, "AblativeArmor.Target", damageAbsorbed), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);

            if (ad.Attacker is GamePlayer)
                (ad.Attacker as GamePlayer)?.Out.SendMessage(LanguageMgr.GetTranslation((ad.Attacker as GamePlayer)?.Client, "AblativeArmor.Attacker", damageAbsorbed), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);

            if (ablativehp <= 0)
            {
                GameSpellEffect effect = SpellHandler.FindEffectOnTarget(living, this);
                if (effect != null)
                    effect.Cancel(false);
            }
            else
            {
                living.TempProperties.setProperty(ABLATIVE_HP, ablativehp);
            }
        }

        // Check if Melee
        protected virtual bool MatchingDamageType(ref AttackData ad)
        {

            if (ad == null || (ad.AttackResult != GameLiving.eAttackResult.HitStyle && ad.AttackResult != GameLiving.eAttackResult.HitUnstyled))
                return false;
            if (!ad.IsMeleeAttack && ad.AttackType != AttackData.eAttackType.Ranged)
                return false;

            return true;
        }

        protected virtual void OnDamageAbsorbed(AttackData ad, int DamageAmount)
        {
        }

        public override PlayerXEffect GetSavedEffect(GameSpellEffect e)
        {
            if ( //VaNaTiC-> this cannot work, cause PulsingSpellEffect is derived from object and only implements IConcEffect
                 //e is PulsingSpellEffect ||
                 //VaNaTiC<-
                Spell.Pulse != 0 || Spell.Concentration != 0 || e.RemainingTime < 1)
                return null;
            PlayerXEffect eff = new PlayerXEffect();
            eff.Var1 = Spell.ID;
            eff.Duration = e.RemainingTime;
            eff.IsHandler = true;
            eff.Var2 = (int)Spell.Value;
            eff.SpellLine = SpellLine.KeyName;
            return eff;
        }

        public override void OnEffectRestored(GameSpellEffect effect, int[] vars)
        {
            effect.Owner.TempProperties.setProperty(ABLATIVE_HP, (int)vars[1]);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));
        }

        public override int OnRestoredEffectExpires(GameSpellEffect effect, int[] vars, bool noMessages)
        {
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));
            effect.Owner.TempProperties.removeProperty(ABLATIVE_HP);

            if (!noMessages && Spell.Pulse == 0)
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
                    if (effect.Owner != player)
                    {
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);
                        string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                        player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            return 0;
        }
        #region Delve Info
        public override IList<string> DelveInfo
        {
            get
            {
                var list = new List<string>();

                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "AblativeArmor.DelveInfo.Function"));
                list.Add("");
                list.Add(Spell.Description);
                list.Add("");
                if (Spell.Damage != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "AblativeArmor.DelveInfo.Absorption1", Spell.Damage));
                if (Spell.Damage > 100)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "AblativeArmor.DelveInfo.Absorption2"));
                if (Spell.Damage == 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "AblativeArmor.DelveInfo.Absorption3"));
                if (Spell.Value != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Value", Spell.Value));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Target", Spell.Target));
                if (Spell.Range != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Range", Spell.Range));
                if (Spell.Duration >= ushort.MaxValue * 1000)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + " Permanent.");
                else if (Spell.Duration > 60000)
                    list.Add(string.Format(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + Spell.Duration / 60000 + ":" + (Spell.Duration % 60000 / 1000).ToString("00") + " min"));
                else if (Spell.Duration != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Duration") + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
                if (Spell.Power != 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.PowerCost", Spell.Power.ToString("0;0'%'")));
                if (Spell.CastTime < 0.1)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "AblativeArmor.DelveInfo.CastingTime"));
                else if (Spell.CastTime > 0)
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.CastingTime", (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'")));

                if (ServerProperties.Properties.SERV_LANGUAGE != "DE")
                {
                    list.Add(GetAblativeType());
                    if (Spell.Radius != 0)
                        list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Radius", Spell.Radius));
                    if (Spell.Frequency != 0)
                        list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.Frequency", (Spell.Frequency * 0.001).ToString("0.0")));
                    if (Spell.DamageType != 0)
                        list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "DelveInfo.DamageType", Spell.DamageType));
                }
                return list;
            }
            #endregion
        }
        
        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            double absorbPercent = (Spell.Damage > 0 ? Spell.Damage : 25);
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.MeleeAblativeArmor.MainDescription", absorbPercent, Spell.Value);
        }

        protected virtual string GetAblativeType()
        {
            return "Type: Melee Absorption";
        }

        public AblativeArmorSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("MagicAblativeArmor")]
    public class MagicAblativeArmorSpellHandler : AblativeArmorSpellHandler
    {
        public MagicAblativeArmorSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        protected override bool MatchingDamageType(ref AttackData ad)
        {
            if (ad == null || (ad.AttackResult == GameLiving.eAttackResult.HitStyle && ad.AttackResult == GameLiving.eAttackResult.HitUnstyled))
                return false;
            if (ad.IsMeleeAttack && ad.AttackType == AttackData.eAttackType.Ranged)
                return false;

            return true;
        }

        protected override string GetAblativeType()
        {
            return "Type: Magic Absorption";
        }
        
        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            double absorbPercent = (Spell.Damage > 0 ? Spell.Damage : 25);
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.MagicAblativeArmor.MainDescription", absorbPercent, Spell.Value);
        }
    }

    [SpellHandler("BothAblativeArmor")]
    public class BothAblativeArmorSpellHandler : AblativeArmorSpellHandler
    {
        public BothAblativeArmorSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        protected override bool MatchingDamageType(ref AttackData ad)
        {
            return true;
        }

        protected override string GetAblativeType()
        {
            return "Type: Melee/Magic Absorption";
        }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            double absorbPercent = (Spell.Damage > 0 ? Spell.Damage : 25);
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.BothAblativeArmor.MainDescription", absorbPercent, Spell.Value);
        }
    }
}