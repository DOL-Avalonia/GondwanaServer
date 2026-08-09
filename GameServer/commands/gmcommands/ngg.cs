using System;
using System.Linq;
using System.Text;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.Database;

namespace DOL.GS.Commands
{
    [Cmd("&ngg", ePrivLevel.GM, "NPC Gear Generator",
       "'/ngg random [color] [pattern]' Create a completely random equipment template and weapon set",
       "'/ngg cloth [color] [pattern]' Create a random set of cloth",
       "'/ngg leather [color] [pattern]' Create a random set of leather",
       "'/ngg studded [color] [pattern]' Create a random set of studded",
       "'/ngg chain [color] [pattern]' Create a random set of chain",
       "'/ngg scale [color] [pattern]' Create a random set of scale",
       "'/ngg plate [color] [pattern]' Create a random set of plate",
       "'/ngg cloak [color] [category]' Add a cloak (Categories: ToA, Class, Regular, Guard, Realm, Otherworldly, Special)",
       "'/ngg mask [maskType] [color]' Apply a specific mask to the head slot",
       "'/ngg epic [class] [color] [DF]' create the class epic set (armor+wep+cloak) to the NPC",
       "'/ngg weapon [class] [template] [color]' create a class weapon set to the NPC (templates: epic, epicdf, classic)",
       "'/ngg equip [slotNumber] [color]' Add a random equipement to the NPC slot",
       "'/ngg colors' Display all available armor colors and IDs",
       "'/ngg save' Save the template and NPC to database",
       "---------------------------------",
       "|class| : class name - exemple : '/ngg epic shadowblade'",
       "---------------------------------",
       "'|color| : ID or Name (e.g. 43 or Cloth_Black) to select the same color for all parts (if empty : random)'",
       "or |Cloak| |Head| |Hands| |Arms| |Torso| |Legs| |Boots| to choose the color of each part.",
       "exemple : '/ngg plate Metal_Red Metal_Gold Metal_Gold Metal_Gold Metal_Red Metal_Gold Metal_Gold'",
       "---------------------------------",
       "|slotNumber| :",
       "- for weapons : 10 = 'right hand', 11 = 'left hand', 12 = 'two handed', 13 = 'distance'",
       "- for armors : 21 = 'head', 22 = 'hands', 23 = 'boots', 25 = 'torso', 26 = 'cloak', 27 = 'legs', 28 = 'arms'",
       "exemple : '/ngg equip 12 ' Add a two handed weapon to the NPC with a random color.",
       "---------------------------------",
       "|pattern| : pattern type",
       "'Possessed, Good, Corrupt, Minotaur, Oceanus, Stygia, Volcanus, Aerus, or Class[Name] (e.g. ClassHealer)'",
       "---------------------------------",
       "|category| : cloak category type",
       "'toa, regular, guard, realm, otherworldly, special, class (Class[Name] (e.g. ClassHealer))'",
       "---------------------------------",
       "|mask| : mask type",
       "eye, horned, slitted, skull, plague, beast, devour, satyr, death, ornatedplague, ornatedbeast, ornatedsatyr, ornatedskull, yule, demon, pumpkin, angel, midona, tentacled, hothead, sunburst, headorbit, eyesfire, eyesblue, eyesgreen",
       "---------------------------------",
       "don't forget to use '/ngg save' when you have finish the npc template.")]

    public class NGGCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private static byte[] slots = { 0x15, 0x16, 0x1C, 0x19, 0x1B, 0x17, 0x1A, 0x0A, 0x0B, 0x0C, 0x0D };
        private static ushort[] validColors;

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            Array colorArray = Enum.GetValues(typeof(eColor));
            validColors = new ushort[colorArray.Length];
            int cIdx = 0;
            foreach (byte c in colorArray) validColors[cIdx++] = (ushort)c;
        }

        private void DisplayColors(GameClient client)
        {
            client.Player.Out.SendMessage("--- Available NGG Colors ---", eChatType.CT_System, eChatLoc.CL_PopupWindow);
            Array colorValues = Enum.GetValues(typeof(eColor));
            StringBuilder sb = new StringBuilder();
            int count = 0;

            foreach (eColor color in colorValues)
            {
                sb.AppendFormat("{0} ({1}), ", color.ToString(), (int)color);
                count++;
                if (count % 4 == 0)
                {
                    client.Player.Out.SendMessage(sb.ToString().TrimEnd(',', ' '), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    sb.Clear();
                }
            }
            if (sb.Length > 0) client.Player.Out.SendMessage(sb.ToString().TrimEnd(',', ' '), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            client.Player.Out.SendMessage("----------------------------", eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        private ushort ParseColor(string input)
        {
            if (ushort.TryParse(input, out ushort colorId)) return colorId;
            try { return (ushort)(byte)Enum.Parse(typeof(eColor), input, true); }
            catch { return 999; } // 999 acts as an invalid flag
        }

        private static ushort GetRandomColor() => validColors[Util.Random(validColors.Length - 1)];

        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length < 2)
            {
                DisplaySyntax(client); return;
            }

            if (args[1].Equals("colors", StringComparison.OrdinalIgnoreCase))
            {
                DisplayColors(client);
                return;
            }

            GameNPC npc = client.Player.TargetObject as GameNPC;
            if (npc == null && !args[1].Equals("save", StringComparison.OrdinalIgnoreCase))
            {
                client.Out.SendMessage("You must target an NPC.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (args[1].Equals("save", StringComparison.OrdinalIgnoreCase))
            {
                if (npc == null) return;
                string tn;
                do { tn = args.Length >= 3 ? args[2] : Guid.NewGuid().ToString(); } 
                while (!npc.Inventory.SaveIntoDatabase(tn));
                
                npc.EquipmentTemplateID = tn;
                npc.SaveIntoDatabase();
                client.Player.Out.SendMessage($"Equipment saved as: {tn}", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // PATTERN / COLOR / MASK / DF PARSER
            string patternStr = "None";
            string cloakCategory = "Regular";
            string maskTypeStr = "Random";
            ushort color = 0;
            bool colorSet = false;
            bool isDF = false;
            eCharacterClass targetClass = eCharacterClass.Unknown;
            string[] validCloakCategories = { "toa", "regular", "guard", "realm", "otherworldly", "special", "class" };

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                ushort parsedColorId;

                bool isNumber = ushort.TryParse(arg, out parsedColorId);
                bool isCloakCategory = args[1].Equals("cloak", StringComparison.OrdinalIgnoreCase) && validCloakCategories.Contains(arg.ToLower());

                if (arg.Equals("DF", StringComparison.OrdinalIgnoreCase)) isDF = true;
                else if (isCloakCategory) cloakCategory = arg;
                else if (!isNumber && Enum.TryParse(arg, true, out PatternType pType) && pType != PatternType.None) patternStr = pType.ToString();
                else if (!isNumber && Enum.TryParse(arg, true, out eMaskType mType)) maskTypeStr = mType.ToString();
                else if (!isNumber && Enum.TryParse(arg, true, out eCharacterClass cClass)) targetClass = cClass;
                else if (arg.StartsWith("Class", StringComparison.OrdinalIgnoreCase)) patternStr = arg;
                else
                {
                    ushort c = ParseColor(arg);
                    if (c != 999 || arg == "0" || arg.Equals("White", StringComparison.OrdinalIgnoreCase)) { color = c; colorSet = true; }
                    else if (i > 1 && args[1].Equals("cloak", StringComparison.OrdinalIgnoreCase)) cloakCategory = arg;
                }
            }

            if (!colorSet) color = GetRandomColor();

            GameNpcInventoryTemplate template = npc?.Inventory as GameNpcInventoryTemplate ?? new GameNpcInventoryTemplate();
            eRealm npcRealm = npc!.Realm == eRealm.None ? eRealm.Albion : npc.Realm;
            int level = npc.Level > 0 ? npc.Level : 50;

            if (args[1].Equals("mask", StringComparison.OrdinalIgnoreCase))
            {
                if (maskTypeStr.Equals("Random", StringComparison.OrdinalIgnoreCase) || maskTypeStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                    maskTypeStr = ItemModelManager.GetRandomMaskType();

                template.RemoveNPCEquipment(eInventorySlot.HeadArmor);

                int maskModel = ItemModelManager.GetMaskModel(maskTypeStr, npc.Model);
                int effect = ItemModelManager.GetMaskEffect(maskTypeStr);

                template.AddNPCEquipment(eInventorySlot.HeadArmor, (ushort)maskModel, color, effect);
                npc.Inventory = template;
                npc.BroadcastLivingEquipmentUpdate();
                return;
            }

            if (args[1].Equals("weapon", StringComparison.OrdinalIgnoreCase))
            {
                if (targetClass == eCharacterClass.Unknown)
                {
                    client.Out.SendMessage("Invalid class name.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                string wTemplate = "epic"; // default
                if (args.Any(a => a.Equals("epicdf", StringComparison.OrdinalIgnoreCase))) wTemplate = "epicdf";
                else if (args.Any(a => a.Equals("classic", StringComparison.OrdinalIgnoreCase))) wTemplate = "classic";

                // Clean the weapon slots first to avoid overlap issues
                template.RemoveNPCEquipment(eInventorySlot.RightHandWeapon);
                template.RemoveNPCEquipment(eInventorySlot.LeftHandWeapon);
                template.RemoveNPCEquipment(eInventorySlot.TwoHandWeapon);
                template.RemoveNPCEquipment(eInventorySlot.DistanceWeapon);

                ItemModelManager.EquipClassWeapons(npc, template, targetClass, wTemplate, color);

                npc.Inventory = template;
                npc.BroadcastLivingEquipmentUpdate();
                return;
            }

            if (args[1].Equals("epic", StringComparison.OrdinalIgnoreCase) && args.Length >= 3)
            {
                Clear(npc);
                template = new GameNpcInventoryTemplate();
                targetClass = eCharacterClass.Unknown;

                foreach (string arg in args)
                {
                    if (Enum.TryParse(arg, true, out eCharacterClass parsedClass)) { targetClass = parsedClass; break; }
                }

                if (targetClass != eCharacterClass.Unknown)
                {
                    // 1. Equip Armor (Slots 0 to 5)
                    for (int i = 0; i < 6; i++)
                    {
                        int aModel = ItemModelManager.GetClassEpicArmor(targetClass, (eInventorySlot)slots[i]);
                        int headEffect = 0;

                        if (aModel <= 0)
                        {
                            eObjectType baseMat = ItemModelManager.GetDefaultArmorMaterialForClass(targetClass, level);
                            ItemModelManager.GetArmorData(baseMat, (eInventorySlot)slots[i], level, npcRealm, "None", npc.Model, out aModel, out _, out _, out headEffect);
                        }

                        if (aModel > 0) template.AddNPCEquipment((eInventorySlot)slots[i], (ushort)aModel, color, headEffect);
                    }

                    // 2. Equip Weapon
                    ItemModelManager.EquipClassWeapons(npc, template, targetClass, isDF ? "epicdf" : "epic", color);

                    // 3. Equip Cloak
                    int cloakModel = ItemModelManager.GetCloakModel("class", targetClass);
                    if (cloakModel <= 0) cloakModel = ItemModelManager.GetCloakModel("Regular");
                    template.AddNPCEquipment(eInventorySlot.Cloak, (ushort)cloakModel, color, 0);
                }
                else
                {
                    client.Out.SendMessage($"Invalid class name.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
                
                npc.Inventory = template;
                npc.BroadcastLivingEquipmentUpdate();
                return;
            }

            // CLOAK COMMAND HANDLING
            if (args[1].Equals("cloak", StringComparison.OrdinalIgnoreCase))
            {
                template.RemoveNPCEquipment(eInventorySlot.Cloak);
                int clkModel = ItemModelManager.GetCloakModel(cloakCategory);
                template.AddNPCEquipment(eInventorySlot.Cloak, (ushort)clkModel, color, 0);
                npc.Inventory = template;
                npc.BroadcastLivingEquipmentUpdate();
                return;
            }

            if (args[1].Equals("equip", StringComparison.OrdinalIgnoreCase) && args.Length >= 3)
            {
                byte slotb = Convert.ToByte(Convert.ToUInt16(args[2]));
                int sloti = Array.IndexOf(slots, slotb);
                if (sloti >= 7) {
                    template.RemoveNPCEquipment((eInventorySlot)slotb);
                    ushort wpnModel = (ushort)ItemModelManager.GetRandomWeaponModelForNgg(sloti, npcRealm);
                    if (wpnModel > 0) template.AddNPCEquipment((eInventorySlot)slotb, wpnModel, color, 0);
                    
                    if (sloti == 7 || sloti == 8) npc.SwitchWeapon(GameLiving.eActiveWeaponSlot.Standard);
                    else if (sloti == 9) npc.SwitchWeapon(GameLiving.eActiveWeaponSlot.TwoHanded);
                    else if (sloti == 10) npc.SwitchWeapon(GameLiving.eActiveWeaponSlot.Distance);
                }
                npc.Inventory = template;
                npc.BroadcastLivingEquipmentUpdate();
                return;
            }

            eObjectType objType = eObjectType.GenericArmor;
            bool isRandom = false;

            switch (args[1].ToLower())
            {
                case "cloth": objType = eObjectType.Cloth; break;
                case "leather": objType = eObjectType.Leather; break;
                case "studded": objType = eObjectType.Studded; break;
                case "chain": objType = eObjectType.Chain; break;
                case "scale": objType = eObjectType.Scale; break;
                case "plate": objType = eObjectType.Plate; break;
                case "random": objType = eObjectType.GenericArmor; isRandom = true; break;
                default: DisplaySyntax(client); return;
            }

            Clear(npc);
            template = new GameNpcInventoryTemplate();

            for (int i = 0; i < 6; i++)
            {
                eObjectType currentType = objType;
                if (currentType == eObjectType.GenericArmor)
                {
                    eObjectType[] randomMats = { eObjectType.Cloth, eObjectType.Leather, eObjectType.Studded, eObjectType.Chain, eObjectType.Scale, eObjectType.Plate };
                    currentType = randomMats[Util.Random(randomMats.Length - 1)];
                }

                ItemModelManager.GetArmorData(currentType, (eInventorySlot)slots[i], level, npcRealm, patternStr, npc.Model, out int model, out _, out bool ext, out int effect);

                if (model > 0)
                {
                    template.AddNPCEquipment((eInventorySlot)slots[i], (ushort)model, color, effect);
                    if (ext) template.GetItem((eInventorySlot)slots[i]).Extension = 5; // Give visual extension
                }
            }

            if (Util.Chance(50))
            {
                template.AddNPCEquipment(eInventorySlot.Cloak, (ushort)ItemModelManager.GetCloakModel("Regular"), GetRandomColor(), 0);
            }

            if (isRandom)
            {
                // Assigns random weapon set with classic template
                eCharacterClass[] validClasses = {
                    eCharacterClass.Armsman, eCharacterClass.Mercenary, eCharacterClass.Paladin, eCharacterClass.Cleric, eCharacterClass.Sorcerer, eCharacterClass.Minstrel, eCharacterClass.Theurgist, eCharacterClass.Cabalist, eCharacterClass.Infiltrator, eCharacterClass.Scout, eCharacterClass.Wizard, eCharacterClass.Heretic, eCharacterClass.MaulerAlb,
                    eCharacterClass.Warrior, eCharacterClass.Shadowblade, eCharacterClass.Skald, eCharacterClass.Berserker, eCharacterClass.Savage, eCharacterClass.Thane, eCharacterClass.Healer, eCharacterClass.Shaman, eCharacterClass.Hunter, eCharacterClass.Runemaster, eCharacterClass.Bonedancer, eCharacterClass.Spiritmaster, eCharacterClass.Warlock, eCharacterClass.Valkyrie, eCharacterClass.MaulerMid,
                    eCharacterClass.Hero, eCharacterClass.Blademaster, eCharacterClass.Champion, eCharacterClass.Warden, eCharacterClass.Druid, eCharacterClass.Bard, eCharacterClass.Nightshade, eCharacterClass.Ranger, eCharacterClass.Eldritch, eCharacterClass.Enchanter, eCharacterClass.Mentalist, eCharacterClass.Animist, eCharacterClass.Valewalker, eCharacterClass.Vampiir, eCharacterClass.Bainshee, eCharacterClass.MaulerHib
                };
                eCharacterClass randomClass = validClasses[Util.Random(validClasses.Length - 1)];
                ItemModelManager.EquipClassWeapons(npc, template, randomClass, "classic", color);
            }
            else
            {
                npc.Inventory = template;
            }

            npc.BroadcastLivingEquipmentUpdate();
        }

        private void Clear(GameNPC target)
        {
            if (target != null) { target.Inventory = null; target.EquipmentTemplateID = null; }
        }
    }
}