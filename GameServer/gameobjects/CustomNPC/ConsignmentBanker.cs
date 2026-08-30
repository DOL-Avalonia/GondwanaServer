using DOL.Database;
using DOL.GS.Housing;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.Scripts
{
    public enum VaultType
    {
        Personal,
        Guild
    }

    public abstract class ConsignmentBanker : GameNPC
    {
        protected virtual VaultType? BankerType => null;

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            if (BankerType.HasValue)
            {
                string lang = player.Client?.Account?.Language ?? "EN";
                string msg = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Interact.HelpRetrieve") + "\n\n" +
                             "[" + LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Consignment") + "]\n" +
                             "[" + LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Vaults") + "]\n" +
                             "[" + LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.ArchivedGenistars") + "]";

                player.Out.SendMessage(msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text)) return false;
            if (!(source is GamePlayer player)) return true;

            string lang = player.Client?.Account?.Language ?? "EN";

            if (BankerType.HasValue)
            {
                string cmdConsignment = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Consignment");
                string cmdVaults = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Vaults");
                string cmdVault = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Vault");
                string cmdArchivedGenistars = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.ArchivedGenistars");
                string cmdRecover = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Recover");
                string cmdPersonal = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Personal");
                string cmdGuild = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Guild");

                if (text.Equals("consignment", StringComparison.OrdinalIgnoreCase) || text.Equals(cmdConsignment, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryGetConsignmentMerchant(player, BankerType.Value, out GameConsignmentMerchant cm))
                        cm.Interact(player);
                    else
                        player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Whisper.CannotAccessMerchant"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    return true;
                }

                if (text.Equals("vaults", StringComparison.OrdinalIgnoreCase) || text.Equals(cmdVaults, StringComparison.OrdinalIgnoreCase))
                {
                    string msg = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Whisper.WhichVault") + "\n" +
                                 "[" + cmdVault + " 1]\n" +
                                 "[" + cmdVault + " 2]\n" +
                                 "[" + cmdVault + " 3]\n" +
                                 "[" + cmdVault + " 4]";
                    player.Out.SendMessage(msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    return true;
                }

                if (text.StartsWith("vault ", StringComparison.OrdinalIgnoreCase) || text.StartsWith(cmdVault + " ", StringComparison.OrdinalIgnoreCase))
                {
                    string numStr = text.StartsWith("vault ", StringComparison.OrdinalIgnoreCase) ? text.Substring(6) : text.Substring(cmdVault.Length + 1);
                    if (int.TryParse(numStr, out int index) && index >= 1 && index <= 4)
                    {
                        if (TryGetVault(player, BankerType.Value, index - 1, out GameVault vault))
                            vault.Interact(player);
                        else
                            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Whisper.CannotAccessVault"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                    }
                    return true;
                }

                if (text.Equals("archived genistars", StringComparison.OrdinalIgnoreCase) || text.Equals(cmdArchivedGenistars, StringComparison.OrdinalIgnoreCase))
                {
                    RecoverArchivedGenistars(player, BankerType.Value, -1);
                    return true;
                }

                if (text.StartsWith("recover ", StringComparison.OrdinalIgnoreCase) || text.StartsWith(cmdRecover + " ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = text.Split(' ');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int index))
                    {
                        if ((parts[1].Equals("personal", StringComparison.OrdinalIgnoreCase) || parts[1].Equals(cmdPersonal, StringComparison.OrdinalIgnoreCase)) && BankerType == VaultType.Personal)
                            RecoverArchivedGenistars(player, VaultType.Personal, index);
                        else if ((parts[1].Equals("guild", StringComparison.OrdinalIgnoreCase) || parts[1].Equals(cmdGuild, StringComparison.OrdinalIgnoreCase)) && BankerType == VaultType.Guild)
                            RecoverArchivedGenistars(player, VaultType.Guild, index);
                    }
                    else if (parts.Length == 2 && int.TryParse(parts[1], out int idx))
                    {
                        RecoverArchivedGenistars(player, BankerType.Value, idx);
                    }
                    return true;
                }
            }

            return true;
        }

        protected void RecoverArchivedGenistars(GamePlayer player, VaultType type, int selectedIndex)
        {
            string lang = player.Client?.Account?.Language ?? "EN";
            string ownerId = type == VaultType.Personal ? player.InternalID : player.Guild?.GuildID;

            if (string.IsNullOrEmpty(ownerId))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.NoValidOwner"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            House house = type == VaultType.Personal ? HouseMgr.GetHouseByPlayer(player) : HouseMgr.GetGuildHouseByPlayer(player);
            if (house == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.MustOwnHouse"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            bool hasGenistarInInv = false;
            lock (player.Inventory)
            {
                foreach (InventoryItem item in player.Inventory.AllItems)
                {
                    if (item.Id_nb.StartsWith("genistar_pet") || item.Id_nb.StartsWith("genistar_remains") || (item.Template != null && item.Template.Flags == 26))
                    {
                        hasGenistarInInv = true;
                        break;
                    }
                }
            }

            if (hasGenistarInInv)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.AlreadyCarryGenistar"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            var archivedGenistars = GameServer.Database.SelectObjects<DBGenistar>(
                DB.Column("OwnerID").IsEqualTo(ownerId).And(DB.Column("State").IsEqualTo(5))).OrderBy(g => g.GenistarID).ToList();

            if (archivedGenistars.Count == 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.NoArchivedGenistars"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            var houseGenistars = GameServer.Database.SelectObjects<DBGenistar>(DB.Column("HouseNumber").IsEqualTo(house.HouseNumber).And(DB.Column("State").IsNotEqualTo(5)));
            int limit = house.GetGenistarLimit();

            if (limit <= 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.HouseNotSupportGenistar"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            if (houseGenistars.Count >= limit)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.MaxCapacityGenistars", limit), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            if (archivedGenistars.Count > 1 && selectedIndex == -1)
            {
                string cmdRecover = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Recover");
                string tag = this is MasterConsignmentBanker
                    ? (type == VaultType.Personal
                        ? $"{cmdRecover} {LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Personal")}"
                        : $"{cmdRecover} {LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Guild")}")
                    : cmdRecover;

                string listMsg = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.WhichGenistar") + "\n";
                int i = 1;
                foreach (var g in archivedGenistars)
                {
                    string name = !string.IsNullOrEmpty(g.CustomName) ? g.CustomName : g.Name;
                    int level = GenistarPet.CalculateGenistarLevel(g.OwnerID, g.GenistarExperience);
                    listMsg += $"[{tag} {i}] {name} ({LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.Level")} {level})\n";
                    i++;
                }
                player.Out.SendMessage(listMsg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            DBGenistar genToRecover = null;
            if (archivedGenistars.Count == 1)
            {
                genToRecover = archivedGenistars[0];
            }
            else if (selectedIndex > 0 && selectedIndex <= archivedGenistars.Count)
            {
                genToRecover = archivedGenistars[selectedIndex - 1];
            }

            if (genToRecover == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.InvalidSelection"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return;
            }

            int assignedDictKey = -1;
            foreach (var kvp in house.OutdoorItems)
            {
                if (kvp.Value.BaseItem != null && kvp.Value.BaseItem.Flags == 25)
                {
                    if (!houseGenistars.Any(g => g.PlaceholderKey == kvp.Key))
                    {
                        assignedDictKey = kvp.Key;
                        break;
                    }
                }
            }

            if (assignedDictKey == -1)
            {
                ItemTemplate placeholderTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>("genistar_placeholder");
                if (placeholderTemplate == null)
                {
                    var tmpls = GameServer.Database.SelectObjects<ItemTemplate>(DB.Column("Flags").IsEqualTo(25));
                    if (tmpls.Count > 0) placeholderTemplate = tmpls[0];
                }

                if (placeholderTemplate != null)
                {
                    int pos = 0;
                    while (house.OutdoorItems.ContainsKey(pos)) pos++;

                    int freePosition = 0;
                    var usedPositions = house.OutdoorItems.Values.Select(v => (int)v.Position).ToHashSet();
                    while (usedPositions.Contains(freePosition)) freePosition++;

                    var oitem = new OutdoorItem
                    {
                        BaseItem = placeholderTemplate,
                        Model = placeholderTemplate.Model,
                        Position = freePosition,
                        Rotation = 0
                    };

                    DBHouseOutdoorItem odbitem = oitem.CreateDBOutdoorItem(house.HouseNumber);
                    oitem.DatabaseItem = odbitem;
                    GameServer.Database.AddObject(odbitem);

                    house.OutdoorItems.Add(pos, oitem);
                    house.SpawnGenistarVisual(pos, oitem, null);
                    house.SaveIntoDatabase();
                    house.SendUpdate();

                    assignedDictKey = pos;
                }
            }

            if (assignedDictKey == -1)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.NoGardenSlot"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            ItemTemplate baseItemTmpl = GameServer.Database.FindObjectByKey<ItemTemplate>("genistar_pet");
            if (baseItemTmpl == null) return;

            ItemUnique unique = new ItemUnique(baseItemTmpl);
            string dispName = !string.IsNullOrEmpty(genToRecover.CustomName) ? genToRecover.CustomName : genToRecover.Name;
            unique.Name = $"Genistar: {dispName}";
            unique.PackageID = genToRecover.GenistarID;
            unique.Charges = 1;
            unique.MaxCharges = 1;
            unique.Condition = baseItemTmpl.MaxCondition;

            GameServer.Database.AddObject(unique);

            InventoryItem petItem = GameInventoryItem.Create(unique);
            petItem.PackageID = genToRecover.GenistarID;
            petItem.Count = 1;
            petItem.Condition = unique.Condition;

            lock (player.Inventory)
            {
                if (player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, petItem))
                {
                    genToRecover.State = 6;
                    genToRecover.HouseNumber = house.HouseNumber;
                    genToRecover.PlaceholderKey = assignedDictKey;
                    GameServer.Database.SaveObject(genToRecover);

                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.GenistarNPC.PackSuccess", dispName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.RecoveredGenistar", dispName), eChatType.CT_Say, eChatLoc.CL_PopupWindow);

                    house.UpdateGenistarVisual(assignedDictKey, 1293);
                }
                else
                {
                    GameServer.Database.DeleteObject(unique);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Recover.BackpackFull"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }
        }

        protected static bool TryGetConsignmentMerchant(GamePlayer player, VaultType type, out GameConsignmentMerchant consignmentMerchant)
        {
            consignmentMerchant = null;
            string ownerId = type == VaultType.Personal ? player.InternalID : player.Guild?.GuildID;

            if (string.IsNullOrEmpty(ownerId)) return false;

            House house = type == VaultType.Personal ? HouseMgr.GetHouseByPlayer(player) : HouseMgr.GetGuildHouseByPlayer(player);
            if (house != null && house.ConsignmentMerchant != null)
                return false;

            consignmentMerchant = new RecoveredConsignmentMerchant(player, ownerId);
            return true;
        }

        protected static bool TryGetVault(GamePlayer player, VaultType type, int index, out GameVault vault)
        {
            vault = null;
            string ownerId = type == VaultType.Personal ? player.InternalID : player.Guild?.GuildID;

            if (string.IsNullOrEmpty(ownerId)) return false;

            House house = type == VaultType.Personal ? HouseMgr.GetHouseByPlayer(player) : HouseMgr.GetGuildHouseByPlayer(player);
            if (house != null && house.HousepointItems.Values.Any(h => h.Index == index && h.ItemTemplateID.EndsWith("_vault")))
                return false;

            vault = new RecoveredVault(player, ownerId, index);
            return true;
        }
    }

    public class MasterConsignmentBanker : ConsignmentBanker
    {
        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);
            GuildName = "Master Consignment Banker";
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            string lang = player.Client?.Account?.Language ?? "EN";
            string msg = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Master.Interact", player.Name) + "\n\n" +
                         "[" + LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.PersonalConsignment") + "]\n" +
                         "[" + LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.GuildConsignment") + "]\n" +
                         "[" + LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.PersonalVaults") + "]\n" +
                         "[" + LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.GuildVaults") + "]\n" +
                         "[" + LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.ArchivedGenistarsPersonal") + "]\n" +
                         "[" + LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.ArchivedGenistarsGuild") + "]";

            player.Out.SendMessage(msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (!base.WhisperReceive(source, text)) return false;
            if (!(source is GamePlayer player)) return true;

            string lang = player.Client?.Account?.Language ?? "EN";

            string cmdPersCons = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.PersonalConsignment");
            string cmdGuildCons = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.GuildConsignment");
            string cmdPersVaults = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.PersonalVaults");
            string cmdGuildVaults = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.GuildVaults");
            string cmdPersVault = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.PersonalVault");
            string cmdGuildVault = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.GuildVault");
            string cmdArchGenPers = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.ArchivedGenistarsPersonal");
            string cmdArchGenGuild = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.ArchivedGenistarsGuild");

            if (text.Equals("personal consignment", StringComparison.OrdinalIgnoreCase) || text.Equals(cmdPersCons, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetConsignmentMerchant(player, VaultType.Personal, out GameConsignmentMerchant cm))
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Master.CannotAccessPersonalMerchant"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                else
                    cm.Interact(player);
                return true;
            }

            if (text.Equals("guild consignment", StringComparison.OrdinalIgnoreCase) || text.Equals(cmdGuildCons, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryGetConsignmentMerchant(player, VaultType.Guild, out GameConsignmentMerchant cm))
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Master.CannotAccessMerchant"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                else
                    cm.Interact(player);
                return true;
            }

            if (text.Equals("personal vaults", StringComparison.OrdinalIgnoreCase) || text.Equals(cmdPersVaults, StringComparison.OrdinalIgnoreCase))
            {
                string msg = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Whisper.WhichVault") + "\n" +
                             "[" + cmdPersVault + " 1]\n" +
                             "[" + cmdPersVault + " 2]\n" +
                             "[" + cmdPersVault + " 3]\n" +
                             "[" + cmdPersVault + " 4]";
                player.Out.SendMessage(msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (text.StartsWith("personal vault ", StringComparison.OrdinalIgnoreCase) || text.StartsWith(cmdPersVault + " ", StringComparison.OrdinalIgnoreCase))
            {
                string numStr = text.StartsWith("personal vault ", StringComparison.OrdinalIgnoreCase) ? text.Substring(15) : text.Substring(cmdPersVault.Length + 1);
                if (int.TryParse(numStr, out int index) && index >= 1 && index <= 4)
                {
                    if (TryGetVault(player, VaultType.Personal, index - 1, out GameVault vault)) vault.Interact(player);
                    else player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Master.CannotAccessVault"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                return true;
            }

            if (text.Equals("guild vaults", StringComparison.OrdinalIgnoreCase) || text.Equals(cmdGuildVaults, StringComparison.OrdinalIgnoreCase))
            {
                string msg = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Whisper.WhichVault") + "\n" +
                             "[" + cmdGuildVault + " 1]\n" +
                             "[" + cmdGuildVault + " 2]\n" +
                             "[" + cmdGuildVault + " 3]\n" +
                             "[" + cmdGuildVault + " 4]";
                player.Out.SendMessage(msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                return true;
            }

            if (text.StartsWith("guild vault ", StringComparison.OrdinalIgnoreCase) || text.StartsWith(cmdGuildVault + " ", StringComparison.OrdinalIgnoreCase))
            {
                string numStr = text.StartsWith("guild vault ", StringComparison.OrdinalIgnoreCase) ? text.Substring(12) : text.Substring(cmdGuildVault.Length + 1);
                if (int.TryParse(numStr, out int index) && index >= 1 && index <= 4)
                {
                    if (TryGetVault(player, VaultType.Guild, index - 1, out GameVault vault)) vault.Interact(player);
                    else player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Master.CannotAccessVault"), eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                }
                return true;
            }

            if (text.Equals("archived genistars (personal)", StringComparison.OrdinalIgnoreCase) || text.Equals(cmdArchGenPers, StringComparison.OrdinalIgnoreCase))
            {
                RecoverArchivedGenistars(player, VaultType.Personal, -1);
                return true;
            }

            if (text.Equals("archived genistars (guild)", StringComparison.OrdinalIgnoreCase) || text.Equals(cmdArchGenGuild, StringComparison.OrdinalIgnoreCase))
            {
                RecoverArchivedGenistars(player, VaultType.Guild, -1);
                return true;
            }

            string cmdRecover = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Recover");
            string cmdPersonal = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Personal");
            string cmdGuild = LanguageMgr.GetTranslation(lang, "ConsignmentBanker.Cmd.Guild");

            if (text.StartsWith("recover personal ", StringComparison.OrdinalIgnoreCase) || text.StartsWith(cmdRecover + " " + cmdPersonal + " ", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = text.Split(' ');
                if (parts.Length == 3 && int.TryParse(parts[2], out int index))
                    RecoverArchivedGenistars(player, VaultType.Personal, index);
                return true;
            }

            if (text.StartsWith("recover guild ", StringComparison.OrdinalIgnoreCase) || text.StartsWith(cmdRecover + " " + cmdGuild + " ", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = text.Split(' ');
                if (parts.Length == 3 && int.TryParse(parts[2], out int index))
                    RecoverArchivedGenistars(player, VaultType.Guild, index);
                return true;
            }

            return true;
        }
    }

    public class PersonalConsignmentBanker : ConsignmentBanker
    {
        protected override VaultType? BankerType => VaultType.Personal;
        public override void LoadFromDatabase(DataObject obj) { base.LoadFromDatabase(obj); GuildName = "Personal Consignment Banker"; }
    }

    public class GuildConsignmentBanker : ConsignmentBanker
    {
        protected override VaultType? BankerType => VaultType.Guild;
        public override void LoadFromDatabase(DataObject obj) { base.LoadFromDatabase(obj); GuildName = "Guild Consignment Banker"; }
    }

    /// <summary>
    /// Specialized temporary CM spawned by the Bankers to enforce withdrawal bounds
    /// </summary>
    public class RecoveredConsignmentMerchant : GameConsignmentMerchant
    {
        private string m_overrideOwnerId;
        public RecoveredConsignmentMerchant(GamePlayer player, string ownerId)
        {
            m_overrideOwnerId = ownerId;
            houseRequired = false;
            OwnerID = ownerId;
            HouseNumber = 0;
            Position = player.Position;
            CurrentRegionID = player.CurrentRegionID;
            Name = "Recovered Consignment";
            Model = 144;
        }

        public override string GetOwner(GamePlayer player) => m_overrideOwnerId;

        public override bool CanHandleMove(GamePlayer player, ushort fromClientSlot, ushort toClientSlot)
        {
            return player.ActiveInventoryObject == this && this.IsVaultInventorySlot(fromClientSlot) && !this.IsVaultInventorySlot(toClientSlot);
        }

        public override bool SetSellPrice(GamePlayer player, ushort clientSlot, uint price) { return false; }
        public override void OnPlayerBuy(GamePlayer player, eInventorySlot fromClientSlot, eInventorySlot toClientSlot, bool usingMarketExplorer = false) { }
    }

    /// <summary>
    /// Specialized temporary Vault spawned by the Bankers to enforce withdrawal bounds
    /// </summary>
    public class RecoveredVault : GameVault
    {
        private string m_overrideOwnerId;
        public RecoveredVault(GamePlayer player, string ownerId, int index)
        {
            m_overrideOwnerId = ownerId;
            Index = index;
            CurrentRegionID = player.CurrentRegionID;
            Position = player.Position;
            Name = $"Recovered Vault {index + 1}";
            Model = 1489;
        }

        public override string GetOwner(GamePlayer player = null) => m_overrideOwnerId;

        public override bool CanHandleMove(GamePlayer player, ushort fromSlot, ushort toSlot)
        {
            return player.ActiveInventoryObject == this && IsVaultInventorySlot(fromSlot) && !IsVaultInventorySlot(toSlot);
        }

        public override bool AddItem(GamePlayer player, InventoryItem item, bool quiet = false) => false;
        public override bool CanAddItem(GamePlayer player, InventoryItem item) => false;
    }
}