using System;
using System.Collections.Generic;
using DOL.Database;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Scripts
{
    [CmdAttribute(
        "&echangeurnpc",
        ePrivLevel.GM,
        "Commands.GM.EchangeurNPC.Description",
        "Commands.GM.EchangeurNPC.Usage.Add",
        "Commands.GM.EchangeurNPC.Usage.Remove",
        "Commands.GM.EchangeurNPC.Usage.Money",
        "Commands.GM.EchangeurNPC.Usage.XP",
        "Commands.GM.EchangeurNPC.Usage.Item",
        "Commands.GM.EchangeurNPC.Usage.Quest",
        "Commands.GM.EchangeurNPC.Usage.Info",
        "Commands.GM.EchangeurNPC.Usage.Pricemoney",
        "Commands.GM.EchangeurNPC.Usage.Priceressource1",
        "Commands.GM.EchangeurNPC.Usage.Priceressource2",
        "Commands.GM.EchangeurNPC.Usage.Priceressource3",
        "Commands.GM.EchangeurNPC.Usage.Priceressource",
        "Commands.GM.EchangeurNPC.Usage.UniqueStonemaker",
        "Commands.GM.EchangeurNPC.Usage.UniqueVialPlayer",
        "Commands.GM.EchangeurNPC.Usage.UniqueVialNpcAll",
        "Commands.GM.EchangeurNPC.Usage.UniqueVialNpcName",
        "Commands.GM.EchangeurNPC.Usage.UniqueVialNpcBlood",
        "Commands.GM.EchangeurNPC.Usage.AdditionalDescription")]
    public class EchangeurNPCCommandHandler : AbstractCommandHandler, ICommandHandler
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

            ITextNPC npc = player.TargetObject as ITextNPC;
            string item;
            switch (args[1].ToLower())
            {
                case "add":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    int amount;
                    if (!int.TryParse(args[2], out amount) || amount <= 0)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    item = string.Join(" ", args, 3, args.Length - 3);
                    if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(item))
                    {
                        npc.GetTextNPCPolicy(player).EchangeurDB[item] = new DBEchangeur
                        {
                            ItemRecvCount = amount,
                            ItemRecvID = item,
                            ItemGiveCount = 0,
                            ItemGiveID = "",
                            GainMoney = 0,
                            GainXP = 0
                        };
                        npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                        player.Out.SendMessage(item + " a été ajouté.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    else
                        player.Out.SendMessage(item + " existe déjà.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    break;

                case "remove":
                    if (npc == null || args.Length < 3)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    item = string.Join(" ", args, 2, args.Length - 2);
                    if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(item))
                        player.Out.SendMessage(item + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    else
                    {
                        DBEchangeur db = npc.GetTextNPCPolicy(player).EchangeurDB[item];
                        npc.GetTextNPCPolicy(player).EchangeurDB.Remove(item);
                        GameServer.Database.DeleteObject(db);
                        if (npc.GetTextNPCPolicy(player).Reponses.ContainsKey(item))
                            npc.GetTextNPCPolicy(player).Reponses.Remove(item);
                        npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                        player.Out.SendMessage(item + " a été retiré.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "money":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    item = args[2];
                    long money;
                    if (!long.TryParse(args[3], out money) || money < 0)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(item))
                        player.Out.SendMessage(item + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    else
                    {
                        npc.GetTextNPCPolicy(player).EchangeurDB[item].GainMoney = money;
                        npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                        player.Out.SendMessage(item + " donne " + Money.GetString(money, player.Client.Account.Language) + " maintenant.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "xp":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    item = args[2];
                    int xp;
                    if (!int.TryParse(args[3], out xp))
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(item))
                        player.Out.SendMessage(item + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    else
                    {
                        npc.GetTextNPCPolicy(player).EchangeurDB[item].GainXP = xp;
                        npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                        player.Out.SendMessage(item + " donne " + (xp > 0 ? xp + " xp" : (-xp) + "/1000 du niveau en cours") + " maintenant.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "item":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    item = args[2];
                    string item2 = args[4];
                    int count;
                    if (!int.TryParse(args[3], out count) || count < 0)
                    {
                        DisplaySyntax(client);
                        return;
                    }

                    if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(item))
                        player.Out.SendMessage(item + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    else
                    {
                        npc.GetTextNPCPolicy(player).EchangeurDB[item].ItemGiveCount = count;
                        npc.GetTextNPCPolicy(player).EchangeurDB[item].ItemGiveID = item2;
                        npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                        player.Out.SendMessage(item + " donne " + count + " " + item2 + " maintenant.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }
                    break;

                case "unique":
                    if (npc == null || args.Length < 3)
                    {
                        player.Out.SendMessage("Syntax: /echangeurnpc unique vial <player | npc> <all | name <name> | blood <blood> | stonemaker>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }
                    
                    string category = args[2].ToLower();
                    string dbKey = "";

                    if (category == "stonemaker")
                    {
                        dbKey = "STONEMAKER:ALL";
                    }
                    else if (category == "vial")
                    {
                        if (args.Length < 4) return;
                        string vialType = args[3].ToLower();
                        if (vialType == "player") 
                        {
                            dbKey = "PLAYERVIAL:ALL";
                        }
                        else if (vialType == "npc")
                        {
                            if (args.Length < 5)
                            {
                                player.Out.SendMessage("Syntax: /echangeurnpc unique vial npc <all | name <name> | blood <blood>>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }
                            
                            string subType = args[4].ToLower();
                            if (subType == "all") 
                                dbKey = "NPCVIAL:ALL";
                            else if (subType == "name" && args.Length >= 6) 
                                dbKey = "NPCVIAL:NAME:" + string.Join("", args, 5, args.Length - 5).Replace(" ", "");
                            else if (subType == "blood" && args.Length >= 6) 
                                dbKey = "NPCVIAL:BLOOD:" + string.Join("", args, 5, args.Length - 5).Replace(" ", ""); 
                            else 
                            {
                                player.Out.SendMessage("Provide mob name or blood type. Example: /echangeurnpc unique vial npc blood MachineOil", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }
                        }
                        else 
                        {
                            player.Out.SendMessage("Syntax: /echangeurnpc unique vial <player | npc> <all | name <name> | blood <blood>>", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                    }
                    else
                    {
                        player.Out.SendMessage("Unknown unique category. Use 'stonemaker' or 'vial'.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    if (!string.IsNullOrEmpty(dbKey))
                    {
                        if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(dbKey))
                        {
                            npc.GetTextNPCPolicy(player).EchangeurDB[dbKey] = new DBEchangeur
                            {
                                ItemRecvCount = 1,
                                ItemRecvID = dbKey,
                                ItemGiveCount = 0,
                                ItemGiveID = "",
                                GainMoney = 0,
                                GainXP = 0
                            };
                            npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                            player.Out.SendMessage($"Unique Item Configuration '{dbKey}' has been added to {((GameNPC)npc).Name}.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            player.Out.SendMessage($"Unique Item Configuration '{dbKey}' already exists on this NPC.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                    break;

                case "quest":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    item = args[2];
                    string questName = args[3];
                    int step;
                    if (!int.TryParse(args[4], out step) || step < 0)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(item))
                        player.Out.SendMessage(item + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    else
                    {
                        npc.GetTextNPCPolicy(player).EchangeurDB[item].QuestName = questName;
                        npc.GetTextNPCPolicy(player).EchangeurDB[item].Step = step;
                        npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                        player.Out.SendMessage(item + " donne la quête " + questName + " étape " + step + " maintenant.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    }


                    break;

                case "info":
                    if (npc == null)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    List<string> text = new List<string>();
                    foreach (KeyValuePair<string, DBEchangeur> pair in npc.GetTextNPCPolicy(player).EchangeurDB)
                    {
                        text.Add(" - " + pair.Value.ItemRecvID + " (" + pair.Value.ItemRecvCount + "):");
                        if (pair.Value.ItemGiveCount > 0)
                            text.Add("     " + pair.Value.ItemGiveCount + " " + pair.Value.ItemGiveID);
                        if (pair.Value.GainMoney > 0)
                            text.Add("     " + Money.GetString(pair.Value.GainMoney, player.Client.Account.Language));
                        if (pair.Value.GainXP > 0)
                            text.Add("     " + pair.Value.GainXP + "xp");
                        if (pair.Value.GainXP < 0)
                            text.Add("     " + (-pair.Value.GainXP) + "/1000 du niveau en cours");
                        if (npc.GetTextNPCPolicy(player).Reponses.ContainsKey(pair.Value.ItemRecvID))
                            text.Add("     Réponse: " + npc.GetTextNPCPolicy(player).Reponses[pair.Value.ItemRecvID]);
                        text.Add(" . " + pair.Value.ChangedItemCount + " Items échangés");

                        text.Add(" PriceMoney: " + pair.Value.MoneyPrice + " or");
                        if (!string.IsNullOrEmpty(pair.Value.PriceRessource1))
                        {
                            var val1 = pair.Value.PriceRessource1.Split(new char[] { '|' });
                            if (val1.Length == 2)
                                text.Add(" PriceRessource1:  " + val1[1] + " " + val1[0]);
                        }
                        if (!string.IsNullOrEmpty(pair.Value.PriceRessource2))
                        {
                            var val2 = pair.Value.PriceRessource2.Split(new char[] { '|' });
                            if (val2.Length == 2)
                                text.Add(" PriceRessource2:  " + val2[1] + " " + val2[0]);
                        }
                        if (!string.IsNullOrEmpty(pair.Value.PriceRessource3))
                        {
                            var val3 = pair.Value.PriceRessource3.Split(new char[] { '|' });
                            if (val3.Length == 2)
                                text.Add(" PriceRessource3:  " + val3[1] + " " + val3[0]);
                        }
                        if (!string.IsNullOrEmpty(pair.Value.QuestName))
                        {
                            text.Add(" Quest: " + pair.Value.QuestName + " step " + pair.Value.Step);
                        }

                    }
                    player.Out.SendCustomTextWindow("Info " + ((GameNPC)npc).Name, text);
                    break;

                case "pricemoney":
                    if (npc == null || args.Length < 4)
                    {
                        DisplaySyntax(client);
                        return;
                    }
                    item = args[2];
                    int gold = 0;

                    if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(item))
                        player.Out.SendMessage(item + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    else
                    {
                        if (int.TryParse(args[3], out gold))
                        {
                            npc.GetTextNPCPolicy(player).EchangeurDB[item].MoneyPrice = gold;
                            npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                            player.Out.SendMessage("il faut désormais " + gold + " or(s) pour échanger " + item + " maintenant.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            DisplaySyntax(client);
                        }
                    }

                    break;


                case "priceressource1":
                    this.ChangeRessource(1, npc, args, client);
                    break;
                case "priceressource2":
                    this.ChangeRessource(2, npc, args, client);
                    break;
                case "priceressource3":
                    this.ChangeRessource(3, npc, args, client);
                    break;


                case "priceressource":
                    if (args.Length != 5 || npc == null)
                    {
                        DisplaySyntax(client);
                        break;
                    }

                    if (args[2] != "remove")
                    {
                        DisplaySyntax(client);
                        break;
                    }

                    int num = 0;
                    if (int.TryParse(args[3], out num))
                    {
                        if (num == 1 || num == 2 || num == 3)
                        {
                            item = args[4];

                            if (!string.IsNullOrEmpty(item))
                            {
                                if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(item))
                                    player.Out.SendMessage(item + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                else
                                {
                                    if (num == 1)
                                    {
                                        npc.GetTextNPCPolicy(player).EchangeurDB[item].PriceRessource1 = null;
                                    }
                                    else if (num == 2)
                                    {
                                        npc.GetTextNPCPolicy(player).EchangeurDB[item].PriceRessource2 = null;
                                    }
                                    else
                                    {
                                        npc.GetTextNPCPolicy(player).EchangeurDB[item].PriceRessource3 = null;
                                    }

                                    npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                                    player.Out.SendMessage("La ressource " + num + " a été supprimée pour l'objet " + item, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    break;
                                }
                            }
                        }
                    }

                    DisplaySyntax(client);
                    break;


                default:
                    DisplaySyntax(client);
                    break;
            }
            return;
        }

        private void ChangeRessource(int ressource, ITextNPC npc, string[] args, GameClient client)
        {
            if (npc == null || args.Length < 5)
            {
                DisplaySyntax(client);
                return;
            }

            var player = client.Player;
            string item = args[2];
            string exchangeItem = args[3];
            int itemCount = 0;

            if (string.IsNullOrEmpty(exchangeItem))
            {
                DisplaySyntax(client);
                return;
            }

            if (!npc.GetTextNPCPolicy(player).EchangeurDB.ContainsKey(item))
                client.Out.SendMessage(item + " n'existe pas.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            else
            {
                if (int.TryParse(args[4], out itemCount))
                {
                    if (ressource == 1)
                    {
                        npc.GetTextNPCPolicy(player).EchangeurDB[item].PriceRessource1 = string.Format("{0}|{1}", exchangeItem, itemCount);
                    }
                    else if (ressource == 2)
                    {
                        npc.GetTextNPCPolicy(player).EchangeurDB[item].PriceRessource2 = string.Format("{0}|{1}", exchangeItem, itemCount);
                    }
                    else
                    {
                        npc.GetTextNPCPolicy(player).EchangeurDB[item].PriceRessource3 = string.Format("{0}|{1}", exchangeItem, itemCount);
                    }

                    npc.GetTextNPCPolicy(player).SaveIntoDatabase();
                    client.Out.SendMessage("il faut désormais " + itemCount + " " + exchangeItem + " pour échanger " + item + " maintenant.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    DisplaySyntax(client);
                }
            }
        }

        public override void DisplaySyntax(GameClient client)
        {
            if (client == null || !client.IsPlaying)
                return;
            CmdAttribute[] attrib = (CmdAttribute[])GetType().GetCustomAttributes(typeof(CmdAttribute), false);
            if (attrib.Length == 0)
                return;

            int i = 0;
            string lines = LanguageMgr.GetTranslation(client.Account.Language, attrib[0].Description) + "\n\n";
            foreach (string usage in attrib[0].Usage)
            {
                var str = LanguageMgr.GetTranslation(client.Account.Language, usage);
                i += str.Length;
                if (i > 2000)
                {
                    client.Out.SendMessage(lines, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    lines = "";
                    i = str.Length;
                }
                lines += str + "\n";
                //log.Debug(str.Length + " - " + i);
            }
            client.Out.SendMessage(lines, eChatType.CT_System, eChatLoc.CL_PopupWindow);
            return;
        }
    }
}
