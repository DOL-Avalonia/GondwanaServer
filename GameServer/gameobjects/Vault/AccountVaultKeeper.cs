using System.Collections.Generic;
using DOL.Database;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS
{
    public class AccountVaultKeeper : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            /*if (player.HCFlag)
            {
                SayTo(player,$"I'm sorry {player.Name}, my vault is not Hardcore enough for you.");
                return false;
            }*/

            // if (player.Level <= 1)
            // {
            //     SayTo(player,$"I'm sorry {player.Name}, come back if you are venerable to use my services.");
            //     return false;
            // }

            string message = LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.AccountVault.Keeper.Greetings", player.Name) + "\n\n";
            message += LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.AccountVault.Keeper.Access");
            player.Out.SendMessage(message, eChatType.CT_Say, eChatLoc.CL_PopupWindow);

            ItemTemplate vaultItem = GetDummyVaultItem(player);
            AccountVault vault = new AccountVault(player, 0, vaultItem);
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
                AccountVault vault = new AccountVault(player, 0, GetDummyVaultItem(player));
                player.ActiveInventoryObject = vault;
                player.Out.SendInventoryItemsUpdate(vault.GetClientInventory(player), eInventoryWindowType.HouseVault);
            }
            else if (text is ("second" or "deuxième"))
            {
                AccountVault vault = new AccountVault(player, 1, GetDummyVaultItem(player));
                player.ActiveInventoryObject = vault;
                player.Out.SendInventoryItemsUpdate(vault.GetClientInventory(player), eInventoryWindowType.HouseVault);
            }

            return true;
        }

        private static ItemTemplate GetDummyVaultItem(GamePlayer player)
        {
            ItemTemplate vaultItem = new ItemTemplate();
            vaultItem.Object_Type = (int)eObjectType.HouseVault;
            vaultItem.Name = "Vault";
            vaultItem.ObjectId = player.Client.Account.Name + "_" + player.Realm.ToString();
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

    public sealed class AccountVault : CustomVault
    {
        private readonly int m_vaultNumber = 0;

        /// <summary>
        /// An account vault that masquerades as a house vault to the game client
        /// </summary>
        /// <param name="player">Player who owns the vault</param>
        /// <param name="vaultNPC">NPC controlling the interaction between player and vault</param>
        /// <param name="vaultNumber">Valid vault IDs are 0-1</param>
        /// <param name="dummyTemplate">An ItemTemplate to satisfy the base class's constructor</param>
        public AccountVault(GamePlayer player, int vaultNumber, ItemTemplate dummyTemplate)
            : base(player, player.Client.Account.Name + "_" + player.Realm.ToString(), vaultNumber, dummyTemplate)
        {
            m_vaultNumber = vaultNumber;
            TranslationId = "GameUtils.AccountVault.Item.Name";
        }

        public override string GetOwner(GamePlayer player)
        {
            return player.Client.Account.Name + "_" + player.Realm.ToString();
        }

        public override int FirstDBSlot
        {
            get
            {
                switch (m_vaultNumber)
                {
                    case 0:
                        return (int)2500;
                    case 1:
                        return (int)2600;
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
                        return (int)2599;
                    case 1:
                        return (int)2699;
                    default: return 0;
                }
            }
        }
    }
}