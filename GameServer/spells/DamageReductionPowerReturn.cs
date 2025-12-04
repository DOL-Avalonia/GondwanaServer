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
*///made by Yemla, based off Alblative
using System;
using System.Collections.Generic;
using System.Reflection;

using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Events;
using log4net;
using System.Collections;
using DOL.GS;
using DOL.AI.Brain;
using DOL.GS.Keeps;
using DOL.Language;


namespace DOL.GS.Spells
{
    [SpellHandlerAttribute("DmgReductionAndPowerReturn")]
    public class DamageReductionAndPowerReturnSpellHandler : SpellHandler
    {
        public const string Damage_Reduction = "damage reduction";

        public override void OnEffectStart(GameSpellEffect effect)
        {
            effect.Owner.TempProperties.setProperty(Damage_Reduction, 100000);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));

            eChatType toLiving = (Spell.Pulse == 0) ? eChatType.CT_Spell : eChatType.CT_SpellPulse;
            eChatType toOther = (Spell.Pulse == 0) ? eChatType.CT_System : eChatType.CT_Spell;///Pulse;
            MessageToLiving(effect.Owner, Spell.Message1, toLiving);
            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (!(effect.Owner == player))
                {
                    player.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message2,
                        player.GetPersonalizedName(effect.Owner)), toOther, eChatLoc.CL_SystemWindow);
                }
            }
        }

        /// <summary>
        /// When an applied effect expires.
        /// Duration spells only.
        /// </summary>
        /// <param name="effect">The expired effect</param>
        /// <param name="noMessages">true, when no messages should be sent to player and surrounding</param>
        /// <returns>immunity duration in milliseconds</returns>
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));
            effect.Owner.TempProperties.removeProperty(Damage_Reduction);
            if (!noMessages && Spell.Pulse == 0)
            {
                MessageToLiving(effect.Owner, Spell.Message3, eChatType.CT_SpellExpires);
                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player))
                    {
                        player.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message4,
                            player.GetPersonalizedName(effect.Owner)), eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            return 0;
        }
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        private void OnAttack(DOLEvent e, object sender, EventArgs arguments)
        {
            GameLiving living = sender as GameLiving;
            if (living == null) return;
            AttackedByEnemyEventArgs attackedByEnemy = arguments as AttackedByEnemyEventArgs;
            AttackData ad = null;
            if (attackedByEnemy != null)
                ad = attackedByEnemy.AttackData;

            //         Log.DebugFormat("sender:{0} res:{1} IsMelee:{2} Type:{3}", living.Name, ad.AttackResult, ad.IsMeleeAttack, ad.AttackType);

            int damagereduction = living.TempProperties.getProperty<int>(Damage_Reduction);
            double absorbPercent = Spell.Damage;
            int damageAbsorbed = (int)(0.01 * absorbPercent * (ad.Damage + ad.CriticalDamage));
            if (damageAbsorbed > damagereduction)
                damageAbsorbed = damagereduction;
            damagereduction -= damageAbsorbed;
            ad.Damage -= damageAbsorbed;
            OnDamageAbsorbed(ad, damageAbsorbed);

            //TODO correct messages
            if (ad.Damage > 0)
                MessageToLiving(ad.Target, LanguageMgr.GetTranslation((ad.Target as GamePlayer)?.Client, "SpellHandler.DmgReductionAndPowerReturn.DamageAbsorbed", damageAbsorbed), eChatType.CT_Spell);
            MessageToLiving(ad.Attacker, LanguageMgr.GetTranslation((ad.Attacker as GamePlayer)?.Client, "SpellHandler.DmgReductionAndPowerReturn.DamageReduced", damageAbsorbed), eChatType.CT_Spell);
            if (damageAbsorbed > 0)
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.DmgReductionAndPowerReturn.ManaReturn", damageAbsorbed), eChatType.CT_Spell);
            Caster.Mana = Caster.Mana + damageAbsorbed;
            if (Caster.Mana == Caster.MaxMana)
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.DamageToPower.NoMorePower"), eChatType.CT_SpellResisted);

            if (damagereduction <= 0)
            {
                GameSpellEffect effect = SpellHandler.FindEffectOnTarget(living, this);
                if (effect != null)
                    effect.Cancel(false);
            }
            else
            {
                living.TempProperties.setProperty(Damage_Reduction, damagereduction);
            }
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
            effect.Owner.TempProperties.setProperty(Damage_Reduction, (int)vars[1]);
            GameEventMgr.AddHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));
        }

        public override int OnRestoredEffectExpires(GameSpellEffect effect, int[] vars, bool noMessages)
        {
            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttack));
            effect.Owner.TempProperties.removeProperty(Damage_Reduction);
            if (!noMessages && Spell.Pulse == 0)
            {
                MessageToLiving(effect.Owner, Spell.Message3, eChatType.CT_SpellExpires);
                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player))
                    {
                        player.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message4,
                            player.GetPersonalizedName(effect.Owner)), eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            return 0;
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
                    list.Add("Damage Absorb: " + Spell.Damage + "%");
                if (Spell.Value != 0)
                    list.Add("Power Return: " + Spell.Damage + "%");
                if (Spell.CastTime < 0.1)
                    list.Add("Casting time: Instant");
                else if (Spell.CastTime > 0)
                    list.Add("Casting time: " + (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'"));
                if (Spell.Duration >= ushort.MaxValue * 1000)
                    list.Add("Duration: Permanent.");
                else if (Spell.Duration > 60000)
                    list.Add(string.Format("Duration: {0}:{1} min", Spell.Duration / 60000, (Spell.Duration % 60000 / 1000).ToString("00")));
                else if (Spell.Duration != 0)

                    if (Spell.Range != 0)
                        list.Add("Range: " + Spell.Range);
                if (Spell.Radius != 0)
                    list.Add("Radius: " + Spell.Radius);

                list.Add("Power cost: " + Spell.Power.ToString("0;0'%'"));

                if (Spell.Frequency != 0)
                    list.Add("Frequency: " + (Spell.Frequency * 0.001).ToString("0.0"));

                if (Spell.DamageType != 0)
                    list.Add("Damage Type: " + Spell.DamageType);
                return list;
            }
        }
        public DamageReductionAndPowerReturnSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
