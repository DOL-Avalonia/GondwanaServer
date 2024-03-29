using System;
using System.Collections.Generic;
using DOL.GS.Scripts;
using DOL.Language;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&amtenpc",
        ePrivLevel.GM,
        "Commands.GM.AmteNpc.Description",
        "Commands.GM.AmteNpc.Usage.Info",
        "Commands.GM.AmteNpc.Usage.Param")]
    public class AmteNpcCommand : AbstractCommandHandler, ICommandHandler
    {
        public AmteCustomParam GetCPFromName(IAmteNPC npc, string name)
        {
            for (var cp = npc.GetCustomParam(); cp != null; cp = cp.next)
                if (cp.name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                    return cp;
            return null;
        }

        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null || !(client.Player.TargetObject is IAmteNPC))
            {
                DisplaySyntax(client);
                return;
            }

            var npc = (GameNPC)client.Player.TargetObject;
            var amteNpc = (IAmteNPC)npc;
            switch (args[1])
            {
                case "info":
                    var infos = new List<string>
                    {
                        "Nom: " + npc.Name,
                        "Niveau: " + npc.Level,
                        "Paramètres du pnj:"
                    };
                    Util.AddRange(infos, amteNpc.DelveInfo());
                    client.Out.SendCustomTextWindow("Informations de " + npc.Name, infos);
                    break;

                case "param":
                    try
                    {
                        var cp = GetCPFromName(amteNpc, args[2]);
                        if (cp == null)
                        {
                            ChatUtil.SendSystemMessage(client, "Erreur: le paramètre \"" + args[2] + "\" n'existe pas.");
                            break;
                        }

                        cp.Value = args[3];
                        npc.SaveIntoDatabase();
                        ChatUtil.SendSystemMessage(client, "Le paramètre \"" + cp.name + "\" vaut maintenant \"" + cp.Value + "\".");
                    }
                    catch (Exception e)
                    {
                        ChatUtil.SendSystemMessage(client, "Erreur: " + e.Message);
                    }

                    break;

                default:
                    DisplaySyntax(client);
                    break;
            }
        }
    }
}
