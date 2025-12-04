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
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.AI.Brain;
using DOL.Language;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    [SpellHandlerAttribute("HealOverTime")]
    public class HoTSpellHandler : SpellHandler
    {
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            ConsumePower(target);
            base.FinishSpellCast(target, force);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            // TODO: correct formula
            double eff = 1.25;
            if (Caster is GamePlayer)
            {
                double lineSpec = Caster.GetModifiedSpecLevel(m_spellLine.Spec);
                if (lineSpec < 1)
                    lineSpec = 1;
                eff = 0.75;
                if (Spell.Level > 0)
                {
                    eff += (lineSpec - 1.0) / Spell.Level * 0.5;
                    if (eff > 1.25)
                        eff = 1.25;
                }
            }
            return base.ApplyEffectOnTarget(target, eff);
        }

        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            return new GameSpellEffect(this, Spell.Duration, Spell.Frequency, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            SendEffectAnimation(effect.Owner, 0, false, 1);

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer ownerPlayer = effect.Owner as GamePlayer;

            // Send healing start message to the affected player
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

            // Send localized message to nearby players
            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (player != effect.Owner)
                {
                    string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                    string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : Spell.GetFormattedMessage2(player, personalizedTargetName);
                    player.MessageFromArea(effect.Owner, message2, eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }
        }

        public override void OnEffectPulse(GameSpellEffect effect)
        {
            base.OnEffectPulse(effect);
            OnDirectEffect(effect.Owner, effect.Effectiveness);
        }

        public override bool OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target.ObjectState != GameObject.eObjectState.Active) return false;
            if (target.IsAlive == false) return false;

            if (!base.OnDirectEffect(target, effectiveness))
                return false;
            
            double heal = Spell.Value * effectiveness;

            target.Health += (int)heal;

            #region PVP DAMAGE

            if (target.DamageRvRMemory > 0 && (target is GamePlayer || (target as NecromancerPet)?.GetPlayerOwner() is not null))
            {
                target.DamageRvRMemory -= (long)Math.Max(heal, 0);
            }

            #endregion PVP DAMAGE

            // Send healing message, using the same method for players and NPCs
            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            string message1 = Spell.Message1;
            if (string.IsNullOrEmpty(message1))
            {
                message1 = string.Empty;
            }
            else
            {
                message1 = target is GamePlayer ownerPlayer
                    ? Spell.GetFormattedMessage1(ownerPlayer)
                    : LanguageMgr.GetTranslation(casterLanguage, message1, target.GetName(0, false));
            }
            MessageToLiving(target, message1, eChatType.CT_Spell);
            return true;
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);
            if (!noMessages)
            {
                string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
                GamePlayer ownerPlayer = effect.Owner as GamePlayer;

                // Send expiration message to the affected player
                string message3 = Spell.Message3;
                if (string.IsNullOrEmpty(message3))
                {
                    message3 = string.Empty;
                }
                else
                {
                    message3 = ownerPlayer != null
                        ? Spell.GetFormattedMessage3(ownerPlayer)
                        : LanguageMgr.GetTranslation(casterLanguage, message3, effect.Owner.GetName(0, false));
                }
                MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);

                // Send expiration messages to nearby players
                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (player != effect.Owner)
                    {
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                        string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                        player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            return 0;
        }

        public HoTSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.HoT.MainDescription", Spell.Value, Spell.Frequency / 1000.0);
        }
    }
}
