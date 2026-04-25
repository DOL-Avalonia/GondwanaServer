using AmteScripts.PvP.CTF;
using DOL.Database;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;
using System.Collections.Generic;
using static AmteScripts.PvP.PvPScore;

namespace DOL.GS
{
    public class CharacterVaultKeeper : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;


            string message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.AccountVault.Keeper.Greetings", player.Name) + "\n\n";
            message += LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.AccountVault.Keeper.Access");
            player.Out.SendMessage(message, eChatType.CT_Say, eChatLoc.CL_PopupWindow);

            ItemTemplate vaultItem = GetDummyVaultItem(player);
            CharacterVault vault = new CharacterVault(player, 0, vaultItem);
            player.ActiveInventoryObject = vault;
            player.Out.SendInventoryItemsUpdate(vault.GetClientInventory(player), eInventoryWindowType.HouseVault);
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text))
                return false;

            GamePlayer player = source as GamePlayer;

            if (player == null)
                return false;

            if (text is ("first" or "première"))
            {
                CharacterVault vault = new CharacterVault(player, 0, GetDummyVaultItem(player));
                player.ActiveInventoryObject = vault;
                player.Out.SendInventoryItemsUpdate(vault.GetClientInventory(player), eInventoryWindowType.HouseVault);
            }
            else if (text is ("second" or "deuxième"))
            {
                CharacterVault vault = new CharacterVault(player, 1, GetDummyVaultItem(player));
                player.ActiveInventoryObject = vault;
                player.Out.SendInventoryItemsUpdate(vault.GetClientInventory(player), eInventoryWindowType.HouseVault);
            }

            return true;
        }

        public static ItemTemplate GetDummyVaultItem(GamePlayer player)
        {
            ItemTemplate vaultItem = new ItemTemplate();
            vaultItem.Object_Type = (int)eObjectType.HouseVault;
            vaultItem.Name = "Vault";
            vaultItem.ObjectId = player.InternalID;
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

    public sealed class CharacterVault : CustomVault
    {
        private readonly int m_vaultNumber = 0;

        /// <summary>
        /// A character vault that masquerades as a house vault to the game client
        /// </summary>
        /// <param name="player">Player who owns the vault</param>
        /// <param name="vaultNPC">NPC controlling the interaction between player and vault</param>
        /// <param name="vaultNumber">Valid vault IDs are 0-1</param>
        /// <param name="dummyTemplate">An ItemTemplate to satisfy the base class's constructor</param>
        public CharacterVault(GamePlayer player, int vaultNumber, ItemTemplate dummyTemplate)
            : base(player, player.InternalID, vaultNumber, dummyTemplate)
        {
            m_vaultNumber = vaultNumber;
            Name = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.CharacterVault.Item.Name", player.Name);
        }

        /// <inheritdoc />
        public override bool AddItem(GamePlayer player, InventoryItem item, bool quiet = false)
        {
            if (!CanAddItem(player, item))
            {
                if (!quiet)
                {
                    player.Out.SendMessage("GameUtils.CustomVault.NoAdd", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                return false;
            }

            var updated = GameInventoryObjectExtensions.AddItem(this, player, item);
            if (updated.Count == 0)
            {
                if (!quiet)
                {
                    player.Out.SendMessage("GameUtils.CharacterVault.Full", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                return false;
            }

            lock (m_vaultSync)
            {
                this.NotifyPlayers(this, player, _observers, updated);
            }
            return true;
        }

        public override string GetOwner(GamePlayer player)
        {
            return (player.InternalID);
        }

        /// <inheritdoc />
        public override bool CanHoldItem(InventoryItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item is StorageBagItem || item is FlagInventoryItem || item is PvPTreasure || item is AmteScripts.Managers.TerritoryRelicInventoryItem)
            {
                return false;
            }

            // Character vaults can hold untradable items
            return true;
        }

        public override int FirstDBSlot
        {
            get
            {
                switch (m_vaultNumber)
                {
                    case 0:
                        return (int)2700;
                    case 1:
                        return (int)2800;
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
                        return (int)2799;
                    case 1:
                        return (int)2899;
                    default: return 0;
                }
            }
        }
    }
}