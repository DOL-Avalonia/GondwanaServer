using System.Collections.Generic;
using System.Numerics;
using DOL.Database;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS
{
    public class GuildVaultKeeper : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            if (player.Guild == null || player.GuildRank == null)
            {
                player.SendTranslatedMessage("GameUtils.GuildVault.Keeper.NoGuild", eChatType.CT_Say, eChatLoc.CL_PopupWindow, player.Name);
                return true;
            }

            if (player.Guild.IsSystemGuild)
            {
                player.SendTranslatedMessage("GameUtils.GuildVault.Keeper.BadGuild", eChatType.CT_Say, eChatLoc.CL_PopupWindow, player.Name);
                return true;
            }

            string message;
            ItemTemplate vaultItem;
            GuildVault vault;

            if (player.GuildRank.CanViewVault(0))
            {
                vaultItem = GetDummyVaultItem(player);
                vault = new GuildVault(player, 0, vaultItem);
                if (player.GuildRank.CanViewVault(1))
                {
                    if (player.GuildRank.CanViewVault(2))
                    {
                        message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.GuildVault.Keeper.Access123", player.Guild.Name);
                    }
                    else
                    {
                        message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.GuildVault.Keeper.Access12", player.Guild.Name);
                    }
                }
                else
                {
                    if (player.GuildRank.CanViewVault(2))
                    {
                        message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.GuildVault.Keeper.Access13", player.Guild.Name);
                    }
                    else
                    {
                        message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.GuildVault.Keeper.Access1", player.Guild.Name);
                    }
                }
            }
            else if (player.GuildRank.CanViewVault(1))
            {
                vaultItem = GetDummyVaultItem(player);
                vault = new GuildVault(player, 1, vaultItem);
                if (player.GuildRank.CanViewVault(2))
                {
                    message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.GuildVault.Keeper.Access23", player.Guild.Name);
                }
                else
                {
                    message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.GuildVault.Keeper.Access2", player.Guild.Name);
                }
            }
            else if (player.GuildRank.CanViewVault(2))
            {
                vaultItem = GetDummyVaultItem(player);
                vault = new GuildVault(player, 2, vaultItem);
                message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.GuildVault.Keeper.Access3", player.Guild.Name);
            }
            else
            {
                player.SendTranslatedMessage("GameUtils.GuildVault.Keeper.NoPermissions", eChatType.CT_Say, eChatLoc.CL_PopupWindow, player.Name);
                return true;
            }
            message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.GuildVault.Keeper.Greetings", player.Name) + "\n\n" + message;
            player.Out.SendMessage(message, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            player.ActiveInventoryObject = vault;
            player.Out.SendInventoryItemsUpdate(vault.GetClientInventory(player), eInventoryWindowType.HouseVault);
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text))
                return false;

            GamePlayer player = source as GamePlayer;

            if (player?.Guild == null)
                return false;

            int index;

            switch (text)
            {
                case "first":
                case "première":
                    index = 0;
                    break;

                case "second":
                case "deuxième":
                    index = 1;
                    break;

                case "third":
                case "troisième":
                    index = 2;
                    break;

                default:
                    return true;
            }

            GuildVault vault = new GuildVault(player, index, GetDummyVaultItem(player));
            player.ActiveInventoryObject = vault;
            player.Out.SendInventoryItemsUpdate(vault.GetClientInventory(player), eInventoryWindowType.HouseVault);

            return true;
        }

        private static ItemTemplate GetDummyVaultItem(GamePlayer player)
        {
            ItemTemplate vaultItem = new ItemTemplate();
            vaultItem.Object_Type = (int)eObjectType.HouseVault;
            vaultItem.Name = "Vault";
            vaultItem.ObjectId = player.Guild?.GuildID;
            switch (player.Realm)
            {
                case eRealm.Albion:
                    vaultItem.Id_nb = "housing_alb_vault";
                    vaultItem.Model = 1489;
                    break;
                case eRealm.Hibernia:
                    vaultItem.Id_nb = "housing_hib_vault";
                    vaultItem.Model = 1491;
                    break;
                case eRealm.Midgard:
                    vaultItem.Id_nb = "housing_mid_vault";
                    vaultItem.Model = 1493;
                    break;
            }

            return vaultItem;
        }
    }

    public sealed class GuildVault : CustomVault
    {
        private readonly int m_vaultNumber = 0;

        public const int NUM_VAULTS = 3;

        public Guild Guild { get; init; }

        /// <summary>
        /// A guild vault that masquerades as a house vault to the game client
        /// </summary>
        /// <param name="player">Player who owns the vault</param>
        /// <param name="vaultNPC">NPC controlling the interaction between player and vault</param>
        /// <param name="vaultNumber">Valid vault IDs are 0-1</param>
        /// <param name="dummyTemplate">An ItemTemplate to satisfy the base class's constructor</param>
        public GuildVault(GamePlayer player, int vaultNumber, ItemTemplate dummyTemplate)
            : base(player, player.Guild?.GuildID ?? "", vaultNumber, dummyTemplate)
        {
            Guild = player.Guild;
            m_vaultNumber = vaultNumber;
            Name = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.GuildVault.Item.Name", player.Guild?.Name ?? "unknown");
        }

        /// <inheritdoc />
        public override bool CanView(GamePlayer player)
        {
            if (player.Guild != Guild || player.GuildRank == null)
                return false;
            return player.GuildRank.CanViewVault(m_vaultNumber);
        }

        /// <inheritdoc />
        public override bool CanAddItem(GamePlayer player, InventoryItem item)
        {
            if (player.Guild != Guild || player.GuildRank == null)
                return false;
            return player.GuildRank.CanDepositInVault(m_vaultNumber);
        }

        /// <inheritdoc />
        public override bool CanRemoveItem(GamePlayer player, InventoryItem item)
        {
            if (player.Guild != Guild || player.GuildRank == null)
                return false;
            return player.GuildRank.CanWithdrawFromVault(m_vaultNumber);
        }

        public override string GetOwner(GamePlayer player)
        {
            return (player.Guild?.GuildID);
        }

        public override int FirstDBSlot
        {
            get
            {
                switch (m_vaultNumber)
                {
                    case 0:
                        return (int)2900;
                    case 1:
                        return (int)3000;
                    case 2:
                        return (int)3100;
                    default: return 0;
                }
            }
        }

        public override int LastDBSlot
        {
            get
            {
                switch (m_vaultNumber)
                {
                    case 0:
                        return (int)2999;
                    case 1:
                        return (int)3099;
                    case 2:
                        return (int)3199;
                    default: return 0;
                }
            }
        }

        /// <summary>
        /// List of items in the vault.
        /// </summary>
        public override IList<InventoryItem> DBItems(GamePlayer player = null)
        {
            return GameServer.Database.SelectObjects<InventoryItem>(DB.Column("OwnerID").IsEqualTo(Guild.GuildID).And(DB.Column("SlotPosition").IsGreaterOrEqualTo(FirstDBSlot).And(DB.Column("SlotPosition").IsLessOrEqualTo(LastDBSlot))));
        }
    }
}