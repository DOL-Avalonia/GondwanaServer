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
using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using log4net;
using DOL.Language;

namespace DOL.GS.SkillHandler
{
    /// <summary>
    /// Handler for Sprint Ability clicks
    /// </summary>
    [SkillHandlerAttribute(Abilities.Engage)]
    public class EngageAbilityHandler : IAbilityActionHandler
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// wait 5 sec to engage after attack
        /// </summary>
        public const int ENGAGE_ATTACK_DELAY_TICK = 5000;

        /// <summary>
        /// Endurance lost on every attack
        /// </summary>
        public const int ENGAGE_DURATION_LOST = 15;

        /// <summary>
        /// Execute engage ability
        /// </summary>
        /// <param name="ab">The used ability</param>
        /// <param name="player">The player that used the ability</param>
        public void Execute(Ability ab, GamePlayer player)
        {
            if (player == null)
            {
                if (log.IsWarnEnabled)
                    log.Warn("Could not retrieve player in EngageAbilityHandler.");
                return;
            }
            /*
			//Cancel old engage effects on player
			if (player.IsEngaging)
			{
				EngageEffect engage = (EngageEffect)player.EffectList.GetOfType(typeof(EngageEffect));
				if (engage != null)
				{
					engage.Cancel(false);
					return;
				}
			}
			 */
            if (!player.IsAlive)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Engage.CannotUseDead"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            if (player.IsSitting)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Engage.CannotUseStanding"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            if (player.ActiveWeaponSlot == GameLiving.eActiveWeaponSlot.Distance)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Engage.CannotUseNoCaCWeapons"), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                return;
            }

            GameLiving target = player.TargetObject as GameLiving;
            if (target == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Engage.NoTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // You cannot engage a mob that was attacked within the last 5 seconds...
            if (target.LastAttackedByEnemyTick > target.CurrentRegion.Time - EngageAbilityHandler.ENGAGE_ATTACK_DELAY_TICK)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Engage.TargetAttackedRecently", target.GetName(0, true)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!GameServer.ServerRules.IsAllowedToAttack(player, target, true))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Skill.Ability.Engage.NotAllowedToEngageTarget", target.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            //Cancel old engage effects on player
            foreach (EngageEffect engage in player.EffectList.GetAllOfType<EngageEffect>())
            {

                if (engage != null)
                {
                    engage.Cancel(false);
                    engage.Stop();
                    return;
                }
            }
            new EngageEffect().Start(player);
        }
    }
}