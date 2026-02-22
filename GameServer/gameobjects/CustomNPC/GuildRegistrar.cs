#nullable enable

/*
* DAWN OF LIGHT - The first free open source DAoC server emulator
* 
* This program is free software; you can redistribute it and/or
* modify it under the terms of the GNU General Public License
* as published by the Free Software Foundation; either version 2
* of the License, or (at your option) any later version.
* 
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with this program; if not, write to the Free Software
* Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*
*/

using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static DOL.GS.ArtifactMgr;

namespace DOL.GS
{
    [NPCGuildScript("Guild Registrar")]
    public class GuildRegistrar : AbstractLibrarian
    {
        private const string INTERACT_KEY_FORM_GUILD = "GuildRegistrar.Keyword.FormGuild";
        private const string INTERACT_KEY_CONSULT_REGISTER = "GuildRegistrar.Keyword.ConsultRegister";
        private const string INTERACT_KEY_PREVIOUS_PAGE = "GuildRegistrar.List.PreviousPage";
        private const string INTERACT_KEY_NEXT_PAGE = "GuildRegistrar.List.NextPage";

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            Task.Run(async () =>
            {
                var cache = EnsurePlayerCache(player);
                var taskForm = cache.TranslateResponseKey(INTERACT_KEY_FORM_GUILD);
                if (Properties.GUILD_REQUIRE_REGISTER)
                {
                    var taskConsult = cache.TranslateResponseKey(INTERACT_KEY_CONSULT_REGISTER);

                    SayTo(player, [
                            LanguageMgr.Translate(player, "GuildRegistrar.Interact.RegisterMode.Line1"),
                            LanguageMgr.Translate(player, "GuildRegistrar.Interact.RegisterMode.Line2", await taskForm),
                            Task.FromResult(string.Empty),
                            LanguageMgr.Translate(player, "GuildRegistrar.Interact.RegisterMode.Line3", await taskConsult)
                    ]);
                }
                else
                {
                    SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Interact.NormalMode", await taskForm));
                }
            });

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text))
                return false;

            if (source is not GamePlayer player)
                return true;
            
            var cache = GetPlayerCache(player);
            if (cache is null) // Cache gone or first interaction somehow
                return Interact(player);

            var keyword = cache.GetResponseKey(text);
            switch (keyword)
            {
                case INTERACT_KEY_CONSULT_REGISTER when Properties.GUILD_REQUIRE_REGISTER:
                    SendGuildRegisterList(cache);
                    return true;
                
                case INTERACT_KEY_FORM_GUILD:
                    if (Properties.GUILD_REQUIRE_REGISTER)
                    {
                        // Register mode instructions
                        var taskConsult = cache.TranslateResponseKey(INTERACT_KEY_CONSULT_REGISTER);
                        SayTo(player, [
                            LanguageMgr.Translate(player, "GuildRegistrar.Whisper.Form.Register.Line1"),
                            LanguageMgr.Translate(player, "GuildRegistrar.Whisper.Form.Register.Line2"),
                            Task.FromResult(string.Empty),
                            LanguageMgr.Translate(player, "GuildRegistrar.Whisper.Form.Register.Line3", taskConsult)
                        ]);
                    }
                    else
                    {
                        // Normal mode instructions
                        SayTo(player, [
                            LanguageMgr.Translate(player, "GuildRegistrar.Whisper.Form.Normal.Line1", Properties.GUILD_NUM),
                            LanguageMgr.Translate(player, "GuildRegistrar.Whisper.Form.Normal.Line2")
                        ]);
                    }
                    return true;
            }
            
            // Click on a register title in the list -> show FULL TEXT (Fix #4)
            if (Properties.GUILD_REQUIRE_REGISTER)
            {
                var regClicked = GameServer.Database.SelectObject<DBBook>(b =>
                    b.Title == text && b.IsGuildRegistry);

                if (regClicked != null)
                {
                    ShowRegistry(cache, regClicked);
                    return true;
                }
            }

            return true;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (source is not GamePlayer player || item == null)
                return false;

            if (!Properties.GUILD_REQUIRE_REGISTER)
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Receive.NotRequired"));
                return true;
            }

            long bookId = item.MaxCondition;
            if (bookId <= 0)
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Receive.NotBook"));
                return true;
            }

            DBBook book = GameServer.Database.FindObjectByKey<DBBook>(bookId);
            if (book == null)
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Receive.NoLedger"));
                return true;
            }

            // Must be a guild registry
            if (!book.IsGuildRegistry)
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Receive.NotRegister"));
                return true;
            }

            // Must be stamped/legalized
            if (!IsBookStamped(book))
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Receive.NotStamped"));
                return true;
            }

            // Guild name = book title
            string guildName = (book.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(guildName) ||
                guildName.Length > Guild.MAX_CREATE_NAME_LENGTH ||
                !Commands.GuildCommandHandler.IsValidGuildName(guildName))
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Receive.InvalidName"));
                return true;
            }

            if (GuildMgr.DoesGuildExist(guildName))
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Receive.NameTaken"));
                return true;
            }

            int required = Properties.GUILD_NUM;
            var founders = BookUtils.ExtractFounders(book.Text, required);

            if (string.IsNullOrWhiteSpace(founders.leader) || founders.members.Count != required - 1)
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Receive.IncompleteList"));
                return true;
            }

            // Build founder name list (leader first)
            var founderNames = new List<string>(required);
            founderNames.Add(founders.leader);
            founderNames.AddRange(founders.members);

            // Any founder can present it (leader OR member)
            if (!founderNames.Any(n => n.Equals(player.Name, StringComparison.OrdinalIgnoreCase)))
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Receive.NotFounder"));
                return true;
            }

            // Validate founders exist and are guildless (DB), and unique accounts
            var accounts = new List<string>(required);

            foreach (string fn in founderNames)
            {
                var ch = BookUtils.GetCharacter(fn);
                if (ch == null)
                {
                    SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Validate.FounderNotFound", fn));
                    return true;
                }

                if (!BookUtils.IsGuildless(ch))
                {
                    SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Validate.FounderInGuild", fn));
                    return true;
                }

                accounts.Add(BookUtils.GetAccountName(ch));
            }

            if (!BookUtils.AccountsAreUnique(accounts))
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Validate.SameAccount"));
                return true;
            }

            Guild newGuild = GuildMgr.CreateGuild(player.Realm, guildName, player);
            if (newGuild == null)
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Error.CreateFailed"));
                return true;
            }

            // Add founders (leader rank 0, others rank 5)
            foreach (string fn in founderNames)
            {
                ushort rankId = fn.Equals(founders.leader, StringComparison.OrdinalIgnoreCase) ? (ushort)0 : (ushort)5;
                AssignFounderToGuild(newGuild, fn, rankId);
            }

            // Consume the register item
            if (!player.Inventory.RemoveItem(item))
            {
                GuildMgr.DeleteGuild(newGuild);
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Error.TakeItemFailed"));
                return true;
            }

            UpdateBookAfterGuildCreation(book, guildName);

            try
            {
                book.Save();
            }
            catch
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Error.BookUpdateFailed"));
            }

            RefreshGuildAndMembersUI(newGuild);
            SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Success", guildName));
            return true;
        }

        private static void UpdateBookAfterGuildCreation(DBBook book, string guildName)
        {
            book.Author = GUILD_REGISTER_AUTHOR;
            book.Title = guildName;
            book.Name = $"[REGISTER] {guildName}";
            book.PlayerID = string.Empty;
            book.IsInLibrary = false;
            book.CurrentPriceCopper = 0;
            book.BasePriceCopper = 0;
            book.IsGuildRegistry = true;
            book.IsStamped = false;
            book.StampBy = string.Empty;
            book.StampDate = DateTime.MinValue;
        }

        private static bool IsBookStamped(DBBook book)
        {
            if (book.IsStamped)
                return true;

            if (!string.IsNullOrWhiteSpace(book.StampBy))
                return true;

            if (book.StampDate != DateTime.MinValue)
                return true;

            string text = book.Text ?? string.Empty;
            if (text.IndexOf("#stamped", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("#guildstamped", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private void AssignFounderToGuild(Guild guild, string characterName, ushort rankId)
        {
            var rank = guild.GetRankByID(rankId);
            var client = WorldMgr.GetClientByPlayerName(characterName, true, false);
            var gp = client?.Player;

            if (gp != null)
            {
                guild.AddPlayer(gp, rank);
                gp.Guild = guild;
                gp.GuildID = guild.GuildID;
                gp.GuildName = guild.Name;
                gp.GuildRank = rank;

                gp.Out.SendUpdatePlayer();
                guild.UpdateMember(gp);
                guild.GetListOfOnlineMembers();
                return;
            }

            var ch = BookUtils.GetCharacter(characterName);
            if (ch == null)
                return;

            ch.GuildID = guild.GuildID;
            ch.GuildRank = rankId;

            GameServer.Database.SaveObject(ch);
        }

        private static void RefreshGuildAndMembersUI(Guild guild)
        {
            try
            {
                guild.UpdateGuildWindow();

                foreach (GamePlayer ply in guild.GetListOfOnlineMembers())
                {
                    ply.Out.SendUpdatePlayer();
                    guild.UpdateMember(ply);
                }

                guild.UpdateGuildWindow();
                guild.GetListOfOnlineMembers();
            }
            catch { }
        }

        private void SendGuildRegisterList(PlayerCache cache)
        {
            var player = cache.Player;
            var registers = GameServer.Database
                .SelectObjects<DBBook>(b => b.IsGuildRegistry)
                .OrderBy(b => b.Title)
                .Select(b => new PlayerCache.BookListEntry(b))
                .ToList();

            cache.BookList = registers;
            if (registers.Count == 0)
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.List.None"));
                return;
            }

            Task.Run(async () =>
            {
                var count = await LanguageMgr.Translate(player, "GuildRegistrar.List.Count", registers.Count);
                SayTo(player, count);
                await SendGuildRegisterPage(cache, 0);
            });
        }
        private async Task SendGuildRegisterPage(PlayerCache cache, int page)
        {
            var sb = new StringBuilder(2048);
            var books = cache.GetBooksForPage(page).ToList();
            if (!books.Any())
                return;

            var totalPages = Math.Max(1, cache.TotalListPages);
            page = Math.Clamp(0, page, totalPages - 1);
            var player = cache.Player;
            var taskStamped = LanguageMgr.Translate(player, "GuildRegistrar.List.StampedBy");
            var taskPage = LanguageMgr.Translate(player, "GuildRegistrar.List.CurrentPage", page + 1, totalPages);
            Task<string>?[] navigationTasks = [ taskPage, null, null ];
            if (page > 0)
            {
                navigationTasks[1] = ChatUtil.ToResponse(cache.TranslateResponseKey(INTERACT_KEY_PREVIOUS_PAGE));
            }

            if (page + 1 < cache.TotalListPages)
            {
                navigationTasks[2] = ChatUtil.ToResponse(cache.TranslateResponseKey(INTERACT_KEY_NEXT_PAGE));
            }

            int i = 0;
            cache.CurrentListPage = page;
            foreach (var b in cache.GetBooksForPage(page).ToList())
            {
                if (i != 0)
                    sb.Append('\n');
                
                // Clickable title
                sb.Append('[')
                    .Append(b.Title) // No translation, this is guild name
                    .Append(']');

                // Optional metadata
                if (!string.IsNullOrWhiteSpace(b.StampedBy) || b.StampDate != DateTime.MinValue)
                {
                    sb.Append(" - ")
                        .Append(await taskStamped)
                        .Append(" ")
                        .Append(string.IsNullOrWhiteSpace(b.StampedBy) ? "?" : b.StampedBy);

                    if (b.StampDate != DateTime.MinValue)
                        sb.Append(" - ").Append(b.StampDate.ToString("yyyy-MM-dd HH:mm"));
                }

                if (sb.Length > 1800)
                {
                    player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    sb.Clear();
                }
                ++i;
            }

            if (sb.Length > 0)
                player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);

            string navText = string.Join(' ', await Task.WhenAll(navigationTasks.Where(t => t != null).Cast<Task<string>>()));
            player.Out.SendMessage(navText, eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        private void ShowRegistry(PlayerCache cache, DBBook registry)
        {
            var player = cache.Player;
            string language = string.IsNullOrEmpty(registry.Language) ? Properties.SERV_LANGUAGE : registry.Language;
            var taskAuthor = LanguageMgr.Translate(player, "GuildRegistrar.Read.Author", registry.Author);
            var taskLanguage = player.AutoTranslateEnabled ? LanguageMgr.Translate(player, "GuildRegistrar.Read.Language", language) : null;
            var taskTitle = LanguageMgr.Translate(player, "GuildRegistrar.Read.Title", registry.Title);
            var taskInk = LanguageMgr.Translate(player, "GuildRegistrar.Read.Ink", registry.Ink);

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

                var text = await cache.TranslateBookText(registry);
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
            });
        }
    }
}