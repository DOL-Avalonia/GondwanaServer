using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using DOL.Database;
using DOL.GS.Housing;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOL.GS.PacketHandler;
using log4net;
using static DOL.GS.RolloverSchedulerService;

namespace DOL.GS
{
    public static class HouseRentService
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private static readonly Queue<House> _removalQueue = new();
        private static readonly object _removalLock = new();
        private static ECSGameTimer _testModeTimer;

        public static void Initialize()
        {
            RolloverSchedulerService.Instance.Subscribe(IntervalKey.Hourly, CheckRents);

            if (Properties.RENT_DUE_DAYS < 0)
            {
                if (log.IsInfoEnabled)
                    log.Info("[HouseRentService] RENT_DUE_DAYS is negative. Testing mode: checking rents every minute.");
                _testModeTimer = new ECSGameTimer(null, static t => { CheckRents(); return 60_000; }, 60_000);
            }
        }

        private static void CheckRents()
        {
            if (Properties.RENT_DUE_DAYS == 0)
                return;

            // NOTE: Gondwana's rent logic includes AutoPayRent from DBBanque (guild + personal),
            // eviction messages, and guild bank withdrawal. This is a direct port of your
            // HouseMgr.CheckRents body, minus the Timer plumbing.
            try
            {
                foreach (var regs in HouseMgr.AllHousesByRegion())
                {
                    foreach (var entry in regs.Value)
                    {
                        House house = entry.Value;
                        ProcessHouseRent(house);
                    }
                }

                while (true)
                {
                    House house;
                    lock (_removalLock)
                    {
                        if (!_removalQueue.TryDequeue(out house))
                            break;
                    }
                    NotifyEviction(house);
                    HouseMgr.RemoveHouse(house);
                }
            }
            catch (Exception ex)
            {
                log.Error("[HouseRentService] Unhandled exception during CheckRents!", ex);
            }
        }

        private static void ProcessHouseRent(House house)
        {
            if (string.IsNullOrEmpty(house.OwnerID) || house.NoPurge)
                return;

            var now = DateTime.Now;
            var diff = now - house.LastPaid;
            long rent = HouseMgr.GetRentByModel(house.Model);

            bool isRentDue = Properties.RENT_DUE_DAYS > 0
                ? diff.Days >= Properties.RENT_DUE_DAYS
                : diff.TotalMinutes >= 1; // testing mode

            if (rent <= 0 || !isRentDue)
                return;

            // ---- Gondwana AutoPayRent (guild) ----
            if (house.DatabaseItem.GuildHouse)
            {
                Guild guild = GuildMgr.GetGuildByGuildID(house.OwnerID);
                if (guild != null)
                {
                    DBBanque bank = GameServer.Database.FindObjectByKey<DBBanque>(house.OwnerID);
                    if (bank != null && bank.AutoPayRent && guild.GetGuildBank() >= rent)
                    {
                        guild.WithdrawGuildBank(null, rent, false);
                        house.LastPaid = now;
                        house.SaveIntoDatabase();
                        NotifyGuildAutoPaid(guild, rent);
                        return;
                    }
                }
            }
            else
            {
                // ---- Gondwana AutoPayRent (personal) ----
                DBBanque bank = GameServer.Database.FindObjectByKey<DBBanque>(house.OwnerID);
                if (bank != null && bank.AutoPayRent && bank.Money >= rent)
                {
                    bank.Money -= rent;
                    GameServer.Database.SaveObject(bank);
                    house.LastPaid = now;
                    house.SaveIntoDatabase();
                    NotifyPersonalAutoPaid(house.OwnerID, rent);
                    return;
                }
            }

            // ---- Lockbox / consignment fallback ----
            long lockboxAmount = house.KeptMoney;
            var consignment = house.ConsignmentMerchant;
            long consignmentAmount = consignment?.TotalMoney ?? 0;

            if (lockboxAmount >= rent)
            {
                house.KeptMoney -= rent;
                house.LastPaid = now;
                house.SaveIntoDatabase();
                return;
            }

            long remaining = rent - lockboxAmount;
            if (remaining <= consignmentAmount)
            {
                house.KeptMoney = 0;
                consignment.TotalMoney -= remaining;
                house.LastPaid = now;
                house.SaveIntoDatabase();
                return;
            }

            lock (_removalLock)
                _removalQueue.Enqueue(house);
        }

        private static void NotifyGuildAutoPaid(Guild guild, long rent)
        {
            foreach (GamePlayer p in guild.GetListOfOnlineMembers())
            {
                if (p.GuildRank != null && p.GuildRank.RankLevel <= 3)
                {
                    string rentFormatted = Finance.Currency.Copper.Mint(rent).ToText(p.Client.Account.Language);
                    p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client.Account.Language,
                        "Scripts.Player.Housing.GuildRentPaidFromBank", rentFormatted),
                        eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                }
            }
        }

        private static void NotifyPersonalAutoPaid(string ownerId, long rent)
        {
            GameClient client = WorldMgr.GetClientByPlayerID(ownerId, true, false);
            if (client?.Player == null)
                return;

            string rentFormatted = Finance.Currency.Copper.Mint(rent).ToText(client.Account.Language);
            client.Player.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language,
                "Scripts.Player.Housing.RentPaidFromBank", rentFormatted),
                eChatType.CT_Important, eChatLoc.CL_SystemWindow);
        }

        private static void NotifyEviction(House h)
        {
            if (h.DatabaseItem.GuildHouse)
            {
                Guild guild = GuildMgr.GetGuildByGuildID(h.OwnerID);
                if (guild != null)
                {
                    foreach (GamePlayer p in guild.GetListOfOnlineMembers())
                        p.Out.SendMessage(LanguageMgr.GetTranslation(p.Client.Account.Language,
                            "Scripts.Player.Housing.GuildHouseEvicted", h.HouseNumber),
                            eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
            else
            {
                GameClient client = WorldMgr.GetClientByPlayerID(h.OwnerID, true, false);
                client?.Player?.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language,
                    "Scripts.Player.Housing.PersonalHouseEvicted", h.HouseNumber),
                    eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }
    }
}