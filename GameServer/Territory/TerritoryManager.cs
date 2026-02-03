using AmteScripts.Managers;
using DOL.Database;
using DOL.events.server;
using DOL.Events;
using DOL.GameEvents;
using DOL.GS;
using DOL.GS.Commands;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.PropertyCalc;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOL.MobGroups;
using DOLDatabase.Tables;
using log4net;
using Microsoft.CodeAnalysis.Operations;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Timers;
using static DOL.GS.Area;
using static DOL.GS.GameObject;

namespace DOL.Territories
{
    public class TerritoryManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private static TerritoryManager instance;
        public static readonly ushort NEUTRAL_EMBLEM = 256;
        private readonly string BOSS_CLASS = "DOL.GS.Scripts.TerritoryBoss";
        private static Dictionary<Timer, Territory> m_TerritoriesAttacked;

        public static TerritoryManager Instance => instance ?? (instance = new TerritoryManager());

        [NotNull] public List<Territory> Territories
        {
            get;
        } = new List<Territory>();

        private TerritoryManager()
        {
            m_TerritoriesAttacked = new Dictionary<Timer, Territory>();
        }

        public bool Init()
        {
            return true;
        }

        public void TerritoryAttacked(Territory territory, GamePlayer player)
        {
            if (!m_TerritoriesAttacked.ContainsValue(territory))
            {
                Timer timer = new Timer(20000);
                timer.Elapsed += TerritoryAttackedCallback;
                timer.Enabled = true;
                m_TerritoriesAttacked.Add(timer, territory);
                territory.OwnerGuild?.SendMessageToGuildMembersKey("TerritoryManager.Territory.Attacked", eChatType.CT_Important, eChatLoc.CL_SystemWindow, territory.Name);

                if (territory.OwnerGuild != null)
                {
                    foreach (var client in WorldMgr.GetAllPlayingClients())
                    {
                        if (client.Player.Guild == territory.OwnerGuild)
                        {
                            client.Player.Out.SendSoundEffect(9213, client.Player.Position, 0);
                        }
                    }
                }

            }
        }

        private void TerritoryAttackedCallback(object sender, ElapsedEventArgs e)
        {
            Timer timer = sender as Timer;
            timer!.Stop();
            timer.Dispose();
            m_TerritoriesAttacked.Remove(timer);
        }

        [GameEventLoaded]
        public static void LoadTerritories(DOLEvent e, object sender, EventArgs arguments)
        {
            var values = GameServer.Database.SelectAllObjects<TerritoryDb>();
            int count = 0;

            if (values == null)
            {
                log.Info(count + " Territoires Chargés");
                return;
            }

            foreach (TerritoryDb territoryDb in values)
            {
                List<IArea> areas = new List<IArea>();

                if (!WorldMgr.Zones.TryGetValue(territoryDb.ZoneId, out Zone zone) || zone == null)
                {
                    log.Error($"Cannot find Zone {territoryDb.ZoneId} for territory {territoryDb.Name}");
                    continue;
                }

                foreach (var areaID in territoryDb.AreaIDs.Split('|'))
                {
                    var areaDb = GameServer.Database.SelectObjects<DBArea>(DB.Column("area_id").IsEqualTo(areaID))?.FirstOrDefault();

                    if (areaDb == null)
                    {
                        log.Error($"Cannot find Area in Database with ID {areaID} for territory {territoryDb.Name}");
                        continue;
                    }

                    var area = zone.GetAreas().OfType<AbstractArea>().FirstOrDefault(a => string.Equals(areaID, a.DbArea.ObjectId));
                    if (area == null)
                    {
                        log.Error($"Cannot find Area {areaID} for territory {territoryDb.Name}");
                        continue;
                    }

                    areas.Add(area);
                }

                MobInfo mobInfo = new();
                string[] bossInfo = null;
                string bossID = null;
                if (!string.IsNullOrEmpty(territoryDb.BossMobId))
                {
                    bossInfo = territoryDb.BossMobId.Split('|');
                    bossID = bossInfo[0];
                }

                if (!string.IsNullOrEmpty(territoryDb.GroupId))
                {
                    mobInfo = Instance.FindBossFromGroupId(territoryDb.GroupId);

                    if (!string.Equals(bossID, mobInfo.Mob.InternalID))
                    {
                        log.Error($"Territory {territoryDb.Name} ({territoryDb.ObjectId}) has Boss ID {bossID} which does not match boss {mobInfo.Mob.InternalID} from GroupId {territoryDb.GroupId}");
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(bossID))
                {
                    mobInfo.Mob = zone.GetNPCsOfZone(eRealm.None).FirstOrDefault(npc => npc.InternalID == bossID);
                    if (mobInfo.Mob == null)
                    {
                        mobInfo.Error = $"Could not find boss with ID {bossID} for territory {territoryDb.Name} ({territoryDb.ObjectId})";
                    }
                }

                if (mobInfo.Error != null)
                {
                    log.Error(mobInfo.Error);
                    return;
                }

                if (mobInfo.Mob is TerritoryLord lord)
                {
                    try
                    {
                        lord.ParseParameters(bossInfo!.Skip(1).ToArray());
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Could not load TerritoryLoard parameters ${bossID} for territory {territoryDb.Name} {territoryDb.ObjectId}: {ex.Message}");
                    }
                }

                Territory territory = new Territory(zone, areas, mobInfo.Mob, territoryDb);

                Instance.Territories.Add(territory);

                count++;
            }

            GuildMgr.GetAllGuilds().ForEach(g => g.LoadTerritories());
            log.Info(count + " Territoires Chargés");
        }

        public static Territory GetCurrentTerritory(GameObject obj)
        {
            return GetCurrentTerritory(obj.CurrentAreas);
        }

        public static Territory GetCurrentTerritory(IEnumerable<IArea> areas)
        {
            return Instance.Territories.FirstOrDefault(t => areas.Any(t.IsInTerritory));
        }

        public static Territory GetTerritoryAtArea(string areaID)
        {
            return Instance.Territories.FirstOrDefault(t => t.Areas.OfType<AbstractArea>().Any(a => string.Equals(a.DbArea.ObjectId, areaID)));
        }

        public static Territory GetTerritoryAtArea(IArea area)
        {
            return Instance.Territories.FirstOrDefault(t => t.Areas.Contains(area));
        }

        public static Territory GetTerritoryByID(string ID)
        {
            return Instance.Territories.FirstOrDefault(t => string.Equals(t.ID, ID));
        }

        public void ChangeGuildOwner(GameNPC mob, Guild guild)
        {
            if (guild is not { GuildType: Guild.eGuildType.PlayerGuild } || mob == null || string.IsNullOrEmpty(mob.InternalID))
            {
                return;
            }
            
            // TODO: Review this, this seems extremely inefficient...
            Territory territory = this.Territories.FirstOrDefault(t => t.BossId.Equals(mob.InternalID));
            //For Boss change also Guild for Mob linked in same Event
            if (mob.Event != null)
            {
                mob.Event.Mobs!.ForEach(m => m.GuildName = guild.Name);
            }

            if (territory == null || territory.Mobs == null)
            {
                log.Error("Cannot get Territory from MobId: " + mob.InternalID);
                return;
            }

            territory.OwnerGuild = guild;
        }

        public MobInfo FindBossFromGroupId(string groupId)
        {
            // TODO: Review this, this seems extremely inefficient...
            var bossEvent = GameEventManager.Instance.Events.Values.FirstOrDefault(e => e.KillStartingGroupMobId?.Equals(groupId) == true);
            if (bossEvent == null)
            {
                return new MobInfo()
                {
                    Error = "Impossible de trouver l'event lié au GroupId: " + groupId
                };
            }

            var boss = bossEvent.Mobs.FirstOrDefault(m => m.GetType().FullName!.Equals(BOSS_CLASS));

            if (boss == null)
            {
                return new MobInfo()
                {
                    Error = $"Aucun mob avec la classe {BOSS_CLASS} a été trouvé dans l'Event {bossEvent.ID}"
                };
            }

            return new MobInfo()
            {
                Mob = boss
            };
        }

        public IEnumerable<Territory> GetContextualTerritories(GamePlayer player, bool subterritories = false)
        {
            IEnumerable<Territory> all = Territories;
            all = all.Where(t => (t.Type is Territory.eType.Subterritory) == subterritories);
            if (player.IsInPvP)
            {
                if (PvpManager.Instance.CurrentSessionType is PvpManager.eSessionTypes.TerritoryCapture)
                {
                    var zones = PvpManager.Instance.CurrentZones.Select(z => z.ID).ToArray();
                    all = all.Where(t => zones.Contains(t.Zone?.ID ?? 0));
                }
                else
                    all = []; // Don't show any in other sessions
            }
            else
            {
                // TODO: Should we cache this somewhere else? Maybe in Territory?
                all = all.Where(t => t.Zone != null && !PvpSessionMgr.AllZones.Contains(t.Zone.ID));
            }
            return all;
        }

        public IList<string> GetTerritoriesInformations(GamePlayer player)
        {
            List<string> infos = new List<string>();
            List<string> ownedTerritories = new List<string>();
            List<string> otherTerritories = new List<string>();
            string language = player.Client?.Account?.Language ?? Properties.SERV_LANGUAGE;
            string neutral = LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TerritoryNeutral");
            string expiresIn = LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Subterritories.TerritoryExpiresIn");
            bool listSubs = player.IsInPvP && PvpManager.Instance.CurrentSessionType is PvpManager.eSessionTypes.TerritoryCapture;
            
            foreach (var territory in GetContextualTerritories(player, listSubs))
            {
                string line = territory.Name + " / " + territory.Zone.Description;

                if (territory.IsNeutral())
                {
                    line += " / " + neutral;
                    otherTerritories.Add(line);
                }
                else if (territory.IsOwnedBy(player))
                {
                    if (territory.ExpireTime != null)
                    {
                        line += " / " + string.Format(expiresIn, LanguageMgr.TranslateTimeLong(language, territory.ExpireTime.Value - DateTime.Now)); 
                    }
                    ownedTerritories.Add(line);
                }
                else
                {
                    line += " / " + territory.OwnerGuild?.Name ?? "????";
                    otherTerritories.Add(line);
                }
            }
            if (ownedTerritories.Any())
            {
                infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TerritoriesOwned", player.Guild.Name));
                infos.AddRange(ownedTerritories);
                infos.Add(string.Empty);
                if (otherTerritories.Count > 0)
                {
                    infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TerritoriesOther"));
                    infos.AddRange(otherTerritories);
                }
            }
            else
            {
                infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TerritoriesList"));
                infos.AddRange(otherTerritories);
            }

            // ---- Guild Territories Info ----
            infos.Add(string.Empty);
            infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.GuildInfo"));
            infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.MaxTerritories", player.Guild?.MaxTerritories ?? 0, player.Guild?.TerritoryCount ?? 0));
            infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.DailyRent", CalculateGuildTerritoryTax(player.Guild)));
            infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.DailyMeritPoints", ownedTerritories.Count * Properties.DAILY_MERIT_POINTS));
            string timeBeforeRent;
            if (ownedTerritories.Count == 0)
            {
                timeBeforeRent = LanguageMgr.GetTranslation(language, "Language.NotApplicable");
            }
            else
            {
                var nextPayment = WorldMgr.GetTimeBeforeNextDay() / 1000;
                var seconds = nextPayment % 60;
                var minutes = nextPayment / 60 % 60;
                var hours = nextPayment / 3600;
                timeBeforeRent = LanguageMgr.TranslateTimeShort(player, (int)hours, (int)minutes, (int)seconds);
            }
            infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TimeBeforeRent", timeBeforeRent));
            infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TotalXPBonus", ownedTerritories.Count == 0 ? 0 : player.Guild.TerritoryBonusExperienceFactor * 100));

            int guildBP = 0;
            if (ownedTerritories.Count > 0)
            {
                guildBP = player.Guild.CalcBPOnKill(65);
                if (Properties.BP_RATE >= 0)
                    guildBP = (int)(guildBP * Properties.BP_RATE);
                var playerBPBonus = player.GetModified(eProperty.BountyPoints);
                if (playerBPBonus != 0)
                    guildBP += (int)(playerBPBonus * 0.01 * guildBP);
            }
            
            infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TotalBPBonus", guildBP));

            // ---- Territory Bonuses ----
            infos.Add(string.Empty);
            infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.Bonuses"));
            infos.Add(LanguageMgr.GetTranslation(language, "Language.Resists") + ':');
            infos.Add("-- " + GetInfoResist(language, eResist.Natural, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + GetInfoResist(language, eResist.Spirit, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + GetInfoResist(language, eResist.Crush, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + GetInfoResist(language, eResist.Slash, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + GetInfoResist(language, eResist.Thrust, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + GetInfoResist(language, eResist.Body, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + GetInfoResist(language, eResist.Cold, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + GetInfoResist(language, eResist.Heat, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + GetInfoResist(language, eResist.Energy, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + GetInfoResist(language, eResist.Matter, player.Guild, ownedTerritories.Count));
            infos.Add("-- " + LanguageMgr.GetTranslation(language, "Language.DamageType.Melee.Noun") + (ownedTerritories.Count == 0 ? ": 0%" : $": {player.Guild.TerritoryMeleeAbsorption}%"));
            infos.Add("-- " + LanguageMgr.GetTranslation(language, "Language.DamageType.Spell.Noun") + (ownedTerritories.Count == 0 ? ": 0%" : $": {player.Guild.TerritorySpellAbsorption}%"));
            infos.Add("-- " + LanguageMgr.GetTranslation(language, "Language.DamageType.DoT.Noun") + (ownedTerritories.Count == 0 ? ": 0%" : $": {player.Guild.TerritoryDotAbsorption}%"));
            infos.Add("-- " + LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.DebuffDuration") + (ownedTerritories.Count == 0 ? ": 0%" : $": -{player.Guild.TerritoryDebuffDurationReduction}%"));
            infos.Add("-- " + LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.SpellRange") + (ownedTerritories.Count == 0 ? ": 0%" : $": {player.Guild.TerritorySpellRangeBonus}%"));
            return infos;
        }

        public IList<string> GetSubterritoriesInformations(GamePlayer player)
        {
            List<string> infos = new List<string>();
            List<string> ownedTerritories = new List<string>();
            List<string> otherTerritories = new List<string>();
            string language = player.Client?.Account?.Language ?? Properties.SERV_LANGUAGE;
            string neutral = LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Subterritories.TerritoryNeutral");
            string expiresIn = LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Subterritories.TerritoryExpiresIn");

            foreach (var territory in GetContextualTerritories(player, true))
            {
                string line = territory.Name + " / " + territory.Zone.Description;

                if (territory.IsNeutral())
                {
                    line += " / " + neutral;
                    otherTerritories.Add(line);
                }
                else if (territory.IsOwnedBy(player))
                {
                    if (territory.ExpireTime != null)
                    {
                        line += " / " + string.Format(expiresIn, LanguageMgr.TranslateTimeLong(language, territory.ExpireTime.Value - DateTime.Now));
                    }
                    ownedTerritories.Add(line);
                }
                else
                {
                    line += " / " + (territory.OwnerGuild?.Name ?? "????");
                    otherTerritories.Add(line);
                }
            }

            if (ownedTerritories.Any())
            {
                infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TerritoriesOwned", player.Guild.Name));
                infos.AddRange(ownedTerritories);
                infos.Add(string.Empty);
                if (otherTerritories.Count > 0)
                {
                    infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TerritoriesOther"));
                    infos.AddRange(otherTerritories);
                }
            }
            else
            {
                infos.Add(LanguageMgr.GetTranslation(language, "Commands.Players.Guild.Territories.TerritoriesList"));
                infos.AddRange(otherTerritories);
            }
            return infos;
        }

        private string GetInfoResist(string language, eResist resist, Guild guild, int numTerritories)
        {
            if (numTerritories == 0)
            {
                return LanguageMgr.GetResistNoun(language, resist) + ": 0%";
            }
            else
            {
                return LanguageMgr.GetResistNoun(language, resist) + $": {guild.GetResistFromTerritories(resist)}%";
            }
        }


        public static Territory GetTerritoryFromMobId(string mobId)
        {
            if (string.IsNullOrEmpty(mobId)) return null;

            foreach (var territory in Instance.Territories)
            {
                if (territory.Mobs != null &&
                    territory.Mobs.Any(m => m != null && m.InternalID != null && m.InternalID.Equals(mobId)))
                {
                    return territory;
                }
            }
            return null;
        }

        public Territory AddTerritory(Territory.eType type, IArea area, string areaId, ushort regionId, MobGroup group = null, GameNPC boss = null)
        {
            if (!WorldMgr.Regions.TryGetValue(regionId, out Region region) || areaId == null)
            {
                return null;
            }

            var coords = GetCoordinates(area);
            if (coords == Coordinate.Nowhere)
            {
                return null;
            }

            var zone = region.GetZone(coords);
            if (zone == null)
            {
                return null;
            }

            var territory = new Territory(type, zone, new List<IArea>{area}, (area as AbstractArea)?.Description ?? "New territory", boss, coords, regionId, group);

            try
            {
                territory.SaveIntoDatabase();
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return null;
            }
            this.Territories.Add(territory);

            return territory;
        }

        public static Coordinate GetCoordinates(IArea area)
        {
            return area switch
            {
                DOL.GS.Area.Circle circle => circle.Coordinate,
                Square sq => sq.Coordinate,
                Polygon poly => poly.Coordinate,
                _ => Coordinate.Nowhere
            };
        }

        public int CalculateGuildTerritoryTax(Guild? guild)
        {
            if (guild == null || guild.Territories?.Any() != true)
                return 0;

            int total = 0;
            int counter = 0;
            foreach (var territory in guild.Territories.Where(t => !t.IsSubterritory))
            {
                counter++;
                int tax = counter >= guild.MaxTerritories ? Properties.DAILY_TAX + 10 : Properties.DAILY_TAX;
                total += territory.IsBannerSummoned ? (int)Math.Round((tax * (1.0d - Properties.TERRITORY_BANNER_PERCENT_OFF / 100D))) : tax;
            }
            return total;
        }

        public void ProceedPayments()
        {
            foreach (var guildGroup in this.Territories.Where(t => !t.IsSubterritory).GroupBy(t => t.OwnerGuild))
            {
                var guild = guildGroup.Key;

                if (guild == null)
                {
                    continue;
                }
                int count = guildGroup.Count();
                bool shouldRemoveTerritories = false;
                var players = guild.GetListOfOnlineMembers();
                int tax = CalculateGuildTerritoryTax(guild);

                if (guild.TryPayTerritoryTax(Money.GetMoney(0, 0, tax, 0, 0)))
                {
                    players.Foreach(p => p.Out.SendMessage(Language.LanguageMgr.GetTranslation(p.Client.Account.Language, "Commands.Players.Guild.TerritoryPaid", tax),
                                                           eChatType.CT_Guild, eChatLoc.CL_SystemWindow));
                    int mp = count * Properties.DAILY_MERIT_POINTS;
                    guild.GainMeritPoints(mp);
                    players.Foreach(p => p.Out.SendMessage(Language.LanguageMgr.GetTranslation(p.Client.Account.Language, "Commands.Players.Guild.TerritoryMeritPoints", mp),
                                                           eChatType.CT_Guild, eChatLoc.CL_SystemWindow));
                }
                else
                {
                    players.Foreach(p =>
                    {
                        p.Out.SendMessage(Language.LanguageMgr.GetTranslation(p.Client.Account.Language, "Commands.Players.Guild.TerritoryNoMoney"), eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                        p.Out.SendSoundEffect(9214, p.Position, 0);
                    });

                    foreach (var territory in guildGroup)
                    {
                        territory.OwnerGuild = null;
                    }
                }
            }
        }
        
        public static bool IsPlayerInOwnedTerritory(GamePlayer player)
        {
            return IsPlayerInOwnedTerritory(player, player);
        }
        
        public static bool IsPlayerInOwnedTerritory(GamePlayer player, GameObject obj)
        {
            return GetCurrentTerritory(obj.CurrentAreas)?.IsOwnedBy(player) == true;
        }

        public void ReleaseSubTerritoriesInZones(IEnumerable<Zone> zones)
        {
            var allSubs = this.Territories.Where(t => t.Type == Territory.eType.Subterritory);
            foreach (var terr in allSubs)
            {
                if (zones.Contains(terr.Zone))
                {
                    terr.OwnerGuild = null;
                }
            }
        }
    }

    public class MobInfo
    {
        public GameNPC Mob
        {
            get;
            set;
        }

        public string Error
        {
            get;
            set;
        }
    }
}