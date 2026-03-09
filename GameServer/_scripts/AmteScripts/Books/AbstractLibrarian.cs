#nullable enable

using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS.Scripts
{
    public abstract class AbstractLibrarian : AmteMob
    {
        protected const int BOOKS_PER_PAGE = 7;
        
        protected const string INTERACT_KEY_PREVIOUS_PAGE = "Librarian.BookList.PreviousPage";
        protected const string INTERACT_KEY_NEXT_PAGE = "Librarian.BookList.NextPage";

        protected record class PlayerCache(GamePlayer Player)
        {
            public record class BookListEntry
            {
                public BookListEntry(DBBook book)
                {
                    Title = book.Title;
                    Author = book.Author;
                    Price = book.CurrentPriceCopper;
                    Upvotes = book.UpVotes;
                    Downvotes = book.DownVotes;
                    StampedBy = book.StampBy;
                    StampDate = book.StampDate;
                    IsStamped = book.IsStamped;
                }

                public string Title { get; } = string.Empty;
                public string Author { get; } = string.Empty;
                public string Language { get; } = string.Empty;
                public string StampedBy { get; } = string.Empty;
                public DateTime StampDate { get; } = DateTime.MinValue;
                public bool IsStamped { get; }
                public int Price { get; } = 0;
                public int Upvotes { get; } = 0;
                public int Downvotes { get; } = 0;
                public float Rating => Upvotes + Downvotes == 0 ? 0 : (float)Upvotes / (Upvotes + Downvotes);
            }

            public DBBook? CurrentBook { get; set; }

            private ConcurrentDictionary<string, string> ResponseToTranslationKey { get; } = new();
            private ConcurrentDictionary<string, string> TranslationKeyToPrefix { get; } = new();
            private ConcurrentDictionary<string, string> ResponseToBookTitle { get; } = new();
            public long LastAccessed { get; set; } = long.MinValue;
            public List<BookListEntry>? BookList { get; set; } = null;
            public int CurrentListPage { get; set; }
            public int TotalListPages => BookList == null ? 0 : (int)Math.Ceiling((float)BookList.Count / BOOKS_PER_PAGE);
            
            public IEnumerable<BookListEntry> GetBooksForPage(int page)
            {
                if (BookList == null)
                    return Enumerable.Empty<BookListEntry>();

                return BookList.Skip(page * BOOKS_PER_PAGE).Take(BOOKS_PER_PAGE);
            }

            public void AddResponseKey(string baseKey, string translation)
            {
                ResponseToTranslationKey[translation.ToLowerInvariant()] = baseKey;
            }

            public async Task<string> TranslateResponseKey(string baseKey)
            {
                var translated = await LanguageMgr.Translate(Player, baseKey);
                if (string.IsNullOrEmpty(translated))
                    return string.Empty;

                AddResponseKey(baseKey, translated);
                return translated;
            }

            public async Task<string> TranslatePrefixKey(string baseKey)
            {
                var translated = await LanguageMgr.Translate(Player, baseKey);
                if (string.IsNullOrEmpty(translated))
                    return string.Empty;

                TranslationKeyToPrefix[baseKey] = translated;
                return translated;
            }

            public async Task<string> TranslateBookTitle(DBBook book)
            {
                var lang = book.Language;
                var title = book.Title;
                if (string.IsNullOrEmpty(lang))
                    lang = Properties.SERV_LANGUAGE;

                var translated = await AutoTranslateManager.Translate(lang, Player, title);
                if (string.IsNullOrEmpty(translated))
                    return string.Empty;

                ResponseToBookTitle[translated] = title;
                return translated;
            }

            public async Task<string> TranslateBookTitle(BookListEntry book)
            {
                var lang = book.Language;
                var title = book.Title;
                if (string.IsNullOrEmpty(lang))
                    lang = Properties.SERV_LANGUAGE;

                var translated = await AutoTranslateManager.Translate(lang, Player, title);
                if (string.IsNullOrEmpty(translated))
                    return string.Empty;

                ResponseToBookTitle[translated] = title;
                return translated;
            }

            public async Task<string> TranslateBookText(DBBook book)
            {
                var lang = book.Language;
                var text = book.Text;
                if (string.IsNullOrEmpty(lang))
                    lang = Properties.SERV_LANGUAGE;

                var translated = await AutoTranslateManager.Translate(lang, Player, text);
                if (string.IsNullOrEmpty(translated))
                    return string.Empty;

                return translated;
            }

            public string? GetResponseKey(string translation, string? orElse = null)
            {
                return ResponseToTranslationKey.TryGetValue(translation.ToLowerInvariant(), out string? value) ? value : orElse;
            }

            public string? GetResponsePrefix(string baseKey, string? orElse = null)
            {
                if (TranslationKeyToPrefix.TryGetValue(baseKey, out string? translated))
                {
                    return translated;
                }
                return orElse;
            }

            public string? GetBookTitleID(string translatedTitle, string? prefix = null, string? orElse = null)
            {
                if (prefix is not null)
                {
                    translatedTitle = translatedTitle.Substring(prefix.Length).Trim();
                }

                if (string.IsNullOrEmpty(translatedTitle))
                {
                    return CurrentBook?.Title ?? orElse;
                }

                return ResponseToBookTitle.TryGetValue(translatedTitle, out string? value) ? value : orElse;
            }

            public DBBook? GetBook(string translatedtitle, string? prefix = null, bool onlyInLibrary = true, bool includeRegistry = false)
            {
                string? originalTitle = GetBookTitleID(translatedtitle, prefix);
                if (originalTitle is null)
                    return null;

                DBBook? bookToRead;
                if (onlyInLibrary)
                {
                    if (includeRegistry)
                    {
                        bookToRead = GameServer.Database.SelectObject<DBBook>(b => b.IsInLibrary && b.Title == originalTitle);
                    }
                    else
                    {
                        bookToRead = GameServer.Database.SelectObject<DBBook>(b => b.IsInLibrary && b.Title == originalTitle && b.IsGuildRegistry == false);
                    }
                }
                else if (includeRegistry)
                {
                    bookToRead = GameServer.Database.SelectObject<DBBook>(b => b.Title == originalTitle);
                }
                else
                {
                    bookToRead = GameServer.Database.SelectObject<DBBook>(b => b.Title == originalTitle && b.IsGuildRegistry == false);
                }
                return bookToRead;
            }
        }

        /// <summary>
        /// How long to keep player cache entries before cleaning them up
        /// </summary>
        private const long CACHE_KEEPALIVE_MILLISECONDS = 30 * 60 * 1000;
        /// <summary>
        /// How often to check for stale cache entries
        /// </summary>
        private const int CACHE_CHECK_INTERVAL_MILLISECONDS = 30 * 1000;
        private readonly ConcurrentDictionary<string, PlayerCache> _playerCaches = new();
        private RegionTimer? _cleanupTimer;

        protected AbstractLibrarian() : base()
        {
        }

        protected AbstractLibrarian(INpcTemplate tpl) : base(tpl)
        {
        }

        /// <inheritdoc />
        public override bool AddToWorld()
        {
            if (!base.AddToWorld())
                return false;

            _cleanupTimer = new RegionTimer(this, CleanupTimer);
            _cleanupTimer.Start(CACHE_CHECK_INTERVAL_MILLISECONDS);
            return true;
        }

        /// <inheritdoc />
        public override bool RemoveFromWorld()
        {
            if (!base.RemoveFromWorld())
                return false;

            _cleanupTimer.Stop();
            return true;
        }

        private int CleanupTimer(RegionTimer callingTimer)
        {
            var now = Environment.TickCount64;
            var toRemove = _playerCaches.Where(kv => kv.Value.LastAccessed + CACHE_KEEPALIVE_MILLISECONDS < now).Select(kv => kv.Key).ToList();
            toRemove.ForEach(key => _playerCaches.TryRemove(key, out _));
            return CACHE_CHECK_INTERVAL_MILLISECONDS;
        }
        
        protected PlayerCache EnsurePlayerCache(GamePlayer player)
        {
            var cache = _playerCaches.GetOrAdd(player.InternalID + ':' + player.Language, _ => new PlayerCache(player));
            cache.LastAccessed = Environment.TickCount64;
            return cache;
        }
        
        protected PlayerCache? GetPlayerCache(GamePlayer player)
        {
            if (!_playerCaches.TryGetValue(player.InternalID + ':' + player.Language, out PlayerCache cache))
            {
                return null;
            }

            cache.LastAccessed = Environment.TickCount64;
            return cache;
        }

        protected async Task SendBookListPage(PlayerCache cache, int page)
        {
            var sb = new StringBuilder(2048);
            var books = cache.GetBooksForPage(page).ToList();
            if (!books.Any())
                return;

            var totalPages = Math.Max(1, cache.TotalListPages);
            page = Math.Clamp(0, page, totalPages - 1);
            var player = cache.Player;
            var pageTask = LanguageMgr.Translate(player, "Librarian.BookList.CurrentPage", page + 1, totalPages);
            var upvoteTask = LanguageMgr.Translate(player, "Librarian.BookList.VotesPrefixPositive");
            var downvoteTask = LanguageMgr.Translate(player, "Librarian.BookList.VotesPrefixNegative");
            Task<string>?[] navigationTasks = [ pageTask, null, null ];
            if (page > 0)
            {
                navigationTasks[1] = ChatUtil.ToResponse(cache.TranslateResponseKey(INTERACT_KEY_PREVIOUS_PAGE));
            }

            if (page + 1 < cache.TotalListPages)
            {
                navigationTasks[2] = ChatUtil.ToResponse(cache.TranslateResponseKey(INTERACT_KEY_NEXT_PAGE));
            }

            var upvotes = await upvoteTask;
            var downvotes = await downvoteTask;
            cache.CurrentListPage = page;
            foreach (var b in books)
            {
                string price = Money.GetString(b.Price);
                string title = await cache.TranslateBookTitle(b);

                sb.Append("\n[")
                    .Append(title)
                    .Append("] - ")
                    .Append(b.Author)
                    .Append(" - ")
                    .Append(price)
                    .Append(" - ")
                    .Append(upvotes)
                    .Append(' ')
                    .Append(b.Upvotes)
                    .Append(" / ")
                    .Append(downvotes)
                    .Append(' ')
                    .Append(b.Downvotes);

                if (sb.Length > 1800)
                {
                    player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
                player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);

            string navText = string.Join(' ', await Task.WhenAll(navigationTasks.Where(t => t != null).Cast<Task<string>>()));
            player.Out.SendMessage(navText, eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        protected void SendGuildRegisterList(PlayerCache cache)
        {
            var player = cache.Player;
            List<PlayerCache.BookListEntry>? registers = null;
            
            registers = GameServer.Database
                .SelectObjects<DBBook>(b => b.IsGuildRegistry && b.IsInLibrary)
                .OrderByDescending(b => b.IsStamped)
                .ThenBy(b => b.Title)
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
            var taskDefunct = LanguageMgr.Translate(player, "GuildRegistrar.List.Defunct");
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
            async Task AddBookDetails(PlayerCache.BookListEntry b)
            {
                if (i != 0)
                    sb.Append('\n');
                
                // Clickable title
                sb.Append('[')
                    .Append(b.Title) // No translation, this is guild name
                    .Append(']');

                if (!b.IsStamped)
                {
                    sb.Append(" - ");
                    sb.Append(await taskDefunct);
                }

                // Optional metadata
                if (!string.IsNullOrWhiteSpace(b.StampedBy) || b.StampDate != DateTime.MinValue)
                {
                    sb.Append(" - ")
                        .Append(await taskStamped)
                        .Append(' ')
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

            foreach (var book in books)
            {
                AddBookDetails(book);
            }

            if (sb.Length > 0)
                player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);

            string navText = string.Join(' ', await Task.WhenAll(navigationTasks.Where(t => t != null).Cast<Task<string>>()));
            player.Out.SendMessage(navText, eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }
    }
}
