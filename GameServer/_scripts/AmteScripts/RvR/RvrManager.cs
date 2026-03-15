using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Amte;
using AmteScripts.Utils;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOL.Territories;
using DOL.GS.Keeps;
using log4net;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.ObjectModel;

namespace AmteScripts.Managers
{
    public class RvrManager
    {
        const string ALBION = "Albion";
        const string HIBERNIA = "Hibernia";
        const string MIDGARD = "Midgard";
        
        const string RvRAlbion = "ALB";
        const string RvRHibernia = "HIB";
        const string RvRMidgard = "MID";

        private static readonly IReadOnlyCollection<eRealm> _RvRRealms = Constants.PLAYER_REALMS;

        public static string GetRealmKey(eRealm realm)
        {
            var full = realm.ToString();

            return full.Substring(0, int.Min(full.Length, 3)).ToUpperInvariant();
        }

        private static readonly string[] _RvRRealmKeys = _RvRRealms.Select(GetRealmKey).ToArray();

        public enum MapType
        {
            // Novice,
            Debutant,
            Standard,
            Expert,
            // Divine,
            Master01,
            Master02,
            Master03
        }
        
        private static readonly MapType[] _BasicMaps = [ MapType.Debutant, MapType.Standard, MapType.Expert ];
        private static readonly MapType[] _MasterMaps = [ MapType.Master01, MapType.Master02, MapType.Master03  ];
        private static readonly MapType[] _AllMaps = _BasicMaps.Concat(_MasterMaps).ToArray();

        public static IReadOnlyCollection<eRealm> RvRRealms { get; } = _RvRRealms;
        public static IReadOnlyCollection<MapType> AllMaps { get; } = _AllMaps.AsReadOnly();
        public static IReadOnlyCollection<MapType> BasicMaps { get; } = _BasicMaps.AsReadOnly();
        public static IReadOnlyCollection<MapType> MasterMaps { get; } = _MasterMaps.AsReadOnly();

        private static readonly string[] RvRMasterSpawns = _MasterMaps.SelectMany(map =>
        {
            return _RvRRealmKeys.Select(r => "RvR-" + map + '-' + r);
        }).ToArray();
        //const string RvRDivineMID = "RvR-Divine-MID";

        private static int RVR_RADIUS = Properties.RvR_AREA_RADIUS;
        private static DateTime _startTime = DateTime.Today.AddHours(20D); //20H00
        private static DateTime _endTime = _startTime.Add(TimeSpan.FromHours(4)).Add(TimeSpan.FromMinutes(5)); //4H00 + 5
        private const int _checkInterval = 30 * 1000; // 30 seconds
        private DateTime _lastRpTick = DateTime.Now;
        private static readonly Position _stuckSpawn = Position.Create(51, 434303, 493165, 3088, 1069);
        private Dictionary<ushort, IList<string>> RvrStats = new Dictionary<ushort, IList<string>>();
        private Dictionary<eRealm, int> Scores = new Dictionary<eRealm, int>();
        private Dictionary<GamePlayer, short> kills = new Dictionary<GamePlayer, short>();
        private int checkScore = 0;
        private int checkNumberOfPlayer = 0;
        private string winnerName = "";
        private DateTime RvRBonusDate = DateTime.Now.Date;

        private MapType? _currentMasterMap;
        public MapType? CurrentMasterMap => _currentMasterMap;

        #region Static part
        private static readonly Random _rng = new Random();
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private static RvrManager _instance;
        private static RegionTimer _timer;

        public static RvrManager Instance { get { return _instance; } }

        [ScriptLoadedEvent]
        public static void OnServerStarted(DOLEvent e, object sender, EventArgs args)
        {
            log.Info("RvRManger: Started");
            _instance = new RvrManager();
            foreach (var realm in _RvRRealms)
                _instance.Scores[realm] = 0;
            _timer = new RegionTimer(WorldMgr.GetRegion(1).TimeManager)
            {
                Callback = _instance._CheckRvr
            };
            _timer.Start(10000);
        }

        [ScriptUnloadedEvent]
        public static void OnServerStopped(DOLEvent e, object sender, EventArgs args)
        {
            log.Info("RvRManger: Stopped");
            _timer.Stop();
        }
        #endregion

        private bool _isOpen;
        private bool _isForcedOpen;
        private IEnumerable<ushort> _regions = Enumerable.Empty<ushort>();

        private readonly Guild _albion;
        private readonly Guild _midgard;
        private readonly Guild _hibernia;

        public bool IsOpen { get { return _isOpen; } }
        public IEnumerable<ushort> Regions { get { return _regions; } }

        public static eRealm WinnerRealm
        {
            get
            {
                switch (Instance.winnerName)
                {
                    case ALBION:
                        return eRealm.Albion;
                    case MIDGARD:
                        return eRealm.Midgard;
                    case HIBERNIA:
                        return eRealm.Hibernia;
                    default:
                        return eRealm.None;
                }
            }
        }

        public Dictionary<GamePlayer, short> Kills
        {
            get
            {
                return kills;
            }
            set
            {
                kills = value;
            }
        }

        private readonly Dictionary<MapType, RvRMap> _maps = new();

        private RvrManager()
        {
            _albion = GuildMgr.GetGuildByName(ALBION);
            if (_albion == null)
                _albion = GuildMgr.CreateGuild(eRealm.Albion, ALBION);

            _hibernia = GuildMgr.GetGuildByName(HIBERNIA);
            if (_hibernia == null)
                _hibernia = GuildMgr.CreateGuild(eRealm.Hibernia, HIBERNIA);

            _midgard = GuildMgr.GetGuildByName(MIDGARD);
            if (_midgard == null)
                _midgard = GuildMgr.CreateGuild(eRealm.Midgard, MIDGARD);

            _albion.SaveIntoDatabase();
            _midgard.SaveIntoDatabase();
            _hibernia.SaveIntoDatabase();
            InitMapsAndTerritories();

            foreach (var map in _maps.Values)
            {
                map.Realms.Foreach(m => m.KeepHoldingTime = TimeSpan.Zero);
            }
        }

        public void OnControlChange(string lordId, Guild guild)
        {
            var map = _maps.Values.FirstOrDefault(m => m.RvRTerritory != null && m.RvRTerritory.BossId.Equals(lordId));

            if (map != null)
            {
                map.RvRTerritory.OwnerGuild = guild;
                map.RvRTerritory.ToggleBanner(true);
                map.RvRTerritory.Mobs.ForEach(m =>
                {
                    m.GuildName = guild.Name;
                    m.Realm = guild.Realm;
                });
                map.RvRTerritory.Boss.Realm = guild.Realm;
                AbstractGameKeep keep = GameServer.KeepManager.GetKeepCloseToSpot(map.RvRTerritory.Boss.Position, 100000);
                keep.TempRealm = guild.Realm;
                keep.Reset(guild.Realm);
                keep.Guild = guild;
                // reset all doors
                foreach (GameKeepDoor door in keep.Doors.Values)
                {
                    door.Reset(guild.Realm);
                }
            }
        }

        public RvRTerritory GetRvRTerritory(ushort regionId)
        {
            var map = this._maps.Values.FirstOrDefault(v => v.RegionId == regionId);

            return map?.RvRTerritory;
        }

        public IEnumerable<ushort> InitMapsAndTerritories()
        {
            _maps.Clear();
            
            var allNPCs = WorldMgr.GetNPCsByGuild("RVR", eRealm.None).Where(n => n.Name.StartsWith("RvR-")).ToList();

            foreach (var mapType in _AllMaps)
            {
                var mapName = mapType.ToString();
                var npcs = allNPCs.Where(n => n.Name.StartsWith("RvR-" + mapName)).ToList();
                RvRMap? map;

                if (_maps.TryGetValue(mapType, out map))
                {
                    log.Error($"RvR: RvR-{mapName} already exists, cannot load");
                    return null;
                }
                
                map = BuildRvRMap(mapType, npcs);
                if (map != null)
                    _maps.Add(mapType, map);
            }

            _regions = _maps.Select(m => m.Value.RegionId).Distinct().ToList();
            _regions.Foreach(r => this.RvrStats[r] = new string[] { });
            return _regions;
        }

        private RvRMap? BuildRvRMap(MapType mapType, IEnumerable<GameNPC> spawnNPCs)
        {
            // TODO: It's a bit weird that we identify the map by the NPC names...
            var mapName = mapType.ToString();
            var byRegion = spawnNPCs.GroupBy(n => n.CurrentRegionID).ToList();
            if (byRegion.Count > 1)
            {
                log.Error($"RvR: RvR-{mapName} is referenced by NPCs from multiple regions { string.Join(',', byRegion) } -- not sure which region this map is supposed to be in. Map will be disabled");
                return null;
            }

            var region = WorldMgr.GetRegion(byRegion[0].Key);
            if (region == null)
            {
                log.Error($"RvR: RvR-{mapName} is in unknown region {region}");
                return null;
            }

            var regionId = byRegion[0].Key;
            var lord = (LordRvR)(region.Objects.FirstOrDefault(o => o is LordRvR));
            if (lord == null)
            {
                log.Error("Cannot Init RvR Map " + mapName + " because no LordRvR was present in Region " + regionId + ". Add a LordRvR in this RvR");
                return null;
            }
            
            var areaName = string.IsNullOrEmpty(lord.GuildName) ? mapName : lord.GuildName;
            //var areaName = "";
            var area = new Area.Circle(areaName, lord.Position.X, lord.Position.Y, lord.Position.Z, RVR_RADIUS);
            var territory = new RvRTerritory(lord.CurrentZone, new List<IArea> { area }, area.Description, lord, area.Coordinate, lord.CurrentRegionID, null);
            var map = new RvRMap()
            {
                RvRTerritory = territory,
                MapType = mapType,
                RegionId = regionId
            };

            foreach (var realm in _RvRRealms)
            {
                var key = GetRealmKey(realm);
                string spawnKey = "RvR-" + mapName + '-' + key;
                GameNPC? npc = spawnNPCs.FirstOrDefault(n => string.Equals(n.Name, spawnKey));
                if (npc == null)
                {
                    log.Warn($"No spawn point {spawnKey} found in region {regionId} for RvR Map {mapName}");
                    continue;
                }
                map[realm].Spawn = npc.Position;
            }
            return map;
        }

        public void OnPlayerLogIn(GamePlayer player)
        {
            var rvr = GameServer.Database.SelectObject<RvrPlayer>(r => r.PlayerID == player.InternalID);
            if (rvr == null)
            {
                // Lost RvR data, oops, move the player out. Unfortunately this means we lost the player's real guild as well
                if (player.Client.Account.PrivLevel > 1)
                {
                    // Leave GMs alone
                    return;
                }
                RemovePlayer(player, rvr);
            }

            if (!string.IsNullOrEmpty(rvr!.PvPSession))
                return;

            if (!string.IsNullOrEmpty(rvr.GuildID))
            {
                Guild guild = GuildMgr.GetGuildByGuildID(rvr.GuildID);

                if (guild != null)
                {
                    player.RealGuild = guild;
                }
                else if (GameServer.Instance.Logger.IsDebugEnabled)
                {
                    GameServer.Instance.Logger.DebugFormat("Could not find guild {0} for RvR player {1} ({2}) logging in", rvr.GuildID, player.Name, player.InternalID);
                }
            }
            if (!_isOpen && player.Client.Account.PrivLevel <= 1)
            {
                RemovePlayer(player, rvr);
            }
        }

        private DateTime lastHoldingUpdate = DateTime.Now;

        private int _CheckRvr(RegionTimer callingtimer)
        {
            Console.WriteLine("Check RVR");
            DateTime currentTime = DateTime.Now;
            var elapsed = currentTime - lastHoldingUpdate;
            lastHoldingUpdate = currentTime;

            if (_isOpen)
            {
                foreach (var kv in ActiveMaps())
                {
                    var map = kv.Value;
                    var holdingRealm = map.HoldingRealm;
                    if (holdingRealm != null)
                    {
                        holdingRealm.KeepHoldingTime += elapsed;
                    }
                }
            }

            if (!_isOpen)
            {
                _regions.Foreach(id => WorldMgr.GetClientsOfRegion(id).Foreach(p => RemovePlayer(p, false)));
                if (currentTime >= _startTime && currentTime < _endTime)
                    Open(false);
            }
            else
            {
                // Count the number of player in RvR
                int countPlayer = 0;
                int albPop = 0;
                int midPop = 0;
                int hibPop = 0;

                foreach (var id in _regions)
                {
                    foreach (var cl in WorldMgr.GetClientsOfRegion(id))
                    {
                        if (cl.Player.Guild == null)
                        {
                            if (cl.Account.PrivLevel <= 1)
                            {
                                RemovePlayer(cl.Player);
                            }
                        }
                        else
                        {
                            cl.Player.IsInRvR = true;
                            countPlayer++;
                        }
                    }
                }

                if (!_isForcedOpen)
                {
                    if ((currentTime < _startTime || currentTime > _endTime) && !Close())
                        _regions.Foreach(id => WorldMgr.GetClientsOfRegion(id).Foreach(c => RemovePlayer(c, false)));
                }

                // check the Score every minutes and if the number of player is less than 8 pending 5 minutes stop count the point 
                if (checkScore == 0)
                {
                    // check if the number of players is sufficient to count points
                    if (countPlayer < Properties.RvR_NUMBER_OF_NEEDED_PLAYERS)
                        checkNumberOfPlayer++;
                    else
                        checkNumberOfPlayer = 0;

                    if (checkNumberOfPlayer < 5)
                    {
                        ActiveMaps().ForEach(map =>
                        {
                            var holdingGuild = map.Value.HoldingGuild;
                            if (holdingGuild != null && Scores.ContainsKey(holdingGuild.Realm))
                            {
                                int points = map.Key switch
                                {
                                    MapType.Debutant => 1,
                                    MapType.Standard => 2,
                                    MapType.Expert => 3,
                                    _ /* Assume master */ => 4
                                };
                                Scores[holdingGuild.Realm] += points;
                            }
                        });
                    }
                }

                checkScore = (checkScore + 1) % 2;

                if ((currentTime - _lastRpTick).TotalMilliseconds >= Properties.RVR_TICK_INTERVAL_MS)
                {
                    _lastRpTick = currentTime;

                    double popFactor = 1.0;
                    if (countPlayer >= 0 && countPlayer < 21)
                    {
                        int lowPopBonusPercent = (countPlayer <= 5) ? 15 : 21 - countPlayer;
                        popFactor = 1.0 + (lowPopBonusPercent / 100.0);
                    }
                    else if (countPlayer >= 40)
                    {
                        int penaltyPercent = ((countPlayer - 40) / 10) * 5;
                        if (penaltyPercent > 0)
                            popFactor = Math.Max(0.1, 1.0 - (penaltyPercent / 100.0));
                    }

                    int maxPop = Math.Max(albPop, Math.Max(midPop, hibPop));
                    double rpRate = Properties.RP_RATE != -1 ? Properties.RP_RATE : 1.0;

                    foreach (var id in _regions)
                    {
                        foreach (var cl in WorldMgr.GetClientsOfRegion(id))
                        {
                            if (cl.Player != null && cl.Player.IsInRvR && cl.Player.IsAlive)
                            {
                                double mapFactor = cl.Player.Level switch
                                {
                                    >= 20 and < 29 => 0.25,
                                    >= 29 and < 38 => 0.50,
                                    >= 38 and < 46 => 0.75,
                                    >= 46 => 1.0,
                                    _ => 0.0
                                };

                                if (mapFactor > 0)
                                {
                                    double realmBalanceFactor = 1.0;
                                    if (countPlayer >= 21)
                                    {
                                        int myPop = 0;
                                        switch (cl.Player.Realm)
                                        {
                                            case eRealm.Albion: myPop = albPop; break;
                                            case eRealm.Midgard: myPop = midPop; break;
                                            case eRealm.Hibernia: myPop = hibPop; break;
                                        }

                                        double diffPercent = ((double)(maxPop - myPop) / countPlayer) * 100.0;
                                        int realmLowPopBonusPercent = 0;

                                        if (diffPercent >= 15.0) realmLowPopBonusPercent = 15;
                                        else if (diffPercent >= 10.0) realmLowPopBonusPercent = 10;
                                        else if (diffPercent >= 5.0) realmLowPopBonusPercent = 5;

                                        if (realmLowPopBonusPercent > 0)
                                            realmBalanceFactor = 1.0 + (realmLowPopBonusPercent / 100.0);
                                    }

                                    long tickAmount = (long)Math.Max(1, Math.Round(Properties.RVR_TICK_BASE_RP * mapFactor * popFactor * realmBalanceFactor * rpRate));
                                    cl.Player.GainRealmPoints(tickAmount, false, false, true);
                                }
                            }
                        }
                    }
                }
            }

            if (currentTime.Date > RvRBonusDate)
            {
                ClearRvRBonus();
                RvRBonusDate = currentTime.Date;
                _currentMasterMap = null;
            }

            if (currentTime > _endTime)
            {
                _startTime = _startTime.AddHours(24D);
                _endTime = _endTime.AddHours(24D);
            }

            SaveScore();

            return _checkInterval;
        }

        private void SaveScore()
        {
            if (!Directory.Exists("temp"))
                Directory.CreateDirectory("temp");
            var lines = new string[]
            {
                DateTime.Now.ToString("o"),
                Scores.TryGetValue(eRealm.Albion, out var a) ? a.ToString() : "0",
                Scores.TryGetValue(eRealm.Hibernia, out var h) ? h.ToString() : "0",
                Scores.TryGetValue(eRealm.Midgard, out var m) ? m.ToString() : "0",
                winnerName ?? string.Empty,
                _currentMasterMap?.ToString() ?? string.Empty
            };
            File.WriteAllLines("temp/RvRScore.dat", lines);
        }

        private MapType? ReadLastMasterMapFromFile()
        {
            try
            {
                var path = "temp/RvRScore.dat";
                if (!File.Exists(path)) return null;
                var lines = File.ReadAllLines(path);
                if (lines.Length >= 6)
                {
                    var val = (lines[5] ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(val) && Enum.TryParse<MapType>(val, out var mapType))
                        return mapType;
                    return null;
                }
            }
            catch { /* ignore */ }
            return null;
        }

        private void ClearRvRBonus()
        {
            WorldMgr.GetClientsOfRealm(WinnerRealm).Foreach((client) =>
            {
                client.Player.BaseBuffBonusCategory[eProperty.MythicalCoin] -= 5;
                client.Player.BaseBuffBonusCategory[eProperty.XpPoints] -= 10;
                client.Player.BaseBuffBonusCategory[eProperty.RealmPoints] -= 5;
                client.Out.SendUpdatePlayer();
            });
            winnerName = "";
        }

        public bool Open(bool force)
        {
            _isForcedOpen = force;
            if (_isOpen)
                return true;
            _isOpen = true;

            _albion.RealmPoints = 0;
            _midgard.RealmPoints = 0;
            _hibernia.RealmPoints = 0;

            _albion.MeritPoints = 0;
            _midgard.MeritPoints = 0;
            _hibernia.MeritPoints = 0;

            _albion.BountyPoints = 0;
            _midgard.BountyPoints = 0;
            _hibernia.BountyPoints = 0;

            _albion.HasGuildBanner = false;
            _midgard.HasGuildBanner = false;
            _hibernia.HasGuildBanner = false;

            foreach (var map in _maps)
                map.Value.Realms.Foreach(i => i.KeepHoldingTime = TimeSpan.Zero);

            Scores.Clear();
            foreach (var realm in _RvRRealms)
                Scores[realm] = 0;
            
            winnerName = string.Empty;

            MapType? previousPick = _currentMasterMap ?? ReadLastMasterMapFromFile();
            SelectRandomMasterMap(avoidSame: true, previous: previousPick);

            SaveScore();
            RebuildActiveRegions();

            kills = new Dictionary<GamePlayer, short>();
            checkNumberOfPlayer = 0;

            ActiveMaps().Foreach(m =>
            {
                if (m.Value.RvRTerritory != null)
                {
                    ((LordRvR)m.Value.RvRTerritory.Boss).StartRvR();
                    m.Value.RvRTerritory.Reset();
                    m.Value.RvRTerritory.ToggleBanner(false);
                }
                m.Value.Active = true;
            });

            log.Info($"RvRManager: Opened with Master map = {_currentMasterMap} (force={force})");
            return true;
        }

        private void SelectRandomMasterMap(bool avoidSame = false, MapType? previous = null)
        {
            if (_MasterMaps.Length == 0)
            {
                log.Info($"RvRManager: No master map to select");
                return;
            }
            
            MapType? old = previous ?? _currentMasterMap;
            MapType? pick;

            do
            {
                pick = _MasterMaps[_rng.Next(_MasterMaps.Length)];
            }
            while (_MasterMaps.Length > 1 && (avoidSame && old == pick));

            _currentMasterMap = pick;
            log.Info($"RvRManager: Selected Master map: {_currentMasterMap}");
        }

        private void RebuildActiveRegions()
        {
            _regions = ActiveMaps()
                .Select(kv => kv.Value.RegionId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        /// <summary>
        /// Enumerates only the maps that are active for scoring/territory today:
        /// - Debutant / Standard / Expert (always on)
        /// - Master: only the selected prefix (Master01/02/03)
        /// </summary>
        private IEnumerable<KeyValuePair<MapType, RvRMap>> ActiveMaps()
        {
            foreach (var type in _BasicMaps)
            {
                if (_maps.TryGetValue(type, out RvRMap map))
                    yield return new KeyValuePair<MapType, RvRMap>(type, map);
            }

            if (_currentMasterMap != null)
            {
                if (_maps.TryGetValue(_currentMasterMap.Value, out RvRMap map))
                    yield return new KeyValuePair<MapType, RvRMap>(_currentMasterMap.Value, map);
                else
                    log.Error($"RvR: Current master map is set to {_currentMasterMap}, but it was not found in _maps");
            }
        }
        
        public bool Close()
        {
            if (!_isOpen)
                return false;
            _isOpen = false;
            _isForcedOpen = false;

            foreach (var map in _maps)
                map.Value.Realms.Foreach(i => i.KeepHoldingTime = TimeSpan.Zero);

            string messageScore = GetMessageScore();
            WorldMgr.GetAllPlayingClients().Foreach((c) =>
            {
                string message = LanguageMgr.GetTranslation(c, "RvrManager.Score.Title") + "\n";
                message += messageScore;
                if (string.IsNullOrEmpty(winnerName))
                    message += LanguageMgr.GetTranslation(c, "RvrManager.Score.NoWinner");
                else
                    message += LanguageMgr.GetTranslation(c, "RvrManager.Score.Winner") + ": " + winnerName;
                c.Out.SendMessage(message, eChatType.CT_Help, eChatLoc.CL_SystemWindow);
            });

            ActiveMaps().Select(m => m.Value).Where(m => m.RvRTerritory != null).Foreach(m =>
            {
                ((LordRvR)m.RvRTerritory.Boss).StopRvR();
                m.RvRTerritory.Reset();
            });

            this._maps.Values.Foreach(map =>
            {
                var characters = GameServer.Database.SelectObjects<DOLCharacters>(c => c.Region == +map.RegionId);
                foreach (DOLCharacters chr in characters)
                {
                    var client = WorldMgr.GetClientByPlayerID(chr.ObjectId, true, false);
                    if (client != null)
                    {
                        RemovePlayer(client);
                    }
                    else
                    {
                        RemovePlayer(chr);
                    }
                }
            });

            string message = string.Format("RvR Scores for the {0} :\n", DateTime.Now.Date.ToString("MM/dd/yyyy"));
            message += messageScore;
            if (string.IsNullOrEmpty(winnerName))
                message += "Winner: none";
            else
                message += "Winner: " + winnerName;

            short countKilledPlayers = 0;
            short maxKills = 0;
            string champion = "";
            foreach (KeyValuePair<GamePlayer, short> killsPerPlayer in Kills)
            {
                countKilledPlayers += killsPerPlayer.Value;
                if (killsPerPlayer.Value > maxKills)
                {
                    maxKills = killsPerPlayer.Value;
                    champion = killsPerPlayer.Key.Name;
                }
            }
            if (!string.IsNullOrEmpty(champion))
            {
                var championPlayer = Kills.FirstOrDefault(kvp => kvp.Key.Name == champion).Key;
                if (championPlayer != null)
                {
                    TaskManager.UpdateTaskProgress(championPlayer, "RvRChampionOfTheDay", 1);
                }
                NewsMgr.CreateNews("GameObjects.GamePlayer.RvR.Champion", 0, eNewsType.RvRGlobal, false, true, countKilledPlayers, champion, maxKills);
                message += string.Format(LanguageMgr.GetTranslation("EN", "GameObjects.GamePlayer.RvR.Champion", countKilledPlayers, champion, maxKills));
            }
            else
            {
                NewsMgr.CreateNews("GameObjects.GamePlayer.RvR", 0, eNewsType.RvRGlobal, false, true, countKilledPlayers);
                message += string.Format(LanguageMgr.GetTranslation("EN", "GameObjects.GamePlayer.RvR", countKilledPlayers));
            }

            if (Properties.DISCORD_ACTIVE)
            {
                var hook = new DolWebHook(Properties.DISCORD_WEBHOOK_ID);
                hook.SendMessage(message);
            }

            if (!string.IsNullOrEmpty(winnerName))
                ApplyRvRBonus();

            return true;
        }

        private void ApplyRvRBonus()
        {
            WorldMgr.GetClientsOfRealm(WinnerRealm).Foreach((client) =>
            {
                client.Player.BaseBuffBonusCategory[eProperty.MythicalCoin] += 5;
                client.Player.BaseBuffBonusCategory[eProperty.XpPoints] += 10;
                client.Player.BaseBuffBonusCategory[eProperty.RealmPoints] += 5;
                client.Out.SendUpdatePlayer();
            });
        }

        private string GetMessageScore()
        {
            string result = string.Empty;

            foreach (var realm in _RvRRealms)
            {
                result += realm + ": " + (Scores.TryGetValue(realm, out int score) ? score : 0) + " points\n";
            }
            var winner = Scores.GroupBy(kv => kv.Value).OrderBy(g => g.Key)?.LastOrDefault().ToArray();
            if (winner is not { Length: 1 })
                winnerName = string.Empty;
            else
                winnerName = winner[0].Key.ToString();
            return result;
        }

        public bool AddPlayer(GamePlayer player)
        {
            if (!_isOpen || player.Level < 20)
                return false;
            if (player.Client.Account.PrivLevel == (uint)ePrivLevel.GM)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RvRManager.GMNotAllowed"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }
            RvrPlayer rvr = new RvrPlayer(player, null);
            rvr.PvPSession = "RvR";
            GameServer.Database.AddObject(rvr);

            if (player.Guild != null)
            {
                player.RealGuild = player.Guild;
                player.Guild.RemovePlayer("RVR", player);
            }

            bool isAdded = false;

            if (player.Level < 20)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "RvRManager.LowLevel"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return isAdded;
            }
            else
            {
                switch (player.Realm)
                {
                    case eRealm.Albion:
                        _albion.AddPlayer(player);
                        isAdded = this.AddPlayerToCorrectZone(player, "ALB");
                        break;

                    case eRealm.Midgard:
                        _midgard.AddPlayer(player);
                        isAdded = this.AddPlayerToCorrectZone(player, "MID");
                        break;

                    case eRealm.Hibernia:
                        _hibernia.AddPlayer(player);
                        isAdded = this.AddPlayerToCorrectZone(player, "HIB");
                        break;
                }

                if (isAdded)
                {
                    player.IsInRvR = true;
                }

                if (player.Guild != null)
                    foreach (var i in player.Inventory.AllItems.Where(i => i.Emblem != 0))
                        i.Emblem = player.Guild.Emblem;

            }
            return isAdded;
        }

        private bool AddPlayerToCorrectZone(GamePlayer player, string realm)
        {
            MapType? mapType = player.Level switch
            {
                >= 20 and < 29 => MapType.Debutant,
                >= 29 and < 38 => MapType.Standard,
                >= 38 and < 46 => MapType.Expert,
                >= 46 => CurrentMasterMap,
                _ => null
            };

            if (mapType == null)
                return false;

            if (!_maps.TryGetValue(mapType.Value, out RvRMap map))
            {
                log.Error($"RvR: No RvR map found for type {mapType} for player {player.Name} ({player.InternalID})!");
                return false;
            }

            var spawn = map[player.Realm]?.Spawn;
            if (spawn == null)
            {
                log.Error($"RvR: Map {mapType} has no spawn for player {player.Name} ({player.InternalID})!");
                return false;
            }

            player.MoveTo(spawn.Value);
            player.Bind(true);
            return true;
        }

        public void RemovePlayer(GameClient client, bool force = false)
        {
            if (client.Player != null)
                RemovePlayer(client.Player);
        }

        private void RemovePlayer(GamePlayer player, RvrPlayer rvrPlayer, bool force = false)
        {
            if (rvrPlayer == null)
            {
                if (player.Client.Account.PrivLevel <= 1)
                {
                    GameServer.Instance.Logger.Error("Player " + player.Name + " (" + player.InternalID + ") logged into RvR but RvR data was not found, potentially lost their guild info");
                }
                else if (!force) // By default, don't boot GMs
                {
                    return;
                }

                player.MoveTo(_stuckSpawn);
                if (player.Guild != null && player.Guild.GuildType == Guild.eGuildType.RvRGuild)
                    player.Guild.RemovePlayer("RVR", player);
                player.RealGuild = null;
                player.SaveIntoDatabase();
            }
            else
            {
                rvrPlayer.ResetCharacter(player);
                if (player.Client.Account.PrivLevel <= 1 || force)
                    player.MoveTo(Position.Create((ushort)rvrPlayer.OldRegion, rvrPlayer.OldX, rvrPlayer.OldY, rvrPlayer.OldZ, (ushort)rvrPlayer.OldHeading));
                if (player.Guild != null)
                    player.Guild.RemovePlayer("RVR", player);
                player.RealGuild = null;
                if (!string.IsNullOrWhiteSpace(rvrPlayer.GuildID))
                {
                    var guild = GuildMgr.GetGuildByGuildID(rvrPlayer.GuildID);
                    if (guild != null)
                    {
                        guild.AddPlayer(player, guild.GetRankByID(rvrPlayer.GuildRank), true);

                        foreach (var i in player.Inventory.AllItems.Where(i => i.Emblem != 0))
                            i.Emblem = guild.Emblem;
                    }
                }
                player.SaveIntoDatabase();
                rvrPlayer.PvPSession = "None";
                GameServer.Database.DeleteObject(rvrPlayer);
            }
        }

        public void RemovePlayer(GamePlayer player, bool force = false)
        {
            if (player.Client.Account.PrivLevel == (uint)ePrivLevel.GM)
                return;
            player.IsInRvR = false;
            var rvr = GameServer.Database.SelectObject<RvrPlayer>(r => r.PlayerID == player.InternalID);
            RemovePlayer(player, rvr);
        }

        public void RemovePlayer(DOLCharacters ch)
        {
            var rvr = GameServer.Database.SelectObject<RvrPlayer>(r => r.PlayerID == ch.ObjectId);
            if (rvr == null)
            {
                // AHHHHHHHHHHH
            }
            else
            {
                rvr.ResetCharacter(ch);
                GameServer.Database.SaveObject(ch);
                GameServer.Database.DeleteObject(rvr);
            }
        }


        private readonly Dictionary<string, List<string>> _statCache = new();
        private readonly Dictionary<string, DateTime> _statLastCacheUpdate = new();

        public IList<string> GetStatistics(GamePlayer player)
        {
            var statList = new List<string>();
            var language = player.Client.Account.Language;

            if (!_isOpen)
            {
                if (WinnerRealm == player.Realm)
                {
                    statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.WinnerBonuses"));
                    statList.Add("");
                    statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.BonusGold"));
                    statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.BonusExperience"));
                    statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.BonusRealmPoints"));
                }
                else
                {
                    statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.LoserNoBonuses"));
                }
                return statList;
            }

            // RVR Bonuses: RP/Tick, Population bonuses && Yearly events RvR RP Bonus
            int rvrPop = 0;
            int albPop = 0;
            int midPop = 0;
            int hibPop = 0;
            var yearlyEvent = DOL.GameEvents.GameEventManager.Instance.GetActiveYearlyEvent();
            int yearlyEventBonusPercent = Properties.YEARLY_EVENT_RP_BONUS_PERCENT;
            bool hasyearlyEvent = yearlyEvent != null && yearlyEventBonusPercent > 0;

            foreach (GameClient client in WorldMgr.GetAllPlayingClients())
            {
                if (client.Player != null && client.Player.IsInRvR)
                {
                    rvrPop++;
                    switch (client.Player.Realm)
                    {
                        case eRealm.Albion: albPop++; break;
                        case eRealm.Midgard: midPop++; break;
                        case eRealm.Hibernia: hibPop++; break;
                    }
                }
            }

            double mapFactor = player.Level switch
            {
                >= 20 and < 29 => 0.25,
                >= 29 and < 38 => 0.50,
                >= 38 and < 46 => 0.75,
                >= 46 => 1.0,
                _ => 0.0
            };

            double popFactor = 1.0;
            if (rvrPop >= 0 && rvrPop < 21)
            {
                int lowPopBonusPercent = (rvrPop <= 5) ? 15 : 21 - rvrPop;
                popFactor = 1.0 + (lowPopBonusPercent / 100.0);
            }
            else if (rvrPop >= 40)
            {
                int penaltyPercent = ((rvrPop - 40) / 10) * 5;
                if (penaltyPercent > 0)
                    popFactor = Math.Max(0.1, 1.0 - (penaltyPercent / 100.0));
            }

            double realmBalanceFactor = 1.0;
            int myPop = 0;
            string realmName = "";
            int maxPop = Math.Max(albPop, Math.Max(midPop, hibPop));

            switch (player.Realm)
            {
                case eRealm.Albion: myPop = albPop; realmName = "Albion"; break;
                case eRealm.Midgard: myPop = midPop; realmName = "Midgard"; break;
                case eRealm.Hibernia: myPop = hibPop; realmName = "Hibernia"; break;
            }

            if (rvrPop >= 21 && !string.IsNullOrEmpty(realmName))
            {
                double diffPercent = ((double)(maxPop - myPop) / rvrPop) * 100.0;
                int realmLowPopBonusPercent = 0;

                if (diffPercent >= 15.0) realmLowPopBonusPercent = 15;
                else if (diffPercent >= 10.0) realmLowPopBonusPercent = 10;
                else if (diffPercent >= 5.0) realmLowPopBonusPercent = 5;

                if (realmLowPopBonusPercent > 0)
                    realmBalanceFactor = 1.0 + (realmLowPopBonusPercent / 100.0);
            }

            statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.RPBonusesDisplay"));

            // 1. Tick Display
            if (mapFactor > 0)
            {
                double rpRate = Properties.RP_RATE != -1 ? Properties.RP_RATE : 1.0;
                long estimatedTickRp = (long)Math.Max(1, Math.Round(Properties.RVR_TICK_BASE_RP * mapFactor * popFactor * realmBalanceFactor * rpRate));
                statList.Add($"  {LanguageMgr.GetTranslation(language, "RvRManager.TickBonus", estimatedTickRp)}");
            }

            // 2. Global Pop (Display only the bonus, hidden penalty)
            if (rvrPop >= 0 && rvrPop < 21)
            {
                int lowPopBonusPercent = (rvrPop <= 5) ? 15 : 21 - rvrPop;
                statList.Add($"  {LanguageMgr.GetTranslation(language, "RvRManager.LowPopBonus", lowPopBonusPercent)}");
            }

            // 3. Realm Balance
            if (rvrPop >= 21 && realmBalanceFactor > 1.0)
            {
                int realmLowPopBonusPercent = (int)Math.Round((realmBalanceFactor - 1.0) * 100.0);
                statList.Add($"  {LanguageMgr.GetTranslation(language, "RvRManager.RealmLowPopBonus", realmName, realmLowPopBonusPercent)}");
            }

            // 4. Yearly Event
            if (hasyearlyEvent)
            {
                statList.Add($"  {LanguageMgr.GetTranslation(language, "RvRManager.YearlyEventBonus", yearlyEvent!.EventName, yearlyEventBonusPercent)}");
            }

            statList.Add("");

            // RvR Scores
            statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.TotalScores"));
            foreach (var realm in _RvRRealms)
            {
                statList.Add($"{realm}: " + (Scores.TryGetValue(realm, out int score) ? score : 0) + " points");
            }
            statList.Add("");

            // Current Champion
            statList.Add("--------------------------------------------------------------");
            statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.CurrentChampion") + " " + GetCurrentChampion(language));
            statList.Add("--------------------------------------------------------------");
            statList.Add("");

            TimeSpan updateDiff = TimeSpan.MaxValue;
            DateTime now = DateTime.Now;
            if (_statLastCacheUpdate.TryGetValue(language, out DateTime lastUpdate))
            {
                updateDiff = now - lastUpdate;
            }

            List<string> stats;
            // Update statistics every 30 seconds
            if (!_statCache.TryGetValue(language, out stats) || updateDiff >= TimeSpan.FromSeconds(30))
            {
                stats = new();
                _statLastCacheUpdate[language] = now;
                
                foreach (var (type, map) in ActiveMaps())
                {
                    string zoneDisplayName = type switch
                    {
                        MapType.Debutant => "Debutant (lv 20 to 28)",
                        MapType.Standard => "Standard (lv 29 to 37)",
                        MapType.Expert => "Expert (lv 38 to 45)",
                        _ /* Assume master */ => "Master (lv 46 to 50)"
                    };

                    stats.Add($"------------ RvR {zoneDisplayName} ------------");
                    var realms = _RvRRealms.ToArray();
                    int[] playerCounts = new int[realms.Length];
                    long[] guildPoints  = new long[realms.Length];
                    int albIndex = Array.IndexOf(realms, eRealm.Albion);
                    int hibIndex = Array.IndexOf(realms, eRealm.Hibernia);
                    int midIndex = Array.IndexOf(realms, eRealm.Midgard);
                    foreach (var client in WorldMgr.GetClientsOfRegion(map.RegionId))
                    {
                        int index = client.Player?.Realm switch
                        {
                            eRealm.Albion => albIndex,
                            eRealm.Hibernia => hibIndex,
                            eRealm.Midgard => midIndex,
                            _ => -1
                        };
                        
                        if (index == -1)
                            continue;

                        ++playerCounts[index];

                        if (client.Player!.Guild != null)
                            guildPoints[index] = client.Player.Guild.RealmPoints; // This is weird, but currently we don't have a list for it, so...
                    }

                    foreach (var (realm, i) in _RvRRealms.Select((realm, i) => (realm, i)))
                    {
                        stats.Add($" - {GlobalConstants.RealmToName(realm)}: {playerCounts[i]} {LanguageMgr.GetTranslation(language, "RvRManager.Players")}, {guildPoints[i]} {LanguageMgr.GetTranslation(language, "RvRManager.RealmPoints")}");
                    }

                    stats.Add("");
                    stats.Add(LanguageMgr.GetTranslation(language, "RvRManager.HoldingTime"));

                    foreach (var realm in _RvRRealms)
                    {
                        var holdingTime = map[realm]?.KeepHoldingTime ?? TimeSpan.Zero;
                        stats.Add($"   {GlobalConstants.RealmToName(realm)}: {Math.Round(holdingTime.TotalSeconds, 1)} seconds");
                    }

                    stats.Add("");

                    _statCache[language] = stats;
                }
            }

            statList.AddRange(stats);

            // Admin/GM Infos
            if (player.Client.Account.PrivLevel > 1)
            {
                statList.Add("");
                statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.Status") + (_isOpen ? " ouvert" : " fermé") + ".");
                statList.Add(LanguageMgr.GetTranslation(language, "RvRManager.UpdateFrequency"));
                statList.Add($"Current Master Map: {_currentMasterMap}");
            }

            return statList;
        }

        private string GetCurrentChampion(string language)
        {
            string champion = "";
            short maxKills = 0;
            bool tie = false;

            foreach (KeyValuePair<GamePlayer, short> killsPerPlayer in Kills)
            {
                if (killsPerPlayer.Value > maxKills)
                {
                    maxKills = killsPerPlayer.Value;
                    champion = killsPerPlayer.Key.Name;
                    tie = false;
                }
                else if (killsPerPlayer.Value == maxKills)
                {
                    tie = true;
                }
            }

            return tie ? LanguageMgr.GetTranslation(language, "RvRManager.None") : champion;
        }

        public bool IsInRvr(GameLiving obj)
        {
            return IsOpen && obj != null && _regions.Any(id => id == obj.CurrentRegionID);
        }

        public bool IsAllowedToAttack(GameLiving attacker, GameLiving defender, bool quiet)
        {
            if (attacker.Realm == defender.Realm)
            {
                if (!quiet)
                    _MessageToLiving(attacker, LanguageMgr.GetTranslation((attacker as GamePlayer)?.Client, "RvRManager.CannotAttackRealmMember"), eChatType.CT_System);
                return false;
            }
            return true;
        }

        public bool IsRvRRegion(ushort id)
        {
            return _maps.Any(kv => kv.Value.RegionId == id);
        }

        private static void _MessageToLiving(GameLiving living, string message, eChatType chatType)
        {
            if (living is GamePlayer)
                ((GamePlayer)living).Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }

    public class RvRMap
    {
        public RvRTerritory RvRTerritory { get; init; }
        
        public bool Active { get; set; }
        
        public ushort RegionId { get; init; }
        
        public RvrManager.MapType MapType { get; init; }

        public class RealmInfo(eRealm realm)
        {
            public eRealm Realm { get; set; } = realm;
            
            public Position? Spawn { get; set; } = null;

            public TimeSpan KeepHoldingTime { get; set; } = TimeSpan.Zero;
        }

        public RealmInfo[] Realms { get; } = RvrManager.RvRRealms.Select(r => new RealmInfo(r)).ToArray();

        public Guild? HoldingGuild => RvRTerritory?.OwnerGuild;

        public RealmInfo? HoldingRealm => HoldingGuild != null ? this[HoldingGuild.Realm] : null;

        public RealmInfo? this[eRealm realm]
        {
            get
            {
                int idx = (int)(realm - Constants.FIRST_PLAYER_REALM);
                if (idx >= Realms.Length)
                {
                    return null;
                }
                return Realms[idx];
            }
        }
    }
}
