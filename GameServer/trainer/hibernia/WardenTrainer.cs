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
using DOL.Language;

namespace DOL.GS.Trainer
{
    /// <summary>
    /// Warden Trainer
    /// </summary>
    [NPCGuildScript("Warden Trainer", eRealm.Hibernia)]     // this attribute instructs DOL to use this script for all "Warden Trainer" NPC's in Albion (multiple guilds are possible for one script)
    public class WardenTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass
        {
            get { return eCharacterClass.Warden; }
        }

        /// <summary>
        /// The free starter armor from trainer
        /// </summary>
        public const string ARMOR_ID1 = "warden_item";


        /// <summary>
        /// Interact with trainer
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            // check if class matches.
            if (player.CharacterClass.ID == (int)TrainedClass)
            {
                player.Out.SendMessage(this.Name + " says, \"Do you wish to learn some more, " + player.Name + "? Step up and receive your training!\"", eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            }
            else
            {
                // perhaps player can be promoted
                if (CanPromotePlayer(player))
                {
                    player.Out.SendMessage(this.Name + " says, \"Nidewst! You wish to follow the Path of Focus and train as a [Warden]?\"", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    if (!player.IsLevelRespecUsed)
                    {
                        OfferRespecialize(player);
                    }
                }
                else
                {
                    CheckChampionTraining(player);
                }
            }
            return true;
        }

        /// <summary>
        /// Talk to trainer
        /// </summary>
        /// <param name="source"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text)) return false;
            GamePlayer player = source as GamePlayer;

            switch (text)
            {
                case "Warden":
                    // promote player to other class
                    if (CanPromotePlayer(player))
                    {
                        PromotePlayer(player, (int)eCharacterClass.Warden, "Good then! Welcome to the ways of the Warden! Here, take this as a gift, to start you on the path of a Warden.", null); // TODO: gifts
                        player.ReceiveItem(this, ARMOR_ID1, eInventoryActionType.Other);
                    }
                    break;
            }
            return true;
        }
    }
}
