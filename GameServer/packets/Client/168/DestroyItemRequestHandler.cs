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
using AmteScripts.PvP.CTF;
using DOL.Database;
using DOL.GS.Scripts;
using DOL.Language;
using System;
using System.Collections;
using System.Numerics;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.DestroyItemRequest, "Handles destroy item requests from client", eClientStatus.PlayerInGame)]
    public class DestroyItemRequestHandler : IPacketHandler
    {
        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            packet.Skip(4);
            int slot = packet.ReadShort();
            InventoryItem item = client.Player.Inventory.GetItem((eInventorySlot)slot);
            if (item != null)
            {
                if (item.IsIndestructible)
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client, "DestroyItemRequestHandler.CantDestroyItem", item.GetName(0, false)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (item.Id_nb == "ARelic")
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client, "DestroyItemRequestHandler.CantDestroyRelic"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (item is FlagInventoryItem)
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client, "DestroyItemRequestHandler.CantDestroyPvPFlag"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (client.Player.Inventory.EquippedItems.Contains(item))
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client, "DestroyItemRequestHandler.CantDestroyEquippedItem"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                if (item.Id_nb.StartsWith("genistar_pet") || item.Id_nb.StartsWith("genistar_remains"))
                {
                    client.Player.TempProperties.setProperty("PacketDestroyGenistarSlot", (eInventorySlot)slot);

                    string prompt = LanguageMgr.GetTranslation(client, "DestroyItemRequestHandler.GenItemDestroyAttempt", item.Name);

                    client.Player.Out.SendCustomDialog(prompt, new CustomDialogResponse(DestroyGenistarPacketResponseCallback));
                    return;
                }

                if (client.Player.Inventory.RemoveItem(item))
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client, "DestroyItemRequestHandler.ItemDestroyed", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    InventoryLogging.LogInventoryAction(client.Player, "", "(destroy)", eInventoryActionType.Other, item, item.Count);
                }
            }
        }

        private void DestroyGenistarPacketResponseCallback(GamePlayer player, byte response)
        {
            eInventorySlot slot = player.TempProperties.getProperty<eInventorySlot>("PacketDestroyGenistarSlot", eInventorySlot.Invalid);
            player.TempProperties.removeProperty("PacketDestroyGenistarSlot");

            if (slot == eInventorySlot.Invalid) return;

            if (response != 0x01)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "DestroyItemRequestHandler.GenItemDestroyAbort"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                player.Out.SendInventorySlotsUpdate(null);
                return;
            }

            InventoryItem item = player.Inventory.GetItem(slot);
            if (item == null) return;

            if (player.Inventory.RemoveItem(item))
            {
                GenistarLifecycleManager.ExecuteTotalVaporization(player, item);

                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "DestroyItemRequestHandler.ItemDestroyed", item.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                InventoryLogging.LogInventoryAction(player, "", "(ui_vaporize)", eInventoryActionType.Other, item, item.Count);
            }
            else
            {
                player.Out.SendInventorySlotsUpdate(null);
            }
        }
    }
}
