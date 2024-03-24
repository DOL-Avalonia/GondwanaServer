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
    /// Naturalist Trainer
    /// </summary>
    [NPCGuildScript("Naturalist Trainer", eRealm.Hibernia)]     // this attribute instructs DOL to use this script for all "Naturalist Trainer" NPC's in Albion (multiple guilds are possible for one script)
    public class NaturalistTrainer : GameTrainer
    {
        public override eCharacterClass TrainedClass
        {
            get { return eCharacterClass.Naturalist; }
        }

        public const string PRACTICE_WEAPON_ID = "training_club";
        public const string PRACTICE_SHIELD_ID = "training_shield";

        public NaturalistTrainer() : base(eChampionTrainerType.Naturalist)
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
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NaturalistTrainer.Interact.Text1", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                else
                {
                    OfferTraining(player);
                }

                // ask for basic equipment if player doesnt own it
                if (player.Inventory.CountItemTemplate(PRACTICE_WEAPON_ID, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NaturalistTrainer.Interact.Text2", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                if (player.Inventory.CountItemTemplate(PRACTICE_SHIELD_ID, eInventorySlot.MinEquipable, eInventorySlot.LastBackpack) == 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NaturalistTrainer.Interact.Text3", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
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
                case "Bard":
                case "Barde":
                    if (player.Race == (int)eRace.Celt || player.Race == (int)eRace.Firbolg)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NaturalistTrainer.Bard.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NaturalistTrainer.Bard.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    }
                    return true;
                case "Druid":
                case "Druide":
                    if (player.Race == (int)eRace.Celt || player.Race == (int)eRace.Firbolg || player.Race == (int)eRace.Sylvan)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NaturalistTrainer.Druid.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NaturalistTrainer.Druid.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    }
                    return true;
                case "Warden":
                case "Sentinelle":
                    if (player.Race == (int)eRace.Celt || player.Race == (int)eRace.Firbolg || player.Race == (int)eRace.Sylvan || player.Race == (int)eRace.HiberniaMinotaur || player.Race == (int)eRace.Shar)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NaturalistTrainer.Warden.Explain", this.Name), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "NaturalistTrainer.Warden.Refuse", this.Name), eChatType.CT_Say, eChatLoc.CL_ChatWindow);
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
