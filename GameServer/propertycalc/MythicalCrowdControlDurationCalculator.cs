﻿/*
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

namespace DOL.GS.PropertyCalc
{
    /// <summary>
    /// Calculator for Mythical Tension
    /// </summary>
    [PropertyCalculator(eProperty.MythicalCrowdDuration)]
    public class MythicalCrowdControlDurationCalculator : PropertyCalculator
    {
        public override int CalcValue(GameLiving living, eProperty property)
        {
            int percent = 100
                - living.BaseBuffBonusCategory[(int)property] // buff reduce the duration
                + living.DebuffCategory[(int)property]
                - Math.Min(15, living.ItemBonus[(int)property]) // cap 15% from items
                - living.AbilityBonus[(int)property];

            return Math.Max(1, percent);
        }
    }
}