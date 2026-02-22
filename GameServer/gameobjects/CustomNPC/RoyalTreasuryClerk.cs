#nullable enable

using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS
{
    public class RoyalTreasuryClerk : AbstractLibrarian
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private const string PERSONAL_RECALL_STONE_ID = "Personal_Bind_Recall_Stone";
        private const string WHISPER_CONTINUE = "continue";

        private const string TP_BOOK_ID = "GuildReg_BookId";
        private const string TP_STEP = "GuildReg_Step";
        private const string TP_MEMBER_INDEX = "GuildReg_MemberIndex";
        private const string TP_AWAIT_STAMP = "GuildReg_AwaitStamp";
        private const string TP_USED_ACCOUNTS = "GuildReg_UsedAccounts";

        private const int STEP_NONE = 0;
        private const int STEP_COLLECT_LEADER = 1;
        private const int STEP_COLLECT_MEMBERS = 2;
        private const int STEP_CONFIRM_GUILDNAME = 3;
        private const int STEP_AWAIT_STAMP = 4;

        private const string TAG_PROCESSING = "#processing";
        private const string TAG_STAMPED = "#GuildStamped";
        private const string TAG_LEADER_PREFIX = "#GuildLeader_";

        private const string INTERACT_KEY_REGISTER = "RoyalTreasuryClerk.Keyword.GuildRegister";
        private const string INTERACT_KEY_STONE = "RoyalTreasuryClerk.Other";

        private static bool HasRecallStone(GamePlayer player)
        {
            return player.Inventory.CountItemTemplate(PERSONAL_RECALL_STONE_ID, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) > 0;
        }

        private static bool IsRoyalScrollItem(InventoryItem? item)
        {
            if (item == null)
                return false;

            if (!string.IsNullOrEmpty(item.Id_nb) &&
                item.Id_nb.StartsWith("scroll_royal", StringComparison.OrdinalIgnoreCase))
                return true;

            if (item.Template != null &&
                !string.IsNullOrEmpty(item.Template.Id_nb) &&
                item.Template.Id_nb.Equals("scroll_royal", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static bool BookHasTag(DBBook? book, string tag)
        {
            if (string.IsNullOrEmpty(book?.Text)) return false;
            return book.Text.Contains(tag, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureProcessingTag(DBBook? book)
        {
            if (book == null) return;
            if (string.IsNullOrEmpty(book.Text)) book.Text = string.Empty;

            if (!BookHasTag(book, TAG_PROCESSING))
            {
                book.Text += "\n" + TAG_PROCESSING + "\n";
            }
        }

        private static void RemoveProcessingTag(DBBook? book)
        {
            if (book == null || string.IsNullOrEmpty(book.Text)) return;
            // Remove whole lines that equal "#processing"
            var lines = book.Text.Replace("\r", "").Split('\n');
            var kept = new List<string>(lines.Length);
            foreach (var l in lines)
            {
                if (l.Trim().Equals(TAG_PROCESSING, StringComparison.OrdinalIgnoreCase))
                    continue;
                kept.Add(l);
            }
            book.Text = string.Join("\n", kept);
        }

        /// <summary>
        /// Finds the register item in inventory that points to the given DBBook ID.
        /// </summary>
        private InventoryItem? FindInventoryItemForBook(GamePlayer? player, long bookId)
        {
            if (player == null || bookId <= 0) return null;

            for (var slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                var it = player.Inventory.GetItem(slot);
                if (it == null) continue;
                if (!IsRoyalScrollItem(it)) continue;
                if (it.MaxCondition != (int)bookId) continue;
                return it;
            }

            return null;
        }

        /// <summary>
        /// Applies state directly to the *InventoryItem instance*,
        /// because existing inventory items do not automatically inherit template changes.
        /// </summary>
        private void ApplyUniqueState(GamePlayer? player, InventoryItem? invItem, string statePrefix, string title, bool pickable, bool dropable, bool tradable)
        {
            if (player == null || invItem == null)
                return;

            string newName = $"{statePrefix} {title}".Trim();

            invItem.Name = newName;
            invItem.IsPickable = pickable;
            invItem.IsDropable = dropable;
            invItem.IsTradable = tradable;

            GameServer.Database.SaveObject(invItem);
            player.Out.SendInventoryItemsUpdate([invItem]);
            player.Out.SendInventorySlotsUpdate([invItem.SlotPosition]);
        }

        private Task<string>? ValidateInitialBook(GamePlayer player, InventoryItem? item, DBBook? book)
        {
            if (item == null || book == null)
            {
                return LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.NotRegister");
            }

            // Parchment check
            if (!IsRoyalScrollItem(item))
            {
                return LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.NotParchment");
            }

            // Ink check
            if (!string.Equals(book.InkId, "ink_royal", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.NotInk");
            }

            if (player.Client.Account.PrivLevel <= 1)
            {
                // Minimum words
                int minWords = Properties.GUILD_REGISTER_MIN_WORDS;
                int wc = BookUtils.CountWords(book.Text);
                if (wc < minWords)
                {
                    return LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.TooShort", minWords, wc);
                }

                // Deep nonsense/spam check
                if (Properties.BOOK_ENABLE_PUBLISH_HEURISTICS && BookUtils.LooksLikeGibberish(book.Text))
                {
                    return LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.Gibberish");
                }

                // Prohibited content
                if (BookUtils.ContainsProhibitedTerms(book.Text, out string bad))
                {
                    return LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.Prohibited", bad);
                }

                if (BookUtils.ContainsProhibitedTerms(book.Title, out string badTitle))
                {
                    return LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.TitleProhibited", badTitle);
                }
            }

            // Title validity
            if (string.IsNullOrWhiteSpace(book.Title) ||
                book.Title.Length > Guild.MAX_CREATE_NAME_LENGTH ||
                !Commands.GuildCommandHandler.IsValidGuildName(book.Title))
            {
                return LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.NameInvalid");
            }

            if (GuildMgr.DoesGuildExist(book.Title))
            {
                return LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.NameTaken");
            }
            return null;
        }

        private DBBook? GetRegisterFromItem(InventoryItem item)
        {
            if (!IsRoyalScrollItem(item))
                return null;

            if (item.MaxCondition <= 0)
                return null;

            return GameServer.Database.FindObjectByKey<DBBook>((long)item.MaxCondition);
        }

        private InventoryItem? FindPlayerRegisterBook(GamePlayer? player, out DBBook? book)
        {
            book = null;
            if (player == null) return null;

            for (var slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                var it = player.Inventory.GetItem(slot);
                var b = GetRegisterFromItem(it);
                if (b is null)
                    continue;

                // Must be processing for resume
                if (!BookHasTag(b, TAG_PROCESSING))
                    continue;

                // If already stamped, not an "in-progress" candidate
                if (b.IsStamped || BookHasTag(b, TAG_STAMPED))
                    continue;

                book = b;
                return it;
            }

            return null;
        }

        private bool TryResumeFromInventory(GamePlayer player, InventoryItem? invItem, out DBBook? book)
        {
            book = null;

            invItem ??= FindPlayerRegisterBook(player, out book);
            if (invItem == null || book == null)
                return false;

            int required = Properties.GUILD_NUM;
            var founders = BookUtils.ExtractFounders(book.Text, required);

            player.TempProperties.setProperty(TP_BOOK_ID, (int)book.ID);
            RebuildUsedAccountsFromFounders(player, founders);

            if (string.IsNullOrWhiteSpace(founders.leader))
            {
                player.TempProperties.setProperty(TP_STEP, STEP_COLLECT_LEADER);
                player.TempProperties.setProperty(TP_MEMBER_INDEX, 0);
                player.TempProperties.setProperty(TP_AWAIT_STAMP, false);
            }
            else if (founders.members.Count < required - 1)
            {
                player.TempProperties.setProperty(TP_STEP, STEP_COLLECT_MEMBERS);
                player.TempProperties.setProperty(TP_MEMBER_INDEX, founders.members.Count + 1);
                player.TempProperties.setProperty(TP_AWAIT_STAMP, false);
            }
            else
            {
                // Founders complete -> await stamp
                player.TempProperties.setProperty(TP_STEP, STEP_AWAIT_STAMP);
                player.TempProperties.setProperty(TP_AWAIT_STAMP, true);
            }

            return true;
        }

        private void RebuildUsedAccountsFromFounders(GamePlayer player, (string leader, List<string> members) founders)
        {
            try
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(founders.leader))
                {
                    var ch = BookUtils.GetCharacter(founders.leader);
                    if (ch != null)
                    {
                        string acc = BookUtils.GetAccountName(ch);
                        if (!string.IsNullOrWhiteSpace(acc)) used.Add(acc);
                    }
                }

                foreach (var m in founders.members)
                {
                    if (string.IsNullOrWhiteSpace(m)) continue;
                    var ch = BookUtils.GetCharacter(m);
                    if (ch != null)
                    {
                        string acc = BookUtils.GetAccountName(ch);
                        if (!string.IsNullOrWhiteSpace(acc)) used.Add(acc);
                    }
                }

                player.TempProperties.setProperty(TP_USED_ACCOUNTS, string.Join("|", used));
            }
            catch { }
        }

        private (InventoryItem? book, bool success) EnsureBookStillPresentOrReset(GamePlayer player)
        {
            int bookId = player.TempProperties.getProperty<int>(TP_BOOK_ID, 0);
            int step = player.TempProperties.getProperty<int>(TP_STEP, STEP_NONE);

            if (step == STEP_NONE || bookId <= 0)
                return (null, true);

            var book = FindInventoryItemForBook(player, bookId);
            if (book == null)
            {
                ResetState(player);
                SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Lost"));
                return (null, false);
            }

            return (book, true);
        }

        private void ShowResumePrompt(GamePlayer player, DBBook book)
        {
            int required = Properties.GUILD_NUM;
            var founders = BookUtils.ExtractFounders(book.Text, required);

            int step = player.TempProperties.getProperty<int>(TP_STEP, STEP_NONE);
            if (step == STEP_COLLECT_LEADER)
            {
                SayTo(player, [
                    LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Leader.Line1"),
                    LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Leader.Line2"),
                    LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Leader.Line3")
                ]);
            }
            else if (step == STEP_COLLECT_MEMBERS)
            {
                int idx = player.TempProperties.getProperty<int>(TP_MEMBER_INDEX, founders.members.Count + 1);
                SayTo(player, [
                    LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Member.Line1", founders.leader),
                    LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Member.Line2", idx, required - 1)
                ]);
            }
            else if (step == STEP_AWAIT_STAMP)
            {
                SayTo(player, [
                     LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Stamp.Line1"),
                     LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Stamp.Line2")
                ]);
            }
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player) || player == null)
                return false;

            if (Properties.GUILD_REQUIRE_REGISTER)
            {
                // TODO: Why does the AI discard the boolean here?
                //        scratch that, TODO rewrite this with a human brain
                var (bookItem, _) = EnsureBookStillPresentOrReset(player);

                // Only resume if the book is explicitly in "#processing"
                if (TryResumeFromInventory(player, bookItem, out DBBook activeBook))
                {
                    ShowResumePrompt(player, activeBook);
                    return true;
                }

                var hasStone = GlobalConstants.IsExpansionEnabled((int)eClientExpansion.DarknessRising) ? HasRecallStone(player) : false;
                Task.Run(async () =>
                {
                    var cache = EnsurePlayerCache(player);
                    
                    // Start them now so they can run in parallel
                    var guildTask = cache.TranslateResponseKey(INTERACT_KEY_REGISTER);
                    var line1Task = LanguageMgr.Translate(player, "RoyalTreasuryClerk.Interact.Intro.Line1");
                    var line2Task = LanguageMgr.Translate(player, "RoyalTreasuryClerk.Interact.Intro.Line2", guildTask);

                    if (!GlobalConstants.IsExpansionEnabled((int)eClientExpansion.DarknessRising))
                    {
                        SayTo(player, [
                            await line1Task,
                            await line2Task
                        ]);
                        return;
                    }

                    var stoneKey = hasStone ? "RoyalTreasuryClerk.Stone.HaveOne" : "RoyalTreasuryClerk.Stone.Missing";
                    var stoneTask = LanguageMgr.TranslateWithPlaceholders(player, stoneKey);
                    var anotherKey = LanguageMgr.GetTranslationOrDefaultLang(player, "RoyalTreasuryClerk.Other");
                    var checkingTask = LanguageMgr.Translate(player, "RoyalTreasuryClerk.Stone.Checking");

                    var checking = await checkingTask;
                    var (stoneLine, keys) = await stoneTask;
                    string? another = null;
                    if (keys != null && !keys.TryGetValue(anotherKey, out another))
                    {
                        another = keys.FirstOrDefault().Value;
                        if (keys.Count != 1)
                        {
                            log.WarnFormat("Detected {keys.Count} keywords in {stoneKey} \"{stoneLine}\" for player {player}, will use (\"{another}\")");
                        }
                    }

                    if (another != null)
                        cache.AddResponseKey(INTERACT_KEY_STONE, another);

                    SayTo(player, [
                        await line1Task,
                        await line2Task
                    ]);

                    SayTo(player, [
                        checking,
                        stoneLine
                    ]);
                });
                return true;
            }

            // MODE B: guild register OFF
            if (GlobalConstants.IsExpansionEnabled((int)eClientExpansion.DarknessRising))
            {
                player.Out.SendMessage(LanguageMgr.Translate(player, "RoyalTreasuryClerk.Checking"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);

                if (!HasRecallStone(player))
                {
                    player.Out.SendMessage(LanguageMgr.Translate(player, "RoyalTreasuryClerk.Nostone"),
                        eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }

                return true;
            }

            return false;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text) || source is not GamePlayer player)
                return false;

            bool drEnabled = GlobalConstants.IsExpansionEnabled((int)eClientExpansion.DarknessRising);
            bool registerEnabled = Properties.GUILD_REQUIRE_REGISTER;
            if (!drEnabled && !registerEnabled)
                return true;

            var cache = GetPlayerCache(player);
            if (cache != null)
            {
                var keyword = cache.GetResponseKey(text);
                switch (keyword)
                {
                    // Start guild legalization flow
                    case INTERACT_KEY_REGISTER when registerEnabled:
                        {
                            SayTo(player, [
                                LanguageMgr.Translate(player, "RoyalTreasuryClerk.Whisper.Guild.Line1"),
                                LanguageMgr.Translate(player, "RoyalTreasuryClerk.Whisper.Guild.Line2"),
                                LanguageMgr.Translate(player, "RoyalTreasuryClerk.Whisper.Guild.Line3")
                            ]);
                            return true;
                        }

                    // Bind stone service (DR)
                    case INTERACT_KEY_STONE when drEnabled:
                        {
                            if (!HasRecallStone(player))
                            {
                                SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Stonegive"));
                                player.ReceiveItem(this, PERSONAL_RECALL_STONE_ID, eInventoryActionType.Other);
                            }
                            return true;
                        }
                }
            }

            text = (text ?? "").Trim();
            if (registerEnabled)
            {
                int step = player.TempProperties.getProperty<int>(TP_STEP, STEP_NONE);

                if (step == STEP_NONE)
                {
                    TryResumeFromInventory(player, null, out _);
                    step = player.TempProperties.getProperty<int>(TP_STEP, STEP_NONE);
                }

                if (text.Equals(WHISPER_CONTINUE, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryResumeFromInventory(player, null, out DBBook activeBook))
                        ShowResumePrompt(player, activeBook);
                    else
                        SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.NoActive"));
                    return true;
                }

                if (step != STEP_NONE)
                {
                    var (_, success) = EnsureBookStillPresentOrReset(player);
                    if (!success)
                        return true;

                    return HandleConversation(player, text);
                }
            }
            return true;
        }

        // ReceiveItem: start processing OR stamp
        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (source is not GamePlayer player || item == null)
                return false;

            if (!Properties.GUILD_REQUIRE_REGISTER)
                return false;

            // TODO: We have the item, so why do we look it up here again, and also AGAIN at the end?
            var (bookItem, _) = EnsureBookStillPresentOrReset(player);

            bool awaitingStamp = player.TempProperties.getProperty<bool>(TP_AWAIT_STAMP, false);
            if (awaitingStamp)
            {
                if (!item.Id_nb.Equals("guild_stamp", StringComparison.OrdinalIgnoreCase))
                {
                    SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Stamp.WrongItem"));
                    return true;
                }

                if (!player.Inventory.RemoveItem(item))
                {
                    SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Stamp.TakeFail"));
                    return true;
                }

                FinalizeStamp(player);
                return true;
            }

            long bookId = item.MaxCondition;
            if (bookId <= 0)
            {
                SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.NotRegister"));
                return true;
            }

            DBBook book = GameServer.Database.FindObjectByKey<DBBook>(bookId);
            if (book == null)
            {
                SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Stamp.NoLedger"));
                return true;
            }

            // If already stamped, clerk does nothing with it
            if (book.IsStamped || BookHasTag(book, TAG_STAMPED))
            {
                SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Stamp.AlreadyDone"));
                return true;
            }

            // Only enter the step-based conversation after FULL validation AND after adding #processing.
            // If book is not yet processing, we validate now and then mark it processing.
            if (!BookHasTag(book, TAG_PROCESSING))
            {
                var failTask = ValidateInitialBook(player, item, book);
                if (failTask != null)
                {
                    SayTo(player, failTask);
                    return true;
                }

                // Mark draft registry + processing tag
                book.IsGuildRegistry = true;
                book.IsStamped = false;
                book.StampBy = string.Empty;
                book.StampDate = DateTime.MinValue;
                EnsureProcessingTag(book);
                book.Save();

                // Change unique item to [Processing] and lock it
                ApplyUniqueState(player, item, "[Processing]", book.Title, true, false, false);

                player.TempProperties.setProperty(TP_BOOK_ID, (int)bookId);
                player.TempProperties.setProperty(TP_STEP, STEP_COLLECT_LEADER);
                player.TempProperties.setProperty(TP_MEMBER_INDEX, 0);
                player.TempProperties.setProperty(TP_AWAIT_STAMP, false);
                player.TempProperties.setProperty(TP_USED_ACCOUNTS, string.Empty);

                SayTo(player, [
                    LanguageMgr.Translate(player, "RoyalTreasuryClerk.Process.Start.Line1"),
                    LanguageMgr.Translate(player, "RoyalTreasuryClerk.Process.Start.Line2"),
                    LanguageMgr.Translate(player, "RoyalTreasuryClerk.Process.Start.Line3")
                ]);
                return true;
            }

            // If already processing, resume prompt based on tags
            player.TempProperties.setProperty(TP_BOOK_ID, (int)bookId);
            TryResumeFromInventory(player, null, out _);
            ShowResumePrompt(player, book);
            return true;
        }

        private bool HandleConversation(GamePlayer player, string text)
        {
            int step = player.TempProperties.getProperty<int>(TP_STEP, STEP_NONE);
            int bookId = player.TempProperties.getProperty<int>(TP_BOOK_ID, 0);
            DBBook book = DOLDB<DBBook>.SelectObject(DB.Column(nameof(DBBook.ID)).IsEqualTo((long)bookId));

            if (book == null || !BookHasTag(book, TAG_PROCESSING))
            {
                ResetState(player);
                SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Lost"));
                return true;
            }

            int required = Properties.GUILD_NUM;
            Task<string>? failMsg;

            switch (step)
            {
                case STEP_COLLECT_LEADER:
                    {
                        failMsg = TryValidateFounder(player, text, out var ch);
                        if (failMsg != null)
                        {
                            SayTo(player, failMsg);
                            return true;
                        }

                        book.Text = RemoveTagLines(book.Text, TAG_LEADER_PREFIX);
                        book.Text += $"\n{TAG_LEADER_PREFIX}{ch.Name}\n";
                        book.Save();

                        AddUsedAccount(player, ch.AccountName);

                        player.TempProperties.setProperty(TP_STEP, STEP_COLLECT_MEMBERS);
                        player.TempProperties.setProperty(TP_MEMBER_INDEX, 1);

                        SayTo(player, [
                             LanguageMgr.Translate(player, "RoyalTreasuryClerk.Process.LeaderSaved.Line1", ch.Name),
                             LanguageMgr.Translate(player, "RoyalTreasuryClerk.Process.LeaderSaved.Line2", required - 1)
                        ]);
                        return true;
                    }

                case STEP_COLLECT_MEMBERS:
                    {
                        int idx = player.TempProperties.getProperty<int>(TP_MEMBER_INDEX, 1);

                        if (idx < 1 || idx > required - 1)
                        {
                            player.TempProperties.setProperty(TP_STEP, STEP_CONFIRM_GUILDNAME);
                            return ConfirmGuildName(player, book);
                        }

                        failMsg = TryValidateFounder(player, text, out var ch);
                        if (failMsg != null)
                        {
                            SayTo(player, failMsg);
                            return true;
                        }

                        var founders = BookUtils.ExtractFounders(book.Text, required);
                        var allNames = new List<string>();
                        if (!string.IsNullOrWhiteSpace(founders.leader)) allNames.Add(founders.leader);
                        allNames.AddRange(founders.members);

                        if (allNames.Any(n => n.Equals(ch.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Process.DuplicateMember"));
                            return true;
                        }

                        book.Text += $"\n#GuildMember{idx:00}_{ch.Name}\n";
                        book.Save();

                        AddUsedAccount(player, ch.AccountName);

                        idx++;
                        player.TempProperties.setProperty(TP_MEMBER_INDEX, idx);

                        if (idx <= required - 1)
                            SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Process.MemberSaved", idx, required - 1));
                        else
                        {
                            player.TempProperties.setProperty(TP_STEP, STEP_CONFIRM_GUILDNAME);
                            ConfirmGuildName(player, book);
                        }

                        return true;
                    }

                case STEP_CONFIRM_GUILDNAME:
                    {
                        string newName = text?.Trim();
                        if (string.IsNullOrWhiteSpace(newName) ||
                            newName.Length > Guild.MAX_CREATE_NAME_LENGTH ||
                            !Commands.GuildCommandHandler.IsValidGuildName(newName))
                        {
                            SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.NameInvalid"));
                            return true;
                        }
                        if (GuildMgr.DoesGuildExist(newName))
                        {
                            SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Validate.NameTaken"));
                            return true;
                        }

                        book.Title = newName;
                        book.Name = $"[REGISTER] {newName}";
                        book.Save();

                        // Also update item name while processing
                        var invItem = FindInventoryItemForBook(player, book.ID);
                        if (invItem != null)
                        {
                            ApplyUniqueState(player, invItem, "[Processing]", book.Title, true, false, false);
                        }

                        return ConfirmGuildName(player, book);
                    }

                case STEP_AWAIT_STAMP:
                default:
                    // If we reach here without a dialog, guide them
                    player.TempProperties.setProperty(TP_STEP, STEP_AWAIT_STAMP);
                    player.TempProperties.setProperty(TP_AWAIT_STAMP, true);
                    SayTo(player, [
                        LanguageMgr.Translate(player, "RoyalTreasuryClerk.Process.AwaitStamp.Line1"),
                        LanguageMgr.Translate(player, "RoyalTreasuryClerk.Process.AwaitStamp.Line2")
                    ]);
                    return true;
            }
        }

        private bool ConfirmGuildName(GamePlayer player, DBBook book)
        {
            Task.Run(async () =>
            {
                var line1Task = LanguageMgr.Translate(player, "RoyalTreasuryClerk.Confirm.Dialog.Line1");
                var line2Task = LanguageMgr.Translate(player, "RoyalTreasuryClerk.Confirm.Dialog.Line2");
                player.Out.SendCustomDialog(
                    await line1Task  +"\n\n" + " " +
                    book.Title + " " + "\n\n" +
                    await line2Task ,
                    (ply, resp) =>
                    {
                        if (resp != 0x01)
                        {
                            SayTo(ply, LanguageMgr.Translate(ply, "RoyalTreasuryClerk.Confirm.Declined"));
                            ply.TempProperties.setProperty(TP_STEP, STEP_CONFIRM_GUILDNAME);
                            return;
                        }

                        ply.TempProperties.setProperty(TP_STEP, STEP_AWAIT_STAMP);
                        ply.TempProperties.setProperty(TP_AWAIT_STAMP, true);

                        SayTo(ply, [
                              LanguageMgr.Translate(ply, "RoyalTreasuryClerk.Process.AwaitStamp.Line1"),
                              LanguageMgr.Translate(ply, "RoyalTreasuryClerk.Process.AwaitStamp.Line2")
                        ]);
                    });
                
            });

            return true;
        }

        private bool FinalizeStamp(GamePlayer player)
        {
            int bookId = player.TempProperties.getProperty<int>(TP_BOOK_ID, 0);
            DBBook book = DOLDB<DBBook>.SelectObject(DB.Column(nameof(DBBook.ID)).IsEqualTo((long)bookId));

            if (book == null || !BookHasTag(book, TAG_PROCESSING))
            {
                ResetState(player);
                SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Resume.Lost"));
                return false;
            }

            int required = Properties.GUILD_NUM;
            var founders = BookUtils.ExtractFounders(book.Text, required);
            if (string.IsNullOrWhiteSpace(founders.leader) || founders.members.Count != required - 1)
            {
                SayTo(player, LanguageMgr.Translate(player, "RoyalTreasuryClerk.Stamp.Incomplete"));
                return false;
            }

            book.IsGuildRegistry = true;
            book.IsStamped = true;
            book.StampBy = Name;
            book.StampDate = DateTime.Now;

            // Mark stamped tag and remove processing tag for clarity
            if (!BookHasTag(book, TAG_STAMPED))
                book.Text += "\n" + TAG_STAMPED + "\n";
            RemoveProcessingTag(book);

            book.Save();

            var invItem = FindInventoryItemForBook(player, book.ID);
            if (invItem != null)
            {
                ApplyUniqueState(player, invItem, "[Stamped]", book.Title, false, false, false);
            }

            SayTo(player, [
                LanguageMgr.Translate(player, "RoyalTreasuryClerk.Stamp.Success.Line1"),
                LanguageMgr.Translate(player, "RoyalTreasuryClerk.Stamp.Success.Line2")
            ]);

            ResetState(player);
            return true;
        }

        private Task<string>? TryValidateFounder(GamePlayer requester, string inputName, out DOLCharacters ch)
        {
            ch = null;

            string name = (inputName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return LanguageMgr.Translate(requester, "RoyalTreasuryClerk.Founder.NoName");
            }

            ch = BookUtils.GetCharacter(name);
            if (ch == null)
            {
                return LanguageMgr.Translate(requester, "RoyalTreasuryClerk.Founder.NotFound");
            }

            if (!BookUtils.IsGuildless(ch))
            {
                return LanguageMgr.Translate(requester, "RoyalTreasuryClerk.Founder.InGuild");
            }

            string acc = BookUtils.GetAccountName(ch);
            if (string.IsNullOrWhiteSpace(acc))
            {
                return LanguageMgr.Translate(requester, "RoyalTreasuryClerk.Founder.NoAccount");
            }

            if (requester.Client.Account.PrivLevel <= 1)
            {
                // TODO: This doesn't check the accounts of the other founders.
                var used = GetUsedAccounts(requester);
                if (used.Contains(acc, StringComparer.OrdinalIgnoreCase))
                {
                    return LanguageMgr.Translate(requester, "RoyalTreasuryClerk.Founder.SameAccount");
                }
            }
            return null;
        }

        private static HashSet<string> GetUsedAccounts(GamePlayer player)
        {
            string raw = player.TempProperties.getProperty<string>(TP_USED_ACCOUNTS, string.Empty) ?? string.Empty;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var part in raw.Split('|'))
            {
                var p = part.Trim();
                if (!string.IsNullOrWhiteSpace(p))
                    set.Add(p);
            }

            return set;
        }

        private static void AddUsedAccount(GamePlayer player, string accountName)
        {
            var used = GetUsedAccounts(player);
            used.Add(accountName);

            player.TempProperties.setProperty(TP_USED_ACCOUNTS, string.Join("|", used));
        }

        private static string RemoveTagLines(string text, string tagPrefix)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var lines = text.Replace("\r", "").Split('\n');
            var kept = new List<string>();
            foreach (var l in lines)
            {
                if (l.TrimStart().StartsWith(tagPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                kept.Add(l);
            }
            return string.Join("\n", kept);
        }

        private static void ResetState(GamePlayer player)
        {
            player.TempProperties.removeProperty(TP_BOOK_ID);
            player.TempProperties.removeProperty(TP_STEP);
            player.TempProperties.removeProperty(TP_MEMBER_INDEX);
            player.TempProperties.removeProperty(TP_AWAIT_STAMP);
            player.TempProperties.removeProperty(TP_USED_ACCOUNTS);
        }
    }
}
