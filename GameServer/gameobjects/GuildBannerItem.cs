using DOL.AI.Brain;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using DOL.Events;
using DOL.Language;
using DOL.GS.PacketHandler;
using DOL.Database;
using DOL.GS.Effects;
using DOL.GS.Geometry;
using DOL.GS.Spells;
using log4net;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// This class represents an inventory item
    /// </summary>
    public class GuildBannerItem : GameInventoryItem
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        public enum eStatus : byte
        {
            Active = 1,
            Dropped = 2,
            Recovered = 3
        }

        public GuildBannerItem()
            : base()
        {
        }

        public GuildBannerItem(ItemTemplate template)
            : base(template)
        {
        }

        public GuildBannerItem(InventoryItem item)
            : base(item)
        {
            OwnerID = item.OwnerID;
            ObjectId = item.ObjectId;
        }

        public GuildBanner Banner { get; set; }

        public List<GamePlayer> StealingPlayers { get; set; } = new List<GamePlayer>();

        public WorldInventoryItem WorldItem { get; private set; }

        public ECSGameTimer GroundTimer { get; private set; }

        public eStatus Status { get; set; }


        /// <summary>
        /// Player receives this item (added to players inventory)
        /// </summary>
        /// <param name="player"></param>
        public override void OnReceive(GamePlayer player)
        {
            /* VANILLA:
            // for guild banners we don't actually add it to inventory but instead register
            // if it is rescued by a friendly player or taken by the enemy

            player.Inventory.RemoveItem(this);

            int trophyModel = 0;
            eRealm realm = eRealm.None;

            switch (Model)
            {
                case 3223:
                    trophyModel = 3359;
                    realm = eRealm.Albion;
                    break;
                case 3224:
                    trophyModel = 3361;
                    realm = eRealm.Midgard;
                    break;
                case 3225:
                    trophyModel = 3360;
                    realm = eRealm.Hibernia;
                    break;
            }

            // if picked up by an enemy then turn this into a trophy
            if (realm != player.Realm)
            {
                ItemUnique template = new ItemUnique(Template);
                template.ClassType = "";
                template.Model = trophyModel;
                template.IsDropable = true;
                template.IsIndestructible = false;

                GameServer.Database.AddObject(template);
                GameInventoryItem trophy = new GameInventoryItem(template);
                player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, trophy);
                OwnerGuild.SendMessageToGuildMembers(player.Name + " of " + GlobalConstants.RealmToName(player.Realm) + " has captured your guild banner!", eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                OwnerGuild.GuildBannerLostTime = DateTime.Now;
            }
            else
            {
                Status = eStatus.Recovered;

                // A friendly player has picked up the banner.
                if (OwnerGuild != null)
                {
                    OwnerGuild.SendMessageToGuildMembers(player.Name + " has recovered your guild banner!", eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                }

                if (SummonPlayer != null)
                {
                    SummonPlayer.GuildBanner = null;
                }
            }
            */

            // GONDWANA:
            player.Inventory.RemoveItem(this); // Remove the item because the actual banner item is never in an inventory (only the trophy)
            if (Banner == null)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("Guild banner item {0} received by {1} has no guild banner object", Id_nb, player.Name);
                }
                return;
            }

            if (Banner.Guild == null)
            {
                if (log.IsWarnEnabled)
                {
                    log.WarnFormat("Guild banner item {0} received by {1} has no guild", Id_nb, player.Name);
                }
                return;
            }

            if (Status != eStatus.Dropped)
            {
                // Player is summoning the banner
                return;
            }

            if (player.Group != null && player.Group == Banner.OwningPlayer?.Group) // Same group as owner, different or same guild, transfer to player
            {
                Status = eStatus.Recovered;
                Banner.Guild.SendPlayerActionTranslationToGuildMembers(player, "GameUtils.Guild.Banner.PickedUp", eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                player.Group.SendPlayerActionTranslationToGroupMembers(player, "GameUtils.Guild.Banner.PickedUp.OtherGuild", eChatType.CT_Group, eChatLoc.CL_SystemWindow, Banner.Guild.Name);
                Banner.OwningPlayer = player;
                StealingPlayers.Clear();
                return;
            }

            Banner.Guild.ActiveGuildBanner = null;
            if (player.Guild == Banner.Guild) // Same guild as owner, different group
            {
                Status = eStatus.Recovered;
                Banner.Guild.SendPlayerActionTranslationToGuildMembers(player, "GameUtils.Guild.Banner.Recovered", eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
            }
            else if (StealingPlayers.Contains(player)) // Picked up by killers, turn into a trophy
            {
                OnCapture(player);
            }
            else if (GameServer.ServerRules.IsAllowedToAttack(player, Banner.OwningPlayer, true)) // Enemy
            {
                OnCapture(player);
            }
            else // Otherwise friendly player not in guild or group
            {
                Status = eStatus.Recovered;
                Banner.Guild.SendPlayerActionTranslationToGuildMembers(player, "GameUtils.Guild.Banner.Recovered", eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
            }
            StealingPlayers.Clear();
        }

        protected void OnCapture(GamePlayer player)
        {
            int trophyModel = Model switch
            {
                3223 => 3359, // Albion
                3224 => 3361, // Midgard
                3225 => 3360, // Hibernia
                _ => 0
            };
            ItemUnique template = new ItemUnique(Template);
            template.ClassType = "";
            template.Model = trophyModel;
            template.IsDropable = true;
            template.IsIndestructible = false;

            GameServer.Database.AddObject(template);
            GameInventoryItem trophy = new GameInventoryItem(template);
            player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, trophy);
            if (player.Guild != null)
            {
                Banner.Guild.SendPlayerActionTranslationToGuildMembers(player, "GameUtils.Guild.Banner.Captured.InGuild", eChatType.CT_Guild, eChatLoc.CL_SystemWindow, player.Guild.Name);
                Banner.OwningPlayer.Group?.SendPlayerActionTranslationToGroupMembers(player, "GameUtils.Guild.Banner.Captured.OtherGuild.InGuild", eChatType.CT_Group, eChatLoc.CL_SystemWindow, player.Guild.Name, Banner.Guild.Name);
                player.Guild.SendPlayerActionTranslationToGuildMembers(player, "GameUtils.Guild.Banner.Captured.OtherGuild", eChatType.CT_Guild, eChatLoc.CL_SystemWindow, Banner.Guild.Name);
            }
            else
            {
                Banner.Guild.SendPlayerActionTranslationToGuildMembers(player, "GameUtils.Guild.Banner.Captured", eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                Banner.OwningPlayer.Group?.SendPlayerActionTranslationToGroupMembers(player, "GameUtils.Guild.Banner.Captured.OtherGuild", eChatType.CT_Group, eChatLoc.CL_SystemWindow, Banner.Guild.Name);
                player.SendTranslatedMessage("GameUtils.Guild.Banner.Captured.You", eChatType.CT_Loot, eChatLoc.CL_SystemWindow, Banner.Guild.Name);
            }
            Banner.Guild.GuildBannerLostTime = DateTime.Now;
        }

        public WorldInventoryItem CreateWorldItem()
        {
            WorldInventoryItem item = new WorldInventoryItem(this);
            GamePlayer player = Banner.OwningPlayer;

            item.Position = player.Position + Vector.Create(player.Orientation, length: 30);
            item.Heading = player.Heading;
            item.CurrentRegionID = player.CurrentRegionID;
            return item;
        }

        /// <summary>
        /// Player was killed and dropped the banner
        /// </summary>
        /// <param name="killer"></param>
        public void OnPlayerKilled(GameObject killer)
        {
            GamePlayer killed = Banner.OwningPlayer;
            WorldItem = CreateWorldItem();
            GameObject realKiller = killer;
            if (killer is GameNPC { Brain: IControlledBrain { Owner: GamePlayer } } pet)
            {
                realKiller = ((IControlledBrain)pet.Brain).Owner;
            }
            if (realKiller is GameLiving { Group: not null } livingKiller) // Player or NPC in a group
            {
                StealingPlayers = new List<GamePlayer>(livingKiller.Group.GetPlayersInTheGroup());
            }
            else if (realKiller is GamePlayer playerKiller)// Killer not in a group
            {
                StealingPlayers = new List<GamePlayer>
                {
                    playerKiller
                };
            }

            if (StealingPlayers.Any()) // Only restrict pickups if killed by player
            {
                if (Banner.OwningPlayer.Group != null)
                {
                    foreach (GamePlayer owningPlayer in Banner.OwningPlayer.Group.GetPlayersInTheGroup())
                    {
                        WorldItem.AddOwner(owningPlayer);
                    }
                }
                else
                {
                    WorldItem.AddOwner(Banner.OwningPlayer);
                }

                foreach (GamePlayer stealingPlayer in StealingPlayers)
                {
                    WorldItem.AddOwner(stealingPlayer);
                }
            }

            //WorldItem.StartPickupTimer(10);
            GroundTimer = new ECSGameTimer(WorldItem, GroundTimerCallback, 30000);

            Banner.OwningPlayer.Guild?.SendPlayerActionTranslationToGuildMembers(killed, "GameUtils.Guild.Banner.Dropped", eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
            killed.Group?.SendPlayerActionTranslationToGroupMembers(killed, "GameUtils.Guild.Banner.Dropped.OtherGuild", eChatType.CT_Group, eChatLoc.CL_SystemWindow, Banner.Guild.Name);
            Banner.Stop();
            Status = eStatus.Dropped;
            WorldItem.AddToWorld();
        }

        protected int GroundTimerCallback(ECSGameTimer timer)
        {
            if (WorldItem != null)
            {
                WorldItem.RemoveFromWorld();
                WorldItem.Delete();
                WorldItem = null;
            }
            return 0;
        }

        /// <summary>
        /// Drop this item on the ground
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public override WorldInventoryItem Drop(GamePlayer player)
        {
            return null;
        }


        public override void OnRemoveFromWorld()
        {
            if (GroundTimer is { IsAlive: true })
            {
                GroundTimer.Stop();
                GroundTimer = null;
            }

            if (Banner is { Guild: not null })
            {
                if (Status == eStatus.Dropped)
                {
                    Banner.Guild.ActiveGuildBanner = null;
                    // banner was dropped and not picked up, must be re-purchased
                    Banner.Guild.HasGuildBanner = false;
                    Banner.Guild.SendMessageToGuildMembersKey("GameUtils.Guild.Banner.Lost", eChatType.CT_Guild, eChatLoc.CL_SystemWindow);
                }
            }

            base.OnRemoveFromWorld();
        }


        /// <summary>
        /// Is this a valid item for this player?
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public override bool CheckValid(GamePlayer player)
        {
            return false;
        }
    }
}
