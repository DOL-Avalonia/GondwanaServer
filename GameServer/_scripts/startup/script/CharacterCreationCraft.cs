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
using System.Collections.Generic;

using DOL.Events;
using DOL.Database;

namespace DOL.GS.GameEvents
{
    /// <summary>
    /// Enable Crafting Level Upon Character Creation
    /// </summary>
    public static class CharacterCreationCraft
    {
        /// <summary>
        /// Register Character Creation Events
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        [ScriptLoadedEvent]
        public static void OnScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(DatabaseEvent.CharacterCreated, new DOLEventHandler(OnCharacterCreation));
        }

        /// <summary>
        /// Unregister Character Creation Events
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(DatabaseEvent.CharacterCreated, new DOLEventHandler(OnCharacterCreation));
        }

        /// <summary>
        /// Triggered When New Character is Created.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public static void OnCharacterCreation(DOLEvent e, object sender, EventArgs args)
        {
            // Check Args
            var chArgs = args as CharacterEventArgs;

            if (chArgs == null)
                return;

            ResetCraftingSkills(chArgs.Character);
        }

        public static void ResetCraftingSkills(DOLCharacters ch)
        {
            int basicCraftingSkill = 1;
            // If player has basic crafting already, keep old value
            if (!ServerProperties.Properties.PLAYERCREATION_PRIMARY_CRAFTINGSKILL && ch.SerializedCraftingSkills != null)
            {
                foreach (string skill in Util.SplitCSV(ch.SerializedCraftingSkills))
                {
                    string[] values = skill.Split('|');
                    if (values.Length > 1 && values[0] == "BasicCrafting" ||
                        (eCraftingSkill)Convert.ToInt32(values[0]) == eCraftingSkill.BasicCrafting)
                    {
                        basicCraftingSkill = Convert.ToInt32(values[1]);
                    }
                }

            }

            if (!ServerProperties.Properties.PLAYERCREATION_PRIMARY_CRAFTINGSKILL)
            {
                // Add all Crafting skills at level 1
                var collectionAllCraftingSkills = new List<string>();
                foreach (int craftingSkillId in Enum.GetValues(typeof(eCraftingSkill)))
                {
                    if (craftingSkillId <= 0) continue;
                    if (craftingSkillId == (int)eCraftingSkill.BasicCrafting)
                    {
                        collectionAllCraftingSkills.Add($"{craftingSkillId}|{basicCraftingSkill}");
                    }
                    else
                    {
                        collectionAllCraftingSkills.Add($"{craftingSkillId}|1");
                    }
                    if (craftingSkillId == (int)eCraftingSkill._Last)
                        break;
                }

                // Set Primary Skill to Basic.
                ch.SerializedCraftingSkills = string.Join(";", collectionAllCraftingSkills);
                ch.CraftingPrimarySkill = (int)eCraftingSkill.BasicCrafting;
            }
            else
            {
                ch.SerializedCraftingSkills = $"{(int)eCraftingSkill.BasicCrafting}|{basicCraftingSkill}";
                ch.CraftingPrimarySkill = (int)eCraftingSkill.BasicCrafting;
            }
        }

        public static void ResetCraftingSkills(GamePlayer player)
        {
            player.ResetCraftingSkills();
        }

    }
}
