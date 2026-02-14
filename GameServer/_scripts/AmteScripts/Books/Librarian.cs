#nullable enable
using DOL.Database;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOL.Numbers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Transactions;
using static System.Net.Mime.MediaTypeNames;

namespace DOL.GS.Scripts
{
    public class Librarian : AmteMob
    {
        private int MinWords => Properties.BOOK_MIN_WORDS;
        private int BaseCopperAtMin => Properties.BOOK_BASE_PRICE_COPPER_AT_MIN_WORDS;
        private int VoteStepPercent => Properties.BOOK_RATING_POSITIVE_BONUS_PERCENT;
        private int MaxMultiplierPercent => Properties.BOOK_MAX_RATING_MULTIPLIER_PERCENT;
        private const int PreviewWordsCount = 15;
        private const string GUILD_REGISTER_AUTHOR = "Guild Register";

        private static bool Eq(string? a, string? b)
            => a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private record class PlayerCache(GamePlayer Player)
        {
            private ConcurrentDictionary<string, string> ResponseToTranslationKey { get; } = new();
            private ConcurrentDictionary<string, string> TranslationKeyToPrefix { get; } = new();
            private ConcurrentDictionary<string, string> ResponseToBookTitle { get; } = new();
            public long LastAccessed { get; set; } = long.MinValue;
            public bool HasBooks { get; set; } = false;
            public DBBook? CurrentBook { get; set; }

            public async Task<string> TranslateResponseKey(string baseKey)
            {
                var translated = await LanguageMgr.Translate(Player, baseKey);
                if (string.IsNullOrEmpty(translated))
                    return string.Empty;

                ResponseToTranslationKey[translated.ToLowerInvariant()] = baseKey;
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

            public DBBook? GetBook(string translatedtitle, string? prefix = null, bool onlyInLibrary = true)
            {
                string? originalTitle = GetBookTitleID(translatedtitle, prefix);
                if (originalTitle is null)
                    return null;
                
                DBBook? bookToRead = GameServer.Database.SelectObject<DBBook>(b => b.IsInLibrary == onlyInLibrary && b.Title == originalTitle);
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

        private const string INTERACT_KEY_CONSULT_BOOKS = "Librarian.Menu.ConsultBooks";
        private const string INTERACT_KEY_CONSULT_GUILDS = "Librarian.Menu.ConsultGuildRegister";
        private const string INTERACT_KEY_ADD_BOOK = "Librarian.Menu.AddBook";
        private const string INTERACT_KEY_COLLECT_ROYALTIES = "Librarian.Menu.CollectRoyalties";
        private const string INTERACT_KEY_PREFIX_VOTE_UP = "Librarian.Prefix.VotePlus";
        private const string INTERACT_KEY_PREFIX_VOTE_DOWN = "Librarian.Prefix.VoteMinus";
        private const string INTERACT_KEY_PREFIX_BUY = "Librarian.Prefix.Buy";
        private const string INTERACT_KEY_PREFIX_READ = "Librarian.Prefix.Read";
        private const string INTERACT_KEY_PREFIX_LEGACYREAD = "Librarian.Prefix.LegacyBook";
        
        private PlayerCache EnsurePlayerCache(GamePlayer player)
        {
            var cache = _playerCaches.GetOrAdd(player.InternalID + ':' + player.Language, _ => new PlayerCache(player));
            cache.LastAccessed = Environment.TickCount64;
            return cache;
        }
        
        private PlayerCache? GetPlayerCache(GamePlayer player)
        {
            if (!_playerCaches.TryGetValue(player.InternalID + ':' + player.Language, out PlayerCache cache))
            {
                return null;
            }

            cache.LastAccessed = Environment.TickCount64;
            return cache;
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

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            Task.Run(async () =>
            {
                var cache = EnsurePlayerCache(player);
                
                // Start them all... in parallel, so that if translation is slow it doesn't delay the first ones.
                var text1 = LanguageMgr.Translate(player, "Librarian.InteractText01");
                var text2 = LanguageMgr.Translate(player, "Librarian.InteractText02");
                var consultBooks = cache.TranslateResponseKey(INTERACT_KEY_CONSULT_BOOKS);
                var consultGuilds = Properties.GUILD_REQUIRE_REGISTER ? cache.TranslateResponseKey(INTERACT_KEY_CONSULT_GUILDS) : null;
                var addBook = cache.TranslateResponseKey(INTERACT_KEY_ADD_BOOK);
                var royalties = cache.TranslateResponseKey(INTERACT_KEY_COLLECT_ROYALTIES);
                
                // Await them so we don't have a delay between greeting and menu
                await text1;
                await text2;
                await consultBooks;
                if (Properties.GUILD_REQUIRE_REGISTER)
                {
                    await consultGuilds!;
                }
                await addBook;
                await royalties;
                
                // Greeting popup
                player.Out.SendMessage(
                    await text1 + "\n" +
                    await text2,
                    eChatType.CT_Say, eChatLoc.CL_PopupWindow);

                // Menu popup
                var sb = new StringBuilder(512);

                sb.Append("[").Append(await consultBooks).Append("]\n");

                if (Properties.GUILD_REQUIRE_REGISTER)
                {
                    sb.Append("[").Append(await consultGuilds!).Append("]\n");
                }

                sb.Append("[").Append(await addBook).Append("]\n")
                    .Append("[").Append(await royalties).Append("]");

                player.Out.SendMessage(sb.ToString(), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            });

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            var player = source as GamePlayer;
            if (!base.WhisperReceive(source, text) || player == null)
                return false;

            var cache = GetPlayerCache(player);
            if (cache is null) // Cache is gone / player didn't talk to librarian first
                return Interact(player);
            
            string? response = cache.GetResponseKey(text);
            if (response is not null)
            {
                switch (response)
                {
                    // Consult register list
                    case INTERACT_KEY_CONSULT_GUILDS when Properties.GUILD_REQUIRE_REGISTER:
                        SendGuildRegisterList(player);
                        return true;
                        
                    // List normal books
                    case INTERACT_KEY_CONSULT_BOOKS:
                        SendBookList(cache);
                        return true;
                    
                    case INTERACT_KEY_ADD_BOOK:
                        player.Out.SendMessage(LanguageMgr.Translate(player, "Librarian.ResponseText02"),
                                               eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return true;
                    
                    case INTERACT_KEY_COLLECT_ROYALTIES:
                        CollectRoyalties(player);
                        return true;
                }
            }

            if (Properties.GUILD_REQUIRE_REGISTER)
            {
                // Click on a register title -> show FULL TEXT
                var regClicked = GameServer.Database.SelectObject<DBBook>(b => b.Title == text && b.Author == GUILD_REGISTER_AUTHOR);
                if (regClicked != null)
                {
                    BooksMgr.ReadBook(player, regClicked);
                    return true;
                }
            }

            if (!cache.HasBooks)
                return true;
            
            // Author-only full read shortcut: "Read "
            string? readPrefix = cache.GetResponsePrefix(INTERACT_KEY_PREFIX_READ);
            if (readPrefix is not null && text.StartsWith(readPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var bookToRead = cache.GetBook(text, readPrefix);
                if (bookToRead == null)
                {
                    player.Out.SendMessage(LanguageMgr.Translate(player, "Librarian.Book.NotFound"),
                                           eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }

                if (bookToRead.PlayerID != player.InternalID)
                {
                    player.Out.SendMessage(LanguageMgr.Translate(player, "Librarian.Read.AuthorOnly"),
                                           eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }

                BooksMgr.ReadBook(player, bookToRead);
                return true;
            }

            // Vote + / Vote -
            string? votePlusPrefix = cache.GetResponsePrefix(INTERACT_KEY_PREFIX_VOTE_UP);
            string? voteMinusPrefix = cache.GetResponsePrefix(INTERACT_KEY_PREFIX_VOTE_DOWN);
            if (votePlusPrefix is not null && text.StartsWith(votePlusPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var title = cache.GetBookTitleID(text, votePlusPrefix);
                Vote(cache, title, +1);
                return true;
            }

            if (voteMinusPrefix is not null && text.StartsWith(voteMinusPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var title = cache.GetBookTitleID(text, voteMinusPrefix);
                Vote(cache, title, -1);
                return true;
            }

            // Buy
            string? buyPrefix = cache.GetResponsePrefix(INTERACT_KEY_PREFIX_BUY);
            if (buyPrefix is not null && text.StartsWith(buyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Todo, review these async tasks; can we wrap BuyBook in there? It adds to inventory, is the inventory threadsafe?
                var bookToBuy = cache.GetBook(text, buyPrefix);
                if (bookToBuy == null)
                {
                    player.Out.SendMessage(LanguageMgr.Translate(player, "Librarian.Book.NotFound"),
                                           eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }

                if (bookToBuy.PlayerID == player.InternalID)
                {
                    player.Out.SendMessage(LanguageMgr.Translate(player, "Librarian.Buy.CannotBuyOwn"),
                                           eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }

                BuyBook(cache, bookToBuy);
                return true;
            }

            // Clicking on a book title -> preview
            var originalTitle = cache.GetBookTitleID(text);
            var clicked = GameServer.Database.SelectObject<DBBook>(
                b => b.IsInLibrary && b.Title == originalTitle && b.Author != GUILD_REGISTER_AUTHOR);

            if (clicked != null)
            {
                ShowBookPreview(cache, clicked);
                return true;
            }

            // "Book <id>" -> preview
            string? legacyPrefix = cache.GetResponseKey(INTERACT_KEY_PREFIX_LEGACYREAD);
            if (legacyPrefix is not null && text.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = cache.GetBookTitleID(text, legacyPrefix);
                if (int.TryParse(token, out int bookId))
                {
                    var legacyById = GameServer.Database.SelectObject<DBBook>(
                        b => b.ID == bookId && b.IsInLibrary && b.Author != GUILD_REGISTER_AUTHOR);

                    if (legacyById != null)
                    {
                        ShowBookPreview(cache, legacyById);
                        return true;
                    }
                }
            }

            player.Out.SendMessage(LanguageMgr.Translate(player, "Librarian.ResponseText03"),
                                   eChatType.CT_System, eChatLoc.CL_PopupWindow);
            return true;
        }

        private async Task SendBookList(PlayerCache cache)
        {
            var player = cache.Player;
            var text1 = LanguageMgr.Translate(player, "Librarian.ResponseText01");
            var books = GameServer.Database
                .SelectObjects<DBBook>(b => b.IsInLibrary && b.Author != GUILD_REGISTER_AUTHOR)
                .OrderBy(b => b.Title);
            
            var sb = new StringBuilder(2048);

            if (books.Any())
            {
                var upvoteTask = LanguageMgr.Translate(player, "Librarian.BookList.VotesPrefixPositive");
                var downvoteTask = LanguageMgr.Translate(player, "Librarian.BookList.VotesPrefixNegative");
                
                var upvotes = await upvoteTask;
                var downvotes = await downvoteTask;

                player.Out.SendMessage(await text1, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                
                foreach (var b in books)
                {
                    string price = Money.GetString(b.CurrentPriceCopper);
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
                        .Append(b.UpVotes)
                        .Append(" / ")
                        .Append(downvotes)
                        .Append(' ')
                        .Append(b.DownVotes);

                    if (sb.Length > 1800)
                    {
                        player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        sb.Clear();
                    }
                }

                player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
            else
            {
                player.Out.SendMessage(await text1, eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
            cache.HasBooks = true;
        }

        private void SendGuildRegisterList(GamePlayer player)
        {
            var registers = GameServer.Database
                .SelectObjects<DBBook>(b => b.Author == GUILD_REGISTER_AUTHOR)
                .OrderBy(b => b.Title)
                .ToList();

            if (registers == null || registers.Count == 0)
            {
                player.Out.SendMessage(LanguageMgr.Translate(player, "Librarian.GuildRegister.None"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            player.Out.SendMessage(LanguageMgr.Translate(player, "Librarian.GuildRegister.Count", registers.Count),
                eChatType.CT_System, eChatLoc.CL_PopupWindow);

            var sb = new StringBuilder(2048);
            foreach (var b in registers)
            {
                sb.Append("\n[")
                  .Append(b.Title)
                  .Append("]");

                if (!string.IsNullOrWhiteSpace(b.StampBy) || b.StampDate != DateTime.MinValue)
                {
                    sb.Append(" - ")
                      .Append(LanguageMgr.Translate(player, "Librarian.GuildRegister.StampedBy"))
                      .Append(": ")
                      .Append(string.IsNullOrWhiteSpace(b.StampBy) ? "?" : b.StampBy);

                    if (b.StampDate != DateTime.MinValue)
                        sb.Append(" - ").Append(b.StampDate.ToString("yyyy-MM-dd HH:mm"));
                }

                if (sb.Length > 1800)
                {
                    player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
                player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        private async Task ShowBookPreview(PlayerCache cache, DBBook book)
        {
            var player = cache.Player;
            bool isAuthor = (book.PlayerID == player.InternalID);
            string price = Money.GetString(book.CurrentPriceCopper);
            cache.CurrentBook = book;

            var sb = new StringBuilder(1024);
            var taskTitle = cache.TranslateBookTitle(book);
            var taskAuthor = LanguageMgr.Translate(player, "Librarian.Preview.Author");
            var taskPrice = LanguageMgr.Translate(player, "Librarian.Preview.Price");
            var taskVotes = LanguageMgr.Translate(player, "Librarian.Preview.Votes");
            var taskVoteSep = LanguageMgr.Translate(player, "Librarian.Preview.VotesSeparator");
            var taskPreview = LanguageMgr.Translate(player, "Librarian.Preview.PreviewLabel");
            var taskText = cache.TranslateBookText(book);
            var taskUpvote = isAuthor ? null : cache.TranslatePrefixKey(INTERACT_KEY_PREFIX_VOTE_UP);
            var taskDownvote = isAuthor ? null : cache.TranslatePrefixKey(INTERACT_KEY_PREFIX_VOTE_DOWN);
            var taskBuy = isAuthor ? null : cache.TranslatePrefixKey(INTERACT_KEY_PREFIX_BUY);
            var taskRead = isAuthor ? cache.TranslatePrefixKey(INTERACT_KEY_PREFIX_BUY) : null;
            string preview = GetFirstWords(await taskText, PreviewWordsCount);
            string title = await taskTitle;

            sb.Append(title)
              .Append("\n")
              .Append(await taskAuthor + " ").Append(book.Author)
              .Append("\n")
              .Append(await taskPrice + " ").Append(price)
              .Append("\n")
              .Append(await taskVotes + " ").Append(book.UpVotes)
              .Append(await taskVoteSep + " ")
              .Append(book.DownVotes)
              .Append("\n\n")
              .Append(await taskPreview + " ")
              .Append(preview);

            player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);

            if (!isAuthor)
            {
                // Vote buttons
                player.Out.SendMessage($"[{await taskUpvote!}]  [{await taskDownvote!}]",
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);

                // Buy button
                player.Out.SendMessage($"[{await taskBuy!} {title}]",
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
            else
            {
                // Read button (author only)
                player.Out.SendMessage($"[{await taskRead!} {title}]",
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
        }

        private static string GetFirstWords(string text, int count)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "(...)";

            string normalized = Regex.Replace(text, @"\s+", " ").Trim();
            var words = normalized.Split(' ');

            if (words.Length <= count)
                return normalized;

            return string.Join(" ", words.Take(count)) + "...";
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            var p = source as GamePlayer;
            if (p == null || item == null)
                return false;

            if (string.IsNullOrEmpty(item.Id_nb) || !item.Id_nb.StartsWith("scroll", StringComparison.OrdinalIgnoreCase))
            {
                p.Out.SendMessage(LanguageMgr.Translate(p, "Librarian.ResponseText06"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            bool isRoyalScroll =
                item.Id_nb.StartsWith("scroll_royal", StringComparison.OrdinalIgnoreCase) ||
                (item.Template != null && item.Template.Id_nb.Equals("scroll_royal", StringComparison.OrdinalIgnoreCase));

            var book = GameServer.Database.FindObjectByKey<DBBook>(item.MaxCondition);
            if (book == null)
            {
                p.Out.SendMessage(LanguageMgr.Translate(p, "Librarian.ResponseText05"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (book.PlayerID != p.InternalID)
            {
                p.Out.SendMessage(LanguageMgr.Translate(p, "Librarian.ResponseText04"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (isRoyalScroll)
            {
                p.Out.SendCustomDialog(
                    LanguageMgr.Translate(p, "Librarian.RoyalScroll.Dialog"),
                    (ply, resp) =>
                    {
                        if (resp != 0x01)
                        {
                            ply.Out.SendMessage(LanguageMgr.Translate(ply, "Librarian.RoyalScroll.Cancelled"),
                                eChatType.CT_System, eChatLoc.CL_PopupWindow);
                            return;
                        }

                        HandleLibrarianBookHandIn(ply, book);
                    });

                return false;
            }

            HandleLibrarianBookHandIn(p, book);
            return false;
        }

        private void HandleLibrarianBookHandIn(GamePlayer p, DBBook book)
        {
            // Prevent publishing registers at all (safety)
            if (book.Author == GUILD_REGISTER_AUTHOR || book.IsGuildRegistry)
            {
                p.Out.SendMessage(LanguageMgr.Translate(p, "Librarian.Publish.RegisterBlocked"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            if (!book.IsInLibrary)
            {
                if (!TryPublishBook(p, book))
                    return;

                book.IsInLibrary = true;
                book.Save();

                UpdateAuthorUniqueItemPrice(book);

                p.Out.SendMessage(LanguageMgr.Translate(p, "Librarian.Publish.Added"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
            else
            {
                book.IsInLibrary = false;
                book.Save();

                p.Out.SendMessage(LanguageMgr.Translate(p, "Librarian.Publish.Removed"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
        }

        private bool TryPublishBook(GamePlayer author, DBBook book)
        {
            int words = BookUtils.CountWords(book.Text);
            if (words < MinWords)
            {
                author.Out.SendMessage(LanguageMgr.Translate(author, "Librarian.Publish.TooShort", MinWords, words),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (Properties.BOOK_ENABLE_PUBLISH_HEURISTICS &&
                BookUtils.LooksLikeGibberish(book.Text))
            {
                author.Out.SendMessage(LanguageMgr.Translate(author, "Librarian.Publish.Gibberish"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (BookUtils.ContainsProhibitedTerms(book.Text, out string bad))
            {
                author.Out.SendMessage(LanguageMgr.Translate(author, "Librarian.Publish.Prohibited", bad),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            book.WordCount = words;
            book.BasePriceCopper = ComputeBasePrice(words);

            book.CurrentPriceCopper = ApplyRating(book.BasePriceCopper, book.UpVotes, book.DownVotes);
            book.Save();

            UpdateAuthorUniqueItemPrice(book);

            return true;
        }

        private int ComputeBasePrice(int words)
        {
            int basePrice = BaseCopperAtMin;
            int price = (int)Math.Round((double)basePrice * words / Math.Max(1, MinWords));
            return Math.Max(basePrice, price);
        }

        /// <summary>
        /// +VoteStepPercent per upvote, -VoteStepPercent per downvote.
        /// Clamped to +/- MaxMultiplierPercent.
        /// Price is clamped to at least 25% of base price.
        /// </summary>
        private int ApplyRating(int basePrice, int upVotes, int downVotes)
        {
            int netVotes = upVotes - downVotes;
            int percent = netVotes * VoteStepPercent;

            if (percent > MaxMultiplierPercent) percent = MaxMultiplierPercent;
            if (percent < -MaxMultiplierPercent) percent = -MaxMultiplierPercent;

            double factor = 1.0 + (percent / 100.0);
            int priced = (int)Math.Round(basePrice * factor);

            int floor = Math.Max(1, (int)Math.Ceiling(basePrice * 0.25));
            return Math.Max(floor, priced);
        }

        private void BuyBook(PlayerCache cache, DBBook book)
        {
            cache.CurrentBook = book;
            var buyer = cache.Player;
            int priceCopper = book.CurrentPriceCopper;
            if (priceCopper <= 0)
            {
                buyer.Out.SendMessage(LanguageMgr.Translate(buyer, "Librarian.Buy.NotForSale"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            if (buyer.CopperBalance < priceCopper)
            {
                buyer.Out.SendMessage(LanguageMgr.Translate(buyer, "Librarian.Buy.NotEnoughMoney", Money.GetString(priceCopper)),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            var item = BooksMgr.CreateBookItem(book);
            if (item == null)
            {
                buyer.Out.SendMessage(LanguageMgr.Translate(buyer, "Librarian.Buy.CreateItemFail"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            if (item.Template != null)
                item.Template.Price = priceCopper;

            lock (buyer.Inventory)
            {
                var slot = buyer.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (slot == eInventorySlot.Invalid)
                {
                    buyer.Out.SendMessage(LanguageMgr.Translate(buyer, "Librarian.Buy.NoInventorySpace"),
                        eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return;
                }

                if (!buyer.RemoveMoney(Currency.Copper.Mint(priceCopper)))
                    return;

                buyer.Inventory.AddItem(slot, item);
                InventoryLogging.LogInventoryAction(this, buyer, eInventoryActionType.Merchant, item, 1);
            }

            book.RoyaltiesPendingCopper += priceCopper;
            book.TotalSold += 1;
            book.Save();

            Task.Run(async () =>
            {
                var taskSuccess = LanguageMgr.Translate(buyer, "Librarian.Buy.Success");
                var taskUpvote = cache.TranslatePrefixKey(INTERACT_KEY_PREFIX_VOTE_UP);
                var taskDownvote = cache.TranslatePrefixKey(INTERACT_KEY_PREFIX_VOTE_DOWN);
                var title = await cache.TranslateBookTitle(book);
                var success = string.Format(await taskSuccess, title, Money.GetString(priceCopper));

                buyer.Out.SendMessage(success,
                                      eChatType.CT_Merchant, eChatLoc.CL_PopupWindow);

                buyer.Out.SendMessage($"[{await taskUpvote}]  [{await taskDownvote}]",
                                      eChatType.CT_System, eChatLoc.CL_PopupWindow);
            });
        }

        private void Vote(PlayerCache cache, string? bookID, int voteValue)
        {
            var voter = cache.Player;
            DBBook? book = null;
            if (!string.IsNullOrEmpty(bookID))
            {
                book = GameServer.Database.SelectObject<DBBook>(
                    b => b.IsInLibrary && b.Title == bookID && b.Author != GUILD_REGISTER_AUTHOR);
            }

            if (book == null)
            {
                voter.Out.SendMessage(LanguageMgr.Translate(voter, "Librarian.Book.NotFound"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            if (book.PlayerID == voter.InternalID)
            {
                voter.Out.SendMessage(LanguageMgr.Translate(voter, "Librarian.Vote.CannotVoteOwn"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            var existing = GameServer.Database.SelectObject<DBBookVote>(
                v => v.BookID == book.ID && v.VoterPlayerID == voter.InternalID);

            if (existing != null)
            {
                voter.Out.SendMessage(LanguageMgr.Translate(voter, "Librarian.Vote.AlreadyVoted"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            var vote = new DBBookVote
            {
                BookID = book.ID,
                VoterPlayerID = voter.InternalID,
                VoteValue = voteValue,
                VoteDate = DateTime.UtcNow
            };

            GameServer.Database.AddObject(vote);

            if (voteValue > 0) book.UpVotes++;
            else book.DownVotes++;

            book.CurrentPriceCopper = ApplyRating(book.BasePriceCopper, book.UpVotes, book.DownVotes);
            book.Save();

            UpdateAuthorUniqueItemPrice(book);

            voter.Out.SendMessage(LanguageMgr.Translate(voter, "Librarian.Vote.Recorded"),
                eChatType.CT_System, eChatLoc.CL_PopupWindow);

            ShowBookPreview(cache, book);
        }

        private void UpdateAuthorUniqueItemPrice(DBBook book)
        {
            var unique = GameServer.Database.SelectObject<ItemUnique>(u => u.MaxCondition == (int)book.ID);
            if (unique == null)
                return;

            unique.Price = book.CurrentPriceCopper;
            GameServer.Database.SaveObject(unique);
        }

        private void CollectRoyalties(GamePlayer author)
        {
            var books = GameServer.Database
                .SelectObjects<DBBook>(b => b.PlayerID == author.InternalID && b.RoyaltiesPendingCopper > 0)
                .ToList();

            if (books.Count == 0)
            {
                author.Out.SendMessage(LanguageMgr.Translate(author, "Librarian.Royalties.None"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return;
            }

            long total = 0;
            foreach (var b in books)
            {
                total += b.RoyaltiesPendingCopper;
                b.RoyaltiesPendingCopper = 0;
                b.Save();
            }

            author.AddMoney(Currency.Copper.Mint(total));
            author.Out.SendMessage(LanguageMgr.Translate(author, "Librarian.Royalties.Collected", Money.GetString(total)),
                eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }
    }
}
