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
using DOL.Database;
using DOL.Language;
using DOL.GS.PacketHandler;


namespace DOL.GS
{

    public class LeatherCrafting : AbstractCraftingSkill
    {

        public LeatherCrafting()
        {
            Icon = 0x07;
            Name = LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "Crafting.Name.Leathercrafting");
            eSkill = eCraftingSkill.LeatherCrafting;
        }

        public override void GainCraftingSkillPoints(GamePlayer player, Recipe recipe)
        {
            if (Util.Chance(CalculateChanceToGainPoint(player, recipe.Level)))
            {
                if (player.GetCraftingSkillValue(eCraftingSkill.LeatherCrafting) < subSkillCap)
                {
                    player.GainCraftingSkill(eCraftingSkill.LeatherCrafting, 1);
                }
                player.Out.SendUpdateCraftingSkills();
            }
        }
    }
}
