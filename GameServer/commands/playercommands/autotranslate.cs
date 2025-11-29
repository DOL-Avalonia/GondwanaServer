using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&autotranslate",
        new string[] { "&at" },
        ePrivLevel.Player,
        "Commands.Players.Autotranslate.Description",
        "Commands.Players.Autotranslate.Usage")]
    public class AutoTranslateCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (IsSpammingCommand(client.Player, "autotranslate"))
                return;

            var player = client.Player;
            if (player == null)
                return;

            // /autotranslate on | off  OR just /autotranslate
            if (args.Length >= 2)
            {
                switch (args[1].ToLower())
                {
                    case "on":
                    case "1":
                    case "true":
                        player.AutoTranslateEnabled = true;
                        player.Out.SendMessage(
                            LanguageMgr.GetTranslation(client, "Commands.Players.Autotranslate.Enabled"),
                            eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;

                    case "off":
                    case "0":
                    case "false":
                        player.AutoTranslateEnabled = false;
                        player.Out.SendMessage(
                            LanguageMgr.GetTranslation(client, "Commands.Players.Autotranslate.Disabled"),
                            eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                }
            }

            player.AutoTranslateEnabled = !player.AutoTranslateEnabled;

            player.Out.SendMessage(
                LanguageMgr.GetTranslation(
                    client,
                    player.AutoTranslateEnabled
                        ? "Commands.Players.Autotranslate.NowOn"
                        : "Commands.Players.Autotranslate.NowOff"),
                eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}