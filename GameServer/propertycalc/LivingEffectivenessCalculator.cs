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
    /// Calculator for Living Effectiveness
    /// </summary>
    [PropertyCalculator(eProperty.LivingEffectiveness)]
    public class LivingEffectivenessCalculator : PropertyCalculator
    {
        public override int CalcValue(GameLiving living, eProperty property)
        {
            double value = living.BaseEffectiveness;
            int itemBonus = Math.Min(10, living.ItemBonus[eProperty.LivingEffectiveness]);

            int buffBonus = Math.Min(30, living.BaseBuffBonusCategory[eProperty.LivingEffectiveness])
                          + Math.Min(50, living.SpecBuffBonusCategory[eProperty.LivingEffectiveness])
                          + Math.Min(50, living.AbilityBonus[eProperty.LivingEffectiveness])
                          + Math.Min(30, living.OtherBuffBonus[eProperty.LivingEffectiveness]);

            int debuff = Math.Max(0, living.DebuffCategory[eProperty.LivingEffectiveness])
                       + Math.Max(0, living.SpecDebuffCategory[eProperty.LivingEffectiveness]);

            double mult = living.BuffBonusMultCategory1.Get((int)eProperty.LivingEffectiveness);

            if (living is GamePet pet)
            {
                GameLiving owner = pet.GetLivingOwner();
                if (owner != null && owner != living)
                {
                    itemBonus += Math.Min(10, owner.ItemBonus[eProperty.LivingEffectiveness]);

                    int ownerBuffBonus = Math.Min(30, owner.BaseBuffBonusCategory[eProperty.LivingEffectiveness])
                                       + Math.Min(50, owner.SpecBuffBonusCategory[eProperty.LivingEffectiveness])
                                       + Math.Min(50, owner.AbilityBonus[eProperty.LivingEffectiveness])
                                       + Math.Min(30, owner.OtherBuffBonus[eProperty.LivingEffectiveness]);

                    buffBonus += ownerBuffBonus;

                    debuff += Math.Max(0, owner.DebuffCategory[eProperty.LivingEffectiveness])
                            + Math.Max(0, owner.SpecDebuffCategory[eProperty.LivingEffectiveness]);

                    mult *= owner.BuffBonusMultCategory1.Get((int)eProperty.LivingEffectiveness);
                    value *= owner.BaseEffectiveness;
                }

                buffBonus = Math.Min(25, buffBonus);
            }

            int bonus = 100 + itemBonus + buffBonus - debuff;
            value *= (bonus / 100.0);
            value *= mult;

            return (int)Math.Max(0, Math.Round(value * 100));
        }

        /// <inheritdoc />
        public override int CalcValueBase(GameLiving living, eProperty property)
        {
            double value = living.BaseEffectiveness;
            int itemBonus = Math.Min(10, living.ItemBonus[eProperty.LivingEffectiveness]);

            if (living is GamePet pet)
            {
                GameLiving owner = pet.GetLivingOwner();
                if (owner != null && owner != living)
                {
                    itemBonus += Math.Min(10, owner.ItemBonus[eProperty.LivingEffectiveness]);
                    value *= owner.BaseEffectiveness;
                }
            }

            int bonus = 100 + itemBonus;
            value *= (bonus / 100.0);

            return (int)Math.Max(0, Math.Round(value * 100));
        }
    }
}