using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;
using DOL.Territories;
using System.Linq;

namespace DOL.GS.Commands
{
    [Cmd(
        "&gvg",
        ePrivLevel.GM,
        "Manage GvG status",
        "/gvg <on|off> - Forces GvG open or reverts to schedule",
        "/gvg debug - Toggles a 5m ON / 2m OFF fast cycle",
        "/gvg resetrelics - Forces GvG to reset territory relics",
        "/gvg territoryreset <TerritoryID|all> - Forces a specific territory or all territories to become neutral")]
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
                    GvGManager.IsOpen = true;
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.ForcedOpen"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    break;
                case "off":
                    GvGManager.ForceOpen = false;
                    GvGManager.EvaluateSchedule();
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.TimeSchedule"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    break;
                case "debug":
                    GvGManager.ToggleDebugMode();
                    if (GvGManager.DebugMode)
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.DebugModeOn"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    else
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.DebugModeOff"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    break;
                case "resetrelics":
                    AmteScripts.Managers.TerritoryRelicManager.OnGvGOpened();
                    client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.ResetRelics"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    break;
                case "territoryreset":
                    if (args.Length < 3)
                    {
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.TerritoryResetSyntax"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    string target = args[2].ToLower();

                    if (target == "all")
                    {
                        int count = 0;
                        var territories = TerritoryManager.Instance.Territories.ToList();
                        foreach (var t in territories)
                        {
                            if (t.OwnerGuild != null)
                            {
                                t.OwnerGuild = null;
                                count++;
                            }
                        }
                        client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.ResetAllTerrSuccess", count), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        var t = TerritoryManager.GetTerritoryByID(args[2]);
                        if (t != null)
                        {
                            if (t.OwnerGuild != null)
                            {
                                t.OwnerGuild = null;
                                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.ResetOneTerrSuccess", t.Name, t.ID), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.ResetTerrNeutral", t.Name, t.ID), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                        }
                        else
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GvG.Command.ResetTerrNotFound", args[2]), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                    break;
                default:
                    DisplaySyntax(client);
                    break;
            }
        }
    }
}