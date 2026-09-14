using AmteScripts.Managers;
using AmteScripts.Utils;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Linq;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&rvr",
        ePrivLevel.Player,
        "Commands.GM.RvR.Description",
        "Commands.GM.RvR.Usage.Info",
        "Commands.GM.RvR.Usage.Open",
        "Commands.GM.RvR.Usage.Close",
        "Commands.GM.RvR.Usage.Unforce",
        "Commands.GM.RvR.Usage.Status",
        "Commands.GM.RvR.Usage.Refresh",
        "Commands.GM.RvR.Usage.Reset")]
    public class RvRCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length <= 1)
            {
                DisplayCmd(client);
                return;
            }

            string subcmd = args[1].ToLower();

            if (client.Account.PrivLevel == 1 && subcmd != "info")
            {
                DisplayCmd(client);
                return;
            }

            switch (args[1].ToLower())
            {
                case "open":
                    if (RvrManager.Instance.Open(true))
                        DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvROpened", string.Join("-", RvrManager.Instance.Regions.OrderBy(r => r))));
                    else
                        DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvRNotOpened"));
                    break;

                case "close":
                    DisplayMessage(client, RvrManager.Instance.Close() ? LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvRClosed") : LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvRNotClosed"));
                    break;

                case "unforce":
                    if (!RvrManager.Instance.IsOpen)
                    {
                        DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvRUnforceNotPossible"));
                        break;
                    }
                    RvrManager.Instance.Open(false);
                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvRWillCloseAutomatically"));
                    break;

                case "status":
                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvRStatus", RvrManager.Instance.IsOpen ? "open, les regions sont: " + string.Join("-", RvrManager.Instance.Regions) + "." : "close"));
                    break;

                case "refresh":
                    if (RvrManager.Instance.IsOpen || PvpManager.Instance.IsOpen)
                    {
                        DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvRMustBeClosed"));
                        break;
                    }
                    var rvr = string.Join(", ", RvrManager.Instance.InitMapsAndTerritories());
                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvRMapsUsed", rvr));
                    break;

                case "reset":
                    RvrManager.Instance.Close();
                    PvpManager.Instance.Open(string.Empty, false);

                    DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "RvRManager.RvRReset"));
                    break;

                case "info":
                    // This branch merges the functionality of the separate RvRInfo command.
                    client.Out.SendCustomTextWindow("RvR Info", RvrManager.Instance.GetStatistics(client.Player));
                    break;

                default:
                    DisplayCmd(client);
                    break;
            }
        }

        private void DisplayCmd(GameClient client)
        {
            if (client.Account.PrivLevel > 1)
            {
                // For GMs and admins, show the full list.
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Description"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Usage.Info"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Usage.Open"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Usage.Close"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Usage.Unforce"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Usage.Status"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Usage.Refresh"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Usage.Reset"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else
            {
                // Regular players only see the description and info usage.
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Description"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "Commands.GM.RvR.Usage.Info"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
    }
}