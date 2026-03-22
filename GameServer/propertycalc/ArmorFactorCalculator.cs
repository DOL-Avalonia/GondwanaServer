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

using DOL.GS.Keeps;

namespace DOL.GS.PropertyCalc
{
    /// <summary>
    /// The Armor Factor calculator
    ///
    /// BuffBonusCategory1 is used for base buffs directly in player.GetArmorAF because it must be capped by item AF cap
    /// BuffBonusCategory2 is used for spec buffs, level*1.875 cap for players
    /// BuffBonusCategory3 is used for debuff, uncapped
    /// BuffBonusCategory4 is used for buffs, uncapped
    /// BuffBonusMultCategory1 unused
    /// ItemBonus is used for players TOA bonuse, living.Level cap
    /// </summary>
    [PropertyCalculator(eProperty.ArmorFactor)]
    [PropertyCalculator(eProperty.ArmorFactor)]
    public class ArmorFactorCalculator : PropertyCalculator
    {
        public override int CalcValue(GameLiving living, eProperty property)
        {
            switch (living)
            {
                case GamePlayer:
                case GameTrainingDummy:
                    return CalculatePlayerArmorFactor(living, property);
                case GameKeepDoor:
                case GameKeepComponent:
                    return CalculateKeepComponentArmorFactor(living);
                case GameBossNPC:
                    return CalculateLivingArmorFactor(living, property, 12.5 /* * epicNpc.ArmorFactorScalingFactor */, 50);
                case NecromancerPet:
                    return CalculateLivingArmorFactor(living, property, 12.5, 121);
                case GamePet:
                    return CalculateLivingArmorFactor(living, property, 12.5, 175);
                case GuardLord:
                    return CalculateLivingArmorFactor(living, property, 12.5, 134);
                default:
                    return CalculateLivingArmorFactor(living, property, 12.5, 200);
            }

            static int CalculatePlayerArmorFactor(GameLiving living, eProperty property)
            {
                // Base AF buffs are calculated in the item's armor calc since they have the same cap.
                int armorFactor = Math.Min((int) (living.Level * 1.875), living.SpecBuffBonusCategory[property]);
                armorFactor -= Math.Abs(living.DebuffCategory[property]);
                armorFactor += Math.Min(living.Level, living.ItemBonus[property]);
                armorFactor += living.OtherBuffBonus[property];
                return armorFactor;
            }

            static int CalculateLivingArmorFactor(GameLiving living, eProperty property, double factor, double divisor)
            {
                int armorFactor = (int) ((1 + living.Level / divisor) * (living.Level * factor));

                if (living is GameNPC npc)
                    armorFactor += npc.ArmorFactor;

                // Some source state either base AF or spec AF isn't supposed to work on NPCs.
                // In any case, having some buffs not doing anything feels pretty bad. Some pets also have a self spec AF buff.
                armorFactor += living.BaseBuffBonusCategory[property] + living.SpecBuffBonusCategory[property];
                armorFactor -= Math.Abs(living.DebuffCategory[property]);
                armorFactor += living.OtherBuffBonus[property];
                return armorFactor;
            }

            static int CalculateKeepComponentArmorFactor(GameLiving living)
            {
                GameKeepComponent component = null;

                if (living is GameKeepDoor keepDoor)
                    component = keepDoor.Component;
                else if (living is GameKeepComponent)
                    component = living as GameKeepComponent;

                if (component == null)
                    return 0;

                double keepLevelMod = 1 + component.Keep.Level * 0.1;
                int typeMod = component.Keep is GameKeep ? 24 : 12;
                return (int) (component.Keep.BaseLevel * keepLevelMod * typeMod);
            }
        }
    }
}
