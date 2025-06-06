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
using System.Collections.Generic;
using DOL.GS.Effects;
using DOL.Database;
using DOL.Language;
using DOL.GS.PacketHandler;
using AmteScripts.Managers;

namespace DOL.GS.Spells
{
    /// <summary>
    /// The spell used for the Personal Bind Recall Stone.
    /// </summary>
    /// <author>Aredhel</author>
    [SpellHandlerAttribute("GuarksTeleport")]
    public class GuarksTeleport : SpellHandler
    {
        public GuarksTeleport(GameLiving caster, Spell spell, SpellLine spellLine)
            : base(caster, spell, spellLine) { }


        /// <summary>
        /// Can this spell be queued with other spells?
        /// </summary>
        public override bool CanQueue
        {
            get { return false; }
        }


        /// <summary>
        /// Whether this spell can be cast on the selected target at all.
        /// </summary>
        /// <param name="selectedTarget"></param>
        /// <returns></returns>
        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            GamePlayer player = Caster as GamePlayer;
            if (player == null)
                return false;

            if (player.CurrentRegion.IsRvR || player.CurrentRegion.IsInstance || PvpManager.Instance.IsIn(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Items.Specialitems.GuarkRingCannotUseHere"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.IsMoving)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Items.Specialitems.GuarkRingDoNotMove"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.InCombat || GameRelic.IsPlayerCarryingRelic(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language,"Items.Specialitems.GuarkRingWaitBeforeUse"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            return true;
        }


        /// <summary>
        /// Always a constant casting time
        /// </summary>
        /// <returns></returns>
        public override int CalculateCastingTime()
        {
            return m_spell.CastTime;
        }


        /// <summary>
        /// Apply the effect.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="effectiveness"></param>
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (Caster is not GamePlayer { InCombat: false, IsMoving: false } player || GameRelic.IsPlayerCarryingRelic(player))
                return false;

            SendEffectAnimation(player, 0, false, 1);

            UniPortalEffect effect = new UniPortalEffect(this, 1000);
            effect.Start(player);

            player.MoveTo(player.Position);
            return true;
        }


        public override void CasterMoves()
        {
            InterruptCasting();
            if (Caster is GamePlayer)
                (Caster as GamePlayer).Out.SendMessage(LanguageMgr.GetTranslation((Caster as GamePlayer).Client, "SpellHandler.CasterMove"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }


        public override void InterruptCasting()
        {
            m_startReuseTimer = false;
            base.InterruptCasting();
        }

        public override IList<string> DelveInfo
        {
            get
            {
                var list = new List<string>();
                list.Add(string.Format("{0}", Spell.Description));
                return list;
            }
        }
    }
}
