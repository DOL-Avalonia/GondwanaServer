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
    public class LootGeneratorWood : LootGeneratorBase
    {
        private static readonly List<string> _wooden_boardTemplateIds = new List<string> { "rowan_wooden_boards", "elm_wooden_boards", "oak_wooden_boards", "ironwood_wooden_boards", "heartwood_wooden_boards", "runewood_wooden_boards", "stonewood_wooden_boards", "ebonwood_wooden_boards", "dyrwood_wooden_boards", "duskwood_wooden_boards" };
        private static readonly Dictionary<string, float> _wooden_boardValue = new Dictionary<string, float> {
            { "rowan_wooden_boards", 5},
            { "elm_wooden_boards", 8},
            { "oak_wooden_boards", 12},
            { "ironwood_wooden_boards", 20},
            { "heartwood_wooden_boards", 25},
            { "runewood_wooden_boards", 30},
            { "stonewood_wooden_boards", 35},
            { "ebonwood_wooden_boards", 38},
            { "dyrwood_wooden_boards", 42},
            { "duskwood_wooden_boards", 45},
        };
        private readonly ItemTemplate[] _wooden_boards;

        public LootGeneratorWood() : base()
        {
            this._wooden_boards = GameServer.Database.SelectObjects<ItemTemplate>(it => _wooden_boardTemplateIds.Contains(it.Id_nb)).ToArray();
        }

        private int CalculateWoodenBoardChance(int mobLevel, float woodenBoardValue)
        {
            // Calculate the chance based on player and wooden board level
            int levelDifference = mobLevel - (int)woodenBoardValue;
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
                if ((int)woodenBoardValue == 45)
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

            var woodenBoards = new LootList(1);
            foreach (var woodenBoard in _wooden_boards)
            {
                if (_wooden_boardValue.TryGetValue(woodenBoard.Id_nb, out float value))
                {
                    int chance = CalculateWoodenBoardChance(mob.Level, value);
                    if (chance > 0)
                    {
                        woodenBoards.AddRandom(chance, woodenBoard, 1);
                    }
                }
            }

            foreach (var woodenBoard in woodenBoards.GetLoot())
                loot.AddFixed(woodenBoard, 1);

            return loot;
        }
    }
}
