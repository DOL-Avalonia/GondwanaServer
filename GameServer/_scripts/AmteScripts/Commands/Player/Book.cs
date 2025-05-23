using System;
using System.Reflection;
using DOL.Database;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using log4net;
using DOL.Language;


namespace DOL.GS.Scripts
{
    [CmdAttribute(
         "&book",
         ePrivLevel.Player,
         "Commands.Players.Book.Description",
         "Commands.Players.Book.Usage")]
    public class BookCommandHandler : ICommandHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            try
            {
                if (args.Length < 2)
                {
                    Aide(player);
                    return;
                }

                string ScrollTitle = args[2];

                DBBook theScroll = null;

                switch (args[1])
                {
                    case "write":
                    case "remove":
                    case "correct":
                        // Selection du livre
                        theScroll = GetBookFromTitle(ScrollTitle);
                        if (theScroll == null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Book.NotExist", ScrollTitle), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        // Verification des droits
                        if (!isAuthor(player, theScroll))
                            return;
                        break;
                }

                switch (args[1])
                {
                    #region Création
                    case "create":

                        ScrollTitle = String.Join(" ", args, 2, args.Length - 2);
                        var item = player.Inventory.GetItem(eInventorySlot.LastBackpack);

                        if (item == null || (item.Id_nb != "scroll") || (item.Name != "Parchemin vierge"))
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Book.NeedBlankScroll"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        var book = GameServer.Database.SelectObject<DBBook>(b => b.Title == ScrollTitle);
                        if (book != null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Book.Exists"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (!player.Inventory.RemoveItem(item))
                            return;

                        theScroll = new DBBook
                        {
                            Name = "[" + player.Name + "] " + ScrollTitle,
                            Title = ScrollTitle,
                            Author = player.Name,
                            Text = "",
                            PlayerID = player.InternalID,
                            Ink = "",
                            InkId = "",
                        };
                        theScroll.Save();

                        var iu = new ItemUnique(item.Template)
                        {
                            Name = "[" + player.Name + "] " + ScrollTitle,
                            Model = 498,
                            MaxCondition = (int)theScroll.ID
                        };
                        GameServer.Database.AddObject(iu);
                        player.Inventory.AddItem(eInventorySlot.LastBackpack, GameInventoryItem.Create(iu));
                        player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Book.Created"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        break;
                    #endregion
                    #region Ecriture
                    case "write":
                        if (!HaveFeather(player) || !HaveInk(player) || !HaveRightInk(player, theScroll!.InkId))
                            return;

                        theScroll.Ink = (theScroll.Ink == "") ? GetInkType(player) : theScroll.Ink;
                        theScroll.InkId = (theScroll.InkId == "") ? GetInkId(player) : theScroll.InkId;

                        theScroll.Text += string.Join(" ", args, 3, args.Length - 3);
                        theScroll.Text += "\n";

                        DecInk(player, theScroll.InkId);
                        theScroll.Save();
                        player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Book.Writing", ScrollTitle), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        break;
                    #endregion
                    #region Suppression
                    case "remove":
                        for (var i = eInventorySlot.FirstBackpack; i <= eInventorySlot.LastBackpack; i++)
                        {
                            InventoryItem itm = player.Inventory.GetItem(i);
                            if (itm != null && itm.Name == theScroll!.Name)
                            {
                                player.Inventory.RemoveCountFromStack(itm, itm.Count);
                                InventoryLogging.LogInventoryAction(player, "", "(destroy)", eInventoryActionType.Other, itm, itm.Count);
                            }
                        }
                        GameServer.Database.DeleteObject(theScroll);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Book.Burned", ScrollTitle), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        break;
                    #endregion
                    #region Correction
                    case "correct":
                        if (theScroll!.Text.IndexOf("\n") == -1)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Book.EmptyScroll"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (!HaveAcid(player))
                            return;

                        theScroll.Text = theScroll.Text.Substring(0, theScroll.Text.Length - 2);
                        theScroll.Text = theScroll.Text.Substring(0, theScroll.Text.LastIndexOf('\n') + 1);

                        theScroll.Save();
                        player.Out.SendMessage(LanguageMgr.GetTranslation(client, "Commands.Players.Book.LastLineErased", ScrollTitle), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        break;
                        #endregion
                }

            }
            catch (Exception e)
            {
                log.Info("/book Exception: " + e);
                Aide(player);
            }
        }


        public DBBook GetBookFromTitle(string ScrollTitle)
        {
            return GameServer.Database.SelectObject<DBBook>(b => b.Title == ScrollTitle);
        }

        /// <summary>
        /// Retourne le type d'encre
        /// </summary>
        public string GetInkType(GamePlayer player)
        {
            string ItemName;
            for (var i = eInventorySlot.FirstBackpack; i <= eInventorySlot.LastBackpack; i++)
                if (player.Inventory.GetItem(i) != null)
                {
                    ItemName = player.Inventory.GetItem(i).Id_nb;
                    if ((ItemName.StartsWith("ink_")) || (ItemName.StartsWith("blood_")))
                        return player.Inventory.GetItem(i).Name.Replace("(Special) ", "");
                }
            return LanguageMgr.GetTranslation(player.Client.Account.Language, "Commands.Players.Book.UnknownInk");
        }

        /// <summary>
        /// Retourne le type d'encre
        /// </summary>
        public string GetInkId(GamePlayer player)
        {
            string itemId;
            for (var i = eInventorySlot.FirstBackpack; i <= eInventorySlot.LastBackpack; i++)
                if (player.Inventory.GetItem(i) != null)
                {
                    itemId = player.Inventory.GetItem(i).Id_nb;
                    if ((itemId.StartsWith("ink_")) || (itemId.StartsWith("blood_")))
                        return itemId;
                }
            return LanguageMgr.GetTranslation(player.Client.Account.Language, "Commands.Players.Book.UnknownInk");
        }

        /// <summary>
        /// Baisse le niveau d'encre de la fiole
        /// </summary>
        public void DecInk(GamePlayer player, string ink)
        {
            InventoryItem item;
            for (var i = eInventorySlot.FirstBackpack; i <= eInventorySlot.LastBackpack; i++)
            {
                item = player.Inventory.GetItem(i);
                if (item != null && item.Id_nb == ink)
                {
                    --item.Condition;
                    if (item.Condition <= 0)
                    {
                        player.Inventory.RemoveCountFromStack(item, item.Count);
                        InventoryLogging.LogInventoryAction(player, "", "(amte;writing)", eInventoryActionType.Other, item, item.Count);
                    }
                    else
                        player.Client.Out.SendInventoryItemsUpdate(new[] { item });
                    break;
                }
            }
        }

        /// <summary>
        /// Retourne true si player est l'auteur du livre
        /// </summary>
        public bool isAuthor(GamePlayer player, DBBook theScroll)
        {
            if (theScroll.PlayerID != player.InternalID)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.NotAuthor", theScroll.Title), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Retourne true si le joueur a une plume
        /// </summary>
        public bool HaveFeather(GamePlayer player)
        {
            for (var i = eInventorySlot.FirstBackpack; i <= eInventorySlot.LastBackpack; i++)
                if (player.Inventory.GetItem(i) != null)
                    if (player.Inventory.GetItem(i).Id_nb == "feather" ||
                        player.Inventory.GetItem(i).Id_nb.StartsWith("feather_"))
                        return true;
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.NeedFeather"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            return false;
        }

        /// <summary>
        /// Retourne true si le joueur a de l'encre
        /// </summary>
        public bool HaveInk(GamePlayer player)
        {
            for (var i = eInventorySlot.FirstBackpack; i <= eInventorySlot.LastBackpack; i++)
                if (player.Inventory.GetItem(i) != null)
                    if (player.Inventory.GetItem(i).Id_nb.StartsWith("ink_") ||
                        player.Inventory.GetItem(i).Id_nb.StartsWith("blood_"))
                        return true;
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.NeedInk"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            return false;
        }

        /// <summary>
        /// Retourne true si le joueur a de l'acide
        /// </summary>
        public bool HaveAcid(GamePlayer player)
        {
            for (var i = eInventorySlot.FirstBackpack; i <= eInventorySlot.LastBackpack; i++)
                if (player.Inventory.GetItem(i) != null)
                    if (player.Inventory.GetItem(i).Id_nb == "corrector")
                        return true;
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.NeedCorrector"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            return false;
        }

        /// <summary>
        /// Retourne true si le joueur a la bonne encre
        /// </summary>
        public bool HaveRightInk(GamePlayer player, string ink)
        {
            if (String.IsNullOrWhiteSpace(ink))
                return true;
            for (var i = eInventorySlot.FirstBackpack; i <= eInventorySlot.LastBackpack; i++)
                if (player.Inventory.GetItem(i) != null &&
                    player.Inventory.GetItem(i).Id_nb == ink)
                    return true;
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.NeedRightInk", ink), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            return false;
        }

        /// <summary>
        /// Affiche l'aide au joueur
        /// </summary>
        public void Aide(GamePlayer player)
        {
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.HelpTitle"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.HelpCreate"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.HelpWrite"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.HelpWriteNote"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.HelpRemove"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.HelpCorrect"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Book.HelpUse"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}