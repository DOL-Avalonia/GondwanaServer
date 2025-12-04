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

using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Language;
using System.Numerics;
using DOL.GS.Styles;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    [SpellHandler("Bladeturn")]
    public class BladeturnSpellHandler : SpellHandler
    {
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            ConsumePower(target);
            base.FinishSpellCast(target, force);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            eChatType toLiving = (Spell.Pulse == 0) ? eChatType.CT_Spell : eChatType.CT_SpellPulse;
            eChatType toOther = (Spell.Pulse == 0) ? eChatType.CT_System : eChatType.CT_SpellPulse;

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer ownerPlayer = effect.Owner as GamePlayer;

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
            if (!noMessages && Spell.Pulse == 0)
            {
                string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
                GamePlayer ownerPlayer = effect.Owner as GamePlayer;

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
                    if (!(effect.Owner == player))
                    {
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                        string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                        player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            return 0;
        }

        public static bool BlockAttack(GameSpellEffect bladeturnEffect, AttackData ad)
        {
            if (bladeturnEffect.SpellHandler is not BladeturnSpellHandler)
                return false;
            
            GamePlayer playerAttacker = ad.Attacker as GamePlayer;
            bool penetrate = false;
            bool consume = true;
            double missChance = (double)bladeturnEffect.SpellHandler.Caster.EffectiveLevel / (double)ad.Attacker.EffectiveLevel;
            if (ad is { Style.StealthRequirement: true, Attacker: GamePlayer } && StyleProcessor.CanUseStyle((GamePlayer)ad.Attacker, ad.Target, ad.Style, ad.Weapon)) // stealth styles pierce bladeturn
            {
                penetrate = true;
            }
            else
            {
                bool penetratingArrow = ad is { AttackType: AttackData.eAttackType.Ranged } && ad.Target != bladeturnEffect.SpellHandler.Caster && playerAttacker?.HasAbility(Abilities.PenetratingArrow) == true;
                if (ad.Attacker.RangedAttackType == GameLiving.eRangedAttackType.Long
                    || penetratingArrow ) // penetrating arrow attack pierce bladeturn
                {
                    penetrate = true;
                }
                else if (ad.IsMeleeAttack && !Util.ChanceDouble(missChance))
                {
                    penetrate = true;
                }
                else if (ad.SpellHandler is Archery)
                {
                    switch (ad.SpellHandler.Spell.LifeDrainReturn)
                    {
                        case (int)Archery.eShotType.Critical:
                            // Crits penetrate but do not consume the effect (https://github.com/DOL-Avalonia/GondwanaServer/blob/6302afd11387bedcb06c1e296296258c2b6e8767/GameServer/spells/Archery/Archery.cs#L214)
                            penetrate = true;
                            consume = false;
                            break;
                        
                        case (int)Archery.eShotType.Power:
                            penetrate = true;
                            consume = true;
                            break;
                        
                        default:
                            penetrate = false;
                            consume = true;
                            break;
                    }
                }
            }

            GamePlayer playerOwner = bladeturnEffect.Owner as GamePlayer;
            if (penetrate)
            {
                playerAttacker?.SendTranslatedMessage("SpellHandler.Archery.PenetrateBarrier", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
            }
            else
            {
                playerAttacker?.SendTranslatedMessage("SpellHandler.Archery.StrikeAbsorbed", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                playerOwner?.SendTranslatedMessage("SpellHandler.Archery.BlowAbsorbed", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                ad.AttackResult = GameLiving.eAttackResult.Missed;
                ad.MissChance = missChance;
            }

            if (consume && !GameServer.ServerRules.IsPveOnlyBonus(eProperty.BladeturnReinforcement) || !GameServer.ServerRules.IsPvPAction(ad.Attacker, ad.Target, false))
            {
                int chanceToKeep = bladeturnEffect.Owner.GetModified(eProperty.BladeturnReinforcement);
                if (chanceToKeep > 0 && Util.Chance(chanceToKeep))
                {
                    playerOwner?.SendTranslatedMessage("GameLiving.CalculateEnemyAttackResult.BladeturnKept", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                    consume = false;
                }
            }

            if (consume)
            {
                bladeturnEffect.Cancel(false);
                bladeturnEffect.Owner.Stealth(false);
            }
            return !penetrate;
        }

        public override PlayerXEffect GetSavedEffect(GameSpellEffect e)
        {
            PlayerXEffect eff = new PlayerXEffect();
            eff.Var1 = Spell.ID;
            eff.Duration = e.RemainingTime;
            eff.IsHandler = true;
            eff.Var2 = (int)(Spell.Value * e.Effectiveness);
            eff.SpellLine = SpellLine.KeyName;
            return eff;
        }

        public override int OnRestoredEffectExpires(GameSpellEffect effect, int[] vars, bool noMessages)
        {
            if (!noMessages && Spell.Pulse == 0)
            {
                MessageToLiving(effect.Owner, Spell.Message3, eChatType.CT_SpellExpires);
                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player))
                    {
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);
                        player.MessageFromArea(effect.Owner, Util.MakeSentence(Spell.Message4, personalizedTargetName), eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            return 0;
        }

        public BladeturnSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string description = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Bladeturn.MainDescription");

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return description + "\n\n" + secondDesc;
            }

            return description;
        }
    }
}
