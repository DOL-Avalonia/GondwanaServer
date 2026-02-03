using System;
using DOL.Database;
using DOL.GameEvents;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using DOL.Language;
using System.Linq;

namespace DOL.GS.Scripts
{
    [CmdAttribute(
         "&teleportnpc",
         ePrivLevel.GM,
         "Commands.GM.TeleportNPC.Description",
         "Commands.GM.TeleportNPC.Usage.Create",
         "Commands.GM.TeleportNPC.Usage.Create.Douanier",
         "Commands.GM.TeleportNPC.Usage.Text",
         "Commands.GM.TeleportNPC.Usage.Refuse",
         "Commands.GM.TeleportNPC.Usage.Radius",
         "Commands.GM.TeleportNPC.Usage.Level",
         "Commands.GM.TeleportNPC.Usage.AddJump",
         "Commands.GM.TeleportNPC.Usage.Jump",
         "Commands.GM.TeleportNPC.Usage.RemoveJump",
         "Commands.GM.TeleportNPC.Usage.Password",
         "Commands.GM.TeleportNPC.Usage.Conditions.Visible",
         "Commands.GM.TeleportNPC.Usage.Conditions.Item",
         "Commands.GM.TeleportNPC.Usage.Conditions.Slot",
         "Commands.GM.TeleportNPC.Usage.Conditions.Condition",
         "Commands.GM.TeleportNPC.Usage.Conditions.Niveaux",
         "Commands.GM.TeleportNPC.Usage.Conditions.Bind",
         "Commands.GM.TeleportNPC.Usage.AdditionalDescription",
         "Commands.GM.TeleportNPC.Usage.Conditions.Hours",
         "Commands.GM.TeleportNPC.Usage.Conditions.Event",
         "Commands.GM.TeleportNPC.Usage.Conditions.CompletedQuest",
         "Commands.GM.TeleportNPC.Usage.Conditions.QuestStep",
         "Commands.GM.TeleportNPC.Usage.TerritoryLinked",
         "Commands.GM.TeleportNPC.Usage.ShowTeleporterIndicator",
         "Commands.GM.TeleportNPC.Usage.ShowBoundary",
         "Commands.GM.TeleportNPC.Usage.BoundaryModel",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.Use",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.Seconds",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.CastTicks",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.ClientEffect",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.CastEffect",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.PlayerEffect")]
    public class TeleportNPCCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null) return;
            GamePlayer player = client.Player;

            if (args.Length < 2)
            {
                DisplaySyntax(client);
                return;
            }

            TeleportNPC npc = player.TargetObject as TeleportNPC;
            string text = "";
            switch (args[1].ToLower())
            {
                #region create - text - refuse
                case "create":
                    bool isRenaissance = false;
                    if (args.Length > 2 && args[2] == "douanier")
                    {
                        if (args.Length < 4)
                        {
                            DisplaySyntax(client);
                            break;
                        }

                        int price = 0;

                        if (!int.TryParse(args[3], out price))
                        {
                            DisplaySyntax(client);
                            break;
                        }

                        if (args.Length == 5)
                        {
                            if (!bool.TryParse(args[4], out isRenaissance))
                            {
                                DisplaySyntax(client);
                                player.Out.SendMessage("Le parametre IsRenaissance(true ou false) n'est pas correct" + text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                                return;
                            }
                        }

                        npc = new Douanier()
                        {
                            Position = player.Position,
                            Heading = player.Heading,
                            CurrentRegion = player.CurrentRegion,
                            Name = "Maitre Douanier",
                            GuildName = "Douanier",
                            Realm = 0,
                            Model = 40,
                            Price = Money.GetMoney(0, 0, price, 0, 0),
                            IsRenaissance = isRenaissance

                        };
                    }
                    else
                    {
                        if (args.Length == 3)
                        {
                            if (!bool.TryParse(args[2], out isRenaissance))
                            {
                                DisplaySyntax(client);
                                player.Out.SendMessage("Le parametre IsRenaissance(true ou false) n'est pas correct" + text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                                return;
                            }
                        }

                        npc = new TeleportNPC
                        {
                            Position = player.Position,
                            Heading = player.Heading,
                            CurrentRegion = player.CurrentRegion,
                            Name = "Nouveau téléporteur",
                            Realm = 0,
                            Model = 40,
                            Text = "Texte à définir.{5}",
                            IsRenaissance = isRenaissance
                        };
                    }

                    if (!npc.IsPeaceful)
                        npc.Flags ^= GameNPC.eFlags.PEACE;
                    npc.LoadedFromScript = false;
                    npc.AddToWorld();
                    npc.SaveIntoDatabase();
                    break;

                case "text":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    text = string.Join(" ", args, 2, args.Length - 2);
                    text = text.Replace('|', '\n');
                    text = text.Replace(';', '\n');
                    npc.Text = text;
                    npc.SaveIntoDatabase();
                    player.Out.SendMessage("Texte défini:\n" + text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;

                case "refuse":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    text = string.Join(" ", args, 2, args.Length - 2);
                    text = text.Replace('|', '\n');
                    text = text.Replace(';', '\n');
                    if (text == "NO TEXT")
                        npc.Text_Refuse = "";
                    else
                        npc.Text_Refuse = text;
                    npc.SaveIntoDatabase();
                    player.Out.SendMessage("Texte défini:\n" + text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
                #endregion

                #region radius - level
                case "radius":
                    if (npc == null || args.Length <= 2)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    try
                    {
                        ushort min = (ushort)int.Parse(args[2]);
                        if (min > 500) min = 500;
                        npc.Range = min;
                        npc.Realm = 0;
                        if (!npc.IsPeaceful)
                            npc.Flags ^= GameNPC.eFlags.PEACE;
                        if (!npc.IsDontShowName)
                            npc.Flags ^= GameNPC.eFlags.DONTSHOWNAME;
                        npc.Model = 1;
                        npc.SaveIntoDatabase();
                        player.Out.SendMessage("Le rayon est maintenant de " + min + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    catch { DisplaySyntax(client); }
                    break;

                case "level":
                    if (npc == null || args.Length <= 2)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    try
                    {
                        byte min = (byte)int.Parse(args[2]);
                        if (min > 49) min = 49;
                        npc.MinLevel = min;
                        npc.SaveIntoDatabase();
                        player.Out.SendMessage("Le niveau minimum requis est maintenant de " + min + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    catch { DisplaySyntax(client); }
                    break;
                #endregion

                #region addjump - removejump - jump
                case "addjump":
                    if (npc == null || args.Length <= 7)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    int X;
                    int Y;
                    int Z;
                    ushort Heading;
                    ushort RegionID;
                    try
                    {
                        X = int.Parse(args[2]);
                        Y = int.Parse(args[3]);
                        Z = int.Parse(args[4]);
                        Heading = (ushort)int.Parse(args[5]);
                        RegionID = (ushort)int.Parse(args[6]);
                        text = string.Join(" ", args, 7, args.Length - 7);
                    }
                    catch { DisplaySyntax(client); return; }
                    if (text.ToLower() == "area")
                        text = "Area";

                    npc.AddJumpPos(text, X, Y, Z, Heading, RegionID);
                    npc.SaveIntoDatabase();
                    player.Out.SendMessage("Le jump \"" + text + "\" a été ajouté.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;

                case "removejump":
                    if (npc == null || args.Length <= 2)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    text = string.Join(" ", args, 2, args.Length - 2);
                    if (npc.RemoveJumpPos(text))
                    {
                        player.Out.SendMessage("Le jump \"" + text + "\" a été supprimé.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        npc.SaveIntoDatabase();
                    }
                    else
                        player.Out.SendMessage("Le jump \"" + text + "\" n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;

                case "jump":
                    if (npc == null)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    foreach (TeleportNPC.JumpPos pos in npc.GetJumpList())
                    {
                        text += pos.Name + ": " + pos.Position + "\n";
                        text += " -> " + pos.Conditions + "\n";
                    }
                    if (text == "")
                        text = "Aucun jump";
                    player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
                #endregion

                #region conditions
                case "condition":
                case "conditions":
                    _OnConditionCommand(client, args, npc);
                    break;
                #endregion

                case "territorylinked":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args[2].Equals("on", StringComparison.CurrentCultureIgnoreCase))
                        npc.IsTerritoryLinked = true;
                    else if (args[2].Equals("off", StringComparison.CurrentCultureIgnoreCase))
                        npc.IsTerritoryLinked = false;
                    else
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    npc.SaveIntoDatabase();
                    player.Out.SendMessage("Territory linked set to " + npc.IsTerritoryLinked + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;

                case "showindicator":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args[2].Equals("on", StringComparison.CurrentCultureIgnoreCase))
                        npc.ShowTPIndicator = true;
                    else if (args[2].Equals("off", StringComparison.CurrentCultureIgnoreCase))
                        npc.ShowTPIndicator = false;
                    else
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    npc.SaveIntoDatabase();
                    player.Out.SendMessage("Show teleporter indicator set to " + npc.ShowTPIndicator + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);

                    ReloadTeleportNPC(client, npc);
                    break;

                #region Show Boundary
                case "showboundary":
                    if (npc == null || args.Length < 3) { DisplaySyntax(client); return; }
                    if (args[2].Equals("on", StringComparison.CurrentCultureIgnoreCase)) npc.ShowBoundary = true;
                    else if (args[2].Equals("off", StringComparison.CurrentCultureIgnoreCase)) npc.ShowBoundary = false;
                    else { DisplaySyntax(client); return; }
                    npc.SaveIntoDatabase();
                    player.Out.SendMessage("Show boundary set to " + npc.ShowBoundary + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    ReloadTeleportNPC(client, npc);
                    break;

                case "boundarymodel":
                    if (npc == null || args.Length < 3) { DisplaySyntax(client); return; }
                    if (int.TryParse(args[2], out int bModel))
                    {
                        npc.BoundaryModel = bModel;
                        npc.SaveIntoDatabase();
                        player.Out.SendMessage("Boundary model set to " + bModel, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        ReloadTeleportNPC(client, npc);
                    }
                    break;
                #endregion

                #region Area Pulse Settings
                case "useareapulse":
                    if (npc == null || args.Length < 3) { DisplaySyntax(client); return; }
                    if (args[2].Equals("on", StringComparison.CurrentCultureIgnoreCase)) npc.UseAreaPulse = true;
                    else if (args[2].Equals("off", StringComparison.CurrentCultureIgnoreCase)) npc.UseAreaPulse = false;
                    npc.SaveIntoDatabase();
                    player.Out.SendMessage("UseAreaPulse set to " + npc.UseAreaPulse, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    ReloadTeleportNPC(client, npc);
                    break;

                case "areapulseseconds":
                    if (npc == null || args.Length < 3) { DisplaySyntax(client); return; }
                    if (int.TryParse(args[2], out int seconds))
                    {
                        npc.AreaPulseSeconds = seconds;
                        npc.SaveIntoDatabase();
                        player.Out.SendMessage("AreaPulseSeconds set to " + seconds, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        ReloadTeleportNPC(client, npc);
                    }
                    break;

                case "areapulsecastticks":
                    if (npc == null || args.Length < 3) { DisplaySyntax(client); return; }
                    if (int.TryParse(args[2], out int ticks))
                    {
                        npc.AreaPulseCastTicks = ticks;
                        npc.SaveIntoDatabase();
                        player.Out.SendMessage("AreaPulseCastTicks set to " + ticks, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        ReloadTeleportNPC(client, npc);
                    }
                    break;

                case "areapulseclienteffect":
                    if (npc == null || args.Length < 3) { DisplaySyntax(client); return; }
                    if (ushort.TryParse(args[2], out ushort effect))
                    {
                        npc.AreaPulseClientEffect = effect;
                        npc.SaveIntoDatabase();
                        player.Out.SendMessage("AreaPulseClientEffect (Self) set to " + effect, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "areapulsecasteffect":
                    if (npc == null || args.Length < 3) { DisplaySyntax(client); return; }
                    if (ushort.TryParse(args[2], out ushort effectCast))
                    {
                        npc.AreaPulseCastEffect = effectCast;
                        npc.SaveIntoDatabase();
                        player.Out.SendMessage("AreaPulseCastEffect (Animation) set to " + effectCast, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "areapulseplayereffect":
                    if (npc == null || args.Length < 3) { DisplaySyntax(client); return; }
                    if (ushort.TryParse(args[2], out ushort effectPlayer))
                    {
                        npc.AreaPulsePlayerEffect = effectPlayer;
                        npc.SaveIntoDatabase();
                        player.Out.SendMessage("AreaPulsePlayerEffect (Target) set to " + effectPlayer, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;
                #endregion

                #region password
                case "password":
                    if (npc == null || args.Length < 2)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args.Length > 2)
                    {
                        npc.WhisperPassword = string.Join(" ", args, 2, args.Length - 2);
                        npc.SaveIntoDatabase();
                        DisplayMessage(client, "Teleporter password set to : \"" + npc.WhisperPassword + "\".");
                    }
                    else
                    {
                        npc.WhisperPassword = string.Empty;
                        npc.SaveIntoDatabase();
                        DisplayMessage(client, "Teleporter password removed.");
                    }
                    break;
                #endregion

                default:
                    DisplaySyntax(client);
                    break;
            }
        }

        private void ReloadTeleportNPC(GameClient client, TeleportNPC targetMob)
        {
            if (targetMob == null)
            {
                client.Player.Out.SendMessage("No target selected or target is not a TeleportNPC.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (targetMob.LoadedFromScript == false)
            {
                targetMob.RemoveFromWorld();
                DBTeleportNPC dbTeleportNPC = GameServer.Database.SelectObject<DBTeleportNPC>(DB.Column("MobID").IsEqualTo(targetMob.InternalID));
                if (dbTeleportNPC != null)
                {
                    targetMob.LoadFromDatabase(GameServer.Database.FindObjectByKey<Mob>(targetMob.InternalID));
                    targetMob.AddToWorld();
                    client.Player.Out.SendMessage(targetMob.Name + " reloaded!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    client.Player.Out.SendMessage("Failed to reload TeleportNPC. Database entry not found.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }
            else
            {
                client.Player.Out.SendMessage(targetMob.Name + " is loaded from a script and can't be reloaded!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        private void _OnConditionCommand(GameClient client, string[] args, TeleportNPC npc)
        {
            if (args.Length < 5 || npc == null)
            {
                DisplaySyntax(client);
                return;
            }
            if (!npc.JumpPositions.ContainsKey(args[2]))
            {
                DisplayMessage(client, "Le pnj sélectionné ne contient pas le jump \"" + args[2] + "\" !");
                return;
            }
            TeleportNPC.JumpPos jump = npc.JumpPositions[args[2]];
            int min, max, questID, stepID;
            switch (args[3].ToLower())
            {
                #region visible
                case "visible":
                    if (args[4].Equals("on", StringComparison.CurrentCultureIgnoreCase) || args[4].Equals("off", StringComparison.CurrentCultureIgnoreCase))
                    {
                        jump.Conditions.Visible = args[4].Equals("on", StringComparison.CurrentCultureIgnoreCase);
                        DisplayMessage(client,
                                       "Le jump \"" + jump.Name + "\" est maintenant "
                                       + (jump.Conditions.Visible ? "" : "in") + "visible dans la liste des jumps.");
                    }
                    else
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    break;
                #endregion

                #region item
                case "item":
                case "objet":
                    jump.Conditions.Item = args[4];
                    DisplayMessage(client, "Le jump \"" + jump.Name + "\" nécessite maintenant l'item avec le template: \"" + args[4] + "\".");
                    break;

                case "slot":
                    if (int.TryParse(args[4], out int slot))
                    {
                        jump.Conditions.RequiredSlot = slot;
                        DisplayMessage(client, "Le jump \"" + jump.Name + "\" nécessite l'item équipé dans le slot: " + slot + ".");
                    }
                    else { DisplaySyntax(client); return; }
                    break;

                case "condition":
                    if (int.TryParse(args[4], out int condAmount))
                    {
                        jump.Conditions.ConditionAmount = condAmount;
                        DisplayMessage(client, "Le jump \"" + jump.Name + "\" consommera " + condAmount + " points de condition de l'item.");
                    }
                    else { DisplaySyntax(client); return; }
                    break;
                #endregion

                #region niveaux
                case "level":
                case "niveau":
                case "niveaux":
                    if (int.TryParse(args[4], out min))
                    {
                        int maxLevel = jump.Conditions.LevelMax;
                        if (args.Length > 5 && !int.TryParse(args[5], out maxLevel))
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        jump.Conditions.LevelMin = min;
                        jump.Conditions.LevelMax = maxLevel;
                        DisplayMessage(client,
                                       "Le jump \"" + jump.Name + "\" demande un niveau compris entre " + min + " et "
                                       + maxLevel + ".");
                    }
                    else
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    break;
                #endregion

                case "bind":
                    if (args[4].Equals("on", StringComparison.CurrentCultureIgnoreCase) || args[4].Equals("off", StringComparison.CurrentCultureIgnoreCase))
                    {
                        jump.Conditions.Bind = args[4].Equals("on", StringComparison.CurrentCultureIgnoreCase);
                        if (jump.Conditions.Bind)
                            DisplayMessage(client, "Le jump \"" + jump.Name + "\" bind le joueur après l'avoir téléporté.");
                        else
                            DisplayMessage(client, "Le jump \"" + jump.Name + "\" ne bind pas le joueur après l'avoir téléporté.");
                    }
                    else
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    break;

                #region hours
                case "hour":
                case "hours":
                case "heure":
                case "heures":
                    if (int.TryParse(args[4], out min) && int.TryParse(args[5], out max))
                    {
                        jump.Conditions.HourMin = min;
                        jump.Conditions.HourMax = max;
                        DisplayMessage(client, "Le jump \"" + jump.Name + "\" est disponible entre " + min + "h et " + max + "h.");
                    }
                    else
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    break;
                #endregion

                #region completedquest
                case "completedquest":
                case "quest":
                    if (int.TryParse(args[4], out questID))
                    {
                        jump.Conditions.RequiredCompletedQuestID = questID;
                        DisplayMessage(client, "Le jump \"" + jump.Name + "\" nécessite la quête complétée avec ID : " + questID + ".");
                    }
                    else
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    break;
                #endregion

                #region queststep
                case "queststep":
                case "step":
                    if (int.TryParse(args[4], out questID) && int.TryParse(args[5], out stepID))
                    {
                        jump.Conditions.RequiredQuestStepID = stepID;
                        DisplayMessage(client, "Le jump \"" + jump.Name + "\" nécessite la quête ID : " + questID + ", étape : " + stepID + ".");
                    }
                    else
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    break;
                #endregion

                #region event
                case "event":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    GameEvent e;
                    if (args.Length > 4)
                    {
                        e = GameEventManager.Instance.GetEventByID(args[4]);
                        if (e == null)
                        {
                            DisplayMessage(client, $"Event ID {args[4]} not found.");
                        }
                        else
                        {
                            jump.Conditions.ActiveEventId = e.ID;
                            lock (e.RelatedNPCs)
                            {
                                e.RelatedNPCs.Add(npc);
                            }
                            DisplayMessage(client, "Teleporter required event set to : \"" + jump.Conditions.ActiveEventId + "\".");
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(jump.Conditions.ActiveEventId))
                        {
                            e = GameEventManager.Instance.GetEventByID(jump.Conditions.ActiveEventId);
                            if (e != null)
                            {
                                lock (e.RelatedNPCs)
                                {
                                    e.RelatedNPCs.Remove(npc);
                                }
                            }
                        }
                        jump.Conditions.ActiveEventId = string.Empty;
                        DisplayMessage(client, "Teleporter required event removed.");
                    }
                    break;
                #endregion

                default:
                    DisplaySyntax(client);
                    return;
            }
            npc.SaveIntoDatabase();
            npc.RemoveFromWorld();
            npc.AddToWorld();
        }
    }
}
