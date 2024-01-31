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
    /// Viking Trainer
    /// </summary>
    [NPCGuildScript("Viking Trainer", eRealm.Midgard)]      // this attribute instructs DOL to use this script for all "Acolyte Trainer" NPC's in Albion (multiple guilds are possible for one script)
    public class VikingTrainer : GameTrainer
    {
        public const string PRACTICE_WEAPON_ID = "training_axe";
        public const string PRACTICE_SHIELD_ID = "small_training_shield";

        public override eCharacterClass TrainedClass
        {
            get { return eCharacterClass.Viking; }
        }

        public VikingTrainer() : base(eChampionTrainerType.Viking)
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
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                else
                {
                    OfferTraining(player);
                }

                // ask for basic equipment if player doesnt own it
                if (player.Inventory.GetFirstItemByID(PRACTICE_WEAPON_ID, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == null)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if (player.Inventory.GetFirstItemByID(PRACTICE_SHIELD_ID, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == null)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Interact.Text3", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
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
                case "Warrior":
                case "Guerrier":
                    if (player.Race == (int)eRace.Dwarf || player.Race == (int)eRace.Kobold || player.Race == (int)eRace.Norseman || player.Race == (int)eRace.Troll || player.Race == (int)eRace.Valkyn || player.Race == (int)eRace.MidgardMinotaur)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Warrior.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Warrior.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    return true;
                case "Berserker":
                    if (player.Race == (int)eRace.Dwarf || player.Race == (int)eRace.Troll || player.Race == (int)eRace.Norseman || player.Race == (int)eRace.Valkyn || player.Race == (int)eRace.MidgardMinotaur)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Berserker.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Berserker.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    return true;
                case "Skald":
                    if (player.Race == (int)eRace.Dwarf || player.Race == (int)eRace.Kobold || player.Race == (int)eRace.Norseman || player.Race == (int)eRace.Troll || player.Race == (int)eRace.MidgardMinotaur)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Skald.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Skald.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    return true;
                case "Thane":
                    if (player.Race == (int)eRace.Dwarf || player.Race == (int)eRace.Frostalf || player.Race == (int)eRace.Norseman || player.Race == (int)eRace.Troll || player.Race == (int)eRace.MidgardMinotaur || player.Race == (int)eRace.Valkyn)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Thane.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Thane.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    return true;
                case "Savage":
                case "Sauvage":
                    if (player.Race == (int)eRace.Dwarf || player.Race == (int)eRace.Kobold || player.Race == (int)eRace.Norseman || player.Race == (int)eRace.Troll || player.Race == (int)eRace.Valkyn)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Savage.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Savage.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    return true;
                case "Valkyrie":
                    if (player.Race == (int)eRace.Dwarf || player.Race == (int)eRace.Valkyn || player.Race == (int)eRace.Norseman || player.Race == (int)eRace.Frostalf)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Valkyrie.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "VikingTrainer.Valkyrie.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    return true;
                case "Mauler":
                case "Kan-Laresh":
                    if (player.Race == (int)eRace.Kobold || player.Race == (int)eRace.Norseman || player.Race == (int)eRace.Troll || player.Race == (int)eRace.MidgardMinotaur)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Baseclass.Mauler.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Baseclass.Mauler.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    }
                case "practice weapon":
                case "arme d'entraînement":
                    if (player.Inventory.GetFirstItemByID(PRACTICE_WEAPON_ID, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == null)
                    {
                        player.ReceiveItem(this, PRACTICE_WEAPON_ID, eInventoryActionType.Other);
                    }
                    return true;
                case "training shield":
                case "bouclier d'entraînement":
                    if (player.Inventory.GetFirstItemByID(PRACTICE_SHIELD_ID, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) == null)
                    {
                        player.ReceiveItem(this, PRACTICE_SHIELD_ID, eInventoryActionType.Other);
                    }
                    return true;
            }
            return true;
        }
    }
}
