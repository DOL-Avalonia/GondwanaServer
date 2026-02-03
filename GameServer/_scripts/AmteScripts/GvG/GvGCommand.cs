using DOL.GS.Scripts;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [Cmd(
        "&gvg",
        ePrivLevel.GM,
        "Manage GvG status",
        "/gvg <on|off> - Forces GvG open or reverts to schedule")]
    public class GvGCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length < 2)
            {
                DisplaySyntax(client);
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.Status", GvGManager.ForceOpen), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            switch (args[1].ToLower())
            {
                case "on":
                    GvGManager.ForceOpen = true;
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.ForcedOpen"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    break;
                case "off":
                    GvGManager.ForceOpen = false;
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.TimeSchedule"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    break;
                default:
                    DisplaySyntax(client);
                    break;
            }
        }
    }
}