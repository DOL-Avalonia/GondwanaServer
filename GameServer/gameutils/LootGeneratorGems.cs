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
using System.Collections.Generic;
using System.Linq;
using DOL.Database;

namespace DOL.GS
{
    /// <summary>
    /// MoneyLootGenerator
    /// At the moment this generaotr only adds money to the loot
    /// </summary>
    public class LootGeneratorGems : LootGeneratorBase
    {
        private static readonly List<string> _gemTemplateIds = new List<string> { "Lo_gem", "Um_gem", "On_gem", "Ee_gem", "Pal_gem", "Mon_gem", "Ros_gem", "Zo_gem", "Kath_gem", "Ra_gem" };
        private static readonly Dictionary<string, float> _gemValue = new Dictionary<string, float> {
            { "Lo_gem", 5},
            { "Um_gem", 8},
            { "On_gem", 12},
            { "Ee_gem", 20},
            { "Pal_gem", 25},
            { "Mon_gem", 30},
            { "Ros_gem", 35},
            { "Zo_gem", 38},
            { "Kath_gem", 42},
            { "Ra_gem", 45},
        };
        private readonly ItemTemplate[] _gems;

        public LootGeneratorGems() : base()
        {
            this._gems = GameServer.Database.SelectObjects<ItemTemplate>(it => _gemTemplateIds.Contains(it.Id_nb)).ToArray();
        }

        private int CalculateGemChance(int mobLevel, float gemValue)
        {
            // Calculate the chance based on player and gem level
            int levelDifference = mobLevel - (int)gemValue;
            if (mobLevel > 45 && mobLevel <= 59)
            {
                int additionalChance = (mobLevel - 45) * 10;
                int widenedLevelDifference = (mobLevel - 45) + 5;

                if (levelDifference >= -4 && levelDifference <= widenedLevelDifference)
                {
                    return 65 + additionalChance;
                }
                else if (levelDifference >= 5 && levelDifference <= 9)
                {
                    return 18 - additionalChance;
                }
                else if (levelDifference >= -9 && levelDifference <= -5)
                {
                    return 14 - additionalChance;
                }
                else if (levelDifference < -9)
                {
                    return 0;
                }
                else
                {
                    return 3 - additionalChance;
                }
            }
            else if (mobLevel > 59)
            {
                if ((int)gemValue == 45)
                {
                    return 100;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                if (levelDifference >= -4 && levelDifference <= 4)
                {
                    return 65;
                }
                else if (levelDifference >= 5 && levelDifference <= 9)
                {
                    return 18;
                }
                else if (levelDifference >= -9 && levelDifference <= -5)
                {
                    return 14;
                }
                else if (levelDifference < -9)
                {
                    return 0;
                }
                else
                {
                    return 3;
                }
            }
        }

        public override LootList GenerateLoot(GameNPC mob, GameObject killer)
        {
            LootList loot = base.GenerateLoot(mob, killer);
            if (mob.Level > 59)
            {
                if (Util.Chance(30))
                    return loot;
            }
                else
            {
                if (Util.Chance(60))
                    return loot;
            }

            var gems = new LootList(1);
            foreach (var gem in _gems)
            {
                if ( _gemValue.TryGetValue(gem.Id_nb, out float value))
                {
                    int chance = CalculateGemChance(mob.Level, value);
                    if (chance > 0)
                    {
                        gems.AddRandom(chance, gem, 1);
                    }
                }
            }

            foreach (var gem in gems.GetLoot())
                loot.AddFixed(gem, 1);

            return loot;
        }
    }
}
