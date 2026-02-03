using System;
using DOL.Database;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Scripts
{
    public class Copyist : AmteMob
    {
        private const int COPY_COST_COPPER = 20000;

        private static string FormatCost(GameClient client, int copper)
        {
            int gold = copper / 10000;
            int rem = copper % 10000;
            int silver = rem / 100;
            int cop = rem % 100;

            string goldName = LanguageMgr.GetTranslation(client, "Money.GetString.Text4");
            string silverName = LanguageMgr.GetTranslation(client, "Money.GetString.Text5");
            string copperName = LanguageMgr.GetTranslation(client, "Money.GetString.Text6");

            var parts = new System.Collections.Generic.List<string>(3);
            if (gold > 0) parts.Add($"{gold} {goldName}");
            if (silver > 0) parts.Add($"{silver} {silverName}");
            if (cop > 0 || parts.Count == 0) parts.Add($"{cop} {copperName}");

            return string.Join(" ", parts);
        }

        private string GetCostText(GamePlayer p)
        {
            return FormatCost(p.Client, COPY_COST_COPPER);
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            string costText = GetCostText(player);

            player.Client.Out.SendMessage(
                LanguageMgr.GetTranslation(player.Client, "Copyist.InteractText01") + "\n" +
                LanguageMgr.GetTranslation(player.Client, "Copyist.InteractText02", costText),
                eChatType.CT_Say, eChatLoc.CL_PopupWindow);

            return true;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            GamePlayer p = source as GamePlayer;
            if (p == null || item == null)
                return false;

            if (!item.Id_nb.StartsWith("scroll"))
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Copyist.ResponseText05"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            var book = GameServer.Database.FindObjectByKey<DBBook>(item.MaxCondition);
            if (book == null)
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Copyist.ResponseText04"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (book.PlayerID != p.InternalID)
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Copyist.ResponseText01"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            string costText = GetCostText(p);

            if (!p.RemoveMoney(Currency.Copper.Mint(COPY_COST_COPPER)))
            {
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Copyist.ResponseText02", costText),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            // Create a new unique scroll that points to the same DBBook ID
            var iu = new ItemUnique(item.Template)
            {
                Id_nb = "scroll" + Guid.NewGuid(),
                Name = item.Name,
                Model = item.Model,
                MaxCondition = (int)book.ID
            };
            GameServer.Database.AddObject(iu);

            var invItem = GameInventoryItem.Create(iu);
            if (!p.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, invItem))
            {
                // refund if no space
                p.AddMoney(Currency.Copper.Mint(COPY_COST_COPPER));
                p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Copyist.NoInventorySpace"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            InventoryLogging.LogInventoryAction(this, p, eInventoryActionType.Merchant, invItem, invItem.Count);
            p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client, "Copyist.ResponseText03") + " " + p.Name + ".",
                eChatType.CT_System, eChatLoc.CL_PopupWindow);

            return true;
        }
    }
}
