#nullable enable

using DOL.GS;
using DOL.GS.PacketHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AmteScripts.PvP
{
    public abstract class PvPEntity
    {
        private readonly string m_id = string.Empty;
        public enum eType
        {
            Player = 0,
            GuildGroup = 1,
        }

        public string InternalID => m_id;

        public eType Type { get; }

        public override int GetHashCode()
        {
            return m_id.GetHashCode();
        }

        public abstract int Count { get; }

        public virtual GamePlayer? AsPlayer { get => null; }

        public virtual Guild? AsGuild { get => null; }

        public virtual Group? AsGroup { get => null; }

        public virtual string Name { get => null; }

        public virtual Guild? AssociatedGuild { get => null; }

        protected PvPEntity(string id, eType type)
        {
            m_id = id;
            Type = type;
        }

        public abstract IEnumerable<GamePlayer> GetPlayers();

        public void SendMessage(string task, eChatType type, eChatLoc loc) => SendMessage(Task.FromResult(task), type, loc);

        public IEnumerable<Task> SendMessages(IEnumerable<Task<string>> tasks, eChatType type, eChatLoc loc)
        {
            return GetPlayers().Select(async p =>
            {
                foreach (var task in tasks)
                {
                    await task;
                }
            });
        }

        public IEnumerable<Task> SendTranslations(IEnumerable<string> keys, eChatType type, eChatLoc loc, params object[] args)
        {
            return GetPlayers().Select(async p =>
            {
                foreach (var key in keys)
                {
                    await p.SendTranslatedMessage(key, type, loc, args);
                }
            });
        }

        public IEnumerable<Task> SendMessage(Task<string> task, eChatType type, eChatLoc loc)
        {
            return GetPlayers().Select(async p => p.SendMessage(task, type, loc));
        }

        public IEnumerable<Task> SendTranslation(string key, eChatType type, eChatLoc loc, params object[] args)
        {
            return GetPlayers().Select(async p => p.SendTranslatedMessage(key, type, loc, args));
        }
    }

    public sealed class PvPPlayerEntity : PvPEntity
    {
        private GamePlayer? m_player = null;

        public override int Count => 1;

        public override GamePlayer? AsPlayer => m_player;

        public override string Name => AsPlayer?.Name;

        public override Guild? AssociatedGuild => AsPlayer?.Guild;

        public PvPPlayerEntity(string id) : base(id, eType.Player)
        {
        }

        public PvPPlayerEntity(GamePlayer player) : base(player.InternalID, eType.Player)
        {
            m_player = player;
        }

        public override IEnumerable<GamePlayer> GetPlayers()
        {
            return m_player == null ? [] : [m_player];
        }
    }

    public sealed class PvPGuildGroupEntity : PvPEntity
    {
        private Group? m_group;
        private Guild? m_guild;

        public PvPGuildGroupEntity(string guildID) : this(GuildMgr.GetGuildByGuildID(guildID))
        {
        }

        public PvPGuildGroupEntity(Guild guild, Group? group = null) : base(guild.GuildID, eType.GuildGroup)
        {
            m_group = group;
            m_guild = guild;
        }

        public override int Count => m_group?.MemberCount ?? 0;

        public override Group? AsGroup => m_group;

        public override Guild? AsGuild => m_guild;

        public override Guild? AssociatedGuild => AsGuild;

        public override string Name => AsGuild?.Name;

        public override IEnumerable<GamePlayer> GetPlayers()
        {
            return m_group?.GetMembers().OfType<GamePlayer>() ?? Enumerable.Empty<GamePlayer>();
        }
    }
}
