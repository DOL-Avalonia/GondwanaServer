using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;


namespace DOL.GS.Scripts
{
    [NPCGuildScript("Trash Bin")]
    public class TrashBinNPC : GameNPC
    {
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "TrashBinNPC.Interact"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);

            return true;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            GamePlayer player = source as GamePlayer;
            if (player == null || item == null)
                return false;

            if (item is StorageBagItem)
            {
                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "TrashBinNPC.CannotDestroyObject"));
                return false;
            }

            player.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client, "TrashBinNPC.ConfirmDestroy", item.Name), new CustomDialogResponse(DestroyItemResponse));
            player.TempProperties.setProperty("TrashBinItem", item);

            return true;
        } 

        private void DestroyItemResponse(GamePlayer player, byte response)
        {
            InventoryItem item = player.TempProperties.getProperty<InventoryItem>("TrashBinItem", null);

            player.TempProperties.removeProperty("TrashBinItem");

            if (response != 0x01) // Player did not confirm
            {
                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "TrashBinNPC.WillNotDestroy"));
                return;
            }

            if (item == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "TrashBinNPC.ItemNotFound"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (item is AmteScripts.Managers.TerritoryRelicInventoryItem)
            {
                player.DropTerritoryRelicsOnDeath(player);
            }

            if (player.Inventory.RemoveItem(item))
            {
                _ = SayTo(player, LanguageMgr.GetTranslation(player.Client, "TrashBinNPC.ItemDestroyed", item.Name));
                InventoryLogging.LogInventoryAction(player, "", "(TrashBinNPC destroy)", eInventoryActionType.Other, item, item.Count);
            }
            else
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "TrashBinNPC.UnableToDestroy"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
    }
}