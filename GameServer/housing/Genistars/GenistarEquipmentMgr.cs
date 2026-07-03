using DOL.Database;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.Scripts
{
    public static class GenistarEquipmentMgr
    {
        public enum eGenistarEquipCategory
        {
            Animal,
            Humanoid,
            Titan
        }

        public static eGenistarEquipCategory GetEquipCategory(DBGenistar db)
        {
            if (db == null) return eGenistarEquipCategory.Animal;

            if (db.Name == "Magma Colossus" || db.Name == "Fenrir Apex" ||
                db.Name == "Abyssal Wyrm" || db.Name == "Titan Bear")
                return eGenistarEquipCategory.Titan;

            if (db.BodyType == (int)NpcTemplateMgr.eBodyType.Humanoid ||
                db.BodyType == (int)NpcTemplateMgr.eBodyType.Demon ||
                db.BodyType == (int)NpcTemplateMgr.eBodyType.Giant)
                return eGenistarEquipCategory.Humanoid;

            return eGenistarEquipCategory.Animal;
        }

        public static void AdjustDBStats(DBGenistar db, ItemTemplate tpl, bool isEquipping)
        {
            if (tpl == null || db == null) return;
            int multiplier = isEquipping ? 1 : -1;
            int flag = tpl.Flags;

            if (flag >= 38 && flag <= 40) // Armor
            {
                db.ArmorFactor = Math.Max(0, db.ArmorFactor + (tpl.DPS_AF * multiplier));
                db.ArmorAbsorb = Math.Max(0, db.ArmorAbsorb + (tpl.SPD_ABS * multiplier));
            }
            else if (flag >= 30 && flag <= 36) // Weapon & Shield
            {
                db.WeaponDPS = Math.Max(0, db.WeaponDPS + (tpl.DPS_AF * multiplier));
                db.WeaponSpd = Math.Max(0, db.WeaponSpd + (tpl.SPD_ABS * multiplier));

                if (flag == 32) // Specific Shield Injection
                {
                    db.BlockChance = Math.Max(0, db.BlockChance + (10 * multiplier));
                }
            }

            ModifyGenistarStat(db, tpl.Bonus1Type, tpl.Bonus1, multiplier);
            ModifyGenistarStat(db, tpl.Bonus2Type, tpl.Bonus2, multiplier);
            ModifyGenistarStat(db, tpl.Bonus3Type, tpl.Bonus3, multiplier);
            ModifyGenistarStat(db, tpl.Bonus4Type, tpl.Bonus4, multiplier);
            ModifyGenistarStat(db, tpl.Bonus5Type, tpl.Bonus5, multiplier);
            ModifyGenistarStat(db, tpl.Bonus6Type, tpl.Bonus6, multiplier);
            ModifyGenistarStat(db, tpl.Bonus7Type, tpl.Bonus7, multiplier);
            ModifyGenistarStat(db, tpl.Bonus8Type, tpl.Bonus8, multiplier);
            ModifyGenistarStat(db, tpl.Bonus9Type, tpl.Bonus9, multiplier);
            ModifyGenistarStat(db, tpl.Bonus10Type, tpl.Bonus10, multiplier);
            ModifyGenistarStat(db, tpl.ExtraBonusType, tpl.ExtraBonus, multiplier);
        }

        private static void ModifyGenistarStat(DBGenistar db, int type, int amount, int multiplier)
        {
            if (type <= 0 || amount == 0) return;
            int val = amount * multiplier;

            switch (type)
            {
                case (int)eProperty.Strength: db.Strength = Math.Max(0, db.Strength + val); break;
                case (int)eProperty.Dexterity: db.Dexterity = Math.Max(0, db.Dexterity + val); break;
                case (int)eProperty.Constitution: db.Constitution = Math.Max(0, db.Constitution + val); break;
                case (int)eProperty.Quickness: db.Quickness = Math.Max(0, db.Quickness + val); break;
                case (int)eProperty.Intelligence: db.Intelligence = Math.Max(0, db.Intelligence + val); break;
                case (int)eProperty.Piety: db.Piety = Math.Max(0, db.Piety + val); break;
                case (int)eProperty.Empathy: db.Empathy = Math.Max(0, db.Empathy + val); break;
                case (int)eProperty.Charisma: db.Charisma = Math.Max(0, db.Charisma + val); break;

                case (int)eProperty.MaxHealth: db.MaxHealth = Math.Max(0, db.MaxHealth + val); break;

                case (int)eProperty.Resist_Body: db.ResistBody = Math.Max(0, db.ResistBody + val); break;
                case (int)eProperty.Resist_Cold: db.ResistCold = Math.Max(0, db.ResistCold + val); break;
                case (int)eProperty.Resist_Crush: db.ResistCrush = Math.Max(0, db.ResistCrush + val); break;
                case (int)eProperty.Resist_Energy: db.ResistEnergy = Math.Max(0, db.ResistEnergy + val); break;
                case (int)eProperty.Resist_Heat: db.ResistHeat = Math.Max(0, db.ResistHeat + val); break;
                case (int)eProperty.Resist_Matter: db.ResistMatter = Math.Max(0, db.ResistMatter + val); break;
                case (int)eProperty.Resist_Slash: db.ResistSlash = Math.Max(0, db.ResistSlash + val); break;
                case (int)eProperty.Resist_Spirit: db.ResistSpirit = Math.Max(0, db.ResistSpirit + val); break;
                case (int)eProperty.Resist_Thrust: db.ResistThrust = Math.Max(0, db.ResistThrust + val); break;
                case (int)eProperty.Resist_Natural: db.ResistNatural = Math.Max(0, db.ResistNatural + val); break;

                case (int)eProperty.CounterAttack: db.CounterAttackChance = Math.Max(0, db.CounterAttackChance + val); break;
                case (int)eProperty.ArmorFactor: db.ArmorFactor = Math.Max(0, db.ArmorFactor + val); break;
                case (int)eProperty.ArmorAbsorption: db.ArmorAbsorb = Math.Max(0, db.ArmorAbsorb + val); break;

                case (int)eProperty.SpellRange: db.CastRange = Math.Max(0, db.CastRange + val); break;
                case (int)eProperty.MeleeSpeed: db.WeaponSpd = Math.Max(0, db.WeaponSpd + val); break;

                case (int)eProperty.EvadeChance: db.EvadeChance = Math.Max(0, db.EvadeChance + val); break;
                case (int)eProperty.BlockChance: db.BlockChance = Math.Max(0, db.BlockChance + val); break;
                case (int)eProperty.ParryChance: db.ParryChance = Math.Max(0, db.ParryChance + val); break;

                case (int)eProperty.PieceAblative: db.AblativeShield = Math.Max(0.0, db.AblativeShield + (val / 100.0)); break;
                case (int)eProperty.DPS: db.WeaponDPS = Math.Max(0, db.WeaponDPS + val); break;
                case (int)eProperty.MagicAbsorption: db.SpellmagicABS = Math.Max(0, db.SpellmagicABS + val); break;
                case (int)eProperty.MythicalCrowdDuration: db.CCResist = Math.Max(0, db.CCResist + val); break;

                case (int)eProperty.LootChance: db.ProcSpellChance = Math.Max(0, db.ProcSpellChance + val); break;
                case (int)eProperty.StyleAbsorb: db.MeleeABS = Math.Max(0, db.MeleeABS + val); break;
                case (int)eProperty.LivingEffectiveness: db.EffectivenessMod = Math.Max(0, db.EffectivenessMod + val); break;
                case (int)eProperty.CriticalDotHitChance: db.DotABS = Math.Max(0, db.DotABS + val); break;
                case (int)eProperty.OffhandChanceBonus: db.LeftHandSwingChance = Math.Max(0, db.LeftHandSwingChance + val); break;
                case (int)eProperty.MythicalDebuffResistChance: db.DebuffResist = Math.Max(0, db.DebuffResist + val); break;
            }
        }

        public static void ApplyEquipmentBonuses(DBGenistar db, GameNPC creature)
        {
            if (db == null || creature == null) return;

            // 1. Reset dynamic combat variables to biological baseline updated from the DB!
            creature.WeaponDps = db.WeaponDPS;
            creature.WeaponSpd = db.WeaponSpd;
            creature.BlockChance = (byte)db.BlockChance;
            creature.LeftHandSwingChance = (byte)db.LeftHandSwingChance;
            db.ProcSpellID = "";

            if (string.IsNullOrEmpty(db.EquipmentTemplateID)) return;

            List<string> templateIdsToProcess = new List<string>();
            var equips = GameServer.Database.SelectObjects<NPCEquipment>(DB.Column("TemplateID").IsEqualTo(db.EquipmentTemplateID));

            int activeWeaponSlot = (int)eInventorySlot.RightHandWeapon;
            if (creature.ActiveWeaponSlot == GameLiving.eActiveWeaponSlot.TwoHanded) activeWeaponSlot = (int)eInventorySlot.TwoHandWeapon;
            else if (creature.ActiveWeaponSlot == GameLiving.eActiveWeaponSlot.Distance) activeWeaponSlot = (int)eInventorySlot.DistanceWeapon;

            var activeWep = equips.FirstOrDefault(e => e.Slot == activeWeaponSlot);
            if (activeWep != null && !string.IsNullOrEmpty(activeWep.EventID)) templateIdsToProcess.Add(activeWep.EventID);

            var leftHand = equips.FirstOrDefault(e => e.Slot == (int)eInventorySlot.LeftHandWeapon);
            if (leftHand != null && !string.IsNullOrEmpty(leftHand.EventID)) templateIdsToProcess.Add(leftHand.EventID);

            var armorSet = equips.FirstOrDefault(e => e.Slot == (int)eInventorySlot.TorsoArmor);
            if (armorSet != null && !string.IsNullOrEmpty(armorSet.EventID)) templateIdsToProcess.Add(armorSet.EventID);

            // 2. Extract and Apply Damage Types and Procs ONLY (Stats handled via DB AdjustDBStats)
            foreach (string id in templateIdsToProcess.Distinct())
            {
                var tpl = GameServer.Database.FindObjectByKey<ItemTemplate>(id);
                if (tpl == null) continue;

                if (activeWep != null && activeWep.EventID == id)
                {
                    if (tpl.Type_Damage > 0) creature.MeleeDamageType = (eDamageType)tpl.Type_Damage;
                }

                if (tpl.ProcSpellID > 0) db.ProcSpellID = tpl.ProcSpellID.ToString();
            }
        }

        public static bool TryEquipGear(GamePlayer player, DBGenistar db, GameNPC creature, InventoryItem item)
        {
            string lang = player.Client?.Account?.Language ?? "EN";
            if (item == null || item.Template == null) return false;

            int flag = item.Template.Flags;

            // Check if it's a valid Genistar item at all
            if (flag < 30 || flag > 40)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.CantEquip"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            var category = GetEquipCategory(db);
            List<eInventorySlot> targetSlots = new List<eInventorySlot>();
            eInventorySlot anchorSlot = eInventorySlot.Invalid;

            // ---- Armor Categorization ----
            if (flag >= 38 && flag <= 40)
            {
                if (flag == 38 && category != eGenistarEquipCategory.Animal)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.ArmorAnimal"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (flag == 39 && category != eGenistarEquipCategory.Humanoid)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.ArmorHumanoid"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (flag == 40 && category != eGenistarEquipCategory.Titan)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.ArmorTitan"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                anchorSlot = eInventorySlot.TorsoArmor;
                targetSlots.Add(eInventorySlot.TorsoArmor);

                if (flag == 39)
                {
                    targetSlots.AddRange(new[] { eInventorySlot.HeadArmor, eInventorySlot.ArmsArmor, eInventorySlot.LegsArmor, eInventorySlot.HandsArmor, eInventorySlot.FeetArmor, eInventorySlot.Cloak });
                }
            }
            // ---- Weapon Categorization ----
            else
            {
                if (flag == 30 && category != eGenistarEquipCategory.Animal)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.GearAnimal"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (flag == 36 && category != eGenistarEquipCategory.Titan)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.GearTitan"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (flag >= 31 && flag <= 35 && category != eGenistarEquipCategory.Humanoid)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.GearHumanoid"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }

                switch (flag)
                {
                    case 30: case 31: case 36: anchorSlot = eInventorySlot.RightHandWeapon; break;
                    case 32: case 34: anchorSlot = eInventorySlot.LeftHandWeapon; break;
                    case 33: anchorSlot = eInventorySlot.TwoHandWeapon; break;
                    case 35: anchorSlot = eInventorySlot.DistanceWeapon; break;
                }
                if (anchorSlot != eInventorySlot.Invalid)
                    targetSlots.Add(anchorSlot);
            }

            if (targetSlots.Count == 0) return false;

            string equipTemplateId = db.EquipmentTemplateID;
            if (string.IsNullOrEmpty(equipTemplateId))
            {
                equipTemplateId = "genistar_eq_" + db.GenistarID;
                db.EquipmentTemplateID = equipTemplateId;
                GameServer.Database.SaveObject(db);
            }

            var existingEquips = GameServer.Database.SelectObjects<NPCEquipment>(DB.Column("TemplateID").IsEqualTo(equipTemplateId));
            var existingAnchor = existingEquips.FirstOrDefault(e => e.Slot == (int)anchorSlot);

            if (existingAnchor != null)
            {
                if (existingAnchor.EventID == item.Id_nb || (existingAnchor.Model == item.Model && existingAnchor.Color == item.Color && existingAnchor.Effect == item.Effect))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.Duplicate"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            if (item.Count > 1) player.Inventory.RemoveCountFromStack(item, 1);
            else player.Inventory.RemoveItem(item);

            if (existingAnchor != null && !string.IsNullOrEmpty(existingAnchor.EventID))
            {
                var oldTpl = GameServer.Database.FindObjectByKey<ItemTemplate>(existingAnchor.EventID);
                if (oldTpl != null)
                {
                    AdjustDBStats(db, oldTpl, false);
                    InventoryItem returnedItem = GameInventoryItem.Create(oldTpl);
                    returnedItem.Count = 1;
                    player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, returnedItem);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.Swapped", oldTpl.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }

            AdjustDBStats(db, item.Template, true);

            foreach (var existing in existingEquips)
            {
                if (targetSlots.Contains((eInventorySlot)existing.Slot))
                    GameServer.Database.DeleteObject(existing);
            }

            foreach (var slot in targetSlots)
            {
                NPCEquipment newEquip = new NPCEquipment();
                newEquip.TemplateID = equipTemplateId;
                newEquip.Slot = (int)slot;
                newEquip.Color = item.Color;
                newEquip.Effect = item.Effect;
                newEquip.Extension = item.Extension;

                if (flag == 39)
                {
                    if (slot == eInventorySlot.TorsoArmor) newEquip.Model = item.Model;
                    else if (slot == eInventorySlot.LegsArmor) newEquip.Model = item.Model + 1;
                    else if (slot == eInventorySlot.ArmsArmor) newEquip.Model = item.Model + 2;
                    else if (slot == eInventorySlot.HeadArmor) newEquip.Model = item.Model + 3;
                    else if (slot == eInventorySlot.FeetArmor) newEquip.Model = item.Model + 4;
                    else if (slot == eInventorySlot.HandsArmor) newEquip.Model = item.Model + 5;
                    else if (slot == eInventorySlot.Cloak) newEquip.Model = item.Model + 6;
                }
                else
                {
                    newEquip.Model = item.Model;
                }

                if (slot == anchorSlot)
                {
                    newEquip.EventID = item.Id_nb;
                }

                GameServer.Database.AddObject(newEquip);
            }

            GameNpcInventoryTemplate newInv = new GameNpcInventoryTemplate();
            if (newInv.LoadFromDatabase(equipTemplateId))
            {
                creature.Inventory = newInv.CloseTemplate();
                creature.EquipmentTemplateID = equipTemplateId;
                creature.BroadcastLivingEquipmentUpdate();
            }

            if (category == eGenistarEquipCategory.Animal || category == eGenistarEquipCategory.Titan)
            {
                db.VisibleWeaponSlot = 0;
                creature.SwitchWeapon(GameLiving.eActiveWeaponSlot.Standard);
            }

            GameServer.Database.SaveObject(db);

            creature.BossSpellABS = db.SpellmagicABS;
            creature.BossMeleeABS = db.MeleeABS;
            creature.BossDotABS = db.DotABS;
            creature.BossMaxHealthMod = db.MaxHealth;
            creature.BossEffectivenessMod = db.EffectivenessMod;
            creature.BossCCResist = db.CCResist;
            creature.BossDebuffResist = db.DebuffResist;
            creature.BossCastRangeMod = db.CastRange;
            creature.BossAblativeShieldMult = db.AblativeShield;

            creature.AutoSetStats();
            player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.Equipped", item.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            return true;
        }

        public static void TryUnequipGear(GamePlayer player, DBGenistar db, GameNPC creature, string slotName)
        {
            string lang = player.Client?.Account?.Language ?? "EN";

            if (string.IsNullOrEmpty(db.EquipmentTemplateID))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.NoMods"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            List<eInventorySlot> slotsToRemove = new List<eInventorySlot>();
            eInventorySlot anchorSlot = eInventorySlot.Invalid;

            switch (slotName.ToLower())
            {
                case "right":
                case "righthand": anchorSlot = eInventorySlot.RightHandWeapon; slotsToRemove.Add(anchorSlot); break;
                case "left":
                case "lefthand":
                case "shield": anchorSlot = eInventorySlot.LeftHandWeapon; slotsToRemove.Add(anchorSlot); break;
                case "two":
                case "twohand":
                case "twohanded": anchorSlot = eInventorySlot.TwoHandWeapon; slotsToRemove.Add(anchorSlot); break;
                case "distance":
                case "bow":
                case "ranged": anchorSlot = eInventorySlot.DistanceWeapon; slotsToRemove.Add(anchorSlot); break;
                case "armor":
                    anchorSlot = eInventorySlot.TorsoArmor;
                    slotsToRemove.AddRange(new[] { eInventorySlot.TorsoArmor, eInventorySlot.HeadArmor, eInventorySlot.ArmsArmor, eInventorySlot.LegsArmor, eInventorySlot.HandsArmor, eInventorySlot.FeetArmor, eInventorySlot.Cloak });
                    break;
                default:
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.InvalidSlot"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
            }

            var equips = GameServer.Database.SelectObjects<NPCEquipment>(DB.Column("TemplateID").IsEqualTo(db.EquipmentTemplateID));
            var targetEq = equips.FirstOrDefault(e => e.Slot == (int)anchorSlot);

            if (targetEq == null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.EmptySlot"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!player.Inventory.IsSlotsFree(1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.BackpackFull"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!string.IsNullOrEmpty(targetEq.EventID))
            {
                var tpl = GameServer.Database.FindObjectByKey<ItemTemplate>(targetEq.EventID);
                if (tpl != null)
                {
                    AdjustDBStats(db, tpl, false);
                    GameServer.Database.SaveObject(db);
                    InventoryItem returnedItem = GameInventoryItem.Create(tpl);
                    returnedItem.Count = 1;
                    player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, returnedItem);
                    player.Out.SendMessage(LanguageMgr.GetTranslation(lang, "Genistar.Equip.Retrieved", tpl.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }

            foreach (var e in equips)
            {
                if (slotsToRemove.Contains((eInventorySlot)e.Slot)) GameServer.Database.DeleteObject(e);
            }

            GameNpcInventoryTemplate newInv = new GameNpcInventoryTemplate();
            if (newInv.LoadFromDatabase(db.EquipmentTemplateID))
            {
                creature.Inventory = newInv.CloseTemplate();
                creature.BroadcastLivingEquipmentUpdate();
            }
            else
            {
                creature.Inventory = null;
                creature.BroadcastLivingEquipmentUpdate();
            }

            creature.AutoSetStats();
        }
    }
}