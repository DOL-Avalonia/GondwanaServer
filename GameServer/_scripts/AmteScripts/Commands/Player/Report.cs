using DOL.GS.PacketHandler;
using DOL.Language;


namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&report",
        ePrivLevel.Player,
        "Commands.Players.Report.Description",
        "Commands.Players.Report.Usage")]
    public class AmteReportCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length == 1)
            {
                DisplaySyntax(client);
                return;
            }

            string message = string.Join(" ", args, 1, args.Length - 1);
            message = "[Report] " + client.Player.Name + ": \"" + message + "\".";
            client.Out.SendMessage(message, eChatType.CT_Staff, eChatLoc.CL_ChatWindow);
            foreach (var cl in WorldMgr.GetAllPlayingClients())
                if (cl.Account.PrivLevel >= 2 && cl != client)
                    cl.Out.SendMessage(message, eChatType.CT_Staff, eChatLoc.CL_ChatWindow);
        }
    }
}