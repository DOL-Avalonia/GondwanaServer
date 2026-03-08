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
            var founders = BookUtils.ExtractFounders(book, required);

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

            if (player.Client.Account.PrivLevel <= 1)
            {
                if (!BookUtils.AccountsAreUnique(accounts))
                {
                    SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Validate.SameAccount"));
                    return true;
                }
            }

            Guild newGuild = GuildMgr.CreateGuild(player.Realm, guildName, player);
            if (newGuild == null)
            {
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Error.CreateFailed"));
                return true;
            }

            // Add founders (leader rank 0, others rank 5)
            AssignFoundersToGuild(newGuild, founders.leader, founderNames);

            // Consume the register item
            if (!player.Inventory.RemoveItem(item))
            {
                GuildMgr.DeleteGuild(newGuild);
                SayTo(player, LanguageMgr.Translate(player, "GuildRegistrar.Error.TakeItemFailed"));
                return true;
            }

            UpdateBookAfterGuildCreation(book, newGuild);

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

        private static void UpdateBookAfterGuildCreation(DBBook book, Guild guild)
        {
            book.Title = guild.Name;
            book.Name = $"[REGISTER] {guild.Name}";
            book.IsInLibrary = true;
            book.CurrentPriceCopper = 0;
            book.BasePriceCopper = 0;
            book.IsGuildRegistry = true;
        }

        private static bool IsBookStamped(DBBook book)
        {
            if (book.IsStamped)
                return true;

            if (!string.IsNullOrWhiteSpace(book.StampBy))
                return true;

            if (book.StampDate != DateTime.MinValue)
                return true;

            return false;
        }

        private bool AssignFoundersToGuild(Guild guild, string leader, List<string> founders)
        {
            var leaderRank = guild.GetRankByID(0);
            var founderRank = guild.GetRankByID(5);
            List<GamePlayer> onlinePlayers = new();
            List<DOLCharacters> offlinePlayers = new();
            GamePlayer? leaderPlayer = null;
            DOLCharacters? leaderCharacter = null;

            foreach (var name in founders)
            {
                var client = WorldMgr.GetClientByPlayerName(name, true, false);
                var gp = client?.Player;
                if (gp != null && string.IsNullOrEmpty(gp.GuildID))
                {
                    if (string.Equals(leader, name, StringComparison.InvariantCultureIgnoreCase))
                        leaderPlayer = gp;
                    onlinePlayers.Add(gp);
                    continue;
                }

                var ch = BookUtils.GetCharacter(name);
                if (ch != null && string.IsNullOrEmpty(ch.GuildID))
                {
                    if (string.Equals(leader, name, StringComparison.InvariantCultureIgnoreCase))
                        leaderCharacter = ch;
                    offlinePlayers.Add(ch);
                }
            }

            if (onlinePlayers.Count == 0 && offlinePlayers.Count == 0)
                return false; // Players joined another guild or somehow changed name or deleted character

            if (leaderPlayer is null && leaderCharacter is null)
            {
                leaderPlayer = onlinePlayers.FirstOrDefault();
                if (leaderPlayer is null)
                    leaderCharacter = offlinePlayers.FirstOrDefault();
            }

            bool success = false;
            success = guild.AddPlayers(onlinePlayers.Select(p => new KeyValuePair<GamePlayer, DBRank>(p, p == leaderPlayer ? leaderRank : founderRank))) || success;
            success = guild.AddOfflinePlayers(offlinePlayers.Select(p => new KeyValuePair<DOLCharacters, DBRank>(p, p == leaderCharacter ? leaderRank : founderRank))) || success;
            return success;
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

        private void ShowRegistry(PlayerCache cache, DBBook registry)
        {
            BooksMgr.ReadGuildRegistry(cache.Player, registry);
        }
    }
}