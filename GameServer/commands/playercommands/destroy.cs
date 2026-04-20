using DOL.GS.PacketHandler;
using DOL.GS.Commands;
using DOL.GS;
using DOL.Database;
using AmteScripts.PvP.CTF;
using DOL.Language;

namespace DOL.GS.Commands
{
    [Cmd(
        "&destroy",
        ePrivLevel.Player,
        "Commands.Players.Destroy.Description",
        "Commands.Players.Destroy.Syntax")]
    public class DestroyCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            if (player == null)
                return;

            InventoryItem item = null;
            eInventorySlot slot = eInventorySlot.Invalid;

            if (args.Length > 1)
            {
                if (int.TryParse(args[1], out int slotNumber))
                {
                    if (slotNumber < 1 || slotNumber > 40)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.SlotNumberRange"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    slot = (eInventorySlot)(slotNumber + (int)eInventorySlot.FirstBackpack - 1);
                    item = player.Inventory.GetItem(slot);

                    if (item == null)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.NoItemInSlot"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }
                }
                else
                {
                    string itemName = string.Join(" ", args, 1, args.Length - 1);
                    item = player.Inventory.GetFirstItemByName(itemName, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);

                    if (item == null)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.NoItemByName"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    slot = (eInventorySlot)item.SlotPosition;
                }
            }
            else
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.UsageMessage"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // Security Check: Ensure the item is in the player's backpack
            if (slot < eInventorySlot.FirstBackpack || slot > eInventorySlot.LastBackpack)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.OnlyBackpackItems"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (item.IsIndestructible)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.CannotDestroyItem"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (item.Id_nb == "ARelic")
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "DestroyItemRequestHandler.CantDestroyRelic"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            player.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.ConfirmDestroy", item.Name), new CustomDialogResponse(DestroyItemResponse));
            player.TempProperties.setProperty("DestroyItemSlot", slot);
        }

        protected void DestroyItemResponse(GamePlayer player, byte response)
        {
            if (response != 0x01) // If the player did not click "OK"
                return;


            eInventorySlot slot = player.TempProperties.getProperty<eInventorySlot>("DestroyItemSlot", eInventorySlot.Invalid);
            player.TempProperties.removeProperty("DestroyItemSlot");

            if (slot == eInventorySlot.Invalid)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.NoItemSelected"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            InventoryItem item = player.Inventory.GetItem(slot);

            if (item == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.ItemNotFound"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // Security Check: Ensure the item is in the player's backpack
            if (slot < eInventorySlot.FirstBackpack || slot > eInventorySlot.LastBackpack)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.OnlyBackpackItems"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (item is AmteScripts.Managers.TerritoryRelicInventoryItem)
            {
                player.DropTerritoryRelicsOnDeath(player);
            }

            if (item is FlagInventoryItem)
            {
                player.DropFlagsOnDeath(player);
            }

            if (player.Inventory.RemoveItem(item))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.ItemDestroyed", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                InventoryLogging.LogInventoryAction(player, "", "(destroy)", eInventoryActionType.Other, item, item.Count);
            }
            else
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Destroy.FailedToDestroy"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
    }
}