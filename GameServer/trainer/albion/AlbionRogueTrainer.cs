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
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Trainer
{
    /// <summary>
    /// Albion Rogue Trainer
    /// </summary>
    [NPCGuildScript("Rogue Trainer", eRealm.Albion)]        // this attribute instructs DOL to use this script for all "Rogue Trainer" NPC's in Albion (multiple guilds are possible for one script)
    public class AlbionRogueTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass
        {
            get { return eCharacterClass.AlbionRogue; }
        }

        public const string PRACTICE_WEAPON_ID = "practice_dirk";

        public AlbionRogueTrainer() : base(eChampionTrainerType.AlbionRogue)
        {
        }

        /// <summary>
        /// Interact with trainer
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            // check if class matches
            if (player.CharacterClass.ID == (int)TrainedClass)
            {
                // player can be promoted
                if (player.Level >= 5)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AlbionRogueTrainer.Interact.Text1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                else
                {
                    OfferTraining(player);
                }

                // ask for basic equipment if player doesnt own it
                if (player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AlbionRogueTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
            }
            else
            {
                CheckChampionTraining(player);
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
                case "Infiltrator":
                case "Sicaire":
                    if (player.Race == (int)eRace.Briton || player.Race == (int)eRace.Highlander || player.Race == (int)eRace.Saracen || player.Race == (int)eRace.Inconnu)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AlbionRogueTrainer.Infiltrator.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AlbionRogueTrainer.Infiltrator.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    }
                    return true;
                case "Minstrel":
                case "Ménestrel":
                    if (player.Race == (int)eRace.Briton || player.Race == (int)eRace.Saracen || player.Race == (int)eRace.Highlander || player.Race == (int)eRace.Inconnu)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AlbionRogueTrainer.Minstrel.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AlbionRogueTrainer.Minstrel.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    }
                    return true;
                case "Scout":
                case "Éclaireur":
                    if (player.Race == (int)eRace.Briton || player.Race == (int)eRace.Saracen || player.Race == (int)eRace.Highlander || player.Race == (int)eRace.Inconnu)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AlbionRogueTrainer.Scout.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AlbionRogueTrainer.Scout.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    }
                    return true;
                case "practice weapon":
                case "arme d'entraînement":
                    if (player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0)
                    {
                        player.ReceiveItem(this, PRACTICE_WEAPON_ID, eInventoryActionType.Other);
                    }
                    return true;
            }
            return true;
        }
    }
}
