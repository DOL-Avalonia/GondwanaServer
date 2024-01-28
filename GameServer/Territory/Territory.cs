using Discord;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.GS.Spells;
using DOL.Language;
using DOLDatabase.Tables;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static DOL.GS.Area;

namespace DOL.Territory
{
    public class Territory
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string id;

        public Territory(IArea area, string areaId, Vector3 center, ushort regionId, ushort zoneId, string groupId, GameNPC boss, bool IsBannerSummoned, string guild = null, string bonus = null, string id = null)
        {
            this.id = id;
            this.Area = area;
            this.Center = center;
            this.RegionId = regionId;
            this.Name = ((AbstractArea)area).Description;
            this.ZoneId = zoneId;
            this.AreaId = areaId;
            this.GroupId = groupId;
            this.BossId = boss?.InternalID;
            this.Boss = boss;
            this.Coordinates = TerritoryManager.GetCoordinates(area);
            this.Radius = this.GetRadius();
            this.OriginalGuilds = new Dictionary<string, string>();
            this.Bonus = new List<eResist>();
            this.Mobs = this.GetMobsInTerritory();
            this.SetBossAndMobsInEventInTerritory();
            this.SaveOriginalGuilds();
            this.LoadBonus(bonus);
            this.IsBannerSummoned = IsBannerSummoned;
            GuildOwner = guild;
        }

        /// <summary>
        /// Key: MobId | Value: Original GuildName
        /// </summary>
        public Dictionary<string, string> OriginalGuilds
        {
            get;
        }

        public List<eResist> Bonus
        {
            get;
        }

        public string AreaId
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public string GuildOwner
        {
            get;
            set;
        }

        public ushort RegionId
        {
            get;
            set;
        }

        public ushort ZoneId
        {
            get;
            set;
        }

        public IArea Area
        {
            get;
        }

        public Vector3 Center
        {
            get;
            init;
        }

        public IEnumerable<GameNPC> Mobs
        {
            get;
        }

        public string BossId
        {
            get;
            set;
        }

        public string GroupId
        {
            get;
            set;
        }

        public GameNPC Boss
        {
            get;
            set;
        }

        public GuildPortalNPC Portal
        {
            get;
            private set;
        }

        private RegionTimer m_portalTimer;

        private readonly object m_portalLock = new();

        public AreaCoordinate Coordinates
        {
            get;
            set;
        }

        public ushort Radius
        {
            get;
            set;
        }

        public bool IsBannerSummoned
        {
            get;
            set;
        }

        private void LoadBonus(string raw)
        {
            if (raw != null)
            {
                foreach (var item in raw.Split(new char[] { '|' }))
                {
                    if (Enum.TryParse(item, out eResist resist))
                    {
                        this.Bonus.Add(resist);
                    }
                }
            }
        }

        private string SaveBonus()
        {
            return !this.Bonus.Any() ? null : string.Join("|", this.Bonus.Select(b => (byte)b));
        }

        private void SetBossAndMobsInEventInTerritory()
        {
            if (this.Boss != null)
            {
                this.Boss.IsInTerritory = true;
                var gameEvent = GameEvents.GameEventManager.Instance.Events.FirstOrDefault(e => e.ID.Equals(this.Boss.EventID));

                if (gameEvent?.Mobs?.Any() == true)
                {
                    gameEvent.Mobs.ForEach(m => m.IsInTerritory = true);
                }
            }
        }

        protected virtual void SaveOriginalGuilds()
        {
            if (this.Mobs != null)
            {
                this.Mobs.ForEach(m => this.SaveMobOriginalGuildname(m));
            }

            if (this.Boss != null)
            {
                var gameEvent = GameEvents.GameEventManager.Instance.Events.FirstOrDefault(e => e.ID.Equals(this.Boss.EventID));

                if (gameEvent?.Mobs?.Any() == true)
                {
                    gameEvent.Mobs.ForEach(m => this.SaveMobOriginalGuildname(m));
                }
            }
        }

        protected void SaveMobOriginalGuildname(GameNPC mob)
        {
            if (!this.OriginalGuilds.ContainsKey(mob.InternalID))
            {
                this.OriginalGuilds.Add(mob.InternalID, mob.GuildName ?? string.Empty);
            }
        }


        private IEnumerable<GameNPC> GetMobsInTerritory()
        {
            List<GameNPC> mobs = new List<GameNPC>();
            if (this.Coordinates == null)
            {
                log.Error($"Impossible to get mobs from territory {this.Name} because Area with ID: {this.Area.ID} is not supported");
                return null;
            }

            if (Radius == 0)
            {
                return null;
            }

            var items = WorldMgr.Regions[this.RegionId].GetNPCsInRadius(this.Coordinates.X, this.Coordinates.Y, 0, this.Radius, false, true);

            foreach (GameObject item in items.Cast<GameObject>())
            {
                if (item is GameNPC mob && !mob.IsCannotTarget)
                {
                    mob.IsInTerritory = true;
                    mobs.Add(mob);
                }
            }

            return mobs;
        }

        private ushort GetRadius()
        {
            if (this.Area is Area.Circle circle)
            {
                return (ushort)circle.Radius;
            }
            else if (this.Area is Square sq)
            {
                if (sq.Height <= 0 || sq.Width <= 0)
                {
                    return 0;
                }

                if (sq.Height > sq.Width)
                {
                    return (ushort)Math.Ceiling(sq.Height / 2D);
                }
                else
                {
                    return (ushort)Math.Ceiling(sq.Width / 2D);
                }
            }
            else if (this.Area is Polygon poly)
            {
                return (ushort)poly.Radius;
            }
            else
            {
                log.Error($"Territory initialisation failed, cannot determine radius from Area. Area ID: {Area.ID} not supported ");
                return 0;
            }
        }

        public void SpawnPortalNpc(GamePlayer spawner)
        {

            Guild guild = spawner.Guild;
            GuildPortalNPC portalNpc = GuildPortalNPC.Create(this, spawner);
            portalNpc.AddToWorld();
            RegionTimer timer = new RegionTimer(portalNpc);
            timer.Callback = new RegionTimerCallback(PortalExpireCallback);
            timer.Interval = Properties.GUILD_PORTAL_DURATION * 1000;
            lock (m_portalLock)
            {
                if (Portal != null)
                {
                    DespawnPortalNpc();
                }
                Portal = portalNpc;
                m_portalTimer = timer;
                m_portalTimer.Start(m_portalTimer.Interval);
            }
            foreach (GamePlayer player in guild.GetListOfOnlineMembers())
            {
                player.Client.Out.SendCustomDialog(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Guild.TerritoryPortal.Called", this.Name), PlayerAcceptsSummon);
            }
        }

        public void ClearPortal()
        {
            lock (m_portalLock)
            {
                if (Portal != null)
                {
                    DespawnPortalNpc();
                }
            }
        }

        private void PlayerAcceptsSummon(GamePlayer player, byte response)
        {
            if (response == 0)
            {
                return;
            }
            lock (m_portalLock)
            {
                if (Portal == null || Portal.OwningGuild != player.Guild)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Commands.Players.Guild.TerritoryPortal.Expired"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    Portal.SummonPlayer(player);
                }
            }
        }

        private void DespawnPortalNpc()
        {
            m_portalTimer.Stop();
            m_portalTimer = null;
            Portal.RemoveFromWorld();
            Portal.Delete();
            Portal = null;
        }

        private int PortalExpireCallback(RegionTimer timer)
        {
            lock (m_portalLock)
            {
                DespawnPortalNpc();
            }
            timer.Stop();
            return 0;
        }

        /// <summary>
        /// GM Informations
        /// </summary>
        /// <returns></returns>
        public IList<string> GetInformations()
        {
            return new string[]
            {
                " Area Id: " + this.AreaId,
                " Boss Id: " + this.BossId,
                " Boss Name: " + this.Boss.Name,
                " Group Id: " + this.GroupId,
                " Region: " + this.RegionId,
                " Zone: " + this.ZoneId,
                " Guild Owner: " + (this.GuildOwner ?? "None"),
                " Bonus: " + (this.Bonus?.Any() == true ? (string.Join(" | ", this.Bonus.Select(b => b.ToString()))) : "-"),
                "",
                " Mobs -- Count( " + this.Mobs.Count() + " )",
                " Is Banner Summoned: " + this.IsBannerSummoned,
                "",
                 string.Join("\n", this.Mobs.Select(m => " * Name: " + m.Name + " |  Id: " + m.InternalID))
            };
        }


        public virtual void SaveIntoDatabase()
        {
            TerritoryDb db = null;
            bool isNew = false;

            if (this.id == null)
            {
                db = new TerritoryDb();
                isNew = true;
            }
            else
            {
                db = GameServer.Database.FindObjectByKey<TerritoryDb>(this.id);
            }

            if (db != null)
            {
                db.AreaId = this.AreaId;
                db.AreaX = this.Coordinates.X;
                db.AreaY = this.Coordinates.Y;
                db.BossMobId = this.BossId;
                db.GroupId = this.GroupId;
                db.GuidldOwner = this.GuildOwner;
                db.RegionId = this.RegionId;
                db.ZoneId = this.ZoneId;
                db.Bonus = this.SaveBonus();
                db.IsBannerSummoned = this.IsBannerSummoned;

                if (isNew)
                {
                    GameServer.Database.AddObject(db);
                    id = db.ObjectId;
                }
                else
                {
                    GameServer.Database.SaveObject(db);
                }
            }
        }
    }
}