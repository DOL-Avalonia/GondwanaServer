#nullable enable
using DOL.GS;
using log4net;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static AmteScripts.Managers.PvpManager;

namespace AmteScripts.PvP
{
    public record PvPScore(bool IsSoloScore)
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        
        public PvPScore(string playerName, string playerID, bool isSoloScore) : this(isSoloScore)
        {
            PlayerName = playerName;
            PlayerID = playerID;
        }
        
        public PvPScore(GamePlayer player, bool isSoloScore) : this(player.Name!, player.InternalID!, isSoloScore)
        {
        }

        public PvPScore(Guild guild) : this(guild.Name!, guild.GuildID!, false)
        {
        }

        protected IEnumerable<KeyValuePair<string, Item>> CopyItems(PvPScore other)
        {
            return Treasure_Items.Select(kv => new KeyValuePair<string, Item>(kv.Key, kv.Value with { }));
        }

        public PvPScore Copy()
        {
            return this with
            {
                Treasure_Items = new(CopyItems(this))
            };
        }

        public string PlayerName { get; set; }
        public string PlayerID { get; set; }

        // --- For session type #1: PvP Combat ---
        [DefaultValue(0)]
        public int PvP_SoloKills { get; set; }
        [DefaultValue(0)]
        public int PvP_SoloKillsPoints { get; set; }
        [DefaultValue(0)]
        public int PvP_GroupKills { get; set; }
        [DefaultValue(0)]
        public int PvP_GroupKillsPoints { get; set; }

        // --- For session type #2: Flag Capture ---
        [DefaultValue(0)]
        public int Flag_KillFlagCarrierCount { get; set; }
        [DefaultValue(0)]
        public int Flag_KillFlagCarrierPoints { get; set; }
        [DefaultValue(0)]
        public int Flag_FlagReturnsCount { get; set; }
        [DefaultValue(0)]
        public int Flag_FlagReturnsPoints { get; set; }
        [DefaultValue(0)]
        public int Flag_OwnershipPoints { get; set; }

        // --- For session type #3: Treasure Hunt ---
        /// <summary>
        /// For storing the item deposits in this chest.
        /// </summary>
        public record Item(string Id_nb, int PointsPerItem)
        {
            public int Count { get; set; } = 0;

            public string ItemName { get; set; } = string.Empty;

            public int Points => Count * PointsPerItem;
            
            public Item(PvPTreasure treasure) : this(treasure.Id_nb!, treasure.TreasurePoints)
            {
                ItemName = treasure.Name!;
                Count = treasure.Count;
            }

            public bool IsSameItem(Item item)
            {
                return Equals(Id_nb, item.Id_nb) && Equals(ItemName, item.ItemName) && Equals(PointsPerItem, item.PointsPerItem);
            }

            public void Merge(Item item)
            {
                Debug.Assert(IsSameItem(item));

                this.Count += item.Count;
                if (string.IsNullOrEmpty(ItemName))
                    ItemName = item.ItemName;
                item.Count = 0;
            }

            public Item Split(int count = 1)
            {
                count = Math.Min(this.Count, count);
                this.Count -= count;
                return this with { Count = count };
            }
        }

        [DefaultValue(0)]

        [IgnoreDataMember]
        public ReaderWriterDictionary<string, Item> Treasure_Items { get; init; } = new();

        public IEnumerable<Item> Treasure_ItemsSerializer
        {
            get => Treasure_Items.Values;
            set
            {
                Treasure_Items.FreezeWhile((dict) =>
                {
                    dict.Clear();
                    foreach (var i in value)
                    {
                        dict.Add(i.Id_nb, i);
                    }
                });
            }
        }

        public int Treasure_BroughtTreasuresPoints { get; set; }
        [DefaultValue(0)]
        public int Treasure_StolenTreasuresPoints { get; set; }

        // --- For session type #4: Bring Friends ---
        [DefaultValue(0)]
        public int Friends_BroughtFriendsPoints { get; set; }
        [DefaultValue(0)]
        public int Friends_BroughtFamilyBonus { get; set; }
        [DefaultValue(0)]
        public int Friends_FriendKilledCount { get; set; }
        [DefaultValue(0)]
        public int Friends_FriendKilledPoints { get; set; }
        [DefaultValue(0)]
        public int Friends_KillEnemyFriendCount { get; set; }
        [DefaultValue(0)]
        public int Friends_KillEnemyFriendPoints { get; set; }

        // --- For session type #5: Capture Territories ---
        [DefaultValue(0)]
        public int Terr_TerritoriesCapturedCount { get; set; }
        [DefaultValue(0)]
        public int Terr_TerritoriesCapturedPoints { get; set; }
        [DefaultValue(0)]
        public int Terr_TerritoriesOwnershipPoints { get; set; }

        // --- For session type #6: Boss Kill Cooperation ---
        [DefaultValue(0)]
        public int Boss_BossHitsCount { get; set; }
        [DefaultValue(0)]
        public int Boss_BossHitsPoints { get; set; }
        [DefaultValue(0)]
        public int Boss_BossKillsCount { get; set; }
        [DefaultValue(0)]
        public int Boss_BossKillsPoints { get; set; }

        // --- For session type #7: King of the Hill ---
        [DefaultValue(0)]
        public int KotH_Ticks { get; set; }
        [DefaultValue(0)]
        public int KotH_Captures { get; set; }
        [DefaultValue(0)]
        public int KotH_Points { get; set; }
        [DefaultValue(0)]
        public int KotH_PressurePoints { get; set; }
        [DefaultValue(0)]
        public int KotH_CapturePoints { get; set; }

        // --- For session type #8: Core Run (Red Light/Green Light) ---
        [DefaultValue(0)]
        public int CoreRun_CoresDelivered { get; set; }
        [DefaultValue(0)]
        public int CoreRun_CenterReached { get; set; }
        [DefaultValue(0)]
        public int CoreRun_Eliminations { get; set; } // Times died to Red Light

        public virtual void TakeItems(PvPScore rhs, bool copy = false)
        {
            Treasure_BroughtTreasuresPoints += rhs.Treasure_BroughtTreasuresPoints;
            Treasure_StolenTreasuresPoints += rhs.Treasure_StolenTreasuresPoints;
                
            List<KeyValuePair<string, PvPScore.Item>>? itemScore = null;
            if (copy)
            {
                rhs.Treasure_Items.HoldWhile(items =>
                {
                    itemScore = new(items);
                });
            }
            else
            {
                rhs.Treasure_Items.FreezeWhile(items =>
                {
                    rhs.Treasure_BroughtTreasuresPoints = 0;
                    rhs.Treasure_StolenTreasuresPoints = 0;
                    itemScore = new(items);
                    items.Clear();
                });
            }
            Treasure_Items.Add(itemScore!);
        }

        public virtual PvPScore Add(PvPScore rhs, bool transferItems = true, bool takeItems = true)
        {
            PvP_SoloKills += rhs.PvP_SoloKills;
            PvP_SoloKillsPoints += rhs.PvP_SoloKillsPoints;
            PvP_GroupKills += rhs.PvP_GroupKills;
            PvP_GroupKillsPoints += rhs.PvP_GroupKillsPoints;
            Flag_KillFlagCarrierCount += rhs.Flag_KillFlagCarrierCount;
            Flag_KillFlagCarrierPoints += rhs.Flag_KillFlagCarrierPoints;
            Flag_FlagReturnsCount += rhs.Flag_FlagReturnsCount;
            Flag_FlagReturnsPoints += rhs.Flag_FlagReturnsPoints;
            Flag_OwnershipPoints += rhs.Flag_OwnershipPoints;
            if (transferItems)
            {
                TakeItems(rhs, !takeItems);
            }
            Friends_BroughtFriendsPoints += rhs.Friends_BroughtFriendsPoints;
            Friends_BroughtFamilyBonus += rhs.Friends_BroughtFamilyBonus;
            Friends_FriendKilledCount += rhs.Friends_FriendKilledCount;
            Friends_FriendKilledPoints += rhs.Friends_FriendKilledPoints;
            Friends_KillEnemyFriendCount += rhs.Friends_KillEnemyFriendCount;
            Friends_KillEnemyFriendPoints += rhs.Friends_KillEnemyFriendPoints;
            Terr_TerritoriesCapturedCount += rhs.Terr_TerritoriesCapturedCount;
            Terr_TerritoriesCapturedPoints += rhs.Terr_TerritoriesCapturedPoints;
            Terr_TerritoriesOwnershipPoints += rhs.Terr_TerritoriesOwnershipPoints;
            Boss_BossHitsCount += rhs.Boss_BossHitsCount;
            Boss_BossHitsPoints += rhs.Boss_BossHitsPoints;
            Boss_BossKillsCount += rhs.Boss_BossKillsCount;
            Boss_BossKillsPoints += rhs.Boss_BossKillsPoints;
            KotH_Ticks += rhs.KotH_Ticks;
            KotH_Captures += rhs.KotH_Captures;
            KotH_Points += rhs.KotH_Points;
            KotH_PressurePoints += rhs.KotH_PressurePoints;
            KotH_CapturePoints += rhs.KotH_CapturePoints;
            CoreRun_CoresDelivered += rhs.CoreRun_CoresDelivered;
            CoreRun_CenterReached += rhs.CoreRun_CenterReached;
            CoreRun_Eliminations += rhs.CoreRun_Eliminations;
            return this;
        }

        /// <summary>
        /// Helper: returns total points for the current session type,
        /// summing only the relevant fields.
        /// You can expand this logic to handle any special bonus, etc.
        /// </summary>
        public int GetTotalPoints(eSessionTypes sessionType)
        {
            switch (sessionType)
            {
                case eSessionTypes.Deathmatch: // PvP Combats
                    return PvP_SoloKillsPoints + PvP_GroupKillsPoints;

                case eSessionTypes.CaptureTheFlag: // Flag Capture
                    return PvP_SoloKillsPoints + PvP_GroupKillsPoints +
                        Flag_KillFlagCarrierPoints +
                        Flag_FlagReturnsPoints +
                        Flag_OwnershipPoints;

                case eSessionTypes.TreasureHunt: // Treasure Hunt
                    return PvP_SoloKillsPoints + PvP_GroupKillsPoints +
                        Treasure_BroughtTreasuresPoints -
                        Treasure_StolenTreasuresPoints;

                case eSessionTypes.BringAFriend: // Bring Friends
                    return PvP_SoloKillsPoints + PvP_GroupKillsPoints +
                        Friends_BroughtFriendsPoints +
                        Friends_BroughtFamilyBonus -
                        Friends_FriendKilledPoints +
                        Friends_KillEnemyFriendPoints;

                case eSessionTypes.TerritoryCapture: // Capture Territories
                    return PvP_SoloKillsPoints + PvP_GroupKillsPoints +
                        Terr_TerritoriesCapturedPoints +
                        Terr_TerritoriesOwnershipPoints;

                case eSessionTypes.BossHunt: // Boss Kill Cooperation
                    return PvP_SoloKillsPoints + PvP_GroupKillsPoints +
                        Boss_BossHitsPoints +
                        Boss_BossKillsPoints;

                case eSessionTypes.KingOfTheHill: // King of the Hill
                    return PvP_SoloKillsPoints + PvP_GroupKillsPoints +
                        KotH_Points +
                        KotH_CapturePoints +
                        KotH_PressurePoints;

                case eSessionTypes.CoreRun: // Core Run
                    return PvP_SoloKillsPoints + PvP_GroupKillsPoints + 
                        (CoreRun_CoresDelivered * 20) + (CoreRun_CenterReached * 10) - (CoreRun_Eliminations * 3);

                default:
                    return 0;
            }
        }

        private string Serialize(IEnumerable<Object> list)
        {
            var str = string.Join(',', list.Select(o => Serialize(o)));
            return string.IsNullOrEmpty(str) ? string.Empty : str;
        }

        private string Serialize(object? obj)
        {
            return obj switch
            {
                string s => s,
                IEnumerable enumerable => Serialize(enumerable.Cast<Object>()),
                Item item => $"{item.Count}:{item.PointsPerItem}:{item.Id_nb}",
                _ => Convert.ToString(obj) ?? string.Empty
            };
        }

        public string Serialize()
        {
            // TODO: Take a StreamWriter as parameter and write directly
            var playerScoreType = typeof(PvPScore);
            var properties = playerScoreType.GetProperties();
            return string.Join(
                ';', properties
                    .Where(p => p.GetCustomAttribute(typeof(IgnoreDataMemberAttribute)) == null)
                    .Select(p =>
                    {
                        object? value = p.GetValue(this);

                        if (value == null || Equals(p.GetCustomAttribute<DefaultValueAttribute>()?.Value, value))
                            return string.Empty;
                        
                        var str = Serialize(value);
                        if (string.IsNullOrEmpty(str))
                            return string.Empty;
                        
                        return p.Name + '=' + str;
                    })
                    .Where(s => !string.IsNullOrEmpty(s))
            );
        }

        private static IEnumerable<Item> ParseItems(string str)
        {
            foreach (var itemStr in str.Split(','))
            {
                var values = itemStr.Split(':');
                yield return new Item(values[2], int.Parse(values[1])) { Count = int.Parse(values[0]) };
            }
        }

        private static object Parse(PropertyInfo prop, string str)
        {
            var type = prop.PropertyType;
            if (prop.Name is nameof(Treasure_ItemsSerializer))
            {
                return ParseItems(str);
            }
            else
            {
                return Convert.ChangeType(str, type);
            }
        }

        public static PvPScore Parse(IEnumerable<string> parameters, bool isSolo)
        {
            PvPScore score = new PvPScore(isSolo);
            foreach (var playerScoreInfo in parameters)
            {
                var playerScore = playerScoreInfo.Split('=');
                var playerScoreKey = playerScore[0];

                PropertyInfo? p = typeof(PvPScore).GetProperty(playerScoreKey);
                if (p == null)
                {
                    log.Warn($"PvP score \"{playerScoreKey}\" with value \"{playerScore[1]}\" of player {score.PlayerID} is unknown");
                    continue;
                }

                try
                {
                    p.SetValue(score, Parse(p, playerScore[1]));
                }
                catch (Exception ex)
                {
                    log.Warn($"Cannot set PvP score \"{playerScoreKey}\" to value \"{playerScore[1]}\": {ex}");
                }
            }
            return score; 
        }
    }

    public record PvPGroupScore(Guild Guild) : PvPScore(Guild)
    {
        public PvPGroupScore(Guild guild, IEnumerable<PvPScore> players) : this(guild)
        {
            Scores = new Dictionary<string, PvPScore>(players.Select(p => new KeyValuePair<string, PvPScore>(p.PlayerID, p)));
            foreach (var s in Scores.Values)
            {
                Add(s);
            }
        }
        
        public PvPGroupScore(Guild guild, IEnumerable<GamePlayer> players) : this(guild, players.Select(p => new PvPScore(p, false)))
        {
        }

        public PvPGroupScore(PvPGroupScore other) : base(other)
        {
            Guild = other.Guild;
            Treasure_Items = new(CopyItems(other));
            Scores = new(other.Scores.Select(kv => new KeyValuePair<string, PvPScore>(kv.Key, kv.Value.Copy())));
        }

        public Dictionary<string, PvPScore> Scores { get; init; } = new();

        public PvPScore Totals => this;
        public PvPScore GetOrCreateScore(GamePlayer player)
        {
            return GetOrCreateScore(player.InternalID!);
        }
        
        public PvPScore GetOrCreateScore(string playerId)
        {
            if (!Scores.TryGetValue(playerId, out var score))
            {
                score = new PvPScore(string.Empty, playerId, false);
                Scores[playerId] = score;
            }
            return score!;
        }

        /// <inheritdoc />
        public override PvPScore Add(PvPScore rhs, bool transferItems = true, bool takeItems = true)
        {
            base.Add(rhs, transferItems, takeItems);
            if (!Equals(rhs.PlayerID, PlayerID))
            {
                var nested = GetOrCreateScore(rhs.PlayerID);
                nested.Add(rhs, true, false); // we COPY the item data here; to be retrieved later when disbanding maybe
            }
            return this;
        }

        public int PlayerCount => Scores.Count;
    }
}
