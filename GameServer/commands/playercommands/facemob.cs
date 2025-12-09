using System;
using System.Linq;
using DOL.Database;
using DOL.GS;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&facemob",
        ePrivLevel.Player,
        "Commands.Players.Facemob.Description",
        "Commands.Players.Facemob.Usage")]
    public class FaceMobCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client?.Player == null)
                return;

            if (IsSpammingCommand(client.Player, "facemob"))
                return;

            if (args.Length < 2)
            {
                client.Out.SendMessage(
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Facemob.Error.Name"
                    ),
                    eChatType.CT_System,
                    eChatLoc.CL_SystemWindow
                );
                return;
            }

            var player = client.Player;
            string requiredTemplateId = Properties.FACEMOB_REQUIRED_ITEMTEMPLATE;

            if (!string.IsNullOrWhiteSpace(requiredTemplateId))
            {
                int count = player.Inventory?.CountItemTemplate(requiredTemplateId, eInventorySlot.MinEquipable, eInventorySlot.LastBagHorse) ?? 0;

                if (count <= 0)
                {
                    string itemName = requiredTemplateId;
                    ItemTemplate template = GameServer.Database.FindObjectByKey<ItemTemplate>(requiredTemplateId);

                    if (template != null && !string.IsNullOrEmpty(template.Name))
                        itemName = template.Name;

                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.Players.Facemob.Error.MissingItem", itemName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }

            string mobName = string.Join(" ", args, 1, args.Length - 1);
            var currentRegion = player.CurrentRegion;

            if (currentRegion == null)
                return;

            GameNPC nearestMob = null;
            int nearestDistance = int.MaxValue;

            // Search all realms for NPCs with this name, then filter by region
            foreach (var realm in Constants.ALL_REALMS)
            {
                GameObject[] objects = WorldMgr.GetObjectsByName(mobName, realm, typeof(GameNPC), ignoreCase: true);

                if (objects == null || objects.Length == 0)
                    continue;

                foreach (GameNPC npc in objects.OfType<GameNPC>())
                {
                    if (npc == null || npc.CurrentRegion != currentRegion)
                        continue;

                    if (!npc.IsAlive)
                        continue;

                    int distance = (int)player.GetDistanceTo(npc);

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestMob = npc;
                    }
                }
            }

            if (nearestMob == null)
            {
                client.Out.SendMessage(
                    string.Format(
                        LanguageMgr.GetTranslation(
                            client.Account.Language,
                            "Commands.Players.Facemob.Error.NotFound"
                        ),
                        mobName
                    ),
                    eChatType.CT_System,
                    eChatLoc.CL_SystemWindow
                );
                return;
            }

            if (nearestMob != null)
            {
                player.TurnTo(nearestMob.Position.Coordinate);
            }
            client.Out.SendPlayerJump(true);

            client.Out.SendMessage(
                string.Format(
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Facemob.Facing"
                    ),
                    nearestMob!.Name,
                    nearestDistance
                ),
                eChatType.CT_System,
                eChatLoc.CL_SystemWindow
            );
        }
    }
}
