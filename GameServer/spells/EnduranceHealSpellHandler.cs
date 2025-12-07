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
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("EnduranceHeal")]
    public class EnduranceHealSpellHandler : SpellHandler
    {
        public EnduranceHealSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        private bool m_healed = false;

        /// <inheritdoc />
        public override int CalculatePowerCost(GameLiving target)
        {
            // group heals seem to use full power even if no heals
            if (!m_healed && Spell.Target.Equals("realm", StringComparison.OrdinalIgnoreCase))
                return base.CalculatePowerCost(target) >> 1; // only 1/2 power if no heal
            else
                return base.CalculatePowerCost(target);
        }

        protected override bool ExecuteSpell(GameLiving target, bool force)
        {
            var targets = SelectTargets(target, force);
            if (targets.Count <= 0) return false;

            int spellValue = (int)Math.Round(Spell.Value);

            foreach (GameLiving healTarget in targets)
            {
                if (Spell.Value < 0)
                    // Restore a percentage of the target's endurance
                    spellValue = (int)Math.Round(Spell.Value * -0.01) * target.MaxEndurance;

                m_healed |= HealTarget(healTarget, spellValue);
            }

            ConsumePower(target);

            // send animation for non pulsing spells only
            if (Spell.Pulse == 0)
            {
                if (m_healed)
                {
                    // send animation on all targets if healed
                    foreach (GameLiving healTarget in targets)
                        SendEffectAnimation(healTarget, 0, false, 1);
                }
                else
                {
                    // show resisted effect if not healed
                    SendEffectAnimation(Caster, 0, false, 0);
                }
            }

            if (!m_healed && Spell.CastTime == 0) m_startReuseTimer = false;

            return true;
        }

        public virtual bool HealTarget(GameLiving target, int amount)
        {
            if (target == null || target.ObjectState != GameLiving.eObjectState.Active) return false;

            // we can't heal enemy people
            if (!(Caster is TextNPC) && !GameServer.ServerRules.IsSameRealm(Caster, target, true))
                return false;

            if (!target.IsAlive)
            {
                //"You cannot heal the dead!" sshot550.tga
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.HealSpell.TargetDead", target.GetName(0, true)), eChatType.CT_SpellResisted);
                return false;
            }

            int heal = target.ChangeEndurance(Caster, GameLiving.eEnduranceChangeType.Spell, amount);

            if (heal == 0)
            {
                if (Spell.Pulse == 0)
                {
                    if (target == m_caster) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.EnduranceHeal.CasterFull"), eChatType.CT_SpellResisted);
                    else MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.EnduranceHeal.TargetFull", target.GetName(0, true)), eChatType.CT_SpellResisted);
                }
                return false;
            }

            if (m_caster == target)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.EnduranceHeal.CasterHealed", heal), eChatType.CT_Spell);
                if (heal < amount)
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.EnduranceHeal.CasterFull"), eChatType.CT_Spell);
            }
            else
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.EnduranceHeal.TargetHealed", target.GetName(0, false), heal), eChatType.CT_Spell);
                MessageToLiving(target, LanguageMgr.GetTranslation((target as GamePlayer)?.Client, "SpellHandler.EnduranceHeal.TargetRestored", m_caster.GetName(0, false), heal), eChatType.CT_Spell);
                if (heal < amount)
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.EnduranceHeal.TargetFull", target.GetName(0, true)), eChatType.CT_Spell);
            }
            return true;
        }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (selectedTarget != null && selectedTarget.EndurancePercent >= 90)
            {
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.EnduranceHeal.TargetTooHigh"), eChatType.CT_SpellResisted);
                return false;
            }
            return base.CheckBeginCast(selectedTarget, quiet);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string amountStr = (Spell.Value < 0 ? Spell.Value + "%" : Spell.Value.ToString());
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.EnduranceHeal.MainDescription", amountStr);
        }
    }
}