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
using System.Collections.Generic;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    [SpellHandler("Conversion")]
    public class ConversionSpellHandler : SpellHandler
    {
        public const string ConvertDamage = "Conversion";

        public override void FinishSpellCast(GameLiving target)
        {
            m_caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            effect.Owner.TempProperties.setProperty(ConvertDamage, 100000);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));

            eChatType toLiving = (Spell.Pulse == 0) ? eChatType.CT_Spell : eChatType.CT_SpellPulse;
            eChatType toOther = (Spell.Pulse == 0) ? eChatType.CT_System : eChatType.CT_SpellPulse;

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer ownerPlayer = effect.Owner as GamePlayer;

            // Handle translation for the effect owner
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
                if (!(effect.Owner == player))
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
            effect.Owner.TempProperties.removeProperty(ConvertDamage);
            return 1;
        }

        protected virtual void OndamageConverted(AttackData ad, int DamageAmount)
        {
        }

        private void OnAttack(DOLEvent e, object sender, EventArgs arguments)
        {
            GameLiving living = sender as GameLiving;
            if (living == null) return;
            AttackedByEnemyEventArgs attackedByEnemy = arguments as AttackedByEnemyEventArgs;
            AttackData ad = null;
            if (attackedByEnemy != null)
            {
                ad = attackedByEnemy.AttackData;
            }
            int reduceddmg = living.TempProperties.getProperty<int>(ConvertDamage);
            double absorbPercent = Spell.Damage;
            int damageConverted = (int)(0.01 * absorbPercent * (ad!.Damage + ad.CriticalDamage));

            if (damageConverted > reduceddmg)
            {
                damageConverted = reduceddmg;
                reduceddmg -= damageConverted;
                ad.Damage -= damageConverted;
                OndamageConverted(ad, damageConverted);
            }

            if (ad.Damage > 0)
                MessageToLiving(ad.Target, LanguageMgr.GetTranslation((ad.Target as GamePlayer)?.Client, "SpellHandler.Conversion.ConvertToHealth", damageConverted), eChatType.CT_Spell);
            MessageToLiving(ad.Attacker, LanguageMgr.GetTranslation((ad.Attacker as GamePlayer)?.Client, "SpellHandler.Conversion.MagicalAbsorption", damageConverted), eChatType.CT_Spell);

            if (Caster.Health != Caster.MaxHealth)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.ConvertToHealth", damageConverted), eChatType.CT_Spell);
                Caster.Health = Caster.Health + damageConverted;

                #region PVP DAMAGE
                
                    
                if (ad.Target.DamageRvRMemory > 0 && (ad.Target is GamePlayer || (ad.Target as NecromancerPet)?.GetLivingOwner() is not null))
                {
                    ad.Target.DamageRvRMemory -= (long)Math.Max(damageConverted, 0);
                }

                #endregion PVP DAMAGE

            }
            else
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.CannotConvertMoreHealth"), eChatType.CT_Spell);
            }

            if (Caster.Endurance != Caster.MaxEndurance)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.ConvertToEndurance", damageConverted), eChatType.CT_Spell);
                Caster.Endurance = Caster.Endurance + damageConverted;
            }
            else
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.CannotConvertMoreEndurance"), eChatType.CT_Spell);
            }
            if (Caster.Mana != Caster.MaxMana)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.ConvertToMana", damageConverted), eChatType.CT_Spell);
                Caster.Mana = Caster.Mana + damageConverted;
            }
            else
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.CannotConvertMoreMana"), eChatType.CT_Spell);
            }

            if (reduceddmg <= 0)
            {
                GameSpellEffect effect = SpellHandler.FindEffectOnTarget(living, this);
                if (effect != null)
                    effect.Cancel(false);
            }
        }
        public override IList<string> DelveInfo
        {
            get
            {
                var list = new List<string>();
                list.Add("Name: " + Spell.Name);
                list.Add("Description: " + Spell.Description);
                list.Add("Target: " + Spell.Target);
                if (Spell.Damage != 0)
                {
                    list.Add("Damage Absorb: " + Spell.Damage + "%");
                    list.Add("Health Return: " + Spell.Damage + "%");
                    list.Add("Power Return: " + Spell.Damage + "%");
                    list.Add("Endurance Return: " + Spell.Damage + "%");
                }
                return list;
            }
        }
        public ConversionSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Conversion.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }

    [SpellHandler("MagicConversion")]
    public class MagicConversionSpellHandler : ConversionSpellHandler
    {
        //public const string ConvertDamage = "Conversion";

        private void OnAttack(DOLEvent e, object sender, EventArgs arguments)
        {
            GameLiving living = sender as GameLiving;
            if (living == null) return;
            AttackedByEnemyEventArgs attackedByEnemy = arguments as AttackedByEnemyEventArgs;
            AttackData ad = null;
            if (attackedByEnemy != null)
            {
                ad = attackedByEnemy.AttackData;
            }


            if (ad!.Damage > 0)
            {
                switch (attackedByEnemy!.AttackData.AttackType)
                {
                    case AttackData.eAttackType.Spell:
                        {
                            int reduceddmg = living.TempProperties.getProperty<int>(ConvertDamage, 0);
                            double absorbPercent = Spell.Damage;
                            int damageConverted = (int)(0.01 * absorbPercent * (ad.Damage + ad.CriticalDamage));
                            if (damageConverted > reduceddmg)
                            {
                                damageConverted = reduceddmg;
                                reduceddmg -= damageConverted;
                                ad.Damage -= damageConverted;
                                OndamageConverted(ad, damageConverted);
                            }
                            if (reduceddmg <= 0)
                            {
                                GameSpellEffect effect = SpellHandler.FindEffectOnTarget(living, this);
                                if (effect != null)
                                    effect.Cancel(false);
                            }
                            MessageToLiving(ad.Target, LanguageMgr.GetTranslation((ad.Target as GamePlayer)?.Client, "SpellHandler.Conversion.ConvertToHealth", damageConverted), eChatType.CT_Spell);
                            MessageToLiving(ad.Attacker, LanguageMgr.GetTranslation((ad.Attacker as GamePlayer)?.Client, "SpellHandler.Conversion.MagicalAbsorption", damageConverted), eChatType.CT_Spell);
                            if (Caster.Health != Caster.MaxHealth)
                            {
                                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.ConvertToHealth", damageConverted), eChatType.CT_Spell);
                                Caster.Health = Caster.Health + damageConverted;
                            }
                            else
                            {
                                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.CannotConvertMoreHealth"), eChatType.CT_Spell);
                            }

                            if (Caster.Endurance != Caster.MaxEndurance)
                            {
                                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.ConvertToEndurance", damageConverted), eChatType.CT_Spell);
                                Caster.Endurance = Caster.Endurance + damageConverted;
                            }
                            else
                            {
                                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.CannotConvertMoreEndurance"), eChatType.CT_Spell);
                            }
                            if (Caster.Mana != Caster.MaxMana)
                            {
                                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.ConvertToMana", damageConverted), eChatType.CT_Spell);
                                Caster.Mana = Caster.Mana + damageConverted;
                            }
                            else
                            {
                                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Conversion.CannotConvertMoreMana"), eChatType.CT_Spell);
                            }
                        }
                        break;
                }
            }
        }

        public MagicConversionSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.MagicConversion.MainDescription", Spell.Value);

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc;
            }

            return mainDesc;
        }
    }
}
