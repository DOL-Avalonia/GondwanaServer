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
using DOL.GS.Effects;
using DOL.GS.Spells;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.Utils;
using DOL.Language;

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

            GameLiving living = sender as GameLiving;
            if (living == null)
                return;

            AttackData ad = args.AttackData;
            if (ad is not { AttackType: AttackData.eAttackType.Spell or AttackData.eAttackType.DoT, AttackResult: GameLiving.eAttackResult.HitUnstyled or GameLiving.eAttackResult.HitStyle })
                return;

            int chanceToReflect = living.GetModified(eProperty.MythicalSpellReflect);
            if (chanceToReflect <= 0 || !Util.Chance(chanceToReflect))
                return;

            Spell spellToCast = ad.SpellHandler.Spell.Copy();
            SpellLine line = ad.SpellHandler.SpellLine;

            if (ad.SpellHandler.Parent is BomberSpellHandler bomber)
            {
                spellToCast = bomber.Spell.Copy();
                line = bomber.SpellLine;
            }

            spellToCast.Power = spellToCast.Power * 20 / 100;
            spellToCast.Damage = spellToCast.Damage * 30 / 100;
            spellToCast.Value = spellToCast.Value * 30 / 100;
            spellToCast.Duration = spellToCast.Duration * 30 / 100;
            spellToCast.CastTime = 0;

            double absorbPercent = 30; // Fixed value for Mythical Spell Reflect
            int damageAbsorbed = (int)(0.01 * absorbPercent * (ad.Damage + ad.CriticalDamage));

            ad.Damage -= damageAbsorbed;

            if (damageAbsorbed > 0)
            {
                if (living is GamePlayer player)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "MythicalSpellReflect.Self.Absorb", damageAbsorbed), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
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

            foreach (GamePlayer pl in living.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                pl.Out.SendSpellEffectAnimation(living, living, clientEffect, 0, false, 1);
            }

            ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(living, spellToCast, line);
            if (spellHandler is BomberSpellHandler bomberSpell)
            {
                bomberSpell.ReduceSubSpellDamage = 30;
            }

            const string MYTH_REFLECT_ABSORB_FLAG = "MYTH_REFLECT_ABSORB_PCT_THIS_HIT";
            living.TempProperties.setProperty(MYTH_REFLECT_ABSORB_FLAG, 30);
            living.TempProperties.setProperty("MYTH_REFLECT_ABSORB_TICK", living.CurrentRegion.Time);

            spellHandler.StartSpell(ad.Attacker, false);
        }
    }
}