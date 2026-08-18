using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&epicmount",
        ePrivLevel.Player,
        "EpicMount.Cmd.Description",
        "EpicMount.Cmd.Usage.Cruise",
        "EpicMount.Cmd.Usage.Move",
        "EpicMount.Cmd.Usage.Target",
        "EpicMount.Cmd.Usage.Left",
        "EpicMount.Cmd.Usage.Right",
        "EpicMount.Cmd.Usage.Stop",
        "EpicMount.Cmd.Usage.Rise",
        "EpicMount.Cmd.Usage.Down",
        "EpicMount.Cmd.Usage.Land")]
    public class EpicMountCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            string lang = client.Account.Language;

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }

            GamePlayer player = client.Player;
            if (player == null) return;

            string action = args[1].ToLower();

            if (action == "left" || action == "right" || action == "gauche" || action == "droite")
            {
                if (IsSpammingCommand(player, "epicmount_turn", 300))
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Cmd.Wait"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }
            else if (action != "target" && action != "cible")
            {
                if (IsSpammingCommand(player, "epicmount", 1500))
                {
                    client.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Cmd.Wait"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }

            if (!player.IsRiding || player.Steed == null || !(player.Steed is GameEpicFlyingMount))
            {
                client.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Cmd.NotRiding"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            GameEpicFlyingMount mount = (GameEpicFlyingMount)player.Steed;
            int amount = 180;

            switch (action)
            {
                case "cruise":
                case "forward":
                case "voler":
                    mount.ToggleCruise();
                    break;
                case "move":
                case "go":
                case "aller":
                    mount.MoveToGroundTarget();
                    break;
                case "target":
                case "cible":
                    mount.MoveToSelectedTarget();
                    break;
                case "left":
                case "gauche":
                    mount.Rotate(-15);
                    break;
                case "right":
                case "droite":
                    mount.Rotate(15);
                    break;
                case "stop":
                case "halt":
                case "arret":
                    mount.IsCruising = false;
                    mount.IsMovingToTarget = false;
                    mount.StopMoving();
                    client.Out.SendMessage(LanguageMgr.GetTranslation(lang, "EpicMount.Cmd.Stopped"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;
                case "rise":
                case "up":
                case "monter":
                    mount.ChangeAltitude(amount);
                    break;
                case "down":
                case "descendre":
                    mount.ChangeAltitude(-amount);
                    break;
                case "land":
                case "atterrir":
                    mount.ChangeAltitude(-20000);
                    break;
                default:
                    DisplaySyntax(client);
                    break;
            }
        }

        public void DisplaySyntax(GameClient client, string[] args = null)
        {
            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "EpicMount.Cmd.Usage.Full"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}