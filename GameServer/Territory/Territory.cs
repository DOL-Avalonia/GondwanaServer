﻿using Discord;
using DOL.Database;
using DOL.GameEvents;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.PropertyCalc;
using DOL.GS.ServerProperties;
using DOL.GS.Spells;
using DOL.Language;
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
                this.PortalPosition = null;
            }
            else
            {
                this.PortalPosition = new Vector3(db.PortalX.Value, db.PortalY.Value, db.PortalY.Value);
            }
            this.Zone = zone;
            this.RegionId = db.RegionId;
            this.Name = db.Name;
            this.GroupId = db.GroupId;
            this.BossId = boss?.InternalID;
            this.Boss = boss;
            this.OriginalGuilds = new Dictionary<string, string>();
            this.BonusResist = new();
            this.Mobs = this.GetMobsInTerritory();
            this.SetBossAndMobsInEventInTerritory();
            this.SaveOriginalGuilds();
            this.LoadBonus(db.Bonus);
            this.IsBannerSummoned = db.IsBannerSummoned;
            guild_id = db.OwnerGuildID;

            if (!IsNeutral())
            {
                OwnerGuild = GuildMgr.GetGuildByGuildID(guild_id);
                if (OwnerGuild == null)
                {
                    log.Error($"Territory Manager cant find guild {guild_id}");
                }
                else
                {
                    if (IsBannerSummoned)
                    {
                        ToggleBanner(true);
                    }
                }
            }
        }

        public Territory(Zone zone, List<IArea> areas, string name, GameNPC boss, Vector3? portalPosition, ushort regionID, string groupId)
        {
            this.ID = Guid.NewGuid().ToString();
            this.Areas = areas;
            this.PortalPosition = portalPosition;
            this.Zone = zone;
            this.RegionId = regionID;
            this.Name = name;
            this.GroupId = groupId;
            this.BossId = boss?.InternalID;
            this.Boss = boss;
            this.OriginalGuilds = new Dictionary<string, string>();
            this.BonusResist = new();
            this.Mobs = this.GetMobsInTerritory();
            this.SetBossAndMobsInEventInTerritory();
            this.SaveOriginalGuilds();
            this.IsBannerSummoned = false;
            guild_id = null;
        }

        /// <summary>
        /// Key: MobId | Value: Original GuildName
        /// </summary>
        public Dictionary<string, string> OriginalGuilds
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

        public List<IArea> Areas
        {
            get;
        }

        public Vector3? PortalPosition
        {
            get;
            set;
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

        public bool IsBannerSummoned
        {
            get;
            set;
        }

        public int CurrentBannerResist
        {
            get;
            set;
        }

        public string ID
        {
            get;
            private set;
        }

        private Object m_lockObject = new();

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
                        if (value == m_ownerGuild)
                        {
                            return;
                        }
                        bool save = !string.Equals(value.GuildID, guild_id);
                        SetGuildOwner(value, save);
                        if (IsBannerSummoned)
                            ToggleBanner(true);
                    }
                }
            }
        }

        private void SetGuildOwner(Guild guild, bool saveChange)
        {
            //remove Territory from old Guild if any
            if (m_ownerGuild != null)
            {
                m_ownerGuild.RemoveTerritory(this);
                ToggleBanner(false);
                ClearPortal();
            }

            guild.AddTerritory(this, saveChange);
            guild_id = guild.GuildID;
            m_ownerGuild = guild;

            Mobs.ForEach(m => m.GuildName = guild.Name);
            Boss.GuildName = guild.Name;

            if (saveChange)
                SaveIntoDatabase();
        }

        private void ChangeMagicAndPhysicalResistance(GameNPC mob, int value)
        {
            eProperty Property1 = eProperty.Resist_Heat;
            eProperty Property2 = eProperty.Resist_Cold;
            eProperty Property3 = eProperty.Resist_Matter;
            eProperty Property4 = eProperty.Resist_Body;
            eProperty Property5 = eProperty.Resist_Spirit;
            eProperty Property6 = eProperty.Resist_Energy;
            eProperty Property7 = eProperty.Resist_Crush;
            eProperty Property8 = eProperty.Resist_Slash;
            eProperty Property9 = eProperty.Resist_Thrust;
            ApplyBonus(mob, Property1, value);
            ApplyBonus(mob, Property2, value);
            ApplyBonus(mob, Property3, value);
            ApplyBonus(mob, Property4, value);
            ApplyBonus(mob, Property5, value);
            ApplyBonus(mob, Property6, value);
            ApplyBonus(mob, Property7, value);
            ApplyBonus(mob, Property8, value);
            ApplyBonus(mob, Property9, value);
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

        private void ToggleBannerUnsafe(bool add)
        {
            var cls = WorldMgr.GetAllPlayingClients().Where(c => c.Player.CurrentZone.ID.Equals(Zone.ID));
            int mobBannerResist;

            if (add)
            {
                IsBannerSummoned = true;
                mobBannerResist = Properties.TERRITORYMOB_BANNER_RESIST + (OwnerGuild?.GuildLevel >= 15 ? 15 : 0);
            }
            else
            {
                IsBannerSummoned = false;
                mobBannerResist = -CurrentBannerResist;
            }

            foreach (var mob in Mobs)
            {
                RefreshEmblem(mob);

                // Update magic and physical resistance bonus
                ChangeMagicAndPhysicalResistance(mob, mobBannerResist);

                cls.Foreach(c => c.Out.SendLivingEquipmentUpdate(mob));
            }

            // Update magic and physical resistance bonus
            ChangeMagicAndPhysicalResistance(Boss, mobBannerResist);
            CurrentBannerResist += mobBannerResist;
            RefreshEmblem(Boss);
            cls.Foreach(c => c.Out.SendLivingEquipmentUpdate(Boss));


            foreach (IArea iarea in Areas)
            {
                if (!(iarea is AbstractArea area))
                {
                    log.Error($"Impossible to get items from territory {this.Name}'s area {iarea.ID} because its type is not supported");
                    continue;
                }

                if (area is Circle circle)
                {
                    Zone.GetObjectsInRadius(Zone.eGameObjectType.ITEM, circle.Position.X, circle.Position.Y, circle.Position.Z, (ushort)circle.Radius, new ArrayList(), true).OfType<TerritoryBanner>().ForEach(i => i.Emblem = OwnerGuild?.Emblem ?? i.OriginalEmblem);
                }
                else
                {
                    log.Error($"Impossible to get mobs items territory {this.Name}'s area {area.Description} ({iarea.ID}) because its type  is not supported");
                    continue;
                }
            }
        }

        public void ToggleBanner(bool add)
        {
            lock (m_lockObject)
            {
                ToggleBannerUnsafe(add);
                SaveIntoDatabase();
            }
        }

        private void ReleaseTerritory()
        {
            m_ownerGuild = null;
            ClearPortal();
            ToggleBannerUnsafe(false);
            if (Boss != null)
            {
                var gameEvents = GameEventManager.Instance.Events.FirstOrDefault(e => e.ID.Equals(Boss.EventID));

                if (gameEvents?.Mobs?.Any() == true)
                {
                    gameEvents.Mobs.ForEach(m =>
                    {
                        if (OriginalGuilds.TryGetValue(m.InternalID, out var originalName))
                        {
                            m.GuildName = originalName;
                        }
                        else
                        {
                            m.GuildName = null;
                        }
                    });
                }
            }

            if (m_ownerGuild != null)
            {
                m_ownerGuild.RemoveTerritory(this);
            }

            foreach (var mob in Mobs)
            {
                if (OriginalGuilds.ContainsKey(mob.InternalID))
                {
                    mob.GuildName = OriginalGuilds[mob.InternalID];
                }
                else
                {
                    mob.GuildName = null;
                }
            }

            Boss.RestoreOriginalGuildName();
            SaveIntoDatabase();
        }

        private void RefreshEmblem(GameNPC mob)
        {
            if (mob is not { ObjectState: eObjectState.Active, CurrentRegion: not null, Inventory: { VisibleItems: not null } })
                return;

            if (m_ownerGuild != null)
            {
                foreach (var item in mob.Inventory.VisibleItems.Where(i => i.SlotPosition == 26 || i.SlotPosition == 11))
                {
                    item.Emblem = m_ownerGuild.Emblem;
                }
            }
            else
            {
                foreach (var item in mob.Inventory.VisibleItems.Where(i => i.SlotPosition == 11 || i.SlotPosition == 26))
                {
                    var equipment = GameServer.Database.SelectObjects<NPCEquipment>(DB.Column("TemplateID").IsEqualTo(mob.EquipmentTemplateID)
                                                                                        .And(DB.Column("TemplateID").IsEqualTo(item.SlotPosition)))?.FirstOrDefault();

                    if (equipment != null)
                    {
                        item.Emblem = equipment.Emblem;
                    }
                }
            }
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
                    } else switch (parsedItem[0])
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

        public bool IsInTerritory(IArea area)
        {
            return this.Areas.Any(a => a.ID == area.ID);
        }

        public bool IsOwnedBy(Guild guild)
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
            if (player.Guild is not { GuildType: Guild.eGuildType.PlayerGuild })
            {
                return false;
            }
            return IsOwnedBy(player.Guild);
        }

        public bool IsNeutral()
        {
            return string.IsNullOrEmpty(guild_id);
        }


        private IEnumerable<GameNPC> GetMobsInTerritory()
        {
            List<GameNPC> mobs = new List<GameNPC>();

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
                    mobs.AddRange(region.GetNPCsInRadius(circle.Position, (ushort)circle.Radius, false, true).Cast<GameNPC>().Where(n => !n.IsCannotTarget));
                }
                else
                {
                    log.Error($"Impossible to get mobs from territory {this.Name}'s area {area.Description} ({iarea.ID}) because its type  is not supported");
                    continue;
                }
            }

            mobs.ForEach(m => m.IsInTerritory = true);
            return mobs;
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
            infos.Add(" Boss Id: " + this.BossId);
            infos.Add(" Boss Name: " + this.Boss.Name);
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

        public virtual void SaveIntoDatabase()
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

            if (db != null)
            {
                db.AreaIDs = String.Join('|', this.Areas.OfType<AbstractArea>().Select(a => a.DbArea.ObjectId));
                db.Name = this.Name;
                db.BossMobId = this.BossId;
                db.GroupId = this.GroupId;
                db.OwnerGuildID = this.guild_id;
                db.RegionId = this.RegionId;
                db.ZoneId = this.Zone.ID;
                db.Bonus = this.SaveBonus();
                db.IsBannerSummoned = this.IsBannerSummoned;

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
        }
    }
}