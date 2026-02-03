using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using System.Text;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS
{
    public class RoyalTreasuryClerk : GameNPC
    {
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

        private static string T(GamePlayer p, string key, params object[] args)
            => LanguageMgr.GetTranslation(p.Client, key, args);

        private static string GetGuildKeyword(GamePlayer player)
        {
            string kw = T(player, "RoyalTreasuryClerk.Keyword.GuildRegister");
            // Fallback if translation is missing
            if (string.IsNullOrWhiteSpace(kw) || kw.StartsWith("RoyalTreasuryClerk.", StringComparison.OrdinalIgnoreCase))
                return "guild register";
            return kw;
        }

        private static bool HasRecallStone(GamePlayer player)
        {
            return player.Inventory.CountItemTemplate(PERSONAL_RECALL_STONE_ID, eInventorySlot.Min_Inv, eInventorySlot.Max_Inv) > 0;
        }

        private static bool IsRoyalScrollItem(InventoryItem item)
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

        /// <summary>
        /// Keyword used in "GUILD_REQUIRE_REGISTER = true" mode for stone service.
        /// </summary>
        private static string GetStoneKeyword(GamePlayer player)
        {
            string kw = T(player, "RoyalTreasuryClerk.Other");
            if (string.IsNullOrWhiteSpace(kw) || kw.StartsWith("RoyalTreasuryClerk.", StringComparison.OrdinalIgnoreCase))
                kw = "another";
            return kw;
        }

        private void SayTo(GamePlayer player, string msg)
        {
            player?.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        private static bool BookHasTag(DBBook book, string tag)
        {
            if (book == null || string.IsNullOrEmpty(book.Text)) return false;
            return book.Text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureProcessingTag(DBBook book)
        {
            if (book == null) return;
            if (string.IsNullOrEmpty(book.Text)) book.Text = string.Empty;

            if (!BookHasTag(book, TAG_PROCESSING))
            {
                book.Text += "\n" + TAG_PROCESSING + "\n";
            }
        }

        private static void RemoveProcessingTag(DBBook book)
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
        private InventoryItem FindInventoryItemForBook(GamePlayer player, long bookId)
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
        private void ApplyUniqueState(GamePlayer player, InventoryItem invItem, string statePrefix, string title, bool pickable, bool dropable, bool tradable)
        {
            if (player == null || invItem == null)
                return;

            string newName = $"{statePrefix} {title}".Trim();

            invItem.Name = newName;
            invItem.IsPickable = pickable;
            invItem.IsDropable = dropable;
            invItem.IsTradable = tradable;

            GameServer.Database.SaveObject(invItem);
            player.Out.SendInventoryItemsUpdate(new InventoryItem[] { invItem });
            player.Out.SendInventorySlotsUpdate(new int[] { invItem.SlotPosition });
        }

        private bool ValidateInitialBook(GamePlayer player, InventoryItem item, DBBook book, out string failMsg)
        {
            failMsg = string.Empty;

            if (item == null || book == null)
            {
                failMsg = T(player, "RoyalTreasuryClerk.Validate.NotRegister");
                return false;
            }

            // Parchment check
            if (!IsRoyalScrollItem(item))
            {
                failMsg = T(player, "RoyalTreasuryClerk.Validate.NotParchment");
                return false;
            }

            // Ink check
            if (!string.Equals(book.InkId, "ink_royal", StringComparison.OrdinalIgnoreCase))
            {
                failMsg = T(player, "RoyalTreasuryClerk.Validate.NotInk");
                return false;
            }

            // Minimum words
            int minWords = Properties.GUILD_REGISTER_MIN_WORDS;
            int wc = BookUtils.CountWords(book.Text);
            if (wc < minWords)
            {
                failMsg = T(player, "RoyalTreasuryClerk.Validate.TooShort", minWords, wc);
                return false;
            }

            // Deep nonsense/spam check
            if (Properties.BOOK_ENABLE_PUBLISH_HEURISTICS && BookUtils.LooksLikeGibberish(book.Text))
            {
                failMsg = T(player, "RoyalTreasuryClerk.Validate.Gibberish");
                return false;
            }

            // Prohibited content
            if (BookUtils.ContainsProhibitedTerms(book.Text, out string bad))
            {
                failMsg = T(player, "RoyalTreasuryClerk.Validate.Prohibited", bad);
                return false;
            }

            if (BookUtils.ContainsProhibitedTerms(book.Title, out string badTitle))
            {
                failMsg = T(player, "RoyalTreasuryClerk.Validate.TitleProhibited", badTitle);
                return false;
            }

            // Title validity
            if (string.IsNullOrWhiteSpace(book.Title) ||
                book.Title.Length > Guild.MAX_CREATE_NAME_LENGTH ||
                !Commands.GuildCommandHandler.IsValidGuildName(book.Title))
            {
                failMsg = T(player, "RoyalTreasuryClerk.Validate.NameInvalid");
                return false;
            }

            if (GuildMgr.DoesGuildExist(book.Title))
            {
                failMsg = T(player, "RoyalTreasuryClerk.Validate.NameTaken");
                return false;
            }

            return true;
        }

        private InventoryItem FindPlayerRegisterBook(GamePlayer player, out DBBook book)
        {
            book = null;
            if (player == null) return null;

            for (var slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                var it = player.Inventory.GetItem(slot);
                if (it == null) continue;

                if (!IsRoyalScrollItem(it)) continue;
                if (it.MaxCondition <= 0) continue;

                var b = GameServer.Database.FindObjectByKey<DBBook>((long)it.MaxCondition);
                if (b == null) continue;

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

        private bool TryResumeFromInventory(GamePlayer player, out DBBook book)
        {
            book = null;

            var invItem = FindPlayerRegisterBook(player, out book);
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

        private bool EnsureBookStillPresentOrReset(GamePlayer player)
        {
            int bookId = player.TempProperties.getProperty<int>(TP_BOOK_ID, 0);
            int step = player.TempProperties.getProperty<int>(TP_STEP, STEP_NONE);

            if (step == STEP_NONE || bookId <= 0)
                return true;

            bool hasIt = FindInventoryItemForBook(player, bookId) != null;

            if (!hasIt)
            {
                ResetState(player);
                SayTo(player, T(player, "RoyalTreasuryClerk.Resume.Lost"));
                return false;
            }

            return true;
        }

        private void ShowResumePrompt(GamePlayer player, DBBook book)
        {
            int required = Properties.GUILD_NUM;
            var founders = BookUtils.ExtractFounders(book.Text, required);

            int step = player.TempProperties.getProperty<int>(TP_STEP, STEP_NONE);
            if (step == STEP_COLLECT_LEADER)
            {
                SayTo(player,
                    T(player, "RoyalTreasuryClerk.Resume.Leader.Line1") + "\n" +
                    T(player, "RoyalTreasuryClerk.Resume.Leader.Line2") + "\n" +
                    T(player, "RoyalTreasuryClerk.Resume.Leader.Line3"));
            }
            else if (step == STEP_COLLECT_MEMBERS)
            {
                int idx = player.TempProperties.getProperty<int>(TP_MEMBER_INDEX, founders.members.Count + 1);
                SayTo(player,
                    T(player, "RoyalTreasuryClerk.Resume.Member.Line1", founders.leader) + "\n" +
                    T(player, "RoyalTreasuryClerk.Resume.Member.Line2", idx, required - 1));
            }
            else if (step == STEP_AWAIT_STAMP)
            {
                SayTo(player,
                     T(player, "RoyalTreasuryClerk.Resume.Stamp.Line1") + "\n" +
                     T(player, "RoyalTreasuryClerk.Resume.Stamp.Line2"));
            }
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player) || player == null)
                return false;

            if (Properties.GUILD_REQUIRE_REGISTER)
            {
                EnsureBookStillPresentOrReset(player);

                // Only resume if the book is explicitly in "#processing"
                if (TryResumeFromInventory(player, out DBBook activeBook))
                {
                    ShowResumePrompt(player, activeBook);
                    return true;
                }

                string guildKw = GetGuildKeyword(player);
                player.Out.SendMessage(
                    T(player, "RoyalTreasuryClerk.Interact.Intro.Line1") + "\n" +
                    T(player, "RoyalTreasuryClerk.Interact.Intro.Line2", guildKw),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);

                if (GlobalConstants.IsExpansionEnabled((int)eClientExpansion.DarknessRising))
                {
                    string kw = GetStoneKeyword(player);
                    player.Out.SendMessage(T(player, "RoyalTreasuryClerk.Stone.Checking"), eChatType.CT_System, eChatLoc.CL_PopupWindow);

                    if (!HasRecallStone(player))
                    {
                        player.Out.SendMessage(T(player, "RoyalTreasuryClerk.Stone.Missing").Replace("[another]", "[" + kw + "]"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    }
                    else
                    {
                        player.Out.SendMessage(T(player, "RoyalTreasuryClerk.Stone.HaveOne").Replace("[another]", "[" + kw + "]"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    }
                }

                return true;
            }

            // MODE B: guild register OFF
            if (GlobalConstants.IsExpansionEnabled((int)eClientExpansion.DarknessRising))
            {
                player.Out.SendMessage(T(player, "RoyalTreasuryClerk.Checking"),
                    eChatType.CT_System, eChatLoc.CL_PopupWindow);

                if (!HasRecallStone(player))
                {
                    player.Out.SendMessage(T(player, "RoyalTreasuryClerk.Nostone"),
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

            text = (text ?? "").Trim();

            if (Properties.GUILD_REQUIRE_REGISTER)
            {
                int step = player.TempProperties.getProperty<int>(TP_STEP, STEP_NONE);

                if (step == STEP_NONE)
                {
                    TryResumeFromInventory(player, out _);
                    step = player.TempProperties.getProperty<int>(TP_STEP, STEP_NONE);
                }

                if (text.Equals(WHISPER_CONTINUE, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryResumeFromInventory(player, out DBBook activeBook))
                        ShowResumePrompt(player, activeBook);
                    else
                        SayTo(player, T(player, "RoyalTreasuryClerk.Resume.NoActive"));
                    return true;
                }

                if (step != STEP_NONE)
                {
                    if (!EnsureBookStillPresentOrReset(player))
                        return true;

                    return HandleConversation(player, text);
                }

                // Start guild legalization flow
                string guildKw = GetGuildKeyword(player);
                if (text.Equals(guildKw, StringComparison.OrdinalIgnoreCase))
                {
                    SayTo(player,
                        T(player, "RoyalTreasuryClerk.Whisper.Guild.Line1") + "\n" +
                        T(player, "RoyalTreasuryClerk.Whisper.Guild.Line2") + "\n" +
                        T(player, "RoyalTreasuryClerk.Whisper.Guild.Line3"));
                    return true;
                }

                // Bind stone service (DR)
                if (GlobalConstants.IsExpansionEnabled((int)eClientExpansion.DarknessRising))
                {
                    string newKw = GetStoneKeyword(player);
                    string oldKw = T(player, "RoyalTreasuryClerk.Other");

                    bool askedStone =
                        (!string.IsNullOrWhiteSpace(newKw) && text.Equals(newKw, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(oldKw) && text.Equals(oldKw, StringComparison.OrdinalIgnoreCase));

                    if (askedStone && !HasRecallStone(player))
                    {
                        SayTo(player, T(player, "RoyalTreasuryClerk.Stonegive"));
                        player.ReceiveItem(this, PERSONAL_RECALL_STONE_ID, eInventoryActionType.Other);
                        return true;
                    }
                }

                return true;
            }

            // MODE B: Guild register feature OFF
            if (GlobalConstants.IsExpansionEnabled((int)eClientExpansion.DarknessRising))
            {
                string other = T(player, "RoyalTreasuryClerk.Other");
                if (!string.IsNullOrEmpty(other) && text.Equals(other, StringComparison.OrdinalIgnoreCase))
                {
                    if (!HasRecallStone(player))
                    {
                        SayTo(player, T(player, "RoyalTreasuryClerk.Stonegive"));
                        player.ReceiveItem(this, PERSONAL_RECALL_STONE_ID, eInventoryActionType.Other);
                    }
                    return true;
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

            EnsureBookStillPresentOrReset(player);

            bool awaitingStamp = player.TempProperties.getProperty<bool>(TP_AWAIT_STAMP, false);
            if (awaitingStamp)
            {
                if (!item.Id_nb.Equals("guild_stamp", StringComparison.OrdinalIgnoreCase))
                {
                    SayTo(player, T(player, "RoyalTreasuryClerk.Stamp.WrongItem"));
                    return true;
                }

                if (!player.Inventory.RemoveItem(item))
                {
                    SayTo(player, T(player, "RoyalTreasuryClerk.Stamp.TakeFail"));
                    return true;
                }

                FinalizeStamp(player);
                return true;
            }

            long bookId = item.MaxCondition;
            if (bookId <= 0)
            {
                SayTo(player, T(player, "RoyalTreasuryClerk.Validate.NotRegister"));
                return true;
            }

            DBBook book = GameServer.Database.FindObjectByKey<DBBook>(bookId);
            if (book == null)
            {
                SayTo(player, T(player, "RoyalTreasuryClerk.Stamp.NoLedger"));
                return true;
            }

            // If already stamped, clerk does nothing with it
            if (book.IsStamped || BookHasTag(book, TAG_STAMPED))
            {
                SayTo(player, T(player, "RoyalTreasuryClerk.Stamp.AlreadyDone"));
                return true;
            }

            // Only enter the step-based conversation after FULL validation AND after adding #processing.
            // If book is not yet processing, we validate now and then mark it processing.
            if (!BookHasTag(book, TAG_PROCESSING))
            {
                if (!ValidateInitialBook(player, item, book, out string failMsg))
                {
                    SayTo(player, failMsg);
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

                SayTo(player,
                    T(player, "RoyalTreasuryClerk.Process.Start.Line1") + "\n" +
                    T(player, "RoyalTreasuryClerk.Process.Start.Line2") + "\n" +
                    T(player, "RoyalTreasuryClerk.Process.Start.Line3"));
                return true;
            }

            // If already processing, resume prompt based on tags
            player.TempProperties.setProperty(TP_BOOK_ID, (int)bookId);
            TryResumeFromInventory(player, out _);
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
                SayTo(player, T(player, "RoyalTreasuryClerk.Resume.Lost"));
                return true;
            }

            int required = Properties.GUILD_NUM;

            switch (step)
            {
                case STEP_COLLECT_LEADER:
                    {
                        if (!TryValidateFounder(player, text, out var ch, out string failMsg))
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

                        SayTo(player,
                             T(player, "RoyalTreasuryClerk.Process.LeaderSaved.Line1", ch.Name) + "\n" +
                             T(player, "RoyalTreasuryClerk.Process.LeaderSaved.Line2", required - 1));
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

                        if (!TryValidateFounder(player, text, out var ch, out string failMsg))
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
                            SayTo(player, T(player, "RoyalTreasuryClerk.Process.DuplicateMember"));
                            return true;
                        }

                        book.Text += $"\n#GuildMember{idx:00}_{ch.Name}\n";
                        book.Save();

                        AddUsedAccount(player, ch.AccountName);

                        idx++;
                        player.TempProperties.setProperty(TP_MEMBER_INDEX, idx);

                        if (idx <= required - 1)
                            SayTo(player, T(player, "RoyalTreasuryClerk.Process.MemberSaved", idx, required - 1));
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
                            SayTo(player, T(player, "RoyalTreasuryClerk.Validate.NameInvalid"));
                            return true;
                        }
                        if (GuildMgr.DoesGuildExist(newName))
                        {
                            SayTo(player, T(player, "RoyalTreasuryClerk.Validate.NameTaken"));
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
                    SayTo(player,
                        T(player, "RoyalTreasuryClerk.Process.AwaitStamp.Line1") + "\n" +
                        T(player, "RoyalTreasuryClerk.Process.AwaitStamp.Line2"));
                    return true;
            }
        }

        private bool ConfirmGuildName(GamePlayer player, DBBook book)
        {
            player.Out.SendCustomDialog(
                T(player, "RoyalTreasuryClerk.Confirm.Dialog.Line1") +"\n\n" + " " +
                book.Title + " " + "\n\n" +
                T(player, "RoyalTreasuryClerk.Confirm.Dialog.Line2"),
                (ply, resp) =>
                {
                    if (resp != 0x01)
                    {
                        SayTo(ply, T(ply, "RoyalTreasuryClerk.Confirm.Declined"));
                        ply.TempProperties.setProperty(TP_STEP, STEP_CONFIRM_GUILDNAME);
                        return;
                    }

                    ply.TempProperties.setProperty(TP_STEP, STEP_AWAIT_STAMP);
                    ply.TempProperties.setProperty(TP_AWAIT_STAMP, true);

                    SayTo(ply,
                        T(ply, "RoyalTreasuryClerk.Process.AwaitStamp.Line1") + "\n" +
                        T(ply, "RoyalTreasuryClerk.Process.AwaitStamp.Line2"));
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
                SayTo(player, T(player, "RoyalTreasuryClerk.Resume.Lost"));
                return false;
            }

            int required = Properties.GUILD_NUM;
            var founders = BookUtils.ExtractFounders(book.Text, required);
            if (string.IsNullOrWhiteSpace(founders.leader) || founders.members.Count != required - 1)
            {
                SayTo(player, T(player, "RoyalTreasuryClerk.Stamp.Incomplete"));
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

            SayTo(player,
                T(player, "RoyalTreasuryClerk.Stamp.Success.Line1") + "\n" +
                T(player, "RoyalTreasuryClerk.Stamp.Success.Line2"));

            ResetState(player);
            return true;
        }

        private bool TryValidateFounder(GamePlayer requester, string inputName, out DOLCharacters ch, out string failMsg)
        {
            failMsg = string.Empty;
            ch = null;

            string name = (inputName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                failMsg = T(requester, "RoyalTreasuryClerk.Founder.NoName");
                return false;
            }

            ch = BookUtils.GetCharacter(name);
            if (ch == null)
            {
                failMsg = T(requester, "RoyalTreasuryClerk.Founder.NotFound");
                return false;
            }

            if (!BookUtils.IsGuildless(ch))
            {
                failMsg = T(requester, "RoyalTreasuryClerk.Founder.InGuild");
                return false;
            }

            string acc = BookUtils.GetAccountName(ch);
            if (string.IsNullOrWhiteSpace(acc))
            {
                failMsg = T(requester, "RoyalTreasuryClerk.Founder.NoAccount");
                return false;
            }

            var used = GetUsedAccounts(requester);
            if (used.Contains(acc, StringComparer.OrdinalIgnoreCase))
            {
                failMsg = T(requester, "RoyalTreasuryClerk.Founder.SameAccount");
                return false;
            }

            return true;
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
