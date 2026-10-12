using System;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&serverproperties",
        ePrivLevel.Admin,
        "AdminCommands.ServerProperties.Description",
        "AdminCommands.ServerProperties.Usage")]
    public class ServerPropertiesCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (GameServer.Instance.Configuration.DBType == Database.Connection.ConnectionType.DATABASE_XML)
            {
                DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "AdminCommands.ServerProperties.DataBaseXML"));
                return;
            }
            bool wasLogMgrEnabled = Properties.LOS_MGR_ENABLE;
            Properties.Refresh();
            if (Properties.LOS_MGR_ENABLE)
                DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "AdminCommands.ServerProperties.LosCheckMgrChanged"));
            DisplayMessage(client, LanguageMgr.GetTranslation(client.Account.Language, "AdminCommands.ServerProperties.PropertiesRefreshed"));
        }
    }
}