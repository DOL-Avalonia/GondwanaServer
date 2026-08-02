using DOL.Database;
using DOL.Events;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using System;
using System.Linq;

namespace DOL.GS.Scripts
{
    public class PasswordCurseItem : GameInventoryItem
    {
        public PasswordCurseItem(ItemTemplate template) : base(template) { }
        public PasswordCurseItem(ItemUnique template) : base(template) { }
        public PasswordCurseItem(InventoryItem item) : base(item) { }
        public PasswordCurseItem() : base() { }

        /// <summary>
        /// Registers a global chat listener once when the server starts.
        /// (This uses the exact same logic as GuarkRing)
        /// </summary>
        [GameServerStartedEvent]
        public static void Init(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(GameLivingEvent.Say, new DOLEventHandler(OnPlayerSay));
        }

        /// <summary>
        /// Triggered whenever any player says something in normal chat.
        /// </summary>
        public static void OnPlayerSay(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;
            if (player == null) return;

            SayEventArgs sArgs = args as SayEventArgs;
            if (sArgs == null || string.IsNullOrEmpty(sArgs.Text)) return;

            string spokenText = sArgs.Text.Trim();

            // Look through the player's equipped items
            lock (player.Inventory)
            {
                foreach (InventoryItem item in player.Inventory.EquippedItems)
                {
                    if (item is PasswordCurseItem pci)
                    {
                        pci.ProcessPasswordAction(player, spokenText);
                    }
                }
            }
        }

        /// <summary>
        /// Process the spoken text to see if it matches the item's configured passwords.
        /// </summary>
        public void ProcessPasswordAction(GamePlayer player, string spokenText)
        {
            if (player.CurrentRegionID == ServerRules.AmtenaelRules.HousingRegionID)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemCannotUseHere"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return;
            }
            if (JailMgr.IsPrisoner(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemUsageJailed"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (player.IsRiding || player.IsOnHorse)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemMounted"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (SpellHandler.FindEffectOnTarget(player, "Petrify") != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemPetrified"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (player.IsDamned)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemDamned"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (player.IsCrafting)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemCrafting"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (player.IsClimbing)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemClimbing"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (player.TempProperties.getProperty<bool>("ArenaParticipant", false))
            {
                player.Out.SendMessage("You cannot use this item while participating in an Arena Contest.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (player.TempProperties.getProperty<object>(StealCommandHandlerBase.PLAYER_VOL_TIMER, null) != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemStealing"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (player.PlayerAfkMessage != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Items.Specialitems.PasswordCurseItemUsageAFK"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (player.HasTerritoryRelic())
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemUsageRelic"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            var wsd = SpellHandler.FindEffectOnTarget(player, "WarlockSpeedDecrease");
            if (wsd != null)
            {
                int rm = wsd.Spell?.ResurrectMana ?? 0;
                string appearancetype = LanguageMgr.GetWarlockMorphAppearance(player.Client.Account.Language, rm);

                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Items.Specialitems.PasswordCurseItemUsageMorphed", appearancetype), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                return;
            }

            string pkg = PackageID ?? Template?.PackageID ?? "";
            if (!pkg.Split(';').Any(p => p.StartsWith("PASSWORD")))
                return;

            int spellIdToCast = 0;

            // -------------------------------------------------------------
            // Define passwords / interactions per item ID_nb here:
            // -------------------------------------------------------------
            if (Id_nb == "Belt_of_the_archangel")
            {
                if (spokenText.Equals("HEAVEN", StringComparison.OrdinalIgnoreCase))
                {
                    spellIdToCast = 3345;
                }
                else if (spokenText.Equals("HELL", StringComparison.OrdinalIgnoreCase))
                {
                    spellIdToCast = 6679;
                }
            }
            // Add additional cursed item checks down here using "else if (Id_nb == "..."



            // If no valid formula was spoken for this item, do nothing
            if (spellIdToCast <= 0) return;

            int flag = Template != null ? Template.Flags : Flags;

            // Password manual consumption is for flags 43 and 45. (Flag 44 only consumes on death)
            if (flag == 43 || flag == 45)
            {
                double manaPct = 0;
                int condLoss = 0;
                bool destroyOnMana = false;
                bool destroyOnCond = false;

                foreach (string p in pkg.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = p.Split('|');
                    if (parts.Length > 0)
                    {
                        if (parts[0] == "MANA" && parts.Length >= 2)
                        {
                            double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out manaPct);
                            if (parts.Length >= 3 && parts[2] == "DESTROY") destroyOnMana = true;
                        }
                        else if (parts[0] == "COND" && parts.Length >= 2)
                        {
                            int.TryParse(parts[1], out condLoss);
                            if (parts.Length >= 3 && parts[2] == "DESTROY") destroyOnCond = true;
                        }
                    }
                }

                bool unequip = false;
                bool broken = false;
                bool destroy = false;
                bool statsChanged = false;
                bool invChanged = false;
                bool isPureMelee = GamePlayer.IsPureMeleeClass((eCharacterClass)player.CharacterClass.ID);

                if (isPureMelee)
                {
                    if (manaPct > 0)
                    {
                        int enduCost = (int)(player.MaxEndurance * (manaPct * 2.1) / 100.0);
                        int hpCost = (int)(player.MaxHealth * (manaPct * 0.4) / 100.0);
                        if (hpCost < 1) hpCost = 1;

                        if (player.Endurance >= enduCost && player.Health > hpCost)
                        {
                            player.Endurance -= enduCost;
                            player.Health -= hpCost;
                            statsChanged = true;
                        }
                        else
                        {
                            unequip = true;
                            if (destroyOnMana) destroy = true;
                        }
                    }
                }
                else
                {
                    if (manaPct > 0)
                    {
                        int manaCost = (int)(player.MaxMana * manaPct / 100.0);
                        if (player.Mana >= manaCost)
                        {
                            player.Mana -= manaCost;
                            statsChanged = true;
                        }
                        else
                        {
                            unequip = true;
                            if (destroyOnMana) destroy = true;
                        }
                    }
                }

                if (condLoss > 0 && !unequip && !destroy)
                {
                    Condition -= condLoss;
                    invChanged = true;
                    if (Condition <= 0)
                    {
                        Condition = 0;
                        unequip = true;
                        broken = true;
                        if (destroyOnCond) destroy = true;
                    }
                }

                if (statsChanged) player.Out.SendCharStatsUpdate();
                if (invChanged)
                {
                    player.Out.SendUpdateWeaponAndArmorStats();
                    player.Out.SendInventorySlotsUpdate(new int[] { SlotPosition });
                }

                // If the player lacks the resources (or item broke), force unequip and PREVENT the spell from firing!
                if (unequip)
                {
                    player.ForceUnequip(this, broken, destroy);
                    return;
                }
            }

            // Target has enough mana/condition, or item is flag 44. Fire the Spell!
            Spell spellToCast = SkillBase.GetSpellByID(spellIdToCast);
            if (spellToCast != null)
            {
                ISpellHandler handler = ScriptMgr.CreateSpellHandler(player, spellToCast, SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects));
                if (handler != null)
                {
                    handler.StartSpell(player);
                }
            }
        }
    }
}