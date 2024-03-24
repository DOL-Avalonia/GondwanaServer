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
using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using DOL.Database;
using DOL.Language;
using DOL.GS.Keeps;
using log4net;
using System.Linq;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Territory;
using DOL.GS.Finance;
using System.Timers;
using System.Numerics;

namespace DOL.GS
{
    /// <summary>
    /// Guild inside the game.
    /// </summary>
    public class Guild
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public enum eRank : int
        {
            Emblem,
            AcHear,
            AcSpeak,
            Demote,
            Promote,
            GcHear,
            GcSpeak,
            Invite,
            OcHear,
            OcSpeak,
            Remove,
            Leader,
            Alli,
            View,
            Claim,
            Upgrade,
            Release,
            Buff,
            Dues,
            Withdraw,
            BuyBanner,
            Summon
        }

        public enum eBonusType : byte
        {
            None = 0,
            RealmPoints = 1,
            BountyPoints = 2,   // not live like?
            MasterLevelXP = 3,  // Not implemented
            CraftingHaste = 4,
            ArtifactXP = 5,
            Experience = 6
        }

        public static string BonusTypeToName(eBonusType bonusType)
        {
            string bonusName = "None";

            switch (bonusType)
            {
                case Guild.eBonusType.ArtifactXP:
                    bonusName = "Artifact XP";
                    break;
                case Guild.eBonusType.BountyPoints:
                    bonusName = "Bounty Points";
                    break;
                case Guild.eBonusType.CraftingHaste:
                    bonusName = "Crafting Speed";
                    break;
                case Guild.eBonusType.Experience:
                    bonusName = "PvE Experience";
                    break;
                case Guild.eBonusType.MasterLevelXP:
                    bonusName = "Master Level XP";
                    break;
                case Guild.eBonusType.RealmPoints:
                    bonusName = "Realm Points";
                    break;
            }

            return bonusName;
        }

        /// <summary>
        /// This holds all players inside the guild (InternalID, GamePlayer)
        /// </summary>
        protected readonly Dictionary<string, GamePlayer> m_onlineGuildPlayers = new Dictionary<string, GamePlayer>();

        /// <summary>
        /// This holds all new recrue in the guild who can't leave during the server property timer
        /// </summary>
        private Dictionary<GamePlayer, DateTime> m_invite_Players = new Dictionary<GamePlayer, DateTime>();

        /// <summary>
        /// This holds all old members guild in the guild who can't enter during the server property timer
        /// </summary>
        private Dictionary<GamePlayer, DateTime> m_leave_Players = new Dictionary<GamePlayer, DateTime>();

        /// <summary>
        /// Use this object to lock the guild member list
        /// </summary>
        public Object m_memberListLock = new Object();

        /// <summary>
        /// This holds all players inside the guild
        /// </summary>
        protected Alliance m_alliance = null;

        /// <summary>
        /// This holds the DB instance of the guild
        /// </summary>
        protected DBGuild m_DBguild;

        /// <summary>
        /// the runtime ID of the guild
        /// </summary>
        protected ushort m_id;

        /// <summary>
        /// Territories List containing Area Ids
        /// </summary>
        private List<string> territories;

        /// <summary>
        /// Territory Resists based on owned Territories
        /// </summary>
        private Dictionary<eResist, int> TerritoryResists;

        /// <summary>
        /// Stores claimed keeps (unique)
        /// </summary>
        protected List<AbstractGameKeep> m_claimedKeeps = new List<AbstractGameKeep>();

        public enum eGuildType
        {
            PlayerGuild,
            ServerGuild,
            RvRGuild
        }

        public eGuildType GuildType { get; set; }

        public bool IsSystemGuild
        {
            get
            {
                return GuildType is eGuildType.ServerGuild or eGuildType.RvRGuild;
            }
        }

        public eRealm Realm
        {
            get
            {
                return (eRealm)m_DBguild.Realm;
            }
            set
            {
                m_DBguild.Realm = (byte)value;
            }
        }

        public string Webpage
        {
            get
            {
                return this.m_DBguild.Webpage;
            }
            set
            {
                this.m_DBguild.Webpage = value;
            }
        }

        public IEnumerable<string> Territories => this.territories.ToList();

        public DBRank[] Ranks
        {
            get
            {
                return this.m_DBguild.Ranks;
            }
            set
            {
                this.m_DBguild.Ranks = value;
            }
        }

        public int GuildHouseNumber
        {
            get
            {
                if (m_DBguild.GuildHouseNumber == 0)
                    m_DBguild.HaveGuildHouse = false;

                return m_DBguild.GuildHouseNumber;
            }
            set
            {
                m_DBguild.GuildHouseNumber = value;

                if (value == 0)
                    m_DBguild.HaveGuildHouse = false;
                else
                    m_DBguild.HaveGuildHouse = true;
            }
        }

        public bool GuildOwnsHouse
        {
            get
            {
                if (m_DBguild.GuildHouseNumber == 0)
                    m_DBguild.HaveGuildHouse = false;

                return m_DBguild.HaveGuildHouse;
            }
            set
            {
                m_DBguild.HaveGuildHouse = value;
            }
        }

        public double GetGuildBank()
        {
            return this.m_DBguild.Bank;
        }

        public bool TryPayTerritoryTax(double tax)
        {
            if (m_DBguild.Bank >= tax)
            {
                m_DBguild.Bank -= tax;
                GameServer.Database.SaveObject(m_DBguild);
                return true;
            }

            return false;
        }

        public bool IsGuildDuesOn()
        {
            return m_DBguild.Dues;
        }

        public long GetGuildDuesPercent()
        {
            long defaultValue = 0;
            if (this.GuildLevel > 2 && this.GuildLevel < 13)
            {
                return Properties.GUILD_NEW_DUES_SYSTEM ? long.Min(m_DBguild.DuesPercent, (GuildLevel - 2) * 5) : m_DBguild.DuesPercent;
            }
            else if (this.GuildLevel == 2)
            {
                return Properties.GUILD_NEW_DUES_SYSTEM ? long.Min(m_DBguild.DuesPercent, GuildLevel) : m_DBguild.DuesPercent;
            }
            else if (this.GuildLevel >= 13)
            {
                int duesAvailable;
                if (Properties.GUILD_DUES_MAX_VALUE <= 50)
                {
                    duesAvailable = 50;
                }
                else
                {
                    int maxDuesValue = (int)Properties.GUILD_DUES_MAX_VALUE;
                    int additionalDues = (int)Math.Min((this.GuildLevel - 12) * 5, maxDuesValue - 50);
                    duesAvailable = 50 + additionalDues;
                    duesAvailable = Math.Min(duesAvailable, maxDuesValue);
                }

                return Properties.GUILD_NEW_DUES_SYSTEM ? long.Min(m_DBguild.DuesPercent, duesAvailable) : m_DBguild.DuesPercent;
            }
            return defaultValue;
        }

        public void SetGuildDues(bool dues)
        {
            m_DBguild.Dues = dues;
        }

        public void AddTerritory(string area, bool saveChanges)
        {
            if (!territories.Contains(area))
            {
                this.territories.Add(area);
                if (saveChanges)
                {
                    this.m_DBguild.Territories = string.Join("|", this.territories);
                    this.SaveIntoDatabase();
                }
            }

            var territory = TerritoryManager.Instance.Territories.FirstOrDefault(t => t.AreaId.Equals(area));

            if (territory != null)
            {
                if (territories.Count < 6)
                {
                    AddResistsFromTerritory(territory);
                }
            }
        }

        private void AddResistsFromTerritory(Territory.Territory territory)
        {
            foreach (var resist in territory.Bonus)
            {
                if (this.TerritoryResists.ContainsKey(resist))
                {
                    this.TerritoryResists[resist] += 1;
                }
                else
                {
                    this.TerritoryResists.Add(resist, 1);
                }
            }

            this.m_onlineGuildPlayers.Values.Foreach(p => p.Out.SendCharResistsUpdate());
        }

        public void RemoveTerritory(string area)
        {
            if (territories.Contains(area))
            {
                this.territories.Remove(area);
                this.m_DBguild.Territories = this.territories.Any() ? string.Join("|", this.territories) : null;
                this.SaveIntoDatabase();

                var territory = TerritoryManager.Instance.Territories.FirstOrDefault(t => t.AreaId.Equals(area));

                if (territory != null)
                {
                    foreach (var resist in territory.Bonus)
                    {
                        if (TerritoryResists.ContainsKey(resist))
                        {
                            TerritoryResists.Remove(resist);
                        }
                    }

                    this.m_onlineGuildPlayers.Values.Foreach(p => p.Out.SendCharResistsUpdate());
                }
            }
        }

        public int GetResistFromTerritories(eResist resist)
        {
            if (this.TerritoryResists.ContainsKey(resist))
            {
                return this.TerritoryResists[resist];
            }

            return 0;
        }

        public bool DoesGuildOwnTerritory(string area)
        {
            if (area == null)
            {
                return false;
            }

            return this.territories.Contains(area);
        }

        public void SetGuildDuesMaxPercent(long dues)
        {
            if (IsGuildDuesOn() == true)
            {
                this.m_DBguild.DuesPercent = dues;
            }
            else
            {
                this.m_DBguild.DuesPercent = 0;
            }
        }
        /// <summary>
        /// Set guild bank command 
        /// </summary>
        /// <param name="donating"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public void SetGuildBank(GamePlayer donating, double amount)
        {
            if (donating == null || donating.Guild == null)
                return;

            if (amount < 0)
            {
                donating.Out.SendMessage(LanguageMgr.GetTranslation(donating.Client, "Commands.Players.Guild.DepositInvalid"), eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                return;
            }
            else if ((donating.Guild.GetGuildBank() + amount) >= 1000000001)
            {
                donating.Out.SendMessage(LanguageMgr.GetTranslation(donating.Client, "Commands.Players.Guild.DepositFull"), eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!donating.RemoveMoney(Currency.Copper.Mint(long.Parse(amount.ToString()))))
            {
                donating.Out.SendMessage(LanguageMgr.GetTranslation(donating.Client, "Commands.Players.Guild.DepositNoMoney"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return;
            }

            donating.Out.SendMessage(LanguageMgr.GetTranslation(donating.Client, "Commands.Players.Guild.DepositAmount", Money.GetString(long.Parse(amount.ToString()))), eChatType.CT_Guild, eChatLoc.CL_SystemWindow);

            donating.Guild.UpdateGuildWindow();
            m_DBguild.Bank += amount;

            InventoryLogging.LogInventoryAction(donating, "", "(GUILD;" + Name + ")", eInventoryActionType.Other, (long)amount);
            donating.Out.SendUpdatePlayer();
            return;
        }
        public void WithdrawGuildBank(GamePlayer player, double amount, bool withdraw = true)
        {
            if (amount < 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Guild.WithdrawInvalid"), eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                return;
            }
            else if ((player.Guild.GetGuildBank() - amount) < 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Guild.WithdrawTooMuch"), eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                return;
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Guild.Withdrawamount", Money.GetString(long.Parse(amount.ToString()))), eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
            player.Guild.UpdateGuildWindow();
            m_DBguild.Bank -= amount;

            if (withdraw)
            {
                var amt = long.Parse(amount.ToString());
                player.AddMoney(Currency.Copper.Mint(amt));
                InventoryLogging.LogInventoryAction("", "(GUILD;" + Name + ")", player, eInventoryActionType.Other, amt);
                player.Out.SendUpdatePlayer();
                player.SaveIntoDatabase();
            }
            player.Guild.SaveIntoDatabase();
            return;
        }
        /// <summary>
        /// Creates an empty Guild. Don't use this, use
        /// GuildMgr.CreateGuild() to create a guild
        /// </summary>
        public Guild(DBGuild dbGuild)
        {
            this.m_DBguild = dbGuild;
            bannerStatus = "None";
            this.territories = new List<string>();
            this.TerritoryResists = new Dictionary<eResist, int>();
        }

        public void LoadTerritories()
        {
            if (!string.IsNullOrEmpty(m_DBguild.Territories))
            {
                foreach (var area in m_DBguild.Territories.Split(new char[] { '|' }))
                {
                    var territory = TerritoryManager.Instance.Territories.FirstOrDefault(t => t.AreaId.Equals(area));

                    if (territory != null && string.IsNullOrEmpty(territory.GuildOwner))
                    {
                        TerritoryManager.Instance.ChangeGuildOwner(this, territory);
                    }
                }
            }
        }

        public uint GuildPortalAvailableTick
        {
            get;
            set;
        }

        public uint GuildCombatZoneAvailableTick
        {
            get;
            set;
        }

        public int Emblem
        {
            get
            {
                return this.m_DBguild.Emblem;
            }
            set
            {
                this.m_DBguild.Emblem = value;
                this.SaveIntoDatabase();
            }
        }

        /// <summary>
        /// Whether the guild has bought a banner
        /// </summary>
        public bool HasGuildBanner
        {
            get
            {
                return this.m_DBguild.GuildBanner;
            }
            set
            {
                this.m_DBguild.GuildBanner = value;
                this.SaveIntoDatabase();
            }
        }

        /// <summary>
        /// Guild banner currently active
        /// </summary>
        public GuildBanner ActiveGuildBanner
        {
            get;
            set;
        }

        public DateTime GuildBannerLostTime
        {
            get => m_DBguild.GuildBannerLostTime;
            set
            {
                this.m_DBguild.GuildBannerLostTime = value;
                this.SaveIntoDatabase();
            }
        }

        public string Omotd
        {
            get
            {
                return this.m_DBguild.oMotd;
            }
            set
            {
                this.m_DBguild.oMotd = value;
                this.SaveIntoDatabase();
            }
        }

        public string Motd
        {
            get
            {
                return this.m_DBguild.Motd;
            }
            set
            {
                this.m_DBguild.Motd = value;
                this.SaveIntoDatabase();
            }
        }

        public string AllianceId
        {
            get
            {
                return this.m_DBguild.AllianceID;
            }
            set
            {
                this.m_DBguild.AllianceID = value;
                this.SaveIntoDatabase();
            }
        }

        /// <summary>
        /// Gets or sets the guild alliance
        /// </summary>
        public Alliance alliance
        {
            get
            {
                return m_alliance;
            }
            set
            {
                m_alliance = value;
            }
        }

        /// <summary>
        /// Gets or sets the guild id
        /// </summary>
        public string GuildID
        {
            get
            {
                return m_DBguild.GuildID;
            }
            set
            {
                m_DBguild.GuildID = value;
                this.SaveIntoDatabase();
            }
        }

        /// <summary>
        /// Gets or sets the runtime guild id
        /// </summary>
        public ushort ID
        {
            get
            {
                return m_id;
            }
            set
            {
                m_id = value;
            }
        }

        /// <summary>
        /// Gets or sets the guild name
        /// </summary>
        public string Name
        {
            get
            {
                return m_DBguild.GuildName;
            }
            set
            {
                m_DBguild.GuildName = value;
                this.SaveIntoDatabase();
            }
        }

        public long RealmPoints
        {
            get
            {
                return this.m_DBguild.RealmPoints;
            }
            set
            {
                var previousLevel = GuildLevel;
                this.m_DBguild.RealmPoints = value;
                var newLevel = GuildLevel;
                if (newLevel > previousLevel)
                {
                    string newCommands = (newLevel switch
                    {
                        1 => "/gc buff",
                        2 => "/gc dues",
                        3 => "/gc territorybanner, /gc buyterritorydefender & /gc movedefender",
                        4 => "/gc territoryportal",
                        5 => "/gc combatzone",
                        6 => "/gc jailrelease",
                        7 => "/gc buybanner, /gc summon,  /gc unsummon, /gc edit buybanner & /gc edit summonbanner",
                        _ => null
                    })!;

                    foreach (GamePlayer player in GetListOfOnlineMembers())
                    {
                        string msg = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.LevelUp", Name, newLevel);
                        player.Out.SendMessage(msg, eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                        player.Out.SendMessage(msg, eChatType.CT_Guild, eChatLoc.CL_PopupWindow);
                        player.Out.SendMessage(msg, eChatType.CT_Guild, eChatLoc.CL_ChatWindow);
                        if (newCommands != null)
                        {
                            if (newLevel is 3 or 7)
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.CommandsAvailable", newCommands), eChatType.CT_Guild, eChatLoc.CL_PopupWindow);
                            }
                            else
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.CommandAvailable", newCommands), eChatType.CT_Guild, eChatLoc.CL_PopupWindow);
                            }
                        }

                        string moreTerritories = newLevel switch
                        {
                            0 or 1 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreTerritories01"),
                            3 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreTerritories02"),
                            4 or 5 or 6 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreTerritories03"),
                            7 or 8 or 9 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreTerritories04"),
                            >= 10 and <= 13 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreTerritories05"),
                            >= 14 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreTerritories06"),
                            _ => null
                        };
                        string moreDefenders = newLevel switch
                        {
                            3 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreDefenders01"),
                            4 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreDefenders02"),
                            5 => null,
                            6 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreDefenders03"),
                            7 => null,
                            8 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreDefenders04"),
                            9 => null,
                            10 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreDefenders05"),
                            11 => null,
                            12 => null,
                            13 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreDefenders06"),
                            14 => null,
                            15 => null,
                            16 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreDefenders07"),
                            17 => null,
                            18 => null,
                            19 => null,
                            20 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MoreDefenders08"),
                            _ => null
                        };
                        string miscInfos = newLevel switch
                        {
                            2 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos01"),
                            3 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos02", Properties.TERRITORY_BANNER_PERCENT_OFF, Properties.TERRITORYMOB_BANNER_RESIST),
                            4 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos03", Properties.GUILD_PORTAL_DURATION / 60),
                            5 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos04", Properties.GUILD_COMBAT_ZONE_DURATION / 60),
                            6 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos05"),
                            7 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos06"),
                            8 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos07"),
                            11 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos08"),
                            13 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos09"),
                            15 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos10", Properties.TERRITORYMOB_BANNER_RESIST),
                            16 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos11"),
                            18 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos12"),
                            20 => LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MiscInfos13"),
                            _ => null
                        };

                        if (!string.IsNullOrEmpty(miscInfos))
                        {
                            player.Out.SendMessage(miscInfos, eChatType.CT_Guild, eChatLoc.CL_PopupWindow);
                        }
                        if (!string.IsNullOrEmpty(moreTerritories))
                        {
                            player.Out.SendMessage(moreTerritories, eChatType.CT_Guild, eChatLoc.CL_PopupWindow);
                        }
                        if (!string.IsNullOrEmpty(moreDefenders))
                        {
                            player.Out.SendMessage(moreDefenders, eChatType.CT_Guild, eChatLoc.CL_PopupWindow);
                        }
                        if (Properties.GUILD_NEW_DUES_SYSTEM)
                        {
                            if (newLevel > 2 && newLevel < 13)
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MaxDuesAvailable", (newLevel - 2) * 5), eChatType.CT_Guild, eChatLoc.CL_PopupWindow);
                            }
                            else if (newLevel == 2)
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MaxDuesAvailable", newLevel), eChatType.CT_Guild, eChatLoc.CL_PopupWindow);
                            }
                            else if (newLevel >= 13)
                            {
                                int duesAvailable;
                                if (Properties.GUILD_DUES_MAX_VALUE <= 50)
                                {
                                    duesAvailable = 50;
                                }
                                else
                                {
                                    int maxDuesValue = (int)Properties.GUILD_DUES_MAX_VALUE;
                                    int additionalDues = (int)Math.Min((newLevel - 12) * 5, maxDuesValue - 50);
                                    duesAvailable = 50 + additionalDues;
                                    duesAvailable = Math.Min(duesAvailable, maxDuesValue);
                                }

                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.MaxDuesAvailable", duesAvailable), eChatType.CT_Guild, eChatLoc.CL_PopupWindow);
                            }
                        }
                    }

                    NewsMgr.CreateNews("GameUtils.Guild.LevelUp", Realm, eNewsType.RvRGlobal, false, true, Name, newLevel);
                    if (DOL.GS.ServerProperties.Properties.DISCORD_ACTIVE)
                    {
                        DolWebHook hook = new DolWebHook(DOL.GS.ServerProperties.Properties.DISCORD_WEBHOOK_ID);
                        hook.SendMessage(LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "GameUtils.Guild.LevelUp", Name, newLevel));
                    }
                }
                this.SaveIntoDatabase();
            }
        }

        public long BountyPoints
        {
            get
            {
                return this.m_DBguild.BountyPoints;
            }
            set
            {
                this.m_DBguild.BountyPoints = value;
                this.SaveIntoDatabase();
            }
        }

        /// <summary>
        /// Gets or sets the guild claimed keep
        /// </summary>
        public List<AbstractGameKeep> ClaimedKeeps
        {
            get { return m_claimedKeeps; }
            set { m_claimedKeeps = value; }
        }

        /// <summary>
        /// Returns the number of players online inside this guild
        /// </summary>
        public int MemberOnlineCount
        {
            get
            {
                return m_onlineGuildPlayers.Count;
            }
        }

        public Quests.AbstractMission Mission = null;

        /// <summary>
        /// Adds a player to the guild
        /// </summary>
        /// <param name="player">GamePlayer to be added to the guild</param>
        /// <returns>true if added successfully</returns>
        public bool AddOnlineMember(GamePlayer player)
        {
            if (player == null) return false;
            lock (m_memberListLock)
            {
                if (!m_onlineGuildPlayers.ContainsKey(player.InternalID))
                {
                    if (!player.IsAnonymous)
                        NotifyGuildMembers(player);

                    m_onlineGuildPlayers.Add(player.InternalID, player);
                    return true;
                }
            }

            return false;
        }

        private void NotifyGuildMembers(GamePlayer member)
        {
            foreach (GamePlayer player in m_onlineGuildPlayers.Values)
            {
                if (player == member) continue;
                if (player.ShowGuildLogins)
                    player.Out.SendMessage("Guild member " + member.Name + " has logged in!", DOL.GS.PacketHandler.eChatType.CT_System, DOL.GS.PacketHandler.eChatLoc.CL_SystemWindow);
            }
        }

        /// <summary>
        /// Removes a player from the guild
        /// </summary>
        /// <param name="player">GamePlayer to be removed</param>
        /// <returns>true if removed, false if not</returns>
        public bool RemoveOnlineMember(GamePlayer player)
        {
            lock (m_memberListLock)
            {
                if (m_onlineGuildPlayers.ContainsKey(player.InternalID))
                {
                    m_onlineGuildPlayers.Remove(player.InternalID);

                    // now update the all member list to display lastonline time instead of zone
                    Dictionary<string, GuildMgr.GuildMemberDisplay> memberList = GuildMgr.GetAllGuildMembers(player.GuildID);

                    if (memberList != null && memberList.ContainsKey(player.InternalID))
                    {
                        memberList[player.InternalID].ZoneOrOnline = DateTime.Now.ToShortDateString();
                    }

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Remove all Members from memory
        /// </summary>
        public void ClearOnlineMemberList()
        {
            lock (m_memberListLock)
            {
                m_onlineGuildPlayers.Clear();
            }
        }

        /// <summary>
        /// Returns a player according to the matching membername
        /// </summary>
        /// <returns>GuildMemberEntry</returns>
        public GamePlayer GetOnlineMemberByID(string memberID)
        {
            lock (m_memberListLock)
            {
                if (m_onlineGuildPlayers.ContainsKey(memberID))
                    return m_onlineGuildPlayers[memberID];
            }

            return null;
        }

        /// <summary>
        /// Add a player to a guild at rank 9
        /// </summary>
        /// <param name="addPlayer"></param>
        /// <returns></returns>
        public bool AddPlayer(GamePlayer addPlayer, bool force = false)
        {
            return AddPlayer(addPlayer, GetRankByID(9), force);
        }

        /// <summary>
        /// Add a player to a guild with the specified rank
        /// </summary>
        /// <param name="addPlayer"></param>
        /// <param name="rank"></param>
        /// <returns></returns>
        public bool AddPlayer(GamePlayer addPlayer, DBRank rank, bool force = false)
        {
            if (addPlayer == null || addPlayer.Guild != null)
                return false;


            if (!force && !IsSystemGuild && m_leave_Players.ContainsKey(addPlayer) && addPlayer.Client.Account.PrivLevel == 1)
            {
                int time = Properties.RECRUITMENT_TIMER_OPTION - (int)DateTime.Now.Subtract(m_leave_Players[addPlayer]).TotalMinutes;
                addPlayer.Client.Out.SendMessage(LanguageMgr.GetTranslation(addPlayer.Client.Account.Language, "Commands.Players.Guild.NotAbleToBeInvited", time), eChatType.CT_Advise, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (log.IsDebugEnabled)
                log.Debug("Adding player to the guild, guild name=\"" + Name + "\"; player name=" + addPlayer.Name);

            try
            {
                AddOnlineMember(addPlayer);
                addPlayer.GuildName = Name;
                addPlayer.GuildID = GuildID;
                addPlayer.GuildRank = rank;
                addPlayer.Guild = this;
                addPlayer.SaveIntoDatabase();
                GuildMgr.AddPlayerToAllGuildPlayersList(addPlayer);
                addPlayer.Out.SendMessage("You have agreed to join " + this.Name + "!", eChatType.CT_Group, eChatLoc.CL_SystemWindow);
                addPlayer.Out.SendMessage("Your current rank is " + addPlayer.GuildRank.Title + "!", eChatType.CT_Group, eChatLoc.CL_SystemWindow);
                SendMessageToGuildMembers(addPlayer.Name + " has joined the guild!", eChatType.CT_Group, eChatLoc.CL_SystemWindow);
                addPlayer.Client.Out.SendCharResistsUpdate();
                if (IsSystemGuild || force || addPlayer.Client.Account.PrivLevel != 1)
                    return true;
                m_invite_Players.Add(addPlayer, DateTime.Now);
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error("AddPlayer", e);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Delete's a member from this Guild
        /// </summary>
        /// <param name="removername">the player (client) removing</param>
        /// <param name="member">the player named beeing remove</param>
        /// <returns>true or false</returns>
        public bool RemovePlayer(string removername, GamePlayer member)
        {
            GameClient remover = WorldMgr.GetClientByPlayerName(removername, false, true);
            if (!IsSystemGuild && remover is { Account.PrivLevel: < 2 } && m_invite_Players.ContainsKey(member))
            {
                int time = Properties.RECRUITMENT_TIMER_OPTION - (int)DateTime.Now.Subtract(m_invite_Players[member]).TotalMinutes;
                if (member.Name == removername)
                    member.Client.Out.SendMessage(LanguageMgr.GetTranslation(member.Client.Account.Language, "Commands.Players.Guild.NotAbleToLeave", time), eChatType.CT_Advise, eChatLoc.CL_SystemWindow);
                else
                {
                    remover.Out.SendMessage(LanguageMgr.GetTranslation(remover.Account.Language, "Commands.Players.Guild.NotAbleToBeExpelled", time), eChatType.CT_Advise, eChatLoc.CL_SystemWindow);
                }
                return false;
            }
            try
            {
                GuildMgr.RemovePlayerFromAllGuildPlayersList(member);
                RemoveOnlineMember(member);
                member.GuildName = "";
                member.GuildNote = "";
                member.GuildID = "";
                member.GuildRank = null;
                member.Guild = null;
                member.SaveIntoDatabase();

                member.Out.SendObjectGuildID(member, member.Guild);
                // Send message to removerClient about successful removal
                if (removername == member.Name)
                    member.Out.SendMessage("You leave the guild.", DOL.GS.PacketHandler.eChatType.CT_System, DOL.GS.PacketHandler.eChatLoc.CL_SystemWindow);
                else
                    member.Out.SendMessage(removername + " removed you from " + this.Name, PacketHandler.eChatType.CT_System, PacketHandler.eChatLoc.CL_SystemWindow);

                member.Client.Out.SendCharResistsUpdate();
                if (IsSystemGuild || remover is { Account.PrivLevel: > 1 })
                    return true;
                if (!m_leave_Players.ContainsKey(member))
                    m_leave_Players.Add(member, DateTime.Now);
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error("RemovePlayer", e);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Looks up if a given client have access for the specific command in this guild
        /// </summary>
        /// <returns>true or false</returns>
        public bool HasRank(GamePlayer member, Guild.eRank rankNeeded)
        {
            try
            {
                // Is the player in the guild at all?
                if (!m_onlineGuildPlayers.ContainsKey(member.InternalID))
                {
                    log.Debug("Player " + member.Name + " (" + member.InternalID + ") is not a member of guild " + Name);
                    return false;
                }

                // If player have a privlevel above 1, it has access enough
                if (member.Client.Account.PrivLevel > 1)
                    return true;

                if (member.GuildRank == null)
                {
                    if (log.IsWarnEnabled)
                        log.Warn("Rank not in db for player " + member.Name);

                    return false;
                }

                // If player is leader, yes
                if (member.GuildRank.RankLevel == 0)
                    return true;

                switch (rankNeeded)
                {
                    case Guild.eRank.Emblem:
                        {
                            return member.GuildRank.Emblem;
                        }
                    case Guild.eRank.AcHear:
                        {
                            return member.GuildRank.AcHear;
                        }
                    case Guild.eRank.AcSpeak:
                        {
                            return member.GuildRank.AcSpeak;
                        }
                    case Guild.eRank.Demote:
                        {
                            return member.GuildRank.Promote;
                        }
                    case Guild.eRank.Promote:
                        {
                            return member.GuildRank.Promote;
                        }
                    case Guild.eRank.GcHear:
                        {
                            return member.GuildRank.GcHear;
                        }
                    case Guild.eRank.GcSpeak:
                        {
                            return member.GuildRank.GcSpeak;
                        }
                    case Guild.eRank.Invite:
                        {
                            return member.GuildRank.Invite;
                        }
                    case Guild.eRank.OcHear:
                        {
                            return member.GuildRank.OcHear;
                        }
                    case Guild.eRank.OcSpeak:
                        {
                            return member.GuildRank.OcSpeak;
                        }
                    case Guild.eRank.Remove:
                        {
                            return member.GuildRank.Remove;
                        }
                    case Guild.eRank.Alli:
                        {
                            return member.GuildRank.Alli;
                        }
                    case Guild.eRank.View:
                        {
                            return member.GuildRank.View;
                        }
                    case Guild.eRank.Claim:
                        {
                            return member.GuildRank.Claim;
                        }
                    case Guild.eRank.Release:
                        {
                            return member.GuildRank.Release;
                        }
                    case Guild.eRank.Upgrade:
                        {
                            return member.GuildRank.Upgrade;
                        }
                    case Guild.eRank.Dues:
                        {
                            return member.GuildRank.Dues;
                        }
                    case Guild.eRank.Withdraw:
                        {
                            return member.GuildRank.Withdraw;
                        }
                    case Guild.eRank.Leader:
                        {
                            return (member.GuildRank.RankLevel == 0);
                        }
                    case Guild.eRank.Buff:
                        {
                            return member.GuildRank.Buff;
                        }
                    case Guild.eRank.BuyBanner:
                        {
                            return member.GuildRank.BuyBanner;
                        }
                    case Guild.eRank.Summon:
                        {
                            return member.GuildRank.Summon;
                        }
                    default:
                        {
                            if (log.IsWarnEnabled)
                                log.Warn("Required rank not in the DB: " + rankNeeded);

                            ChatUtil.SendDebugMessage(member, "Required rank not in the DB: " + rankNeeded);

                            return false;
                        }
                }
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error("GotAccess", e);
                return false;
            }
        }

        /// <summary>
        /// get rank by level
        /// </summary>
        /// <param name="index">the index of rank</param>
        /// <returns>the dbrank</returns>
        public DBRank GetRankByID(int index)
        {
            try
            {
                foreach (DBRank rank in this.Ranks)
                {
                    if (rank.RankLevel == index)
                        return rank;

                }
                return null;
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error("GetRankByID", e);
                return null;
            }
        }

        /// <summary>
        /// Returns a list of all online members.
        /// </summary>
        /// <returns>ArrayList of members</returns>
        public IList<GamePlayer> GetListOfOnlineMembers()
        {
            return new List<GamePlayer>(m_onlineGuildPlayers.Values);
        }

        /// <summary>
        /// Sends a message to all guild members 
        /// </summary>
        /// <param name="msg">message string</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public void SendMessageToGuildMembers(string msg, PacketHandler.eChatType type, PacketHandler.eChatLoc loc)
        {
            lock (m_onlineGuildPlayers)
            {
                foreach (GamePlayer pl in m_onlineGuildPlayers.Values)
                {
                    if (!HasRank(pl, Guild.eRank.GcHear))
                    {
                        continue;
                    }
                    pl.Out.SendMessage(msg, type, loc);
                }
            }
        }

        /// <summary>
        /// Sends a message that a player did an action to all guild members
        /// </summary>
        /// <param name="player">the player who did the action</param>
        /// <param name="playerMsg">message to player who did the action</param>
        /// <param name="guildMsg">message to others</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public void SendPlayerActionToGuildMembers(GamePlayer player, string playerMsg, string guildMsg, PacketHandler.eChatType type, PacketHandler.eChatLoc loc)
        {
            lock (m_onlineGuildPlayers)
            {
                foreach (GamePlayer pl in m_onlineGuildPlayers.Values)
                {
                    if (!HasRank(pl, Guild.eRank.GcHear))
                    {
                        continue;
                    }
                    if (pl == player)
                    {
                        if (!String.IsNullOrEmpty(playerMsg))
                        {
                            pl.Out.SendMessage(playerMsg, type, loc);
                        }
                    }
                    else
                    {
                        pl.Out.SendMessage(String.Format(guildMsg, pl.GetPersonalizedName(player)), type, loc);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a message that a player did an action to all guild members
        /// </summary>
        /// <param name="player">the player who did the action</param>
        /// <param name="key">message translation</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public void SendPlayerActionTranslationToGuildMembers(GamePlayer player, string key, PacketHandler.eChatType type, PacketHandler.eChatLoc loc, params object[] args)
        {
            lock (m_onlineGuildPlayers)
            {
                foreach (GamePlayer pl in m_onlineGuildPlayers.Values)
                {
                    if (!HasRank(pl, Guild.eRank.GcHear))
                    {
                        continue;
                    }
                    if (args.Length > 0)
                    {
                        var fullArgs = new object[] { pl.GetPersonalizedName(player) }.Concat(args).ToArray();
                        pl.Out.SendMessage(LanguageMgr.GetTranslation(pl.Client.Account.Language, key, fullArgs), type, loc);
                    }
                    else
                    {
                        pl.Out.SendMessage(LanguageMgr.GetTranslation(pl.Client.Account.Language, key, pl.GetPersonalizedName(player)), type, loc);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a message that a player did an action to all guild members
        /// </summary>
        /// <param name="player">the player who did the action</param>
        /// <param name="playerKey">message to player who did the action</param>
        /// <param name="guildKey">message to others</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public void SendPlayerActionTranslationToGuildMembers(GamePlayer player, string playerKey, string guildKey, PacketHandler.eChatType type, PacketHandler.eChatLoc loc, params object[] args)
        {
            lock (m_onlineGuildPlayers)
            {
                foreach (GamePlayer pl in m_onlineGuildPlayers.Values)
                {
                    if (!HasRank(pl, Guild.eRank.GcHear))
                    {
                        continue;
                    }
                    if (pl == player)
                    {
                        if (!String.IsNullOrEmpty(playerKey))
                        {
                            pl.Out.SendMessage(LanguageMgr.GetTranslation(pl.Client.Account.Language, playerKey, args), type, loc);
                        }
                    }
                    else
                    {
                        var fullArgs = new object[] { pl.GetPersonalizedName(player) }.Concat(args).ToArray();
                        pl.Out.SendMessage(LanguageMgr.GetTranslation(pl.Client.Account.Language, guildKey, fullArgs), type, loc);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a message to all guild members with key to translate
        /// <param name="msg">message string</param>
        /// <param name="type">message type</param>
        /// <param name="loc">message location</param>
        public void SendMessageToGuildMembersKey(string msg, eChatType type, eChatLoc loc, params object[] args)
        {
            lock (m_onlineGuildPlayers)
            {
                foreach (GamePlayer pl in m_onlineGuildPlayers.Values)
                {
                    if (!HasRank(pl, Guild.eRank.GcHear))
                    {
                        continue;
                    }
                    pl.Out.SendMessage(LanguageMgr.GetTranslation(pl.Client.Account.Language, msg, args), type, loc);
                }
            }
        }

        /// <summary>
        /// Called when this guild loose bounty points
        /// returns true if BPs were reduced and false if BPs are smaller than param amount
        /// if false is returned, no BPs were removed.
        /// </summary>
        public virtual bool RemoveBountyPoints(long amount)
        {
            if (amount > this.m_DBguild.BountyPoints)
            {
                return false;
            }
            this.m_DBguild.BountyPoints -= amount;
            this.SaveIntoDatabase();
            return true;
        }

        /// <summary>
        /// Gets or sets the guild merit points
        /// </summary>
        public long MeritPoints
        {
            get
            {
                return this.m_DBguild.MeritPoints;
            }
            set
            {
                this.m_DBguild.MeritPoints = value;
                this.SaveIntoDatabase();
            }
        }

        public long GuildLevel
        {
            get
            {
                if (GuildType != eGuildType.PlayerGuild)
                {
                    return 0;
                }
                else
                {
                    // added by Dunnerholl
                    // props to valmerwolf for formula
                    // checked with pendragon
                    return (long)(Math.Sqrt(m_DBguild.RealmPoints / Properties.GUILD_LEVELING_REALMPOINTS_RATIO) + 1);
                }
            }
        }

        /// <summary>
        /// Gets or sets the guild buff type
        /// </summary>
        public eBonusType BonusType
        {
            get
            {
                return (eBonusType)m_DBguild.BonusType;
            }
            set
            {
                this.m_DBguild.BonusType = (byte)value;
                this.SaveIntoDatabase();
            }
        }

        /// <summary>
        /// Gets or sets the guild buff time
        /// </summary>
        public DateTime BonusStartTime
        {
            get => this.m_DBguild.BonusStartTime;
            set
            {
                this.m_DBguild.BonusStartTime = value;
                this.SaveIntoDatabase();
            }
        }

        public string Email
        {
            get
            {
                return this.m_DBguild.Email;
            }
            set
            {
                this.m_DBguild.Email = value;
                this.SaveIntoDatabase();
            }
        }

        public Dictionary<GamePlayer, DateTime> Leave_Players { get => m_leave_Players; set => m_leave_Players = value; }
        public Dictionary<GamePlayer, DateTime> Invite_Players { get => m_invite_Players; set => m_invite_Players = value; }

        /// <summary>
        /// Called when this guild gains merit points
        /// </summary>
        /// <param name="amount">The amount of bounty points gained</param>
        public virtual void GainMeritPoints(long amount)
        {
            MeritPoints += amount;
            UpdateGuildWindow();
        }

        /// <summary>
        /// Called when this guild loose bounty points
        /// </summary>
        /// <param name="amount">The amount of bounty points gained</param>
        public virtual void RemoveMeritPoints(long amount)
        {
            if (amount > MeritPoints)
                amount = MeritPoints;
            MeritPoints -= amount;
            UpdateGuildWindow();
        }

        public bool AddToDatabase()
        {
            return GameServer.Database.AddObject(this.m_DBguild);
        }
        /// <summary>
        /// Saves this guild to database
        /// </summary>
        public bool SaveIntoDatabase()
        {
            return GameServer.Database.SaveObject(m_DBguild);
        }

        private string bannerStatus;
        public string GuildBannerStatus(GamePlayer player)
        {
            bannerStatus = "None";

            if (player.Guild != null)
            {
                if (player.Guild.HasGuildBanner)
                {
                    foreach (GamePlayer plr in player.Guild.GetListOfOnlineMembers())
                    {
                        if (plr.GuildBanner != null)
                        {
                            if (plr.GuildBanner.BannerItem.Status == GuildBannerItem.eStatus.Active)
                            {
                                bannerStatus = "Summoned";
                            }
                            else
                            {
                                bannerStatus = "Dropped";
                            }
                        }
                    }
                    if (bannerStatus == "None")
                    {
                        bannerStatus = "Not Summoned";
                    }
                    return bannerStatus;
                }
            }
            return bannerStatus;
        }

        public void UpdateMember(GamePlayer player)
        {
            if (player.Guild != this)
                return;
            int housenum;
            if (player.Guild.GuildOwnsHouse)
            {
                housenum = player.Guild.GuildHouseNumber;
            }
            else
                housenum = 0;

            string mes = "I";
            mes += ',' + player.Guild.GuildLevel.ToString(); // Guild Level
            mes += ',' + player.Guild.GetGuildBank().ToString(); // Guild Bank money
            mes += ',' + player.Guild.GetGuildDuesPercent().ToString(); // Guild Dues enable/disable
            mes += ',' + player.Guild.BountyPoints.ToString(); // Guild Bounty
            mes += ',' + player.Guild.RealmPoints.ToString(); // Guild Experience
            mes += ',' + player.Guild.MeritPoints.ToString(); // Guild Merit Points
            mes += ',' + housenum.ToString(); // Guild houseLot ?
            mes += ',' + (player.Guild.MemberOnlineCount + 1).ToString(); // online Guild member ?
            mes += ',' + player.Guild.GuildBannerStatus(player); //"Banner available for purchase", "Missing banner buying permissions"
            mes += ",\"" + player.Guild.Motd + '\"'; // Guild Motd
            mes += ",\"" + player.Guild.Omotd + '\"'; // Guild oMotd
            player.Out.SendMessage(mes, eChatType.CT_SocialInterface, eChatLoc.CL_SystemWindow);
            player.Guild.SaveIntoDatabase();
        }

        public void UpdateGuildWindow()
        {
            lock (m_onlineGuildPlayers)
            {
                foreach (GamePlayer player in m_onlineGuildPlayers.Values)
                {
                    player.Guild.UpdateMember(player);
                }
            }
        }
    }
}
