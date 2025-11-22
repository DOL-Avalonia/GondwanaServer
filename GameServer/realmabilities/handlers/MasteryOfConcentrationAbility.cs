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
using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Events;
using DOL.Database;
using DOL.Language;
using DOL.GS.Spells;

namespace DOL.GS.RealmAbilities
{
    public class MasteryofConcentrationAbility : TimedRealmAbility
    {
        public MasteryofConcentrationAbility(DBAbility dba, int level) : base(dba, level) { }
        public const Int32 Duration = 30 * 1000;

        public override void Execute(GameLiving living)
        {
            if (CheckPreconditions(living, DEAD | SITTING | MEZZED | STUNNED)) return;
            GamePlayer caster = living as GamePlayer;

            if (caster == null)
                return;

            MasteryofConcentrationEffect MoCEffect = caster.EffectList.GetOfType<MasteryofConcentrationEffect>();
            if (MoCEffect != null)
            {
                MoCEffect.Cancel(false);
                return;
            }

            // Check for the RA5L on the Sorceror: he cannot cast MoC when the other is up
            ShieldOfImmunityEffect ra5l = caster.EffectList.GetOfType<ShieldOfImmunityEffect>();
            if (ra5l != null)
            {
                caster.Out.SendMessage(LanguageMgr.GetTranslation(caster.Client, "MasteryofConcentrationAbility.BlockedByShield"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return;
            }

            // If Cleric Ascendance is active and not permissive, cancel it before applying MoC
            GameSpellEffect ascEff = SpellHandler.FindEffectOnTarget(caster, "Ascendance");
            if (ascEff != null && (ascEff.Spell?.AmnesiaChance ?? 0) <= 0)
            {
                ascEff.Cancel(false);
                caster.Out.SendMessage(LanguageMgr.GetTranslation(caster.Client, "SpellHandler.Cleric.Ascendance.MocCanceledAscendance"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }

            SendCasterSpellEffectAndCastMessage(living, 7007, true);
            foreach (GamePlayer player in caster.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (caster.IsWithinRadius(player, WorldMgr.INFO_DISTANCE))
                {
                    if (player == caster)
                    {
                        player.MessageToSelf(LanguageMgr.GetTranslation(player.Client, "MasteryofConcentrationAbility.Self.Cast", this.Name), eChatType.CT_Spell);
                        player.MessageToSelf(LanguageMgr.GetTranslation(player.Client, "MasteryofConcentrationAbility.Self.Steadier"), eChatType.CT_Spell);
                    }
                    else
                    {
                        player.MessageFromArea(caster, LanguageMgr.GetTranslation(player.Client, "MasteryofConcentrationAbility.Area.CastsSpell", player.GetPersonalizedName(caster)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "MasteryofConcentrationAbility.Area.Poise", player.GetPersonalizedName(caster)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                }
            }

            DisableSkill(living);

            new MasteryofConcentrationEffect().Start(caster);
        }
        public override int GetReUseDelay(int level)
        {
            return 600;
        }

        public virtual int GetAmountForLevel(int level)
        {
            if (ServerProperties.Properties.USE_NEW_ACTIVES_RAS_SCALING)
            {
                switch (level)
                {
                    case 1: return 25;
                    case 2: return 35;
                    case 3: return 50;
                    case 4: return 60;
                    case 5: return 75;
                }
            }
            else
            {
                switch (level)
                {
                    case 1: return 25;
                    case 2: return 50;
                    case 3: return 75;
                }
            }
            return 25;
        }
    }
}
