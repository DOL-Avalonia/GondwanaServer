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
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&house",
        ePrivLevel.Player,
        "Commands.Players.House.Description",
        "Commands.Players.House.InfoLot")]
    public class HouseCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public void OnCommand(GameClient client, string[] args)
        {
            try
            {
                if (args.Length > 1 && args[1].ToLower() == "info")
                {
                    if (args.Length > 2 && int.TryParse(args[2], out int houseNum))
                    {
                        House specificHouse = HouseMgr.GetHouse(houseNum);
                        if (specificHouse != null && (specificHouse.OwnerID == client.Player.ObjectId || (client.Player.Guild != null && specificHouse.OwnerID == client.Player.Guild.GuildID)))
                        {
                            specificHouse.SendHouseInfo(client.Player);
                        }
                        else
                        {
                            DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.House.NotYourHouse"));
                        }
                        return;
                    }
                }

                if (client.Account.PrivLevel > (int)ePrivLevel.Player)
                {
                    if (args.Length > 1)
                    {
                        HouseAdmin(client.Player, args);
                        return;
                    }

                    if (client.Account.PrivLevel >= (int)ePrivLevel.GM)
                    {
                        DisplayMessage(client, LanguageMgr.GetTranslation(client, "Commands.Players.House.GMInfo"));
                    }

                    if (client.Account.PrivLevel == (int)ePrivLevel.Admin)
                    {
                        DisplayMessage(client, LanguageMgr.GetTranslation(client, "Commands.Players.House.AdminInfo1"));
                        DisplayMessage(client, LanguageMgr.GetTranslation(client, "Commands.Players.House.AdminInfo2"));
                        DisplayMessage(client, LanguageMgr.GetTranslation(client, "Commands.Players.House.AdminInfo3"));
                        DisplayMessage(client, LanguageMgr.GetTranslation(client, "Commands.Players.House.AdminInfo4"));
                    }
                }

                List<House> ownedHouses = new List<House>();
                var dbHouses = GameServer.Database.SelectObjects<DBHouse>(DB.Column("OwnerID").IsEqualTo(client.Player.ObjectId));

                foreach (var dbh in dbHouses)
                {
                    House h = HouseMgr.GetHouse(dbh.HouseNumber);
                    if (h != null) ownedHouses.Add(h);
                }

                // Include the Guild House if players have one
                if (client.Player.Guild != null && client.Player.Guild.GuildOwnsHouse && client.Player.Guild.GuildHouseNumber > 0)
                {
                    House guildHouse = HouseMgr.GetHouse(client.Player.Guild.GuildHouseNumber);
                    if (guildHouse != null && !ownedHouses.Contains(guildHouse))
                    {
                        ownedHouses.Add(guildHouse);
                    }
                }

                if (ownedHouses.Count == 1)
                {
                    // If player owns exactly one house, show the standard House Info window directly
                    House singleHouse = ownedHouses[0];
                    UpdateHouseEmblemAndState(client, singleHouse);
                    singleHouse.SendHouseInfo(client.Player);
                }
                else if (ownedHouses.Count > 1)
                {
                    // If player owns multiple houses, generate the custom list window
                    List<string> textList = new List<string>();
                    int count = 1;

                    foreach (House h in ownedHouses)
                    {
                        UpdateHouseEmblemAndState(client, h);

                        textList.Add($"\u2022 " + LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.House.ListHouse", count.ToString("D2")));
                        textList.Add($"  - " + LanguageMgr.GetTranslation(client.Account.Language, "House.SendHouseInfo.Owner", h.Name));
                        textList.Add($"  - " + LanguageMgr.GetTranslation(client.Account.Language, "House.SendHouseInfo.Lotnum", h.HouseNumber));
                        textList.Add($"  - " + LanguageMgr.GetTranslation(client.Account.Language, "House.SendHouseInfo.Lockbox", Money.GetString(h.KeptMoney)));
                        textList.Add($"  - " + LanguageMgr.GetTranslation(client.Account.Language, "House.SendHouseInfo.RentalPrice", Money.GetString(HouseMgr.GetRentByModel(h.Model))));

                        // Calculate rent due time
                        TimeSpan due;
                        if (Properties.RENT_DUE_DAYS > 0)
                            due = (h.LastPaid.AddDays(Properties.RENT_DUE_DAYS).AddHours(1) - DateTime.Now);
                        else if (Properties.RENT_DUE_DAYS < 0)
                            due = (h.LastPaid.AddMinutes(1) - DateTime.Now);
                        else
                            due = TimeSpan.Zero;

                        string rentStr = LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.House.ListNoRent");
                        if (Properties.RENT_DUE_DAYS > 0)
                            rentStr = LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.House.ListRentDaysHours", due.Days, due.Hours);
                        else if (Properties.RENT_DUE_DAYS < 0)
                            rentStr = LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.House.ListRentSeconds", Math.Max(0, (int)due.TotalSeconds));

                        textList.Add($"  - " + LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.House.ListRentDue", rentStr));
                        textList.Add(" ");
                        textList.Add(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.House.ListMoreInfoInstruction", h.HouseNumber));
                        textList.Add(" ");

                        count++;
                    }

                    client.Out.SendCustomTextWindow(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.House.YourProperties"), textList);
                }
                else
                {
                    DisplayMessage(client, LanguageMgr.GetTranslation(client, "Commands.Players.House.NoHouse"));
                }
            }
            catch
            {
                DisplaySyntax(client);
            }
        }

        private void UpdateHouseEmblemAndState(GameClient client, House house)
        {
            if (client.Player.Guild != null && house.Emblem != client.Player.Guild.Emblem)
            {
                house.Emblem = client.Player.Guild.Emblem;
                house.SaveIntoDatabase();
                house.SendUpdate();
            }
            else if (house.RegionID == client.Player.CurrentRegionID)
            {
                if (client.Player.InHouse == false)
                {
                    client.Out.SendHouse(house);
                    client.Out.SendGarden(house);
                }

                if (house.IsOccupied)
                {
                    client.Out.SendHouseOccupied(house, true);
                }
                house.SendUpdate();
            }
        }

        public void HouseAdmin(GamePlayer player, string[] args)
        {
            if (player.Client.Account.PrivLevel == (int)ePrivLevel.Admin)
            {
                if (args[1].ToLower() == "restart")
                {
                    HouseMgr.Start(player.Client);
                    return;
                }

                if (args[1].ToLower() == "addhookpoints")
                {
                    if (player.TempProperties.getProperty<bool>(HousingConstants.AllowAddHouseHookpoint, false))
                    {
                        player.TempProperties.removeProperty(HousingConstants.AllowAddHouseHookpoint);
                        DisplayMessage(player.Client, LanguageMgr.GetTranslation(player.Client, "Commands.Players.House.HookPointOff"));
                    }
                    else
                    {
                        player.TempProperties.setProperty(HousingConstants.AllowAddHouseHookpoint, true);
                        DisplayMessage(player.Client, LanguageMgr.GetTranslation(player.Client, "Commands.Players.House.HookPointOn"));
                    }

                    return;
                }
            }

            ArrayList houses = (ArrayList)HouseMgr.GetHousesCloseToSpot(player.Position, 700);
            if (houses.Count != 1)
            {
                DisplayMessage(player.Client, LanguageMgr.GetTranslation(player.Client, "Commands.Players.House.FarAway"));
                return;
            }

            if (args[1].ToLower() == "info")
            {
                (houses[0] as House)!.SendHouseInfo(player);
                return;
            }

            // The following commands are for Admins only

            if (player.Client.Account.PrivLevel != (int)ePrivLevel.Admin)
                return;

            if (args[1].ToLower() == "model")
            {
                int newModel = Convert.ToInt32(args[2]);

                if (newModel < 1 || newModel > 12)
                {
                    DisplayMessage(player.Client, LanguageMgr.GetTranslation(player.Client, "Commands.Players.House.ModelInvalid"));
                    return;
                }

                if (houses.Count == 1 && newModel != (houses[0] as House)!.Model)
                {
                    HouseMgr.RemoveHouseItems(houses[0] as House);
                    (houses[0] as House)!.Model = newModel;
                    (houses[0] as House)!.SaveIntoDatabase();
                    (houses[0] as House)!.SendUpdate();

                    DisplayMessage(player.Client, LanguageMgr.GetTranslation(player.Client, "Commands.Players.House.ModelChanged", newModel));
                    GameServer.Instance.LogGMAction(player.Name + " changed house #" + (houses[0] as House)!.HouseNumber + " model to " + newModel);
                }

                return;
            }

            if (args[1].ToLower() == "remove")
            {
                string confirm = "";

                if (args.Length > 2)
                    confirm = args[2];

                if (confirm != "YES")
                {
                    DisplayMessage(player.Client, LanguageMgr.GetTranslation(player.Client, "Commands.Players.House.ConfirmYES"));
                    return;
                }

                if (houses.Count == 1)
                {
                    HouseMgr.RemoveHouse(houses[0] as House);
                    DisplayMessage(player.Client, LanguageMgr.GetTranslation(player.Client, "Commands.Players.House.Removed"));
                    GameServer.Instance.LogGMAction(player.Name + " removed house #" + (houses[0] as House)!.HouseNumber);
                }

                return;
            }
        }

    }
}