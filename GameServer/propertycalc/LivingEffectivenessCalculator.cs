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
using DOL.AI;
using DOL.AI.Brain;

namespace DOL.GS.PropertyCalc
{
    /// <summary>
    /// Calculator for Robbery Resist
    /// </summary>
    [PropertyCalculator(eProperty.LivingEffectiveness)]
    public class LivingEffectivenessCalculator : PropertyCalculator
    {
        public override int CalcValue(GameLiving living, eProperty property)
        {
            double value = living.BaseEffectiveness;
            int bonus = 100;
            
            bonus += Math.Min(10, living.ItemBonus[eProperty.LivingEffectiveness]);
            bonus += Math.Min(30, living.BaseBuffBonusCategory[eProperty.LivingEffectiveness]);
            bonus += Math.Min(50, living.SpecBuffBonusCategory[eProperty.LivingEffectiveness]);
            bonus += Math.Min(50, living.AbilityBonus[eProperty.LivingEffectiveness]);
            bonus += Math.Min(30, living.OtherBuffBonus[eProperty.LivingEffectiveness]);
            bonus -= Math.Max(0, living.DebuffCategory[eProperty.LivingEffectiveness]);
            bonus -= Math.Max(0, living.SpecDebuffCategory[eProperty.LivingEffectiveness]);
            value *= Math.Round(value * bonus / 100);
            value *= living.BuffBonusMultCategory1.Get((int)eProperty.LivingEffectiveness);
            return (int)Math.Max(0, Math.Round(value * 100));
        }

        /// <inheritdoc />
        public override int CalcValueBase(GameLiving living, eProperty property)
        {
            double value = living.BaseEffectiveness;
            int bonus = 100;
            
            bonus += Math.Min(10, living.ItemBonus[eProperty.LivingEffectiveness]);
            value *= Math.Round(value * bonus / 100);
            return (int)Math.Max(0, value * 100);
        }
    }
}
