using System;
using DOL.GS;
using DOL.Events;
using DOL.Database.Attributes;
using DOL.GS.Commands;
using System.Reflection;
using log4net;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DOL.Database
{
    /// <summary>
    /// DBBook
    /// </summary>
    [DataTable(TableName = "Book")]
    public class DBBook : DataObject
    {
        public const string TAG_PROCESSING = "processing";
        public const string TAG_STAMPED = "GuildStamped";
        public const string TAG_LEADER = "GuildLeader";

        /// <summary>
        /// Default date from the DB
        /// </summary>
        public static readonly DateTime DEFAULT_DATE = new DateTime(2000, 1, 1, 0, 0, 0);

        public static readonly Regex MemberRegex = new(@"GuildMember(\d{2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private Dictionary<string, string> m_metadata = new(StringComparer.OrdinalIgnoreCase);
        
        [ScriptLoadedEvent]
        public static void OnScriptsCompiled(DOLEvent e, object sender, EventArgs args)
        {
            GameServer.Database.RegisterDataObject(typeof(DBBook));
            log.Info("DATABASE Book LOADED");
        }

        [PrimaryKey(AutoIncrement = true)]
        public long ID { get; set; }

        [DataElement(AllowDbNull = false, Index = true)]
        public string PlayerID { get; set; }

        [DataElement(AllowDbNull = false, Index = true)]
        public string Author { get; set; }

        [DataElement(AllowDbNull = false, Index = true)]
        public string Name { get; set; }

        [DataElement(AllowDbNull = false, Index = true)]
        public string Title { get; set; }

        [DataElement(AllowDbNull = false)]
        public string Ink { get; set; }

        [DataElement(AllowDbNull = false)]
        public string InkId { get; set; }

        [DataElement(AllowDbNull = false)]
        public string Text { get; set; }

        [DataElement(AllowDbNull = false)]
        public string SerializedMetadata
        {
            get => string.Join(';', m_metadata.Select(kv => kv.Key + '=' + kv.Value));
            set
            {
                var pairs = new List<KeyValuePair<string, string>>();
                if (!string.IsNullOrEmpty(value))
                {
                    var entries = value.Split(';');
                    foreach (var entry in entries)
                    {
                        var kvp = entry.Split('=');
                        var k = kvp[0];
                        var v = kvp.Length > 1 ? kvp[1] : string.Empty;
                        if (kvp.Length == 2)
                            pairs.Add(new KeyValuePair<string, string>(k, v));
                    }
                    m_metadata = pairs.ToDictionary(kv => kv.Key, kv => kv.Value);
                }
            }
        }

        public IDictionary<string, string> Metadata
        {
            get => m_metadata;
        }
        
        public void AddTag(string key)
        {
            m_metadata[key] = string.Empty;
        }
        
        public void AddTag(string key, object value)
        {
            m_metadata[key] = value.ToString();
        }

        public string? GetTag(string key, string? orElse = null)
        {
            return m_metadata.TryGetValue(key, out string? value) ? value : orElse;
        }

        public IEnumerable<KeyValuePair<Match, string>> MatchTags(Regex regex, string? orElse = null)
        {
            return m_metadata.Select(kv => (kv.Key, regex.Match(kv.Key), kv.Value)).Where(t => t.Item2.Success).Select(t => new KeyValuePair<Match, string>(t.Item2, t.Value));
        }
        
        public T GetTag<T>(string key, T orElse = default)
        {
            if (!m_metadata.TryGetValue(key, out string? value))
                return orElse;
            
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public bool RemoveTag(string key)
        {
            return m_metadata.Remove(key);
        }

        public bool HasTag(string key)
        {
            return m_metadata.ContainsKey(key);
        }
        
        public bool HasTag(string key, object? value)
        {
            return m_metadata.TryGetValue(key, out string? v) && v == value?.ToString();
        }

        public bool IsGuildLeader(string playerID)
        {
            return HasTag(TAG_LEADER, playerID);
        }

        public bool IsGuildFounder(string playerID)
        {
            return PlayerID == playerID || IsGuildLeader(playerID) || MatchTags(MemberRegex).Any(kv => kv.Value == playerID);
        }

        [DataElement(AllowDbNull = false, Index = true)]
        public bool IsInLibrary { get; set; }

        [DataElement(AllowDbNull = false)]
        public int WordCount { get; set; }

        [DataElement(AllowDbNull = false)]
        public int BasePriceCopper { get; set; }

        [DataElement(AllowDbNull = false)]
        public int CurrentPriceCopper { get; set; }

        [DataElement(AllowDbNull = false)]
        public int UpVotes { get; set; }

        [DataElement(AllowDbNull = false)]
        public int DownVotes { get; set; }

        [DataElement(AllowDbNull = false)]
        public long RoyaltiesPendingCopper { get; set; }

        [DataElement(AllowDbNull = false)]
        public long TotalSold { get; set; }
        
        /// <summary>
        /// Four states for a guild registry:
        /// 1. !IsStamped && !IsInLibrary: book is being prepared, can be edited by guild founder only
        /// 2. IsStamped && !IsInLibrary: book is finished and stamped, waiting to be submitted, can't be edited
        /// 3. IsStamped && IsInLibrary: book is in library, guild exists, can't be edited
        /// 4. !IsStamped && IsInLibrary: book is in library, guild has been deleted, can be removed by founders
        /// </summary>
        [DataElement(AllowDbNull = false, Index = true)]
        public bool IsGuildRegistry { get; set; }

        [DataElement(AllowDbNull = false)]
        public bool IsStamped { get; set; }

        [DataElement(AllowDbNull = false)]
        public string StampBy { get; set; } = string.Empty;

        [DataElement(AllowDbNull = true)]
        public DateTime StampDate { get; set; }
        [DataElement(AllowDbNull = false, Varchar = 2, DefaultDBValue = "")]
        public string Language
        {
            get;
            set;
        } = string.Empty;

        public DBBook()
        {
        }

        public void Save()
        {
            Dirty = true;
            if (!IsPersisted)
                GameServer.Database.AddObject(this);
            else
                GameServer.Database.SaveObject(this);
        }
    }
}