using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static DOL.GS.ArtifactMgr;

namespace DOL.GS.Scripts
{
    public class BooksMgr
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(GamePlayerEvent.UseSlot, new DOLEventHandler(PlayerUseSlot));
        }
        [ScriptUnloadedEvent]
        public static void ScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(GamePlayerEvent.UseSlot, new DOLEventHandler(PlayerUseSlot));
        }

        protected static void PlayerUseSlot(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;
            if (player == null) return;

            UseSlotEventArgs uArgs = (UseSlotEventArgs)args;

            InventoryItem item = player.Inventory.GetItem((eInventorySlot)uArgs.Slot);
            if (item == null) return;

            if (item.Id_nb.StartsWith("scroll"))
                ReadBook(player, GameServer.Database.FindObjectByKey<DBBook>(item.MaxCondition));
        }

        public static void ReadGuildRegistry(GamePlayer player, DBBook registry)
        {
            if (registry == null)
            {
                player.Client.Out.SendMessage("~~ Parchemin Vierge ~~", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            string language = string.IsNullOrEmpty(registry.Language) ? Properties.SERV_LANGUAGE : registry.Language;
            var taskAuthor = LanguageMgr.Translate(player, "GuildRegistrar.Read.Author", registry.Author);
            var taskLanguage = player.AutoTranslateEnabled ? LanguageMgr.Translate(player, "GuildRegistrar.Read.Language", language) : null;
            var taskTitle = LanguageMgr.Translate(player, "GuildRegistrar.Read.Title", registry.Title);
            var taskInk = LanguageMgr.Translate(player, "GuildRegistrar.Read.Ink", registry.Ink);
            var (leader, founders) = BookUtils.ExtractFounders(registry);
            var taskLeader = LanguageMgr.Translate(player, "GuildRegistrar.Read.Leader");
            var taskFounder = LanguageMgr.Translate(player, "GuildRegistrar.Read.Founder");
            var taskText = AutoTranslateManager.Translate(registry.Language, player, registry.Text);

            Task.Run(async () =>
            {
                var sb = new StringBuilder(2048);
                sb
                    .Append("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\n")
                    .Append(await taskAuthor).Append('\n')
                    .Append(await taskTitle).Append('\n');

                if (taskLanguage is not null)
                {
                    sb.Append(await taskLanguage).Append('\n');
                }
            
                sb
                    .Append(await taskInk).Append('\n')
                    .Append("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\n");

                player.Client.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                sb.Clear();

                var text = await taskText;
                for (int i = 0; i < text.Length; i++)
                {
                    if (i + 2 < text.Length)
                    {
                        if ((text[i] == '\n') && (text[i + 1] == '\n'))
                        {
                            player.Client.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                            sb.Clear();
                            i++;
                            i++;
                            continue;
                        }
                        else if (sb.Length > 1900)
                        {
                            player.Client.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                            sb.Clear();
                        }
                    }
                    sb.Append(text[i]);
                }

                player.Client.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                sb.Clear();

                if (!string.IsNullOrEmpty(leader))
                {
                    var leaderStr = string.Format(await taskLeader, leader);
                    sb.Append(leaderStr).Append('\n');
                }
                foreach (var founder in founders)
                {
                    var founderStr = string.Format(await taskFounder, founder);
                    sb.Append(founderStr).Append('\n');
                }
                player.Client.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            });
        }

        public static void ReadBook(GamePlayer player, DBBook dbBook)
        {
            if (dbBook == null)
            {
                player.Client.Out.SendMessage("~~ Parchemin Vierge ~~", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            if (dbBook.IsGuildRegistry)
            {
                ReadGuildRegistry(player, dbBook);
                return;
            }

            var sb = new StringBuilder(2048);
            sb
                .Append("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\n")
                .Append("Auteur: ").Append(dbBook.Author).Append("\n")
                .Append("Titre: ").Append(dbBook.Title).Append("\n")
                .Append("Encre utilisée: " + dbBook.Ink + "\n")
                .Append("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\n");

            player.Client.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            sb.Clear();

            for (int i = 0; i < dbBook.Text.Length; i++)
            {
                if (i + 2 < dbBook.Text.Length && (dbBook.Text[i] == '\n') && (dbBook.Text[i + 1] == '\n'))
                {
                    player.Client.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    sb.Clear();
                    i++;
                    i++;
                    continue;
                }
                else if (sb.Length > 1900)
                {
                    player.Client.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    sb.Clear();
                }

                sb.Append(dbBook.Text[i]);
            }
            
            if (sb.Length > 0)
                player.Client.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
        }

        public static InventoryItem CreateBookItem(DBBook book)
        {
            var baseTemplate = GameServer.Database.SelectObject<ItemTemplate>(t => t.Id_nb == "scroll");
            if (baseTemplate == null) return null;

            var iu = new ItemUnique(baseTemplate)
            {
                Id_nb = "scroll" + Guid.NewGuid(),
                Name = "[" + book.Author + "] " + book.Title,
                Model = 498,
                MaxCondition = (int)book.ID
            };

            GameServer.Database.AddObject(iu);
            return GameInventoryItem.Create(iu);
        }
    }
}
