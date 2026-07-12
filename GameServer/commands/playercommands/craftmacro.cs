/*
 * GONDWANA SERVER DAOC RP/PvP/GvG server, following Amtenael and Avalonia
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
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System.Collections.Generic;


namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&craftmacro",
        ePrivLevel.Player,
        "Commands.Players.Craftmacro.Description",
        "Commands.Players.Craftmacro.Usage.Set",
        "Commands.Players.Craftmacro.Usage.Clear",
        "Commands.Players.Craftmacro.Usage.Show",
        "Commands.Players.Craftmacro.Usage.Buy",
        "Commands.Players.Craftmacro.Usage.BuyNb",
        "Commands.Players.Craftmacro.Usage.BuyTo"
    )]
    public class CraftMacroCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public const string CraftQueueLength = "CraftQueueLength";
        public const string RecipeToCraft = "RecipeToCraft";

        public void OnCommand(GameClient client, string[] args)
        {
            if (IsSpammingCommand(client.Player, "craftmacro"))
                return;

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }

            GamePlayer player = client.Player;

            #region set

            if (args[1] == "set")
            {
                if (args.Length >= 3)
                {
                    int.TryParse(args[2], out int count);

                    if (count > 0)
                    {
                        if (count > Properties.MAX_CRAFT_QUEUE)
                        {
                            count = Properties.MAX_CRAFT_QUEUE;
                        }

                        client.Player.TempProperties.setProperty(CraftQueueLength, count);
                        DisplayMessage(client,
                            LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.Queue.Set",
                                count));
                    }
                }
                else
                {
                    DisplayMessage(client,
                        LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.Usage.Set"));
                }

                return;
            }

            #endregion

            #region clear

            if (args[1] == "clear")
            {

                client.Player.TempProperties.removeProperty(CraftQueueLength);
                client.Player.TempProperties.removeProperty(RecipeToCraft);

                DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.Queue.Cleared"));
            }

            #endregion

            #region show

            if (args[1] == "show")
            {
                int length = client.Player.TempProperties.getProperty<int>(CraftQueueLength);
                if (length != 0)
                {
                    DisplayMessage(client,
                        LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.Queue.Set",
                            length));
                }
                else
                {
                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.Queue.SetOne"));
                }

                return;
            }

            #endregion

            #region buy

            if (args[1] == "buy")
            {
                int amount = 1;
                if (args.Length >= 3)
                {
                    int.TryParse(args[2], out amount);
                    if (amount == 0)
                    {
                        amount = 1;
                    }
                }

                var recipe = client.Player.TempProperties.getProperty<Recipe>("RecipeToCraft");
                if (recipe != null)
                {
                    if (client.Player.TargetObject is GameMerchant merchant)
                    {
                        var catalog = merchant.Catalog.GetAllEntries();

                        IList<Ingredient> recipeIngredients;

                        lock (recipe)
                        {
                            recipeIngredients = recipe.Ingredients;
                        }

                        foreach (var ingredient in recipeIngredients)
                        {
                            foreach (var items in catalog)
                            {
                                if (ingredient.Material.Name != items.Item.Name) continue;
                                if (items.Item.Id_nb == "beetle_carapace") continue;
                                merchant.OnPlayerBuy(client.Player, items.SlotPosition, items.Page,
                                    ingredient.Count * amount);
                                break;
                            }
                        }

                        return;
                    }
                    /* This code is disabled as it seems to pertain to scripts that are not on Gondwana

                    else if (client.Player.TargetObject is GameGuardMerchant guardMerchant)
                    {
                        var merchantitems = DOLDB<DbMerchantItem>.SelectObjects(DB.Column("ItemListID")
                            .IsEqualTo(guardMerchant.TradeItems.ItemsListID));

                        IList<Ingredient> recipeIngredients;

                        lock (recipe)
                        {
                            recipeIngredients = recipe.Ingredients;
                        }

                        foreach (var ingredient in recipeIngredients)
                        {
                            foreach (var items in merchantitems)
                            {
                                var item =
                                    GameServer.Database.FindObjectByKey<DbItemTemplate>(items.ItemTemplateID);
                                if (item.Id_nb == "beetle_carapace") continue;
                                if (item != ingredient.Material) continue;
                                guardMerchant.OnPlayerBuy(client.Player, items.SlotPosition, items.PageNumber,
                                    ingredient.Count * amount);
                            }
                        }

                        return;
                    }
                    else if (client.Player.TargetObject is GuardCurrencyMerchant guardCurrencyMerchant)
                    {
                        var merchantitems = DOLDB<DbMerchantItem>.SelectObjects(DB.Column("ItemListID")
                            .IsEqualTo(guardCurrencyMerchant.TradeItems.ItemsListID));

                        IList<Ingredient> recipeIngredients;

                        lock (recipe)
                        {
                            recipeIngredients = recipe.Ingredients;
                        }

                        foreach (var ingredient in recipeIngredients)
                        {
                            foreach (var items in merchantitems)
                            {
                                var item =
                                    GameServer.Database.FindObjectByKey<DbItemTemplate>(items.ItemTemplateID);
                                if (item.Id_nb == "beetle_carapace") continue;
                                if (item != ingredient.Material) continue;
                                guardCurrencyMerchant.OnPlayerBuy(client.Player, items.SlotPosition, items.PageNumber,
                                    ingredient.Count * amount);
                            }
                        }

                        return;
                    } */
                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.MustTargetMerchant"));
                    return;
                }

                DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.SelectRecipe"));
                return;
            }

            #endregion

            #region buyto

            if (args[1] == "buyto")
            {
                if (args.Length < 3)
                {
                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.Usage.BuyTo"));
                    return;
                }

                if (int.TryParse(args[2], out int amount))
                {
                    if (amount == 0)
                    {
                        amount = 1;
                    }
                }
                else
                {
                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.Usage.BuyTo"));
                    return;
                }

                var recipe = client.Player.TempProperties.getProperty<Recipe>("RecipeToCraft");
                if (recipe != null)
                {
                    if (client.Player.TargetObject is GameMerchant merchant)
                    {
                        var catalog = merchant.Catalog.GetAllEntries();

                        IList<Ingredient> recipeIngredients;

                        lock (recipe)
                        {
                            recipeIngredients = recipe.Ingredients;
                        }

                        var playerItems = new List<InventoryItem>();
                        List<Scripts.StorageBagItem> bags = new List<Scripts.StorageBagItem>();

                        lock (client.Player.Inventory)
                        {
                            foreach (var pItem in client.Player.Inventory.AllItems)
                            {
                                if (pItem.SlotPosition >= (int)eInventorySlot.FirstBackpack &&
                                    pItem.SlotPosition <= (int)eInventorySlot.LastBackpack)
                                {
                                    playerItems.Add(pItem);

                                    // Make bag contents readable to the CraftMacro checker
                                    if (pItem is Scripts.StorageBagItem sbi)
                                        bags.Add(sbi);
                                }
                            }
                        }

                        foreach (var bag in bags)
                        {
                            Scripts.StorageBagVault vault = new Scripts.StorageBagVault(client.Player, bag);
                            foreach (var vItem in vault.DBItems(client.Player))
                            {
                                if (vItem != null) playerItems.Add(vItem);
                            }
                        }

                        foreach (var ingredient in recipeIngredients)
                        {
                            foreach (var item in catalog)
                            {
                                if (item.Item.Name != ingredient.Material.Name) continue;
                                if (item.Item.Id_nb == "beetle_carapace") continue;
                                int playerAmount = 0;

                                foreach (var pItem in playerItems)
                                {
                                    if (pItem.Template == ingredient.Material)
                                        playerAmount += pItem.Count;
                                }

                                merchant.OnPlayerBuy(client.Player, item.SlotPosition, item.Page,
                                    (ingredient.Count * amount) - playerAmount);
                            }
                        }

                        DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.BoughtItems", amount, recipe.Product.Name));
                        return;
                    }

                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.MustTargetMerchant"));
                    return;
                }

                DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Craftmacro.SelectRecipe"));
            }

            #endregion
        }
    }
}
