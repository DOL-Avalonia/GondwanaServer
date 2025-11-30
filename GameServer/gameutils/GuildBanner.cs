using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using log4net;
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace DOL.GS
{
    public class BannerVisual
    {
        /// <summary>
        /// The player that created the banner, used for the emblem.
        /// May be different from the player CARRYING the banner.
        /// </summary>
        public GamePlayer OwningPlayer
        {
            get;
            set;
        }

        /// <summary>
        /// Item associated with this banner, if any
        /// </summary>
        public GameInventoryItem? Item
        {
            get;
            set;
        }

        protected GamePlayer m_carryingPlayer;

        /// <summary>
        /// Player currently carrying the banner, showing the visual 
        /// </summary>
        public virtual GamePlayer CarryingPlayer
        {
            get => m_carryingPlayer;
            set
            {
                if (value == m_carryingPlayer)
                    return;

                var prev = m_carryingPlayer;
                m_carryingPlayer = value;
                if (prev != null)
                {
                    prev.ActiveBanner = null;
                }
                if (value != null)
                {
                    value.ActiveBanner = this;
                }
            }
        }

        protected int? m_emblem;
        
        /// <summary>
        /// Current emblem for this visual
        /// Get defaults to Item ?? Guild ?? 0, never null.
        /// Set overrides this default, can be set to null to reset to default.
        /// </summary>
        [NotNull] public virtual int? Emblem
        {
            get
            {
                if (m_emblem != null)
                    return m_emblem;

                if (Item?.Emblem is not 0)
                    return Item?.Emblem;

                return OwningPlayer?.Guild?.Emblem;
            }
            set => m_emblem = value;
        }

        public virtual void Drop()
        {
            var prev = CarryingPlayer;
            CarryingPlayer = null;
            if (prev != null)
                Item?.Drop(prev);
        }

        public virtual void PutAway()
        {
            CarryingPlayer = null;
        }

        public virtual void TransferTo(GameObject who, bool forced)
        {
            throw new NotImplementedException();
        }
    }
    
    public class GuildBanner : BannerVisual
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        /// <summary>
        /// Tick timer
        /// </summary>
        private RegionTimer m_timer;

        /// <summary>
        /// Banner expiration timer
        /// </summary>
        private RegionTimer m_expireTimer;

        private GuildBannerItem m_item;
        
        public GuildBanner(GamePlayer player)
        {
            OwningPlayer = player;
            GuildBannerItem item = new GuildBannerItem(GuildBannerTemplate);

            item.Banner = this;
            BannerItem = item;
            if (Properties.GUILD_BANNER_DURATION > 0)
            {
                m_expireTimer = new RegionTimer(player, new RegionTimerCallback(BannerExpireCallback), Properties.GUILD_BANNER_DURATION * 1000);
            }
        }

        /// <summary>
        /// Player who summoned or recovered the banner
        /// </summary>
        public override GamePlayer CarryingPlayer
        {
            get => base.CarryingPlayer;
            set
            {
                if (value != null)
                {
                    if (value == CarryingPlayer)
                        return;

                    if (!CanStart(value))
                        return;

                    Start(value);
                }
                else
                {
                    DoStop();
                }
            }
        }

        public Guild Guild => OwningPlayer.Guild;

        public GuildBannerItem BannerItem
        {
            get => m_item;
            private set
            {
                m_item = value;
                m_item.Emblem = Guild?.Emblem ?? 0;
            }
        }

        private void Start(GamePlayer carrier)
        {
            base.CarryingPlayer = carrier;
            if (Guild != null)
                Guild.ActiveGuildBanner = this;
            AddHandlers();
                    
            m_timer?.Stop();
            m_timer = new RegionTimer(CarryingPlayer, new RegionTimerCallback(TimerTick));
            m_timer.Start(1);
        }

        protected bool CanStart(GamePlayer player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.Group != null)
            {
                foreach (GamePlayer groupPlayer in OwningPlayer.Group.GetPlayersInTheGroup())
                {
                    if (groupPlayer.ActiveBanner != null)
                    {
                        player.SendTranslatedMessage("GameUtils.Guild.Banner.BannerInGroup", eChatType.CT_Loot, eChatLoc.CL_SystemWindow);
                        return false;
                    }
                }
            }
            else if (!Properties.GUILD_BANNER_ALLOW_SOLO && OwningPlayer.Client.Account.PrivLevel <= (int)ePrivLevel.Player)
            {
                player.SendTranslatedMessage("GameUtils.Guild.Banner.BannerNoGroup", eChatType.CT_Loot, eChatLoc.CL_SystemWindow);
                return false;
            }
            
            return true;
        }

        protected void DoStop()
        {
            if (m_timer != null)
            {
                m_timer.Stop();
                m_timer = null;
            }
            
            if (CarryingPlayer != null)
            {
                RemoveHandlers();
                base.CarryingPlayer = null;
            }

            if (Guild != null)
            {
                Guild.ActiveGuildBanner = null;
            }
        }

        public void Stop()
        {
            CarryingPlayer = null;
        }

        private void ApplyBannerEffect(GamePlayer player)
        {
            GuildBannerEffect effect = GuildBannerEffect.CreateEffectOfClass(CarryingPlayer, player);

            if (effect != null)
            {
                IGameEffect oldEffect = player.EffectList.GetOfType(effect.GetType());
                (oldEffect as GuildBannerEffect)?.Stop();
                effect.Start(player);
            }
        }

        private int TimerTick(RegionTimer timer)
        {
            if (CarryingPlayer != null)
            {
                if (CarryingPlayer.Group == null)
                {
                    if (!Properties.GUILD_BANNER_ALLOW_SOLO  && CarryingPlayer.Client.Account.PrivLevel <= 1)
                    {
                        CarryingPlayer = null;
                        return 9000;
                    }
                }
                else
                {
                    var groupMembers = CarryingPlayer.Group.GetPlayersInTheGroup()
                        .Where(p => p.Guild != Guild
                                   && p is { ObjectState: GameObject.eObjectState.Active, IsAlive: true }
                                   && p.GetDistanceSquaredTo(CarryingPlayer) < 1500 * 1500);
                    foreach (GamePlayer player in groupMembers)
                    {
                        ApplyBannerEffect(player);
                    }
                }
                foreach (GamePlayer player in Guild.GetListOfOnlineMembers().Where(p => p is { ObjectState: GameObject.eObjectState.Active, IsAlive: true } && p.GetDistanceSquaredTo(CarryingPlayer) < 1500 * 1500))
                {
                    ApplyBannerEffect(player);
                }
            }
            return 9000; // Pulsing every 9 seconds with a duration of 9 seconds - Tolakram
        }

        protected virtual void AddHandlers()
        {
            GameEventMgr.AddHandler(CarryingPlayer, GamePlayerEvent.LeaveGroup, new DOLEventHandler(PlayerLeaveGroup));
            GameEventMgr.AddHandler(CarryingPlayer, GroupEvent.MemberJoined, new DOLEventHandler(PlayerJoinGroup));
            GameEventMgr.AddHandler(CarryingPlayer, GamePlayerEvent.Quit, new DOLEventHandler(PlayerPutAwayBanner));
            GameEventMgr.AddHandler(CarryingPlayer, GamePlayerEvent.StealthStateChanged, new DOLEventHandler(PlayerPutAwayBanner));
            GameEventMgr.AddHandler(CarryingPlayer, GamePlayerEvent.Linkdeath, new DOLEventHandler(PlayerPutAwayBanner));
            GameEventMgr.AddHandler(CarryingPlayer, GamePlayerEvent.RegionChanging, new DOLEventHandler(PlayerPutAwayBanner));
            GameEventMgr.AddHandler(CarryingPlayer, GamePlayerEvent.Dying, new DOLEventHandler(PlayerDied));
        }

        protected virtual void RemoveHandlers()
        {
            GameEventMgr.RemoveHandler(CarryingPlayer, GamePlayerEvent.LeaveGroup, new DOLEventHandler(PlayerLeaveGroup));
            GameEventMgr.RemoveHandler(CarryingPlayer, GroupEvent.MemberJoined, new DOLEventHandler(PlayerJoinGroup));
            GameEventMgr.RemoveHandler(CarryingPlayer, GamePlayerEvent.Quit, new DOLEventHandler(PlayerPutAwayBanner));
            GameEventMgr.RemoveHandler(CarryingPlayer, GamePlayerEvent.StealthStateChanged, new DOLEventHandler(PlayerPutAwayBanner));
            GameEventMgr.RemoveHandler(CarryingPlayer, GamePlayerEvent.Linkdeath, new DOLEventHandler(PlayerPutAwayBanner));
            GameEventMgr.RemoveHandler(CarryingPlayer, GamePlayerEvent.RegionChanging, new DOLEventHandler(PlayerPutAwayBanner));
            GameEventMgr.RemoveHandler(CarryingPlayer, GamePlayerEvent.Dying, new DOLEventHandler(PlayerDied));
        }

        protected void PlayerPutAwayBanner(DOLEvent e, object sender, EventArgs args)
        {
            PutAway();
        }

        protected int LeaveGroupCallback(RegionTimer timer)
        {
            timer.Stop();
            m_expireTimer = null;
            if (!Properties.GUILD_BANNER_ALLOW_SOLO)
            {
                CarryingPlayer?.SendTranslatedMessage("GameUtils.Guild.Banner.BannerNoGroup", eChatType.CT_Loot, eChatLoc.CL_SystemWindow);
                PutAway();
            }
            return 0;
        }

        protected int BannerExpireCallback(RegionTimer timer)
        {
            timer.Stop();
            m_expireTimer = null;
            PutAway();
            return 0;
        }

        public override void PutAway()
        {
            if (CarryingPlayer != null)
            {
                Guild.SendPlayerActionTranslationToGuildMembers(CarryingPlayer, "GameUtils.Guild.Banner.PutAway", eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                CarryingPlayer.Group?.SendPlayerActionTranslationToGroupMembers(CarryingPlayer, "GameUtils.Guild.Banner.PutAway.OtherGuild", eChatType.CT_Group, eChatLoc.CL_SystemWindow, Guild.Name);
            }
            base.PutAway();
        }

        protected void PlayerLeaveGroup(DOLEvent e, object sender, EventArgs args)
        {
            if (!Properties.GUILD_BANNER_ALLOW_SOLO)
            {
                CarryingPlayer.SendTranslatedMessage("GameUtils.Guild.Banner.LeavesGroup", eChatType.CT_Group, eChatLoc.CL_SystemWindow, Guild.Name);
                (args as LeaveGroupEventArgs)?.Group.SendTranslatedMessageToGroupMembers("GameUtils.Guild.Banner.LeavesGroup", eChatType.CT_Group, eChatLoc.CL_SystemWindow, Guild.Name);
                m_expireTimer?.Stop();
                m_expireTimer = new RegionTimer(CarryingPlayer, LeaveGroupCallback, 30000);
            }
            else
            {
                (args as LeaveGroupEventArgs)?.Group.SendTranslatedMessageToGroupMembers("GameUtils.Guild.Banner.LeavesGroup", eChatType.CT_Group, eChatLoc.CL_SystemWindow, Guild.Name);
            }
        }

        protected void PlayerJoinGroup(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GamePlayer { Group: not null } player)
            {
                return;
            }

            if (player.Group.Leader == player)
            {
                foreach (GamePlayer groupPlayer in OwningPlayer.Group.GetPlayersInTheGroup())
                {
                    if (groupPlayer.ActiveBanner is GuildBanner gBanner)
                    {
                        groupPlayer.SendTranslatedMessage("GameUtils.Guild.Banner.BannerInGroup", eChatType.CT_Loot, eChatLoc.CL_SystemWindow);
                        gBanner.PutAway();
                    }
                }
            }
            else
            {
                foreach (GamePlayer groupPlayer in OwningPlayer.Group.GetPlayersInTheGroup())
                {
                    if (groupPlayer.ActiveBanner != null)
                    {
                        player.SendTranslatedMessage("GameUtils.Guild.Banner.BannerInGroup", eChatType.CT_Loot, eChatLoc.CL_SystemWindow);
                        PutAway();
                        break;
                    }
                }
            }
        }

        /// <inheritdoc />
        public override void TransferTo(GameObject who, bool forced)
        {
            if (forced)
                m_item.OnPlayerKilled(who);
            else
                base.TransferTo(who, forced);
        }

        protected void PlayerDied(DOLEvent e, object sender, EventArgs args)
        {
            DyingEventArgs arg = args as DyingEventArgs;
            if (arg == null) return;

            TransferTo(arg.Killer, true);
        }

        protected ItemTemplate m_guildBannerTemplate;
        public ItemTemplate GuildBannerTemplate
        {
            get
            {
                if (m_guildBannerTemplate == null)
                {
                    string guildIDNB = "GuildBanner_" + Guild.GuildID;

                    m_guildBannerTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(guildIDNB) ?? new ItemTemplate();
                    m_guildBannerTemplate.CanDropAsLoot = false;
                    m_guildBannerTemplate.Id_nb = guildIDNB;
                    m_guildBannerTemplate.IsDropable = false;
                    m_guildBannerTemplate.IsPickable = true;
                    m_guildBannerTemplate.IsTradable = false;
                    m_guildBannerTemplate.IsIndestructible = true;
                    m_guildBannerTemplate.Item_Type = 41;
                    m_guildBannerTemplate.Level = 1;
                    m_guildBannerTemplate.MaxCharges = 1;
                    m_guildBannerTemplate.MaxCount = 1;
                    m_guildBannerTemplate.Emblem = Guild.Emblem;
                    switch (OwningPlayer.Realm)
                    {
                        case eRealm.Albion:
                            m_guildBannerTemplate.Model = 3223;
                            break;
                        case eRealm.Midgard:
                            m_guildBannerTemplate.Model = 3224;
                            break;
                        case eRealm.Hibernia:
                            m_guildBannerTemplate.Model = 3225;
                            break;
                    }
                    m_guildBannerTemplate.Name = Guild.Name + "'s Banner";
                    m_guildBannerTemplate.Object_Type = (int)eObjectType.HouseWallObject;
                    m_guildBannerTemplate.Realm = 0;
                    m_guildBannerTemplate.Quality = 100;
                    m_guildBannerTemplate.ClassType = "DOL.GS.GuildBannerItem";
                    m_guildBannerTemplate.PackageID = "GuildBanner";
                }

                return m_guildBannerTemplate;
            }
        }

    }
}



