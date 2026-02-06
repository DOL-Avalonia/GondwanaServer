using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using System.Reflection;
using DOL.GameEvents;
using log4net;
using DOL.Language;


namespace DOL.GS.Scripts
{
    [CmdAttribute(
         "&textnpc",
         ePrivLevel.GM,
         "Commands.GM.TextNPC.Description",
         "Commands.GM.TextNPC.Usage.Create",
         "Commands.GM.TextNPC.Usage.CreateMerchant",
         "Commands.GM.TextNPC.Usage.CreateGuard",
         "Commands.GM.TextNPC.Usage.CreateItemMerchant",
         "Commands.GM.TextNPC.Usage.Response",

         //text
         "Commands.GM.TextNPC.Usage.Text",
         "Commands.GM.TextNPC.Usage.Add",
         "Commands.GM.TextNPC.Usage.Remove",

         //questtext
         "Commands.GM.TextNPC.Usage.Quest.Text",

         //emote
         "Commands.GM.TextNPC.Usage.Emote.Add",
         "Commands.GM.TextNPC.Usage.Emote.Remove",
         "Commands.GM.TextNPC.Usage.Emote.Help",

         //Spell
         "Commands.GM.TextNPC.Usage.Spell.Add",
         "Commands.GM.TextNPC.Usage.Spell.Remove",
         "Commands.GM.TextNPC.Usage.Spell.Help",
         "Commands.GM.TextNPC.Usage.Spell.Cast",

         //Give Item
         "Commands.GM.TextNPC.Usage.Give.Item.Add",
         "Commands.GM.TextNPC.Usage.Give.Item.Remove",

         //phrase cc general
         "Commands.GM.TextNPC.Usage.RandomPhrase.Add",
         "Commands.GM.TextNPC.Usage.RandomPhrase.Remove",
         "Commands.GM.TextNPC.Usage.RandomPhrase.interval",
         "Commands.GM.TextNPC.Usage.RandomPhrase.Help",
         "Commands.GM.TextNPC.Usage.RandomPhrase.View",

         //conditions
         "Commands.GM.TextNPC.Usage.IsOutlawfriendly",
         "Commands.GM.TextNPC.Usage.IsRegularfriendly",
         "Commands.GM.TextNPC.Usage.Startevent.Add",
         "Commands.GM.TextNPC.Usage.Startevent.Remove",
         "Commands.GM.TextNPC.Usage.Stopevent.Add",
         "Commands.GM.TextNPC.Usage.Stopevent.Remove",
         "Commands.GM.TextNPC.Usage.Responsetrigger.Add",
         "Commands.GM.TextNPC.Usage.Responsetrigger.Remove",
         "Commands.GM.TextNPC.Usage.Quest",
         "Commands.GM.TextNPC.Usage.Quest.Add",
         "Commands.GM.TextNPC.Usage.Quest.Remove",
         "Commands.GM.TextNPC.Usage.Level",
         "Commands.GM.TextNPC.Usage.Guild.Add",
         "Commands.GM.TextNPC.Usage.Guild.Remove",
         "Commands.GM.TextNPC.Usage.GuilAAdd",
         "Commands.GM.TextNPC.Usage.GuildARemove",
         "Commands.GM.TextNPC.Usage.RaceAdd",
         "Commands.GM.TextNPC.Usage.RaceRemove",
         "Commands.GM.TextNPC.Usage.RaceList",
         "Commands.GM.TextNPC.Usage.ClassAdd",
         "Commands.GM.TextNPC.Usage.ClassRemove",
         "Commands.GM.TextNPC.Usage.ClassList",
         "Commands.GM.TextNPC.Usage.Hour",
         "Commands.GM.TextNPC.Usage.Condition.List",
         "Commands.GM.TextNPC.Usage.Condition.Help",


         "Commands.GM.TextNPC.AdditionalDescription")]
    public class TextNPCCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private void RefreshIndicators(GameNPC npc)
        {
            if (npc == null) return;
            foreach (GamePlayer player in npc.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendNPCsQuestEffect(npc, npc.GetQuestIndicator(player));
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

            ITextNPC npc = player.TargetObject as ITextNPC;
            string text = "";
            string reponse = "";
            bool isRenaissance = false;
            IList<string> lines;
            TextNPCPolicy textnpc;
            switch (args[1].ToLower())
            {
                #region create - view - reponse - text - questtext
                case "create":
                case "createmerchant":
                case "createitemmerchant":
                case "createguard":
                    if (args[1].ToLower() == "create") npc = new TextNPC();
                    else if (args[1].ToLower() == "createmerchant") npc = new TextNPCMerchant();
                    else if (args[1].ToLower() == "createitemmerchant") npc = new TextNPCItemMerchant();
                    else if (args[1].ToLower() == "createguard") npc = new GuardTextNPC();

                    if (args.Length > 2)
                    {
                        bool.TryParse(args[2], out isRenaissance);
                    }

                    if (npc == null) npc = new TextNPC();
                    textnpc = npc.GetTextNPCPolicy(player);
                    ((GameNPC)npc).LoadedFromScript = false;
                    ((GameNPC)npc).Position = player.Position;
                    ((GameNPC)npc).Heading = player.Heading;
                    ((GameNPC)npc).CurrentRegion = player.CurrentRegion;
                    ((GameNPC)npc).Name = "Nouveau pnj";
                    ((GameNPC)npc).Realm = 0;
                    if (!((GameNPC)npc).IsPeaceful)
                        ((GameNPC)npc).Flags ^= GameNPC.eFlags.PEACE;
                    ((GameNPC)npc).Model = 40;
                    ((GameNPC)npc).IsRenaissance = isRenaissance;
                    textnpc.Interact_Text = "Texte à définir.";
                    ((GameNPC)npc).AddToWorld();
                    ((GameNPC)npc).SaveIntoDatabase();
                    break;

                case "reponse":
                    if (npc == null)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    textnpc = npc.GetTextNPCPolicy(player);
                    if (textnpc?.Reponses != null && textnpc.Reponses.Count > 0)
                    {
                        foreach (var de in textnpc.Reponses)
                        {
                            if (text.Length > 1)
                                text += "\n";
                            if (de.Value.Length > 20)
                                text += "[" + de.Key + "] Réponse: " + de.Value.Substring(0, 20).Trim('[', ']') + "...";
                            else
                                text += "[" + de.Key + "] Réponse: " + de.Value.Trim('[', ']') + "...";
                        }
                    }
                    else
                        text = "Ce pnj n'a aucune réponse de défini.";
                    player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
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
                    textnpc = npc.GetOrCreateTextNPCPolicy(player);
                    textnpc.Interact_Text = (text == "NO TEXT" ? "" : text);
                    textnpc.SaveIntoDatabase();
                    player.Out.SendMessage("Texte défini:\n" + text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;

                case "questtext":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    var QuestName = args[2];
                    text = string.Join(" ", args, 3, args.Length - 3);
                    text = text.Replace('|', '\n');
                    text = text.Replace(';', '\n');
                    if (text == "NO TEXT")
                        text = "";

                    textnpc = npc.GetOrCreateTextNPCPolicy(player);
                    textnpc.QuestTexts[QuestName] = text;
                    textnpc.SaveIntoDatabase();
                    player.Out.SendMessage("Texte défini:\n" + text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
                #endregion

                #region add - remove
                case "add":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    reponse = args[2];
                    string texte = string.Join(" ", args, 3, args.Length - 3);
                    texte = texte.Replace('|', '\n');
                    texte = texte.Replace(';', '\n');
                    textnpc = npc.GetOrCreateTextNPCPolicy(player);
                    if (textnpc.Reponses.ContainsKey(reponse))
                    {
                        textnpc.Reponses[reponse] = texte;
                        player.Out.SendMessage("Réponse \"" + reponse + "\" modifié avec le texte:\n" + texte, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        textnpc.Reponses.Add(reponse, texte);
                        player.Out.SendMessage("Réponse \"" + reponse + "\" ajouté avec le texte:\n" + texte, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    }
                    textnpc.SaveIntoDatabase();
                    break;

                case "remove":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    textnpc = npc.GetTextNPCPolicy(player);
                    if (textnpc != null && textnpc.Reponses != null && textnpc.Reponses.Count > 0)
                    {
                        if (textnpc.Reponses.ContainsKey(args[2]))
                        {
                            text = "La réponse \"" + args[2] + "\" a été supprimé dont le texte était:\n" + textnpc.Reponses[args[2]];
                            textnpc.Reponses.Remove(args[2]);
                            textnpc.SaveIntoDatabase();
                        }
                        else
                            text = "Ce pnj n'a pas de réponse \"" + args[2] + "\" défini.";
                    }
                    else
                        text = "Ce pnj n'a aucune réponse de défini.";
                    player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
                #endregion

                #region emote add/remove/help
                case "emote":
                    if (npc == null || args.Length < 2)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args.Length > 4)
                        reponse = string.Join(" ", args, 4, args.Length - 4);
                    if (args[2].ToLower() == "add")
                    {
                        if (args.Length < 5)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        try
                        {
                            textnpc = npc.GetOrCreateTextNPCPolicy(player);
                            if (textnpc.EmoteReponses.ContainsKey(reponse))
                            {
                                textnpc.EmoteReponses[reponse] = (eEmote)Enum.Parse(typeof(eEmote), args[3], true);
                                player.Out.SendMessage("Emote réponse \"" + reponse + "\" modifié", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                textnpc.EmoteReponses.Add(reponse, (eEmote)Enum.Parse(typeof(eEmote), args[3], true));
                                player.Out.SendMessage("Emote réponse \"" + reponse + "\" ajouté", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            textnpc.SaveIntoDatabase();
                        }
                        catch
                        {
                            player.Out.SendMessage("L'emote n'est pas valide.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                    }
                    else if (args[2].ToLower() == "remove")
                    {
                        if (args.Length < 3)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        textnpc = npc.GetTextNPCPolicy(player);
                        if (textnpc != null && textnpc.EmoteReponses.ContainsKey(reponse))
                        {
                            textnpc.EmoteReponses.Remove(reponse);
                            textnpc.SaveIntoDatabase();
                            player.Out.SendMessage("Emote réponse \"" + reponse + "\" supprimée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                    else if (args[2].ToLower() == "help")
                    {
                        lines = new List<string>();
                        lines.Add("Si la réponse est 'INTERACT' (sans les guillemets) alors l'emote sera faite lorsque le joueur parle au pnj (clic droit)");
                        lines.Add("Liste des emotes:");
                        foreach (string t in Enum.GetNames(typeof(eEmote)))
                            lines.Add(t);
                        player.Out.SendCustomTextWindow("Les emote réponses pour les nuls !", lines);
                    }
                    break;
                #endregion

                #region spell add/remove/help
                case "spell":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args.Length > 4)
                        reponse = string.Join(" ", args, 4, args.Length - 4);
                    if (args[2].ToLower() == "add")
                    {
                        if (args.Length < 5)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        try
                        {
                            textnpc = npc.GetOrCreateTextNPCPolicy(player);
                            if (textnpc.SpellReponses.ContainsKey(reponse))
                            {
                                textnpc.SpellReponses[reponse] = ushort.Parse(args[3]);
                                player.Out.SendMessage("Spell réponse \"" + reponse + "\" modifiée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                textnpc.SpellReponses.Add(reponse, ushort.Parse(args[3]));
                                player.Out.SendMessage("Spell réponse \"" + reponse + "\" ajoutée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            textnpc.SaveIntoDatabase();
                        }
                        catch (Exception e)
                        {
                            log.Debug("ERROR: ", e);
                            player.Out.SendMessage("Le spellid n'est pas valide.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                    }
                    else if (args[2].ToLower() == "remove")
                    {
                        if (args.Length < 4)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        textnpc = npc.GetTextNPCPolicy(player);
                        if (textnpc != null && textnpc.SpellReponses.ContainsKey(reponse))
                        {
                            textnpc.SpellReponses.Remove(reponse);
                            textnpc.SaveIntoDatabase();
                            player.Out.SendMessage("Spell réponse \"" + reponse + "\" supprimée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                            player.Out.SendMessage("Ce pnj n'a pas de spell réponse '" + reponse + "'.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else if (args[2].ToLower() == "help")
                    {
                        lines = new List<string>();
                        lines.Add("Si la réponse est 'INTERACT' (sans les guillemets) alors l'animation du spell sera faite lorsque le joueur parle au pnj (clic droit).");
                        player.Out.SendCustomTextWindow("Les spell réponses pour les nuls !", lines);
                    }
                    else if (args[2].ToLower() == "cast")
                    {
                        textnpc = npc.GetOrCreateTextNPCPolicy(player);
                        if (textnpc.SpellReponses.ContainsKey(reponse))
                        {
                            if (textnpc.SpellReponsesCast.ContainsKey(reponse))
                            {
                                textnpc.SpellReponsesCast[reponse] = bool.Parse(args[3]);
                                player.Out.SendMessage("Spell réponse \"" + reponse + "\" cast modified", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                textnpc.SpellReponsesCast.Add(reponse, bool.Parse(args[3]));
                                player.Out.SendMessage("Spell réponse \"" + reponse + "\" ajoutée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            textnpc.SaveIntoDatabase();
                        }
                    }
                    break;
                #endregion

                #region giveitem add/remove
                case "giveitem":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args.Length > 4)
                        reponse = string.Join(" ", args, 4, args.Length - 4);
                    if (args[2].ToLower() == "add")
                    {
                        if (args.Length < 5)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        try
                        {
                            textnpc = npc.GetOrCreateTextNPCPolicy(player);
                            if (textnpc.GiveItem.ContainsKey(reponse))
                            {
                                textnpc.GiveItem[reponse] = args[3];
                                player.Out.SendMessage("Giveitem réponse \"" + reponse + "\" modifiée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                textnpc.GiveItem.Add(reponse, args[3]);
                                player.Out.SendMessage("Giveitem réponse \"" + reponse + "\" ajoutée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            textnpc.SaveIntoDatabase();
                        }
                        catch (Exception e)
                        {
                            log.Debug("ERROR: ", e);
                            player.Out.SendMessage("Le itemtemplateid n'est pas valide.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                    }
                    else if (args[2].ToLower() == "remove")
                    {
                        if (args.Length < 4)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        textnpc = npc.GetTextNPCPolicy(player);
                        if (textnpc != null && textnpc.GiveItem.ContainsKey(reponse))
                        {
                            textnpc.GiveItem.Remove(reponse);
                            textnpc.SaveIntoDatabase();
                            player.Out.SendMessage("giveitem réponse \"" + reponse + "\" supprimée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                            player.Out.SendMessage("Ce pnj n'a pas de giveitem réponse '" + reponse + "'.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;
                #endregion

                #region randomphrase add/remove/interval/help/view
                case "randomphrase":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args[2].ToLower() == "add")
                    {
                        if (args.Length < 6 || (args[4].ToLower() != "say" && args[4].ToLower() != "yell" && args[4].ToLower() != "em"))
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        reponse = args[4].ToLower() + ":" + string.Join(" ", args, 5, args.Length - 5);
                        try
                        {
                            textnpc = npc.GetOrCreateTextNPCPolicy(player);
                            if (textnpc.RandomPhrases.ContainsKey(reponse))
                            {
                                if (args[3] != "0")
                                    textnpc.RandomPhrases[reponse] = (eEmote)Enum.Parse(typeof(eEmote), args[3], true);
                                else
                                    textnpc.RandomPhrases[reponse] = 0;
                                player.Out.SendMessage("L'emote de la phrase \"" + reponse + "\" a été modifié", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                textnpc.RandomPhrases.Add(reponse, (eEmote)Enum.Parse(typeof(eEmote), args[3], true));
                                player.Out.SendMessage("La phrase \"" + reponse + "\" a été ajouté", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            textnpc.SaveIntoDatabase();
                        }
                        catch
                        {
                            player.Out.SendMessage("L'emote n'est pas valide.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                    }

                    if (args[2].ToLower() == "remove")
                    {
                        if (args.Length < 4)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        text = string.Join(" ", args, 3, args.Length - 3);
                        textnpc = npc.GetTextNPCPolicy(player);
                        if (textnpc != null && textnpc.RandomPhrases.ContainsKey("say:" + text) ||
                            textnpc.RandomPhrases.ContainsKey("yell:" + text) ||
                            textnpc.RandomPhrases.ContainsKey("em:" + text))
                        {
                            if (textnpc.RandomPhrases.ContainsKey("say:" + text))
                                textnpc.RandomPhrases.Remove("say:" + text);
                            else if (textnpc.RandomPhrases.ContainsKey("em:" + text))
                                textnpc.RandomPhrases.Remove("yell:" + text);
                            else
                                textnpc.RandomPhrases.Remove("em:" + text);
                            textnpc.SaveIntoDatabase();
                            player.Out.SendMessage("Phrase \"" + text + "\" supprimée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                            player.Out.SendMessage("Ce pnj n'a pas de phrase '" + text + "'.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }

                    if (args[2].ToLower() == "interval")
                    {
                        if (args.Length < 4)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        textnpc = npc.GetOrCreateTextNPCPolicy(player);
                        try
                        {
                            textnpc.PhraseInterval = int.Parse(args[3]);
                            textnpc.SaveIntoDatabase();
                        }
                        catch
                        {
                            player.Out.SendMessage("L'interval n'est pas valide.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }

                    if (args[2].ToLower() == "view")
                    {
                        textnpc = npc.GetOrCreateTextNPCPolicy(player);
                        if (textnpc.RandomPhrases.Count < 1)
                        {
                            player.Out.SendMessage("Ce pnj n'a pas de phrase.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        lines = new List<string>();
                        lines.Add("Phrases que peut dire le pnj à un interval de " + textnpc.PhraseInterval + " secondes:");
                        foreach (var de in textnpc.RandomPhrases)
                            lines.Add(de.Key + " - Emote: " + de.Value);
                        player.Out.SendCustomTextWindow("Les phrases de " + ((GameNPC)npc).Name, lines);
                    }

                    if (args[2].ToLower() == "help")
                    {
                        lines = new List<string>();
                        lines.Add("Pour ajouter une phrase, utilisez '/textnpc randomphrase <emote> <say/yell> <phrase>'.");
                        lines.Add("emote: Si l'emote est '0' alors il n'y aura pas d'emote lorsque le pnj dira la phrase. (voir '/textnpc emote help' pour les emotes possibles').");
                        lines.Add("say/yell/em: C'est le type de phrase envoyé par le pnj, si c'est 'say' le pnj parlera sur le cc général, si c'est 'yell' le pnj parlera fort (rayon d'entente plus grand) sur le cc général, 'em' est utilisé pour les actions comme '/em <text>'.");
                        lines.Add("phrase: La phrase est choisite aléatoirement dans toutes les phrases disponibles.");
                        player.Out.SendCustomTextWindow("Les phrases aléatoire pour les nuls !", lines);
                    }
                    break;
                #endregion

                #region level/guild/race/class/prp/hour/karma
                case "isoutlawfriendly":
                    if (npc == null || args.Length < 2)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    textnpc = npc.GetOrCreateTextNPCPolicy(player);
                    textnpc.IsOutlawFriendly = args[2].ToLower() == "true";
                    textnpc.SaveIntoDatabase();
                    break;
                case "isregularfriendly":
                    if (npc == null || args.Length < 2)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    textnpc = npc.GetOrCreateTextNPCPolicy(player);
                    textnpc.IsOutlawFriendly = args[2].ToLower() == "false";
                    textnpc.SaveIntoDatabase();
                    break;
                case "responsetrigger":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args[2].ToLower() == "add")
                    {
                    }
                    if (args[2].ToLower() == "remove")
                    {
                    }
                    textnpc = npc.GetOrCreateTextNPCPolicy(player);
                    textnpc.SaveIntoDatabase();
                    break;
                case "quest":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args.Length > 4)
                        reponse = string.Join(" ", args, 4, args.Length - 4);
                    if (args[2].ToLower() == "add")
                    {

                        if (args.Length < 5)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        textnpc = npc.GetOrCreateTextNPCPolicy(player);
                        try
                        {
                            if (textnpc.QuestReponses.ContainsKey(reponse))
                            {
                                textnpc.QuestReponses[reponse] = args[3];
                                player.Out.SendMessage("Quest réponse \"" + reponse + "\" modifiée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                textnpc.QuestReponses.Add(reponse, args[3]);
                                player.Out.SendMessage("Quest réponse \"" + reponse + "\" ajoutée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            textnpc.SaveIntoDatabase();
                            RefreshIndicators((GameNPC)npc);

                            var values = args[3].Split('-');
                            if (textnpc.QuestReponsesValues.ContainsKey(reponse))
                                textnpc.QuestReponsesValues.Remove(reponse);
                            if (values.Length < 2)
                                textnpc.QuestReponsesValues.Add(reponse, new Tuple<string, int>(values[0], 0));
                            else
                                textnpc.QuestReponsesValues.Add(reponse, new Tuple<string, int>(values[0], int.Parse(values[1])));
                        }
                        catch (Exception e)
                        {
                            log.Debug("ERROR: ", e);
                            player.Out.SendMessage("Le Quest n'est pas valide.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                    }
                    else if (args[2].ToLower() == "remove")
                    {
                        if (args.Length < 4)
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        textnpc = npc.GetTextNPCPolicy(player);
                        if (textnpc != null && textnpc.QuestReponses.ContainsKey(reponse))
                        {
                            textnpc.QuestReponses.Remove(reponse);
                            if (textnpc.QuestReponsesValues.ContainsKey(reponse))
                                textnpc.QuestReponsesValues.Remove(reponse);
                            textnpc.SaveIntoDatabase();
                            player.Out.SendMessage("Quest réponse \"" + reponse + "\" supprimée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                            player.Out.SendMessage("Ce pnj n'a pas de quest réponse '" + reponse + "'.", eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        RefreshIndicators((GameNPC)npc);
                    }
                    else
                    {
                        eQuestIndicator indicator;
                        if (args.Length < 3 || !Enum.TryParse(args[2], out indicator))
                        {
                            DisplaySyntax(client);
                            return;
                        }
                        textnpc = npc.GetOrCreateTextNPCPolicy(player);
                        textnpc.Condition.CanGiveQuest = indicator;
                        textnpc.SaveIntoDatabase();
                        RefreshIndicators((GameNPC)npc);
                        player.Out.SendMessage($"Indicator set to {indicator}. Visuals refreshed.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "level":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    textnpc = npc.GetOrCreateTextNPCPolicy(player);
                    try
                    {
                        int min = int.Parse(args[2]);
                        int max = int.Parse(args[3]);
                        if (min < 1)
                            min = 1;
                        else if (min > 50)
                            max = 50;
                        if (max < min)
                            max = min;
                        if (max > 50)
                            max = 50;
                        textnpc.Condition.Level_min = min;
                        textnpc.Condition.Level_max = max;
                        textnpc.SaveIntoDatabase();
                    }
                    catch
                    {
                        player.Out.SendMessage("Le level max ou min n'est pas valide.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    player.Out.SendMessage(
                        "Le niveau est maintenant de " + textnpc.Condition.Level_min + " minimum et " + textnpc.Condition.Level_max + " maximum.",
                        eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    RefreshIndicators((GameNPC)npc);
                    break;

                case "guild":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args[2].ToLower() == "add")
                    {
                        if (!GuildMgr.DoesGuildExist(args[3]) && args[3] != "NO GUILD")
                        {
                            player.Out.SendMessage("La guilde \"" + args[3] + "\" n'éxiste pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;
                        }
                        textnpc = npc.GetOrCreateTextNPCPolicy(player);
                        textnpc.Condition.GuildNames.Add(args[3]);
                        textnpc.SaveIntoDatabase();
                        player.Out.SendMessage("La guilde " + args[3] + " a été ajouté aux guildes interdites.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else if (args[2].ToLower() == "remove")
                    {
                        textnpc = npc.GetTextNPCPolicy(player);
                        if (textnpc == null || textnpc.Condition.GuildNames.Count < 1 || !textnpc.Condition.GuildNames.Contains(args[3]))
                        {
                            player.Out.SendMessage("Ce pnj n'a pas d'interdiction sur la guilde \"" + args[3] + "\".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;
                        }
                        textnpc.Condition.GuildNames.Remove(args[3]);
                        textnpc.SaveIntoDatabase();
                        player.Out.SendMessage("La guilde " + args[3] + " a été retirée des guildes interdites.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                        DisplaySyntax(client);

                    RefreshIndicators((GameNPC)npc);
                    break;

                case "guilda":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args[2].ToLower() == "add")
                    {
                        if (!GuildMgr.DoesGuildExist(args[3]) && (args[3] != "NO GUILD" || args[3] != "ALL"))
                        {
                            player.Out.SendMessage("La guilde \"" + args[3] + "\" n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;
                        }
                        textnpc = npc.GetOrCreateTextNPCPolicy(player);
                        if (textnpc.Condition.GuildNamesA.Contains(args[3]))
                        {
                            DisplayMessage(client, "La guilde \"{0}\" a déjà été ajouté.", args[3]);
                            return;
                        }
                        textnpc.Condition.GuildNamesA.Add(args[3]);
                        textnpc.SaveIntoDatabase();
                        player.Out.SendMessage("La guilde " + args[3] + " a été ajouté aux guildes autorisées.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else if (args[2].ToLower() == "remove")
                    {
                        textnpc = npc.GetTextNPCPolicy(player);
                        if (textnpc == null || textnpc.Condition.GuildNamesA.Count < 1 || !textnpc.Condition.GuildNamesA.Contains(args[3]))
                        {
                            player.Out.SendMessage("Ce pnj n'a pas d'autorisation sur la guilde \"" + args[3] + "\".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;
                        }
                        textnpc.Condition.GuildNamesA.Remove(args[3]);
                        textnpc.SaveIntoDatabase();
                        player.Out.SendMessage("La guilde " + args[3] + " a été retirée des guildes autorisées.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                        DisplaySyntax(client);

                    RefreshIndicators((GameNPC)npc);
                    break;

                case "race":
                    if (npc == null || args.Length < 3 || (args.Length < 4 && args[2].ToLower() != "list"))
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args[2].ToLower() == "add")
                    {
                        if (!_RaceNameExist(args[3]))
                        {
                            player.Out.SendMessage("La race \"" + args[3] + "\" n'éxiste pas, voir '/textnpc race list'", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;
                        }
                        textnpc = npc.GetOrCreateTextNPCPolicy(player);
                        textnpc.Condition.Races.Add(args[3].ToLower());
                        textnpc.SaveIntoDatabase();
                        player.Out.SendMessage("La race " + args[3] + " a été ajouté aux races interdites.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else if (args[2].ToLower() == "remove")
                    {
                        textnpc = npc.GetTextNPCPolicy(player);
                        if (textnpc == null || textnpc.Condition.Races.Count < 1 || !textnpc.Condition.Races.Contains(args[3]))
                        {
                            player.Out.SendMessage("Ce pnj n'a pas d'interdiction sur la race \"" + args[3] + "\".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;
                        }
                        textnpc.Condition.Races.Remove(args[3].ToLower());
                        textnpc.SaveIntoDatabase();
                        player.Out.SendMessage("La race " + args[3] + " a été retirée des races interdites.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else if (args[2].ToLower() == "list")
                    {
                        lines = new List<string>();
                        lines.Add("Liste des races existantes:");
                        //TODO: races
                        //foreach(string race in GamePlayer.RACENAMES)
                        //lines.Add(race);
                        player.Out.SendCustomTextWindow("Les races pour les nuls !", lines);
                    }
                    else
                        DisplaySyntax(client);

                    RefreshIndicators((GameNPC)npc);
                    break;

                case "class":
                    if (npc == null || args.Length < 3 || (args.Length < 4 && args[2].ToLower() != "list"))
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (args[2].ToLower() == "add")
                    {
                        if (!_ClassNameExist(args[3]))
                        {
                            player.Out.SendMessage("La classe \"" + args[3] + "\" n'éxiste pas, voir '/textnpc class list'", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;
                        }
                        textnpc = npc.GetOrCreateTextNPCPolicy(player);
                        textnpc.Condition.Classes.Add(args[3].ToLower());
                        textnpc.SaveIntoDatabase();
                        player.Out.SendMessage("La classe " + args[3] + " a été ajouté aux classes interdites.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else if (args[2].ToLower() == "remove")
                    {
                        textnpc = npc.GetTextNPCPolicy(player);
                        if (textnpc == null || textnpc.Condition.Classes.Count < 1 || !textnpc.Condition.Classes.Contains(args[3]))
                        {
                            player.Out.SendMessage("Ce pnj n'a pas d'interdiction sur la classe \"" + args[3] + "\".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;
                        }
                        textnpc.Condition.Classes.Remove(args[3].ToLower());
                        textnpc.SaveIntoDatabase();
                        player.Out.SendMessage("La classe " + args[3] + " a été retirée des classes interdites.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else if (args[2].ToLower() == "list")
                    {
                        lines = new List<string>();
                        lines.Add("Liste des classes existantes:");
                        foreach (string classe in Enum.GetNames(typeof(eCharacterClass)))
                            lines.Add(classe);
                        player.Out.SendCustomTextWindow("Les classes pour les nuls !", lines);
                    }
                    else
                        DisplaySyntax(client);

                    RefreshIndicators((GameNPC)npc);
                    break;

                case "hour":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    textnpc = npc.GetOrCreateTextNPCPolicy(player);
                    try
                    {
                        int min = int.Parse(args[2]);
                        int max = int.Parse(args[3]);
                        textnpc.Condition.Heure_min = min;
                        textnpc.Condition.Heure_max = max;
                        textnpc.SaveIntoDatabase();
                    }
                    catch
                    {
                        player.Out.SendMessage("L'heure max ou min n'est pas valide.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    RefreshIndicators((GameNPC)npc);
                    player.Out.SendMessage("L'heure est maintenant comprise entre " + textnpc.Condition.Heure_min + "h et " + textnpc.Condition.Heure_max + "h.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;
                #endregion

                #region condition list/help
                case "condition":
                    if (args.Length < 3)
                    {
                        DisplaySyntax(client);
                        break;
                    }

                    textnpc = npc?.GetTextNPCPolicy(player);
                    if (args[2].ToLower() == "list")
                    {
                        if (textnpc == null)
                        {
                            lines = new List<string>() { "Ce PNJ n'a pas de texte" };
                        }
                        else
                        {
                            lines = new List<string>
                                {
                                    "Conditions du pnj " + ((GameNPC) npc).Name + ":",
                                    "+ Heure      min: " + textnpc.Condition.Heure_min + " max:" +
                                    textnpc.Condition.Heure_max
                                };
                            if (textnpc.Condition.Level_min != 1 || textnpc.Condition.Level_max != 50)
                                lines.Add("+ Level      min: " + textnpc.Condition.Level_min + " max: " + textnpc.Condition.Level_max);
                            if (textnpc.Condition.GuildNames != null && textnpc.Condition.GuildNames.Count > 0)
                            {
                                lines.Add("+ Guildes interdites:");
                                foreach (string guild in textnpc.Condition.GuildNames)
                                    lines.Add("   " + guild);
                            }
                            if (textnpc.Condition.GuildNames != null && textnpc.Condition.GuildNames.Count > 0)
                            {
                                lines.Add("+ Guildes autorisées:");
                                foreach (string guild in textnpc.Condition.GuildNamesA)
                                    lines.Add("   " + guild);
                            }
                            if (textnpc.Condition.Races != null && textnpc.Condition.Races.Count > 0)
                            {
                                lines.Add("+ Races interdites:");
                                foreach (string race in textnpc.Condition.Races)
                                    lines.Add("   " + race);
                            }
                            if (textnpc.Condition.Classes != null && textnpc.Condition.Classes.Count > 0)
                            {
                                lines.Add("+ Classes interdites:");
                                foreach (string classe in textnpc.Condition.Classes)
                                    lines.Add("   " + classe);
                            }
                            if (textnpc.Condition.CanGiveQuest != eQuestIndicator.None)
                                lines.Add($"+ Quêtes: {textnpc.Condition.CanGiveQuest}");
                        }
                        player.Out.SendCustomTextWindow("Conditions de " + ((GameNPC)npc).Name, lines);
                    }
                    else if (args[2].ToLower() == "help")
                    {
                        lines = new List<string>
                                {
                                    "Type de conditions:",
                                    "+ Level: on règle le niveau minimum et maximum des personnages auquels le pnj parlera. Par exemple, si l'on met 15 en minimum et 50 en maximum, le pnj parlera aux personnages du niveau 15 au niveau 49.",
                                    "+ Guilde: on ajoute les guildes auquelles le pnj ne parlera pas, donc si l'on met par exemple la guilde 'Legion Noire', le pnj ne parlera pas aux membre de la Legion Noire. Pour que le pnj ne parle pas au non guildé, il faut ajouter la guilde 'NO GUILD'.",
                                    "+ Race/Classe: on ajoute les races ou classes auquelles le pnj ne parlera pas, donc si on ajoute 'Troll', le pnj ne parlera pas aux trolls. (Voir '/textnpc race list' et '/textnpc class list' pour voir les races/classes possible).",
                                    "+ Les heures: on règle la tranche d'heure du jeu pendant laquelle parle le pnj. Pour mettre une tranche d'heure de nuit par exemple de 22h à 5h. Il faut mettre 22 en minimum et 5 en maximum, le pnj parlera de 22h00 à 4h59 (heure du jeu). (Cette condition fonctionne aussi pour les phrases/emotes aléatoires",
                                    "+ Quêtes: permet juste d'afficher ou non l'icone de quêtes."
                                };
                        player.Out.SendCustomTextWindow("Les conditions pour les nuls !", lines);
                    }
                    else
                        DisplaySyntax(client);
                    break;
                #endregion

                #region events
                case "startevent":
                    if (npc == null)
                    {
                        player.Out.SendMessage("Vous devez sélectionner un PNJ!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }
                    switch (args[2].ToLower())
                    {
                        case "list":
                            lines = new List<String>();
                            textnpc = npc.GetTextNPCPolicy(player);
                            if (textnpc != null)
                            {
                                foreach (var startevent in textnpc.StartEventResponses)
                                {
                                    GameEvent ev = GameEventManager.Instance.GetEventByID(startevent.Value);
                                    lines.Add("[" + startevent.Key + "] => " + (ev?.EventName ?? "UNKNOWN") + " (" + startevent.Value + ")");
                                }
                            }
                            player.Out.SendCustomTextWindow("StartEvent réponses:", lines);
                            break;

                        case "add":
                            if (args.Length < 5)
                            {
                                player.Out.SendMessage("Syntaxe: /textnpc startevent add <eventId> <text>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            }
                            reponse = string.Join(" ", args, 4, args.Length - 4);
                            GameEvent gameEvent = GameEventManager.Instance.GetEventByID(args[3]);
                            if (gameEvent == null)
                            {
                                player.Out.SendMessage("L'évènement avec ID `" + args[3] + "` n'a pas pu être trouvé", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            }
                            textnpc = npc.GetOrCreateTextNPCPolicy(player);
                            bool modified = textnpc.StartEventResponses.ContainsKey(reponse);
                            textnpc.StartEventResponses[reponse] = args[3];
                            textnpc.SaveIntoDatabase();
                            player.Out.SendMessage("StartEvent réponse \"" + reponse + (modified ? "\" modifiée" : "\" ajoutée") + " : " + gameEvent.EventName, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;

                        case "remove":
                            if (args.Length < 4)
                            {
                                player.Out.SendMessage("Syntaxe: /textnpc startevent remove <text>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            }
                            reponse = string.Join(" ", args, 3, args.Length - 3);
                            textnpc = npc.GetTextNPCPolicy(player);
                            if (textnpc != null && textnpc.StartEventResponses.Remove(reponse))
                            {
                                textnpc.SaveIntoDatabase();
                                player.Out.SendMessage("StartEvent réponse \"" + reponse + "\" supprimée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                player.Out.SendMessage("Aucune réponse StartEvent \"" + reponse + "\" n'a été trouvée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            break;
                    }

                    break;

                case "stopevent":
                    if (npc == null)
                    {
                        player.Out.SendMessage("Vous devez sélectionner un PNJ!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        break;
                    }
                    switch (args[2].ToLower())
                    {
                        case "list":
                            lines = new List<String>();
                            textnpc = npc.GetTextNPCPolicy(player);
                            if (textnpc != null)
                            {
                                foreach (var startevent in textnpc.StartEventResponses)
                                {
                                    GameEvent ev = GameEventManager.Instance.GetEventByID(startevent.Value);
                                    lines.Add("[" + startevent.Key + "] => " + (ev?.EventName ?? "UNKNOWN") + " (" + startevent.Value + ")");
                                }
                            }
                            player.Out.SendCustomTextWindow("StopEvent réponses:", lines);
                            break;

                        case "add":
                            if (args.Length < 5)
                            {
                                player.Out.SendMessage("Syntaxe: /textnpc stopevent add <eventId> <text>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            }
                            reponse = string.Join(" ", args, 4, args.Length - 4);
                            GameEvent? gameEvent = GameEventManager.Instance.GetEventByID(args[3]);
                            if (gameEvent == null)
                            {
                                player.Out.SendMessage("L'évènement avec ID `" + args[3] + "` n'a pas pu être trouvé", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            }

                            textnpc = npc.GetOrCreateTextNPCPolicy(player);
                            bool modified = textnpc.StopEventResponses.ContainsKey(reponse);
                            textnpc.StopEventResponses[reponse] = args[3];
                            textnpc.SaveIntoDatabase();
                            player.Out.SendMessage("StopEvent réponse \"" + reponse + (modified ? "\" modifiée" : "\" ajoutée") + " : " + gameEvent.EventName, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            break;


                        case "remove":
                            if (args.Length < 4)
                            {
                                player.Out.SendMessage("Syntaxe: /textnpc stopevent remove <text>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                break;
                            }
                            reponse = string.Join(" ", args, 3, args.Length - 3);
                            textnpc = npc.GetTextNPCPolicy(player);
                            if (textnpc != null && textnpc.StopEventResponses.Remove(reponse))
                            {
                                textnpc.SaveIntoDatabase();
                                player.Out.SendMessage("StopEvent réponse \"" + reponse + "\" supprimée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                player.Out.SendMessage("Aucune réponse StopEvent \"" + reponse + "\" n'a été trouvée", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            break;
                    }

                    break;
                #endregion

                default:
                    DisplaySyntax(client);
                    break;
            }
            return;
        }

        //TODO: need a new Array
        /*
		private bool RaceNameExist(string name)
		{
			foreach(string race in GamePlayer.RACENAMES())
				if(race.ToLower() == name.ToLower())
					return true;
			return false;
		}
		*/
        private static bool _RaceNameExist(string name)
        {
            return true;
        }

        private static bool _ClassNameExist(string name)
        {
            foreach (string classe in Enum.GetNames(typeof(eCharacterClass)))
                if (classe.ToLower() == name.ToLower())
                    return true;
            return false;
        }
    }
}
