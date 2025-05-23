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
using DOL.Database;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.GS.Geometry;
using DOL.Language;

namespace DOL.GS.Housing
{
    public class GameLotMarker : GameStaticItem
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        private DBHouse m_dbitem;

        public GameLotMarker()
            : base()
        {
            SaveInDB = false;
        }

        public DBHouse DatabaseItem
        {
            get { return m_dbitem; }
            set { m_dbitem = value; }
        }

        public override IList GetExamineMessages(GamePlayer player)
        {
            IList list = new ArrayList();
            list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Housing.LotMarker.TargetLotNumber", DatabaseItem.HouseNumber));

            if (string.IsNullOrEmpty(DatabaseItem.OwnerID))
            {
                list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Housing.LotMarker.ItCanBeBoughtFor", Money.GetString(HouseTemplateMgr.GetLotPrice(DatabaseItem))));
            }
            else if (!string.IsNullOrEmpty(DatabaseItem.Name))
            {
                list.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "Housing.LotMarker.ItIsOwnedBy", DatabaseItem.Name));
            }

            return list;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            House house = HouseMgr.GetHouseByPlayer(player);

            if (house != null)
            {
                //the player might be targeting a lot he already purchased that has no house on it yet
                if (house.HouseNumber != DatabaseItem.HouseNumber && player.Client.Account.PrivLevel != (int)ePrivLevel.Admin)
                {
                    ChatUtil.SendSystemMessage(player, "Housing.LotMarker.YouAlreadyOwnAHouse");
                    return false;
                }
            }

            if (string.IsNullOrEmpty(DatabaseItem.OwnerID))
            {
                player.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client.Account.Language, "Housing.LotMarker.DoYouWantToBuyThisLot1") + "\r\n" + LanguageMgr.GetTranslation(player.Client.Account.Language, "Housing.LotMarker.DoYouWantToBuyThisLot2", Money.GetString(HouseTemplateMgr.GetLotPrice(DatabaseItem))), BuyLot);
            }
            else
            {
                if (HouseMgr.IsOwner(DatabaseItem, player))
                {
                    player.Out.SendMerchantWindow(HouseTemplateMgr.GetLotMarkerItems(this).Catalog, eMerchantWindowType.Normal);
                }
                else
                {
                    ChatUtil.SendSystemMessage(player, "Housing.LotMarker.YouDoNotOwnThisLot");
                }
            }

            return true;
        }

        private void BuyLot(GamePlayer player, byte response)
        {
            if (response != 0x01)
                return;

            lock (DatabaseItem) // Mannen 10:56 PM 10/30/2006 - Fixing every lock(this)
            {
                if (!string.IsNullOrEmpty(DatabaseItem.OwnerID))
                    return;

                if (HouseMgr.GetHouseNumberByPlayer(player) != 0 && player.Client.Account.PrivLevel != (int)ePrivLevel.Admin)
                {
                    ChatUtil.SendMerchantMessage(player, "Housing.LotMarker.YouAlreadyOwnAnotherLotOrHouse", HouseMgr.GetHouseNumberByPlayer(player));
                    return;
                }

                var totalCost = Currency.Copper.Mint(HouseTemplateMgr.GetLotPrice(DatabaseItem));
                if (player.RemoveMoney(totalCost))
                {
                    player.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Housing.LotMarker.YouJustBoughtThisLotFor", totalCost.ToText()), eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
                    InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Merchant, totalCost.Amount);
                    DatabaseItem.LastPaid = DateTime.Now;
                    DatabaseItem.OwnerID = player.ObjectId;
                    CreateHouse(player, 0);
                }
                else
                {
                    ChatUtil.SendMerchantMessage(player, "Housing.LotMarker.YouDontHaveEnoughMoney");
                }
            }
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (source == null || item == null)
                return false;

            if (!(source is GamePlayer))
                return false;

            var player = (GamePlayer)source;

            if (HouseMgr.IsOwner(DatabaseItem, player))
            {
                switch (item.Id_nb)
                {
                    case "housing_alb_cottage_deed":
                        CreateHouse(player, 1);
                        break;
                    case "housing_alb_house_deed":
                        CreateHouse(player, 2);
                        break;
                    case "housing_alb_villa_deed":
                        CreateHouse(player, 3);
                        break;
                    case "housing_alb_mansion_deed":
                        CreateHouse(player, 4);
                        break;
                    case "housing_mid_cottage_deed":
                        CreateHouse(player, 5);
                        break;
                    case "housing_mid_house_deed":
                        CreateHouse(player, 6);
                        break;
                    case "housing_mid_villa_deed":
                        CreateHouse(player, 7);
                        break;
                    case "housing_mid_mansion_deed":
                        CreateHouse(player, 8);
                        break;
                    case "housing_hib_cottage_deed":
                        CreateHouse(player, 9);
                        break;
                    case "housing_hib_house_deed":
                        CreateHouse(player, 10);
                        break;
                    case "housing_hib_villa_deed":
                        CreateHouse(player, 11);
                        break;
                    case "housing_hib_mansion_deed":
                        CreateHouse(player, 12);
                        break;
                    default:
                        return false;
                }

                player.Inventory.RemoveItem(item);

                // Tolakram:  Is this always null when purchasing a house?
                InventoryLogging.LogInventoryAction(player, CurrentHouse?.DatabaseItem?.ObjectId ?? DatabaseItem.ObjectId, "(HOUSE;" + (CurrentHouse == null ? DatabaseItem.HouseNumber : CurrentHouse.HouseNumber) + ")", eInventoryActionType.Other, item, item.Count);

                return true;
            }

            ChatUtil.SendSystemMessage(player, "Housing.LotMarker.YouDoNotOwnThisLot");

            return false;
        }

        private void CreateHouse(GamePlayer player, int model)
        {
            DatabaseItem.Model = model;
            DatabaseItem.Name = player.Name;

            if (player.Guild != null)
            {
                DatabaseItem.Emblem = player.Guild.Emblem;
            }

            var house = new House(DatabaseItem);
            HouseMgr.AddHouse(house);

            if (model != 0)
            {
                // move all players outside the mesh
                foreach (GamePlayer p in player.GetPlayersInRadius(500))
                {
                    house.Exit(p, true);
                }

                RemoveFromWorld();
                Delete();
            }
        }

        public virtual bool OnPlayerBuy(GamePlayer player, int item_slot, int number)
        {
            GameMerchant.OnPlayerBuy(player, item_slot, number, HouseTemplateMgr.GetLotMarkerItems(this));
            return true;
        }

        public virtual bool OnPlayerSell(GamePlayer player, InventoryItem item)
        {
            if (!item.IsDropable || item.Flags == 1)
            {
                ChatUtil.SendMerchantMessage(player, "Housing.LotMarker.ThisItemCantBeSold");
                return false;
            }

            return true;
        }

        public long OnPlayerAppraise(GamePlayer player, InventoryItem item, bool silent)
        {
            if (item == null)
                return 0;

            int itemCount = Math.Max(1, item.Count);
            return item.Price * itemCount / 2;
        }

        public override void SaveIntoDatabase()
        {
            // do nothing !!!
        }

        public static void SpawnLotMarker(DBHouse house)
        {
            var obj = new GameLotMarker
            {
                Position = Position.Create(house.RegionID, house.X, house.Y, house.Z, (ushort)house.Heading),
                Name = "Lot Marker",
                Model = 1308,
                DatabaseItem = house
            };

            //No clue how we can check if a region
            //is in albion, midgard or hibernia instead
            //of checking the region id directly
            switch (obj.CurrentRegionID)
            {
                case 2:
                    obj.Model = 1308;
                    obj.Name = "Albion Lot";
                    break; //ALB
                case 102:
                    obj.Model = 1306;
                    obj.Name = "Midgard Lot";
                    break; //MID
                case 202:
                    obj.Model = 1307;
                    obj.Name = "Hibernia Lot";
                    break; //HIB
            }

            obj.AddToWorld();
        }
    }
}