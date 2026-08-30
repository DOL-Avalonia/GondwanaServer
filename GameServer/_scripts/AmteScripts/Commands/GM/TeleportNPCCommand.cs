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
         "Commands.GM.TeleportNPC.Usage.Conditions.Hours",
         "Commands.GM.TeleportNPC.Usage.Conditions.Event",
         "Commands.GM.TeleportNPC.Usage.Conditions.CompletedQuest",
         "Commands.GM.TeleportNPC.Usage.Conditions.QuestStep",
         "Commands.GM.TeleportNPC.Usage.Conditions.BlockRelic",
         "Commands.GM.TeleportNPC.Usage.Conditions.InstanceRule",
         "Commands.GM.TeleportNPC.Usage.Conditions.ScaleMobs",
         "Commands.GM.TeleportNPC.Usage.Conditions.BossScaling",
         "Commands.GM.TeleportNPC.Usage.Conditions.InstanceSkin",
         "Commands.GM.TeleportNPC.Usage.Conditions.ClonePlayer",
         "Commands.GM.TeleportNPC.Usage.Conditions.Remove",
         "Commands.GM.TeleportNPC.Usage.TerritoryLinked",
         "Commands.GM.TeleportNPC.Usage.ShowTeleporterIndicator",
         "Commands.GM.TeleportNPC.Usage.ShowBoundary",
         "Commands.GM.TeleportNPC.Usage.BoundaryModel",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.Use",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.Seconds",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.CastTicks",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.ClientEffect",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.CastEffect",
         "Commands.GM.TeleportNPC.Usage.AreaPulse.PlayerEffect",
         "Commands.GM.TeleportNPC.Usage.AdditionalDescription")]
    public class TeleportNPCCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private void RefreshIndicators(GameNPC npc)
        {
            if (npc == null) return;
            foreach (GamePlayer player in npc.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                QuestIndicatorManager.RefreshIndicator(npc, player);
            }
        }

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
                            ModelDb = 40,
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
                            ModelDb = 40,
                            Text = "Texte à définir.{5}",
                            IsRenaissance = isRenaissance
                        };
                    }

                    if (!npc.IsPeaceful)
                    {
                        npc.Flags ^= GameNPC.eFlags.PEACE;
                        npc.FlagsDb = (uint)npc.Flags;
                    }

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
                        {
                            npc.Flags ^= GameNPC.eFlags.PEACE;
                            npc.FlagsDb = (uint)npc.Flags;
                        }
                        if (!npc.IsDontShowName)
                        {
                            npc.Flags ^= GameNPC.eFlags.DONTSHOWNAME;
                            npc.FlagsDb = (uint)npc.Flags;
                        }

                        npc.Model = 1;
                        npc.ModelDb = 1;
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
                    string indToggle = args[2].Replace("\"", "").ToLower();
                    if (indToggle == "on")
                        npc.ShowTPIndicator = true;
                    else if (indToggle == "off")
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
                    string boundToggle = args[2].Replace("\"", "").ToLower();
                    if (boundToggle == "on") npc.ShowBoundary = true;
                    else if (boundToggle == "off") npc.ShowBoundary = false;
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
                    RefreshIndicators(targetMob);
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
                case "levels":
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

                #region blockrelic
                case "blockrelic":
                case "relic":
                    if (args.Length > 4 && (args[4].Equals("on", StringComparison.CurrentCultureIgnoreCase) || args[4].Equals("off", StringComparison.CurrentCultureIgnoreCase)))
                    {
                        jump.Conditions.BlockRelic = args[4].Equals("on", StringComparison.CurrentCultureIgnoreCase);
                        if (jump.Conditions.BlockRelic)
                            DisplayMessage(client, "Le jump \"" + jump.Name + "\" est maintenant bloqué si le joueur porte une relique.");
                        else
                            DisplayMessage(client, "Le jump \"" + jump.Name + "\" autorise maintenant les porteurs de relique.");
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

                #region instances
                case "instancerule":
                    if (args.Length < 5) { DisplaySyntax(client); return; }
                    if (Enum.TryParse(args[4], true, out eInstanceRule rule))
                    {
                        jump.Conditions.InstanceRule = rule;
                        DisplayMessage(client, "Instance rule set to: " + rule);
                    }
                    else { DisplaySyntax(client); return; }
                    break;

                case "scalemobs":
                    if (args.Length < 5) { DisplaySyntax(client); return; }

                    if (args[4].Equals("smart", StringComparison.OrdinalIgnoreCase))
                    {
                        jump.Conditions.ScaleMobs = true;
                        jump.Conditions.SmartScale = true;
                        jump.Conditions.ScaleOffset = 0;
                        DisplayMessage(client, "Mob scaling set to: Smart (Class-based for Solo)");
                    }
                    else if (bool.TryParse(args[4], out bool scale))
                    {
                        jump.Conditions.ScaleMobs = scale;
                        jump.Conditions.SmartScale = false;
                        jump.Conditions.ScaleOffset = 0;
                        DisplayMessage(client, "Mob scaling set to: " + scale);
                    }
                    else if (int.TryParse(args[4], out int offset))
                    {
                        jump.Conditions.ScaleMobs = true;
                        jump.Conditions.SmartScale = false;
                        jump.Conditions.ScaleOffset = offset;
                        DisplayMessage(client, "Mob scaling enabled with offset: " + offset);
                    }
                    else { DisplaySyntax(client); return; }
                    break;

                case "bossscaling":
                    if (args.Length < 5) { DisplaySyntax(client); return; }
                    string bossStr = string.Join(" ", args.Skip(4));
                    if (bossStr.ToLower() == "none") bossStr = "";
                    jump.Conditions.BossScaling = bossStr;
                    DisplayMessage(client, "Boss scaling set to: " + (bossStr == "" ? "None" : bossStr));
                    break;

                case "instanceskin":
                    if (args.Length < 5) { DisplaySyntax(client); return; }
                    if (ushort.TryParse(args[4], out ushort skinId))
                    {
                        jump.Conditions.InstanceSkin = skinId;
                        DisplayMessage(client, "Instance skin set to: " + skinId);
                    }
                    else { DisplaySyntax(client); return; }
                    break;

                case "cloneplayerclasses":
                    if (args.Length < 5) { DisplaySyntax(client); return; }
                    if (bool.TryParse(args[4], out bool cloneClasses))
                    {
                        jump.Conditions.ClonePlayerClasses = cloneClasses;
                        if (args.Length >= 6 && ushort.TryParse(args[5], out ushort col))
                            jump.Conditions.CloneClassesColor = col;
                        else
                            jump.Conditions.CloneClassesColor = 0; // Default 0

                        DisplayMessage(client, "Clone player classes set to: " + cloneClasses + (cloneClasses ? $" (Color: {jump.Conditions.CloneClassesColor})" : ""));
                    }
                    else { DisplaySyntax(client); return; }
                    break;
                #endregion

                #region remove
                case "remove":
                    if (args.Length < 5)
                    {
                        DisplaySyntax(client);
                        return;
                    }

                    string propToRemove = args[4].ToLower();

                    if (propToRemove == "all")
                    {
                        if (!string.IsNullOrEmpty(jump.Conditions.ActiveEventId))
                        {
                            var ev = GameEventManager.Instance.GetEventByID(jump.Conditions.ActiveEventId);
                            if (ev != null)
                            {
                                lock (ev.RelatedNPCs) { ev.RelatedNPCs.Remove(npc); }
                            }
                        }

                        jump.Conditions = new TeleportNPC.TeleportCondition("");
                        DisplayMessage(client, "Toutes les conditions pour le jump \"" + jump.Name + "\" ont été supprimées.");
                    }
                    else
                    {
                        switch (propToRemove)
                        {
                            case "visible": jump.Conditions.Visible = true; break;
                            case "item":
                            case "objet": jump.Conditions.Item = ""; break;
                            case "slot": jump.Conditions.RequiredSlot = 0; break;
                            case "condition": jump.Conditions.ConditionAmount = 0; break;
                            case "level":
                            case "niveau":
                            case "niveaux":
                                jump.Conditions.LevelMin = 0;
                                jump.Conditions.LevelMax = 50;
                                break;
                            case "bind": jump.Conditions.Bind = false; break;
                            case "hour":
                            case "hours":
                            case "heure":
                            case "heures":
                                jump.Conditions.HourMin = 0;
                                jump.Conditions.HourMax = 24;
                                break;
                            case "completedquest":
                            case "quest": jump.Conditions.RequiredCompletedQuestID = 0; break;
                            case "queststep":
                            case "step": jump.Conditions.RequiredQuestStepID = 0; break;
                            case "blockrelic":
                            case "relic": jump.Conditions.BlockRelic = false; break;
                            case "event":
                                if (!string.IsNullOrEmpty(jump.Conditions.ActiveEventId))
                                {
                                    var ev = GameEventManager.Instance.GetEventByID(jump.Conditions.ActiveEventId);
                                    if (ev != null)
                                    {
                                        lock (ev.RelatedNPCs) { ev.RelatedNPCs.Remove(npc); }
                                    }
                                }
                                jump.Conditions.ActiveEventId = string.Empty;
                                break;
                            case "instancerule": jump.Conditions.InstanceRule = eInstanceRule.None; break;
                            case "scalemobs":
                                jump.Conditions.ScaleMobs = false;
                                jump.Conditions.SmartScale = false;
                                jump.Conditions.ScaleOffset = 0;
                                break;
                            case "bossscaling": jump.Conditions.BossScaling = string.Empty; break;
                            case "instanceskin": jump.Conditions.InstanceSkin = 0; break;
                            case "cloneplayerclasses":
                                jump.Conditions.ClonePlayerClasses = false;
                                jump.Conditions.CloneClassesColor = 0;
                                break;
                            default:
                                DisplayMessage(client, "La condition \"" + args[4] + "\" n'est pas reconnue.");
                                return;
                        }
                        DisplayMessage(client, "La condition \"" + args[4] + "\" a été supprimée pour le jump \"" + jump.Name + "\".");
                    }
                    break;
                #endregion

                default:
                    DisplaySyntax(client);
                    return;
            }

            npc.SaveIntoDatabase();
            RefreshIndicators(npc);
        }
    }
}
