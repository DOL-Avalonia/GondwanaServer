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
using Discord;
using DOL.Bonus;
using DOL.Database;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.GS.Spells;
using DOL.GS.Scripts;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static AmteScripts.PvP.PvPScore;

namespace DOL.GS
{
    public static class InventoryItemExpansions
    {
        public static bool IsMagicalItem(this InventoryItem self) => self is { Object_Type: (int)eObjectType.Magical, Item_Type: (int)eInventorySlot.FirstBackpack or 41 };

        public static bool IsParchment(this InventoryItem self) => IsMagicalItem(self) && self.Id_nb.Contains("parch", StringComparison.InvariantCultureIgnoreCase);

        public static bool IsPotion(this InventoryItem self) => IsMagicalItem(self) && self.Item_Type == 40 && !self.Id_nb.Contains("parch", StringComparison.InvariantCultureIgnoreCase);
    }
    
    /// <summary>
    /// This class represents an inventory item
    /// </summary>
    public class GameInventoryItem : InventoryItem, IGameInventoryItem, ITranslatableObject
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        protected GamePlayer m_owner = null;


        public GameInventoryItem()
            : base()
        {
            this.BonusConditions = new List<BonusCondition>();
        }

        public GameInventoryItem(ItemTemplate template)
            : base(template)
        {
            this.BonusConditions = BonusCondition.LoadFromString(template.BonusConditions)?.ToList();
        }

        public GameInventoryItem(ItemUnique template)
            : base(template)
        {
            this.BonusConditions = BonusCondition.LoadFromString(template.BonusConditions)?.ToList();
        }

        public GameInventoryItem(InventoryItem item)
            : base(item)
        {
            OwnerID = item.OwnerID;
            ObjectId = item.ObjectId;
            this.BonusConditions = BonusCondition.LoadFromString(item.Template?.BonusConditions)?.ToList();
        }

        public virtual LanguageDataObject.eTranslationIdentifier TranslationIdentifier
        {
            get { return LanguageDataObject.eTranslationIdentifier.eItem; }
        }

        public List<BonusCondition> BonusConditions
        {
            get;
            protected set;
        }

        /// <summary>
        /// Holds the translation id.
        /// </summary>
        protected string m_translationId = "";

        /// <summary>
        /// Gets or sets the translation id.
        /// </summary>
        public string TranslationId
        {
            get { return m_translationId; }
            set { m_translationId = (value == null ? "" : value); }
        }

        /// <summary>
        /// Is this a valid item for this player?
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public virtual bool CheckValid(GamePlayer player)
        {
            m_owner = player;
            return true;
        }

        /// <summary>
        /// Can this item be saved or loaded from the database?
        /// </summary>
        public virtual bool CanPersist
        {
            get
            {
                if (Id_nb == InventoryItem.BLANK_ITEM)
                    return false;

                return true;
            }
        }

        /// <summary>
        /// Can player equip this item?
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public virtual bool CanEquip(GamePlayer player)
        {
            int flags = Template != null ? Template.Flags : Flags;

            if (flags == 43 || flags == 44 || flags == 45)
            {
                if (Condition <= 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.UndeequippableItem.ConditionDepleted", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            if (flags == 45)
            {
                long blockedUntil = player.TempProperties.getProperty<long>("BlockedEquip_" + Id_nb, 0L);
                if (blockedUntil > player.CurrentRegion.Time)
                {
                    long remaining = (blockedUntil - player.CurrentRegion.Time) / 1000;
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.UndeequippableItem.EquipCooldown", remaining, Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            return GameServer.ServerRules.CheckAbilityToUseItem(player, Template);
        }

        #region Create From Object Source

        /// <summary>
        /// This is used to create a PlayerInventoryItem
        /// ClassType will be checked and the approrpiate GameInventoryItem created
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <returns></returns>
        [Obsolete("Use Create() instead")]
        public static GameInventoryItem Create<T>(ItemTemplate item)
        {
            return Create(item);
        }



        private BonusCondition GetBonusCondition(string bonusName)
        {
            return this.BonusConditions?.FirstOrDefault(b => b.BonusName.Equals(bonusName));
        }


        public bool IsBonusAllowed(string bonusName, GamePlayer player)
        {
            var bonusCondition = this.GetBonusCondition(bonusName);

            //If bonus not present, it is allowed
            if (bonusCondition == null)
            {
                return true;
            }

            if (bonusCondition.ChampionLevel > 0 && player.ChampionLevel < bonusCondition.ChampionLevel)
            {
                return false;
            }

            if (bonusCondition.MlLevel > 0 && player.MLLevel < bonusCondition.MlLevel)
            {
                return false;
            }

            if (bonusCondition.IsRenaissanceRequired && !player.IsRenaissance)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// This is used to create a PlayerInventoryItem
        /// template.ClassType will be checked and the approrpiate GameInventoryItem created
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <returns></returns>
        [Obsolete("Use Create() instead")]
        public static GameInventoryItem Create<T>(InventoryItem item)
        {
            return Create(item);
        }

        /// <summary>
        /// This is used to create a PlayerInventoryItem
        /// ClassType will be checked and the approrpiate GameInventoryItem created
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static GameInventoryItem Create(ItemTemplate item)
        {
            string classType = item.ClassType;
            var itemUnique = item as ItemUnique;

            if (!string.IsNullOrEmpty(classType))
            {
                var itemClass = item.ClassType.ToLower();
                var itemIsSpecial = itemClass.StartsWith("currency") || itemClass.StartsWith("token");
                if (!itemIsSpecial)
                {
                    GameInventoryItem gameItem;
                    if (itemUnique != null)
                        gameItem = ScriptMgr.CreateObjectFromClassType<GameInventoryItem, ItemUnique>(classType, itemUnique);
                    else
                        gameItem = ScriptMgr.CreateObjectFromClassType<GameInventoryItem, ItemTemplate>(classType, item);

                    if (gameItem != null)
                        return gameItem;

                    if (log.IsWarnEnabled)
                        log.WarnFormat("Failed to construct game inventory item of ClassType {0}!", classType);
                }
            }

            if (itemUnique != null)
                return new GameInventoryItem(itemUnique);

            return new GameInventoryItem(item);
        }

        /// <summary>
        /// This is used to create a PlayerInventoryItem
        /// template.ClassType will be checked and the approrpiate GameInventoryItem created
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static GameInventoryItem Create(InventoryItem item)
        {
            string classType = item.Template.ClassType;
            var itemIsNoCurrency = !item.ClassType.ToLower().StartsWith("currency.");

            if (!string.IsNullOrEmpty(classType) && itemIsNoCurrency)
            {
                GameInventoryItem gameItem = ScriptMgr.CreateObjectFromClassType<GameInventoryItem, InventoryItem>(classType, item);

                if (gameItem != null)
                    return gameItem;

                if (log.IsWarnEnabled)
                    log.WarnFormat("Failed to construct game inventory item of ClassType {0}!", classType);
            }

            return new GameInventoryItem(item);
        }

        #endregion

        /// <summary>
        /// Player receives this item (added to players inventory)
        /// </summary>
        /// <param name="player"></param>
        public virtual void OnReceive(GamePlayer player)
        {
            m_owner = player;
        }

        /// <summary>
        /// Player loses this item (removed from inventory)
        /// </summary>
        /// <param name="player"></param>
        public virtual void OnLose(GamePlayer player)
        {
            m_owner = null;
        }

        /// <summary>
        /// Drop this item on the ground
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public virtual WorldInventoryItem Drop(GamePlayer player)
        {
            WorldInventoryItem worldItem = new WorldInventoryItem(this);

            var itemPosition = player.Position + Vector.Create(player.Orientation, length: 30);
            worldItem.Position = itemPosition;

            worldItem.AddOwner(player);
            worldItem.AddToWorld();

            return worldItem;
        }

        /// <summary>
        /// This object is being removed from the world
        /// </summary>
        public virtual void OnRemoveFromWorld()
        {
        }

        /// <summary>
        /// Player equips this item
        /// </summary>
        /// <param name="player"></param>
        public virtual void OnEquipped(GamePlayer player)
        {
            if (Template != null && (Template.Flags == 43 || Template.Flags == 44 || Template.Flags == 45))
                player.CheckConsumeTimer();

            CheckValid(player);
        }

        /// <summary>
        /// Player unequips this item
        /// </summary>
        /// <param name="player"></param>
        public virtual void OnUnEquipped(GamePlayer player)
        {
            if (Template != null && (Template.Flags == 43 || Template.Flags == 44 || Template.Flags == 45))
                player.CheckConsumeTimer();

            CheckValid(player);
        }

        /// <summary>
        /// This inventory is used for a spell cast (staves lose condition when spells are cast)
        /// </summary>
        /// <param name="player"></param>
        /// <param name="target"></param>
        public virtual void OnSpellCast(GameLiving owner, GameObject target, Spell spell)
        {
            OnStrikeTarget(owner, target);
        }

        /// <summary>
        /// This inventory strikes an enemy
        /// </summary>
        /// <param name="player"></param>
        /// <param name="target"></param>
        public virtual void OnStrikeTarget(GameLiving owner, GameObject target)
        {
            if (owner is GamePlayer)
            {
                GamePlayer player = owner as GamePlayer;

                if (ConditionPercent > 70 && Util.Chance(ServerProperties.Properties.ITEM_CONDITION_LOSS_CHANCE))
                {
                    int oldPercent = ConditionPercent;
                    double con = GamePlayer.GetConLevel(player!.Level, Level);
                    if (con < -3.0)
                        con = -3.0;
                    int sub = (int)(con + 4);
                    if (oldPercent < 91)
                    {
                        sub *= 2;
                    }

                    // Subtract condition
                    Condition -= sub;
                    if (Condition < 0)
                        Condition = 0;

                    if (ConditionPercent != oldPercent)
                    {
                        if (ConditionPercent == 90)
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Attack.CouldRepair", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        else if (ConditionPercent == 80)
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Attack.NeedRepair", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        else if (ConditionPercent == 70)
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Attack.NeedRepairDire", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        player.Out.SendUpdateWeaponAndArmorStats();
                        player.Out.SendInventorySlotsUpdate(new int[] { SlotPosition });
                    }
                }
            }
        }

        /// <summary>
        /// This inventory is struck by an enemy
        /// </summary>
        /// <param name="player"></param>
        /// <param name="enemy"></param>
        public virtual void OnStruckByEnemy(GameLiving owner, GameLiving enemy)
        {
            if (owner is GamePlayer)
            {
                GamePlayer player = owner as GamePlayer;

                if (ConditionPercent > 70 && Util.Chance(ServerProperties.Properties.ITEM_CONDITION_LOSS_CHANCE))
                {
                    int oldPercent = ConditionPercent;
                    double con = GamePlayer.GetConLevel(player!.Level, Level);
                    if (con < -3.0)
                        con = -3.0;
                    int sub = (int)(con + 4);
                    if (oldPercent < 91)
                    {
                        sub *= 2;
                    }

                    // Subtract condition
                    Condition -= sub;
                    if (Condition < 0)
                        Condition = 0;

                    if (ConditionPercent != oldPercent)
                    {
                        if (ConditionPercent == 90)
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Attack.CouldRepair", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        else if (ConditionPercent == 80)
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Attack.NeedRepair", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        else if (ConditionPercent == 70)
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Attack.NeedRepairDire", Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        player.Out.SendUpdateWeaponAndArmorStats();
                        player.Out.SendInventorySlotsUpdate(new int[] { SlotPosition });
                    }
                }
            }
        }

        /// <summary>
        /// Try and use this item
        /// </summary>
        /// <param name="player"></param>
        /// <returns>true if item use is handled here</returns>
        public virtual bool Use(GamePlayer player)
        {
            // Stop players from manually right-clicking /using the Genistar items
            if (Template != null && Template.Flags == 25)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.UseSlot.GenistarHouseGarden"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true; 
            }

            if (Template != null && (Template.Flags == 46 || Template.Flags == 47))
            {
                player.Out.SendMessage("This loan coupon can only be activated automatically when making an expensive purchase at a merchant.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return true;
            }
            return false;
        }


        /// <summary>
        /// Combine this item with the target item
        /// </summary>
        /// <param name="player"></param>
        /// <param name="targetItem"></param>
        /// <returns>true if combine is handled here</returns>
        public virtual bool Combine(GamePlayer player, InventoryItem targetItem)
        {
            return false;
        }

        /// <summary>
        /// Delve this item
        /// </summary>
        /// <param name="delve"></param>
        /// <param name="player"></param>
        public virtual void Delve(List<String> delve, GamePlayer player)
        {
            if (player == null)
                return;

            string lang = player.Client.Account.Language;

            //**********************************
            //show crafter name
            //**********************************
            if (IsCrafted)
            {
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.CrafterName", Creator));
                delve.Add(" ");
            }

            string desc = GetTranslatedDescription(player);
            if (!string.IsNullOrEmpty(desc))
            {
                delve.Add(desc);
                delve.Add(" ");
            }

            if (this is StorageBagItem bagItem)
            {
                int extraWeight = bagItem.GetAdditionalWeight();
                int totalBagWeight = this.Weight + extraWeight;
                int filledSlots = bagItem.GetFilledSlotsCount();

                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.WriteStorageBagInfo.Weight", totalBagWeight / 10.0f, filledSlots));
                delve.Add(" ");
            }

            WriteUsableClasses(delve, player.Client);

            bool isGenWeapon = Flags >= 30 && Flags <= 36 && Flags != 32;
            bool isGenShield = Flags == 32;
            bool isGenArmor = Flags >= 38 && Flags <= 40;
            bool isGenistar = isGenWeapon || isGenShield || isGenArmor;

            if (isGenWeapon || (!isGenShield && !isGenArmor && Object_Type >= (int)eObjectType.GenericWeapon && Object_Type <= (int)eObjectType._LastWeapon))
            {
                WriteMagicalBonuses(delve, player.Client, false, isGenistar);
                DelveWeaponStats(delve, player);
            }

            if (Object_Type == (int)eObjectType.Instrument)
            {
                WriteMagicalBonuses(delve, player.Client, false, isGenistar);
            }

            if (isGenArmor || (!isGenWeapon && !isGenShield && Object_Type >= (int)eObjectType.Cloth && Object_Type <= (int)eObjectType.Scale))
            {
                WriteMagicalBonuses(delve, player.Client, false, isGenistar);
                DelveArmorStats(delve, player);
            }

            if (isGenShield || (!isGenWeapon && !isGenArmor && Object_Type == (int)eObjectType.Shield))
            {
                WriteMagicalBonuses(delve, player.Client, false, isGenistar);
                DelveShieldStats(delve, player.Client);
            }

            if (Object_Type == (int)eObjectType.Magical || Object_Type == (int)eObjectType.AlchemyTincture || Object_Type == (int)eObjectType.SpellcraftGem)
            {
                WriteUsableClasses(delve, player.Client);
                WriteMagicalBonuses(delve, player.Client, false, isGenistar);
            }

            //***********************************
            //shows info for Poison Potions
            //***********************************
            if (Object_Type == (int)eObjectType.Poison)
            {
                WritePoisonInfo(delve, player.Client);
            }

            if (this.IsPotion() || this.ClassType.Contains("DOL.GS.DieTriggerSpell")) // potion or "DieTriggerSpell" token
            {
                WritePotionInfo(delve, player.Client);
            }
            else if (CanUseEvery > 0)
            {
                // Items with a reuse timer (aka cooldown).
                delve.Add(" ");

                int minutes = CanUseEvery / 60;
                int seconds = CanUseEvery % 60;

                if (minutes == 0)
                {
                    delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.UseEverySec", seconds));
                }
                else
                {
                    delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.UseEveryMin", minutes, seconds));
                }

                // delve.Add(String.Format("Can use item every: {0:00}:{1:00}", minutes, seconds));

                int cooldown = CanUseAgainIn;

                if (cooldown > 0)
                {
                    minutes = cooldown / 60;
                    seconds = cooldown % 60;

                    if (minutes == 0)
                    {
                        delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.UseAgainSec", seconds));
                    }
                    else
                    {
                        delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.UseAgainMin", minutes, seconds));
                    }
                }
            }

            //**********************************
            //Bank loan coupon items
            //**********************************

            if (Flags == 46)
            {
                delve.Add(" ");
                delve.Add("Personal Loan Coupon");
                delve.Add(LanguageMgr.GetTranslation(lang, "DelveInfo.Value", MaxCondition) + " Gold");
                delve.Add("Hand this to any standard merchant when you are short on funds to cash it.");

                long minPrice = 80;
                if (MaxCondition == 600 || MaxCondition == 800) minPrice = 120;

                delve.Add($"Cannot be used for items under {minPrice}g.");
                delve.Add(" ");
            }
            else if (Flags == 47)
            {
                delve.Add(" ");
                delve.Add("House Loan Coupon");
                delve.Add(LanguageMgr.GetTranslation(lang, "DelveInfo.Value", MaxCondition) + " Gold");
                delve.Add("Hand this to a housing merchant or lot marker when short on funds to cash it.");

                long minPrice = 900;
                if (MaxCondition == 3000 || MaxCondition == 6000) minPrice = 1000;
                else if (MaxCondition == 10000 || MaxCondition == 25000) minPrice = 4000;

                delve.Add($"Only usable for housing purchases of {minPrice}g or more.");
                delve.Add(" ");
            }

            //**********************************
            //special mana/cond consuming items
            //**********************************

            int flags = Template != null ? Template.Flags : Flags;

            if (flags >= 43 && flags <= 45)
            {
                string pkg = PackageID ?? Template?.PackageID ?? "";
                double manaPct = 0;
                int condLoss = 0;
                int deathCondLoss = 0;
                bool destroyOnMana = false, destroyOnCond = false, destroyOnDeath = false;
                bool hasPassword = false;
                bool stopHealRegen = false;
                int spellId = 0;
                int effectId = 0;

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
                        else if (parts[0] == "DEATHCOND" && parts.Length >= 2)
                        {
                            int.TryParse(parts[1], out deathCondLoss);
                            if (parts.Length >= 3 && parts[2] == "DESTROY") destroyOnDeath = true;
                        }
                        else if (parts[0] == "PASSWORD")
                        {
                            hasPassword = true;
                        }
                        else if (parts[0] == "STOPHEALREGEN")
                        {
                            stopHealRegen = true;
                        }
                        else if (parts[0] == "SPELL")
                        {
                            if (parts.Length >= 2)
                                int.TryParse(parts[1], out spellId);
                            else
                                spellId = SpellID;
                        }
                        else if (parts[0] == "EFFECT" && parts.Length >= 2)
                        {
                            int.TryParse(parts[1], out effectId);
                        }
                    }
                }

                if (flags == 44 && deathCondLoss == 0 && condLoss > 0)
                {
                    deathCondLoss = condLoss;
                    destroyOnDeath = destroyOnCond;
                }

                if (manaPct > 0 && (flags == 43 || flags == 45))
                {
                    string destroyEmpty = destroyOnMana ? (" " + LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.UndeequippableItem.DestroysEmpty")) : "";

                    if (player.CharacterClass != null && GamePlayer.IsPureMeleeClass((eCharacterClass)player.CharacterClass.ID))
                    {
                        delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.UndeequippableItem.ConsumesEnduHealth", (manaPct * 2.1).ToString("0.##"), (manaPct * 0.4).ToString("0.##"), destroyEmpty));
                    }
                    else
                    {
                        delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.UndeequippableItem.ConsumesMana", manaPct.ToString("0.##"), destroyEmpty));
                    }
                }

                int maxCond = MaxCondition > 0 ? MaxCondition : 50000;

                if (condLoss > 0 && (flags == 43 || flags == 45))
                {
                    double condPct = (condLoss / (double)maxCond) * 100.0;
                    string destroyBrokenCond = destroyOnCond ? (" " + LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.UndeequippableItem.DestroysBroken")) : "";
                    delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.UndeequippableItem.ConsumesCondition", condPct.ToString("0.##"), destroyBrokenCond));
                }

                if (deathCondLoss > 0 && (flags == 44 || flags == 45))
                {
                    double deathCondPct = (deathCondLoss / (double)maxCond) * 100.0;
                    string destroyBrokenDeath = destroyOnDeath ? (" " + LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.UndeequippableItem.DestroysBroken")) : "";
                    delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.UndeequippableItem.LosesConditionDeath", deathCondPct.ToString("0.##"), destroyBrokenDeath));
                }

                if (hasPassword)
                {
                    delve.Add("- Requires a spoken formula to activate the curse and effects.");
                }

                if (stopHealRegen)
                {
                    delve.Add("- Halts all health regeneration while equipped.");
                }

                if (spellId > 0 && !hasPassword)
                {
                    delve.Add($"- Periodically casts a specific Spell.");
                }

                if (effectId > 0 && !hasPassword)
                {
                    delve.Add($"- Periodically applies a visual aura.");
                }

                if (manaPct > 0 || condLoss > 0 || deathCondLoss > 0 || hasPassword || stopHealRegen || spellId > 0 || effectId > 0) delve.Add(" ");
            }

            if (!IsDropable || !IsPickable || !IsTradable || IsIndestructible || !CanUseInRvR || Flags == 2 || (Flags == 1 && IsDropable))
                delve.Add(" ");

            if (!IsPickable)
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.CannotPick"));

            if (!IsTradable)
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.CannotTraded"));

            if (!IsDropable)
            {
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.GetShortItemInfo.NoDrop"));
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.CannotSold"));
            }

            if (IsIndestructible)
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.CannotDestroyed"));

            if (!CanUseInRvR)
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.CannotBeUsedInRvR"));

            if (Flags == 1 && IsDropable)
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.CannotSold"));

            if (Flags == 44 || Flags == 45)
            {
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.UndeequippableItem.CannotUnequip"));
            }

            if (Flags == 2)
            {
                delve.Add(" ");
                delve.Add(LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.HandlePacket.EffectWhenSitting"));
            }

            if (this.ClassType.Contains("DOL.GS.AfkXpToken"))
            {
                var remaininguse = Condition * 100 / MaxCondition;
                delve.Add(" ");
                delve.Add(LanguageMgr.GetTranslation(lang, "DelveInfo.RemainingUse", remaininguse) + "%");
            }

            if (this.ClassType.Contains("DOL.GS.PvPTreasure"))
            {
                delve.Add(" ");
                delve.Add(LanguageMgr.GetTranslation(lang, "DelveInfo.Value", (int)(Condition / 4.0)) + " " + LanguageMgr.GetTranslation(lang, "DetailDisplayHandler.WriteBonusLine.Points"));
            }

            //Add admin info
            if (player.Client.Account.PrivLevel > 1)
            {
                WriteTechnicalInfo(delve, player.Client);
            }
        }

        protected virtual void WriteUsableClasses(IList<string> output, GameClient client)
        {
            if (Util.IsEmpty(AllowedClasses, true))
                return;

            output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteUsableClasses.UsableBy"));

            foreach (string allowed in Util.SplitCSV(AllowedClasses, true))
            {
                int classID = -1;
                if (int.TryParse(allowed, out classID))
                {
                    output.Add("- " + ((eCharacterClass)classID).ToString());
                }
                else
                {
                    log.Error(Id_nb + " has an invalid entry for allowed classes '" + allowed + "'");
                }
            }
        }


        protected virtual void WriteMagicalBonuses(IList<string> output, GameClient client, bool shortInfo, bool isGenistar = false)
        {
            if (!isGenistar)
            {
                if (this is GameInventoryItem gameItemUtility)
                {
                    var utility = gameItemUtility.GetTotalUtility();
                    if (Math.Abs(utility) >= 0.01)
                    {
                        output.Add("Total utility: " + String.Format("{0:0.00}", utility));
                        output.Add(" ");
                    }
                }
            }

            int oldCount = output.Count;

            WriteBonusLine(output, client, Bonus1Type, Bonus1, isGenistar);
            WriteBonusLine(output, client, Bonus2Type, Bonus2, isGenistar);
            WriteBonusLine(output, client, Bonus3Type, Bonus3, isGenistar);
            WriteBonusLine(output, client, Bonus4Type, Bonus4, isGenistar);
            WriteBonusLine(output, client, Bonus5Type, Bonus5, isGenistar);
            WriteBonusLine(output, client, Bonus6Type, Bonus6, isGenistar);
            WriteBonusLine(output, client, Bonus7Type, Bonus7, isGenistar);
            WriteBonusLine(output, client, Bonus8Type, Bonus8, isGenistar);
            WriteBonusLine(output, client, Bonus9Type, Bonus9, isGenistar);
            WriteBonusLine(output, client, Bonus10Type, Bonus10, isGenistar);
            WriteBonusLine(output, client, ExtraBonusType, ExtraBonus, isGenistar);

            if (output.Count > oldCount)
            {
                output.Add(" ");
            }

            /* BONUS REQUIREMENTS */
            if (this.BonusConditions?.Any(b => b.ChampionLevel > 0 || b.MlLevel > 0 || b.IsRenaissanceRequired) == true)
            {
                bool printedTitle = false;

                foreach (var condition in this.BonusConditions.Where(b => b.BonusName != nameof(this.ProcSpellID) && b.BonusName != nameof(this.ProcSpellID1)).OrderBy(b => b.BonusName))
                {
                    string propName = this.GetBonusTypeFromBonusName(client, condition.BonusName, isGenistar);
                    
                    // Skip if the property is undefined (empty slot on the item)
                    if (string.IsNullOrEmpty(propName)) continue; 

                    List<string> conditions = new();
                    if (condition.ChampionLevel > 0)
                        conditions.Add("Champion Level: " + condition.ChampionLevel);
                    if (condition.MlLevel > 0)
                        conditions.Add("ML Level: " + condition.MlLevel);
                    if (condition.IsRenaissanceRequired)
                        conditions.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteBonusConditions.Renaissance"));
                    
                    if (conditions.Count > 0)
                    {
                        if (!printedTitle)
                        {
                            output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteBonusConditions.Title"));
                            printedTitle = true;
                        }
                        output.Add(" - " + propName + ": " + String.Join(" | ", conditions));
                    }
                }
                
                if (printedTitle) output.Add(" ");
            }

            if (output.Count > oldCount)
            {
                output.Insert(oldCount, LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.MagicBonus"));
                output.Insert(oldCount, " ");
            }

            oldCount = output.Count;

            WriteFocusLine(client, output, Bonus1Type, Bonus1, isGenistar);
            WriteFocusLine(client, output, Bonus2Type, Bonus2, isGenistar);
            WriteFocusLine(client, output, Bonus3Type, Bonus3, isGenistar);
            WriteFocusLine(client, output, Bonus4Type, Bonus4, isGenistar);
            WriteFocusLine(client, output, Bonus5Type, Bonus5, isGenistar);
            WriteFocusLine(client, output, Bonus6Type, Bonus6, isGenistar);
            WriteFocusLine(client, output, Bonus7Type, Bonus7, isGenistar);
            WriteFocusLine(client, output, Bonus8Type, Bonus8, isGenistar);
            WriteFocusLine(client, output, Bonus9Type, Bonus9, isGenistar);
            WriteFocusLine(client, output, Bonus10Type, Bonus10, isGenistar);
            WriteFocusLine(client, output, ExtraBonusType, ExtraBonus, isGenistar);

            if (output.Count > oldCount)
            {
                output.Add(" ");
                output.Insert(oldCount, LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.FocusBonus"));
                output.Insert(oldCount, " ");
            }

            if (!shortInfo)
            {
                if (BonusLevel > 0)
                {
                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.HandlePacket.BonusLevel", BonusLevel));
                }

                if (ProcSpellID != 0 || ProcSpellID1 != 0 || SpellID != 0 || SpellID1 != 0)
                {
                    int requiredLevel = LevelRequirement > 0 ? LevelRequirement : Math.Min(50, Level);
                    if (requiredLevel > 1)
                    {
                        output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.LevelRequired2", requiredLevel));
                        output.Add(" ");
                    }
                }

                if (this.IsPotion()) // potion
                {
                    // let WritePotion handle the rest of the display
                    return;
                }


                #region Proc1
                if (ProcSpellID != 0)
                {
                    string spellNote = "";
                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.MagicAbility"));
                    if (GlobalConstants.IsWeapon(Object_Type))
                    {
                        spellNote = LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.StrikeEnemy");
                    }
                    else if (GlobalConstants.IsArmor(Object_Type))
                    {
                        spellNote = LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.StrikeArmor");
                    }

                    SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);
                    if (line != null)
                    {
                        Spell procSpell = SkillBase.FindSpell(ProcSpellID, line);

                        if (procSpell != null)
                        {
                            ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(client.Player, procSpell, line);
                            if (spellHandler != null)
                            {
                                Util.AddRange(output, spellHandler.DelveInfo);
                                output.Add(" ");
                            }
                            else
                            {
                                output.Add("-" + procSpell.Name + " (Spell Handler Not Implemented)");
                            }

                            output.Add(spellNote);
                        }
                        else
                        {
                            output.Add("- Spell Not Found: " + ProcSpellID);
                        }
                    }
                    else
                    {
                        output.Add("- Item_Effects Spell Line Missing");
                    }

                    output.Add(" ");

                    if (this.BonusConditions != null)
                    {
                        var procCondition = this.BonusConditions.FirstOrDefault(b => b.BonusName.Equals(nameof(this.ProcSpellID)));

                        if (procCondition != null)
                        {
                            List<string> conditions = new();
                            if (procCondition.ChampionLevel > 0)
                                conditions.Add("Champion Level: " + procCondition.ChampionLevel);
                            if (procCondition.MlLevel > 0)
                                conditions.Add("ML Level: " + procCondition.MlLevel);
                            if (procCondition.IsRenaissanceRequired)
                                conditions.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteBonusConditions.Renaissance"));
                            if (conditions.Count > 0)
                            {
                                output.Add("Spell Proc Conditions: ");
                                output.Add(" - " + this.GetBonusTypeFromBonusName(client, procCondition.BonusName, isGenistar) + ": " + String.Join(" | ", conditions));
                                output.Add(" ");
                            }
                        }
                    }
                }
                #endregion
                #region Proc2
                if (ProcSpellID1 != 0)
                {
                    string spellNote = "";
                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.MagicAbility"));
                    if (GlobalConstants.IsWeapon(Object_Type))
                    {
                        spellNote = LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.StrikeEnemy");
                    }
                    else if (GlobalConstants.IsArmor(Object_Type))
                    {
                        spellNote = LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.StrikeArmor");
                    }

                    SpellLine line = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);
                    if (line != null)
                    {
                        Spell procSpell = SkillBase.FindSpell(ProcSpellID1, line);

                        if (procSpell != null)
                        {
                            ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(client.Player, procSpell, line);
                            if (spellHandler != null)
                            {
                                Util.AddRange(output, spellHandler.DelveInfo);
                                output.Add(" ");
                            }
                            else
                            {
                                output.Add("-" + procSpell.Name + " (Spell Handler Not Implemented)");
                            }

                            output.Add(spellNote);
                        }
                        else
                        {
                            output.Add("- Spell Not Found: " + ProcSpellID1);
                        }
                    }
                    else
                    {
                        output.Add("- Item_Effects Spell Line Missing");
                    }

                    output.Add(" ");

                    if (this.BonusConditions != null)
                    {
                        var procCondition = this.BonusConditions.FirstOrDefault(b => b.BonusName.Equals(nameof(this.ProcSpellID1)));

                        if (procCondition != null)
                        {
                            List<string> conditions = new();
                            if (procCondition.ChampionLevel > 0)
                                conditions.Add("Champion Level: " + procCondition.ChampionLevel);
                            if (procCondition.MlLevel > 0)
                                conditions.Add("ML Level: " + procCondition.MlLevel);
                            if (procCondition.IsRenaissanceRequired)
                                conditions.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteBonusConditions.Renaissance"));
                            if (conditions.Count > 0)
                            {
                                output.Add("Spell Proc 2 Conditions: ");
                                output.Add(" - " + this.GetBonusTypeFromBonusName(client, procCondition.BonusName, isGenistar) + ": " + String.Join(" | ", conditions));
                                output.Add(" ");
                            }
                        }
                    }
                }
                #endregion
                #region Charge1
                if (SpellID != 0)
                {
                    if (SpellID1 != 0)
                        output.Add(LanguageMgr.GetTranslation(client.Account.Language, "Language.Systems.Spell") + " 1:");
                    SpellLine chargeEffectsLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);
                    if (chargeEffectsLine != null)
                    {
                        Spell spell = SkillBase.FindSpell(SpellID, chargeEffectsLine);
                        if (spell != null)
                        {
                            ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(client.Player, spell, chargeEffectsLine);

                            if (spellHandler != null)
                            {
                                if (MaxCharges > 0)
                                {
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.ChargedMagic"));
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.Charges", Charges));
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.MaxCharges", MaxCharges));
                                    output.Add(" ");
                                }

                                Util.AddRange(output, spellHandler.DelveInfo);
                                output.Add(" ");
                                output.Add("- This spell is cast when the item is used.");
                            }
                            else
                            {
                                output.Add("- Item_Effects Spell Line Missing");
                            }
                        }
                        else
                        {
                            output.Add("- Spell Not Found: " + SpellID);
                        }
                    }

                    output.Add(" ");
                }
                #endregion
                #region Charge2
                if (SpellID1 != 0)
                {
                    if (SpellID != 0)
                        output.Add(LanguageMgr.GetTranslation(client.Account.Language, "Language.Systems.Spell") + " 2:");
                    SpellLine chargeEffectsLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);
                    if (chargeEffectsLine != null)
                    {
                        Spell spell = SkillBase.FindSpell(SpellID1, chargeEffectsLine);
                        if (spell != null)
                        {
                            ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(client.Player, spell, chargeEffectsLine);

                            if (spellHandler != null)
                            {
                                if (MaxCharges > 0)
                                {
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.ChargedMagic"));
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.Charges", Charges1));
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.MaxCharges", MaxCharges1));
                                    output.Add(" ");
                                }

                                Util.AddRange(output, spellHandler.DelveInfo);
                                output.Add(" ");
                                output.Add("- This spell is cast when the item is used.");
                            }
                            else
                            {
                                output.Add("- Item_Effects Spell Line Missing");
                            }
                        }
                        else
                        {
                            output.Add("- Spell Not Found: " + SpellID1);
                        }
                    }

                    output.Add(" ");
                }
                #endregion
                #region Poison
                if (PoisonSpellID != 0)
                {
                    if (GlobalConstants.IsWeapon(Object_Type) || (eObjectType)Object_Type == eObjectType.Poison)// Poisoned Weapon
                    {
                        SpellLine poisonLine = SkillBase.GetSpellLine(GlobalSpellsLines.Mundane_Poisons);
                        if (poisonLine != null)
                        {
                            List<Spell> spells = SkillBase.GetSpellList(poisonLine.KeyName);
                            foreach (Spell spl in spells)
                            {
                                if (spl.ID == PoisonSpellID)
                                {
                                    output.Add(" ");
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.LevelRequired"));
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.Level", spl.Level));
                                    output.Add(" ");
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.ChargedMagic"));
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.Charges", PoisonCharges));
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.MaxCharges", PoisonMaxCharges));
                                    output.Add(" ");

                                    ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(client.Player, spl, poisonLine);
                                    if (spellHandler != null)
                                    {
                                        Util.AddRange(output, spellHandler.DelveInfo);
                                        output.Add(" ");
                                    }
                                    else
                                    {
                                        output.Add("-" + spl.Name + "(Not implemented yet)");
                                    }
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.StrikeEnemy"));
                                    return;
                                }
                            }
                        }
                    }

                    SpellLine chargeEffectsLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);
                    if (chargeEffectsLine != null)
                    {
                        List<Spell> spells = SkillBase.GetSpellList(chargeEffectsLine.KeyName);
                        foreach (Spell spl in spells)
                        {
                            if (spl.ID == SpellID)
                            {
                                output.Add(" ");
                                output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.LevelRequired"));
                                output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.Level", spl.Level));
                                output.Add(" ");
                                if (MaxCharges > 0)
                                {
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.ChargedMagic"));
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.Charges", Charges));
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.MaxCharges", MaxCharges));
                                }
                                else
                                {
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.MagicAbility"));
                                }
                                output.Add(" ");

                                ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(client.Player, spl, chargeEffectsLine);
                                if (spellHandler != null)
                                {
                                    Util.AddRange(output, spellHandler.DelveInfo);
                                    output.Add(" ");
                                }
                                else
                                {
                                    output.Add("-" + spl.Name + "(Not implemented yet)");
                                }
                                output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.UsedItem"));
                                output.Add(" ");
                                if (spl.RecastDelay > 0)
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.UseItem1", Util.FormatTime(spl.RecastDelay / 1000)));
                                else
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.UseItem2"));
                                long lastChargedItemUseTick = client.Player.TempProperties.getProperty<long>(GamePlayer.LAST_CHARGED_ITEM_USE_TICK);
                                long changeTime = client.Player.CurrentRegion.Time - lastChargedItemUseTick;
                                long recastDelay = (spl.RecastDelay > 0) ? spl.RecastDelay : 60000 * 3;
                                if (changeTime < recastDelay) //3 minutes reuse timer
                                    output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.UseItem3", Util.FormatTime((recastDelay - changeTime) / 1000)));
                                return;
                            }
                        }
                    }
                }
                #endregion
            }
        }

        public double GetTotalUtility()
        {
            return ItemUtilityCalculator.GetTotalUtility(this);
        }

        public static double GetSingleUtility(int bonusType, int bonus)
        {
            return ItemUtilityCalculator.GetSingleUtility(bonusType, bonus);
        }

        private string GetBonusTypeFromBonusName(GameClient client, string bonusName, bool isGenistar = false)
        {
            switch (bonusName)
            {
                case nameof(Bonus1):
                    return Bonus1Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus1Type, isGenistar) : string.Empty;
                case nameof(Bonus2):
                    return Bonus2Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus2Type, isGenistar) : string.Empty;
                case nameof(Bonus3):
                    return Bonus3Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus3Type, isGenistar) : string.Empty;
                case nameof(Bonus4):
                    return Bonus4Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus4Type, isGenistar) : string.Empty;
                case nameof(Bonus5):
                    return Bonus5Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus5Type, isGenistar) : string.Empty;
                case nameof(Bonus6):
                    return Bonus6Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus6Type, isGenistar) : string.Empty;
                case nameof(Bonus7):
                    return Bonus7Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus7Type, isGenistar) : string.Empty;
                case nameof(Bonus8):
                    return Bonus8Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus8Type, isGenistar) : string.Empty;
                case nameof(Bonus9):
                    return Bonus9Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus9Type, isGenistar) : string.Empty;
                case nameof(Bonus10):
                    return Bonus10Type > 0 ? SkillBase.GetPropertyName(client, (eProperty)Bonus10Type, isGenistar) : string.Empty;
                case nameof(ExtraBonus):
                    return ExtraBonusType > 0 ? SkillBase.GetPropertyName(client, (eProperty)ExtraBonusType, isGenistar) : string.Empty;

                case nameof(ProcSpellID):
                    return ProcSpellID > 0 ? (SkillBase.GetSpellByID(ProcSpellID)?.Name ?? "(Invalid Spell)") : string.Empty;
                case nameof(ProcSpellID1):
                    return ProcSpellID1 > 0 ? (SkillBase.GetSpellByID(ProcSpellID1)?.Name ?? "(Invalid Spell)") : string.Empty;
                default:
                    return string.Empty;
            }
        }

        protected virtual void WriteBonusLine(IList<string> list, GameClient client, int bonusCat, int bonusValue, bool isGenistar = false)
        {
            if (bonusCat == 0 || bonusValue == 0) return;
            if (SkillBase.CheckPropertyType((eProperty)bonusCat, ePropertyType.Focus))
                return;

            //- Axe: 5 pts
            //- Strength: 15 pts
            //- Constitution: 15 pts
            //- Hits: 40 pts
            //- Fatigue: 8 pts
            //- Heat: 7%
            //Bonus to casting speed: 2%
            //Bonus to armor factor (AF): 18
            //Power: 6 % of power pool.
            string bonusValueStr = bonusValue.ToString("0 ;-0;0 ");
            string propertyName = SkillBase.GetPropertyName(client, (eProperty)bonusCat, isGenistar);
            if (string.IsNullOrEmpty(propertyName))
                propertyName = "UnknownProperty";

            string formattedLine = "";

            if (!isGenistar)
            {
                // Build the line e.g. "2.50 | Strength: 15"
                string singleUti = String.Format("{0:0.00}", GetSingleUtility(bonusCat, bonusValue));
                formattedLine = $"{singleUti} | {propertyName}: {bonusValueStr}";
            }
            else
            {
                formattedLine = $"- {propertyName}: {bonusValueStr}";
            }

            // If it's a % type, add a '%' at the end
            if (bonusCat == (int)eProperty.PowerPool
                || (bonusCat >= (int)eProperty.Resist_First && bonusCat <= (int)eProperty.Resist_Last)
                || (bonusCat >= (int)eProperty.ResCapBonus_First && bonusCat <= (int)eProperty.ResCapBonus_Last)
                || bonusCat == (int)eProperty.CraftingSkillGain
                || bonusCat == (int)eProperty.SpellShieldChance
                || bonusCat == (int)eProperty.WeaponSkill
                || bonusCat == (int)eProperty.RobberyResist
                || bonusCat == (int)eProperty.MythicalCoin
                || (bonusCat >= 116 && bonusCat <= 119)
                || (bonusCat >= 146 && bonusCat <= 147)
                || (bonusCat >= 149 && bonusCat <= 155)
                || (bonusCat >= 169 && bonusCat <= 186)
                || (bonusCat >= 188 && bonusCat <= 199)
                || (bonusCat >= 232 && bonusCat <= 233)
                || (bonusCat >= 245 && bonusCat <= 269))
            {
                formattedLine += "%";
            }
            else
            {
                formattedLine += LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteBonusLine.Points");
            }

            // A) If player's level < item.BonusLevel => entire item is inactive
            bool itemAllInactive = false;
            if (client.Player != null && client.Player.Level < this.BonusLevel)
            {
                itemAllInactive = true;
            }

            // B) Check per-bonus condition
            string bonusName = MatchBonusName(bonusCat, bonusValue);

            bool passBonusCondition = IsBonusConditionMet(bonusName, client.Player);

            if (itemAllInactive || !passBonusCondition)
            {
                formattedLine += " " + LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.Inactive");
            }

            list.Add(formattedLine);
        }

        private string MatchBonusName(int bonusCat, int bonusValue)
        {
            if (this.Bonus1Type == bonusCat && this.Bonus1 == bonusValue)
                return nameof(this.Bonus1);
            if (this.Bonus2Type == bonusCat && this.Bonus2 == bonusValue)
                return nameof(this.Bonus2);
            if (this.Bonus3Type == bonusCat && this.Bonus3 == bonusValue)
                return nameof(this.Bonus3);
            if (this.Bonus4Type == bonusCat && this.Bonus4 == bonusValue)
                return nameof(this.Bonus4);
            if (this.Bonus5Type == bonusCat && this.Bonus5 == bonusValue)
                return nameof(this.Bonus5);
            if (this.Bonus6Type == bonusCat && this.Bonus6 == bonusValue)
                return nameof(this.Bonus6);
            if (this.Bonus7Type == bonusCat && this.Bonus7 == bonusValue)
                return nameof(this.Bonus7);
            if (this.Bonus8Type == bonusCat && this.Bonus8 == bonusValue)
                return nameof(this.Bonus8);
            if (this.Bonus9Type == bonusCat && this.Bonus9 == bonusValue)
                return nameof(this.Bonus9);
            if (this.Bonus10Type == bonusCat && this.Bonus10 == bonusValue)
                return nameof(this.Bonus10);
            if (this.ExtraBonusType == bonusCat && this.ExtraBonus == bonusValue)
                return nameof(this.ExtraBonus);

            return "";
        }

        protected virtual void WriteFocusLine(GameClient client, IList<string> list, int focusCat, int focusLevel, bool isGenistar = false)
        {
            if (!SkillBase.CheckPropertyType((eProperty)focusCat, ePropertyType.Focus))
                return;
            if (focusLevel <= 0)
                return;

            string propertyName = SkillBase.GetPropertyName(client, (eProperty)focusCat, isGenistar);
            if (string.IsNullOrEmpty(propertyName))
                propertyName = "UnknownFocus";

            string line = $"- {propertyName}: {focusLevel} lvls";

            bool itemAllInactive = false;
            if (client.Player != null && client.Player.Level < this.BonusLevel)
            {
                itemAllInactive = true;
            }

            string bonusName = MatchBonusName(focusCat, focusLevel);

            bool passBonusCondition = IsBonusConditionMet(bonusName, client.Player);

            if (itemAllInactive || !passBonusCondition)
            {
                line += " " + LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.Inactive");
            }

            list.Add(line);
        }

        /// <summary>
        /// Checks if the player meets the custom bonus conditions for a given bonusName
        /// (ChampionLevel, MLlevel, IsRenaissance).
        /// </summary>
        private bool IsBonusConditionMet(string bonusName, GamePlayer player)
        {
            var cond = this.BonusConditions?.FirstOrDefault(
                b => b.BonusName.Equals(bonusName, StringComparison.InvariantCultureIgnoreCase));

            if (cond == null)
                return true;

            // If item requires ChampionLevel or MLlevel or Renaissance
            if (cond.ChampionLevel > 0 && player.ChampionLevel < cond.ChampionLevel)
                return false;
            if (cond.MlLevel > 0 && player.MLLevel < cond.MlLevel)
                return false;
            if (cond.IsRenaissanceRequired && !player.IsRenaissance)
                return false;

            return true;
        }

        protected virtual bool IsPvEBonus(eProperty property)
        {
            switch (property)
            {
                case eProperty.DefensiveBonus:
                case eProperty.BladeturnReinforcement:
                case eProperty.NegativeReduction:
                case eProperty.PieceAblative:
                case eProperty.ReactionaryStyleDamage:
                case eProperty.SpellPowerCost:
                case eProperty.StyleCostReduction:
                case eProperty.ToHitBonus:
                    return true;

                default:
                    return false;
            }
        }


        protected virtual void WritePoisonInfo(IList<string> list, GameClient client)
        {
            if (PoisonSpellID != 0)
            {
                SpellLine poisonLine = SkillBase.GetSpellLine(GlobalSpellsLines.Mundane_Poisons);
                if (poisonLine != null)
                {
                    List<Spell> spells = SkillBase.GetSpellList(poisonLine.KeyName);

                    foreach (Spell spl in spells)
                    {
                        if (spl.ID == PoisonSpellID)
                        {
                            list.Add(" ");
                            list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePoisonInfo.LevelRequired"));
                            list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePoisonInfo.Level", spl.Level));
                            list.Add(" ");
                            list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePoisonInfo.ProcAbility"));
                            list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePoisonInfo.Charges", PoisonCharges));
                            list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePoisonInfo.MaxCharges", PoisonMaxCharges));
                            list.Add(" ");

                            ISpellHandler spellHandler = ScriptMgr.CreateSpellHandler(client.Player, spl, poisonLine);
                            if (spellHandler != null)
                            {
                                Util.AddRange(list, spellHandler.DelveInfo);
                            }
                            else
                            {
                                list.Add("-" + spl.Name + " (Not implemented yet)");
                            }
                            break;
                        }
                    }
                }
            }
        }
        
        void WriteSpellInfo(IList<string> list, GameClient client, Spell spl, SpellLine line, int charges, int maxCharges)
        {
            if (!this.ClassType.Contains("DOL.GS.DieTriggerSpell"))
            {
                list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePotionInfo.ChargedMagic"));
                list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePotionInfo.Charges", charges));
                list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePotionInfo.MaxCharges", maxCharges));
            }

            list.Add(" ");
            WritePotionSpellsInfos(list, client, spl, line);
            list.Add(" ");
            long nextPotionAvailTime = client.Player.TempProperties.getProperty<long>("LastPotionItemUsedTick_Type" + spl.SharedTimerGroup);
            // Satyr Update: Individual Reuse-Timers for Pots need a Time looking forward
            // into Future, set with value of "itemtemplate.CanUseEvery" and no longer back into past
            if (!this.ClassType.Contains("DOL.GS.DieTriggerSpell"))
            {
                if (nextPotionAvailTime > client.Player.CurrentRegion.Time)
                {
                    list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePotionInfo.UseItem3", Util.FormatTime((nextPotionAvailTime - client.Player.CurrentRegion.Time) / 1000)));
                }
                else
                {
                    int minutes = CanUseEvery / 60;
                    int seconds = CanUseEvery % 60;

                    if (minutes == 0)
                    {
                        list.Add(String.Format("Can use item every: {0} sec", seconds));
                    }
                    else
                    {
                        list.Add(String.Format("Can use item every: {0}:{1:00} min", minutes, seconds));
                    }
                }
            }

            if (spl.CastTime > 0)
            {
                list.Add(" ");
                list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePotionInfo.NoUseInCombat"));
            }

            if (this.ClassType.Contains("DOL.GS.DieTriggerSpell"))
            {
                list.Add(" LevelRequired: " + LevelRequirement);
            }
        }


        protected virtual void WritePotionInfo(IList<string> list, GameClient client)
        {
            if (SpellID == 0 && SpellID1 == 0)
                return;
            
            SpellLine potionLine = SkillBase.GetSpellLine(this.IsPotion() ? GlobalSpellsLines.Potions_Effects : GlobalSpellsLines.Item_Effects);
            if (potionLine == null)
                return;

            Spell? spell0 = SpellID == 0 ? null : SkillBase.GetSpellByID(SpellID);
            Spell? spell1 = SpellID == 1 ? null : SkillBase.GetSpellByID(SpellID1);
            if (spell0 != null && spell1 != null)
            {
                list.Add(LanguageMgr.GetTranslation(client.Account.Language, "Language.Systems.Spell") + " 1:");
                WriteSpellInfo(list, client, spell0, potionLine, Charges, MaxCharges);
                list.Add(" ");
                list.Add(LanguageMgr.GetTranslation(client.Account.Language, "Language.Systems.Spell") + " 2:");
                WriteSpellInfo(list, client, spell1, potionLine, Charges, MaxCharges);
            }
            else
            {
                if (spell0 != null)
                    WriteSpellInfo(list, client, spell0, potionLine, Charges, MaxCharges);
                if (spell1 != null)
                    WriteSpellInfo(list, client, spell1, potionLine, Charges, MaxCharges);
            }
        }


        protected static void WritePotionSpellsInfos(IList<string> list, GameClient client, Spell spl, NamedSkill line)
        {
            if (spl != null)
            {
                list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteMagicalBonuses.MagicAbility"));
                list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePotionInfo.Type", spl.SpellType));
                list.Add(" ");
                list.Add(spl.Description);
                list.Add(" ");
                if (spl.Value != 0)
                {
                    list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePotionInfo.Value", spl.Value));
                }
                list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePotionInfo.Target", spl.Target));
                if (spl.Range > 0)
                {
                    list.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WritePotionInfo.Range", spl.Range));
                }
                list.Add(" ");
                list.Add(" ");
                if (spl.SubSpellID > 0)
                {
                    List<Spell> spells = SkillBase.GetSpellList(line.KeyName);
                    foreach (Spell subSpell in spells)
                    {
                        if (subSpell.ID == spl.SubSpellID)
                        {
                            WritePotionSpellsInfos(list, client, subSpell, line);
                            break;
                        }
                    }
                }
            }
        }


        protected virtual void DelveShieldStats(IList<string> output, GameClient client)
        {
            double itemDPS = DPS_AF / 10.0;
            double clampedDPS = Math.Min(itemDPS, 1.2 + 0.3 * client.Player.Level);
            double itemSPD = SPD_ABS / 10.0;

            output.Add(" ");
            output.Add(" ");
            output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteClassicShieldInfos.DamageMod"));
            if (itemDPS != 0)
            {
                output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteClassicShieldInfos.BaseDPS", itemDPS.ToString("0.0")));
                output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteClassicShieldInfos.ClampDPS", clampedDPS.ToString("0.0")));
            }
            if (SPD_ABS >= 0)
            {
                output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteClassicShieldInfos.SPD", itemSPD.ToString("0.0")));
            }

            output.Add(" ");

            switch (Type_Damage)
            {
                case 1: output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteClassicShieldInfos.Small")); break;
                case 2: output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteClassicShieldInfos.Medium")); break;
                case 3: output.Add(LanguageMgr.GetTranslation(client.Account.Language, "DetailDisplayHandler.WriteClassicShieldInfos.Large")); break;
            }
        }


        protected virtual void DelveWeaponStats(List<String> delve, GamePlayer player)
        {
            double itemDPS = DPS_AF / 10.0;
            double clampedDPS = Math.Min(itemDPS, 1.2 + 0.3 * player.Level);
            double itemSPD = SPD_ABS / 10.0;
            double effectiveDPS = clampedDPS * Quality / 100.0 * Condition / MaxCondition;

            delve.Add(" ");
            delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicWeaponInfos.DamageMod"));

            if (itemDPS != 0)
            {
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicWeaponInfos.BaseDPS", itemDPS.ToString("0.0")));
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicWeaponInfos.ClampDPS", clampedDPS.ToString("0.0")));
            }

            if (SPD_ABS >= 0)
            {
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicWeaponInfos.SPD", itemSPD.ToString("0.0")));
            }

            if (Quality != 0)
            {
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicWeaponInfos.Quality", Quality));
            }

            if (Condition != 0)
            {
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicWeaponInfos.Condition", ConditionPercent));
            }

            delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language,
                                                 "DetailDisplayHandler.WriteClassicWeaponInfos.DamageType",
                                                 (Type_Damage == 0 ? "None" : GlobalConstants.WeaponDamageTypeToName(Type_Damage))));

            delve.Add(" ");

            delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicWeaponInfos.EffDamage"));

            if (itemDPS != 0)
            {
                delve.Add("- " + effectiveDPS.ToString("0.0") + " DPS");
            }
        }

        protected virtual void DelveArmorStats(List<String> delve, GamePlayer player)
        {
            delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicArmorInfos.ArmorMod"));

            double af = 0;
            int afCap = player.Level + (player.RealmLevel > 39 ? 1 : 0);
            double effectiveAF = 0;

            if (DPS_AF != 0)
            {
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicArmorInfos.BaseFactor", DPS_AF));

                if (Object_Type != (int)eObjectType.Cloth)
                {
                    afCap *= 2;
                }

                af = Math.Min(afCap, DPS_AF);

                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicArmorInfos.ClampFact", (int)af));
            }

            if (SPD_ABS >= 0)
            {
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicArmorInfos.Absorption", SPD_ABS));
            }

            if (Quality != 0)
            {
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicArmorInfos.Quality", Quality));
            }

            if (Condition != 0)
            {
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicArmorInfos.Condition", ConditionPercent));
            }

            delve.Add(" ");
            delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicArmorInfos.EffArmor"));

            if (DPS_AF != 0)
            {
                effectiveAF = af * Quality / 100.0 * Condition / MaxCondition * (1 + SPD_ABS / 100.0);
                delve.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "DetailDisplayHandler.WriteClassicArmorInfos.Factor", (int)effectiveAF));
            }
        }

        public string GetTranslatedName(GamePlayer player)
        {
            string lang = player?.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            return LanguageMgr.GetItemNameMessage(lang, Template?.Name ?? Name);
        }

        public string GetTranslatedDescription(GamePlayer player)
        {
            string lang = player?.Client?.Account?.Language ?? LanguageMgr.DefaultLanguage;
            return LanguageMgr.GetItemDescMessage(lang, Template?.Description ?? Description);
        }

        /// <summary>
        /// Write item technical info
        /// </summary>
        /// <param name="output"></param>
        /// <param name="item"></param>
        public virtual void WriteTechnicalInfo(List<String> delve, GameClient client)
        {
            delve.Add("");
            delve.Add("--- Technical Information ---");
            delve.Add("");

            if (Template is ItemUnique)
            {
                delve.Add("  Item Unique: " + Id_nb);
            }
            else
            {
                delve.Add("Item Template: " + Id_nb);
                delve.Add("Allow Updates: " + (Template as ItemTemplate).AllowUpdate);
            }

            delve.Add("");

            delve.Add("         Name: " + Name);
            delve.Add("    ClassType: " + this.GetType().FullName);
            delve.Add("");
            delve.Add(" SlotPosition: " + SlotPosition);
            if (OwnerLot != 0 || SellPrice != 0)
            {
                delve.Add("    Owner Lot: " + OwnerLot);
                delve.Add("   Sell Price: " + SellPrice);
            }
            delve.Add("");
            delve.Add("        Level: " + Level);
            string objTypeName = GlobalConstants.GetGenistarItemName(Flags) ?? GlobalConstants.ObjectTypeToName(client, Object_Type);
            delve.Add("       Object: " + objTypeName + " (" + Object_Type + ")");
            delve.Add("         Type: " + GlobalConstants.SlotToName(client, Item_Type) + " (" + Item_Type + ")");
            delve.Add("");
            delve.Add("        Model: " + Model);
            delve.Add("    Extension: " + Extension);
            delve.Add("        Color: " + Color);
            delve.Add("       Emblem: " + Emblem);
            delve.Add("       Effect: " + Effect);
            delve.Add("");
            delve.Add("       DPS_AF: " + DPS_AF);
            delve.Add("      SPD_ABS: " + SPD_ABS);
            delve.Add("         Hand: " + Hand);
            delve.Add("  Type_Damage: " + Type_Damage);
            delve.Add("        Bonus: " + Bonus);

            bool isGenWeaponTech = Template != null && Template.Flags >= 30 && Template.Flags <= 36 && Template.Flags != 32;
            bool isGenShieldTech = Template != null && Template.Flags == 32;
            bool isGenArmorTech = Template != null && Template.Flags >= 38 && Template.Flags <= 40;

            if (isGenWeaponTech || (!isGenShieldTech && !isGenArmorTech && GlobalConstants.IsWeapon(Object_Type)))
            {
                delve.Add("");
                delve.Add("         Hand: " + GlobalConstants.ItemHandToName(client, Hand) + " (" + Hand + ")");
                delve.Add("Damage/Second: " + (DPS_AF / 10.0f));
                delve.Add("        Speed: " + (SPD_ABS / 10.0f));
                delve.Add("  Damage type: " + GlobalConstants.WeaponDamageTypeToName(Type_Damage) + " (" + Type_Damage + ")");
                delve.Add("        Bonus: " + Bonus);
            }
            else if (isGenArmorTech || (!isGenWeaponTech && !isGenShieldTech && GlobalConstants.IsArmor(Object_Type)))
            {
                delve.Add("");
                delve.Add("  Armorfactor: " + DPS_AF);
                delve.Add("   Absorption: " + SPD_ABS);
                delve.Add("        Bonus: " + Bonus);
            }
            else if (isGenShieldTech || (!isGenWeaponTech && !isGenArmorTech && Object_Type == (int)eObjectType.Shield))
            {
                delve.Add("");
                delve.Add("Damage/Second: " + (DPS_AF / 10.0f));
                delve.Add("        Speed: " + (SPD_ABS / 10.0f));
                delve.Add("  Shield type: " + GlobalConstants.ShieldTypeToName(Type_Damage) + " (" + Type_Damage + ")");
                delve.Add("        Bonus: " + Bonus);
            }
            else if (Object_Type == (int)eObjectType.Arrow || Object_Type == (int)eObjectType.Bolt)
            {
                delve.Add("");
                delve.Add(" Ammunition #: " + DPS_AF);
                delve.Add("       Damage: " + GlobalConstants.AmmunitionTypeToDamageName(client, SPD_ABS));
                delve.Add("        Range: " + GlobalConstants.AmmunitionTypeToRangeName(client, SPD_ABS));
                delve.Add("     Accuracy: " + GlobalConstants.AmmunitionTypeToAccuracyName(client, SPD_ABS));
                delve.Add("        Bonus: " + Bonus);
            }
            else if (Object_Type == (int)eObjectType.Instrument)
            {
                delve.Add("");
                delve.Add("   Instrument: " + GlobalConstants.InstrumentTypeToName(DPS_AF));
            }

            if (OwnerLot != 0)
            {
                delve.Add("");
                delve.Add("   Owner Lot#: " + OwnerLot);
                delve.Add("   Sell Price: " + SellPrice);
            }

            delve.Add("");
            delve.Add("   Value/Price: " + Money.GetShortString(Price) + " / " + Money.GetShortString((long)(Price * (long)ServerProperties.Properties.ITEM_SELL_RATIO * .01)));
            delve.Add("Count/MaxCount: " + Count + " / " + MaxCount);
            delve.Add("        Weight: " + (Weight / 10.0f) + "lbs");
            delve.Add("       Quality: " + Quality + "%");
            delve.Add("    Durability: " + Durability + "/" + MaxDurability);
            delve.Add("     Condition: " + Condition + "/" + MaxCondition);
            delve.Add("         Realm: " + Realm);
            delve.Add("");
            delve.Add("   Is dropable: " + (IsDropable ? "yes" : "no"));
            delve.Add("   Is pickable: " + (IsPickable ? "yes" : "no"));
            delve.Add("	  CanUseInRvR: " + (CanUseInRvR ? "yes" : "no"));
            delve.Add("   Is tradable: " + (IsTradable ? "yes" : "no"));
            delve.Add("  Is alwaysDUR: " + (IsNotLosingDur ? "yes" : "no"));
            delve.Add(" Is Indestruct: " + (IsIndestructible ? "yes" : "no"));
            delve.Add("  Is stackable: " + (IsStackable ? "yes (" + MaxCount + ")" : "no"));
            delve.Add("");
            delve.Add("   ProcSpellID: " + ProcSpellID);
            delve.Add("  ProcSpellID1: " + ProcSpellID1);
            delve.Add("    ProcChance: " + ProcChance);
            delve.Add("       SpellID: " + SpellID + " (" + Charges + "/" + MaxCharges + ")");
            delve.Add("      SpellID1: " + SpellID1 + " (" + Charges1 + "/" + MaxCharges1 + ")");
            delve.Add(" PoisonSpellID: " + PoisonSpellID + " (" + PoisonCharges + "/" + PoisonMaxCharges + ") ");
            delve.Add("");
            delve.Add("AllowedClasses: " + AllowedClasses);
            delve.Add(" LevelRequired: " + LevelRequirement);
            delve.Add("    BonusLevel: " + BonusLevel);
            delve.Add(" ");
            delve.Add("              Flags: " + Flags);
            delve.Add("     SalvageYieldID: " + SalvageYieldID);
            delve.Add("          PackageID: " + PackageID);
            delve.Add("Requested ClassType: " + ClassType);
        }
    }
}
