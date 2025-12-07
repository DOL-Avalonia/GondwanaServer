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


using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.PlayerClass;
using DOL.GS.Spells;
using DOL.GS.Utils;
using DOL.Language;
using System;

namespace DOL.GS.PropertyCalc
{
    /// <summary>
    /// Calculator for Mythical Spell Reflect
    /// </summary>
    [PropertyCalculator(eProperty.MythicalSpellReflect)]
    public class MythicalSpellReflectCalculator : PropertyCalculator
    {
        public override int CalcValue(GameLiving living, eProperty property)
        {
            int itemBonus = living.ItemBonus[(int)property];
            int buffBonus = living.OtherBuffBonus[eProperty.MythicalSpellReflect];
            int debuff = living.DebuffCategory[eProperty.MythicalSpellReflect];
            int value = (buffBonus + Math.Min(100, itemBonus) - debuff) / 2;
            return Math.Max(0, value);
        }
    }

    public class MythicalSpellReflectHandler
    {
        public static void ApplyEffect(DOLEvent e, object sender, EventArgs arguments)
        {
            if (arguments is not AttackedByEnemyEventArgs args)
                return;

            GameLiving defender = sender as GameLiving;
            if (defender == null)
                return;

            AttackData ad = args.AttackData;
            if (ad is not { AttackType: AttackData.eAttackType.Spell, AttackResult: GameLiving.eAttackResult.HitUnstyled or GameLiving.eAttackResult.HitStyle })
                return;

            int chanceToReflect = defender.GetModified(eProperty.MythicalSpellReflect);
            if (chanceToReflect < 100 && (chanceToReflect <= 0 || !Util.Chance(chanceToReflect)))
                return;

            Spell spellToCast;
            SpellLine line;

            if (ad.SpellHandler.Parent is BomberSpellHandler bomber)
            {
                spellToCast = bomber.Spell.Copy();
                line = bomber.SpellLine;
            }
            else
            {
                spellToCast = ad.SpellHandler.Spell.Copy();
                line = ad.SpellHandler.SpellLine;
            }

            double power = spellToCast.Power * 0.20;
            GamePlayer? defenderPlayer = ad.Target as GamePlayer;
            spellToCast.PowerType = Spell.ePowerType.Mana;
            if (defenderPlayer is { CharacterClass.PowerType: Spell.ePowerType.Endurance })
            {
                spellToCast.PowerType = Spell.ePowerType.Endurance;
                power *= 1.5; // +50% usage of endurance compared to mana
            }

            spellToCast.Power = (int)Math.Round(power);
            spellToCast.Damage = spellToCast.Damage * 30 / 100;
            spellToCast.Value = spellToCast.Value * 30 / 100;
            spellToCast.Duration = spellToCast.Duration * 30 / 100;
            spellToCast.CastTime = 0;

            double absorbPercent = 30; // Fixed value for Mythical Spell Reflect
            int damageAbsorbed = (int)(0.01 * absorbPercent * (ad.Damage + ad.CriticalDamage));

            ad.Damage -= damageAbsorbed;

            if (damageAbsorbed > 0)
            {
                if (defenderPlayer != null)
                {
                    defenderPlayer.Out.SendMessage(LanguageMgr.GetTranslation(defenderPlayer.Client, "MythicalSpellReflect.Self.Absorb", damageAbsorbed), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
                if (ad.Attacker is GamePlayer attacker)
                {
                    attacker.Out.SendMessage(LanguageMgr.GetTranslation(attacker.Client, "MythicalSpellReflect.Target.Absorbs", damageAbsorbed), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                }
            }

            ushort clientEffect = ad.DamageType switch
            {
                eDamageType.Body => 6172,
                eDamageType.Cold => 6057,
                eDamageType.Energy => 6173,
                eDamageType.Heat => 6171,
                eDamageType.Matter => 6174,
                eDamageType.Spirit => 6175,
                _ => 6173,
            };

            foreach (GamePlayer pl in defender.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                pl.Out.SendSpellEffectAnimation(defender, defender, clientEffect, 0, false, 1);
            }

            const string MYTH_REFLECT_ABSORB_FLAG = "MYTH_REFLECT_ABSORB_PCT_THIS_HIT";
            defender.TempProperties.setProperty(MYTH_REFLECT_ABSORB_FLAG, 30);
            defender.TempProperties.setProperty("MYTH_REFLECT_ABSORB_TICK", defender.CurrentRegion.Time);

            if (ad.SpellHandler.Spell.Target is "ground" || (ad.SpellHandler.Target != ad.Target && ad.SpellHandler.Spell.Radius > 0))
            {
                // Don't reflect if being the secondary target of an AOE
                // TODO: Does this check work well when an AOE spell was redirected?
                return;
            }

            ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(defender, spellToCast, line);
            if (spellHandler is BomberSpellHandler bomberSpell)
            {
                bomberSpell.ReduceSubSpellDamage = 30;
            }

            spellHandler.StartSpell(ad.Attacker, false);
        }
    }
}