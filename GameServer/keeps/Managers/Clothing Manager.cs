using System;
using DOL.Database;
using DOL.Events;

namespace DOL.GS.Keeps
{
    public enum eHandFlag { Right = 0, Two = 1, Left = 2 }
    public enum eExtension { Two = 2, Three = 3, Four = 4, Five = 5 }

    public class ClothingMgr
    {
        #region Uniform Declarations
        public static GameNpcInventoryTemplate Albion_Archer = new GameNpcInventoryTemplate(), Albion_Caster = new GameNpcInventoryTemplate(), Albion_Fighter = new GameNpcInventoryTemplate(), Albion_Healer = new GameNpcInventoryTemplate(), Albion_Stealther = new GameNpcInventoryTemplate(), Albion_Lord = new GameNpcInventoryTemplate(), Albion_FighterPK = new GameNpcInventoryTemplate(), Albion_ArcherPK = new GameNpcInventoryTemplate(), Albion_CasterPK = new GameNpcInventoryTemplate();
        public static GameNpcInventoryTemplate Midgard_Archer = new GameNpcInventoryTemplate(), Midgard_Caster = new GameNpcInventoryTemplate(), Midgard_Fighter = new GameNpcInventoryTemplate(), Midgard_Healer = new GameNpcInventoryTemplate(), Midgard_Hastener = new GameNpcInventoryTemplate(), Midgard_Stealther = new GameNpcInventoryTemplate(), Midgard_Lord = new GameNpcInventoryTemplate(), Midgard_FighterPK = new GameNpcInventoryTemplate(), Midgard_ArcherPK = new GameNpcInventoryTemplate(), Midgard_CasterPK = new GameNpcInventoryTemplate();
        public static GameNpcInventoryTemplate Hibernia_Archer = new GameNpcInventoryTemplate(), Hibernia_Caster = new GameNpcInventoryTemplate(), Hibernia_Fighter = new GameNpcInventoryTemplate(), Hibernia_Healer = new GameNpcInventoryTemplate(), Hibernia_Stealther = new GameNpcInventoryTemplate(), Hibernia_Lord = new GameNpcInventoryTemplate(), Hibernia_FighterPK = new GameNpcInventoryTemplate(), Hibernia_ArcherPK = new GameNpcInventoryTemplate(), Hibernia_CasterPK = new GameNpcInventoryTemplate();
        #endregion

        /// <summary>
        /// Builder Pattern: Instantly creates a uniform without repeating code.
        /// </summary>
        private static GameNpcInventoryTemplate BuildUniform(string dbName, int cloak, int torso, int legs, int arms, int hands, int feet, int head, int lHand, int rHand, int twoHand, int dist, int lHandFlag = (int)eHandFlag.Left, int lHandObjType = (int)eObjectType.Shield, int distObjType = 0, int cloakColor = 0)
        {
            GameNpcInventoryTemplate t = new GameNpcInventoryTemplate();
            if (!t.LoadFromDatabase(dbName))
            {
                if (cloak > 0) t.AddNPCEquipment(eInventorySlot.Cloak, (ushort)cloak, cloakColor, 0);
                if (torso > 0) t.AddNPCEquipment(eInventorySlot.TorsoArmor, (ushort)torso);
                if (legs > 0) t.AddNPCEquipment(eInventorySlot.LegsArmor, (ushort)legs);
                if (arms > 0) t.AddNPCEquipment(eInventorySlot.ArmsArmor, (ushort)arms);
                if (hands > 0) t.AddNPCEquipment(eInventorySlot.HandsArmor, (ushort)hands);
                if (feet > 0) t.AddNPCEquipment(eInventorySlot.FeetArmor, (ushort)feet);
                if (head > 0) t.AddNPCEquipment(eInventorySlot.HeadArmor, (ushort)head);
                
                if (lHand > 0) { 
                    t.AddNPCEquipment(eInventorySlot.LeftHandWeapon, (ushort)lHand); 
                    t.GetItem(eInventorySlot.LeftHandWeapon).Hand = lHandFlag;
                    t.GetItem(eInventorySlot.LeftHandWeapon).SlotPosition = Slot.LEFTHAND;
                    if (lHandObjType > 0) t.GetItem(eInventorySlot.LeftHandWeapon).Object_Type = lHandObjType; 
                }
                if (rHand > 0) t.AddNPCEquipment(eInventorySlot.RightHandWeapon, (ushort)rHand);
                if (twoHand > 0) { t.AddNPCEquipment(eInventorySlot.TwoHandWeapon, (ushort)twoHand); t.GetItem(eInventorySlot.TwoHandWeapon).Hand = (int)eHandFlag.Two; }
                if (dist > 0) { 
                    t.AddNPCEquipment(eInventorySlot.DistanceWeapon, (ushort)dist); 
                    t.GetItem(eInventorySlot.DistanceWeapon).Hand = (int)eHandFlag.Two; 
                    t.GetItem(eInventorySlot.DistanceWeapon).SlotPosition = Slot.RANGED;
                    if (distObjType > 0) t.GetItem(eInventorySlot.DistanceWeapon).Object_Type = distObjType;
                }
                t = t.CloseTemplate();
            }
            return t;
        }

        public static void LoadTemplates()
        {
            // --- ALBION ---
            Albion_Archer    = BuildUniform("albion_archer", 3800, 728, 663, 664, 665, 666, 0, 59, 653, 0, 849);
            Albion_Caster    = BuildUniform("albion_caster", 3800, 58, 0, 0, 142, 143, 0, 0, 13, 1170, 0);
            Albion_Fighter   = BuildUniform("albion_fighter", 3800, 662, 663, 664, 665, 666, 95, 60, 10, 649, 0);
            Albion_Lord      = BuildUniform("albion_lord", 3800, 662, 663, 664, 665, 666, 95, 60, 10, 649, 132);
            Albion_Healer    = BuildUniform("albion_healer", 3800, 713, 663, 664, 665, 666, 94, 61, 3282, 0, 0);
            Albion_Stealther = BuildUniform("albion_stealther", 3800, 792, 663, 664, 665, 666, 0, 653, 653, 653, 0, (int)eHandFlag.Left, 0);
            Albion_FighterPK.LoadFromDatabase("alb_fighter_pk"); Albion_ArcherPK.LoadFromDatabase("alb_archer_pk"); Albion_CasterPK.LoadFromDatabase("alb_caster_pk");

            // --- MIDGARD ---
            Midgard_Archer   = BuildUniform("midgard_archer", 3801, 668, 2943, 2944, 2945, 2946, 2874, 59, 0, 328, 1037);
            Midgard_Caster   = BuildUniform("midgard_caster", 3801, 98, 0, 0, 142, 143, 0, 0, 13, 566, 0);
            Midgard_Fighter  = BuildUniform("midgard_fighter", 3801, 668, 2943, 2944, 2945, 2946, 2874, 60, 313, 572, 0);
            Midgard_Lord     = BuildUniform("midgard_lord", 3801, 668, 2943, 2944, 2945, 2946, 2874, 60, 313, 572, 564, (int)eHandFlag.Left, (int)eObjectType.Shield, (int)eObjectType.Longbow);
            Midgard_Healer   = BuildUniform("midgard_healer", 3801, 668, 2943, 2944, 2945, 2946, 2874, 59, 3335, 3336, 0);
            Midgard_Hastener = BuildUniform("midgard_hastener", 443, 230, 0, 0, 233, 234, 0, 228, 0, 0, 0, (int)eHandFlag.Left, 0, 0, 43); 
            Midgard_Stealther= BuildUniform("midgard_stealther", 3801, 668, 2943, 2944, 2945, 2946, 335, 573, 573, 577, 0, (int)eHandFlag.Left, 0);
            Midgard_FighterPK.LoadFromDatabase("mid_fighter_pk"); Midgard_ArcherPK.LoadFromDatabase("mid_archer_pk"); Midgard_CasterPK.LoadFromDatabase("mid_caster_pk");

            // --- HIBERNIA ---
            Hibernia_Archer  = BuildUniform("hibernia_archer", 3802, 667, 989, 990, 991, 992, 1207, 643, 643, 0, 919, (int)eHandFlag.Left, 0, (int)eObjectType.RecurvedBow);
            Hibernia_Caster  = BuildUniform("hibernia_caster", 3802, 97, 0, 0, 142, 143, 0, 0, 13, 1176, 0);
            Hibernia_Fighter = BuildUniform("hibernia_fighter", 3802, 667, 989, 990, 991, 992, 1207, 79, 897, 476, 0);
            Hibernia_Lord    = BuildUniform("hibernia_lord", 3802, 667, 989, 990, 991, 992, 1207, 79, 897, 476, 471, (int)eHandFlag.Left, (int)eObjectType.Shield, (int)eObjectType.CompositeBow);
            Hibernia_Healer  = BuildUniform("hibernia_healer", 3802, 667, 989, 990, 991, 992, 1207, 59, 3247, 0, 0);
            Hibernia_Stealther = BuildUniform("hibernia_stealther", 3802, 667, 989, 990, 991, 992, 0, 2685, 2685, 2687, 0, (int)eHandFlag.Left, 0);
            Hibernia_FighterPK.LoadFromDatabase("hib_fighter_pk"); Hibernia_ArcherPK.LoadFromDatabase("hib_archer_pk"); Hibernia_CasterPK.LoadFromDatabase("hib_caster_pk");
        }

        public static void EquipGuard(GameKeepGuard guard)
        {
            if (!ServerProperties.Properties.AUTOEQUIP_GUARDS_LOADED_FROM_DB && !guard.LoadedFromScript) return;

            if (guard is FrontierHastener && guard.Realm != eRealm.None)
            {
                guard.Inventory = new GameNPCInventory(Midgard_Hastener.CloneTemplate());
                guard.SwitchWeapon(GameLiving.eActiveWeaponSlot.Standard);
                return;
            }

            GameNpcInventoryTemplate targetTemplate = Albion_Fighter; 
            switch (guard.ModelRealm)
            {
                case eRealm.None:
                case eRealm.Albion:
                    if (guard is GuardFighter) targetTemplate = guard.IsPortalKeepGuard ? Albion_FighterPK : Albion_Fighter;
                    else if (guard is GuardLord || guard is MissionMaster) targetTemplate = Albion_Lord;
                    else if (guard is GuardHealer) targetTemplate = Albion_Healer;
                    else if (guard is GuardArcher) targetTemplate = guard.IsPortalKeepGuard ? Albion_ArcherPK : Albion_Archer;
                    else if (guard is GuardCaster) targetTemplate = guard.IsPortalKeepGuard ? Albion_CasterPK : Albion_Caster;
                    else if (guard is GuardStealther) targetTemplate = Albion_Stealther;
                    break;
                case eRealm.Midgard:
                    if (guard is GuardFighter) targetTemplate = guard.IsPortalKeepGuard ? Midgard_FighterPK : Midgard_Fighter;
                    else if (guard is GuardLord || guard is MissionMaster) targetTemplate = Midgard_Lord;
                    else if (guard is GuardHealer) targetTemplate = Midgard_Healer;
                    else if (guard is GuardArcher) targetTemplate = guard.IsPortalKeepGuard ? Midgard_ArcherPK : Midgard_Archer;
                    else if (guard is GuardCaster) targetTemplate = guard.IsPortalKeepGuard ? Midgard_CasterPK : Midgard_Caster;
                    else if (guard is GuardStealther) targetTemplate = Midgard_Stealther;
                    break;
                case eRealm.Hibernia:
                    if (guard is GuardFighter) targetTemplate = guard.IsPortalKeepGuard ? Hibernia_FighterPK : Hibernia_Fighter;
                    else if (guard is GuardLord || guard is MissionMaster) targetTemplate = Hibernia_Lord;
                    else if (guard is GuardHealer) targetTemplate = Hibernia_Healer;
                    else if (guard is GuardArcher) targetTemplate = guard.IsPortalKeepGuard ? Hibernia_ArcherPK : Hibernia_Archer;
                    else if (guard is GuardCaster) targetTemplate = guard.IsPortalKeepGuard ? Hibernia_CasterPK : Hibernia_Caster;
                    else if (guard is GuardStealther) targetTemplate = Hibernia_Stealther;
                    break;
            }

            guard.Inventory = new GameNPCInventory(targetTemplate.CloneTemplate());

            // Apply extensions and colors
            const int renegadeArmorColor = 19;
            if (guard.Realm == eRealm.None)
            {
                foreach (eInventorySlot slot in new[] { eInventorySlot.TorsoArmor, eInventorySlot.ArmsArmor, eInventorySlot.LegsArmor, eInventorySlot.HandsArmor, eInventorySlot.FeetArmor })
                {
                    var item = guard.Inventory.GetItem(slot);
                    if (item != null) { item.Extension = (int)eExtension.Four; item.Color = renegadeArmorColor; }
                }
                var cloak = guard.Inventory.GetItem(eInventorySlot.Cloak);
                if (cloak != null) { cloak.Model = 3632; cloak.Color = renegadeArmorColor; }
            }
            else
            {
                foreach (eInventorySlot slot in new[] { eInventorySlot.TorsoArmor, eInventorySlot.HandsArmor, eInventorySlot.FeetArmor })
                {
                    var item = guard.Inventory.GetItem(slot);
                    if (item != null) item.Extension = (int)eExtension.Five;
                }
            }

            // Set active slots
            if (guard is GuardCaster) guard.SwitchWeapon(GameLiving.eActiveWeaponSlot.TwoHanded);
            else if (guard is GuardArcher) guard.SwitchWeapon(GameLiving.eActiveWeaponSlot.Distance);
            else if ((guard is GuardFighter || guard is GuardLord) && Util.Chance(50)) guard.SwitchWeapon(GameLiving.eActiveWeaponSlot.TwoHanded);
            else guard.SwitchWeapon(GameLiving.eActiveWeaponSlot.Standard);
        }

        public static void SetEmblem(GameKeepGuard guard)
        {
            if (guard.Inventory == null || guard.Component == null) return;
            
            int emblem = guard.Component.Keep.Guild?.Emblem ?? 0;

            InventoryItem cloak = guard.Inventory.GetItem(eInventorySlot.Cloak);
            if (cloak != null) { cloak.Emblem = emblem; if (emblem != 0) cloak.Model = 558; }
            
            InventoryItem shield = guard.Inventory.GetItem(eInventorySlot.LeftHandWeapon);
            if (shield != null) shield.Emblem = emblem;

            guard.BroadcastLivingEquipmentUpdate();
        }
    }
}