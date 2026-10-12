using System;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.CommandHandler, "Handles the players commands", eClientStatus.PlayerInGame)]
    public class PlayerCommandHandler : PacketHandler
    {
        private const string LAST_USED_COMMAND_KEY = "LAST_USED_COMMAND_KEY";

        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            packet.Skip(1);
            if (client.Version < GameClient.eClientVersion.Version1127) packet.Skip(7);
            string cmdLine = packet.ReadString(255);
            client.Player?.TempProperties.setProperty(LAST_USED_COMMAND_KEY, cmdLine);

            if (!ScriptMgr.HandleCommand(client, cmdLine))
            {
                if (cmdLine.Length > 0 && cmdLine[0] == '&') cmdLine = "/" + cmdLine[1..];
                client.Out.SendMessage($"No such command ({cmdLine})", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        protected override string GetLogContext(GameClient client, GSPacketIn packet)
            => client.Player?.TempProperties.getProperty<string>(LAST_USED_COMMAND_KEY, null);
    }
}