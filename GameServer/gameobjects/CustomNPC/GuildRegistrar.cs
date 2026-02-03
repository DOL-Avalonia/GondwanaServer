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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.GS.Scripts;
using DOL.Language;

namespace DOL.GS
{
    [NPCGuildScript("Guild Registrar")]
    public class GuildRegistrar : GameNPC
    {
        private const string GUILD_REGISTER_AUTHOR = "Guild Register";

        private static string T(GamePlayer p, string key, params object[] args)
            => LanguageMgr.GetTranslation(p.Client, key, args);

        private static bool Eq(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // Dynamic Keyword Helpers
        private static string KeywordFormGuild(GamePlayer p)
        {
            string k = T(p, "GuildRegistrar.Keyword.FormGuild");
            return (string.IsNullOrWhiteSpace(k) || k.StartsWith("GuildRegistrar.")) ? "form a guild" : k;
        }

        private static string KeywordConsultRegister(GamePlayer p)
        {
            string k = T(p, "GuildRegistrar.Keyword.ConsultRegister");
            return (string.IsNullOrWhiteSpace(k) || k.StartsWith("GuildRegistrar.")) ? "consult the guild register" : k;
        }

        private void SayTo(GamePlayer player, string msg)
        {
            player?.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            string formKw = KeywordFormGuild(player);

            if (Properties.GUILD_REQUIRE_REGISTER)
            {
                string consultKw = KeywordConsultRegister(player);

                SayTo(player,
                    T(player, "GuildRegistrar.Interact.RegisterMode.Line1") + "\n" +
                    T(player, "GuildRegistrar.Interact.RegisterMode.Line2", formKw) + "\n\n" +
                    T(player, "GuildRegistrar.Interact.RegisterMode.Line3", consultKw));
            }
            else
            {
                SayTo(player, T(player, "GuildRegistrar.Interact.NormalMode", formKw));
            }

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text))
                return false;

            if (source is not GamePlayer player)
                return true;

            text = (text ?? string.Empty).Trim();

            // Click on a register title in the list -> show FULL TEXT (Fix #4)
            if (Properties.GUILD_REQUIRE_REGISTER)
            {
                var regClicked = GameServer.Database.SelectObject<DBBook>(b =>
                    b.Title == text && b.Author == GUILD_REGISTER_AUTHOR);

                if (regClicked != null)
                {
                    // Full read, no preview, no votes, no price
                    BooksMgr.ReadBook(player, regClicked);
                    return true;
                }
            }

            if (Properties.GUILD_REQUIRE_REGISTER && Eq(text, KeywordConsultRegister(player)))
            {
                SendGuildRegisterList(player);
                return true;
            }

            if (Eq(text, KeywordFormGuild(player)))
            {
                if (!Properties.GUILD_REQUIRE_REGISTER)
                {
                    // Normal mode instructions
                    SayTo(player,
                        T(player, "GuildRegistrar.Whisper.Form.Normal.Line1", Properties.GUILD_NUM) + "\n" +
                        T(player, "GuildRegistrar.Whisper.Form.Normal.Line2"));
                }
                else
                {
                    // Register mode instructions
                    string consultKw = KeywordConsultRegister(player);
                    SayTo(player,
                        T(player, "GuildRegistrar.Whisper.Form.Register.Line1") + "\n" +
                        T(player, "GuildRegistrar.Whisper.Form.Register.Line2") + "\n\n" +
                        T(player, "GuildRegistrar.Whisper.Form.Register.Line3", consultKw));
                }
                return true;
            }

            return true;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (source is not GamePlayer player || item == null)
                return false;

            if (!Properties.GUILD_REQUIRE_REGISTER)
            {
                SayTo(player, T(player, "GuildRegistrar.Receive.NotRequired"));
                return true;
            }

            long bookId = item.MaxCondition;
            if (bookId <= 0)
            {
                SayTo(player, T(player, "GuildRegistrar.Receive.NotBook"));
                return true;
            }

            DBBook book = GameServer.Database.FindObjectByKey<DBBook>(bookId);
            if (book == null)
            {
                SayTo(player, T(player, "GuildRegistrar.Receive.NoLedger"));
                return true;
            }

            // Must be a guild registry
            if (!book.IsGuildRegistry)
            {
                SayTo(player, T(player, "GuildRegistrar.Receive.NotRegister"));
                return true;
            }

            // Must be stamped/legalized
            if (!IsBookStamped(book))
            {
                SayTo(player, T(player, "GuildRegistrar.Receive.NotStamped"));
                return true;
            }

            // Guild name = book title
            string guildName = (book.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(guildName) ||
                guildName.Length > Guild.MAX_CREATE_NAME_LENGTH ||
                !Commands.GuildCommandHandler.IsValidGuildName(guildName))
            {
                SayTo(player, T(player, "GuildRegistrar.Receive.InvalidName"));
                return true;
            }

            if (GuildMgr.DoesGuildExist(guildName))
            {
                SayTo(player, T(player, "GuildRegistrar.Receive.NameTaken"));
                return true;
            }

            int required = Properties.GUILD_NUM;
            var founders = BookUtils.ExtractFounders(book.Text, required);

            if (string.IsNullOrWhiteSpace(founders.leader) || founders.members.Count != required - 1)
            {
                SayTo(player, T(player, "GuildRegistrar.Receive.IncompleteList"));
                return true;
            }

            // Build founder name list (leader first)
            var founderNames = new List<string>(required);
            founderNames.Add(founders.leader);
            founderNames.AddRange(founders.members);

            // Any founder can present it (leader OR member)
            if (!founderNames.Any(n => n.Equals(player.Name, StringComparison.OrdinalIgnoreCase)))
            {
                SayTo(player, T(player, "GuildRegistrar.Receive.NotFounder"));
                return true;
            }

            // Validate founders exist and are guildless (DB), and unique accounts
            var accounts = new List<string>(required);

            foreach (string fn in founderNames)
            {
                var ch = BookUtils.GetCharacter(fn);
                if (ch == null)
                {
                    SayTo(player, T(player, "GuildRegistrar.Validate.FounderNotFound", fn));
                    return true;
                }

                if (!BookUtils.IsGuildless(ch))
                {
                    SayTo(player, T(player, "GuildRegistrar.Validate.FounderInGuild", fn));
                    return true;
                }

                accounts.Add(BookUtils.GetAccountName(ch));
            }

            if (!BookUtils.AccountsAreUnique(accounts))
            {
                SayTo(player, T(player, "GuildRegistrar.Validate.SameAccount"));
                return true;
            }

            Guild newGuild = GuildMgr.CreateGuild(player.Realm, guildName, player);
            if (newGuild == null)
            {
                SayTo(player, T(player, "GuildRegistrar.Error.CreateFailed"));
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
                SayTo(player, T(player, "GuildRegistrar.Error.TakeItemFailed"));
                return true;
            }

            UpdateBookAfterGuildCreation(book, guildName);

            try
            {
                book.Save();
            }
            catch
            {
                SayTo(player, T(player, "GuildRegistrar.Error.BookUpdateFailed"));
            }

            RefreshGuildAndMembersUI(newGuild);
            SayTo(player, T(player, "GuildRegistrar.Success", guildName));
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
            book.IsGuildRegistry = false;
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

        private void SendGuildRegisterList(GamePlayer player)
        {
            var registers = GameServer.Database
                .SelectObjects<DBBook>(b => b.Author == GUILD_REGISTER_AUTHOR)
                .OrderBy(b => b.Title)
                .ToList();

            if (registers.Count == 0)
            {
                SayTo(player, T(player, "GuildRegistrar.List.None"));
                return;
            }

            SayTo(player, T(player, "GuildRegistrar.List.Count", registers.Count));

            var sb = new StringBuilder(2048);

            foreach (var b in registers)
            {
                // Clickable title
                sb.Append("\n[")
                  .Append(b.Title)
                  .Append("]");

                // Optional metadata
                if (!string.IsNullOrWhiteSpace(b.StampBy) || b.StampDate != DateTime.MinValue)
                {
                    sb.Append(" - ")
                      .Append(T(player, "GuildRegistrar.List.StampedBy"))
                      .Append(" ")
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
    }
}