using System;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&setwho",
        ePrivLevel.Player,
        "Commands.Players.Setwho.Description",
        "Commands.Players.Setwho.Usage")]
    public class SetWhoCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (IsSpammingCommand(client.Player, "setwho"))
                return;

            if (args.Length < 2)
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Setwho.Help"));
                return;
            }

            if (args[1].ToLower() == "class")
                client.Player.ClassNameFlag = true;
            else if (args[1].ToLower() == "trade")
            {
                if (client.Player.CraftingPrimarySkill == eCraftingSkill.NoCrafting)
                {
                    DisplayMessage(
                        client,
                        LanguageMgr.GetTranslation(
                            client.Account.Language,
                            "Commands.Players.Setwho.Missing.Prof"));
                    return;
                }

                client.Player.ClassNameFlag = false;
            }
            else
            {
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Setwho.Help"));
                return;
            }

            if (client.Player.ClassNameFlag)
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Setwho.Craft.Off"));
            else
                DisplayMessage(
                    client,
                    LanguageMgr.GetTranslation(
                        client.Account.Language,
                        "Commands.Players.Setwho.Craft.On"));
        }
    }
}