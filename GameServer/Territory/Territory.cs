using Discord;
using DOL.Database;
using DOL.GameEvents;
using DOL.gameobjects.CustomNPC;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.PropertyCalc;
using DOL.GS.ServerProperties;
using DOL.GS.Spells;
using DOL.Language;
using DOL.MobGroups;
using DOLDatabase.Tables;
using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using static DOL.GS.Area;
using static DOL.GS.GameObject;

namespace DOL.Territories
{
    public class Territory
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string guild_id;

        public Territory(Zone zone, List<IArea> areas, GameNPC boss, TerritoryDb db)
        {
            this.ID = db.ObjectId;
            this.Areas = areas;
            if (db.PortalX == null || db.PortalY == null || db.PortalZ == null)
            {
                this.PortalCoordinate = null;
            }
            else
            {
                this.PortalCoordinate = Coordinate.Create(db.PortalX.Value, db.PortalY.Value, db.PortalZ.Value);
            }
            this.Zone = zone;
            this.RegionId = db.RegionId;
            this.Name = db.Name;
            this.GroupId = db.GroupId;
            this.BossId = db.BossMobId;
            this.Boss = boss;
            this.OriginalGuilds = new Dictionary<int, string>();
            this.BonusResist = new();
            this.GatherMobsInTerritory();
            this.NumMercenaries = Mobs.Count(n => n.IsMercenary);
            this.Expiration = db.Expiration;
            this.SetBossAndMobsInEventInTerritory();
            this.SaveOriginalGuilds();
            this.LoadBonus(db.Bonus);
            this.IsBannerSummoned = db.IsBannerSummoned;
            this.Type = (eType)db.Type;
            if (db.GuardNPCTemplate != null)
            {
                this.GuardNPCTemplate = NpcTemplateMgr.GetTemplate(db.GuardNPCTemplate.Value);
                if (this.GuardNPCTemplate == null)
                {
                    log.Warn($"Guard NPCTemplate {db.GuardNPCTemplate.Value} not found for territory {Name}");
                }
            }
            if (db.HealerNPCTemplate != null)
            {
                this.HealerNPCTemplate = NpcTemplateMgr.GetTemplate(db.HealerNPCTemplate.Value);
                if (this.HealerNPCTemplate == null)
                {
                    log.Warn($"Healer NPCTemplate {db.HealerNPCTemplate.Value} not found for territory {Name}");
                }
            }
            if (db.MageNPCTemplate != null)
            {
                this.MageNPCTemplate = NpcTemplateMgr.GetTemplate(db.MageNPCTemplate.Value);
                if (this.MageNPCTemplate == null)
                {
                    log.Warn($"Mage NPCTemplate {db.MageNPCTemplate.Value} not found for territory {Name}");
                }
            }
            if (db.ArcherNPCTemplate != null)
            {
                this.ArcherNPCTemplate = NpcTemplateMgr.GetTemplate(db.ArcherNPCTemplate.Value);
                if (this.ArcherNPCTemplate == null)
                {
                    log.Warn($"Archer NPCTemplate {db.ArcherNPCTemplate.Value} not found for territory {Name}");
                }
            }
            guild_id = db.OwnerGuildID;
            ClaimedTime = db.ClaimedTime;
            m_expiration = db.Expiration;

            if (!IsNeutral())
            {
                Guild guild = GuildMgr.GetGuildByGuildID(guild_id);
                if (guild == null)
                {
                    log.Warn($"Territory {Name} ({db.ObjectId}) is owned by guild with ID {guild_id} in the database but no guild with this ID was found");
                    ReleaseTerritory();
                }
            }
        }

        public Territory(eType type, Zone zone, List<IArea> areas, string name, GameNPC boss, Coordinate? portalCoordinate, ushort regionID, MobGroup group = null)
        {
            this.Type = type;
            this.Areas = areas;
            this.PortalCoordinate = portalCoordinate;
            this.Zone = zone;
            this.RegionId = regionID;
            this.Name = name;
            this.GroupId = group?.GroupId ?? string.Empty;
            this.BossId = boss?.InternalID;
            this.Boss = boss;
            this.OriginalGuilds = new Dictionary<int, string>();
            this.BonusResist = new();
            this.GatherMobsInTerritory();
            this.NumMercenaries = Mobs.Count(n => n.IsMercenary);
            this.SetBossAndMobsInEventInTerritory();
            this.SaveOriginalGuilds();
            this.IsBannerSummoned = false;
            guild_id = null;
        }

        public enum eType
        {
            Normal = 0,
            Subterritory = 1
        }

        /// <summary>
        /// Key: MobId | Value: Original GuildName
        /// </summary>
        public Dictionary<int, string> OriginalGuilds
        {
            get;
        }

        public Dictionary<eResist, int> BonusResist
        {
            get;
        }

        public int BonusMeleeAbsorption
        {
            get;
            set;
        }

        public int BonusSpellAbsorption
        {
            get;
            set;
        }

        public int BonusDoTAbsorption
        {
            get;
            set;
        }

        public int BonusReducedDebuffDuration
        {
            get;
            set;
        }

        public int BonusSpellRange
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public ushort RegionId
        {
            get;
            set;
        }

        public Zone Zone
        {
            get;
            set;
        }

        public eType Type
        {
            get;
            set;
        }

        public bool IsSubterritory => Type == eType.Subterritory;

        public List<IArea> Areas
        {
            get;
        }

        public Coordinate? PortalCoordinate
        {
            get;
            set;
        }

        public List<GameNPC> Mobs
        {
            get; private set;
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

        public bool IsBannerSummoned
        {
            get;
            private set;
        }

        public int CurrentBannerResist
        {
            get;
            private set;
        }

        public int CurrentBannerDamage
        {
            get;
            private set;
        }

        public int CurrentBannerCrit
        {
            get;
            private set;
        }

        public string ID
        {
            get;
            private set;
        }

        public NpcTemplate GuardNPCTemplate
        {
            get;
            set;
        }

        public NpcTemplate HealerNPCTemplate
        {
            get;
            set;
        }

        public NpcTemplate MageNPCTemplate
        {
            get;
            set;
        }

        public NpcTemplate ArcherNPCTemplate
        {
            get;
            set;
        }

        public int NumMercenaries
        {
            get;
            private set;
        }

        private long m_expiration;

        public long Expiration // in minutes
        {
            get => m_expiration;
            set
            {
                lock (m_lockObject)
                {
                    m_expiration = value;
                    StartExpireTimer();
                }
            }
        }

        public DateTime? ExpireTime => Expiration == 0 ? null : ClaimedTime?.AddMinutes(Expiration);

        public DateTime? RenewAvailableTime
        {
            get
            {
                if (Properties.TERRITORY_RENEW_PERCENT == 0)
                    return null;
                return ClaimedTime?.AddMinutes(Expiration * (Properties.TERRITORY_RENEW_PERCENT / 100.0f));
            }
        }


        private Timer m_expirationTimer;

        public DateTime? ClaimedTime
        {
            get;
            set;
        }

        private readonly Object m_lockObject = new();

        public string? OwnerGuildID
        {
            get => guild_id;
        }

        private Guild? m_ownerGuild;

        public Guild? OwnerGuild
        {
            get
            {
                lock (m_lockObject)
                {
                    return m_ownerGuild;
                }
            }
            set
            {
                lock (m_lockObject)
                {
                    if (value == null)
                    {
                        ReleaseTerritory();
                    }
                    else
                    {
                        ClaimedTime = DateTime.Now;
                        if (value == m_ownerGuild)
                        {
                            return;
                        }
                        bool captured = !string.Equals(value.GuildID, guild_id);
                        if (captured)
                        {
                            AwardCaptureBonuses(value);
                        }
                        SetGuildOwner(value, captured);
                        StartExpireTimer();
                        if (IsBannerSummoned)
                            ToggleBannerUnsafe(true);
                    }
                }
            }
        }

        private void AwardCaptureBonuses(Guild guild)
        {
            if (OwnerGuild == guild)
                return;

            long realmPoints = 0;

            if (OwnerGuild == null)
            {
                realmPoints = Type == eType.Subterritory ? 150 : 400;
                guild.SendMessageToGuildMembersKey("GameUtils.Guild.Territory.Capture.CapturedPvE", eChatType.CT_Guild, eChatLoc.CL_ChatWindow, guild.Name, realmPoints, Name);
            }
            else
            {
                if (Type == eType.Subterritory)
                {
                    realmPoints = 250;
                }
                else
                {
                    realmPoints = OwnerGuild.GuildLevel switch
                    {
                        <8 => 500,
                        <15 => 600,
                        <20 => 700,
                        >=20 => 800
                    };

                    if (IsBannerSummoned)
                    {
                        realmPoints += 100;
                    }
                }
                guild.SendMessageToGuildMembersKey("GameUtils.Guild.Territory.Capture.CapturedPvP", eChatType.CT_Guild, eChatLoc.CL_ChatWindow, guild.Name, realmPoints, Name, OwnerGuild.Name);
            }
            guild.RealmPoints += realmPoints;
        }

        private void SetGuildOwner(Guild guild, bool saveChange)
        {
            //remove Territory from old Guild if any
            if (m_ownerGuild != null)
            {
                m_ownerGuild.RemoveTerritory(this);
                if (Type != eType.Subterritory)
                {
                    ToggleBannerUnsafe(false);
                }
                ClearPortal();
                List<GameNPC> toRemove = Mobs.Where(mob => mob.IsMercenary).ToList();
                if (toRemove.Count > 0)
                {
                    Mobs.RemoveAll(toRemove.Contains);
                    foreach (var mob in toRemove)
                    {
                        mob.RemoveFromWorld();
                        mob.Delete();
                        mob.DeleteFromDatabase();
                    }
                }
                NumMercenaries = 0;
            }

            guild.AddTerritory(this, saveChange);
            guild_id = guild.GuildID;
            m_ownerGuild = guild;
            if (Type == eType.Subterritory)
            {
                ToggleBannerUnsafe(true);
            }

            Mobs.ForEach(m =>
            {
                m.GuildName = guild.Name;
                m.OnTerritoryOwnerChange(guild);
            });
            Boss.GuildName = guild.Name;

            if (saveChange)
                SaveIntoDatabaseUnsafe();
        }

        private void StartExpireTimer()
        {
            m_expirationTimer?.Stop();
            if (!string.IsNullOrEmpty(guild_id) && m_expiration > 0)
            {
                DateTime now = DateTime.Now;
                if (ClaimedTime == null)
                {
                    log.Warn($"Territory {Name} ({ID}) owned by guild {OwnerGuild?.Name} ({guild_id}) has an expiration but no claim timestamp ; timer starts now");
                    ClaimedTime = DateTime.Now;
                    SaveIntoDatabaseUnsafe();
                }
                DateTime expire = ClaimedTime.Value.AddMinutes(m_expiration);

                if (expire <= now)
                {
                    OwnerGuild?.SendMessageToGuildMembersKey("GameUtils.Guild.Territory.TerritoryExpired", eChatType.CT_Guild, eChatLoc.CL_ChatWindow, Name);
                    ReleaseTerritory();
                }
                else
                {
                    m_expirationTimer = new Timer();
                    m_expirationTimer.Elapsed += ExpireTimerCallback;
                    m_expirationTimer.Interval = (expire - now).TotalMilliseconds;
                    m_expirationTimer.Start();
                }
            }
        }

        private void ExpireTimerCallback(object sender, ElapsedEventArgs args)
        {
            Guild guild;

            lock (m_lockObject)
            {
                m_expirationTimer?.Stop();
                m_expirationTimer = null;
                guild = m_ownerGuild;
                ReleaseTerritory();
            }
            guild?.SendMessageToGuildMembersKey("GameUtils.Guild.Territory.TerritoryExpired", eChatType.CT_Guild, eChatLoc.CL_ChatWindow, Name);

            foreach (var player in guild.GetListOfOnlineMembers())
            {
                player.Out.SendSoundEffect(9219, player.Position, 0);
            }
        }

        private void ChangeMagicAndPhysicalResistance(GameNPC mob, int value)
        {
            eProperty[] properties =
            [
                eProperty.Resist_Heat,
                eProperty.Resist_Cold,
                eProperty.Resist_Matter,
                eProperty.Resist_Body,
                eProperty.Resist_Spirit,
                eProperty.Resist_Energy,
                eProperty.Resist_Crush,
                eProperty.Resist_Slash,
                eProperty.Resist_Thrust
            ];

            foreach (var prop in properties)
            {
                ApplyBonus(mob, prop, value);
            }
        }

        /// <summary>
        /// Method used to apply bonuses
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="Property"></param>
        /// <param name="Value"></param>
        /// <param name="IsSubstracted"></param>
        private void ApplyBonus(GameLiving owner, eProperty Property, int Value)
        {
            IPropertyIndexer tblBonusCat;
            if (Property != eProperty.Undefined)
            {
                tblBonusCat = owner.BaseBuffBonusCategory;
                tblBonusCat[(int)Property] += Value;
            }
        }

        public void RefreshBannerEffects()
        {
            lock (m_lockObject)
            {
                if (!IsBannerSummoned || Type == eType.Subterritory)
                {
                    return;
                }
                SetBannerEffects(false);
                SetBannerEffects(true);
            }
        }

        private void SetBannerEffects(bool add)
        {
            if (Type != eType.Normal)
                return;

            int mobBannerResist = 0;
            int mobBannerCrit = 0;
            int mobBannerDamage = 0;

            if (add)
            {
                mobBannerResist = OwnerGuild.TerritoryBannerResistanceBonus;
                mobBannerCrit = OwnerGuild.TerritoryBannerCriticalChanceBonus;
                mobBannerDamage = OwnerGuild.TerritoryBannerDamageBonus;
            }
            else
            {
                mobBannerResist = -CurrentBannerResist;
                mobBannerCrit = -CurrentBannerCrit;
                mobBannerDamage = -CurrentBannerDamage;
            }

            foreach (var mob in Mobs)
            {
                ChangeMagicAndPhysicalResistance(mob, mobBannerResist);
                ApplyBonus(mob, eProperty.MeleeDamage, mobBannerDamage);
                ApplyBonus(mob, eProperty.SpellDamage, mobBannerDamage);
                ApplyBonus(mob, eProperty.CriticalMeleeHitChance, mobBannerCrit);
                ApplyBonus(mob, eProperty.CriticalSpellHitChance, mobBannerCrit);
                ApplyBonus(mob, eProperty.CriticalHealHitChance, mobBannerCrit);
                ApplyBonus(mob, eProperty.CriticalArcheryHitChance, mobBannerCrit);
            }

            CurrentBannerResist += mobBannerResist;
            CurrentBannerDamage += mobBannerDamage;
            CurrentBannerCrit += mobBannerCrit;
        }

        private void ToggleBannerUnsafe(bool add)
        {
            SetBannerEffects(add);

            IsBannerSummoned = add;
            foreach (var mob in Mobs)
            {
                RefreshEmblem(mob);
            }

            foreach (IArea iarea in Areas)
            {
                if (!(iarea is AbstractArea area))
                {
                    log.Error($"Impossible to get items from territory {this.Name}'s area {iarea.ID} because its type is not supported");
                    continue;
                }

                if (area is Circle circle)
                {
                    Zone.GetObjectsInRadius(Zone.eGameObjectType.ITEM, circle.Coordinate, (ushort)circle.Radius, new ArrayList(), true).OfType<TerritoryBanner>().ForEach(i => i.Emblem = add && m_ownerGuild != null ? m_ownerGuild.Emblem : i.OriginalEmblem);
                }
                else
                {
                    log.Error($"Impossible to get mobs items territory {this.Name}'s area {area.Description} ({iarea.ID}) because its type  is not supported");
                    continue;
                }
            }
        }

        public void Add(GameNPC npc)
        {
            lock (m_lockObject)
            {
                if (this.Mobs.Contains(npc))
                {
                    return;
                }
                this.Mobs.Add(npc);
                OriginalGuilds[npc.ObjectID] = npc.GuildName ?? string.Empty;
                if (OwnerGuild != null)
                {
                    npc.GuildName = OwnerGuild.Name;
                }
                ChangeMagicAndPhysicalResistance(npc, CurrentBannerResist);
                RefreshEmblem(npc);
            }
        }

        public void Remove(GameNPC npc)
        {
            lock (m_lockObject)
            {
                if (!this.Mobs.Remove(npc))
                {
                    return;
                }
                if (OriginalGuilds.Remove(npc.ObjectID, out string ogGuild))
                {
                    npc.GuildName = ogGuild;
                }
                ChangeMagicAndPhysicalResistance(npc, -CurrentBannerResist);
                RefreshEmblem(npc);
            }
        }


        public void ToggleBanner(bool add)
        {
            lock (m_lockObject)
            {
                ToggleBannerUnsafe(add);
                SaveIntoDatabaseUnsafe();
            }
        }

        private void ReleaseTerritory()
        {
            if (m_ownerGuild != null)
            {
                m_ownerGuild.RemoveTerritory(this);
                m_ownerGuild = null;
            }
            if (m_expirationTimer != null)
            {
                m_expirationTimer.Stop();
                m_expirationTimer = null;
            }
            ClaimedTime = null;
            guild_id = string.Empty;
            ClearPortal();
            ToggleBannerUnsafe(false);
            if (Boss != null)
            {
                // TODO: review this, this seems inefficient
                var gameEvents = GameEventManager.Instance.GetEventByID(Boss.EventID);

                if (gameEvents?.Mobs?.Any() == true)
                {
                    gameEvents.Mobs.ForEach(m =>
                    {
                        if (OriginalGuilds.TryGetValue(m.ObjectID, out var originalName))
                        {
                            m.GuildName = originalName;
                        }
                        else
                        {
                            m.GuildName = null;
                        }
                        m.OnTerritoryOwnerChange(null);
                    });
                }
            }

            List<GameNPC> toRemove = new List<GameNPC>();
            foreach (var mob in Mobs)
            {
                if (OriginalGuilds.TryGetValue(mob.ObjectID, out string ogGuild))
                {
                    mob.GuildName = ogGuild;
                }
                else
                {
                    mob.GuildName = string.Empty;
                }
                
                mob.OnTerritoryOwnerChange(null);
                
                if (mob.IsMercenary)
                {
                    toRemove.Add(mob);
                }
            }
            if (toRemove.Count > 0)
            {
                Mobs.RemoveAll(toRemove.Contains);
                foreach (var mob in toRemove)
                {
                    mob.RemoveFromWorld();
                    mob.Delete();
                    mob.DeleteFromDatabase();
                }
            }

            NumMercenaries = 0;
            Boss.RestoreOriginalGuildName();
            SaveIntoDatabaseUnsafe();
        }

        private void RefreshEmblem(GameNPC mob)
        {
            if (mob is not { ObjectState: not eObjectState.Deleted, CurrentRegion: not null, Inventory: { VisibleItems: not null } })
                return;

            bool changed = false;
            if (IsBannerSummoned && m_ownerGuild != null)
            {
                foreach (var item in mob.Inventory.VisibleItems.Where(i => i.SlotPosition == 26 || i.SlotPosition == 11))
                {
                    item.Emblem = m_ownerGuild.Emblem;
                    changed = true;
                }
            }
            else
            {
                foreach (var item in mob.Inventory.VisibleItems.Where(i => i.SlotPosition == 11 || i.SlotPosition == 26))
                {
                    var equipment = GameServer.Database.SelectObjects<NPCEquipment>(DB.Column("TemplateID").IsEqualTo(mob.EquipmentTemplateID)
                                                                                        .And(DB.Column("Slot").IsEqualTo(item.SlotPosition)))?.FirstOrDefault();

                    if (equipment != null)
                    {
                        item.Emblem = equipment.Emblem;
                        changed = true;
                    }
                }
            }

            if (changed && mob.ObjectState == eObjectState.Active)
                mob.BroadcastLivingEquipmentUpdate();
        }

        private RegionTimer m_portalTimer;

        private readonly object m_portalLock = new();

        private void LoadBonus(string raw)
        {
            if (raw != null)
            {
                foreach (var item in raw.Split('|'))
                {
                    var parsedItem = item.Split(':');
                    int amount = 1;
                    if (parsedItem.Length > 1) {
                        if (!int.TryParse(parsedItem[1], out amount) || amount == 0)
                            continue;
                    }
                    if (Enum.TryParse(parsedItem[0], out eResist resist))
                    {
                        int current = 0;
                        this.BonusResist.TryGetValue(resist, out current);
                        this.BonusResist[resist] = current + amount;
                    }
                    else switch (parsedItem[0])
                    {
                        case "melee":
                            BonusMeleeAbsorption += amount;
                            break;

                        case "spell":
                            BonusSpellAbsorption += amount;
                            break;

                        case "dot":
                            BonusDoTAbsorption += amount;
                            break;

                        case "debuffduration":
                            BonusReducedDebuffDuration += amount;
                            break;

                        case "spellrange":
                            BonusSpellRange += amount;
                            break;
                    }
                }
            }
        }

        private string SaveBonus()
        {
            List<string> resists = this.BonusResist.Where(e => e.Value != 0).Select(p => ((byte)p.Key).ToString() + ':' + p.Value).ToList();

            if (BonusMeleeAbsorption != 0)
                resists.Add("melee:" + BonusMeleeAbsorption);
            if (BonusSpellAbsorption != 0)
                resists.Add("spell:" + BonusSpellAbsorption);
            if (BonusDoTAbsorption != 0)
                resists.Add("dot:" + BonusDoTAbsorption);
            if (BonusReducedDebuffDuration != 0)
                resists.Add("debuffduration:" + BonusReducedDebuffDuration);
            if (BonusSpellRange != 0)
                resists.Add("spellrange:" + BonusSpellRange);

            return resists.Count > 0 ? string.Join('|', resists) : null;
        }

        private void SetBossAndMobsInEventInTerritory()
        {
            if (this.Boss != null)
            {
                this.Boss.CurrentTerritory = this;
                GameEventManager.Instance.GetEventByID(Boss.EventID)?.Mobs.ForEach(m => m.CurrentTerritory = this);
                if (!this.Mobs.Contains(Boss))
                {
                    this.Mobs.Add(Boss);
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
                var gameEvent = GameEventManager.Instance.GetEventByID(Boss.EventID);
                if (gameEvent?.Mobs?.Any() == true)
                {
                    gameEvent.Mobs.ForEach(m => this.SaveMobOriginalGuildname(m));
                }
            }
        }

        protected void SaveMobOriginalGuildname(GameNPC mob)
        {
            if (!this.OriginalGuilds.ContainsKey(mob.ObjectID))
            {
                this.OriginalGuilds.Add(mob.ObjectID, mob.GuildName ?? string.Empty);
            }
        }

        public bool IsInTerritory(IArea area)
        {
            return this.Areas.Any(a => a.ID == area.ID);
        }

        public bool IsOwnedBy(Guild? guild)
        {
            if (guild == null)
            {
                return IsNeutral();
            }
            else
            {
                return guild == OwnerGuild || string.Equals(guild_id, guild.GuildID);
            }
        }

        public bool IsOwnedBy(GamePlayer player)
        {
            return IsOwnedBy(player.Guild);
        }

        public bool IsNeutral()
        {
            return string.IsNullOrEmpty(guild_id);
        }

        public void AddArea(AbstractArea area)
        {
            lock (m_lockObject)
            {
                if (!WorldMgr.Regions.TryGetValue(RegionId, out GS.Region region))
                {
                    log.ErrorFormat("Could not find region {0} for territory {1} ({2})", RegionId, Name, ID);
                    return;
                }
                this.Areas.Add(area);

                IEnumerable<GameNPC> mobs;
                if (area is Circle circle)
                {
                   mobs = region.GetNPCsInRadius(circle.Coordinate, (ushort)circle.Radius, false, true).Cast<GameNPC>().Where(n => !n.IsCannotTarget && n is not ShadowNPC);
                }
                else
                {
                    log.Error($"Impossible to get mobs from territory {this.Name}'s area {area.Description} ({area.ID}) because its type  is not supported");
                    return;
                }

                foreach (var mob in mobs)
                {
                    Add(mob);
                }
            }
        }

        private void GatherMobsInTerritory()
        {
            Mobs = new List<GameNPC>();

            var region = WorldMgr.Regions[this.RegionId];
            foreach (IArea iarea in Areas)
            {
                if (!(iarea is AbstractArea area))
                {
                    log.Error($"Impossible to get mobs from territory {this.Name}'s area {iarea.ID} because its type is not supported");
                    continue;
                }

                if (area is Circle circle)
                {
                    region.GetNPCsInRadius(circle.Coordinate, (ushort)circle.Radius, false, true).Cast<GameNPC>().Where(n => !n.IsCannotTarget && n is not ShadowNPC).Foreach(n => n.CurrentTerritory = this);
                }
                else
                {
                    log.Error($"Impossible to get mobs from territory {this.Name}'s area {area.Description} ({iarea.ID}) because its type  is not supported");
                    continue;
                }
            }
            if (Boss != null && !Mobs.Contains(Boss))
            {
                Mobs.Add(Boss);
            }
        }

        public void OnGuildLevelUp(Guild guild, long newLevel, long previousLevel)
        {
            if (guild != OwnerGuild)
                return;

            if (IsBannerSummoned && (previousLevel - 15 < 0) != (newLevel - 15 < 0)) // Went above or below 15
            {
                ToggleBanner(false);
                ToggleBanner(true);
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

        public GameNPC AddMercenary(GamePlayer buyer, NpcTemplate template)
        {
            GameNPC npc;

            lock (m_lockObject)
            {
                if (string.IsNullOrEmpty(template.ClassType))
                {
                    npc = new GameNPC(template);
                }
                else
                {
                    Assembly gasm = Assembly.GetAssembly(typeof(GameServer));
                    npc = (GameNPC)gasm.CreateInstance(template.ClassType, false); // Propagate exception to the caller
                    npc.LoadTemplate(template);
                }
                npc.Position = buyer.Position;
                npc.GuildName = OwnerGuild?.Name ?? template.GuildName ?? string.Empty;
                npc.AutoRespawn = false;
                npc.IsMercenary = true;
                npc.FlagsDb = (uint)npc.Flags;
                npc.LoadedFromScript = false;
                npc.SaveIntoDatabase();
                npc.AddToWorld();
                NumMercenaries += 1;
            }
            return npc;
        }

        public void OnLivingDies(GameLiving dying, GameObject killer)
        {
            if (dying is GameNPC { IsMercenary: true, AutoRespawn: false } mercenary)
            {
                --NumMercenaries;
            }
        }

        /// <summary>
        /// GM Informations
        /// </summary>
        /// <returns></returns>
        public IList<string> GetInformations()
        {
            List<string> infos = new List<string>();
            List<string> bonuses = this.BonusResist.Where(p => p.Value != 0).Select(p => p.Value.ToString() + ' ' + p.Key.ToString()).ToList();

            if (this.BonusMeleeAbsorption != 0)
                bonuses.Add(this.BonusMeleeAbsorption + " Melee");
            if (this.BonusSpellAbsorption != 0)
                bonuses.Add(this.BonusSpellAbsorption + " Spell");
            if (this.BonusDoTAbsorption != 0)
                bonuses.Add(this.BonusDoTAbsorption + " DoT");
            if (this.BonusReducedDebuffDuration != 0)
                bonuses.Add(this.BonusReducedDebuffDuration + " DebuffDuration");
            if (this.BonusSpellRange != 0)
                bonuses.Add(this.BonusSpellRange + " SpellRange");
            infos.Add(" Name: " + this.Name);
            infos.Add(" Area IDs:");
            infos.AddRange(this.Areas.OfType<AbstractArea>().Select(a => "  - " + a.DbArea.ObjectId));
            infos.Add(" Boss Id: " + this.Boss?.InternalID);
            infos.Add(" Boss Name: " + this.Boss.Name);
            if (Boss is TerritoryLord lord)
            {
                infos.AddRange(lord.GetInformations());
            }
            infos.Add(" Group Id: " + this.GroupId);
            infos.Add(" Region: " + this.RegionId);
            infos.Add(" Zone: " + $"{this.Zone.Description} ({this.Zone.ID}) ");
            infos.Add(" Guild Owner: " + (this.OwnerGuild != null ? (this.OwnerGuild.Name + $" ({this.OwnerGuild.ID})") : guild_id));
            infos.Add(" Bonus: " + (bonuses.Any() ? string.Join(" | ", bonuses) : "-"));
            infos.Add(string.Empty);
            infos.Add(" Mobs -- Count( " + this.Mobs.Count() + " )");
            infos.Add(" Is Banner Summoned: " + this.IsBannerSummoned);
            infos.Add(string.Empty);
            infos.AddRange(this.Mobs.Select(m => " * Name: " + m.Name + " |  Id: " + m.InternalID));
            return infos;
        }

        protected virtual void SaveIntoDatabaseUnsafe()
        {
            TerritoryDb db = null;
            bool isNew = false;

            if (this.ID == null)
            {
                db = new TerritoryDb();
                isNew = true;
            }
            else
            {
                db = GameServer.Database.FindObjectByKey<TerritoryDb>(this.ID);
            }

            db.AreaIDs = String.Join('|', this.Areas.OfType<AbstractArea>().Select(a => a.DbArea?.ObjectId));
            db.Name = this.Name;
            db.BossMobId = this.BossId;
            db.GroupId = this.GroupId;
            db.OwnerGuildID = this.guild_id;
            db.RegionId = this.RegionId;
            db.ZoneId = this.Zone.ID;
            db.Bonus = this.SaveBonus();
            db.IsBannerSummoned = this.IsBannerSummoned;
            db.ClaimedTime = ClaimedTime;
            db.Expiration = Expiration;
            db.Type = (int)Type;
            if (this.PortalCoordinate != null)
            {
                db.PortalX = (int)this.PortalCoordinate.Value.X;
                db.PortalY = (int)this.PortalCoordinate.Value.Y;
                db.PortalZ = (int)this.PortalCoordinate.Value.Z;
            }
            else
            {
                db.PortalX = null;
                db.PortalY = null;
                db.PortalZ = null;
            }

            if (isNew)
            {
                GameServer.Database.AddObject(db);
                this.ID = db.ObjectId;
            }
            else
            {
                GameServer.Database.SaveObject(db);
            }
        }

        public virtual void SaveIntoDatabase()
        {
            lock (m_lockObject)
            {
                SaveIntoDatabaseUnsafe();
            }
        }
    }
}