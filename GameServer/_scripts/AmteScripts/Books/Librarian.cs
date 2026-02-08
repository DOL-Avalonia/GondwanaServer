using DOL.Database;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        [NotNull] private readonly ConcurrentDictionary<string, IDictionary<string, string>> _playerResponseKeyMappings = new();
        
        private const string INTERACT_KEY_CONSULT_BOOKS = "Librarian.Menu.ConsultBooks";
        private const string INTERACT_KEY_CONSULT_GUILDS = "Librarian.Menu.ConsultGuildRegister";
        private const string INTERACT_KEY_ADD_BOOK = "Librarian.Menu.AddBook";
        private const string INTERACT_KEY_COLLECT_ROYALTIES = "Librarian.Menu.CollectRoyalties";
        private const string INTERACT_KEY_VOTE_UP = "Librarian.Prefix.VotePlus";
        private const string INTERACT_KEY_VOTE_DOWN = "Librarian.Prefix.VoteMinus";
        private const string INTERACT_KEY_BUY = "Librarian.Prefix.Buy";
        private const string INTERACT_KEY_READ = "Librarian.Prefix.Read";
        private const string INTERACT_KEY_LEGACYREAD = "Librarian.Prefix.LegacyBook";

        private async Task<string> TranslateResponseKey(GamePlayer player, string baseKey)
        {
            var mapping = _playerResponseKeyMappings.GetOrAdd(player.Name, _ => new ConcurrentDictionary<string, string>());
            if (true || !mapping!.TryGetValue(baseKey, out string mapped))
            {
                mapped = await LanguageMgr.Translate(player, baseKey);
                mapping[baseKey] = mapped;
            }
            return mapped;
        }

        private string? GetResponseKey(GamePlayer player, string baseKey, string orElse = null)
        {
            if (!_playerResponseKeyMappings.TryGetValue(player.Name, out IDictionary<string, string> mappings))
                return orElse;

            if (!mappings!.TryGetValue(baseKey, out string response))
                return orElse;

            return response;
        }

        private string? GetResponsePrefix(GamePlayer player, string baseKey, string orElse = null)
        {
            string? response = GetResponseKey(player, baseKey, orElse);
            if (Equals(response, orElse))
                return orElse;

            return response + " ";
        }

        private string PrefixVotePlus(GamePlayer p) => GetResponsePrefix(p, INTERACT_KEY_VOTE_UP);
        private string PrefixVoteMinus(GamePlayer p) => GetResponsePrefix(p, INTERACT_KEY_VOTE_DOWN);
        private string PrefixBuy(GamePlayer p) => GetResponsePrefix(p, INTERACT_KEY_BUY);
        private string PrefixRead(GamePlayer p) => GetResponsePrefix(p, INTERACT_KEY_READ);
        private string PrefixLegacy(GamePlayer p) => GetResponsePrefix(p, INTERACT_KEY_LEGACYREAD);

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            Task.Run(async () =>
            {
                // Start them all... in parallel, so that if translation is slow it doesn't delay the first ones.
                var text1 = LanguageMgr.Translate(player, "Librarian.InteractText01");
                var text2 = LanguageMgr.Translate(player, "Librarian.InteractText02");
                var consultBooks = TranslateResponseKey(player, INTERACT_KEY_CONSULT_BOOKS);
                var consultGuilds = Properties.GUILD_REQUIRE_REGISTER ? TranslateResponseKey(player, INTERACT_KEY_CONSULT_GUILDS) : null;
                var addBook = TranslateResponseKey(player, INTERACT_KEY_ADD_BOOK);
                var royalties = TranslateResponseKey(player, INTERACT_KEY_COLLECT_ROYALTIES);
                
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
                
                // Translate those for the responses...
                await TranslateResponseKey(player, INTERACT_KEY_VOTE_UP);
                await TranslateResponseKey(player, INTERACT_KEY_VOTE_DOWN);
                await TranslateResponseKey(player, INTERACT_KEY_BUY);
                await TranslateResponseKey(player, INTERACT_KEY_READ);
                await TranslateResponseKey(player, INTERACT_KEY_LEGACYREAD);
                
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

            text = (text ?? string.Empty).Trim();

            // Menu labels
            var consultBooks = GetResponseKey(player, INTERACT_KEY_CONSULT_BOOKS);
            var consultGuilds = Properties.GUILD_REQUIRE_REGISTER ? GetResponseKey(player, INTERACT_KEY_CONSULT_GUILDS) : null;
            var addBook = GetResponseKey(player, INTERACT_KEY_ADD_BOOK);
            var royalties = GetResponseKey(player, INTERACT_KEY_COLLECT_ROYALTIES);

            // Consult register list
            if (Properties.GUILD_REQUIRE_REGISTER)
            {
                if (Eq(text, consultGuilds))
                {
                    SendGuildRegisterList(player);
                    return true;
                }
                
                // Click on a register title -> show FULL TEXT
                var regClicked = GameServer.Database.SelectObject<DBBook>(b => b.Title == text && b.Author == GUILD_REGISTER_AUTHOR);
                if (regClicked != null)
                {
                    BooksMgr.ReadBook(player, regClicked);
                    return true;
                }
            }

            // List normal books
            if (Eq(text, consultBooks))
            {
                SendBookList(player);
                return true;
            }

            if (Eq(text, addBook))
            {
                Task.Run(async () =>
                {
                    player.Out.SendMessage(await LanguageMgr.Translate(player, "Librarian.ResponseText02"),
                                           eChatType.CT_System, eChatLoc.CL_PopupWindow);
                });
                return true;
            }

            if (Eq(text, royalties))
            {
                CollectRoyalties(player);
                return true;
            }

            // Author-only full read shortcut: "Read "
            string readPrefix = PrefixRead(player);
            if (readPrefix is not null && text.StartsWith(readPrefix, StringComparison.OrdinalIgnoreCase))
            {
                Task.Run(async () =>
                {
                    string title = text.Substring(readPrefix.Length).Trim();

                    var bookToRead = GameServer.Database.SelectObject<DBBook>(b => b.IsInLibrary && b.Title == title);
                    if (bookToRead == null)
                    {
                        player.Out.SendMessage(await LanguageMgr.Translate(player, "Librarian.Book.NotFound"),
                                               eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return;
                    }

                    if (bookToRead.PlayerID != player.InternalID)
                    {
                        player.Out.SendMessage(await LanguageMgr.Translate(player, "Librarian.Read.AuthorOnly"),
                                               eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return;
                    }

                    BooksMgr.ReadBook(player, bookToRead);
                });
                return true;
            }

            // Vote + / Vote -
            string votePlusPrefix = PrefixVotePlus(player);
            string voteMinusPrefix = PrefixVoteMinus(player);
            if (votePlusPrefix is not null && text.StartsWith(votePlusPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var title = text.Substring(votePlusPrefix.Length).Trim();
                Vote(player, title, +1);
                return true;
            }

            if (voteMinusPrefix is not null && text.StartsWith(voteMinusPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var title = text.Substring(voteMinusPrefix.Length).Trim();
                Vote(player, title, -1);
                return true;
            }

            // Buy
            string buyPrefix = PrefixBuy(player);
            if (buyPrefix is not null && text.StartsWith(buyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string title = text.Substring(buyPrefix.Length).Trim();

                // Todo, review these async tasks; can we wrap BuyBook in there? It adds to inventory, is the inventory threadsafe?
                var bookToBuy = GameServer.Database.SelectObject<DBBook>(b => b.IsInLibrary && b.Title == title);
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

                BuyBook(player, bookToBuy);
                return true;
            }

            // Clicking on a book title -> preview
            var clicked = GameServer.Database.SelectObject<DBBook>(
                b => b.IsInLibrary && b.Title == text && b.Author != GUILD_REGISTER_AUTHOR);

            if (clicked != null)
            {
                ShowBookPreview(player, clicked);
                return true;
            }

            // "Book <id>" -> preview
            string legacyPrefix = PrefixLegacy(player);
            if (legacyPrefix is not null && text.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = text.Substring(legacyPrefix.Length).Trim();
                if (int.TryParse(token, out int bookId))
                {
                    var legacyById = GameServer.Database.SelectObject<DBBook>(
                        b => b.ID == bookId && b.IsInLibrary && b.Author != GUILD_REGISTER_AUTHOR);

                    if (legacyById != null)
                    {
                        ShowBookPreview(player, legacyById);
                        return true;
                    }
                }
            }

            Task.Run(async () =>
            {
                player.Out.SendMessage(await LanguageMgr.Translate(player, "Librarian.ResponseText03"),
                                       eChatType.CT_System, eChatLoc.CL_PopupWindow);
            });
            return true;
        }

        private async Task SendBookList(GamePlayer player)
        {
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

                    sb.Append("\n[")
                        .Append(b.Title)
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

        private void ShowBookPreview(GamePlayer player, DBBook book)
        {
            string price = Money.GetString(book.CurrentPriceCopper);
            string preview = GetFirstWords(book.Text, PreviewWordsCount);

            var sb = new StringBuilder(1024);

            sb.Append(book.Title)
              .Append("\n")
              .Append(LanguageMgr.Translate(player, "Librarian.Preview.Author") + " ").Append(book.Author)
              .Append("\n")
              .Append(LanguageMgr.Translate(player, "Librarian.Preview.Price") + " ").Append(price)
              .Append("\n")
              .Append(LanguageMgr.Translate(player, "Librarian.Preview.Votes") + " ").Append(book.UpVotes)
              .Append(LanguageMgr.Translate(player, "Librarian.Preview.VotesSeparator") + " ")
              .Append(book.DownVotes)
              .Append("\n\n")
              .Append(LanguageMgr.Translate(player, "Librarian.Preview.PreviewLabel") + " ")
              .Append(preview);

            player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);

            bool isAuthor = (book.PlayerID == player.InternalID);

            if (!isAuthor)
            {
                // Vote buttons
                player.Out.SendMessage($"[{PrefixVotePlus(player)}{book.Title}]  [{PrefixVoteMinus(player)}{book.Title}]",
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);

                // Buy button
                player.Out.SendMessage($"[{PrefixBuy(player)}{book.Title}]",
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
            else
            {
                // Read button (author only)
                player.Out.SendMessage($"[{PrefixRead(player)}{book.Title}]",
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

        private void BuyBook(GamePlayer buyer, DBBook book)
        {
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

            buyer.Out.SendMessage(LanguageMgr.Translate(buyer, "Librarian.Buy.Success", book.Title, Money.GetString(priceCopper)),
                eChatType.CT_Merchant, eChatLoc.CL_PopupWindow);

            buyer.Out.SendMessage($"[{PrefixVotePlus(buyer)}{book.Title}]  [{PrefixVoteMinus(buyer)}{book.Title}]",
                eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        private void Vote(GamePlayer voter, string title, int voteValue)
        {
            var book = GameServer.Database.SelectObject<DBBook>(
                b => b.IsInLibrary && b.Title == title && b.Author != GUILD_REGISTER_AUTHOR);

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

            ShowBookPreview(voter, book);
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
