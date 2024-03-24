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
    /// Acolyte Trainer
    /// </summary>
    [NPCGuildScript("Acolyte Trainer", eRealm.Albion)]      // this attribute instructs DOL to use this script for all "Acolyte Trainer" NPC's in Albion (multiple guilds are possible for one script)
    public class AcolyteTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass
        {
            get { return eCharacterClass.Acolyte; }
        }

        public const string PRACTICE_WEAPON_ID = "training_mace";
        public const string PRACTICE_SHIELD_ID = "small_training_shield";

        public AcolyteTrainer()
            : base(eChampionTrainerType.Acolyte)
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
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AcolyteTrainer.Interact.Text1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                else
                {
                    OfferTraining(player);
                }

                // ask for basic equipment if player doesnt own it
                if (player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AcolyteTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if (player.Inventory.CountItemTemplate(PRACTICE_SHIELD_ID, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AcolyteTrainer.Interact.Text3", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
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
                case "Cleric":
                case "Clerc":
                    if (player.Race == (int)eRace.Avalonian || player.Race == (int)eRace.Briton || player.Race == (int)eRace.Highlander)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AcolyteTrainer.Cleric.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AcolyteTrainer.Cleric.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    }
                    return true;
                case "Friar":
                case "Moine":
                    if (player.Race == (int)eRace.Avalonian || player.Race == (int)eRace.Briton || player.Race == (int)eRace.Highlander)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AcolyteTrainer.Friar.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AcolyteTrainer.Friar.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    }
                    return true;
                case "Heretic":
                case "Hérétique":
                    if (player.Race == (int)eRace.Briton || player.Race == (int)eRace.Avalonian || player.Race == (int)eRace.Inconnu || player.Race == (int)eRace.AlbionMinotaur || player.Race == (int)eRace.Saracen || player.Race == (int)eRace.Highlander)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AcolyteTrainer.Heretic.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AcolyteTrainer.Heretic.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    }
                    return true;
                case "practice weapon":
                case "arme d'entraînement":
                    if (player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0)
                    {
                        player.ReceiveItem(this, PRACTICE_WEAPON_ID, eInventoryActionType.Other);
                    }
                    return true;
                case "training shield":
                case "bouclier d'entraînement":
                    if (player.Inventory.CountItemTemplate(PRACTICE_SHIELD_ID, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == 0)
                    {
                        player.ReceiveItem(this, PRACTICE_SHIELD_ID, eInventoryActionType.Other);
                    }
                    return true;
            }
            return true;
        }
    }
}
