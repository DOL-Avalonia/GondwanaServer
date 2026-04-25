using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS.Commands;
using System.Linq;

namespace DOL.GS.Scripts
{
    public class CoffreCommandHandlerBase : AbstractCommandHandler, ICommandHandler
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

            GameCoffre coffre = player.TargetObject as GameCoffre;
            switch (args[1].ToLower())
            {
                #region create - createcatapult - model
                case "create":
                    coffre = new GameCoffre
                    {
                        Name = "Coffre",
                        Model = 1596,
                        Position = player.Position,
                        Heading = player.Heading,
                        CurrentRegionID = player.CurrentRegionID,
                        RespawnInterval = 60 * 60,
                        ItemChance = 100
                    };
                    coffre.LoadedFromScript = false;
                    coffre.AddToWorld();
                    coffre.SaveIntoDatabase();
                    ChatUtil.SendSystemMessage(client, "Vous avez créé un coffre (OID:" + coffre.ObjectID + ")");
                    break;

                case "createcatapult":
                    GameStaticCatapult catapult = new GameStaticCatapult
                    {
                        Name = "Catapult_Backbone",
                        Model = 1682,
                        SecondaryModel = 2598,
                        Position = player.Position,
                        Heading = player.Heading,
                        CurrentRegionID = player.CurrentRegionID,
                        RespawnInterval = 0 * 0,
                        ItemChance = 100,
                        CoffreOpeningInterval = 0,
                        PunishSpellId = 20622
                    };
                    catapult.LoadedFromScript = false;
                    catapult.AddToWorld();
                    catapult.SaveIntoDatabase();
                    ChatUtil.SendSystemMessage(client, "Vous avez créé une catapulte (OID Backbone:" + catapult.ObjectID + ")");
                    break;

                case "model":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.Model = (ushort)int.Parse(args[2]);
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" a maintenant le model " + coffre.Model);
                    break;
                #endregion

                case "interval":
                    int min = 0;

                    if (coffre != null && args.Length == 3 && int.TryParse(args[2], out min) && min >= 0)
                    {
                        coffre.CoffreOpeningInterval = min;
                        coffre.SaveIntoDatabase();
                        ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" a maintenant l'interval d'ouverture de " + min + " minutes");
                    }
                    else
                    {
                        DisplaySyntax(client);
                    }

                    break;

                case "longdistance":
                    bool isLongDitance = false;

                    if (coffre != null && args.Length == 3 && bool.TryParse(args[2], out isLongDitance))
                    {
                        coffre.IsLargeCoffre = isLongDitance;
                        coffre.SaveIntoDatabase();
                        ChatUtil.SendSystemMessage(client, "La valeur LongDistance du coffre \"" + coffre.Name + " est maintenant de: " + isLongDitance);
                    }
                    else
                    {
                        DisplaySyntax(client);
                    }

                    break;

                #region item - add - remove
                case "item":
                    if (coffre == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.ItemChance = int.Parse(args[2]);
                        coffre.RespawnInterval = int.Parse(args[3]) * 60;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" a maintenant " + coffre.ItemChance + " chances de faire apparaitre un item toutes les " + coffre.RespawnInterval / 60 + " minutes.");
                    break;

                case "add":
                    if (coffre == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        if (!coffre.ModifyItemList(args[2], int.Parse(args[3])))
                        {
                            ChatUtil.SendSystemMessage(client, "L'item \"" + args[2] + "\" n'existe pas !");
                            break;
                        }
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" peut faire apparaitre un item \"" + args[2] + "\" avec un taux de chance de " + args[3]);
                    break;
                case "remove":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        if (!coffre.DeleteItemFromItemList(args[2]))
                        {
                            ChatUtil.SendSystemMessage(client, "L'item \"" + args[2] + "\" n'existe pas !");
                            break;
                        }
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" ne peut plus faire apparaitre l'item \"" + args[2] + "\"");
                    break;
                #endregion

                #region name - movehere - delete
                case "name":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    coffre.Name = args[2];
                    coffre.SaveIntoDatabase();
                    break;

                case "movehere":
                    if (coffre == null)
                    {
                        DisplaySyntax(client);
                        break;
                    }

                    coffre.Position = player.Position;
                    coffre.Heading = player.Heading;
                    coffre.SaveIntoDatabase();
                    ChatUtil.SendSystemMessage(client, "Le coffre selectionné a été déplacé à votre position.");
                    break;

                case "delete":
                    if (coffre == null)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    coffre.Delete();
                    coffre.DeleteFromDatabase();
                    ChatUtil.SendSystemMessage(client, "Le coffre selectionné a été supprimé.");
                    break;
                #endregion

                #region reset - info
                case "reset":
                    if (coffre == null)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    GameCoffre oldCoffre = coffre;
                    coffre = new GameCoffre();
                    coffre.Name = "Coffre";
                    coffre.Model = 1596;
                    coffre.Position = oldCoffre.Position;
                    coffre.Heading = oldCoffre.Heading;
                    coffre.CurrentRegionID = oldCoffre.CurrentRegionID;
                    coffre.RespawnInterval = 60 * 60;
                    coffre.ItemChance = 100;
                    coffre.LastOpen = DateTime.MinValue;
                    oldCoffre.RemoveFromWorld();
                    oldCoffre.DeleteFromDatabase();
                    coffre.AddToWorld();

                    coffre.SaveIntoDatabase();
                    ChatUtil.SendSystemMessage(client, "Le coffre selectionné a été remit à zéro.");
                    break;

                case "info":
                    if (coffre == null)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    player.Out.SendCustomTextWindow(coffre.Name, coffre.DelveInfo());
                    break;
                #endregion

                #region copy - randomcopy
                case "randomcopy":
                case "copy":
                    if (coffre == null)
                    {
                        DisplaySyntax(client);
                        break;
                    }

                    GameCoffre coffre2;
                    if (args[1].ToLower() == "randomcopy")
                    {
                        List<GameCoffre.CoffreItem> items = new List<GameCoffre.CoffreItem>(coffre.Items);
                        foreach (GameCoffre.CoffreItem item in items)
                        {
                            if (Util.Chance(50))
                                item.Chance += (int)(item.Chance * Util.RandomDouble() / 10);
                            else if (item.Chance > 1)
                            {
                                item.Chance -= (int)(item.Chance * Util.RandomDouble() / 10);
                                if (item.Chance < 1)
                                    item.Chance = 1;
                            }
                        }
                        coffre2 = new GameCoffre(items);
                    }
                    else
                        coffre2 = new GameCoffre(coffre.Items);

                    coffre2.Name = coffre.Name + "_cpy";
                    coffre2.Position = player.Position;
                    coffre2.Heading = player.Heading;
                    coffre2.CurrentRegion = player.CurrentRegion;
                    coffre2.Model = coffre.Model;
                    coffre2.RespawnInterval = coffre.RespawnInterval;
                    coffre2.TpEffect = coffre.TpEffect;
                    coffre2.TpIsRenaissance = coffre.TpIsRenaissance;
                    coffre2.TpLevelRequirement = coffre.TpLevelRequirement;
                    coffre2.TpX = coffre.TpX;
                    coffre2.TpY = coffre.TpY;
                    coffre2.TpZ = coffre.TpZ;
                    coffre2.TpRegion = coffre.TpRegion;
                    coffre2.TrapRate = coffre.TrapRate;
                    coffre2.NpctemplateId = coffre.NpctemplateId;
                    coffre2.PunishSpellId = coffre.PunishSpellId;
                    coffre2.IsOpeningRenaissanceType = coffre.IsOpeningRenaissanceType;
                    coffre2.IsTeleporter = coffre.IsTeleporter;
                    coffre2.CoffreOpeningInterval = coffre.CoffreOpeningInterval;
                    coffre2.IsLargeCoffre = coffre.IsLargeCoffre;
                    coffre2.ItemChance = coffre.ItemChance;
                    coffre2.KeyItem = coffre.KeyItem;
                    coffre2.TPID = coffre.TPID;
                    coffre2.ShouldRespawnToTPID = coffre.ShouldRespawnToTPID;
                    coffre2.CurrentStep = coffre.CurrentStep;
                    coffre2.PickOnTouch = coffre.PickOnTouch;
                    coffre2.SecondaryModel = coffre.SecondaryModel;
                    coffre2.IsOpenableOnce = coffre.IsOpenableOnce;
                    coffre2.IsTerritoryLinked = coffre.IsTerritoryLinked;
                    coffre2.KeyLoseDur = coffre.KeyLoseDur;
                    coffre2.SwitchFamily = coffre.SwitchFamily;
                    coffre2.SwitchOrder = coffre.SwitchOrder;
                    coffre2.IsSwitch = coffre.IsSwitch;
                    coffre2.WrongOrderResetFamily = coffre.WrongOrderResetFamily;
                    coffre2.ActivatedDuration = coffre.ActivatedDuration;
                    coffre2.ActivatedBySwitchOn = coffre.ActivatedBySwitchOn;
                    coffre2.ActivatedBySwitchOff = coffre.ActivatedBySwitchOff;
                    coffre2.ResetBySwitchOn = coffre.ResetBySwitchOn;
                    coffre2.ResetBySwitchOff = coffre.ResetBySwitchOff;
                    coffre2.SwitchOnSound = coffre.SwitchOnSound;
                    coffre2.WrongFamilyOrderSound = coffre.WrongFamilyOrderSound;
                    coffre2.ActivatedFamilySound = coffre.ActivatedFamilySound;
                    coffre2.DeactivatedFamilySound = coffre.DeactivatedFamilySound;
                    coffre2.LootGenerators = coffre.LootGenerators == null ? null : new List<ILootGenerator>(coffre.LootGenerators);

                    coffre2.ItemChance = coffre.ItemChance;
                    if (args[1].ToLower() == "randomcopy")
                    {
                        var minutes = coffre2.RespawnInterval / 60;
                        if (Util.Chance(50))
                            minutes += (int)(minutes * Util.RandomDouble() / 10);
                        else if (minutes > 1)
                        {
                            minutes -= (int)(minutes * Util.RandomDouble() / 10);
                            if (minutes < 1)
                                minutes = 1;
                        }
                        coffre2.RespawnInterval = minutes * 60;
                        if (Util.Chance(50) && coffre2.ItemChance < 100)
                        {
                            coffre2.ItemChance += (int)(coffre2.ItemChance * Util.RandomDouble() / 10);
                            if (coffre2.ItemChance > 100)
                                coffre2.ItemChance = 100;
                        }
                        else if (coffre2.ItemChance > 0)
                        {
                            coffre2.ItemChance -= (int)(coffre2.ItemChance * Util.RandomDouble() / 10);
                            if (coffre2.ItemChance < 0)
                                coffre2.ItemChance = 0;
                        }
                    }
                    coffre2.AddToWorld();
                    coffre2.SaveIntoDatabase();
                    ChatUtil.SendSystemMessage(client, "Vous avez créé un coffre (OID:" + coffre2.ObjectID + ")");
                    break;
                #endregion

                #region key - difficult
                case "key":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    if (args[2].ToLower() == "nokey")
                        coffre.KeyItem = "";
                    else
                    {
                        if (GameServer.Database.FindObjectByKey<ItemTemplate>(args[2]) == null)
                        {
                            ChatUtil.SendSystemMessage(client, "La clef ayant l'id_nb \"" + args[2] + "\" n'existe pas.");
                            break;
                        }
                        coffre.KeyItem = args[2];
                    }
                    coffre.SaveIntoDatabase();

                    if (coffre.KeyItem == "")
                        ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" n'a plus besoin de clef pour être ouvert.");
                    else
                        ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" a besoin de la clef ayant l'id_nb \"" + coffre.KeyItem + "\" pour être ouvert.");
                    break;

                case "difficult":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        int num = int.Parse(args[2]);
                        if (num >= 100)
                            num = 99;
                        if (num < 0)
                            num = 0;
                        coffre.LockDifficult = num;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    if (coffre.LockDifficult > 0)
                        ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" a maintenant une difficulté pour être crocheter de " + coffre.LockDifficult + "%.");
                    else
                        ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" ne peut plus être crocheté.");
                    break;

                case "traprate":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.TrapRate = int.Parse(args[2]);
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" a maintenant le traprate de " + coffre.TrapRate);
                    break;

                case "npctemplate":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.NpctemplateId = args[2];
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" a maintenant le npctemplate de " + coffre.NpctemplateId);
                    break;

                case "isteleporter":
                    if (coffre == null || args.Length != 2)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.IsTeleporter = !coffre.IsTeleporter;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut IsTeleporter du coffre \"" + coffre.Name + "\" est maitenant: " + coffre.IsTeleporter);
                    break;

                case "pickableanim":
                    if (coffre == null || args.Length != 2)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.HasPickableAnim = !coffre.HasPickableAnim;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut HasPickbleAnim du coffre \"" + coffre.Name + "\" est maitenant: " + coffre.HasPickableAnim);
                    break;

                case "isopeningrenaissance":
                    if (coffre == null || args.Length != 2)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.IsOpeningRenaissanceType = !coffre.IsOpeningRenaissanceType;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut IsOpeningRenaissanceType du coffre \"" + coffre.Name + "\" est maitenant: " + coffre.IsOpeningRenaissanceType);
                    break;

                case "respawn":
                    if (args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        string name = args[2];

                        if (!string.IsNullOrEmpty(name) && GameCoffre.ChestsByName.TryGetValue(name, out List<GameCoffre> coffres) && coffres?.Count == 1)
                        {
                            var chest = coffres.First();
                            chest.RespawnCoffre();
                            ChatUtil.SendSystemMessage(client, "Le coffre \"" + chest.Name + "\" apparait devant vous.");
                        }
                        else
                        {
                            ChatUtil.SendSystemMessage(client, "Le coffre \"" + name + "\" n'a pas été trouvé ou plusieurs coffres avec le meme nom existent.");
                        }
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    break;

                case "teleporter":
                    if (coffre == null || args.Length <= 6)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    int X;
                    int Y;
                    int Z;
                    ushort heading;
                    ushort RegionID;
                    try
                    {
                        X = int.Parse(args[2]);
                        Y = int.Parse(args[3]);
                        Z = int.Parse(args[4]);
                        heading = (ushort)int.Parse(args[5]);
                        RegionID = (ushort)int.Parse(args[6]);
                    }
                    catch { DisplaySyntax(client); return; }

                    coffre.TpX = X;
                    coffre.TpY = Y;
                    coffre.TpZ = Z;
                    coffre.TpRegion = RegionID;
                    coffre.TPHeading = heading;
                    coffre.SaveIntoDatabase();
                    player.Out.SendMessage("Le Coffre \"" + coffre + "\" a recu une nouvelle destination de téléportation", PacketHandler.eChatType.CT_System, PacketHandler.eChatLoc.CL_SystemWindow);

                    if (!coffre.IsTeleporter)
                    {
                        player.Out.SendMessage("Le statut IsTeleporter du Coffre \"" + coffre + "\" n'est pas activé, changez le pour activer la téléportation.", PacketHandler.eChatType.CT_System, PacketHandler.eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "tprequirement":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        int level = int.Parse(args[2]);
                        if (level < 1)
                        {
                            level = 1;
                        }

                        coffre.TpLevelRequirement = level;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le Niveau minimum pour utiliser le téléporteur du coffre \"" + coffre.Name + "\" est maitenant: " + coffre.TpLevelRequirement);
                    break;

                case "punishspellid":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        int spellID = int.Parse(args[2]);
                        coffre.PunishSpellId = spellID;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le PunishSpellID du coffre \"" + coffre.Name + "\" est maitenant: " + coffre.PunishSpellId);
                    break;

                case "tpeffect":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.TpEffect = int.Parse(args[2]);
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "L'effet utilisé pour le téléporteur du coffre \"" + coffre.Name + "\" est maitenant lié au SpellID: " + coffre.TpEffect);
                    break;

                case "tpisrenaissance":
                    if (coffre == null || args.Length != 2)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.TpIsRenaissance = !coffre.TpIsRenaissance;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut IsRenaissance du coffre \"" + coffre.Name + "\" est maitenant: " + coffre.TpIsRenaissance);
                    break;

                case "pickontouch":
                    if (coffre == null || args.Length != 2)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.PickOnTouch = !coffre.PickOnTouch;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut PickOnTouch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.PickOnTouch);
                    break;

                case "secondarymodel":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.SecondaryModel = int.Parse(args[2].Substring(0, Math.Min(5, args[2].Length)));
                        coffre.SaveIntoDatabase();

                        if (coffre is GameStaticCatapult cat && cat.NPCCatapult != null)
                        {
                            cat.NPCCatapult.Model = (ushort)coffre.SecondaryModel;
                        }
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le coffre \"" + coffre.Name + "\" a maintenant le modèle secondaire: " + coffre.SecondaryModel);
                    break;

                case "isopenableonce":
                    if (coffre == null || args.Length != 2)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.IsOpenableOnce = !coffre.IsOpenableOnce;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut IsOpenableOnce du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.IsOpenableOnce);
                    break;

                case "isterritorylinked":
                    if (coffre == null || args.Length != 2)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.IsTerritoryLinked = !coffre.IsTerritoryLinked;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut IsTerritoryLinked du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.IsTerritoryLinked);
                    break;

                case "keylosedur":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.KeyLoseDur = int.Parse(args[2]);
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "La durabilité de la clé du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.KeyLoseDur);
                    break;

                case "tpid":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.TPID = int.Parse(args[2].Substring(0, Math.Min(5, args[2].Length)));
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le TPID du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.TPID);
                    break;

                case "shouldrespawntotpid":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.ShouldRespawnToTPID = !coffre.ShouldRespawnToTPID;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut ShouldRespawnToTPID du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.ShouldRespawnToTPID);
                    break;

                case "currentstep":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.CurrentStep = int.Parse(args[2]);
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le CurrentStep du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.CurrentStep);
                    break;

                case "switchfamily":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.SwitchFamily = args[2];
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "La famille de switch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.SwitchFamily);
                    break;

                case "switchorder":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.SwitchOrder = int.Parse(args[2]);
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "L'ordre de switch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.SwitchOrder);
                    break;

                case "isswitch":
                    if (coffre == null || args.Length != 2)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.IsSwitch = !coffre.IsSwitch;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut IsSwitch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.IsSwitch);
                    break;

                case "wrongorderresetfamily":
                    if (coffre == null || args.Length != 2)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.WrongOrderResetFamily = !coffre.WrongOrderResetFamily;
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le statut WrongOrderResetFamily du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.WrongOrderResetFamily);
                    break;

                case "activatedduration":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.ActivatedDuration = int.Parse(args[2].Substring(0, Math.Min(5, args[2].Length)));
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "La durée d'activation du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.ActivatedDuration + " secondes");
                    break;

                case "activatedbyswitchon":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.ActivatedBySwitchOn = args[2];
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "L'event activé par le switch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.ActivatedBySwitchOn);
                    break;

                case "activatedbyswitchoff":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.ActivatedBySwitchOff = args[2];
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "L'event désactivé par le switch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.ActivatedBySwitchOff);
                    break;

                case "resetbyswitchon":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.ResetBySwitchOn = args[2];
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "L'event reset par le switch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.ResetBySwitchOn);
                    break;

                case "resetbyswitchoff":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.ResetBySwitchOff = args[2];
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "L'event reset par le switch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.ResetBySwitchOff);
                    break;

                case "switchonsound":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.SwitchOnSound = int.Parse(args[2].Substring(0, Math.Min(5, args[2].Length)));
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le son du switch ON du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.SwitchOnSound);
                    break;

                case "wrongfamilyordersound":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.WrongFamilyOrderSound = int.Parse(args[2].Substring(0, Math.Min(5, args[2].Length)));
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le son d'ordre incorrect du switch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.WrongFamilyOrderSound);
                    break;

                case "activatedfamilysound":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.ActivatedFamilySound = int.Parse(args[2].Substring(0, Math.Min(5, args[2].Length)));
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le son de famille activée du switch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.ActivatedFamilySound);
                    break;

                case "deactivatedfamilysound":
                    if (coffre == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    try
                    {
                        coffre.DeactivatedFamilySound = int.Parse(args[2].Substring(0, Math.Min(5, args[2].Length)));
                        coffre.SaveIntoDatabase();
                    }
                    catch
                    {
                        DisplaySyntax(client);
                        break;
                    }
                    ChatUtil.SendSystemMessage(client, "Le son de famille désactivée du switch du coffre \"" + coffre.Name + "\" est maintenant: " + coffre.DeactivatedFamilySound);
                    break;

                case "lootgenerator":
                    if (coffre == null)
                    {
                        DisplaySyntax(client);
                        break;
                    }

                    if (args.Length < 3 || string.Equals(args[2], "list"))
                    {
                        ChatUtil.SendSystemMessage(client, string.Format("{0} générateurs pour ce coffre", coffre.LootGenerators?.Count ?? 0));
                        foreach (ILootGenerator gen in coffre.LootGenerators ?? new List<ILootGenerator>())
                        {
                            ChatUtil.SendSystemMessage(client, "- " + gen.DatabaseId);
                        }
                        break;
                    }

                    switch (args[2])
                    {
                        case "add":
                            {
                                var dbLootGenerator = GameServer.Database.SelectObject<LootGenerator>(DB.Column("LootGenerator_ID").IsEqualTo(GameServer.Database.Escape(args[3])));
                                if (dbLootGenerator == null)
                                {
                                    ChatUtil.SendSystemMessage(client, string.Format("Aucun générateur de butin n'a été trouvé pour l'ID {0}", args[3]));
                                    break;
                                }

                                var lootGenerator = LootMgr.GetGeneratorInCache(dbLootGenerator);
                                if (lootGenerator == null)
                                {
                                    ChatUtil.SendSystemMessage(client, string.Format("Le générateur {0} a une entrée dans la base de donnée mais n'est pas dans le cache, veuillez relancer le serveur", args[2]));
                                    break;
                                }

                                (coffre.LootGenerators ??= new List<ILootGenerator>()).Add(lootGenerator);
                                ChatUtil.SendSystemMessage(client, string.Format("Le coffre {0} utilise maintenant le générateur de butin {1}", coffre.InternalID, lootGenerator.DatabaseId));
                            }
                            break;
                        
                        case "remove":
                            int idx = coffre.LootGenerators?.FindIndex(g => string.Equals(g.DatabaseId, args[3])) ?? -1;
                            if (idx < 0)
                            {
                                ChatUtil.SendSystemMessage(client, string.Format("Impossible de trouver le générateur {0} dans ce coffre.", args[3]));
                                break;
                            }
                            coffre.LootGenerators!.RemoveAt(idx);
                            if (coffre.LootGenerators.Count == 0)
                            {
                                coffre.LootGenerators = null;
                            }
                            ChatUtil.SendSystemMessage(client, string.Format("Le générateur {0} a été supprimé de la liste du coffre {1}", args[3], coffre.InternalID));
                            break;

                        default:
                            DisplaySyntax(client);
                            break;
                    }
                    break;
                    #endregion
            }
        }
    }
    
    [CmdAttribute(
         "&coffre",
         ePrivLevel.GM,
         "Commands.GM.Coffre.Description",
         "Commands.GM.Coffre.Usage.Create",
        "Commands.GM.Coffre.Usage.CreateCatapult",
         "Commands.GM.Coffre.Usage.Model",
         "Commands.GM.Coffre.Usage.Item",
         "Commands.GM.Coffre.Usage.Add",
         "Commands.GM.Coffre.Usage.Remove",
         "Commands.GM.Coffre.Usage.Name",
         "Commands.GM.Coffre.Usage.Movehere",
         "Commands.GM.Coffre.Usage.Delete",
         "Commands.GM.Coffre.Usage.Reset",
         "Commands.GM.Coffre.Usage.Info",
         "Commands.GM.Coffre.Usage.Copy",
         "Commands.GM.Coffre.Usage.RandomCopy",
         "Commands.GM.Coffre.Usage.Key",
         "Commands.GM.Coffre.Usage.Difficult",
         "Commands.GM.Coffre.Usage.traprate",
         "Commands.GM.Coffre.Usage.NPCTemplate",
         "Commands.GM.Coffre.Usage.Respawn",
         "Commands.GM.Coffre.Usage.IsTeleport",
         "Commands.GM.Coffre.Usage.Teleporter",
         "Commands.GM.Coffre.Usage.TPrequirement",
         "Commands.GM.Coffre.Usage.TPEffect",
         "Commands.GM.Coffre.Usage.TPIsRenaissance",
         "Commands.GM.Coffre.Usage.IsOpeningRenaissance",
         "Commands.GM.Coffre.Usage.PunishSpellId",
         "Commands.GM.Coffre.Usage.PickableAnim",
         "Commands.GM.Coffre.Usage.InfoInterval",
         "Commands.GM.Coffre.Usage.LongDistance",
         "Commands.GM.Coffre.Usage.TPID",
         "Commands.GM.Coffre.Usage.ShouldRespawnToTPID",
         "Commands.GM.Coffre.Usage.CurrentStep",
         "Commands.GM.Coffre.Usage.PickOnTouch",
         "Commands.GM.Coffre.Usage.SecondaryModel",
         "Commands.GM.Coffre.Usage.IsOpenableOnce",
         "Commands.GM.Coffre.Usage.IsTerritoryLinked",
         "Commands.GM.Coffre.Usage.KeyLoseDur",
         "Commands.GM.Coffre.Usage.SwitchFamily",
         "Commands.GM.Coffre.Usage.SwitchOrder",
         "Commands.GM.Coffre.Usage.IsSwitch",
         "Commands.GM.Coffre.Usage.WrongOrderResetFamily",
         "Commands.GM.Coffre.Usage.ActivatedDuration",
         "Commands.GM.Coffre.Usage.ActivatedBySwitchOn",
         "Commands.GM.Coffre.Usage.ActivatedBySwitchOff",
         "Commands.GM.Coffre.Usage.ResetBySwitchOn",
         "Commands.GM.Coffre.Usage.ResetBySwitchOff",
         "Commands.GM.Coffre.Usage.SwitchOnSound",
         "Commands.GM.Coffre.Usage.WrongFamilyOrderSound",
         "Commands.GM.Coffre.Usage.ActivatedFamilySound",
         "Commands.GM.Coffre.Usage.DeactivatedFamilySound",
         "Commands.GM.Coffre.Usage.LootGenerator")]
    public class CoffreCommandHandler : CoffreCommandHandlerBase
    {
        
    }

    [CmdAttribute(
        "&chest",
        ePrivLevel.GM,
        "Commands.GM.Chest.Description",
        "Commands.GM.Chest.Usage.Create",
        "Commands.GM.Chest.Usage.CreateCatapult",
        "Commands.GM.Chest.Usage.Model",
        "Commands.GM.Chest.Usage.Item",
        "Commands.GM.Chest.Usage.Add",
        "Commands.GM.Chest.Usage.Remove",
        "Commands.GM.Chest.Usage.Name",
        "Commands.GM.Chest.Usage.Movehere",
        "Commands.GM.Chest.Usage.Delete",
        "Commands.GM.Chest.Usage.Reset",
        "Commands.GM.Chest.Usage.Info",
        "Commands.GM.Chest.Usage.Copy",
        "Commands.GM.Chest.Usage.RandomCopy",
        "Commands.GM.Chest.Usage.Key",
        "Commands.GM.Chest.Usage.Difficult",
        "Commands.GM.Chest.Usage.traprate",
        "Commands.GM.Chest.Usage.NPCTemplate",
        "Commands.GM.Chest.Usage.Respawn",
        "Commands.GM.Chest.Usage.IsTeleport",
        "Commands.GM.Chest.Usage.Teleporter",
        "Commands.GM.Chest.Usage.TPrequirement",
        "Commands.GM.Chest.Usage.TPEffect",
        "Commands.GM.Chest.Usage.TPIsRenaissance",
        "Commands.GM.Chest.Usage.IsOpeningRenaissance",
        "Commands.GM.Chest.Usage.PunishSpellId",
        "Commands.GM.Chest.Usage.PickableAnim",
        "Commands.GM.Chest.Usage.InfoInterval",
        "Commands.GM.Chest.Usage.LongDistance",
        "Commands.GM.Chest.Usage.TPID",
        "Commands.GM.Chest.Usage.ShouldRespawnToTPID",
        "Commands.GM.Chest.Usage.CurrentStep",
        "Commands.GM.Chest.Usage.PickOnTouch",
        "Commands.GM.Chest.Usage.SecondaryModel",
        "Commands.GM.Chest.Usage.IsOpenableOnce",
        "Commands.GM.Chest.Usage.IsTerritoryLinked",
        "Commands.GM.Chest.Usage.KeyLoseDur",
        "Commands.GM.Chest.Usage.SwitchFamily",
        "Commands.GM.Chest.Usage.SwitchOrder",
        "Commands.GM.Chest.Usage.IsSwitch",
        "Commands.GM.Chest.Usage.WrongOrderResetFamily",
        "Commands.GM.Chest.Usage.ActivatedDuration",
        "Commands.GM.Chest.Usage.ActivatedBySwitchOn",
        "Commands.GM.Chest.Usage.ActivatedBySwitchOff",
        "Commands.GM.Chest.Usage.ResetBySwitchOn",
        "Commands.GM.Chest.Usage.ResetBySwitchOff",
        "Commands.GM.Chest.Usage.SwitchOnSound",
        "Commands.GM.Chest.Usage.WrongFamilyOrderSound",
        "Commands.GM.Chest.Usage.ActivatedFamilySound",
        "Commands.GM.Chest.Usage.DeactivatedFamilySound",
        "Commands.GM.Chest.Usage.LootGenerator")]
    public class ChestCommandHandler : CoffreCommandHandlerBase
    {
    }
}