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
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Events;
using DOL.GS.Effects;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Base Handler for the focus shell
    /// </summary>
    [SpellHandlerAttribute("FocusShell")]
    public class FocusShellHandler : SpellHandler
    {
        private GamePlayer FSTarget = null;
        private FSTimer timer = null;

        public FocusShellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (!ServerProperties.Properties.ALLOW_FOCUS_SHELL_OUTSIDE_PVP)
            {
                GamePlayer casterPlayer = Caster as GamePlayer;
                if (casterPlayer != null && !casterPlayer.isInBG && !casterPlayer.IsInRvR && !casterPlayer.IsInPvP)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.FocusShell.MustBeRvR"), eChatType.CT_System);
                    return false;
                }
            }
            //Only works on members of the same realm
            if (GameServer.ServerRules.IsAllowedToAttack(selectedTarget, Caster, true) == false)
            {
                //This spell doesn't work on pets or monsters
                if (selectedTarget is GameNPC)
                {
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.FocusShell.NoPets"), eChatType.CT_SpellResisted);
                    return false;
                }

                if (selectedTarget is GamePlayer)
                {
                    GameSpellEffect currentEffect = (GameSpellEffect)Caster.TempProperties.getProperty<object>(FOCUS_SPELL, null);

                    if (currentEffect != null && currentEffect.SpellHandler is FocusShellHandler)
                    {
                        (currentEffect.SpellHandler as FocusShellHandler)!.FocusSpellAction(null, Caster, null);
                    }

                    FSTarget = selectedTarget as GamePlayer;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.FocusShell.SameRealm"), eChatType.CT_SpellResisted);
                return false;
            }

            return base.CheckBeginCast(selectedTarget, quiet);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            // Add the handler for when the target is attacked and we should reduce the damage
            GameEventMgr.AddHandler(FSTarget, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttacked));

            // Add handlers for the target attacking, casting, etc
            // Don't need to add the handlers twice
            if (FSTarget != Caster)
            {
                GameEventMgr.AddHandler(FSTarget, GameLivingEvent.AttackFinished, new DOLEventHandler(CancelSpell));
                GameEventMgr.AddHandler(FSTarget, GameLivingEvent.CastStarting, new DOLEventHandler(CancelSpell));
            }

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer fsTargetPlayer = FSTarget as GamePlayer;

            // Handle translation for the effect owner
            if (fsTargetPlayer != null)
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : Spell.GetFormattedMessage1(fsTargetPlayer);
                MessageToLiving(FSTarget, message1, eChatType.CT_Spell);
            }
            else
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message1, FSTarget.GetName(0, false));
                MessageToLiving(FSTarget, message1, eChatType.CT_Spell);
            }

            foreach (GamePlayer player in FSTarget.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (player != FSTarget)
                {
                    string personalizedTargetName = player.GetPersonalizedName(FSTarget);

                    string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : Spell.GetFormattedMessage2(player, personalizedTargetName);
                    player.MessageFromArea(FSTarget, message2, eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }

            timer = new FSTimer(Caster, this);
            timer.Start(1);

            base.OnEffectStart(effect);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            // Remove our handler
            GameEventMgr.RemoveHandler(FSTarget, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(OnAttacked));

            if (FSTarget != Caster)
            {
                GameEventMgr.RemoveHandler(FSTarget, GameLivingEvent.AttackFinished, new DOLEventHandler(CancelSpell));
                GameEventMgr.RemoveHandler(FSTarget, GameLivingEvent.CastStarting, new DOLEventHandler(CancelSpell));
            }

            timer.Stop();

            if (!noMessages)
            {
                string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
                GamePlayer fsTargetPlayer = FSTarget as GamePlayer;

                // Handle translation for the effect owner when it expires
                if (fsTargetPlayer != null)
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : Spell.GetFormattedMessage3(fsTargetPlayer);
                    MessageToLiving(FSTarget, message3, eChatType.CT_SpellExpires);
                }
                else
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message3, FSTarget.GetName(0, false));
                    MessageToLiving(FSTarget, message3, eChatType.CT_SpellExpires);
                }

                foreach (GamePlayer player in FSTarget.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (player != FSTarget)
                    {
                        string personalizedTargetName = player.GetPersonalizedName(FSTarget);

                        string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                        player.MessageFromArea(FSTarget, message4, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }
            }

            return base.OnEffectExpires(effect, noMessages);
        }

        private void CancelSpell(DOLEvent e, object sender, EventArgs args)
        {
            //Send the cancel signal, we need to use the faster as the sender!
            FocusSpellAction(null, Caster, null);
        }

        private void OnAttacked(DOLEvent e, object sender, EventArgs args)
        {
            AttackedByEnemyEventArgs attackArgs = args as AttackedByEnemyEventArgs;
            if (attackArgs == null || attackArgs.AttackData == null)
                return;

            //If the attacker was not a mob, then lets do the reduction!
            if (attackArgs.AttackData.Attacker.Realm != eRealm.None)
            {
                //Damage absorbed from normal attack
                int damageAbsorbed = (int)(attackArgs.AttackData.Damage * (Spell.Value * .01));
                attackArgs.AttackData.Damage -= damageAbsorbed;
                //Damage absorbed from critical hit
                damageAbsorbed = (int)(attackArgs.AttackData.CriticalDamage * (Spell.Value * .01));
                attackArgs.AttackData.CriticalDamage -= damageAbsorbed;
            }
        }

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            m_caster.Mana -= CalculatePowerCost(target);
            base.FinishSpellCast(target, force);
        }

        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            return new FocusShellEffect(this, Spell.Duration, Spell.Frequency, effectiveness);
        }

        /// <summary>
        /// Since the focus spell isn't a pulsing spell we need our own mini-timer
        /// </summary>
        private class FSTimer : RegionAction
        {
            //The handler for this timer
            FocusShellHandler m_handler;

            public FSTimer(GameObject actionSource, FocusShellHandler handler) : base(actionSource)
            {
                m_handler = handler;
                Interval = handler.Spell.Frequency;
            }

            public override void OnTick()
            {
                //If the target is still in range
                if (m_handler.Caster.Mana >= m_handler.Spell.PulsePower && m_handler.Caster.IsWithinRadius(m_handler.FSTarget, m_handler.Spell.Range))
                {
                    m_handler.Caster.Mana -= m_handler.Spell.PulsePower;
                    m_handler.Caster.LastAttackTickPvP = m_handler.Caster.CurrentRegion.Time;
                }
                //Target went out of range, stop the spell
                else
                {
                    //Cancel spell
                    m_handler.CancelSpell(null, m_handler.Caster, null);
                    Stop();
                }
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string description = LanguageMgr.GetTranslation(delveClient, "SpellDescription.FocusShell.MainDescription", Spell.Value);

            if (!Properties.ALLOW_FOCUS_SHELL_OUTSIDE_PVP)
            {
                string onlyInPvPORvR = LanguageMgr.GetTranslation(delveClient, "SpellDescription.FocusShell.OnlyInPvPORvR");
                description += "\n\n" + onlyInPvPORvR;
            }

            return description;
        }
    }
}
