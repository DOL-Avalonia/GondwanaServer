using DOL.GS;
using DOL.GS.Scripts;
using AmteScripts.Managers;
using DOL.Language;
using DOL.GS.PacketHandler;
using DOL.Database;

namespace DOL.GS.Scripts
{
    public class CoreGeneratorNPC : TextNPC
    {
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            if (PvpManager.Instance.CurrentSessionType != PvpManager.eSessionTypes.CoreRun)
                return true;

            bool hasCore = false;

            var backpack = player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
            foreach (var item in backpack)
            {
                if (item is PvPTreasure)
                {
                    hasCore = true;
                    break;
                }
            }

            if (hasCore)
            {
                player.Out.SendMessage("You already hold a Core! Run to the edge!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            return true;
        }
    }

    public class CoreCollectorNPC : TextNPC
    {
        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            GamePlayer player = source as GamePlayer;
            if (player == null) return false;

            if (PvpManager.Instance.CurrentSessionType != PvpManager.eSessionTypes.CoreRun)
            {
                player.Out.SendMessage("The Core Run is not active.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false; // Reject item
            }

            if (item is PvPTreasure treasure)
            {
                int points = (int)(treasure.Condition / 4.0);
                if (points < 1) points = 1;

                player.Inventory.RemoveItem(item);
                player.Out.SendMessage($"Core accepted! Condition: {treasure.Condition}%. You score {points} points!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);

                PvpManager.Instance.HandleCoreDelivery(player, points);
                player.Out.SendSpellEffectAnimation(this, player, 106, 0, false, 1);

                return true;
            }

            player.Out.SendMessage("I only accept Power Cores (PvPTreasure).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return false;
        }
    }
}