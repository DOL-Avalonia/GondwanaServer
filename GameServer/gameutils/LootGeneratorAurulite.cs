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

using DOL.GS;
using DOL.AI.Brain;
using DOL.Database;

namespace DOL.GS
{

    /// <summary>
    /// LootGeneratorAurulite
    /// At the moment this generator only adds aurulite to the loot
    /// </summary>
    public class LootGeneratorAurulite : LootGeneratorBase
    {

        public static ItemTemplate m_aurulite = GameServer.Database.FindObjectByKey<ItemTemplate>("aurulite");

        /// <summary>
        /// Generate loot for given mob
        /// </summary>
        /// <param name="mob"></param>
        /// <param name="killer"></param>
        /// <returns>Lootlist with Aurulite drops</returns>
        public override LootList GenerateLoot(GameObject mob, GameObject killer)
        {
            LootList loot = base.GenerateLoot(mob, killer);

            // ItemTemplate aurulite = new ItemTemplate(m_aurulite);  Creating a new ItemTemplate throws an exception later
            ItemTemplate aurulite = GameServer.Database.FindObjectByKey<ItemTemplate>(m_aurulite.Id_nb);


            try
            {
                if ((killer as GameLiving)?.GetController() is not GamePlayer player)
                    return loot;

                int killedcon = (int)player.GetConLevel(mob) + 3;

                if (killedcon <= 0)
                    return loot;

                int lvl = mob.Level + 1;
                if (lvl < 1) lvl = 1;
                int maxcount = 1;

                //Switch pack size
                if (lvl > 0 && lvl < 10)
                {
                    //Aurulite only
                    maxcount = (int)Math.Floor((double)(lvl / 2)) + 1;
                }
                else if (lvl >= 10 && lvl < 20)
                {
                    //Aurulire Chip (x5)
                    aurulite.PackSize = 5;
                    maxcount = (int)Math.Floor((double)((lvl - 10) / 2)) + 1;
                }
                else if (lvl >= 20 && lvl < 30)
                {
                    //Aurulite Fragment (x10)
                    aurulite.PackSize = 10;
                    maxcount = (int)Math.Floor((double)((lvl - 20) / 2)) + 1;

                }
                else if (lvl >= 30 && lvl < 40)
                {
                    //Aurulite Shard (x20)
                    aurulite.PackSize = 20;
                    maxcount = (int)Math.Floor((double)((lvl - 30) / 2)) + 1;
                }
                else if (lvl >= 40 && lvl < 50)
                {
                    //Aurulite Cluster (x30)
                    aurulite.PackSize = 30;
                    maxcount = (int)Math.Floor((double)((lvl - 40) / 2)) + 1;
                }
                else
                {
                    //Aurulite Cache (x40)
                    aurulite.PackSize = 40;
                    maxcount = (int)Math.Round((double)(lvl / 10));
                }

                if (mob is GameNPC npc && npc.IsBoss) // replaces "if (!mob.Name.ToLower().Equals(mob.Name))"  -> Boss mobs drop more aurulite
                {
                    //Named mob or Boss, more cash !
                    maxcount = (int)Math.Round(maxcount * ServerProperties.Properties.LOOTGENERATOR_AURULITE_NAMED_COUNT);
                }

                // Calculate the base chance and apply the loot chance modifier
                int baseChance = ServerProperties.Properties.LOOTGENERATOR_AURULITE_BASE_CHANCE + Math.Max(10, killedcon);
                int lootChanceModifier = player.LootChance;
                int finalChance = Math.Min(100, baseChance + lootChanceModifier);

                // add to loot
                if (maxcount > 0 && Util.Chance(finalChance))
                {
                    // Add to fixed to prevent overrides with loottemplate
                    loot.AddFixed(aurulite, (int)Math.Ceiling(maxcount * ServerProperties.Properties.LOOTGENERATOR_AURULITE_AMOUNT_RATIO));
                }

            }
            catch
            {
                // Prevent displaying errors
                return loot;
            }

            return loot;
        }
    }
}
