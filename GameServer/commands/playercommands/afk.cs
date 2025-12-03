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

using DOL.GS.PacketHandler;
using DOL.GS.SkillHandler;
using DOL.Language;
using DOL.GS.Scripts;
using DOL.GS.Spells;

namespace DOL.GS.Commands
{
    [Cmd(
        "&afk",
        ePrivLevel.Player,
        "Commands.Players.Afk.Description",
        "Commands.Players.Afk.Usage")]
    public class AFKCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            var p = client?.Player;
            if (p == null) return;

            // SUBCOMMAND: /afk combat
            if (args.Length > 1)
            {
                string mode = args[1];

                if (mode.Equals("attack", System.StringComparison.OrdinalIgnoreCase) ||
                    mode.Equals("xp", System.StringComparison.OrdinalIgnoreCase) ||
                    mode.Equals("combat", System.StringComparison.OrdinalIgnoreCase))
                {
                    HandleAfkAttack(client, p);
                    return;
                }
            }

            // ORIGINAL SIMPLE TOGGLE (/afk with no args)
            if (p.IsAfkActive() && args.Length == 1)
            {
                p.ClearAFK(showMessage: true);
                p.DisableSkill(SkillBase.GetAbility(Abilities.Vol), VolAbilityHandler.DISABLE_DURATION_PLAYER);
                return;
            }

            if (!CheckAfkCommonRestrictions(client, p))
                return;

            if (!p.IsAfkDelayElapsed)
            {
                client!.Out.SendMessage(
                    LanguageMgr.GetTranslation(client, "Commands.Players.Afk.Wait"),
                    eChatType.CT_Chat, eChatLoc.CL_SystemWindow);
                return;
            }

            p.InitAfkTimers();

            string msg = args.Length > 1
                ? string.Join(" ", args, 1, args.Length - 1)
                : "AFK";

            p.SetAFK(msg, showMessage: true);
        }

        /// <summary>
        /// AFK restrictions (duel, combat, riding, moving, jailed, stunned, etc).
        /// </summary>
        private static bool CheckAfkCommonRestrictions(GameClient client, GamePlayer p)
        {
            if (p.DuelTarget != null)
            {
                client!.Out.SendMessage(
                    LanguageMgr.GetTranslation(client, "Commands.Players.Afk.CannotWhileDuel"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (p.InCombat)
            {
                client!.Out.SendMessage(
                    LanguageMgr.GetTranslation(client, "Commands.Players.Afk.CannotWhileCombat"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (p.TempProperties.getProperty<object>(StealCommandHandlerBase.PLAYER_VOL_TIMER, null) != null)
            {
                client!.Out.SendMessage(
                    LanguageMgr.GetTranslation(client, "Commands.Players.Afk.CannotWhileStealing"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (p.IsRiding)
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Commands.Players.Afk.CannotWhileRiding"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (p.IsMoving)
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Commands.Players.Afk.CannotWhileMoving"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (JailMgr.IsPrisoner(p))
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Commands.Players.Afk.CannotWhileJailed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (p.IsStunned)
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Commands.Players.Afk.CannotWhileStunned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (p.IsMezzed)
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Commands.Players.Afk.CannotWhileMezzed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (p.IsDamned)
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Commands.Players.Afk.CannotWhileDamned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (p.IsCrafting)
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Commands.Players.Afk.CannotWhileCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            var wsdSrc = SpellHandler.FindEffectOnTarget(p, "WarlockSpeedDecrease");
            if (wsdSrc != null)
            {
                int rm = wsdSrc.Spell?.ResurrectMana ?? 0;
                string appearance = LanguageMgr.GetWarlockMorphAppearance(p.Client.Account.Language, rm);
                p.Out.SendMessage(
                    LanguageMgr.GetTranslation(p.Client, "Commands.Players.Afk.CannotWhilekMorphed", appearance),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Handles /afk attack: toggles "AFK attack mode" which is required for AFK XP.
        /// - Only works if the player targets a GameTrainingDummy in 100 radius as tank class or up to 700 radius for ranged classes.
        /// - If not already AFK, this also turns AFK ON.
        /// - Uses same restrictions as regular /afk.
        /// </summary>
        private static void HandleAfkAttack(GameClient client, GamePlayer p)
        {
            if (!CheckAfkCommonRestrictions(client, p))
                return;

            var dummy = p.TargetObject as GameTrainingDummy;
            if (dummy == null)
            {
                client.Out.SendMessage(
                    LanguageMgr.GetTranslation(client, "Commands.Players.AfkAttack.NeedDummyTarget"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            bool rangedMode;
            if (!p.IsAfkDummyInValidRange(dummy, out rangedMode))
            {
                client.Out.SendMessage(
                    LanguageMgr.GetTranslation(client, "Commands.Players.AfkAttack.NeedDummyRange"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            bool isArcher = p.IsAfkArcherClass();
            bool isRangedCaster = p.IsAfkRangedOrCasterClass();
            bool isPureMelee = !isArcher && !isRangedCaster;

            if (rangedMode)
            {
                if (isArcher && !p.HasValidRangedBowForAfk())
                {
                    client.Out.SendMessage(
                        LanguageMgr.GetTranslation(client, "Commands.Players.AfkAttack.NeedRangedWeapon"),
                        eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }
            else
            {
                if ((isArcher || isPureMelee) && !p.HasValidMeleeWeaponForAfk())
                {
                    client.Out.SendMessage(
                        LanguageMgr.GetTranslation(client, "Commands.Players.AfkAttack.NeedMeleeWeapon"),
                        eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }

            if (!p.IsAfkActive())
            {
                if (!p.IsAfkDelayElapsed)
                {
                    client.Out.SendMessage(
                        LanguageMgr.GetTranslation(client, "Commands.Players.Afk.Wait"),
                        eChatType.CT_Chat, eChatLoc.CL_SystemWindow);
                    return;
                }

                p.InitAfkTimers();
                p.SetAFK("AFK", showMessage: true);
            }

            // Toggle AFK attack mode ON/OFF.
            if (p.IsAfkAttackMode)
            {
                p.StopAfkAttackMode(showMessage: true);
            }
            else
            {
                p.StartAfkAttackMode(dummy);
            }
        }
    }
}