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
using System.Reflection;
using DOL.Database;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.Keeps;
using DOL.GS.PacketHandler;
using DOL.Language;
using log4net;
using DOL.GS.ServerProperties;
using Newtonsoft.Json.Linq;

namespace DOL.GS.Spells
{
    [SpellHandler("DamageAdd")]
    public class DamageAddSpellHandler : AbstractDamageAddSpellHandler
    {
        protected override DOLEvent EventType { get { return GameLivingEvent.AttackFinished; } }

        public virtual double DPSCap(int Level)
        {
            return (1.2 + 0.3 * Level) * 0.7;
        }

        protected override void EventHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            AttackFinishedEventArgs atkArgs = arguments as AttackFinishedEventArgs;
            if (atkArgs == null) return;

            if (atkArgs.AttackData.AttackResult != GameLiving.eAttackResult.HitUnstyled
                && atkArgs.AttackData.AttackResult != GameLiving.eAttackResult.HitStyle) return;

            GameLiving target = atkArgs.AttackData.Target;
            if (target == null) return;

            if (target.ObjectState != GameObject.eObjectState.Active) return;
            if (target.IsAlive == false) return;
            if (target is GameKeepComponent || target is GameKeepDoor) return;

            GameLiving attacker = sender as GameLiving;
            if (attacker == null) return;

            if (attacker.ObjectState != GameObject.eObjectState.Active) return;
            if (attacker.IsAlive == false) return;

            int spread = m_minDamageSpread;
            spread += Util.Random(50);
            double dpsCap = DPSCap(attacker.Level);
            double dps = IgnoreDamageCap ? Spell.Damage : Math.Min(Spell.Damage, dpsCap);
            double damage = dps * atkArgs.AttackData.WeaponSpeed * spread * 0.001; // attack speed is 10 times higher (2.5spd=25)
            double damageResisted = damage * target.GetResist(Spell.DamageType) * -0.01;

            // log.DebugFormat("dps: {0}, damage: {1}, damageResisted: {2}, minDamageSpread: {3}, spread: {4}", dps, damage, damageResisted, m_minDamageSpread, spread);

            if (Spell.Damage < 0)
            {
                damage = atkArgs.AttackData.Damage * Spell.Damage / -100.0;
                damageResisted = damage * target.GetResist(Spell.DamageType) * -0.01;
            }

            AttackData ad = new AttackData();
            ad.Attacker = attacker;
            ad.Target = target;
            ad.Damage = (int)(damage + damageResisted);
            ad.Modifier = (int)damageResisted;
            ad.DamageType = Spell.DamageType;
            ad.AttackType = AttackData.eAttackType.Spell;
            ad.SpellHandler = this;
            ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;

            if (ad.Attacker is GameNPC && ad.Attacker.GetController() is GamePlayer owner)
            {
                MessageToLiving(owner, String.Format(LanguageMgr.GetTranslation(owner.Client, "DamageAddAndShield.EventHandlerDA.YourHitFor"), owner.GetPersonalizedName(ad.Attacker), target.GetName(0, false), ad.Damage), eChatType.CT_Spell);
            }
            else
            {
                GameClient attackerClient = null;
                if (attacker is GamePlayer) attackerClient = ((GamePlayer)attacker).Client;

                if (attackerClient != null)
                {
                    MessageToLiving(attacker, String.Format(LanguageMgr.GetTranslation(attackerClient, "DamageAddAndShield.EventHandlerDA.YouHitExtra"), attackerClient.Player.GetPersonalizedName(target), ad.Damage), eChatType.CT_Spell);
                }
            }

            GameClient targetClient = null;
            if (target is GamePlayer) targetClient = ((GamePlayer)target).Client;

            if (targetClient != null)
            {
                MessageToLiving(target, String.Format(LanguageMgr.GetTranslation(targetClient, "DamageAddAndShield.EventHandlerDA.DamageToYou"), targetClient.Player.GetPersonalizedName(attacker), ad.Damage), eChatType.CT_Spell);
            }

            target.OnAttackedByEnemy(ad);
            attacker.DealDamage(ad);

            foreach (GamePlayer player in ad.Attacker.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null) continue;
                player.Out.SendCombatAnimation(null, target, 0, 0, 0, 0, 0x0A, target.HealthPercent);
            }
        }

        public DamageAddSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string description = LanguageMgr.GetTranslation(language, "SpellDescription.DamageAdd.MainDescription", Spell.Damage, LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType));

            if (Spell.RecastDelay > 0)
            {
                string thirdDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return description + "\n\n" + thirdDesc;
            }

            return description;
        }
    }

    [SpellHandler("DamageShield")]
    public class DamageShieldSpellHandler : AbstractDamageAddSpellHandler
    {
        protected override DOLEvent EventType { get { return GameLivingEvent.AttackedByEnemy; } }

        protected override void EventHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            AttackedByEnemyEventArgs args = arguments as AttackedByEnemyEventArgs;
            if (args == null) return;
            if (args.AttackData.AttackResult != GameLiving.eAttackResult.HitUnstyled
                && args.AttackData.AttackResult != GameLiving.eAttackResult.HitStyle) return;
            if (!args.AttackData.IsMeleeAttack) return;
            GameLiving attacker = sender as GameLiving; //sender is target of attack, becomes attacker for damage shield
            if (attacker == null) return;
            if (attacker.ObjectState != GameObject.eObjectState.Active) return;
            if (attacker.IsAlive == false) return;
            GameLiving target = args.AttackData.Attacker; //attacker becomes target for damage shield
            if (target == null) return;
            if (target.ObjectState != GameObject.eObjectState.Active) return;
            if (target.IsAlive == false) return;

            int spread = m_minDamageSpread;
            spread += Util.Random(50);
            double damage = Spell.Damage * target.AttackSpeed(target.AttackWeapon) * spread * 0.00001;
            double damageResisted = damage * target.GetResist(Spell.DamageType) * -0.01;

            if (Spell.Damage < 0)
            {
                damage = args.AttackData.Damage * Spell.Damage / -100.0;
                damageResisted = damage * target.GetResist(Spell.DamageType) * -0.01;
            }

            AttackData ad = new AttackData();
            ad.Attacker = attacker;
            ad.Target = target;
            ad.Damage = (int)(damage + damageResisted);
            ad.Modifier = (int)damageResisted;
            ad.DamageType = Spell.DamageType;
            ad.SpellHandler = this;
            ad.AttackType = AttackData.eAttackType.Spell;
            ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;
            
            GameClient attackerClient = null;
            if (attacker is GamePlayer) attackerClient = ((GamePlayer)attacker).Client;

            if (ad.Attacker is GameNPC && ad.Attacker.GetPlayerOwner() is {} ownerPlayer)
            {
                MessageToLiving(ownerPlayer, String.Format(LanguageMgr.GetTranslation(ownerPlayer.Client, "DamageAddAndShield.EventHandlerDS.YourHitFor"), ownerPlayer.GetPersonalizedName(ad.Attacker), target.GetName(0, false), ad.Damage), eChatType.CT_Spell);
            }
            else if (attackerClient != null)
            {
                MessageToLiving(attacker, String.Format(LanguageMgr.GetTranslation(attackerClient, "DamageAddAndShield.EventHandlerDS.YouHitFor"), attackerClient.Player.GetPersonalizedName(target), ad.Damage), eChatType.CT_Spell);
            }

            GameClient targetClient = null;
            if (target is GamePlayer) targetClient = ((GamePlayer)target).Client;

            if (targetClient != null)
                MessageToLiving(target, String.Format(LanguageMgr.GetTranslation(targetClient, "DamageAddAndShield.EventHandlerDS.DamageToYou"), targetClient.Player.GetPersonalizedName(attacker), ad.Damage), eChatType.CT_Spell);

            target.OnAttackedByEnemy(ad);
            attacker.DealDamage(ad);
            foreach (GamePlayer player in attacker.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null)
                    continue;
                player.Out.SendCombatAnimation(null, target, 0, 0, 0, 0, 0x14, target.HealthPercent);
            }
            //			Log.Debug(String.Format("spell damage: {0}; damage: {1}; resisted damage: {2}; damage type {3}; minSpread {4}.", Spell.Damage, ad.Damage, ad.Modifier, ad.DamageType, m_minDamageSpread));
            //			Log.Debug(String.Format("dmg {0}; spread: {4}; resDmg: {1}; atkSpeed: {2}; resist: {3}.", damage, damageResisted, target.AttackSpeed(null), ad.Target.GetResist(Spell.DamageType), spread));
        }

        public DamageShieldSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string description = LanguageMgr.GetTranslation(language, "SpellDescription.DamageShield.MainDescription", Spell.Damage, LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType));

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return description + "\n\n" + secondDesc;
            }

            return description;
        }
    }

    public abstract class AbstractDamageAddSpellHandler : SpellHandler
    {
        protected abstract DOLEvent EventType { get; }

        protected abstract void EventHandler(DOLEvent e, object sender, EventArgs arguments);

        protected int m_minDamageSpread = 50;

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = Spell.Duration;
            duration *= (1.0 + m_caster.GetModified(eProperty.SpellDuration) * 0.01);
            return (int)duration;
        }

        protected override bool ExecuteSpell(GameLiving target, bool force = false)
        {
            // set min spread based on spec
            if (Caster is GamePlayer)
            {
                int lineSpec = Caster.GetModifiedSpecLevel(m_spellLine.Spec);
                m_minDamageSpread = 50;
                if (Spell.Level > 0)
                {
                    m_minDamageSpread += (lineSpec - 1) * 50 / Spell.Level;
                    if (m_minDamageSpread > 100) m_minDamageSpread = 100;
                    else if (m_minDamageSpread < 50) m_minDamageSpread = 50;
                }
                else
                {
                    // For level 0 spells, like realm abilities, always work off of full spec to achieve live like damage amounts.
                    // If spec level is used at all it most likely should only be for baseline spells. - tolakram
                    m_minDamageSpread = 100;
                }
            }

            return base.ExecuteSpell(target, force);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer ownerPlayer = effect.Owner as GamePlayer;

            eChatType chatType = (Spell.Pulse == 0) ? eChatType.CT_Spell : eChatType.CT_SpellPulse;

            if (ownerPlayer != null)
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : Spell.GetFormattedMessage1(ownerPlayer);
                MessageToLiving(effect.Owner, message1, chatType);
            }
            else
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message1, effect.Owner.GetName(0, false));
                MessageToLiving(effect.Owner, message1, chatType);
            }

            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (!(effect.Owner == player))
                {
                    string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                    string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : Spell.GetFormattedMessage2(player, personalizedTargetName);
                    player.MessageFromArea(effect.Owner, message2, chatType, eChatLoc.CL_SystemWindow);
                }
            }

            GameEventMgr.AddHandler(effect.Owner, EventType, new DOLEventHandler(EventHandler));
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer ownerPlayer = effect.Owner as GamePlayer;

            if (!noMessages && Spell.Pulse == 0)
            {
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
                    string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                    string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                    player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                }
            }

            GameEventMgr.RemoveHandler(effect.Owner, EventType, new DOLEventHandler(EventHandler));
            return 0;
        }

        public override void OnEffectRestored(GameSpellEffect effect, int[] vars)
        {
            GameEventMgr.AddHandler(effect.Owner, EventType, new DOLEventHandler(EventHandler));
        }

        public override int OnRestoredEffectExpires(GameSpellEffect effect, int[] vars, bool noMessages)
        {
            return OnEffectExpires(effect, noMessages);
        }

        public override PlayerXEffect GetSavedEffect(GameSpellEffect e)
        {
            PlayerXEffect eff = new PlayerXEffect();
            eff.Var1 = Spell.ID;
            eff.Duration = e.RemainingTime;
            eff.IsHandler = true;
            eff.SpellLine = SpellLine.KeyName;
            return eff;
        }

        public AbstractDamageAddSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine) { }
    }
}
