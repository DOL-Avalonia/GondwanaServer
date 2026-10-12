using DOL.GS.PacketHandler;
using DOL.Language;
using System.Linq;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&group",
        new string[] { "&g" },
        ePrivLevel.Player,
        "Commands.Players.Group.Description",
        "Commands.Players.Group.Usage")]
    public class GCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.Group == null)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Group.NotInGroup"
                    )
                );
                return;
            }

            if (IsSpammingCommand(client.Player, "group", 500))
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Group.SlowDown"
                    )
                );
                return;
            }

            if (args.Length >= 2)
            {
                client.Player.Group.SendMessageToGroupMembers(client.Player, string.Join(' ', args.Skip(1)), eChatType.CT_Group, eChatLoc.CL_ChatWindow);
            }
            else
            {
                DisplaySyntax(client);
            }
        }
    }
}