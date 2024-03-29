using System;
using DOL.GS.PacketHandler;
using System.Collections.Generic;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&amteboat",
        ePrivLevel.GM,
        "Commands.GM.Boat.Description",
        "Commands.GM.Boat.Usage.Info",
        "Commands.GM.Boat.Usage.Create",
        "Commands.GM.Boat.Usage.Speed",
        "Commands.GM.Boat.Usage.Path")]
    public class AmteBoatCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }
            GameBoatAmte mob = client.Player.TargetObject as GameBoatAmte;
            switch (args[1])
            {
                case "create":
                    mob = new GameBoatAmte();
                    try
                    {
                        mob.Model = ushort.Parse(args[2]);
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        return;
                    }

                    mob.Position = client.Player.Position;
                    mob.Heading = client.Player.Heading;
                    mob.CurrentRegion = client.Player.CurrentRegion;
                    mob.Name = "New Boat";
                    mob.LoadedFromScript = false;
                    mob.AddToWorld();
                    mob.SaveIntoDatabase();
                    client.Out.SendMessage("Boat created: OID=" + mob.ObjectID, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;

                case "info":
                    if (mob == null)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    var txt = new List<string>
                        {
                            "Nom du path: '" + mob.PathName + "'",
                            "Vitesse sur le path: " + mob.MaxSpeedBase + "'"
                        };
                    client.Out.SendCustomTextWindow(mob.Name + " Info", txt);
                    break;

                case "path":
                    if (mob == null)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    mob.PathName = args[2];
                    mob.SaveIntoDatabase();
                    client.Out.SendMessage("Le trajet est maintenant '" + mob.PathName + "'.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;

                case "speed":
                    if (mob == null)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    try
                    {
                        mob.MaxSpeedBase = short.Parse(args[2]);
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    mob.SaveIntoDatabase();
                    client.Out.SendMessage("La vitesse de trajet est maintenant: " + mob.MaxSpeedBase, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;

                default: DisplaySyntax(client); break;
            }
        }
    }
}